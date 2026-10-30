# Harness — how the experiment ran, and how to run it again

> The scripts in [harness/](harness/) (indexed in [harness/README.md](harness/README.md)) are the ones that ran, copied as they were at the end (with the fixes listed in §8).
> They expect the layout of the machine that ran them (`/home/jinx/git/coai-exp`, WSL2 on Windows 11, vLLM in `wslc`);
> the paths are constants at the top of each script. [make-tables.mjs](harness/make-tables.mjs) is the exception: it
> reads only [data/](data/) and runs anywhere.

## 1. The subject

- Repository: `dew_flow_connect_other_ais` (coai). Task: stories **E5.5** (`--check-consultant` for Kimi, 4 scenarios) and
  **E5.6** (the question consultant's Kimi row, 8 scenarios) of `todo/PLAN_vibe_and_kimi_runtimes_E5.md` — the next code of
  that plan, whose scenarios and brief an Opus judge wrote on 2026-10-16.
- Start commit for every arm: **`b4e2ea96`** (`feat/kimi-consultant` before its WIP commit). The WIP versions of these
  stories (`a90fee9b`, draft PR #929) were not reachable from any arm's clone (checked with `git cat-file`).
- Every arm's result is a branch in that repository: `exp/kimi-e56-{classic,classic-2,classic-3,classic-4,vllm,vllm-think,
  skeleton-1..4,skeleton-haiku-1..4}`. None of them is meant to merge.

## 2. The arms and their pipelines

| Arms | Pipeline (step: model, limit) |
|---|---|
| C, C2, C3, C4 (classic) | 1 code: Sonnet, 75 min → 2 review: Opus, 20 min → 3 fixes: Sonnet, 30 min |
| V (vLLM drafts, thinking off by the coder's choice), VT (thinking forced on) | the same, but in step 1 every new method is first drafted by the local model through [ask.mjs](harness/ask.mjs) |
| Q1–Q4 (skeleton + Qwen fill) | 1 skeleton: Sonnet, 30 min → 2a fill tests: script + Qwen, 20 min → 2b fill production: script + Qwen, 20 min → 3 polish: Sonnet, 40 min → 4 review: Opus, 20 min → 5 fixes: Sonnet, 30 min |
| H1–H4 (skeleton + Haiku fill) | the Q pipeline, with Haiku as the filler through [ask-haiku.mjs](harness/ask-haiku.mjs) |
| T1–T4 (Opus tasks, Sonnet per task) | 1 split: Opus, 25 min (+ 1b one fix round of the task list: 10 min, if the checker refuses it) → per task, in batches of up to 4 parallel worktrees: 2 code: Haiku, 20 min → 3 task review: Sonnet, 15 min → cherry-picked back → 4 review: Opus, 20 min → 5 fixes: Sonnet, 30 min ([t-lib.sh](harness/t-lib.sh)) |
| O1–O4, S1–S4 (part 5) | the T pipeline with no step 3, and Haiku in step 5 (`h-fix`); the split by Opus (O) or Sonnet (S) from `x-split` / `x-split-fix` |
| D1–D4 (part 5) | 1 code: Haiku, 75 min (`d-code`, the classic coder brief naming Haiku) → 2 review: Opus, 20 min → 3 fixes: Haiku, 30 min (`h-fix`) |

The briefs are in [harness/briefs/](harness/briefs/). The step-1 briefs of the classic and vLLM arms differ by one
paragraph (`arm-C.md` / `arm-V.md` / `arm-VT.md`, read back and diffed before the start). The skeleton and polish briefs
(`q1-skeleton.template.md`, `q3-polish.template.md`) are GENERATED from the classic coder brief, so their shared parts are
word for word the same; the review and fix briefs are one file for every arm.

## 3. Isolation and equal conditions

- One clone per arm, made from one prepared base clone (`--single-branch`, `--no-tags`, no remote), copied with its build
  outputs and `node_modules`, then built once more before its start (not timed).
- Each step is ONE headless `claude -p` session in the arm's clone, `--output-format stream-json --verbose`, model by alias
  (`sonnet`, `opus`), effort `high` from the user settings. Its stream is kept per attempt; the `result` event carries the
  host's own duration, cost and tokens per model.
- Headless Claude ignores the project's `permissions.allow` in an untrusted folder, so the harness's `--allowedTools` list
  (the same for every arm) is the real permission set. The reviewer and the judges add `--disallowedTools Edit Write
  NotebookEdit`.
- Tests and builds run lowered and capped (`low-priority.mjs`, `-m:4`, 8 test threads), per the shared CPU rule.
- No arm saw another arm, the WIP branch, or the experiment's notes; commit messages and code were not allowed to name
  the local model, and the judges' diffs are redacted for it (the count is recorded: 0 in every arm).

## 4. Resume after a crash

The machine crashed three times during part 1, so the harness was made resumable before the run continued
([run-arm.sh](harness/run-arm.sh), [run-all.sh](harness/run-all.sh)):

- A finished step writes `<step>.done`; a rerun skips it.
- Each Claude step gets a fixed session id BEFORE its first attempt (`--session-id`); an interrupted step is RESUMED
  (`--resume`) with the time left of its budget (budget − the time its earlier attempts used, read from their stream's
  last write), and told to check `git status` / `git log` first.
- A step whose last stream already holds a `result` event but has no `.done` (its runner died after it finished) is
  marked done, NOT resumed — added after arm C was wrongly resumed (see [incidents.md](incidents.md)).
- Each attempt is timed on its own, so downtime is never counted as work.
- Runners and chains are started with `setsid`, so they do not die with the orchestrating session; the chains
  (`after-three.sh`, `after-c34.sh`, `after-q.sh`, `after-h.sh`) leave a marker per stage and can be started again.
- To pause safely, kill the arm's whole session with SIGKILL (runner first): a polite stop can make Claude write a
  `result` event, which would then read as "finished".

## 5. The fillers

**Local model** ([ask.mjs](harness/ask.mjs)): vLLM 0.29.0 (ROCm) in a `wslc` container on Windows, Qwen3.6-35B-A3B GPTQ
Int4, `--max-num-seqs 4`, 128K context, sampling from the start script (temperature 0.3, top_p 0.9, top_k 20,
repetition_penalty 1.05). vLLM listens on Windows `127.0.0.1` only, so the wrapper calls it through `curl.exe` with the
body on stdin. Up to 4 prompt files at once; one JSON line per request (time, tokens, finish reason, thinking on/off).
On "connection refused / lost" it waits up to 15 min for `/health` and retries (so a coder never writes code itself
during an outage). `COAI_EXP_FORCE_THINK=1` (arm VT) ignores `--no-think` and logs that it did.

**Haiku** ([ask-haiku.mjs](harness/ask-haiku.mjs)): the same interface and log, each prompt sent to
`claude -p --model haiku` with `--tools ""`, a short fixed `--system-prompt`, `--setting-sources user` (no project
rules or hooks; the user settings' effort `high`), `--strict-mcp-config`, `--no-session-persistence`, from an empty
folder. Checked on a toy repo: ~2.1k input tokens per tiny request — the Claude Code system prompt did not load. Each
request's host cost is logged.

**The fill** ([fill.mjs](harness/fill.mjs)) for the skeleton arms:

1. Finds every `// QWEN-FILL-BEGIN <id> <test|prod>` … `// QWEN-FILL-END <id>` region the skeleton left in the files the arm
   changed, and its context pack `vllm/work/<arm>/regions/<id>.md`.
2. Sends, per region: the author's instruction comments, the context pack, and the whole file with the region marked;
   asks for ONLY the code between the markers, one code block. Up to 4 regions at once.
3. **The rail:** the answer replaces only the lines between the markers. An answer that holds a marker, is empty, or has
   unbalanced braces or parentheses is rejected and asked once more; a second rejection leaves the placeholder for the
   polish. (Known limit, seen in Q1 and Q2: a type declared inside a method body passes this check and breaks the build.)
4. Re-indents the answer to the marker, commits the filled files by name, builds, and — if the build passes — runs the test
   classes of the filled files (CTRF report). Writes `fill-<kind>.json`; `fill-state.json` keeps the exact code spliced
   in per region (the survival metric's input). Re-runnable: filled regions are skipped.
5. Two rounds per arm: tests first (`2a`), then production (`2b`), so a test can be seen red before production exists.
   In practice the build failed after every test fill (all 8 skeleton arms), so that red run never happened; the polish
   step checks red against the code at `b4e2ea96`.

## 6. The measurements

| Script | What it computes |
|---|---|
| [metrics.mjs](harness/metrics.mjs) | Time, cost and tokens per arm, step, attempt and model. A finished session: the `result` event. An interrupted one: its session transcript inside the attempt's time window — NOT the stream, whose assistant events carry only a message's starting usage (checked: 955 output tokens in the stream against 44 090 in the host's total for the same session; cache tokens were exact). Its cost is then estimated with unit prices fitted by least squares on the sessions that have a host cost (output, cache read, cache write + input); the fit is checked against those same sessions before use (0.0 % error for Sonnet on 33, Opus on 11). Fill steps: the filler requests inside the step's window (Haiku's host cost; $0 for the local model). |
| [objective-checks.sh](harness/objective-checks.sh) | On the final commit: build; the whole MCP suite (CTRF report); the TS suite (includes the generated-files check); `dotnet format` whitespace + style on the C# files the arm changed (the whole solution fails on analyzer errors in `src_bugs`, which no arm touched); the generator's `--check`; which of the 12 scenarios exist (S5.6.2 is the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`) and pass; leftover fill markers, and placeholders in ADDED lines only (the start commit has its own `throw new Error('boom')`). Known failures before any change (2 MCP, 1 TS) and tests shown flaky on this host are listed apart, never as the arm's. |
| [survival.mjs](harness/survival.mjs) | For each filled region: its meaningful lines (not only braces or punctuation) still present after the polish and in the final commit. Main measure: whitespace-insensitive, in ANY file the arm changed (a polish moved a filled class to another file unchanged). Strict: the same file, the exact trimmed line — a lower bound. Inlined or edited lines count as dropped. Hand-checked on 10 regions before use. |
| [compare.mjs](harness/compare.mjs) | The classic band (C, C2, C3, C4: min / mean / max) and every arm against it. |
| [judge.sh](harness/judge.sh) | The GROUP judge of part 1: one Opus session sees V, C, C2 and VT together under random labels, scores 0–10 on correctness, test honesty, conventions, simplicity, ranks them; 2 runs, every arm on a different label in run 2. |
| [judge-abs.sh](harness/judge-abs.sh) | The ABSOLUTE judge: each diff scored ALONE by its own Opus session against an anchored rubric (10 = nothing a careful reviewer would flag · 8–9 minor only · 6–7 one major or many minor · 4–5 several major or one blocking · 0–3 not delivered), 2 runs per arm with fresh opaque labels, `research/module_*.md` declared out of scope. Incremental: new arms get sessions, old ones keep theirs. |
| [t-survival.mjs](harness/t-survival.mjs) | The task arms: Haiku's meaningful added lines per task (from the per-task diffs) still present in the final commit, whitespace-insensitive, in any changed file; and how many lines the Sonnet task review changed (T only). |
| [mutate.mjs](harness/mutate.mjs) | The mutation checks (part 5, every arm): six frozen mutants, each breaking one spec decision in a clean worktree of the arm's final commit; the test project rebuilt per mutant; the arm's scenario classes run against an unmutated baseline. A mutant is killed if any test turns red; per intended scenario test, caught or missed; an anchor not found or a build failure is "not applicable", never "caught". Validated on T3 (a known vacuous test, reported) and C2 before any part 5 result. |
| `fix-outcomes.json` (post hoc) | Per review finding, what the fixer did, from the fixer's note only — one Opus session, hand-checked on two arms. |
| [make-tables.mjs](harness/make-tables.mjs) | [tables.md](tables.md), from [data/](data/) only. |

## 7. Re-running

```bash
# on the machine: vLLM up (C:\Users\strug\vllm\start-vllm.ps1 … -MaxNumSeqs 4 -NoMtp …), a prepared base clone
./prep-base.sh                         # build + test the start commit once (the baseline failures)
./run-all.sh V C C2                    # detached; again after any crash — it resumes
./after-three.sh                       # VT, the checks, the group judge, the metrics
./after-c34.sh; ./after-q.sh; ./after-h.sh; ./after-t.sh; ./after-x.sh   # x = part 5, rounds of O, D, S
./mut-all.sh & ./mut-awake.sh &        # the mutation checks on every arm (skips an arm already checked)
node metrics.mjs --json > metrics.json; node compare.mjs
# anywhere, from this folder:
node harness/make-tables.mjs
```

## 8. Harness defects found during the run, and fixed

| Found | Defect | Fix |
|---|---|---|
| Warm-up | A 54-token prompt made ~8 500 thinking tokens | `--no-think` allowed per request and logged; VT pins thinking on |
| After crash 1 | Runners died with the orchestrating session | `setsid` detached runners and chains |
| Resume | A finished session (its `result` written) was resumed because its runner died before `.done` (arm C: +14 min, +$0.43) | "finished, runner died" → marked done, not resumed |
| Pause | A `pkill -f` matched the command's own shell | match the exact command start |
| Metrics | Stream output tokens undercounted ~45× for interrupted attempts | read the transcript |
| Metrics | Fill steps marked "estimated"; Haiku requests entered the price fit and broke it | script steps carry their own cost; only Claude sessions are fitted |
| Checks | `format` failed on code no arm touched; two scenario detectors wrong (one pattern with a quote broke the shell); placeholders found in the start commit | format on changed files; fixed detectors; added lines only |
| Checks | One flaky test (fails at the start commit too) counted as new; one deadline test failed once on a saturated machine | a flaky list; the deadline test re-run at both priorities (7/7) and moved to environment |
| Survival | Code moved to another file counted as dropped | whitespace-insensitive, any changed file; strict kept as lower bound |
| Judge | The group judge read uncorrected objective files (v1); its brief did not put `research/module_*.md` out of scope | v2 on corrected files; the absolute judge's brief declares the scope |
| Judge | Q4's absolute sessions had copied its objective file before a correction | the unstarted one corrected; the finished one re-judged (old verdict kept as `verdict.v1.json`) |
| Polish brief | Told the polish to check red against a placeholder (whose `NotImplementedException` is not the tables' reason) | check red against `b4e2ea96` |

## 9. Part 6 — what differs (a second task; scripts in [harness/part6/](harness/part6/))

Part 6 ran in its own folder, `/home/jinx/git/coai-exp2` (the constant `E` at the top of each script), with its own copies of the scripts. They are in
[harness/part6/](harness/part6/), as they ended; absolute home paths are left as they were. The fixed facts of the experiment (base commit, plan path,
branch name, scenario count, the known-failures sentence) are in `exp.env`; the 43 scenarios with their test names are in `scenarios.json`; the failures
known at the base (none) are in `baseline.json`. The design as written before the run is [harness/part6/PROTOCOL.md](harness/part6/PROTOCOL.md).

### 9.1 The pipelines

| Scheme | Steps (model, budget) — `run-arm.sh`, with `t-lib.sh` for SRS |
|---|---|
| C1–C3 | `1-code` Sonnet 180 min → `2-review` Opus 40 (read-only) → `3-fix` Sonnet 60 → `4-gate` Sonnet 150 |
| CR1–CR3 | `1-code` Sonnet 180 → `2-hreview` Haiku 60 → `3-review` Opus 40 → `4-fix` Sonnet 60 → `5-gate` Sonnet 150 |
| SRS1–SRS3 | `1-split` Sonnet 40 (+ `1b-split-fix` 15 min, once, if `tasks-plan.mjs` refuses the list; no SRS arm needed it) → `2-tN-code` Haiku 45 each (batches of up to 4 tasks in parallel git worktrees; their commits are cherry-picked back; a conflict stops the arm) → `3-hreview` Haiku 60 → `4-review` Opus 40 → `5-fix` Sonnet 60 → `6-gate` Sonnet 150 |

`tasks-plan.mjs` checks the Sonnet split: 4 to 7 tasks, one owner for each file, no cycles in the dependencies, the task that owns the plan file depends on all the others (it comes last), every
scenario assigned to a task; it computes the batches (every task whose dependencies are done, at most 4 at a time). The briefs are in
[harness/part6/briefs/](harness/part6/briefs/): `1-code`, `2-review`, `3-fix`, `hr-review-fix` (the Haiku review-and-fix), `x-split` and `x-split-fix`, `t2-coder`,
`gate`, and the two judges. A brief is filled from a template with the deadline, the notes path, the plan and the base; no arm id and no scheme name
ever reaches a brief (a field that stays unfilled stops the runner).

### 9.2 Isolation (different from parts 5b–5c)

- **A random folder per arm.** `r/<token>/` holds the clone (`arm/`), `notes/`, `work/`, `out/` (streams, prompts, timing), `tmp/` (the arm's own `TMPDIR`, so Claude
  Code's temp folder under `/tmp` does not name another arm), the arm's coai data folder, and the SRS worktrees (`wt/`). The token-to-arm map is
  [data/part6/arm-tokens.json](data/part6/arm-tokens.json) (Part 7: [data/part7/arm-tokens.json](data/part7/arm-tokens.json)). Every arm uses the same branch name, `feat/quota-cooldown`.
- **Other work is closed while arms run** (`lock.sh`): the old experiment mode 000; the experiment root, `r/`, `pids/` and the four Claude Code state folders
  (`projects`, `file-history`, `session-env`, `shell-snapshots`) mode 300, which lets the owner enter by name but not list. The modes are saved and restored by `lock.sh unlock`.
  The arms run as the same user, and `chmod` is not in their tools.
- **A deny list** on every step: `Read` of `~/.claude`, `~/.codex`, `~/.gemini`, `~/.antigravity`, `~/.vibe`, `/proc`, the old experiment and `/mnt/v`.
- **`--strict-mcp-config`** on every step: the coder steps get an empty MCP list; the gate step gets only the arm's own coai server, with its own data folder (a copy of the
  operator's `settings.json`: the same providers and switches). The gate step may call `providers`, `open`, `review_plan`, `review_code`, `resolve` and `status` only
  (not `consult`, not `ask_*`: a question row can read every repository).
- **The gate runs one arm at a time** (`flock` on `.gate.lock`), to spare the providers' quotas. The wait for the lock is not counted against the step's budget.
- **After the run** every stream is scanned for paths of another arm ([incidents.md](incidents.md) §2g).

### 9.3 Resume, limits and the detector

As in §4, with these changes. A step whose last result is an **error is never "done"**: the runner stops and a person looks. A **limit** is read from the structured fields first
(`api_error_status` 429 or 529, `api_error`), then from the words; a limited step waits 15 minutes and resumes its own session (up to 40 times), and the wait is not counted. A gate step
that writes `"status": "blocked"` in `gate.json` waits 30 minutes and resumes (up to 8 times). The first version of the detector read only words and missed the
wording of 2026-10-28 ([incidents.md](incidents.md) §2g); `repair-limit-2026-10-28.sh` undid what it caused. The copy of `run-arm.sh` here is the fixed one.
Each attempt is timed alone (`timing.jsonl`), so waits are not work. The step command is `timeout -s INT -k 60 <time left + 10 min>`: a step may run 10 minutes past its budget.

### 9.4 The measures

| Script | What it computes |
|---|---|
| [objective-checks.sh](harness/part6/objective-checks.sh) | On the final commit: the build; the whole MCP suite (CTRF report); the extension suite; `dotnet format` whitespace and style on the changed C# files; which of the 43 scenarios exist (the test name occurs in the sources) and pass (a test of that name, as a whole word, passed in the report); failures against `baseline.json` (none known); the diff by kind. Writes `objective.json`. |
| [metrics6.mjs](harness/part6/metrics6.mjs) | Per arm and step: cost by model from the `result` event of the LAST attempt of each session, summed over the sessions of a step (a resumed attempt reports its session's running total, so adding every attempt over-counts; corrected 2026-10-30, [incidents.md](incidents.md) §2h); active time = the sum of the attempts' wall time from `timing.jsonl` (waits between attempts not counted); attempts; the arm's span (first start to arm-done, waits included). A model other than the step's own shows as a subagent. **Not counted:** the cost of the coai providers (other vendors). |
| [judge-abs.sh](harness/part6/judge-abs.sh) | The absolute judge: one Opus session per diff and run (2 per arm), random labels, the diff redacted for words that name a scheme or a model (counted in `key.json`), 4 sessions at a time; four criteria and an overall score, anchored (10 nothing to flag · 8–9 minor only · 6–7 one major or many minor · 4–5 several major or one blocking · 0–3 not delivered). `research/module_*.md` and `RESULTS_*.md` are out of scope. |
| [coai-findings.mjs](harness/part6/coai-findings.mjs) | Every coai finding of each arm's gate step, from the gate session's own stream: the `review_plan` and `review_code` replies joined with the agent's `resolve` decisions. Writes `coai-findings.json`. |
| [coai-cost.mjs](harness/part6/coai-cost.mjs) | The token counts (`cost.tokensIn`, `cost.tokensOut`) that the coai server reported in each plan and code round reply of each gate stream. Tokens, not dollars; one pair per round, not per provider. Writes `coai-cost.json`. |
| [judge-coai.sh](harness/part6/judge-coai.sh) | One Opus session per arm judges every code finding (real, partly, not real; severity; usefulness) by opening the code at the commit that round reviewed, and scores the set 1–10 and names what it missed. The gate agent's decisions and the providers are not shown. The session works in the arm's clone (not redacted). 3 at a time. |
| [mutate6.mjs](harness/part6/mutate6.mjs) | Eight frozen mutants, each one edit in the arm's final code (the anchors per arm are in the file); a shared clone of the arm's head; the test project built and the arm's test classes run (the classes the arm changed, the classes with a scenario test, and the classes of the area) against an unmutated baseline and then against each mutant. Killed = a test that passed in the baseline fails and fails again on a re-run; a mutant that does not apply or does not build is "not applicable", never "killed". 4 test threads per arm, two arms at a time. |
| [make-tables6.mjs](harness/part6/make-tables6.mjs) | [tables.md](tables.md) §10, from [data/part6/](data/part6/) only (called by `make-tables.mjs`). |
| [make-tables7.mjs](harness/part6/make-tables7.mjs) | [tables.md](tables.md) §11 (Part 7), from [data/part7/](data/part7/) and `data/part6/metrics6.json` (called by `make-tables.mjs`). |

### 9.5 Re-running

```bash
# on the machine (WSL2): a prepared base clone and the arms' coai binary and settings (exp.env)
./prep-base.sh                 # build + test the base once (baseline.json)
./after6.sh                    # makes the arms, locks, runs the rounds of three, unlocks, runs the objective checks (markers in chain/)
./judge-abs.sh; node coai-findings.mjs; ./judge-coai.sh; node metrics6.mjs <map.json>; node mutate6.mjs all
# anywhere, from the repository root:
node research/local-model-coding/harness/make-tables.mjs
```

### 9.6 Harness defects found in Part 6

| Found | Defect | Handling |
|---|---|---|
| 2026-10-29 (after 2026-10-28 21:45 UTC) | The limit detector read words only and missed "You've hit your individual spend limit … session limit resets …" (`api_error_status` 429); steps that ended in it were marked done | Structured fields first; an error result is never done; the damage repaired by `repair-limit-2026-10-28.sh` |
| 2026-10-29 | `Agent` was in the tool list of every step that can write, so a Haiku step could start subagents on other models (SRS3's review-and-fix did) | Not changed during the run; the result is marked contaminated. A re-run should remove `Agent` from the write tools |
| 2026-10-29 | The per-arm `TMPDIR` made a Unix-socket path longer than 108 characters, so one suite test failed inside the arms (not in the objective checks) | Reported by the arms as environmental; no change |
| 2026-10-29 | The scenario pass check matches the exact name as a whole word, so a theory whose method name is longer (C1's S14) reads "absent" though it passed | Not changed; the mutation check found the test by its longer name; reported in [results.md](results.md) §7d |
| 2026-10-29 | The `limited` field of `timing.jsonl` for the 14 attempts of round 1 reads false (written by the old detector) | Not changed; the repair script does not use it |
| 2026-10-30 | `metrics6.mjs` summed the result of every attempt, but a resumed attempt reports the session's running total, so every resumed step was over-counted (Part 6: C1 $19.92 instead of $16.47, CR1 $16.27 instead of $15.52, SRS1 $14.85 instead of $14.64; Part 7: CHH1 $44.92 instead of $23.52) | Fixed: per session, the last attempt's result; the first output kept as `data/part6/metrics6.v1-summed-attempts.json`; every Part 6 cost recomputed ([incidents.md](incidents.md) §2h). `harness/metrics.mjs` (parts 1–5c) was checked: its four resumed attempts do not carry a running total, so it is not affected |

### 9.7 Part 7 (CHH) — what differs from Part 6

| Scheme | Steps (model, budget) — `run-arm.sh`, case `CHH[0-9]*` |
|---|---|
| CHH1–CHH3 | `1-code` Sonnet 180 → `2-hA` Haiku 40, read-only (brief `hA-trace`; its final JSON goes to `notes/hA.json`) → `3-hB` Haiku 40, read-only (brief `hB-tests`; `notes/hB.json`) → `4-hfix` Sonnet 60 (brief `hfix`, which reads both JSON files; `notes/hfix.md`; its diff is kept) → `5-review` Opus 40 → `6-fix` Sonnet 60 → `7-gate` Sonnet 150 |

- **Read steps start no subagent.** `--disallowedTools` for read-mode steps now lists `Agent` and `Task`, next to `Edit Write NotebookEdit` (Part 6's SRS3 Haiku step started Opus and Sonnet subagents).
  A search of every CHH stream for those tool names finds none, and no CHH step shows another model's cost in `metrics6.json`.
- **`review_json` takes a destination**, so the same function keeps the Opus review (`notes/review.json`) and the two Haiku reviews (`notes/hA.json`, `notes/hB.json`).
- **`lock.sh` has `EXTRA_CLOSED`**: a space-separated list of paths that are also set to mode 000 and restored by `unlock`. `after7.sh` fills it with every Part 6 result (the nine arm folders, `judge-abs`,
  `judge-coai`, `mut6`, `coai-findings.json`, `metrics6.json`, the logs, the protocol) and the write-up worktree, so that no CHH arm could read them. It also makes the three arms, runs them at the same
  time (no rounds), unlocks, and runs the objective checks (markers in `chain7/`).
- **The briefs** are in [harness/part6/briefs/](harness/part6/briefs/): `hA-trace`, `hB-tests`, `hfix`. Their checklists describe the defect kinds that Part 6 left in the code (an inherited default, text over several lines, an order of results, a time
  zone that the runtime ignores, a Unix socket path over 108 characters); they name no defect and no file. The hindsight bias that follows is stated in [results.md](results.md) §7e.
- **Everything else is Part 6's:** the objective checks, `judge-abs.sh` and `judge-coai.sh` (they take every arm of the map that has no session yet), `coai-findings.mjs`, `metrics6.mjs` (it knows the step kinds `hA`, `hB` and `hfix`) and
  `mutate6.mjs`, which got the anchors for the CHH arms (the same eight mutants) before any CHH result. A mutation run is not repeated by the script; CHH3's was repeated by hand after the SDK upgrade
  ([incidents.md](incidents.md) §2i).
- **The trust warning.** Every step of every Part 6 and Part 7 arm printed "Ignoring 5 permissions.allow entries … this workspace has not been trusted" (111 of 111 stderr files). The runner's `--allowedTools` is therefore the permission set of
  every step, as in §3 of this file.
