# The gate of task 10's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/10-dag-scheduler.md`, for the bench. The spec is on the branch `spec/10-dag-scheduler` of
the task repository at `b72f8af` (step A ended at `d85a672`), sha256
`bea0388c3b6bf75931e1efc053274f34ccc62eb211855cd8d58e1a172ca8e21b`. It is not on `main` yet; the spec's pull request and the
`explore.json` pin are still to come. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs --needles`, FORMAT at `c204726`, on `b72f8af`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** The package of task 10 ran on the held-out material (2026-11-13, run 1) on the spec as it was before this
gate: both references clean in 10 of 10 passes, all 42 witnesses caught, the slowest load scenario 304 ms (the F-LOAD bound is
570 s; the load duration 540 s), the chunk-parse time 0.1 ms. Fresh-clone checks on 2026-11-16 (conformance, both leak checks, the line scan, the record
and pin checks) all exited 0. No held-out case changed after this gate's review; the package runs again on the pinned text before
it is registered. TODO: that run.

## The bias question (D25), 2026-11-17

**What went to the vendors.** The spec at `d85a672` and the whole suite, the visible samples and the held-out cases, as one text
of 93 724 bytes, built in a scratch folder outside every repository (mode 700) and given to the gate as a file excluded from git
and removed after the round. The trap table was left out (D25). Long literals and long runs of repeated lines were shortened in
that copy only. A first try through retyped text was stopped before anything reached a vendor. The question: does any
requirement or check favour or disadvantage one stack through that language's or runtime's specifics; answer for C# and for Node
separately; does every held-out check test behaviour the spec states; is any held-out expectation wrong against the spec.

**Document gate, one round** (coai `review_document`, document `task10_bias_review`): `proceed`, 1 gating of threshold 5,
4 of 4 reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** No reviewer named a requirement or check that favours either stack. The tests run under
  a German locale and the spec fixes how every number is written, so a .NET program must write and read numbers without
  regard to the locale: a stated requirement, the same for both stacks.
- **Node.** The reviewers named no bias beyond the finding below. Exact integers need exact integer parsing, which the spec
  states. Team lead's note: Node closes the write side of a socket when the client half-closes. R5 now states the half-close
  behaviour, so that default is a stated requirement, not a hidden trap, and it is achievable in both stacks.
- **Held-out checks against the spec.** One reviewer-found case needed a clearer requirement; the finding is below.

**Findings and decisions** (each finding was checked again by an independent judge model; both were judged right, as spec
wording).

1. R5 left unclear what a client's shutdown of its sending side does to requests that were already complete: a case sends two
   complete requests and a report request, half-closes, and expects the replies with the clock advanced, while R5 could be read
   as "a connection that disconnects while replies are unsent leaves the DAGs and the clock unchanged". Accepted and applied
   (`b72f8af`): R5 now says a half-close after the last complete request is not a disconnect, that every complete request that
   arrived before it is processed and answered as R8 says, and that the program may close the connection after the last reply.
2. Not gating. R3 said the tests always set SLOTS, while R22 and a case need it absent (the default of 1). Accepted and applied
   (`b72f8af`): R3 now says the tests may set SLOTS and CLOCK.

**Consultants** (coai `ask_consultants` `a67ae24d`; 2 of 2 answered). Both: agree with every decision; the Node half-open default
is now a stated requirement.

**Declined:** none. **Not re-gated:** the edit of `b72f8af` (R3, R5 and the Gate verdict row; the conformance check ran on it, and
a code round read it). The held-out cases did not change.

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in protocol, comparison
and limits; the two findings are two places where the spec's wording left a case's expectation open.

**Code round over `b72f8af`** (coai `review_code`, 2026-11-17): `proceed`, 0 findings, 6 of 6 reviewers.

**Checks on `b72f8af`.** TODO: not run yet. The spec's pull request, the fresh single-branch clone checks (conformance, leak
check over both repositories, the private line scan of the branch history, the record check, the pin check) and the package run
on the pinned text come after this record.
