# The gate of task 04's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/04-rule-api.md`, for the bench. The spec is on the branch `spec/04-rule-api` of the task
repository at `dc21a4c` (step A ended at `01999d5`; the gate commit is `79008ff`, narrowed by `dc21a4c`), sha256
`42d0f6c9d449e7110eb59d2f3a322be30e7330a8ef1a61fda5fec40e95360956`. It is not on `main` yet; the spec's pull request and the
`explore.json` pin are still to come. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs --needles`, FORMAT at `c204726`, on `dc21a4c`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** The package of task 04 ran on the held-out material (2026-11-13, run 1) on the spec as it was before this
gate: both references clean in 10 of 10 passes, all 36 witnesses caught, the slowest load scenario 744 ms (the F-LOAD bound is
570 s; the load duration 540 s), the chunk-parse time 0.0 ms. Fresh-clone checks on 2026-11-16 (conformance, both leak checks, the line scan, the record
and pin checks) all exited 0. Some held-out cases changed after this gate's review (below), so the package runs again on the
pinned text and the changed cases before it is registered. TODO: that run.

## The bias question (D25), 2026-11-17

**What went to the vendors.** The spec at `01999d5` and the whole suite, the visible samples and the held-out cases, as one text
of 147 102 bytes, built in a scratch folder outside every repository (mode 700) and given to the gate as a file excluded from git
and removed after the round. The trap table was left out (D25). Long literals and long runs of repeated lines were shortened in
that copy only; default JSON content-type headers were left out and long JSON bodies were cut. A first try through retyped text
was stopped before anything reached a vendor. The question: does any requirement or check favour or disadvantage one stack
through that language's or runtime's specifics; answer for C# and for Node separately; does every held-out check test behaviour
the spec states; is any held-out expectation wrong against the spec.

**Document gate, one round** (coai `review_document`, document `task04_bias_review`): `good_enough`, 8 gating of threshold 5,
4 of 4 reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** No reviewer named a requirement or check that favours either stack. The tests run under
  a German locale and the spec fixes how every number is written, so a .NET program must write and read numbers without
  regard to the locale: a stated requirement, the same for both stacks.
- **Node.** The reviewers named no bias. Exact 64-bit integers need exact integer parsing, which R2 states. Team lead's note:
  telling a fractional JSON number sent for an integer (an argument failure, R26) from a whole number needs the number's raw
  text in Node, because plain parsing returns a whole number for a fractional text with a zero fraction; Node's parser gives
  the source text to a reviver, and R2's exact integers already force that kind of parse. R26 has not changed since step A.
- **Held-out checks against the spec.** The reviewers found several held-out checks that the spec's text did not cover or that
  expected a wrong value; the findings are below.

**Findings and decisions** (each finding was checked again by an independent judge model).

1. A load scenario was said to expect half of what its requests produce. Declined: the reviewers miscounted the increments per
   rule; the stated workload and the expected final count agree. Two reviewers made this finding.
2. A case assumed that tokens the service picks were never issued earlier; a service may legally issue a fixed token that the
   case later asks about as unknown, and R24 then requires the saved reply. Accepted: the unknown-token questions now come
   before the first creation (suite change).
3. R17 and R30 exclude two input shapes (a set step without a value, a call step whose arguments are not an array) that cases
   test and expect to fail under R21. Accepted: R17 now names exactly those two departures and defers to R22's order of
   failures (`79008ff`, then `dc21a4c`).
4. R36 said a number meant as an integer has no fraction part, while cases send fractional numbers for integers and expect an
   argument failure. Judged partly right: accepted as a wording gap. R36 now says such a number is an argument failure (R26)
   and may occur (`79008ff`).
5. A concurrency case opened nine connections although R35's limit is eight. Accepted: the case now opens eight (suite change).
6. A concurrency case did not bind each read-back to the result of its own request, so a service that returned another
   request's saved result would pass. Accepted: each read-back is bound to its own captured result (suite change).
7. The load scenario's workload and its expected count disagree (the same as finding 1, from the other reader). Declined, as 1.
8. Held-out scenarios send inputs that R30 says never occur: an empty name and nine connections. Accepted: the empty names are
   removed from the case; the connection count is finding 5 (suite change).

**Consultants** (coai `ask_consultants` `3a46760d`; 2 of 2 answered). Both: agree with every rejection and every fix. One: word
R17 to defer to R22, which was then done in `dc21a4c`. The other: the note on Node's raw number text above; no change.

**Declined:** findings 1 and 7, reasons above. **Not re-gated:** the spec edits of `79008ff` and `dc21a4c` (R17, R36, the Gate
verdict row; the conformance check ran on them, and a code round read them) and the changed held-out cases. No second document
round ran on the changed cases.

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in protocol, comparison
and limits; what the reviewers found were cases that stepped outside the spec's stated input bounds, and two places where the
spec's text did not cover what the cases test.

**Code round over `79008ff`** (coai `review_code`, 2026-11-17): `proceed`, 3 findings, all one: the new wording "arguments may be
any JSON value" also made a valid array a failure. Accepted and fixed in `dc21a4c`. **Code round over `dc21a4c`**: `proceed`,
0 findings, 6 of 6 reviewers.

**Checks on `dc21a4c`.** TODO: not run yet. The spec's pull request, the fresh single-branch clone checks (conformance, leak
check over both repositories, the private line scan of the branch history, the record check, the pin check) and the package run
on the pinned text come after this record.
