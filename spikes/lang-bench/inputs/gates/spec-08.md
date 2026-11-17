# The gate of task 08's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/08-ledger-api.md`, for the bench. The spec is on the branch `spec/08-ledger-api` of the task
repository at `ff9d28c` (step A ended at `b8895a8`; the one edit of the gate is the Gate verdict row), sha256
`8781108fd9eda6da0597038cc51e0373d1726086c4d986bccb51385c0c7702e9`. It merged as the task repository's PR #10 (`21a123a`, the squash of `ff9d28c`, the same text);
`explore.json` pins `21a123a`. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs --needles`, FORMAT at `c204726`, on `ff9d28c`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** The package of task 08 ran on the held-out material (2026-11-13, run 1) on the spec as it was before this
gate: both references clean in 10 of 10 passes, all 52 witnesses caught, the slowest load scenario about 3 s against a load duration of
540 s, the chunk-parse time 0.0 ms. Fresh-clone checks on 2026-11-16 (conformance, both leak checks, the line scan, the record
and pin checks) all exited 0. The held-out cases changed after both document rounds (below), and a later package run (run 6)
caught 47 of 52 witnesses until the witness set was corrected to the changed cases. The package runs again on the pinned text and
the final cases before it is registered. That run is recorded here when it is made.

## The bias question (D25), 2026-11-16 and 2026-11-17

Two document rounds, because the first may have been cut short.

### Round 1, 2026-11-16 (typed in as text)

**What went to the vendors.** The spec and the whole suite, the visible samples and the held-out cases, as one text of 133 403
bytes, built in a scratch folder outside every repository (mode 700), then typed in as text through a helper, never from git. The
trap table was left out (D25). Long literals and long runs of repeated lines were shortened in that copy only; default JSON
content-type headers were left out and long JSON bodies were cut. The helper reported that its copy was cut short early, but
the reviewers cite cases near the end of the suite, so the copy reached at least that far; it may still have been cut short.
This round is therefore not taken as a read of the whole suite, and round 2 repeats it from the file. The question: does any
requirement or check favour or disadvantage one stack through that language's or runtime's specifics; answer for C# and for Node
separately; does every held-out check test behaviour the spec states; is any held-out expectation wrong against the spec.

**Document gate** (coai `review_document`, document `task08_bias_review`): `proceed`, 4 gating of threshold 5, 4 of 4 reviewers
(two vendors, each a reviewing and a summarising reader).

**Findings and decisions.**

1. Two cases open accounts whose opening balances together exceed the largest money value (R32, R27). Accepted: the two cases were changed,
   and two cases were added; the package ran again.
2. A case's transcript holds only the readiness line and the exit; no output is judged (R28). Declined: the finding rests on a
   wrong reading.
3. A case was said to compare reply bodies by the wrong relation. Declined: its bodies are all compared as JSON (R16).
4. The memory value, the heap settings and the deadlines favour one stack. Declined: they are the bench's, the same for both
   stacks.

### Round 2, 2026-11-17 (complete, through a file path)

**What went to the vendors.** The spec and the whole changed suite as one text of 141 516 bytes, built in a scratch folder
outside every repository (mode 700) and given to the gate as a file excluded from git and removed after the round. Same
shortening, same question, same document name, a new review. This round read the whole file.

**Document gate** (coai `review_document`, document `task08_bias_review`, a new review): `proceed`, 3 gating of threshold 5,
4 of 4 reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** No reviewer named a requirement or check that favours either stack. The tests run under
  a German locale and the spec fixes how every number is written, so a .NET program must write and read numbers without
  regard to the locale: a stated requirement, the same for both stacks.
- **Node.** The reviewers named no bias. Exact integers and large money values need exact integer parsing and formatting, which the
  spec states.
- **Held-out checks against the spec.** Three findings, all accepted; below.

**Findings and decisions.**

1. A case asked about ids that never exist, but asked after accounts were created; ids that the service issues are its own to
   choose (R4 and R5 make uniqueness per type), so a legal service could have issued one. Accepted: those questions now come
   before the first creation.
2. The same case asked a cross-type question (an id of one type read as another type), which uniqueness per type also allows.
   Accepted: the cross-type questions were removed.
3. A replay case sent a body with an extra member, outside what R14 and R27 allow. Accepted: the body has the same members in a
   different byte form, so the reply is a conflict by R9's byte rule.

**Team lead's decision, kept.** Some cases use fixed, implausible ids as unknown accounts inside request bodies. No realistic id
generator (random identifiers, a counter from zero or one) issues them. This is a decision of the team lead, not an exclusion
from the hidden suite.

**Consultants** (coai `ask_consultants` `d8443410`; 2 of 2 answered). Both: agree with every decision.

**Declined:** round 1 findings 2 to 4, reasons above. **Not re-gated:** the changed cases after round 2, and the one edit of the
spec (the Gate verdict row, `ff9d28c`).

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in protocol, comparison
and limits; the findings are cases that went past what the spec's uniqueness and byte rules allow, and two cases whose opening balances
broke the largest money value.

**Code round over `ff9d28c`** (coai `review_code`, 2026-11-17): `proceed`, 0 findings, 6 of 6 reviewers. The Gate verdict
row is the only edit in the gate commit.

**Checks on `ff9d28c`** (2026-11-17; a fresh single-branch clone with `origin/main` at `de42de9`; the bench at `b348bb33` with
these records): the conformance check exit 0 (all eleven checks, with the private needles); `explore.mjs leakcheck
--task-repo <clone>` exit 0 over both repositories (the bench's 4614 files and its history since `de2d964c`, the task
repository's 26 files and its branch history); the private line scan of the branch history exit 0 (958 added lines, 0
other hits); `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` and `pin-check.mjs` exit 0;
the push check of the task repository passed. The spec's pull request is the task repository's #10.