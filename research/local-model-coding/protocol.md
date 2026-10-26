# Protocol — local model and Haiku as code fillers (verbatim record)

> This is the experiment's protocol exactly as it was kept during the run (`coai-exp/PROTOCOL.md` on the machine that
> ran it): the design and the predictions written BEFORE each part ran, and every later change appended with its time,
> never edited in place. Read it as the log; the summary is [README.md](README.md), the numbers [results.md](results.md).


Written 2026-10-25, BEFORE any arm ran. Changes after the start are added at the end, with the time, and never edited in place.

## 1. Question

On one real story of this project, does a Sonnet coder that drafts the code through the local vLLM (Qwen3.6-35B-A3B, up to
4 parallel requests) and then finishes it give a different **time**, **cost** (total and per agent), **quality** and
**token count** than a Sonnet coder that writes the code itself? Everything after the coding step is the same classic
scheme in both arms: Opus review, then Sonnet fixes.

## 2. The task (the same for every arm)

Stories **E5.5** (`--check-consultant` for Kimi, 4 scenarios) and **E5.6** (the question consultant's Kimi row, 8 scenarios)
of `todo/PLAN_vibe_and_kimi_runtimes_E5.md` (repo `dew_flow_connect_other_ais`). This is the next code of the plan. Its
scenarios and brief were written by an Opus judge on 2026-10-16, so the rule "no coder without a judge's scenarios and brief"
holds for both arms.

**In scope:** the code, the tests (watched red, then green), `shared/*.json` rows and vectors, the regenerated TS table, the
TS test, `dotnet format`, the generators' `--check`.
**Out of scope:** `research/module_*.md`, RESULTS sections, the live checks (J2), M-K17, the merge with `main`, the release.

**Start commit:** `b4e2ea96` (branch `feat/kimi-consultant` before its WIP commit; E5.1–E5.4 and E5.7 in). The plan's line
anchors were written for this commit. The WIP versions of E5.5/E5.6 (`a90fee9b`, draft #929) and of the E5.4 follow-ups
(`cb9cd304`) are NOT reachable from the arms' clones (checked: `git cat-file` fails in the clone).

## 3. Arms

| Arm | Branch | Step 1 — code | Step 2 — review | Step 3 — fixes |
|---|---|---|---|---|
| **V** (vLLM) | `exp/kimi-e56-vllm` | Sonnet; every new method is drafted by vLLM first, Sonnet integrates, compiles, fixes | Opus | Sonnet (no vLLM) |
| **C** (classic) | `exp/kimi-e56-classic` | Sonnet writes the code itself | Opus | Sonnet |
| **C2** (optional A/A) | `exp/kimi-e56-classic-2` | the same as C | Opus | Sonnet |

**The one variable:** step 1's use of vLLM. The step-1 briefs differ by ONE paragraph (read back and diffed before the start).
Steps 2 and 3 use identical briefs.

## 4. Pinned (the same in every arm)

- The start commit, and an identical warm start: one prepared, built clone (`bin/obj`, `node_modules`) copied per arm.
- Isolation: one clone per arm, no remote, no other branches; the arms cannot see each other or #929.
- Models by alias: `sonnet` for code and fixes, `opus` for the review and the final judge. Effort `high` (user settings).
- Each step is ONE headless `claude -p` session in the arm's clone (`--output-format json`), so the host reports each agent's
  time, cost and tokens. Project hooks and rules load as in a normal session.
- The same tool allow-list. Arm V also may run the vLLM wrapper `vllm/ask.mjs`.
- Deadlines per step: code 75 min, review 20 min, fixes 30 min (≤ 125 min per arm). What exists at a deadline is judged.
- The arms run AT THE SAME TIME (the same API conditions and machine load). Local builds and tests are lowered and capped
  (`low-priority.mjs`, 8 threads), as the rules ask.
- vLLM: 0.29.0 ROCm, `qwen3.6-35b-a3b` GPTQ Int4, `--max-num-seqs 4`, 128K context, thinking ON, `max_tokens` 14000,
  sampling from `start-vllm.ps1` (temperature 0.3, top_p 0.9, top_k 20, repetition_penalty 1.05). One warm-up request before
  the start, not counted.
- No intervention by the orchestrator during a step. Any intervention is logged with its time and reason.

## 5. Metrics

1. **Time:** wall time per step and per arm (start/end UTC, `duration_ms`); vLLM busy time.
2. **Cost:** `total_cost_usd` per session (computed by Claude Code at API list prices), per model (`modelUsage`); per arm;
   vLLM = $0 API (its GPU time is reported).
3. **Quality:**
   - Objective gate per arm: build; `CoaiMcp.Tests` pass/fail/skip; TS tests; `dotnet format --verify-no-changes`;
     generators `--check`; how many of the 12 scenario names exist as tests; CI (`workflow_dispatch`, Windows + Linux).
   - Step-2 findings by severity (before the fixes).
   - A final blind Opus judge: the diffs only, labels X/Y random, run TWICE with the order swapped. Rubric 0–10 for
     correctness against the plan's decisions, test honesty (red for the right reason), conventions, simplicity; plus the
     defects it still finds.
4. **Tokens:** per session input / output / cache read / cache write; vLLM prompt and completion tokens; totals.
5. **Arm V only:** vLLM requests, finish reasons, how much of the vLLM drafts survives in the final diff (hand-checked on
   10 hunks before it is reported), and the coder's notes on compile errors in the drafts.

## 6. Predictions (recorded before the run)

- **Time:** step 1 of V takes 1.2–1.6× the time of C (vLLM is ~28 tok/s per stream, and the drafts need compile fixes).
- **Cost:** V costs 0.8–1.1× C. Most of a Sonnet session's cost is reading context, not writing output, so moving output to
  vLLM saves little.
- **Tokens:** Claude output tokens in V are 20–40 % lower; Claude input tokens are equal or higher; with vLLM tokens, V spends
  more tokens in total.
- **Quality:** V gets equal or more step-2 findings; the blind judge prefers C or rates them equal.

## 7. Limits (what this run cannot settle)

One task and one run per arm. Without C2, the noise between two Sonnet runs is unknown, so a difference under ~25 % in time
or cost is not a result. The task is C# + JSON in this repository; the conclusion is about this task, this vLLM model and
this machine only.

## 8. Changes after the start

Decided before the start (2026-10-25, the owner):
- **GPU lease:** the `dew_flow_rag_qln` daemon does not run, so `dewflow-gpu.mjs` cannot take the card. The owner approved a
  run without the lease (exception to `gpu-lease.md`); vLLM is the only GPU user, and Ollama holds no model.
- **C2 (A/A) is IN.** Three arms: V, C, C2.
- **Keep-awake:** a temporary pwsh process (`SetThreadExecutionState`) keeps Windows and the display awake during the run;
  no power settings are changed.
- **Thinking:** ON by default; the vLLM coder may pass `--no-think` per request, and the wrapper logs it per request. Reason:
  the warm-up made ~8,500 thinking tokens for a 54-token prompt.
- **Baseline on this host (WSL2):** build OK (79 s). `CoaiMcp.Tests` 10,533 total, 10,447 passed, 84 skipped, **2 failed before
  any change** (`KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`,
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`), 6 min 47 s. These two are not counted against any arm.

**Incident, 2026-10-25 18:40:10 local (before any arm started):** Windows stopped with bugcheck 0x154; no dump was written
(volmgr 161). GPU live-kernel reports `0xA1000007` (amdfendr) and `0x193` (`dxgkrnl!ProcessDeadlockThread`) were sent after the
restart. vLLM had been up ~9 min and was generating the warm-up request. Cause not proven. The owner chose to **retry vLLM as
planned** (the same settings). If Windows crashes again during the run, the run stops and the partial data is reported as partial.
- **TS baseline (base clone, no tags):** 6,286 tests, 6,266 passed, 19 skipped, **1 failed before any change**
  (`the baseline names no release that was never tagged` — the clones carry no git tags). Not counted against any arm.
- **Resumable harness** (owner, 2026-10-25): after a crash only the interrupted step continues — it resumes its own Claude
  session (`--session-id` fixed before the first attempt, then `--resume`) with the time left of its budget; finished steps are
  skipped; each attempt is logged on its own, so downtime is not counted as work. Tested: a resumed headless session kept its
  context. The arms run detached (`setsid`), so they do not depend on the orchestrating session.
- **Host note:** `modelUsage` of a headless session also lists `claude-haiku-5-5` (used by the host itself); it is reported
  separately, not hidden.
- **Project settings:** the repository's `.claude/settings.json` allows `Bash` and `Edit`/`Write` as a whole, so the harness's
  `--allowedTools` list adds nothing; the reviewer is kept read-only by `--disallowedTools Edit Write NotebookEdit` and its brief.
  The same in every arm.
- **Start:** 2026-10-25T18:07:51Z — arms V, C, C2 started together by `run-all.sh`.
- **Read-back at the start (one cell per arm):** only V's prompt carries the vLLM paragraph; branches correct; the same deadline
  (19:22 UTC) in all; no placeholder left; every session reports `claude-sonnet-5-5`; the project's rules loader hook ran in all.
  Headless Claude ignores the project's `permissions.allow` (the clones are not "trusted"), so the harness's `--allowedTools`
  list is the real permission set — the same in every arm.
- **Arm VT added (owner, 2026-10-25 ~18:20 UTC):** the same as V, but thinking is ALWAYS on — pinned in the harness
  (`COAI_EXP_FORCE_THINK=1`: the wrapper ignores `--no-think` and logs that it did), and the brief says so. Reason: V's coder
  sent every request with `--no-think` in its first 8 minutes. VT runs AFTER V, C and C2 finish, alone on the GPU as V was, so
  V's measurement is not disturbed; its time of day differs (a limit). Step-1 paragraph differs from V's only in the thinking
  lines and its work folder. The objective checks of V, C and C2 run while VT works (CPU load during VT — recorded).
  The final blind judge sees all four arms (labels W–Z, two runs, every arm on a different label in run 2).
  **Prediction (before VT runs):** VT's step 1 takes 1.3–2× V's time; VT sends fewer vLLM requests than V; more of VT's drafts
  compile without a fix; VT's quality is equal to V's or a little better; VT's Claude cost is within ±20 % of V's.

**Incidents 2 and 3 (2026-10-25):** from 18:18 to 18:28 UTC the system NVMe (Solidigm D7-PS1010 3.84 TB, a data-centre disk,
4Kn, firmware G70YG252, Windows 11 26H2 build 26300.9711 installed the day before) logged ~670 "bad block" events (disk Id 7)
while the three arms built and tested; ~18:30 UTC Windows stopped with bugcheck 0x7A / c000009c (KERNEL_DATA_INPAGE_ERROR);
between 18:31 and 18:48 UTC it stopped again with 0xEF (CRITICAL_PROCESS_DIED) while NOTHING of the experiment ran. The first
incident's cause is therefore re-read as possibly the disk too, not only the GPU — not proven (no SMART media-error data yet).
State at the interruption: all three arms in step 1, ~21–22 min of work each, 3 commits each, pushed to GitHub as
`exp/kimi-e56-*`; the experiment folder and the arms' session transcripts backed up to the owner's NAS (sha256 checked).
The owner chose to **continue the experiment as planned**. Each arm resumes its own step-1 session (`--resume`) with the time
left of its budget; the downtime is not counted. Time of the resume: see the next line.
- **Resume:** 2026-10-25T19:11:04Z — vLLM up and warm; V, C and C2 resumed together by `run-all.sh`.
- **Harness defect found at 20:03 UTC (arm C):** C's step-1 session had FINISHED before the crash (its stream carries the
  `result` event, $2.54), but the crash killed the runner before it wrote `1-code.done`. On the resume the harness therefore
  resumed C's session as "interrupted": C worked 14 more minutes ($0.43), re-ran the suite and made one more commit
  (`2a7f8847`). V's and C2's step-1 sessions really were interrupted (no `result`). So C had extra step-1 time; C is reported
  with and without that attempt. Fixed for later restarts: a step whose last stream has a `result` is marked done, not resumed
  (`run-arm.sh`, swapped atomically; the running VT keeps its old copy).
- **V, C and C2 finished:** C2 19:45, V 19:51, C 19:56 UTC — all well inside their deadlines. VT started 19:58 UTC.
- **Objective checks corrected after a hand check (21:20–21:35 UTC), before any result was reported:**
  (1) `format: false` in every arm came from IL2026/IL3050 analyzer errors in `src_bugs`, which no arm touched; `dotnet format`
  whitespace + style on each arm's changed C# files is clean in all four. (2) "10/12 scenarios" was a detector defect: S5.6.2
  is the existing test `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (passes in all four), and S5.6.8's search
  pattern held a quote that broke the shell — the TS test exists in all four and passes. All four arms: 12/12. (3) The "new
  MCP failure" in C, C2 and VT (`TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`) also fails at the
  start commit when re-run: flaky on this host, not counted. The raw files are kept as `results/<arm>/objective.raw.json`; the
  script is fixed.
- **The first blind judgement (`judge-v1/`) read the wrong objective files** (they favoured V: only V had no "new failure").
  It ranked C2 8, V 7, C 6, VT 5 in BOTH runs (labels permuted). It is kept, but superseded: the judge runs again on the
  corrected files (`judge/`).
- **Arms C3 and C4 (owner, 2026-10-26):** two more classic arms, exactly as C and C2 (same start, briefs, models, deadlines), to measure the noise between classic runs. Started 2026-10-26T08:29:41Z together; nothing else ran on the machine.

## Part 2 — the skeleton scheme (arms Q1–Q4), recorded before they run (2026-10-26)

**Question (owner):** does a split where Sonnet writes only the skeleton and contracts, the local model fills the bodies,
and Sonnet polishes with a minimal diff, change time, cost, tokens or quality against the classic arms (C, C2, C3, C4)?
**Arms:** Q1–Q4, identical (n = 4), branches `exp/kimi-e56-skeleton-1..4`, the same task, start commit, models and
known-failure list as all other arms. **Steps:** 1 skeleton (Sonnet, 30 min; signatures, types, data in full; every body > 3
lines a `QWEN-FILL` region with step-by-step instructions; a context pack per region with the exact existing signatures —
added because wrong names of EXISTING APIs were V's and VT's most common draft defect) → 2a fill tests (`fill.mjs` + vLLM,
no Claude agent, 20 min) → 2b fill production (20 min) → 3 polish (Sonnet, 40 min; smallest diff; red checked against the
code at `b4e2ea96`, never against a placeholder) → 4 review (Opus, the same brief as all arms) → 5 fixes (Sonnet, the same
brief). The fill is a script so the rail is hard: only lines between a region's markers change; an answer holding a marker,
empty, or with unbalanced braces is rejected and asked once more, then left for the polish. **Pinned:** thinking OFF (owner;
V without thinking was faster than VT and not worse), up to 4 requests at once, the same vLLM settings. **Order:** after
C3/C4, two at a time (Q1+Q2, then Q3+Q4) — the system NVMe logged bad blocks under 3 parallel arms. **Owner decisions:**
thinking off; the Opus review and Sonnet fixes added after the polish so totals compare; 2 at a time; the final quality score
is ABSOLUTE — each diff scored alone by its own blind Opus session with an anchored rubric, 2 runs per arm, ALL 10 arms
re-scored the same way (`judge-abs.sh`; `research/module_*.md` declared out of scope in the judge's brief this time).
**New metrics:** compile errors right after each fill; rejected answers (rail); survival of the fill's meaningful lines after
the polish and in the final commit (`survival.mjs` — hand-check 10 regions before reporting); the polish diff's size;
leftover markers/placeholders in the final code. **Harness tested before the run:** `fill.mjs` end to end on a toy repo with
the real vLLM (fill, rail, indentation, commits, resume), `survival.mjs` on the same toy, the leftover checks on a temp repo
and on the start commit (whose own `throw new Error('boom')` lines are why only ADDED lines count).
**Predictions:** Sonnet output tokens per Q arm 30–50 % below the classic arms; total Claude cost still inside the classic
range ($6.5–7.8); survival > 50 % for production regions and < 50 % for test regions; compile errors after the fill > 0 in
every arm; quality inside the classic arms' range.
- **Pause for an owner restart, 2026-10-26T09:17:23Z:** C3 and C4 were in step 3 (fixes). Their sessions were killed with SIGKILL (runner first, then the whole session), so no false `result` was written: C3's fix session had FINISHED just before (its stream holds the result; it will be marked done, not resumed), C4's is interrupted and resumes with the time left. The C3/C4 and Q chains were stopped; both restart from their markers. Pause time is not counted (budgets use each stream's last write).
- **Resume after the pause, 2026-10-26T09:28:29Z:** the owner restarted Claude Code (not Windows: no boot event, 0 new bad blocks, vLLM still up). C3/C4 resumed; both chains restarted.
- **coai gate (owner asked to follow all rules and the coai gates, 2026-10-26):** the gate governs changes that SHIP. The arms are measurement branches that never merge, and all 10 run one pipeline (Sonnet → Opus review → Sonnet fixes); adding the gate to the Q arms alone would change the variable under test. So the gate is not inserted into any arm. It applies when an implementation of E5.5/E5.6 goes into the real branch: plan round to `proceed`, then `review_code` with the scope, every finding resolved, our own Opus reviewers in parallel — proposed after the experiment.
- **C3/C4 checked (09:46 UTC):** build, format (changed files), generator, TS pass; 12/12 scenarios; the only MCP failure is the known flaky `TheRoundsNumstats…` test (moved to `failedEnvironment`, raw files kept; the script now classifies it so for every arm).
- **Q1's fill (10:03 UTC), first real use:** 19/19 test and 2/2 production regions filled, 0 rejected, 21 context packs written (avg ~7.3 KB). The build after the fill failed with 11 SYNTAX errors, all from ONE region (`s56-standin`, a test helper): the answer declared a class inside a method body, which the brace-balance rail cannot see. So the post-fill test run did not happen, and the post-fill compile-error count is a LOWER bound (semantic errors in other regions may be hidden behind the syntax errors). The rail is NOT changed for Q2–Q4: the four Q arms stay one treatment (n = 4); the limit is reported instead.

## Part 3 — the skeleton scheme with Haiku as the filler (arms H1–H4), recorded before they run (2026-10-26T12:10:28Z)

**Owner's request:** four more arms, in parallel, by the same scheme as Q1/Q2, with the local model replaced by Haiku.
**The one variable against Q:** the filler. Everything else is Q's: the same skeleton, polish, review and fix briefs (word for
word), the same `fill.mjs` with its rail and its two rounds, the same prompt per region. Haiku is called through
`ask-haiku.mjs` by a headless Claude Code session that sees ONLY the prompt — `--tools ""`, a short fixed system prompt,
`--setting-sources user` (no project rules or hooks; the user settings' effort `high`, as every model here), no MCP servers,
no saved session, an empty folder. Checked on the toy repo: model `claude-haiku-5-5`, ~2.1k input tokens per tiny request
(no Claude Code system prompt), ~5 s, ~$0.0008. Its cost comes from the host and is counted in the fill steps.
**Rule deviation, by the owner's request:** `subagent-models.md` gives Haiku no role; here it is the object of a measurement.
**Order:** H1–H4 start together while Q3/Q4 still run (owner: in parallel) — up to 6 arms at once, against the earlier
2-at-a-time choice for disk safety; the four clones are made and built one after another. Times of H and of Q3/Q4 are read
with that CPU load in mind. A guard keeps a keep-awake running during H (the Q chain stops all keep-awakes when it ends).
**Predictions:** fewer compile errors after the fill than Q (median), higher survival than Q for both kinds of region, a
smaller polish diff than Q; the Haiku fill costs < $0.50 per arm; total Claude cost still ≈ Q's ($8–9: the skeleton with its
context packs and the polish dominate); quality inside the classic band.
- **Q4's one new MCP failure (12:38 UTC)** was the deadline test `SecuritySourcesCharacterizationTests.The_collection_deadline_…`, in code Q4 did not touch, during a saturated full run (load ~22). Re-run at lowered and normal priority, in Q4 and at the start commit: 7/7 each time → the machine, not a defect (local-cpu-budget.md); moved to `failedEnvironment`, raw file kept.
- **Q4's judge sessions:** the absolute judge had copied Q4's UNcorrected objective file at 12:38. D-f5516a (run 1) had not started — its copy was corrected before it ran. D-70adc8 (run 2) had finished with the old copy: its verdict is kept as `verdict.v1.json` and it is re-judged on the corrected copy in the H chain's (incremental) judge run.
- **Survival metric hand-checked on 10 regions (12:55 UTC) and corrected before any report:** the first version counted a line kept only if it stayed EXACTLY in the SAME file; the polish had moved a filled helper class (Q2 `s567-standin`) unchanged to another file (0/8 → in fact 6/8). Main measure now: whitespace-insensitive, in any file the arm changed; the strict same-file measure is kept as the lower bound. Inlined or edited lines still count as dropped (checked: Q1 `s552` 4/17 is a real rewrite). Q1–Q4 final survival: test regions 50–56 % (strict 43–55 %); production regions only 0–5 lines per arm, too few to read.
- **End of the run, 2026-10-26 14:25 UTC:** H1–H4 done, checked (all pass, 12/12), survival measured, the absolute judge run
  for H and for Q4's re-judged session (Q4: 8 / 8 on the corrected file; its superseded run-2 verdict was 8 as well).
- **Hand checks before the final report:** H survival on 4 regions (a kept line found in the final file; a dropped call —
  3 times after the fill, 0 in the final code — really gone, replaced by a fixture setup); 3 of the absolute judge's major
  findings opened in the code — C's rewritten shared caveat, Q2's deviation 53 ("not wired into `Build` yet", false: the
  final code calls it; names a region that no longer exists), H3's "the bodies are placeholders" (0 left) — all three TRUE.
  The two skeleton majors share a cause: the skeleton step's deviation notes go stale after the fill and the polish.
- **Blinding leak found:** the Q and H diffs contain the word "skeleton" 2–5 times (in those plan deviation notes); the
  classic diffs 0. The absolute judge could tell a skeleton arm; the redaction list did not cover the word. Reported as a
  limit; not re-run.
- **Disk:** 0 bad-block events and 0 unexpected shutdowns from 2026-10-25 19:00 UTC to the end of the run.

## Part 4 — Opus splits into tasks, Haiku codes, Sonnet reviews each task (arms T1–T4), recorded before they run (2026-10-26T15:10:13Z)

**Owner's request:** Fable splits plans into epics and epics into stories; here Opus splits the story pair into TASKS, each
task goes straight to Haiku to implement, and for each task a Sonnet reviews and fixes; then the result goes to the Opus
review as in every arm. **Owner's decisions:** Haiku works as a FULL agent in the clone (reads, edits, builds, runs the
task's tests); 4–6 tasks; the same final tail (Opus review + Sonnet fixes) so totals compare; tasks run IN PARALLEL where
their files do not overlap; and every task must already state the exact methods, DTOs and contracts.
**Pipeline (`t-lib.sh`):** 1 split (Opus, 25 min): `tasks.json` + one document per task — goal, scenarios, EXACT contracts
(full signatures, types, fields, JSON shapes, test names and assertions), the existing code to use with file and line,
red-first steps, commands, done-when, out-of-bounds; each file owned by exactly one task; dependencies; the E5 plan owned by
the last task. `tasks-plan.mjs` checks it (one Opus fix round if invalid) and computes batches (ready tasks together, ≤ 4).
Per task: Haiku codes (20 min), then Sonnet reviews its diff against the document and fixes (15 min), each a headless
session; a parallel task works in its own git worktree (rules mount from the local checkout, `node_modules` linked, its own
build) and its commits are cherry-picked back in order; a single task runs in the arm's clone. Then 4 review (Opus, the
common brief), 5 fixes (Sonnet, the common brief). **Two arms at a time** (T1+T2, then T3+T4). Tested before the run: the
whole task pipeline in a model-free fake mode (5 tasks, two parallel batches, worktrees, cherry-picks in order, cleanup);
the checker on a valid and a broken list.
**Rule deviations, by the owner's request:** Haiku has no role in `subagent-models.md`; a review is an Opus role there,
here each task is reviewed by Sonnet (the final review stays Opus).
**Judge:** the absolute judge, incremental; its redaction list is WIDENED for the T arms (skeleton, Haiku, task ids, task
list/document/split) after the skeleton arms' plan notes gave their scheme away — so T is judged blinder than Q and H were.
**New metrics:** per task Haiku's and Sonnet's time and cost; `t-survival.mjs`: Haiku's meaningful lines kept in the final
code, and the lines each Sonnet task review changed.
**Predictions:** total Claude cost ABOVE the classic band, $10–14 per arm (an Opus split that reads the repository, 4–6
Sonnet task sessions each with a cold start, the Opus review); Haiku's cost $0.5–2 per arm; Haiku's code survives better
than in H (> 80 % of its lines), because Haiku builds and tests itself; the Sonnet task reviews change little (< 30 % of
Haiku's lines in total); work time 60–90 min; quality inside the classic band.
- **End of part 4, 2026-10-26 19:10 UTC:** T1–T4 done (T1+T2 15:12–16:52, T3+T4 16:56–18:31), checked, judged. Every split
  passed `tasks-plan.mjs` at once (no fix round): 5–6 tasks, 3–4 batches, up to 3 tasks in parallel; every parallel task's
  commits were carried back without a conflict. All four build and pass both suites; T1–T3 12/12 scenarios; **T4 11/12 by
  exact name** — it delivered S5.6.6 as a Theory named `APlanWithLeadingOrFlags_IsRefusedForKimi` and widened the spec's
  table without recording it (the judge's major for T4). Cost per arm **$8.13 / $9.39 / $9.40 / $12.01**; elapsed 80–100 min.
- **Correction of an interim statement:** from T1 alone ($8.13) it was said T would "probably end near the classic band,
  about $7–8 per arm"; the four arms average $9.73 — T is the most expensive scheme, nearer the pre-run prediction.
- **Hand checks:** T survival — a kept Haiku line found in T2's final code; T3's largest drop is the E5 plan's status and
  deviation notes (written by Haiku in the plan task, rewritten by the final fixes) — a real drop. The absolute judge's
  T3 major opened in the code: `GitScratch.AfterUnlink` is per async flow and T3's test sets it in `InitializeAsync` after
  an `await`, so the junction-first assertion can never fail — TRUE, and missed by both the Sonnet task review and the
  final Opus review. T judged with the widened redaction (2 / 0 / 0 / 3 redactions).

## Part 5 — no Sonnet review: Opus or Sonnet splits, Haiku codes and fixes, Opus checks once (O, S, D x4), recorded before they run (2026-10-26T20:01:00Z)

**Owner's request**, after T and a consultation (codex gpt-6-astra, `52ceb2d9`, verified and closed `solved`): three
schemes, four runs each. **O1–O4:** Opus splits into tasks → Haiku codes each task (no task review) → ONE Opus review of
the whole stories (the common review brief, fresh session) → Haiku applies its findings (`h-fix`: the common fix brief,
model named Haiku). **D1–D4:** Haiku codes the stories directly from the classic coder brief (`d-code`, model named
Haiku) → Opus review → Haiku fixes — the consultant's "largest untested assumption": does Haiku need a split at all.
**S1–S4:** as O, with SONNET splitting (`x-split`, role "JUDGE (Sonnet)"). **Owner's decisions:** Opus checks the
stories once, never per task; a fresh Opus session for the check (no reuse of the split session); frozen behavioural
(mutation) checks for quality, run by script on EVERY arm including the earlier 18. **Order:** four rounds of three, one
arm of each scheme per round (O_n, D_n, S_n), so the schemes share time of day and machine load; 0 bad-block events since
the crashes, even with 6 arms at once (H). **Briefs:** `x-split` / `x-split-fix` differ from T's split briefs only in the
role and the sentence on what happens after the tasks (no task review; one Opus review; the same smaller model fixes);
T's briefs are untouched. Tested before the run: an O arm end to end in fake mode (5 tasks, 3 batches, carried back in
order, its branch name). **Rule deviations, by the owner's request:** Haiku codes and fixes (no role in
`subagent-models.md`); Sonnet splits in S (a judge's job there).
**Predictions (consultant's decomposition taken into account):** O $6–9 per arm (split $2–6 + Haiku < $1 + review $2–3.7
+ Haiku fix < $0.5), not below the classic band on average; S $4–7 (a Sonnet split is cheaper than Opus's); **D $3.5–6,
the only scheme likely to come in BELOW the classic band**; Haiku's fixes leave more of the review's findings open than
Sonnet's did (checked by the mutation checks and the judge); quality: O and S inside the classic band, D the most likely
to fall below it; mutation checks will show at least one surviving mutant somewhere in the earlier 18 (T3's vacuous test
is one known case — the instrument must catch it to be trusted).
- **Mutation checks built and validated (2026-10-26 20:05–20:15 UTC), before any result:** `mutate.mjs` breaks one spec
  decision at a time in an arm's final code (a clean worktree), rebuilds the test project and re-runs the arm's scenario
  classes against an unmutated baseline. Six frozen mutants: M1 one-shot homes not disposed junction-first (VibeHomes),
  M6 the same on the rootless check's path (ConsultCheckScratch), M2 no planner arm for kimi, M3 kimi × disk "confined",
  M4 kimi × web "confined", M5 a plan with flags accepted. A mutant whose anchor is missing or that does not compile is
  "not applicable", never "caught". **Validation:** on T3 (the known vacuous test) M1's intended S5.6.7 test MISSED the
  mutant, as it must; on the classic C2 every intended test caught its mutant. The first version mapped S5.5.4 to M1 and
  both arms "missed" it — the rootless check disposes through a second call M1 did not touch; M6 was added and M1 narrowed
  the same evening, before any arm was scored. All 30 arms are checked after the O/D/S arms end (`mut-all.sh`).
