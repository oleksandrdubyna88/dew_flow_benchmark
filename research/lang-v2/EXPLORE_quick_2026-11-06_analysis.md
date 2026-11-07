# The quick run: what six daemon and web tasks showed (2026-11-06)

> **Exploratory, not evidence.** The owner's two-day run of 2026-11-05: three pairs of bare C# and TypeScript sessions on each of six tasks, on draft specs, on a harness written fast. It is not the verdict of the v2 comparison, and three pairs per task can suggest a pattern but cannot prove one. The per-task notes beside this file (`EXPLORE_task{04,05,06,07,08,10}_2026-11-05.md`) hold every session's numbers. The held-out cases and references stay private.

## What was run

36 sessions: tasks 04, 06, 08 (web) and 05, 07, 10 (daemon), three pairs each, with C# on .NET SDK 10.0.403 and TypeScript 6.0.3 on Node 26.13.0. Agent: CLI 2.1.310, model `opus`, effort `high`, no gate, no plan, no comparison named. Scored by each spec's 4 visible samples and a private held-out set of 18 to 32 cases, on a clean copy of the agent's tree, in a box with a 512 MiB memory limit, on CPUs 6 and 7.

Spend: **$67.01**, plus $6.10 for the re-run of the four olympiad tasks (24 of 24 accepted, no real difference between the stacks). No session was charged twice: the CLI's total, the broker's total and a recomputation from tokens agree on all 36 (`T1-T5`).

## The result

| Task | Kind | Accepted C# | Accepted Node | Median cost C# | Median cost Node | C# cheaper in | Median time C# | Median time Node |
|---|---|---|---|---|---|---|---|---|
| 04 | web | 3 of 3 | 3 of 3 | $1.15 | $0.93 | 1 of 3 | 5.7 min | 3.7 min |
| 05 | daemon | 3 of 3 | 3 of 3 | $1.50 | $2.35 | 2 of 3 | 10.5 min | 31.5 min |
| 06 | web | 2 of 3 | 3 of 3 | $2.71 | $1.18 | 0 of 3 | 17.9 min | 9.1 min |
| 07 | daemon | 3 of 3 | 3 of 3 | $0.85 | $5.58 | 3 of 3 | 5.1 min | 38.8 min |
| 08 | web | 3 of 3 | 3 of 3 | $0.77 | $0.87 | 3 of 3 | 3.5 min | 4.4 min |
| 10 | daemon | 3 of 3 | 3 of 3 | $1.74 | $2.91 | 3 of 3 | 12.8 min | 27.2 min |
| **All** | | **17 of 18** | **18 of 18** | | | **12 of 18** | | |

**Totals, summed over all 18 sessions of each stack (not medians):** cost $25.95 for C# and $41.06 for Node; session time 2.79 h and 5.74 h. Without task 07: $23.02 and $26.22, and 2.49 h and 3.85 h. The sessions used 399 and 643 assistant messages, 0.62 and 0.90 million output tokens, and 20.7 and 49.7 million cache-read tokens.

## What it means, and what it does not

**Quality: no real difference, with one real defect.** All 36 passed every visible sample, and 35 passed every held-out case on first scoring.

**Cost and time: C# ahead on the daemon tasks, mixed on web, and one task carries most of the gap.** Task 07 is $12.34 of the $15.11 total difference. C# was cheaper in all three pairs of 07, 08 and 10, and dearer in all three of 06.

**Why task 07 costs more in Node.** Read from the session streams. The spec sets the most memory a program may use from the reference's peak, and says that limit is never below 256 MB. All three Node agents' first load test went over it (334, 428 and 447 MB), while the first C# tests measured about 80 to 105 MB. The Node agents then rewrote their engines around typed arrays and their own byte output, and spent roughly 21 to 41 calls on load and memory runs against 1 to 14 for C# (a rough count by pattern). Type and compile errors were minor, 2 or 3 per session. Two things are worth keeping apart:
- **The runtime made the first attempt harder:** V8's young generation, off-heap buffers and an idle baseline of about 47 MB.
- **The agents' reading drove the rest.** They treated 256 MB as the limit, though the spec's limit can be higher and the harness enforced 512 MiB. **So this gap partly measures a mismatch between what the spec said and what the harness enforced. It is not all language.**

## The one failure: a real defect that nearly passed

Task 06, pair 2, C#. The program parsed a request body with `JsonDocument.Parse` directly over Kestrel's pooled buffer, then returned the buffer with `AdvanceTo` while the document was still in use. Under concurrent requests another request can overwrite those bytes, and a valid title reads as invalid.

- **It depends on the CPUs.** On the two scoring CPUs the session failed 28 of 40 single-case replays and 19 of 24 whole-set re-scores. On all CPUs it passed 93 of 93, even at load 42. The Node control passed 40 of 40, 20 on each.
- **So a single scoring is a sample, not a measurement, for a race.** This session had about a one-in-five chance of being recorded as accepted.
- **A sweep found no other.** All 36 sessions re-scored 3 times each on the true CPUs (108 runs, no voids): 105 clean, 2 on this session, and 1 on a C# session of task 07 that reported a leftover process at load 22 and passes 8 of 8 alone. That one was a harness fault: a process still exiting was read as a leftover after one 20 ms recheck, now fixed. Three runs is too few to clear a rare race.
- **It is one session.** Nothing here says C# programs are less correct in general, but a multi-threaded server can have this class of defect and a single-threaded one mostly cannot.

## What is wrong with these numbers

- **Agents saw plan ids.** Comments in 5 of the 32 agent-visible runner files named plan items such as `E1.S8b` and `D56`. This breaks the brief. Both stacks saw the same bytes, so I see no bias between them. It is fixed, with a test.
- **Timing is noisy.** The machine was not quiet: load reached 12 to 20 during some sessions, a coder's tests overlapped task 07 pair 2 and possibly the start of task 04 pair 2, and the machine restarted once between pairs. Every session is flagged `started not quiet`. Cost is not affected by load; time is.
- **The specs are drafts.** Some held-out cases are time-sensitive, and several draft specs have known defects. All passed or were read by hand.
- **The harness had bugs.** All were found by review after the run. They can only cause false rejects or an unjustified rescore, and only the one failure above was recorded, which is explained. One session halted during scoring (a 73 s case against a 75 s memory scope) and scored on resume. One rescore was used, on `ec6637`, a false `void: contaminated`.
- **`time.partition.build_test` is not comparable across stacks.** It reads 0 for C# because `bash build.sh` is not recognised. This report does not use it; `explore.buildTest` in the notes is the symmetric number.
  - *2026-11-07, done (F1.S1 (b)):* v1's classifier now counts `bash build.sh`, `bash run.sh` and the visible-samples runner as `build_test`. The partitions were recomputed from the retained streams, one new dated file per series in `partition-recomputed/` (`explore.mjs repartition`), and every recomputed partition sums to its wall. All 36 sessions were recomputed (6 per series, none skipped). C#: 18 of 18 sessions now have `build_test` above zero (was 0 of 18), summing to 2,311 s. Node: 18 of 18 (was 17 of 18, from the `tsc` calls alone), summing to 2,670 s (was 1,279 s). The records themselves are unchanged.

## What this asks of the v2 plan

These go to the plan's whole re-review; none is decided here.

1. **Repeat the scoring.** One pass cannot catch a race, as this run shows. Decide how many passes, on how many CPUs, and what a mixed result means.
2. **Say the memory bound plainly.** Decide what agents are told, and make the stated bound and the enforced limit agree, or differ on purpose and say so.
3. **Fix the classifier** before anyone reads the time partition across stacks.
   - *2026-11-07, done (F1.S1 (b)):* the classifier is fixed and the partitions are recomputed (see the `time.partition.build_test` point above): C# 18 of 18 sessions with `build_test` above zero, 2,311 s in all; Node 18 of 18, 2,670 s in all.
4. **Keep what a reject saw.** This run's reject could not be read until a replay tool existed. The runner now keeps it.
5. **Test on the same CPUs that score.** A diagnostic on other CPUs gave 93 false reassurances. `rescore` and `replay` now use the same pin.
6. **Fix the draft specs' defects** before freezing them.
