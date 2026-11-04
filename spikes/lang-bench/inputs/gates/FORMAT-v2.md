# The gate of FORMAT v2 (E1.S9)

The record of FORMAT.md v2's gate, for the bench; the document itself is on the task repository's `main` (PR #2, `40e678fe`, FORMAT
sha256 in `specs.d/format.json`). Append only. No held-out trap content (parent D41).

**Before the gate.** CP1 (`checkpoints.md`) passed with conditions: F1–F5 fixed in the text, F6 decided (`Format version: 2`), F14
decided (the class `daemon-web`, change 6.2, epoch 7). The class check and the bounds test (T-L56, T-L58; bench PR #160) were green on
the draft before the first round, as parent D64 asks.

**Document gate, round 1** (2026-11-04, coai `review_document`, text `4be8450`): `proceed`, 2 gating of threshold 5, 4 of 4
reviewers. All three findings accepted and applied (`969c239`):

1. F-LOAD's `{duration}` "at most 600" contradicted 4.6's bound on the whole test. Now the whole scenario, its start and stop included, stays inside 600 seconds.
2. No rule said a capture cannot read an HTTP header. Now stated: a capture on `http` reads the body only, and a header's value is bound in the transcript.
3. Two `header` lines with one name: now written as two lines, in order.

**Consultant reading** (coai consult `f7dcfbb8`, "is any rule ambiguous for a runner, a spec author or an agent reading a sample?";
closed as solved). Seven rules two careful readers would implement differently, each with a concrete example. All accepted (`0a5a383`):

1. 2.6: what a decimal token is. Now an optional `-`, digits, and optionally `.` and digits; no exponent.
2. 2.6: the order of the normalisations. Now applied in the order of the list.
3. 4.3: `until` occurrences. Now they do not overlap.
4. 4.3: `quiet` with a byte already waiting. Now an unread byte counts, whether it waits from earlier or arrives.
5. 4.4: G while another step runs. Now kept on the runner's own clock.
6. 5.5: a decimal against a byte value. Now it matches a byte value written as a decimal, as `rs_checks.py` does.
7. 4.6: what a matching step is. Now one try to match the rest of the entry from one position, as `rs_fields.py` does.

Items 6 and 7 state what the pinned runner does. The other five decide for the daemon and web path, which is not built yet (part ii-b follows this text).

**Document gate, round 2, on the text after those changes** (2026-11-04, `0a5a383`): `proceed`, 3 gating of threshold 5, 4 of 4 reviewers. All four findings accepted and applied (`9e91d5a`):

1. Standard output read by a step had no bound. Now 256 MiB, with an `overflow stdout` entry.
2. 4.6's `bad-json` looked like a transcript entry. Now too deep a JSON value does not match, and the report says `bad-json`.
3. `capture … json` on a non-HTTP connection was undefined. Now only on `http`, otherwise a format error, as `rs_check.py` does.
4. A capture on `lines <k>` was undefined. Now it reads the whole reply just read.

**Declined:** nothing. **Not re-read after round 2:** the four changes of `9e91d5a` and the header row; the class check, the bounds test and the push check ran on them.

**Reviewers' account** (their notes, in short): a format contract for specs and samples with a detailed grammar and comparison rules, which keeps stack-neutral and leaves the tasks, the hidden tests and the runner's code out.
