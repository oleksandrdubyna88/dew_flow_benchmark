# Incidents and corrections

> Everything that went wrong during the run, and every correction made to a number BEFORE it was reported. Times are
> UTC. The full log, in the order it was written, is [protocol.md](protocol.md); how each fix works is
> [harness.md](harness.md) §8.

## 1. The machine

| When (2026-10-25) | What | Effect on the experiment |
|---|---|---|
| 17:40 | Windows stopped: bugcheck **0x154** (UNEXPECTED_STORE_EXCEPTION); no dump written (volmgr 161). GPU live-kernel reports `0xA1000007` (amdfendr) and `0x193` (`dxgkrnl!ProcessDeadlockThread`) sent after the restart. vLLM had been up ~9 min and was generating the warm-up request. | Before any arm started. Owner: retry vLLM as planned. |
| 18:18–18:28 | **~670 "bad block" events** (disk Id 7) on the **system NVMe** (Harddisk0: Solidigm D7-PS1010 3.84 TB, a data-centre disk, 4Kn since its purchase 3 months earlier, firmware G70YG252), rising from 4 to ~110 per minute, while three arms built and tested at once. The first such events since a Windows reinstall the day before (the event log covers only that day). | — |
| ~18:30 | Windows stopped: bugcheck **0x7A / c000009c** (KERNEL_DATA_INPAGE_ERROR, "device data error"). | V, C and C2 interrupted in step 1 after ~16–22 min each. C's session had in fact FINISHED (see §2). |
| 18:31–18:48 | Windows stopped again: bugcheck **0xEF** (CRITICAL_PROCESS_DIED), with NOTHING of the experiment running. | The first crash's cause re-read as possibly the disk too — not proven. |
| from 19:11 | Owner: continue as planned. The arms resumed their own sessions with the time left. | Downtime not counted. |

**What the logs licensed, and what they did not.** The three bugcheck codes are all of the "cannot read from storage /
paging" family, the bad blocks preceded the second crash by two minutes, and the third crash came with no load — so the
disk is the main suspect for all three. The AMD driver live dumps (some before any of this work) may be a separate,
older problem: the owner reports frequent AMD-driver crashes under the previous Windows install. No SMART media-error data
was read (it needs admin; `Get-StorageReliabilityCounter` returned empty error fields from the inbox driver), so neither
cause is proven. The "Harddisk1 controller errors" in the same log are the 31 MB storage of a USB keyboard, unrelated.
The NVMe's "TemperatureMax 77" is its operating LIMIT (the drive's 0–77 °C range), not a reading.

**Later (2026-10-26):** the owner ran the SMART check of the system NVMe and reports it as fine. Together with 0 new
bad-block events from the end of the crashes to the end of part 4, the disk's role in the crashes stays open (not proven
either way).

**What was done to the experiment because of it:** the harness was made resumable (session ids fixed before the first
attempt, `--resume` with the remaining budget); arms ran detached; later batches ran two at a time on the owner's choice
(except H1–H4, run in parallel on the owner's request, up to 6 arms at once); a keep-awake process (no power-setting
change) held Windows out of Modern Standby, which otherwise stops the `wslc` VM. The data was backed up to the owner's
NAS and the branches pushed after each phase. On 2026-10-26 there were 0 new bad-block events through the end of the run.

## 2. Harness defects that touched a number

| Found | Defect | Effect | Handling |
|---|---|---|---|
| 2026-10-25 20:03 | Arm C's step-1 session had finished (its stream holds the `result`, $2.54) before the crash, but the runner died before writing `.done`; on the resume the harness resumed it as "interrupted". | C got +14 min and +$0.43 of step 1, one more commit. | Reported with and without that attempt; the rule "finished, runner died → done" added (and later used correctly for C3 after the 2026-10-26 pause). |
| 2026-10-25 21:20 | The objective checks reported `format: false` everywhere (analyzer errors in `src_bugs`, which no arm touched), 10/12 scenarios everywhere (two detector defects), and one "new" MCP failure in C, C2, VT (a test that fails at the start commit too, but passed in the base's first run). | The first group judge (v1) read these files; only V looked clean. | Corrected by hand before any report (raw files kept as `objective.raw.json`), the script fixed, the group judge re-run (v2). v1 kept: the same order. |
| 2026-10-25 22:00 | For crash-interrupted attempts the metrics took output tokens from the stream, which holds only a message's starting usage. | V's and C2's interrupted step 1 under-estimated by ~$0.6 each. | Read from the session transcripts; checked on a finished session (stream 955 vs host 44 090 output tokens). |
| 2026-10-26 10:03 | Q1's first real fill: one region's answer declared a class inside a method body; the brace-balance rail passed it. | The build failed with 11 syntax errors; the post-fill test run did not happen; the post-fill error count is a lower bound. | NOT fixed for Q2–Q4/H, so all skeleton arms share one treatment; reported as the rail's limit. |
| 2026-10-26 12:38 | Q4's full suite had one "new" failure: a deadline test (`SecuritySourcesCharacterizationTests.The_collection_deadline_…`) in code Q4 did not touch, during a run at load ~22 on 14 cores. | Would have counted against Q4; the absolute judge had already copied Q4's file. | Re-run in Q4 and at the start commit at lowered AND normal priority: 7/7 each → the machine (local-cpu-budget rule). Moved to `failedEnvironment`; the unstarted Q4 judge session corrected, the finished one re-judged (old verdict kept). |
| 2026-10-26 12:55 | The survival metric counted a line kept only if it stayed in the SAME file, exactly. | Q2's moved helper class read 0/8 kept (it is 6/8). | Main measure made whitespace-insensitive across the changed files; strict kept as the lower bound; hand-checked on 10 regions. |
| 2026-10-26 13:05 | The H arms' Haiku requests entered the unit-price fit (their "input" includes cached tokens), giving a nonsense Haiku fit. | No reported value used it. | Filler requests excluded from the fit (they carry their own host cost). |

## 2b. Found in the final hand checks (2026-10-26 14:25–14:45 UTC)

- **Blinding leak:** the Q and H diffs contain the word "skeleton" 2–5 times (in their plan deviation notes), the classic
  diffs 0; the judges' redaction did not cover it, so the absolute judge could tell a skeleton arm. Not re-run; reported
  as a limit.
- **Stale skeleton notes:** 3 of the absolute judge's major findings were opened in the code and were all true; the two
  in skeleton arms (Q2, H3) are deviation notes the skeleton step wrote, which became false after the fill and the polish.
- **Q4's re-judged session** (on the corrected objective file) scored 8, as its superseded verdict had.
- **Disk:** 0 bad-block events and 0 unexpected shutdowns from 2026-10-25 19:00 UTC to the end of the run.

## 2c. Part 4 (T1–T4)

- **An interim estimate reported too early:** from T1 alone ($8.13) the owner was told T would "probably end near the
  classic band, about $7–8 per arm". The four arms average $9.73 (T4 $12.01: its split alone cost $5.94). Corrected in the
  report; the lesson is the one the classic band already taught — one arm is not a scheme.
- **The judge found a test defect two reviews had passed** (T3: an assertion that can never fail, because a per-async-flow
  test hook is set in `InitializeAsync`); opened in the code and confirmed.
- No crash, no harness defect, no conflict when the parallel tasks' commits were carried back; every task list passed the
  checker at once.

## 2d. Part 5 (O, S, D) and the mutation checks (2026-10-26 20:05 – 2026-10-27 05:30 UTC)

- **No crash, no interrupted step**: all 12 arms ran each step once; every cost is the host's own number.
- **A wrong log message (no number touched):** `t-survival.mjs` printed "the Sonnet task reviews changed 0 lines" for O
  and S, which have no task review. Fixed to say "no task review ran"; the JSON now holds `null` there, and
  [tables.md](tables.md) §4b says "no task review". T's numbers are unchanged.
- **Keep-awake gap:** the O/D/S chain stopped keep-awake when it ended, but the mutation chain started after it ran
  without one for about 40 s, until `mut-awake.sh` (added to [harness/](harness/)) guarded it. No sleep occurred.
- **The Haiku price fit is bad** (max error 636 % — the fit now includes many tiny Haiku calls inside other sessions). It
  is used only to estimate interrupted attempts, and no Haiku attempt was interrupted, so no reported cost depends on it.
- **"Missing" scenarios that were renamed:** the detector looks for exact test names and reported 10–11 of 12 for O2, O3,
  D1, D2; each renamed test was found by hand, and the mutation checks confirm each catches its mutant.
- **The fix outcomes were classified by one model** (Opus, from the fixers' notes only) and hand-checked on D4 and O2;
  it flagged its own ambiguous cases (notes that call a finding "fixed" and list a part as not done → `partly`).
- **The mutation checks found a second vacuous test** (H2, the same defect as T3); opened in the code and confirmed.
- **Disk:** 0 bad-block events, 0 unexpected shutdowns.

## 2e. Part 5b (SR, DR) (2026-10-27 10:50–20:03 UTC)

- **Operator stop, 16:19–16:21 UTC** (the owner asked to pause): SR1–SR3 and DR1–DR3 had finished. SR4 and DR4 were
  stopped with SIGINT to their `timeout` processes; `run-arm.sh` read exit 124 as "stopped at the hard deadline", marked
  the steps done and started the next ones (DR4's Haiku review edited two test files for about 2 min). Both arms were
  killed, archived (not deleted) and **re-run from scratch** from 16:35 UTC; no number from the aborted runs is used.
  Lesson: to pause an arm, SIGKILL its whole process group (no end line is written, so the step resumes). Recorded in
  [protocol.md](protocol.md) before any SR4/DR4 result.
- **The chain hung for 53 min (19:02–19:55 UTC)** in the mutation step: a bare `wait` also waited for the keep-awake
  guard, which never ends. Fixed in [harness/after-y.sh](harness/after-y.sh) (it waits for the pair's own jobs) and
  restarted from its markers; nothing re-ran, no number touched.
- **DR1's Opus review stopped at its hard deadline** (30 min, exit 124) after it had written its review JSON; the review
  is used as written.
- **Arms read each other's notes (independence leak).** All arms write their notes to one shared folder. The Haiku
  reviewers of DR3 and DR4 listed that folder and read the previous DR arm's review notes (DR3: `DR2-hreview.md`; DR4:
  `DR3-hreview.md`, and its notes say "still open from DR3 H4" and "re-checked from DR3"). SR3 and SR4's reviewers
  and SR1's fixer touched other arms' format logs only. Found by a scan of the session transcripts for tool calls that
  name another arm's note file. The same scan flags Part 5 fixers (O1, O2, O4, S2, S3, S4); checked one by one, they read
  other arms' tool logs (format, suite and generator output, to learn how to run the tools; O2 also to see whether a
  failing test failed before), and S2 searched D2's and O2's fix notes for the word "format" — no other arm's findings. The harness gave each arm its own file name but not its own folder; a per-arm folder outside every
  other arm's reach is the fix for any re-run.

## 2f. Part 5c (CR, CH) (2026-10-28 11:00–17:27 UTC)

- **A leak not found before: T2 (part 4) read T1's fix notes.** The owner asked, while CR1 and CH1 ran, whether an arm
  could read other runs' work and do better. A scan of every earlier arm's tool calls for paths into another arm's
  notes, clone, results or worktrees found, beyond §2e: **T2's fixer ran `cat notes/T1-fix.md | head -40`** — T1's
  per-finding notes (what the review found and how T1 fixed it); C's fixer, T1's fixer, O1, O4, S3 and S4 read other
  arms' tool logs only (format, suite, TS and generator output). No arm read another arm's clone, branch or transcript.
  T2 is therefore not fully independent of T1; its numbers are kept and read with that condition.
- **The fix for part 5c: a lock, not only a folder.** Each CR/CH arm had its own notes folder from the start. On the
  owner's choice, from 11:31 UTC (CR1 and CH1 in step 1, neither had read anything outside its own folders)
  `harness/lock-others.sh` set mode 000 on 620 paths holding other arms' work (old clones, notes, results, worktrees,
  judge and mutation folders, RESULTS.md, PROTOCOL.md, fix-outcomes.json, mutate.mjs, the old arms' Claude transcripts)
  and mode 300 on the folders above them. The arms run as the same user; `chmod` is not in their allowed tools. A
  watcher ran `unlock-others.sh` once all 8 arms were done (15:59 UTC), before the checks, the judge and the mutation
  checks. Not locked: the owner's own repositories (`dew_flow_connect_other_ais`, `dew_flow_benchmark`). The final scan:
  no CR/CH tool call names another arm's data or those repositories, and none was refused by the lock.
- **CR2's build check: a runtime crash, not the code.** The chain's build ended with "Internal CLR error.
  (0x80131506)" and no compiler error; the same commit built alone: succeeded, 0 warnings, 0 errors. `build` set to
  `true` in `objective.json` before the judge read it; the original and the reason are in `data/arms/CR2/`.
- **A false "chain is gone"** (11:34 UTC): the PID file held `setsid`'s parent, not the chain. No break in the run.
- **Seen in a Haiku review's own test run** (CH2): 17 rounds-database tests failed in its whole-suite run (the coder
  had seen 18). The objective checks on the final commits show 0 new failures in all 8 arms; not investigated further.

## 2g. Part 6 (C, CR, SRS × 3, a second task) (2026-10-28 20:26 – 2026-10-29 20:37 UTC)

- **The overnight spend limit (2026-10-28 21:45–21:47 UTC).** *What happened:* round 1 (C1, CR1, SRS1) had run for 80 minutes when the Claude CLI
  answered "You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your session limit resets 1:20am
  (Europe/Paris)", with `api_error_status` 429. The limit detector in `run-arm.sh` looked for other words and missed this one. A step that ended
  in that error was marked done, and the next step started at once and failed in about 4 seconds. 14 attempts ended this way: C1 3, CR1 4, SRS1 7.
  Three of them (C1's Opus review, CR1's Haiku review, SRS1's task t4) had worked 4–22 minutes first. Round 2 (C2, CR2, SRS2) started at 21:48 UTC
  and ended in a minute with no work and no commit. *Effect on the data:* no commit or finished step was lost. The work done before the limit
  is in the Claude-only cost, once: the session's running total counts C1's review ($3.46 at the limit, $5.30 at its end), CR1's Haiku review ($0.75 and $1.10) and
  SRS1's task t4 ($0.21 and $0.75). (The first count added the two attempts of each: see §2h.) Active minutes
  leave the wait out. Round 1's elapsed times (779–862 min) contain 11 hours of waiting. The `limited` field in round 1's `timing.jsonl` reads false
  for those attempts, because the detector wrote it. *What was changed:* [harness/part6/repair-limit-2026-10-28.sh](harness/part6/repair-limit-2026-10-28.sh)
  (kept as the record) removed the `.done` marker of every step whose last result is an error, the arm-done line, and what was computed from a bogus
  step (a `review.json` parsed from an error, before/diff snapshots at the wrong commit, SRS1's batch markers and the Haiku diffs of t4, t5 and t7).
  Round 2 was reset to a fresh start (its bogus attempts are therefore not in the archives; `chain.log` has their times). The detector now reads
  `api_error_status` and `api_error` first, then the words, and a step whose last result is an error is never marked done: the runner stops for a
  person. The chain restarted at 08:45 UTC and the sessions of round 1 resumed with the time left of their budgets. The run-arm comment says "nine
  steps"; the streams show 14 attempts.
- **SRS3's "Haiku" review step started two subagents.** *What happened:* in `3-hreview` the Haiku session called the `Agent` tool four times. The
  project's hook refused the first two (no model named; the rule `subagent-models.md` accepts `sonnet`, `opus` or `fable` only). The next two named
  `sonnet` (an audit of the 43 scenarios against the tests) and `opus` (a check of the diff against the rules). Both ran in the background.
  *Effect:* of the step's $5.35, Haiku cost $1.57, the Opus subagent $2.84 and the Sonnet subagent $0.94: $3.79 is not Haiku. The step's 12
  findings come from a Haiku session with two stronger helpers. It is the only step of all nine arms with a subagent (`grep -c '"name":"Agent"'`
  on all streams: only this file). *Why it could happen:* `run-arm.sh` allows `Agent` in every step that can write. *What was changed:* nothing in
  the run. SRS3's Haiku-review result is marked contaminated in [tables.md](tables.md) §10 and [results.md](results.md) §7d, and the measured SRS mean cost ($17.09 after the §2h correction, the primary figure) is given with
  "($15.83 if the two subagents are left out)" beside it. A re-run should take `Agent` out of the write tools.
- **The isolation scan.** Every tool call in the nine arms' streams (6,060) and in the two subagents of SRS3 (158) was read for paths. *Checked:* no arm
  read another arm's folder, clone, notes, results or token; none read the old experiment, the owner's repositories or the vendors' homes; no tool call was
  refused by the mode-000/300 folders. Four `Read` calls were refused by the deny list (C1 1, CR2 1, SRS3 2): each one wanted the arm's own saved tool result under `~/.claude/projects/`. Two things were opened one by one. SRS1's Haiku coder (task t2) ran `readlink /proc/<pid>/cwd` on two process
  ids; the answers were two worktrees of SRS1 itself. CR3's Haiku reviewer tried to edit a path in the experiment root (`…/coai-exp2/notes/hreview.md`); the
  file did not exist, the edit was refused, nothing was written. The scan cannot see a walk of `/proc` that a shell command hides; no such command is in the streams.
- **A test that fails only under the isolation.** The per-arm temp folder made a Unix-socket path longer than 108 characters, so
  `RegularFilesTests.A_fifo_a_socket_and_a_device_…` failed in the arms' own whole-suite runs. The brief said that nothing is known to fail. All nine
  arms show it in their own whole-suite runs (1 failed in each): the gate notes of C1, C3, CR2, CR3, SRS1, SRS2 and SRS3 name it and call it environmental, and so do the fix notes
  of C2 and CR1. In the objective checks, with the normal temp folder, it passed (0 new failures in all nine arms). The suite counts in [tables.md](tables.md) §10.3 are those of the objective
  checks, so they include this test as passed. "All passed" in the Part 6 text means: in the objective checks.
- **C1's S14 reads "absent" in the pass check.** The test is named `AnUnknownModel_HasNoGroup_AndEveryKnownOneHasItsKey`, a theory with 8 cases
  that all passed. The check looks for the plan's name as a whole word in the report, and a longer name does not match. So C1 shows 42 of 43
  passing. The test exists and passes, and the mutation check found it by the longer name and it caught its mutant. The objective file is unchanged;
  the absolute judge was told that a longer name is a rename, not an absence.
- **The gate agent left a false note in SRS1.** The coai plan round named the partial class `QuotaCooldowns`. The gate agent saw a code defect and merged the two
  files (`c768433a`), but its brief said not to change the plan file, so the plan's deviation note about the partial class stayed false. The judge found
  that note (two runs; major) and the hand check lowered it to minor. It is an effect of the gate step's brief, not of the coder.
- **Hard deadline.** C2's Opus review ran to its limit (50 minutes, exit 124) after it had written its review; the review is used as written.
- **The operator's commands.** The plan round of every arm returned the operator's panel commands (split into epics, a Fable/Opus role split,
  consultants first, a cadence). All gate agents declined them with the reason that the plan is frozen and the brief fixes the flow.
- **The judge of the coai findings was not blind to the scheme.** It worked in each arm's clone, where the commit messages and the plan's notes were
  visible and not redacted; the label was random and the gate agent's decisions were not shown. The absolute judge saw a redacted diff only.

## 2h. A cost-counting error in Part 6 (found 2026-10-30) and the check of parts 1–5c

- **What was wrong.** The first `metrics6.mjs` took each attempt's `result` event and added the costs of all attempts of a step. A RESUMED
  attempt (`claude --resume`) reports the running total of its session, in `total_cost_usd` and in `modelUsage`, so the sum counted the earlier
  attempts twice. Every resumed step of Part 6 was over-counted.
- **How it was found.** The owner's session measured Part 7's CHH1, step `6-fix`, with 14 attempts. Attempts 2 to 13 each ran one turn in 1–4
  seconds, and each reported exactly the same $1.646 and 31,855 Sonnet output tokens as attempt 1 (checked in the streams). Attempt 14 reported
  attempt 1's total plus its own work ($2.264). The first count gave CHH1 $44.92. In Part 6 the same shape is in the resumed steps of
  round 1: for example C1's Opus review, where a2 ran 5 turns in 53 seconds and reports $1.84 more than a1.
- **What it did to Part 6.** Only the three round-1 arms had resumed steps: C1 $19.92 → **$16.47** (its Opus review $8.75 → $5.30), CR1
  $16.27 → **$15.52** (its Haiku review $1.85 → $1.10), SRS1 $14.85 → **$14.64** (task t4 $0.96 → $0.75). The six arms of rounds 2 and 3 ran each step
  in one attempt and do not change. The scheme means moved from C $16.63, CR $16.55, SRS $17.16 to **C $15.48, CR $16.30, SRS $17.09**
  (SRS without SRS3's two subagents: $15.83, first count $15.90). SRS3's subagent cost ($3.79 of $5.35) is unchanged: that step ran once.
- **What changed in the conclusions.** The first count made C look dearest, and the write-up first explained that with C1's limit-split
  Opus review and then "adjusted" the C mean to $15.16 and said the C/CR order reverses. That adjustment was mostly this counting error.
  With the corrected numbers C has the lowest mean and CR the next, SRS the highest, and no difference is measured with three arms each
  (Welch's t 0.55–1.15). Active minutes, elapsed minutes, quality, findings and mutation results are not touched.
- **What was changed.** `metrics6.mjs` now takes, per session, the result of the LAST attempt, and sums that over the sessions of a step (a header
  comment says why). The first output is kept as `data/part6/metrics6.v1-summed-attempts.json`. Every Part 6 cost in [results.md](results.md),
  [tables.md](tables.md), [README.md](README.md), this file and [protocol.md](protocol.md) was recomputed from `metrics6.json`.
- **Parts 1–5c: not affected.** Four arms have a resumed attempt in `data/arms/<arm>/timing.jsonl`: C (1-code a2), C2 (1-code a2), V (1-code a2) and C4
  (3-fix a2). There `metrics.mjs` also adds the attempts of a step, but there the resumed attempt does NOT carry the session's total: C's a1 reports $2.54 and
  44,090 output tokens, and its a2 only $0.44 and 6,960 (19 turns); C2's a2 is $0.53, V's $0.17 and C4's $0.08, each far below its first attempt's
  cost (C2, V and C4's first attempts were cut off with no result, and their costs were estimated from the transcripts). So those a2 figures are the
  attempts' own work, and the published costs of these arms stand. (Why: those first attempts ended in a Windows crash or a SIGKILL, so the session's
  total was probably not saved for the resume. That is a guess; the check above does not depend on it.) No other arm of parts 1–5c has a resumed attempt.

## 2i. Part 7 (CHH × 3, the same task) (2026-10-29 21:37 – 2026-10-30 05:31 UTC)

- **The bias is in the design, not an incident, and it is stated here too.** The two reviewer focuses were chosen after Part 6, from what Part 6 had
  missed on this same task. The briefs' checklists name the defect kinds (an inherited default, text over several lines, an order of results, a
  time zone that the runtime ignores, a Unix socket path over 108 characters). Read [results.md](results.md) §7e with that in mind.
- **The trust warning in every step of every arm.** Every stderr file of the arms starts "Ignoring 5 permissions.allow entries from
  .claude/settings.json: this workspace has not been trusted. Run Claude Code interactively here once and accept the trust dialog …". *Checked:*
  all 111 stderr files have it: 77 of the 9 Part 6 arms and 34 of the 3 CHH arms (none has any other text). So the condition was the SAME in Part 6
  and Part 7. *Effect:* the project's own allow list was not applied, and the permission set of every step is the runner's `--allowedTools` list, the same
  for every arm ([harness.md](harness.md) §3 said this for the earlier parts). The warning does not touch a number and does not separate the schemes.
- **The spend limit and the account (2026-10-30 00:53–04:08 UTC).** CHH1's last fix step (`6-fix`) worked for 31 minutes (82 turns) and then ended
  with "You've hit your individual spend limit … your session limit resets 5am (Europe/Paris)", `api_error_status` 429. That is 04:00 UTC (Paris is UTC+1
  after 25 October). The runner (with the fixed limit detector of §2g) waited 15 minutes and resumed the same session. Attempts 2 to 13 each ended
  in 3–4 seconds with the same error (12 attempts), and attempt 14, at 04:08:46 UTC, went through and finished in 26 seconds. CHH2 and CHH3 had ended
  before the limit (00:35 and 00:18 UTC); CHH1's gate ran 04:09–04:27. The owner's session reports that the Claude account was changed during the night (and
  the owner's email address in this session's context changed with it). *The data cannot show that:* the reset time in the message also fits
  attempt 14. *The data differ from the owner's note on one point:* the limit began at 00:53 UTC, not at 01:38, so the wait was 3 h 16 min, not 2.5 h.
  *Effect:* CHH1's elapsed time is 410 minutes; its active time (215) leaves the waits out; its cost is counted once (§2h; the first count gave $44.92, the corrected one
  $23.52). No step was marked done by the limit.
- **The .NET SDK was upgraded in the middle of CHH3's mutation run.** The system (apt, unattended) upgraded `dotnet-sdk` from 10.0.113 to 10.0.114 at 05:19:54 UTC.
  CHH3's first run had finished M1 to M4 and its builds of M5 and M6 failed (NETSDK1042, the path `/usr/lib/dotnet/sdk/10.0.113/…` was gone), so they were
  "not applicable". The run was repeated whole (05:22–05:31 UTC). *Effect:* CHH3's final mutation numbers are on SDK 10.0.114, the CHH1 and CHH2 numbers (05:05–05:15)
  and the objective checks (04:28–05:00) on 10.0.113. The first run is kept as `data/part7/arms/CHH3/mutation.attempt1-sdk-upgraded-mid-run.json`. All 24 mutants of
  the final runs were killed on both SDKs.
- **CHH3: a failing suite test in the objective check, and the judge's major.** The objective check of CHH3 reports 1 new failure,
  `HeldFileInheritableOnLinuxTests.ATakeWhoseStreamCannotBeMade_ClosesItsDescriptor_…`. Both judge runs named it a major (and scored 7). The hand check did not confirm it: the
  class alone passed 6 of 6 three times, CHH3 does not touch `HeldFile`, and two whole-suite re-runs gave 0 failures and 16 failures; the second ran
  while the mutation checks and two coders loaded the host. The owner's session reports that this second re-run overlapped the SDK upgrade above; **I could not
  check that time, because no log of the re-run was kept.** Part 6's C3 gate note names the same test as failing once under load. Treated as load-dependent;
  the 7 and 7 stay in the tables as the judge gave them.
- **CHH1's S33 was renamed**, so the exact-name check reads 42 of 43. The renamed test reads the drawn card as a node tree (`pageTree`) and does not run
  the page's script (`runPageHtml`), so it does not read the RUNNING page that the plan's name asks for. Recorded in the arm's plan as deviation 10 ([results.md](results.md) §7e).
- **The read-only steps refuse subagents.** The Part 6 fault (SRS3) was closed for Part 7: `Agent` and `Task` are in `--disallowedTools` for read steps. No
  stream of the three arms has such a call, and no step shows another model's cost.
- **The isolation scan** of the three arms (2,074 tool calls): no path of another arm or of a Part 6 folder, no path of the old experiment, the owner's
  repositories or the vendors' homes, no `/proc`. One `Read` of the arm's own saved tool result under `~/.claude/projects/` (CHH1, review step) was refused by the deny list.
  The lock also closed every Part 6 result and the write-up worktree (22 paths in all, with the old experiment) (a side effect: git lost its registration of that worktree; it was
  recreated from the branch with no change of content).
- **The long temp folder again.** CHH1's `hfix` notes record the same `RegularFilesTests.A_fifo_…` failure as in Part 6 (§2g). In its
  first whole-suite run it first named another test by mistake and corrected that in its notes. The objective check of CHH1 shows 0 new failures.

## 3. Changes of plan during the run (all by the owner, all recorded before the affected arms ran)

- C2 added as an A/A control (before the start).
- VT added after V's coder sent every request without thinking (run after V, C, C2, alone on the GPU).
- C3, C4 added (2026-10-26) to measure the noise between classic runs.
- The skeleton scheme (Q1–Q4) and its four decisions: thinking off, the Opus review and fixes kept after the polish, two
  at a time, an absolute judge for all arms.
- H1–H4: the skeleton scheme with Haiku as the filler, run in parallel.
- A Claude Code restart on 2026-10-26 09:17 (not Windows): C3/C4 paused by SIGKILL and resumed; pause time not counted.
- T1–T4 (Opus split, Haiku per task, Sonnet per task) — 4–6 tasks with exact methods, DTOs and contracts, parallel where
  files do not overlap.
- Part 5 (after a consultation): O, S and D, four each, in rounds of three; one fresh Opus review per arm, never per task;
  Haiku fixes; mutation checks frozen before any result and run on all 30 arms.
- The coai review gate was NOT inserted into any arm: the arms are measurement branches that never merge, and all arms
  had to run one pipeline. It applies when an implementation of E5.5/E5.6 goes into the real branch.
- Part 6 (owner, 2026-10-28): a second, larger task (coai #878 and the quota part of #765, frozen plan `6786582e`, 43 scenarios), three
  schemes × three runs (C, CR, and SRS = Sonnet splits, Haiku codes each task, the same fresh Haiku review-and-fix), every arm ending in the
  coai code gate; per-arm random folders, closed other work, and an own coai data folder per arm; the mutation mutants frozen after the run
  on the final code and before they were applied.
