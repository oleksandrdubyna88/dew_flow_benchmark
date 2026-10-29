# Experiment — Sonnet coders with a local vLLM vs Sonnet coders alone (classic scheme)

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
- **After the run (2026-10-27 04:35–06:30 UTC), recorded as changes, not as design:**
  - `mut-awake.sh` added to keep Windows awake during the mutation checks (the O/D/S chain had stopped keep-awake).
  - `t-survival.mjs` now says "no task review ran" for O and S (it printed "changed 0 lines"); T's numbers unchanged.
  - **Post-hoc measure:** what each fixer did with each review finding (fixed / partly / not fixed / rejected), from the
    fixers' notes, classified by one Opus session and hand-checked on D4 and O2 (`fix-outcomes.json`). It was added
    after the results were seen, to read the prediction "Haiku's fixes leave more findings open" — whose registered test
    was the mutation checks and the judge. It is reported as post hoc.
  - "Renamed" scenario tests (O2, O3, D1, D2) found by hand; the mutation checks' `killedBy` lists confirm them.

## Part 5b — S and D again, with a fresh Haiku review-and-fix before Opus (SR, DR x4), recorded before they run (2026-10-27T12:00Z)

**Owner's request** (2026-10-27, after the Part 5 results): four more runs of each of the two Part 5 schemes the owner
picked — S (Sonnet splits) and D (Haiku codes directly) — with ONE change: when Haiku has finished coding, a FRESH Haiku
session reviews the whole stories and fixes what it found, so Opus reviews code that a Haiku review already passed.
- **SR1–SR4:** Sonnet splits (`x-split`, unchanged) → Haiku codes each task (`t2-coder`) → **Haiku reviews + fixes**
  (`hr-review-fix`, fresh session, 30 min) → Opus reviews once (`2-review`, unchanged) → Haiku fixes (`h-fix`, unchanged).
- **DR1–DR4:** Haiku codes the stories (`d-code`) → **Haiku reviews + fixes** → Opus reviews → Haiku fixes.
- **The new brief** `hr-review-fix` = the Opus review brief's checklist (the 12 scenarios with "would fail without the change",
  the decisions, correctness, security, rules, scope) + the Haiku fix brief's working rules (red test first, the suites,
  commits, notes); findings written to the notes BEFORE any edit. Its diff is kept (`<step>.diff`). The Opus reviewer is not
  told that a Haiku review ran (its brief is unchanged); the judge's redaction already removes the word "Haiku".
- **Unchanged and pinned:** base `b4e2ea96`, every other brief, step budgets, tools, models (claude-haiku-5-5,
  claude-sonnet-5-5, claude-opus-5-5), Claude Code 2.1.310 (the same as Part 5), the judge rubric, the six frozen mutants.
  The `x-split` brief still says nothing about the extra review (kept byte-identical so the split is the same variable as S).
- **Order and load:** four rounds of two (SR_n with DR_n) — Part 5 ran rounds of three, so the load per arm is a little lower;
  wall times are compared with that caveat.
- **Rule deviations, by the owner's request:** Haiku codes, reviews and fixes (no role in `subagent-models.md`).

**Predictions (before any SR/DR arm runs):**
1. The Haiku review-and-fix step costs $0.3–1.0 and 10–30 min per arm, so SR $5.5–8.5 per arm and DR $4.5–6.5 — DR stays the
   cheapest scheme and below the classic band ($6.53–7.85); SR's mean stays inside or below the band.
2. The Opus review finds fewer blocking+major findings than in S and D (mean lower by at least 1 per arm), most clearly in DR.
3. Quality (absolute judge): DR has at most 1 of 4 arms below 7 (D had 2 of 4: 6 and 5.5); SR stays at 7.5–8 like S.
4. The Haiku reviewer does NOT reliably find tests that cannot fail: the mutation checks still show at least one arm of the 8
   with an intended test that misses its mutant.
5. In at least 1 of the 8 arms the Haiku review-and-fix changes something the Opus review then flags as a defect it introduced.
6. Wall time grows by the step's 10–30 min: DR about 60–95 min, SR about 75–135 min.

### Part 5b — operator stop, 2026-10-27 16:19–16:21 UTC (recorded before any SR4/DR4 result)

The owner asked to pause. SR1–SR3 and DR1–DR3 had finished. SR4 (in its first task batch, 2.7 min of Haiku coding, 0 commits) and
DR4 (in 1-code, 645 s of 75 min) were stopped with SIGINT to their `timeout` processes. **run-arm.sh read that as "stopped at its hard
deadline" (exit 124) and marked the steps done**, then started the next steps (SR4 t2, DR4 2-hreview, which edited the coder's two
test files for ~2 min). Those were then killed (SIGKILL). The two arms cannot be resumed cleanly (SR4's task worktrees were already
removed; DR4's clone mixes the coder's and the reviewer's edits), so **both are re-run from scratch** when the chain is restarted. Their
aborted data is kept, not deleted: `aborted-2026-10-27-1621/` (clones, results, notes, build logs). Lesson for the harness: to pause an
arm, kill its whole process group with SIGKILL (no end line is written, so the step resumes), never SIGINT/SIGTERM the `timeout`.
To restart: `setsid nohup ./after-y.sh >> chain-y.log 2>&1 < /dev/null &` — it remakes SR4 and DR4 from `base` and goes on.

### Part 5b — the chain hung in the mutation step, 2026-10-27 19:02–19:55 UTC (recorded after the checks, the judge and the first two mutation results were read)

`after-y.sh` ran the frozen mutation checks two arms at a time and waited for each pair with a bare `wait`. A bare `wait`
also waits for the keep-awake guard, which never ends, so the chain stopped after SR1 and DR1. Fixed in the script (it
waits only for the pair's own jobs) and restarted from its markers at 19:55 UTC; SR1's and DR1's mutation results were
kept, nothing else re-ran. No arm, check or judge result was affected.

## Part 5c — the classic scheme with a fresh Haiku review-and-fix before Opus (CR, CH x4), recorded before they run (2026-10-28T10:58:52Z)

- **Design (the owner's, 2026-10-28):** the classic arm C unchanged (Sonnet codes, 75 min, brief `1-code` with `arm-C.md`),
  plus ONE step after Sonnet's code — the same fresh Haiku review-and-fix as Part 5b (`hr-review-fix`, 30 min). Then the
  Opus review (`2-review`, 20 min, read-only; not told that a Haiku review ran), then the fixes (30 min):
  - **CR1–CR4:** Sonnet fixes (`3-fix`, as in C).
  - **CH1–CH4:** Haiku fixes (`h-fix`; it differs from `3-fix` only in the model's name). The owner's second variant.
- **One change in the harness, for every CR and CH arm:** each arm has its own notes folder, `notes/<arm>/`, so a Haiku
  reviewer can no longer read another arm's notes (the Part 5b leak, incidents §2e). C had the shared folder; C's coder and
  fixer never read another arm's notes, so this does not move the baseline.
- **Order:** four rounds of two (CR_n with CH_n), `after-z.sh`; the same base `b4e2ea96`, the same checks, the absolute judge
  (only the new arms), the frozen mutation checks.
- **Baseline (C, C2, C3, C4):** cost $6.53–7.85 (mean $7.21); per step: code $2.6–3.6, review $2.3–3.6, fix $0.9–2.0; work
  44–62 min; judge 7.75 (7–8); Opus blocking+major 0.75 per arm; the judge's majors 2 in all; 0 weak tests.

| # | Prediction |
|---|---|
| 1 | The Haiku step costs $0.3–1.0 and 10–30 min per arm. CR costs $7.5–9.0 (mean above the classic band). CH costs $6.0–8.0 (mean inside the band, about $1 below CR). |
| 2 | Wall time: CR and CH 60–100 min each (C + the Haiku step). |
| 3 | The Opus review finds at most 1 blocking+major per arm on average in CR and in CH (C: 0.75); no clear fall from C, as in Part 5b. |
| 4 | Judge: CR 7.5–8 in all 4 arms. CH: no arm below 7, and CH's mean at most 0.5 below CR's. |
| 5 | In at least 1 of the 8 arms the Haiku review-and-fix changes something that the Opus review then flags. |
| 6 | All 48 mutants killed; at most 1 of the 8 arms has an intended test that misses its mutant. |

### Part 5c — other arms' data locked during the run (2026-10-28T11:31Z, CR1 and CH1 in step 1)

- The owner asked whether an arm can read other runs' work and do better. A scan of all 40 earlier arms' tool calls found:
  most cross-arm reads were tool logs (which format command was used); real reads of another arm's findings were DR3 and
  DR4 (incidents §2e) and, not recorded before, **T2's fixer read the first 40 lines of `notes/T1-fix.md`** (T1's review
  findings and fixes); O2 and S2 grepped other arms' fix notes for "generator" / "format". No arm read another arm's
  clone, branch or transcript. CR1 and CH1 had read nothing outside their own folders up to the lock.
- On the owner's choice, `lock-others.sh` set mode 000 on everything holding another arm's work (620 paths: the old arm
  clones, old notes, results, worktrees, judge and mutation folders, RESULTS.md, PROTOCOL.md, fix-outcomes.json,
  mutate.mjs, the old arms' Claude transcripts) and mode 300 on the folders above them (no listing). The arms run as the
  same user and `chmod` is not in their allowed tools. Modes saved in `locked-modes.txt`; a watcher runs
  `unlock-others.sh` once all 8 arms are done, before the objective checks, the judge, the mutation checks and the metrics.
- Not covered by the lock: the owner's own repositories (`dew_flow_connect_other_ais` with the old arms' remote branches,
  `dew_flow_benchmark` with the published data). The CR/CH transcripts will be scanned for any read of them.

### Part 5c — after the run (2026-10-28, recorded with the results)

- **The CR2 build check**: the chain's build of CR2's final commit ended with "Fatal error. Internal CLR error.
  (0x80131506)" and no compiler error. Built again alone with the same command: succeeded, 0 warnings, 0 errors.
  `objective.json`'s `build` was set to `true` before the judge read it; the chain's original is kept
  (`data/arms/CR2/objective.json.orig`, `BUILD-RERUN.md`).
- **The lock held**: a scan of all 8 arms' tool calls found no read of another arm's notes, clone, results or
  transcripts, nor of the owner's repositories; no tool call was refused by the lock. The unlock ran at 15:59 UTC
  (625 paths restored) before the objective checks.
- **A false alarm**: the unlock watcher first reported "chain is gone" at 11:34 UTC. `chain-z.pid` held the PID of
  `setsid`'s parent, not of the chain; the chain ran without a break. The PID was corrected and the watcher restarted.

## Part 6 — a second task: C, CR and SRS, each ending in the coai code gate (3 x 3 arms), recorded before they run (2026-10-28)

The full design is [harness/part6/PROTOCOL.md](harness/part6/PROTOCOL.md), copied as written before the run. This is its summary and the
facts the log adds. Part 6 is a NEW task (not E5.5/E5.6), so its numbers stand apart from parts 1–5c.

- **The task:** issue #878 (a spent model is still launched and runs into its timeout) and the quota part of #765 of
  `dew_flow_connect_other_ais`. The plan `todo/PLAN_a_spent_quota_cools_the_model_down.md` (written by Fable from a Sonnet code map)
  passed a coai plan gate (`proceed`, 2 of 2 providers), a consultation (codex-2, every point checked in the code), real `agy 1.4.9` captures
  taken once by the operator and committed as fixtures, and a second plan gate on the revision (`proceed`). It is frozen at `6786582e`:
  43 scenarios (39 C#, 4 node), four stories. The operator's commands to split it into epics were declined for this plan (plan §11).
  Baseline at the base on this host: the whole MCP suite (13,807 tests) and the extension tests pass, 0 failures
  ([harness/part6/baseline.json](harness/part6/baseline.json)).
- **Arms:** 3 schemes x 3 runs, in rounds of three at the same time (C_n, CR_n, SRS_n).
  - **C:** code (Sonnet, 180 min) → review (Opus, 40, read-only) → fix (Sonnet, 60) → coai gate (Sonnet, 150).
  - **CR:** code (Sonnet, 180) → a fresh Haiku review-and-fix (60) → review (Opus, 40) → fix (Sonnet, 60) → coai gate (Sonnet, 150).
  - **SRS:** split (Sonnet, 40; 4–7 tasks, checked by `tasks-plan.mjs`) → per task code (Haiku, 45 each; parallel worktrees) → a fresh
    Haiku review-and-fix (60) → review (Opus, 40) → fix (Sonnet, 60) → coai gate (Sonnet, 150).
- **The coai gate step:** the arm has its own coai server (its own data folder, a copy of the operator's `settings.json`). A plan round on the
  frozen plan only unlocks the code gate; its findings are rejected as "frozen". Then code rounds until `proceed`, `good_enough`, rounds
  spent or `call_human`. Only the gate tools are allowed (not `consult`, not `ask_*`). Gate steps run one arm at a time (a lock), for
  the providers' quotas.
- **Isolation:** each arm under `r/<random token>/` (clone, notes, work, results, temp, coai data); the same branch name in every arm; no brief
  names the arm or its scheme; while the arms run, the old experiment is closed (mode 000) and the folders that would list another arm are
  mode 300; `Read` of `~/.claude`, the vendors' homes, `/proc`, the old experiment and the coai store is denied. Known open path: a deliberate
  `/proc` walk through `node` or `ls`. Every stream is scanned afterwards ([incidents.md](incidents.md) §2g).
- **Limits:** a step that ends on a usage limit, a rate limit or an overload is resumed in its own session after 15 min (up to 40 times); a gate
  that cannot run is resumed after 30 min (up to 8 times). Waiting is not counted against a budget.
- **Measured:** what the earlier parts measured (cost, time, the Opus review's findings, the objective checks with the 43 scenarios by name, the
  absolute blind judge) and, per arm, the coai gate's rounds, verdicts, reviewers, findings (severity, accepted or rejected, fixes). A blind
  Opus judge rates every coai code finding (real, partly, not real; severity; usefulness) and scores each arm's set from 1 to 10. Mutation
  checks: designed after the run on the final code, frozen on 2026-10-29 before any was applied. Not pre-registered, added after the results:
  the hand check of the judge's four major findings, and the per-role cost means.

| # | Prediction (recorded before the run) |
|---|---|
| 1 | Cost per arm (Claude only): C $18–33, CR $20–35, SRS $16–30; CR's mean about $1–3 above C's. |
| 2 | Elapsed per arm, gate included: 3–6 h. The gate step 30–120 min. |
| 3 | The Opus review's blocking+major per arm: C 1–3; CR at most C's; SRS at least C's. |
| 4 | The coai gate's first code round finds ≥ 3 findings in every arm; the blind judge rates at least half of all coai findings real; in at least 2 of the 3 schemes coai finds a real defect the Opus review did not name. |
| 5 | Absolute judge: C and CR 7–8 in every arm; SRS 6.5–8. |
| 6 | Scenarios by exact name: C and CR ≥ 40 of 43 in every arm; SRS ≥ 38. |

The mutation prediction was written in the header of `mutate6.mjs` before the run: M1, M2, M3, M7 and M8 killed in all nine arms; survivors most
likely at M4, M5 or M6; 7–8 of 8 killed per arm; no scheme difference beyond one mutant; at most two not applicable in all.

### Part 6 — the spend limit and its repair (2026-10-28 21:45 UTC – 2026-10-29 08:45 UTC, recorded after the run)

- The arms started at 20:26 UTC (round 1: C1, CR1, SRS1). At 21:45–21:47 UTC the Claude CLI answered "You've hit your individual spend
  limit · … your session limit resets 1:20am (Europe/Paris)", with `api_error_status` 429. `run-arm.sh`'s limit detector looked for other
  words and missed this one. Steps that ended in that error were marked done (14 attempts: C1 3, CR1 4, SRS1 7; each of C1's review, CR1's Haiku review and SRS1's task
  t4 had worked 4–22 min first, the other 11 failed after about 4 s). Round 2 (C2, CR2, SRS2) started at 21:48 UTC and ended in about a minute with no work.
- The problem was found from the log. `repair-limit-2026-10-28.sh` (kept as the record) removed the `.done` marker of every step whose last result is an error (so its own session
  resumes), the arm-done line, and what was computed from a bogus step (a `review.json` parsed from an error, before/diff snapshots taken at the wrong commit,
  SRS1's batch markers and Haiku diffs of t4, t5 and t7). Commits and uncommitted work stayed. Round 2 had no commit and a clean tree, so its out, notes
  and work folders were reset to a fresh start. The detector was changed to read `api_error_status` and `api_error` first, and an error result is
  never "done" (it stops the runner for a person to look). The chain restarted at 08:45 UTC and the round-1 sessions resumed with the time left of their budgets.
- Effect on the data: no step was lost; Claude-only costs of the interrupted attempts are counted (C1's Opus review: $8.75 in two attempts, against $3.98–$4.76 elsewhere, which raises the C mean from $15.16 to $16.63); active minutes leave the wait out; round-1 elapsed times (779–862 min) contain it.

### Part 6 — after the run (2026-10-29, recorded with the results)

- **The chain:** rounds 2 and 3 ran without a break (round 2 ended 13:59 UTC, round 3 at 17:47 UTC). The objective checks ran from 17:47 to 19:27 UTC.
- **SRS3's Haiku review-and-fix started two subagents** (one on Opus, $2.84, and one on Sonnet, $0.94). Reason: `Agent` is in the runner's tool list for every step
  that can write. Two earlier `Agent` calls without a model were refused by the project's hook; the retries named `sonnet` and `opus`. It is the only step in the nine arms
  with a subagent. SRS3's Haiku review result is marked contaminated.
- **The isolation scan** of the nine arms' tool calls (6,060 calls, plus the 158 calls of SRS3's two subagents): no path of another arm, no other arm's token, no path of the old
  experiment, the owner's repositories or the vendors' homes. Two things were looked at one by one: SRS1's `readlink /proc/<pid>/cwd` on two process ids (both were its own worktrees), and a
  failed edit of CR3's Haiku reviewer to a path in the experiment root (the file did not exist; nothing was written there).
- **A test that fails in the arms, not in the checks:** the arms' own whole-suite runs showed one failing test, `RegularFilesTests.A_fifo_a_socket_and_a_device_…` (a Unix-socket path longer
  than 108 characters under the arm's temp folder). The brief had said nothing is known to fail. All nine arms show it (1 failed in each own run): the gate notes of C1, C3, CR2, CR3, SRS1, SRS2 and SRS3 name it, and so do the fix notes of C2 and CR1. In the objective checks, with the
  normal temp folder, it passed, and the suite counts reported in the tables are those of the objective checks.
- **The operator's commands in the plan round:** every arm's plan round carried the operator's panel commands (split into epics, a Fable/Opus role split, consultants first, a consult
  cadence). All nine gate agents declined them, with the reason that the plan is frozen and the brief fixes the flow.
- **C2's Opus review** ran to its hard deadline (50 min, exit 124) after it had written its review. The review is used as written.
- **The objective check and C1's S14:** C1's test for S14 is named `AnUnknownModel_HasNoGroup_AndEveryKnownOneHasItsKey`. The check found it by name (present) but not as a passing whole-word name,
  so C1 reads 42 of 43 passing. The test is a theory of 8 cases and all passed. The mutation check found it by its longer name and it caught its mutant. Left as the check reported it.
- **The judges:** the absolute blind judge, 18 sessions (2 per arm, labels random, the diff redacted; redaction counts 3–13 per arm). The coai findings judge, 9 sessions (one per arm), with the gate agent's decisions
  and the providers not shown. It worked in each arm's clone, where the scheme was not redacted. The main session then opened the four major findings of the absolute judge in the code
  ([data/part6/judge-abs/handcheck.json](data/part6/judge-abs/handcheck.json)).
- **Mutation checks** ran from 19:57 to 20:37 UTC, two arms at a time, 4 test threads each.
