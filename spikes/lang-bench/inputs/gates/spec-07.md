# The gate of task 07's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/07-stream-processor.md`, for the bench. The spec is on the branch
`spec/07-stream-processor` of the task repository at `b46e491` (step A ended at `5760fdb`), sha256
`46f2ffa5129a675c72e5c5159362124c2f92beb75d9d7fcf53941e200b79c2b2`. It merged as the task repository's PR #9 (`67fb437`, the squash of `b46e491`, the same text);
`explore.json` pins `67fb437`. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs --needles`, FORMAT at `c204726`, on `b46e491`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** The package of task 07 ran on the held-out material (2026-11-16, run 4, on a noisy machine that the owner
accepted): both references clean in 10 of 10 passes, all 32 witnesses caught, 25 cases, the slowest load scenario 551.7 s under
the bound of 570 s. Fresh-clone checks on 2026-11-16 (conformance, both leak checks, the line scan, the record and pin checks)
all exited 0. One held-out case changed after this gate's review (below), so the package runs again on the pinned text and the
changed case before it is registered. That run is recorded here when it is made.

## The bias question (D25), 2026-11-17

**What went to the vendors.** The spec at `5760fdb` and the whole suite, the visible samples and the held-out cases, as one text
of 160 629 bytes, built in a scratch folder outside every repository (mode 700) and given to the gate as a file excluded from git
and removed after the round. The trap table was left out (D25). Each case was capped at 20 000 bytes, so only the middle of the
load case was cut; long literals and long runs of repeated lines were shortened in that copy only. The question: does any
requirement or check favour or disadvantage one stack through that language's or runtime's specifics; answer for C# and for
Node separately; does every held-out check test behaviour the spec states; is any held-out expectation wrong against the spec.

**Document gate, one round** (coai `review_document`, document `task07_bias_review`): `proceed`, 3 gating of threshold 5,
4 of 4 reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** No reviewer named a requirement or check that favours either stack. The tests run under
  a German locale and the spec fixes how every number is written, so a .NET program must write and read numbers without
  regard to the locale: a stated requirement, the same for both stacks.
- **Node.** The reviewers named no bias. Exact integers need exact integer parsing, which the spec states.
- **Memory.** Team lead's note: the reference programs peak at 14.3 % of the memory value on C# and 16.6 % on Node. The
  headroom is equal and wide, so the memory rule favours neither stack.
- **Held-out checks against the spec.** The reviewers found one case that read output too late and two wording problems; the
  findings are below.

**Findings and decisions** (each finding was checked again by an independent judge model).

1. R13 and R15 disagree about which keys expire during a flush: R13 makes a key expired when its last window closes, while R15
   asked for expired lines for keys still live after the windows closed, which is literally none; the expected flush output has
   an expired line for each formerly live key. Judged right (wording). Accepted and applied (`b46e491`): R15 now says the flush
   gives the expired line of every key that was live when the flush began.
2. A case sent the termination signal before reading the window and expired lines that its last event causes, although R6
   promises that tests read every such line first. Judged right. Accepted: the case reads those lines first, then pauses and
   signals (suite change).
3. R24 cannot be assessed without the memory rule. Declined: the judge found the claim false. R24 is the shared FORMAT text; the
   memory value is in the task statement, the bench's own, and the same for both stacks.

**Consultants** (coai `ask_consultants` `3ace8450`; 1 of 2 answered, the other row did not answer). The one that answered agrees
with every decision, and the memory figures above are the answer to finding 3.

**Declined:** finding 3, reason above. **Not re-gated:** the edit of `b46e491` (R15 and the Gate verdict row; the conformance
check ran on it, and a code round read it) and the changed held-out case. No second document round ran on the changed case.

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in protocol, comparison
and limits; the findings are one wording conflict between two requirements and one case that read its output too late.

**Code round over `b46e491`** (coai `review_code`, 2026-11-17): `proceed`, 0 findings, 6 of 6 reviewers.

**Checks on `b46e491`** (2026-11-17; a fresh single-branch clone with `origin/main` at `de42de9`; the bench at `b348bb33` with
these records): the conformance check exit 0 (all eleven checks, with the private needles); `explore.mjs leakcheck
--task-repo <clone>` exit 0 over both repositories (the bench's 4614 files and its history since `de2d964c`, the task
repository's 26 files and its branch history); the private line scan of the branch history exit 0 (577 added lines, 0
other hits); `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` and `pin-check.mjs` exit 0;
the push check of the task repository passed. The spec's pull request is the task repository's #9.

**The package on the pinned text (2026-11-18).** `explore.mjs package --task 07 --only all --register` on bench `main` with these
pins, on a machine the owner accepted as noisy: exit 0; the gate green (the spec conforms and is gated); the suite 25 cases,
0 problems; both references accepted, 10 of 10 clean passes each, at the one memory value; the slowest load scenario 247 095 ms
(twin 1 084 ms) against a load duration of 540 s; all 32 witnesses caught. Registered in `inputs/packages.json`, then sealed
(`package --seal`, exit 0); the archive's hash is in the registry and in `inputs/heldout-frozen.json`.

