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

## 2e. Part 5b (SR, DR) — interim (2026-10-27 10:50–20:03 UTC)

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
  name another arm's note file. The same scan flags Part 5 fixers (O1, O2, O4, S2, S3, S4) touching other arms' note or
  log files (O2 and S2: D2's fix notes; S2 also O2's); not yet checked one by one, so part 5's independence is now in
  question too. The harness gave each arm its own file name but not its own folder; a per-arm folder outside every
  other arm's reach is the fix for any re-run.

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
