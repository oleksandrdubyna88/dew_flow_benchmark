# The gate of task 05's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/05-key-value-server.md`, for the bench. The spec is on the task repository's `main` at
`de42de9` (PR #5, the squash of branch `spec/05-key-value-server` at `42481b4`, the same text), sha256
`9b88858ae1ada50b4ffff3973a3db64bba7cf2ae588e61078a479ba6d5b7c4cc`; `explore.json` pins `de42de9`. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs`, FORMAT at `c204726`, on `42481b4`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** One coai plan round (`proceed`) and three code rounds (`proceed`) on 2026-11-10 read the spec's text. The
package of task 05 then ran green on the held-out material (2026-11-11): both references clean in 10 of 10 passes, all 28
witnesses caught, the slowest load scenario 166 s under the bound of 540 s. That run read the spec pinned before this gate; the
package runs again on the pinned text of this gate before it is registered.

## The bias question (D25), 2026-11-11

**What went to the vendors.** The spec at `e935ae0` and the whole suite, the visible samples and the held-out cases, as one text,
built in a scratch folder outside every repository (mode 700) and sent as text, never from git. The trap table was left out (D25).
Long literals and long runs of repeated lines were shortened in that copy only. The question: does any requirement or check favour or
disadvantage one stack through that language's or runtime's specifics; answer for C# and for Node separately; does every held-out
check test behaviour the spec states; is any held-out expectation wrong against the spec.

**Document gate, one round** (coai `review_document`, document `task05_bias_review`): `proceed`, 5 gating of threshold 5, 4 of 4
reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** One reviewer: the German locale of the test environment could make culture-aware integer parsing and formatting of .NET
  differ from the spec. The other: no requirement or check favours either stack.
- **Node.** One reviewer: three runtime defaults differ from what the spec requires — the write side closes when the client
  half-closes, numbers are doubles where the counter is a signed 64-bit integer, and socket writes buffer without limit for a slow
  reader. The other: no bias.
- **Held-out checks against the spec.** One reviewer: every held-out check tests stated behaviour. The other: one held-out check
  compares replies by a relation that the spec's comparison requirement said no requirement names.

**Findings and decisions.**

1. Half-close default of Node (R27). Declined: R27 states the behaviour; naming one stack's socket option in the spec would itself
   favour that stack, and the requirement is explicit and achievable in both (the Node reference passes it).
2. Signed 64-bit counter in Node (R18 to R20). Declined: the spec states the exact range, the integer text and both errors; Node's
   standard library has exact 64-bit integer arithmetic.
3. German locale against .NET parsing and formatting (R38). Declined: R18 and R10 define the integer text and the reply form in ASCII
   digits and an optional leading minus; the locale is the same for both stacks; lenient parsers exist in both stacks.
4. Unbounded write buffering of Node for a slow reader (R26, R34; not gating). Declined: R26 and the memory bound state the behaviour;
   naming a stream API would be a stack-specific hint.
5. A held-out load scenario said to run past the time limit. Declined: the reviewer read the loop nesting wrongly; the pauses total
   150 s, and the slowest load scenario measured 166 s over 20 reference passes, inside 540 s.
6. The comparison requirement said no requirement names another comparison, while concurrent counter replies can only be compared by
   their form and relations. Accepted and applied (`42481b4`): R25 names that comparison, and R31 names R25 as its one exception.

**Consultants** (coai `ask_consultants` `0271bee3`; 2 of 3 rows answered, the third was refused by its own folder limit). Both: decline
findings 1 to 4, because each requirement is explicit, relevant and achievable in both stacks and the spec must not carry one stack's
API; one adds that equal numbers of traps per stack would prove nothing, the argument being a stated, achievable contract applied the
same to both. Both: fix finding 6 now, before the pins move, rather than at the freeze.

**Declined:** findings 1 to 5, reasons above. **Not re-gated:** the edit of `42481b4` (R25, R31 and the Gate verdict row); the
conformance check ran on it.

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in their protocol, comparison and
limits; the stack differences the reviewers name are runtime defaults that the stated requirements already decide.

**Code round over `42481b4`** (coai `review_code`, 2026-11-11): `proceed`, 0 findings, 6 of 6 reviewers.

**Checks on `42481b4`** (a fresh single-branch clone with `origin/main` at `c204726`; the bench at `357e557a` with this record):
the conformance check exit 0; `explore.mjs leakcheck --task-repo <clone>` exit 0 over both repositories, after the owner approved
two person-reviewed entries on 2026-11-11 (a Node server idiom that public bench test programs also hold, and one phrase of R25's
public wording that a private note shares; hashes and reasons only, outside git); the private line scan of the branch history
exit 0; `specs-record.mjs --check` exit 0; `pin-check.mjs` exit 0. The spec's pull request is the task repository's #5.
