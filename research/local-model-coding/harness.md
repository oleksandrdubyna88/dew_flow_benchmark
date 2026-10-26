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
| [make-tables.mjs](harness/make-tables.mjs) | [tables.md](tables.md), from [data/](data/) only. |

## 7. Re-running

```bash
# on the machine: vLLM up (C:\Users\strug\vllm\start-vllm.ps1 … -MaxNumSeqs 4 -NoMtp …), a prepared base clone
./prep-base.sh                         # build + test the start commit once (the baseline failures)
./run-all.sh V C C2                    # detached; again after any crash — it resumes
./after-three.sh                       # VT, the checks, the group judge, the metrics
./after-c34.sh; ./after-q.sh; ./after-h.sh
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
