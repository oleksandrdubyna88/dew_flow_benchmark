# The gate of task 06's spec (F2.S1 P1 and P6)

The record of the gate of `todo/tasks/06-blog-api.md`, for the bench. The spec is on the branch `spec/06-blog-api` of the task
repository at `2527399` (step A ended at `d17d92c`), sha256 `4ce55d325e85f65706190f317735d0c711567763f9ec553c08ec99053fe6b0bd`.
It merged as the task repository's PR #8 (`7f82632`, the squash of `2527399`, the same text);
`explore.json` pins `7f82632`. Append only.
D25's leak rule: no case text, case id or trap name; the verdict, the reviewer count and the findings in words that name no case.

**Checker line** (`node spikes/lang-bench/conformance.mjs --needles`, FORMAT at `c204726`, on `2527399`, exit 0):

`ok format sha256 6c3546bb03cdc70df32a6795b68c52d5c03d0c4efbb968824b1c177cc8cb5d47 (record B, gate inputs/gates/FORMAT-v2.md)`

**Before the gate.** The package of task 06 ran on the held-out material (2026-11-13, run 1) on the spec as it was before this
gate: both references clean in 10 of 10 passes, all 49 witnesses caught, the slowest load scenario 941 ms (the F-LOAD bound is
570 s; the load duration 540 s), the chunk-parse time 0.0 ms. Fresh-clone checks on 2026-11-16 (conformance, both leak checks, the line scan, the record
and pin checks) all exited 0. Some held-out cases changed after this gate's review (below), so the package runs again on the
pinned text and the changed cases before it is registered. That run is recorded here when it is made.

## The bias question (D25), 2026-11-17

**What went to the vendors.** The spec at `d17d92c` and the whole suite, the visible samples and the held-out cases, as one text
of 194 941 bytes, built in a scratch folder outside every repository (mode 700) and given to the gate as a file excluded from git
and removed after the round. The trap table was left out (D25). Long literals and long runs of repeated lines were shortened in
that copy only; default JSON content-type headers were left out and long JSON bodies were cut. A first try through retyped text
was stopped before anything reached a vendor. The question: does any requirement or check favour or disadvantage one stack
through that language's or runtime's specifics; answer for C# and for Node separately; does every held-out check test behaviour
the spec states; is any held-out expectation wrong against the spec.

**Document gate, one round** (coai `review_document`, document `task06_bias_review`): `proceed`, 4 gating of threshold 5,
4 of 4 reviewers (two vendors, each a reviewing and a summarising reader).

**The answer per stack, in words.**

- **C#.** No reviewer named a requirement or check that favours either stack. The tests run under
  a German locale and the spec fixes how every number is written, so a .NET program must write and read numbers without
  regard to the locale: a stated requirement, the same for both stacks. Team lead's note: the default token handler of the C# web stack accepts a token for five minutes after its expiry
  (clock skew). That is a trap for an agent under R7, which states the expiry exactly; it is stated, not a bias.
- **Node.** The reviewers named no bias. Exact integers need exact integer parsing, which the spec states. Team lead's note: the
  default keep-alive timeout of Node's HTTP server is 5 s, shorter than the longest pause a case makes. The runner reconnects once
  on a connection the server closed, so the pause is safe for both stacks.
- **Held-out checks against the spec.** The reviewers found cases that stepped outside the spec's stated input bounds; the
  findings are below. All five were judged right.

**Findings and decisions** (each finding was checked again by an independent judge model).

1. Four concurrency cases opened nine connections (eight clients and one left open) although R46 allows eight and R41 says
   tests never exceed the bound. Accepted: the extra connection is dropped; eight are open (suite change).
2. A case sent a request-body text over the 10 000-character cap of R47, which R41 excludes. Accepted: the case is removed.
3. A case asked for an article by a path segment that is not in the slug form of R18 (R11's lowercase letters, digits, hyphens),
   which R41 excludes. Accepted: the case now uses a path in the slug form.
4. A case with a short-lived token could present it inside the expiry window that R7 excludes, when the machine is slow.
   Accepted: the token's lifetime and the second pause are longer, so every early request stays well before the expiry (suite
   change).
5. Not gating. A case said to test an unset time zone, but the base environment sets it (R50). Accepted: the case's label now
   says the time zone is as the base environment has it; R17 now says to leave it as the base environment sets it (`2527399`).

**Consultants** (coai `ask_consultants` `ee58cb2a`; 2 of 2 answered). Both: agree with every decision. One added the keep-alive
and clock-skew notes above.

**Declined:** none. **Not re-gated:** the edit of `2527399` (R17 and the Gate verdict row; the conformance check ran on it, and a
code round read it) and the changed held-out cases. No second document round ran on the changed cases.

**Reviewers' account** (their notes, in short): the spec and the suite are symmetric for both stacks in protocol, comparison
and limits; the findings are cases outside the stated input bounds and one wrong label.

**Code round over `2527399`** (coai `review_code`, 2026-11-17): `proceed`, 0 findings, 6 of 6 reviewers.

**Checks on `2527399`** (2026-11-17; a fresh single-branch clone with `origin/main` at `de42de9`; the bench at `b348bb33` with
these records): the conformance check exit 0 (all eleven checks, with the private needles); `explore.mjs leakcheck
--task-repo <clone>` exit 0 over both repositories (the bench's 4614 files and its history since `de2d964c`, the task
repository's 26 files and its branch history); the private line scan of the branch history exit 0 (991 added lines, 0
other hits); `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` and `pin-check.mjs` exit 0;
the push check of the task repository passed. The spec's pull request is the task repository's #8.