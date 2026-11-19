# Review checkpoints of the language comparison v2, E1

One dated entry per checkpoint reading (CP1–CP4, CP3-B, CP4-B; `todo/PLAN_lang_comparison_v2_E1.md`, *Review checkpoints inside the epic*).
Each is an Opus judge's reading, verified by the team lead; it blocks what it names until it is recorded here. **Append only.** No held-out
trap content (parent D41).

## 2026-11-03 — CP2 (after E1.S8b, `main` `ae144e1f`, PR #153): passes with conditions

**Read:** layer `2cba87c276de32401d451be1ff8585a588a9ec705f71f103511b93b97fa1c6ad`, runner 0.2.4 (manifest
`df118665ee6894f7d22e0b292e0fec99d2b3eb930c06e933b93f06d38c833d68`), memory policy `cgroup-view-own+gchl-90+node-old-space-90`, record
`inputs/exec-probe-2026-11-03-3.json` (exit 0). The pins on `main` are equal to the record: the live layer identity recomputed, every file hash
equal, no file unlisted; `pin-exec-layer.mjs --check`, `pin-runner.mjs --check` and `specs-record.mjs --check` exit 0. Only the final record is
on the pinned layer; every earlier record names another layer and is kept as history.

**Holds:** T-L30, T-L33, T-L34, T-L43 (revised), T-L50 (the runner half), the consult's B1–B7 and B10, and probe line (f) (the delegated scope:
`memoryMax` 268435456, `swapMax` 0, workload exit 0 — no wrapper fallback, `scope-wrap.sh` not built). T-L31 holds for group A (the `exec` half and
TERM to the host pid); its runner step is part ii-b's.

**Findings** (none blocks CP2 or S3 phase A):

- **F1** — a data run checks the layer identity, the policy and the stack, not the runner manifest (`lib/exec-layer/run.mjs:191-198`; the runner is
  the working copy, `lib/exec-layer/prefix.mjs:19`): a runner edit without a version bump would run a gate on an unpinned runner whose record still
  says 0.2.4.
- **F2** — a run without `data: true` or without a stack gets no identity or policy check and no heap keys (`run.mjs:193-198`, `lib/exec-layer/env.mjs:59`).
- **F3** — the false `fork:` line under a CPU load is a printed flag, never a verdict (`runner/rs_olympiad.py:172-178`, `runner/rs_report.py:214-215`).
- **F4** — the netns holder's 5000 ms ready limit (`lib/exec-layer/netns.mjs:11`) fails safe, as exit 3 and no verdict (record `exec-probe-2026-11-02-3.json`, an older layer).
- **F5** — T-L31's runner `signal TERM` step and its per-stack TERM test are not built (guard `test/exec-layer-runner.test.mjs:161`); due in part ii-b
  before B's first D40 gate.
- **F6** — `pids.max` 512 in `prog/` (`lib/exec-layer/scope-entry.sh:27`) is not named in the pre-registration or FORMAT; to be named there, the same for both stacks.
- **F7** — T-L50's renderer half belongs to E3's renderer. **F8** — T-L32's `double.Parse` half is E3's (the probe prints the culture name only).
- **F9** — on the olympiad path a leftover is a lifecycle record, not a case verdict; E3's acceptance reads `lifecycle.leftover`.
- **F10** — the box set-up is inside the wall (`runner/rs_launch.py:4-7`); the empty program's 8 ms is the same for both stacks and recorded.
- **F11** — the stale stamp lock (outside the identity), Node's typed arrays at 256 MiB (T21, recorded) and the home sentinel folders (cleanup only; the
  box cannot see `$HOME`) touch no verdict.

**Conditions.** S3 phase A may start now. **The D40 gates start only when:** (1) the gate calls the layer with `data: true` and a named stack, and
records `treeSha256`, the policy and the stack beside the runner's JSON (F2); (2) the runner manifest is checked against the pin before the first
gate, in the gate harness or in `runCases` — the second changes the layer identity, so it is done before any gate or not at all (F1); (3) a gate's
exit 3 (holder or launch) is re-run and never counted as a verdict, and a `fork` flag alone never fails a case (F3, F4); (4) the leftovers follow-up
S1 (`todo/PLAN_bench_test_leftovers_followups.md`) changes nothing in `lib/exec-layer/` or the modules the identity covers — or the probe is re-run
and a CP2 addendum is recorded here before the first D40 gate (F11). (5) For group B only: F5 before B's first D40 gate.

**Not checked by this reading:** the suites were not re-run by the judge (1389 of 1389 is PR #153's run); the 1-in-160 fork rate; the host's live
`DefaultOOMPolicy`; `rs_pump.py`, `rs_cgroup.py`, `cleanup.mjs`, `case-view.mjs` and `exec-probe/memory.mjs` were not read in full (the memory
assertions were read through the record); B8, B9 and CI. **Verified by the team lead:** F1, F2, F4 and F6 at the lines named.

## 2026-11-03 — CP1 (E1.S8a parts i and ii-a, `main` `4940d1ff`, runner 0.2.4; the S9 draft `docs/format-v2` `28b93ba`, Format version 2.2, not pushed): passes with conditions

**Read:** FORMAT.md v2.2 in full; runner 0.2.4 (manifest `df118665ee6894f7d22e0b292e0fec99d2b3eb930c06e933b93f06d38c833d68`, every file
hash equal to `inputs/specs.d/runner.json`) — the entry, the lexer, both parsers, the end and pair checks, the matchers, the checks, the
olympiad path, the limits and the environment; `research/module_lang_bench.md` § The runner. The draft's three examples are byte-equal to
`test/fixtures/runner/format/`; `--parse-only` passes them and the 24 sample pairs of the six daemon and web drafts (exit 0); no D18 needle
hit, no plan id, none of the ten stack, flag or threshold words.

**Holds:** V1, V2, V6, V8, V10 and V11 agree; V9 agrees at parse time; V12 agrees for the olympiad stream. V3, V4, V5, V7's refusal and
V12's connection bound are FORMAT-only until part ii-b (parent D51), as planned. F-MEMORY names the two causes and the 90 % heap cap in
stack-neutral words and nothing more; every `##` section has one class.

**Findings:**

- **F1** — a check that relates a binding made inside an unordered group to anything outside it is refused by the runner
  (`runner/rs_arrival.py:68-71`); FORMAT 5.5 has no such rule.
- **F2** — `header <str> <str>` is ambiguous when a string expression has several strings; the runner takes one string as the name
  (`runner/rs_lex.py:388-393`).
- **F3** — 6.3's stated failure names the body, but the runner compares status, headers, then body (`runner/rs_match.py:136-143`), so the
  first difference is the `location` header.
- **F4** — 5.6 says memory "stayed within the registered bound as its control group measured it" and prints `peak above`; F-MEMORY v2.2 and
  the runner (`runner/rs_olympiad.py:147-153`) also fail a runtime's own stop below the bound, as `memory limit`.
- **F5** — the runner stops and fails a case past 256 MiB of standard error (`runner/rs_pump.py:156-158`); F-STDERR says it is never judged.
- **F6** — `Format version: 2.2` against E1.S9's `Format version: 2` and `format.json` `version: 2`.
- **F7** — the layer sets `TZ=UTC` and refuses an `env` line on any other base key (`lib/exec-layer/env.mjs:17,69-82`); F-ENV and 4.2 say
  neither.
- **F8** — runner bounds and structure rules that FORMAT does not state: 1,000,000 expanded steps, the end trigger at the top level, no
  capture in a repeat, no repeat in an unordered group, JSON depth 200, a 500,000-step match cut-off, 200 string parts, a real calendar time.
- **F9** — two grammar examples (4.1, 5.6) use the framing of a real protocol that a group B task uses; 6.2 is neutral.
- **F10–F13** — the runner accepts five actual-only words as names; checks before the last entry, a target without `/` and free sample ids;
  no olympiad normalisation (no task of the series uses one); wording and two stale docstring references.
- **F14** — every section is `common`, so after A's freeze the `daemon`/`web` amendment path of parent D45 applies to nothing, while 4 and
  5 serve only daemon and web and run first in part ii-b. A class for sections that serve daemon and web alone (4 and 5) restores the path;
  it changes D45 and the E1.S9 rule, so it is a plan change.

**Conditions.** S9's gate round starts only when: (1) F1–F5 are fixed in the draft, each with a changelog line (FORMAT text only; the
runner and its pin do not change); (2) F6 is decided; (3) F14 is decided. Before A's specs are gated: F7's F-ENV sentence. Before A's
freeze: F8 and F9. In part ii-b or E1.S8: F10–F13.

**Not checked by this reading:** the suites were not re-run; `rs_launch`, `rs_scope`, `rs_cgroup`, `rs_track`, `rs_view`, `rs_layer`,
`rs_status`, `rs_abort` and `rs_assign` were not read in full; the parent's S9 list beyond E1.S9's Files row; the v1 sentences kept; the
needle scan was a word-boundary scan of `inputs/needles-lang.json`, not `push-check.mjs`; the B drafts were parsed, not conformed; no daemon
or web case can run before part ii-b. **Verified by the team lead:** F1, F2, F3 and F5 at the runner lines named. **Decided by the team
lead, 2026-11-03:** F6 — the gated text says `Format version: 2` (the draft revisions 2.1 and 2.2 stay as changelog lines); F14 — a class of
its own for sections 4 and 5, after `ask_consultants` `8b776383` (2 of 2 answers for it), recorded as the parent's change 6.2, which stops
the plan for a whole re-review.

**CP1 addendum, 2026-11-04.** Conditions (1) to (3) held before FORMAT's gate round: F1–F5 fixed, F6 decided, F14 decided (change 6.2, epoch 7, D60). F7, F8 and F9 are also in the gated text, before A's specs and A's freeze, so they do not stay open. F10–F13 stay with part ii-b and E1.S8. The gate: `inputs/gates/FORMAT-v2.md`.

**CP2 addendum, 2026-11-07 (language comparison F1.S1 (f), the rest of T-L64).** The runner changed in comments and docstrings only (`rs_abort.py`: no stack name in a comment or a docstring; the `ast` of every module is equal but for the version constant, `node spikes/lang-bench/runner-ast-proof.mjs --base bf542f1f`: 35 modules, none differ). `RUNNER_VERSION` moved from 0.3.9 (manifest `c5face5602f0f38fb5198fc31fb04ac971d25249481aefa582031693a5c5eb4c`) to 0.3.12 (manifest `c2d4c07215d41b106930555c05b145de243b83dea4b854d732528cd22c896f95`), pinned by `pin-runner.mjs` (0.3.10, manifest `eff03ef9…`, is an intermediate pin of the same work, kept in the ledger; 0.3.12 is the one in `inputs/specs.json`); the layer identity is unchanged (no file of `lib/exec-layer/` moved). The execution probe was re-run through the layer on the new manifest and passed: `inputs/exec-probe-2026-11-07-2.json` (T-L53; `inputs/exec-probe-2026-11-07.json` is the probe of the intermediate 0.3.10; no earlier record is changed). The three `--check` tools exit 0.

**The private line scan, changed (2026-11-10, owner-approved; recorded 2026-11-11).** `leakscan-lines.py` (outside git) no longer reads `refs-NN/samples/` as a needle source, because that folder is a byte-equal copy of the public samples (proved for task 05, 8 of 8 files by `cmp`), and 8 bare protocol lines of task 05's public samples joined its generic list (a ping and its reply, OK, nil, the integer 1, a one-character bulk value, two reads up to the line end; none names a case, trap or answer). Every held-out file is still read, and the public samples folder is still scanned. Before each later spec pull request (tasks 04, 06, 07, 08, 10) the copy is compared with `cmp` again; on any difference it is read as needles again.

**Task 05's spec gate (2026-11-11, F2.S1 P1 and P6).** The bias question at `proceed` (4 of 4 reviewers), the record `inputs/gates/spec-05.md`; the spec merged as the task repository's PR #5 (`de42de9`). For `leakcheck` the owner approved two person-reviewed entries (hashes and reasons, outside git; 133 to 135): a Node server idiom that public bench test programs also hold, and one phrase of R25's public wording that a private note shares.

**F2.S2 (c) for task 05: not needed (2026-11-11).** S = 1,048,588 bytes, the largest reply task 05's package record counts (a 1 MiB value and its 12 framing bytes). Task 05 is a TCP server: the runner calls its chunked-body parser only for HTTP connections (`rs_net.py` `http_ready` → `rs_conn.parse_response`), so no reply of task 05 is ever chunk-parsed. P = 14.1 ms (median of five; an earlier measurement the same day gave 19.3 ms), the current parse of a synthetic chunked body of S bytes in 4 KiB chunks and 64 KiB reads, on CPUs 6-7, by `test/chunk_parse_p.py`. The owner's reading (2026-11-11, after the consultants split): rule (c)'s S counts the bodies the chunk parser reads, so the 12 bytes of TCP framing above 1 MiB do not trigger the build; an interpretation, not a plan change. Nothing is built. Each later task decides with its own S and P.

**Task 05's package registered (2026-11-12, F2.S1 P8).** `explore.mjs package --task 05 --only all --register` exit 0 on run 5: suite 19 cases, 0 problems; the gate at `proceed`; both references accepted, 10 of 10 clean passes each; load green (slowest 163 386 ms, twin 141 ms, 540 s); witnesses 28 of 28 caught (4 race witnesses at 10 passes each). Identity `87c2dc36239f…` in `inputs/packages.json`. Run 4 was red, 27 of 28: one deterministic witness escaped because a held-out case's client read too fast to keep replies queued at its half-close (the case's fault, not the witness's). The case now reads with a short pause; that witness then failed in 20 of 20 local runs while both references passed; recorded in `inputs/gates/spec-05.md` (after the gate).

**Task 05's package sealed (2026-11-12, F2.S1 P10).** `explore.mjs package --task 05 --seal` exit 0 on the second run. The first run stopped with exit 5 before any archive was registered: the owner's backup share refuses hard links, and the key backup was published only by a hard link (fixed in #203: an exclusive create on such a share). Archive `heldout-05/suite.enc`, sha256 `02c460ce6c35…`; 19 cases, 19 traps; tree `b7a892ee4fe5…` and manifest `d342a312e985…`, the same as the registration. The key backup on the owner's share equals the key by sha256 (`keySha256` `2376f5d27937…`). The entry is in `inputs/heldout-frozen.json`; `inputs/packages.json` gains `archiveSha256`, so task 05's identity is now `32cd9fcea32a…`.

**The leak checks' person-reviewed lists for tasks 04, 06, 07, 08 and 10 (2026-11-12, F2.S1 step A).** The five corrected specs reuse their own public sample lines, which hidden cases also use. The team lead read every hit by hand; the owner approved (in chat) the class "lines of a task's own public samples and the spec's own wording", entered by the team lead with counts in the pull request. The private line scan gained a separate reviewed list (72 lines as sha256 with kind and reason, never text). `leak-reviewed.json` gained 342 `line` entries (266 copies of public samples beside the references, 74 hidden lines that are wholly public sample lines, and the two Int64 limits that equal bare answer lines of task 03, agreed with the consultants), 42 `shingle` entries (note phrases that are public spec wording) and one `line` entry for a C# idiom in a bench test (`#207`). The chunk tier needed a third review kind, `chunk` (this pull request), because long hidden lines share 64-character JSON fragments with the public samples and a `line` entry would blind the check to their hidden-only parts.

**The leak check's reviewed list, continued (2026-11-13, F2.S1 step C).** With the `chunk` kind merged (#209), the team lead entered 186 `chunk` entries: 64-character fragments of long hidden lines that are the five tasks' own public sample text (the class the owner approved on 2026-11-12). The owner approved on 2026-11-13 five single-line language idioms that public bench code also holds (the end of a .NET regular-expression constructor, a .NET exception filter, a Node guard for a closed standard-output pipe, Node's HTTP server constructor, the .NET TCP listener constructor) and the class rule that the team lead may enter such idioms when they hold no task name, task constant or rubric edge case, each read by hand, counts in the pull request. The list holds 392 `line`, 133 `shingle` and 186 `chunk` entries (hashes and reasons only, outside git).

**The five specs' gates (2026-11-16 and 2026-11-17, F2.S1 P1 and P6).** The bias question (D25) of tasks 04, 06, 07, 08 and 10, every round 4 of 4 reviewers: 04 `good_enough`, 06, 07, 08 (two rounds) and 10 `proceed`; no reviewer found a requirement or check that favours C# or Node. 19 of 25 findings accepted (6 in the spec texts, 13 in the private suites), 6 declined with reasons; an independent judge model checked each finding, and two consultants per task agreed. The records are `inputs/gates/spec-NN.md`; the specs merged as the task repository's PRs #6 to #10 and `explore.json` pins them. The bias bundles went to the gate as a file in the spec's worktree, excluded from git and removed after the round, because retyping a bundle of 90 to 195 KB as text was stopped by the model's safeguards (the owner's answer of 2026-11-16).

**F2.S2 (c) for tasks 04, 06, 07, 08 and 10: not needed (2026-11-18).** S, the largest expected reply body of each current suite as the package tool records it: 04 1 203, 06 30 196, 07 150, 08 144, 10 98 045 bytes; P, the parse time of `_chunked` over a body of S bytes (`test/chunk_parse_p.py`, five runs): at most 0.1 ms. Every S is far below 1 MiB and every P below 100 ms, so incremental chunk parsing is not built, the same reading as task 05's line (only bodies the HTTP chunk parser reads count; 07 and 10 are not HTTP tasks).

**The five packages registered and sealed (2026-11-18, F2.S1 P8 and P10).** `package --task NN --only all --register` exit 0 for 04, 06, 07, 08 and 10 on bench `main` (`a7009fd1`) with the gated spec pins, the owner having accepted a noisy machine for these runs (other sessions keep it busy; the reason is in each record): every gate, suite, reference, load and witness item green (cases / witnesses: 04 21/36, 06 32/49, 07 25/32, 08 20/52, 10 21/42); then `package --seal` exit 0 for each. `heldout-check --tasks 04,05,06,07,08,10` exit 0: every archive opens with the key, the key's backup on the share is verified. **The memory value is registered** (`explore.mjs register-memory` exit 0): all twelve references accepted at it. `leakcheck` over both repositories with the six tasks sealed exit 0 (P7). The clear held-out folders are closed. The private material is backed up to the owner's share with a sha256 manifest checked there.

**The freeze's dry run (2026-11-18, F2.S2 (e) step 2).** The whole freeze rehearsed for `spec-frozen-B-20261118` on a scratch clone with a local bare remote, nothing pushed to GitHub: seals 6/6 ok; `freeze.mjs plan` exit 0; `check-commit`, `check-local-tag`, `check-tag` and `check-workspace` (tag and base C) ok; conformance 6/6 ok with each task's own needles at the scratch freeze commit (the combined-file refusal a public-constant false positive); 12 workspaces written; `workspace-diff` and `workspaces --check` ok on both scratch workspace commits; `leakcheck` clean on the scratch freeze commit and both scratch workspace commits; `push-check` 4/4 PASS (three branches, the tag in both forms); the private line scan exit 0 at the T1 head and the scratch freeze commit, and exit 0 at the first scratch workspace commit after 4 single-line runner idioms (60 lines) were marked reviewed under the owner's class rule of 2026-11-13; the refused cases 9/9 refused by the right check; a no-baseline history scan found 8 places in 5 old public commits, all note phrases, none held-out, none in new commits. An independent judge model read the log: CLEAN (on the second reading, after the push checks and the line scan were added). The record's first line, read by `github-rulesets.mjs --apply`: `dry run clean 2026-11-18 packages b055d391037d6c7251f01fe4077305e5dad032781c025682c73563afb4ce8ac7`.

**The task repository's rulesets applied (2026-11-19, F2.S2 (e) step 3).** `github-rulesets.mjs --apply --dry-run-record <the record above>` exit 0 under the owner's go of 2026-11-08 (six packages passed, the dry run clean): created `template-frozen` 27592837, `spec-frozen-tags` 27592840 and `spec-frozen-tags-create` 27592845; read back equal three times (the apply's own read-back and two `--check` runs; again after the tag push); the merge settings ok. The consultants (`687f9412`, 2 of 2) advised applying the rulesets and merging T1 that night and stopping before the tag push for the owner's word.

**The freeze commit C and the tag (2026-11-18 and 2026-11-19, F2.S2 (e) steps 4 to 7).** T1 (the task repository's #11: the six specs' `Freeze tag` row, nothing else; `plan`, `check-commit`, conformance 6/6, the document gate `proceed` 4 of 4, `push-check`, `leakcheck` and the line scan clean against its head) merged as C `502b65ad153887464c0c45ebc13e9bbef545c415`. Step 5: `leakcheck` with C as the candidate exit 0 (2026-11-18); the line scan at C exit 0. Step 6: the twelve workspaces from C proven on a scratch W″ `ba8e963917b1281a2290dece5f912415f843b9da`, never pushed (`workspaces --check`, `check-workspace --base C`, `workspace-diff`, `push-check --branch`, `leakcheck` with W″ as the candidate exit 0, 2026-11-18; the line scan at W″ exit 0). Step 7: `check-local-tag` exit 0 and `push-check --tag` PASS; on the owner's word of 2026-11-19 the annotated tag `spec-frozen-B-20261118` (annotation: the six task numbers) was pushed alone (tag object `346ff923e6d65774176ddd7583d76cd0fc2d52fa`); `git ls-remote --tags` shows it, `freeze.mjs check-tag` exit 0. **From the push the freeze was provisional** (2026-11-19) until W was accepted.

**The workspace commit W, and the freeze accepted (2026-11-19, F2.S2 (e) steps 8 and 9).** T2 (the task repository's #12): the twelve workspaces written from the tag, byte-equal to the scratch proof; against its head `workspaces --check`, `workspace-diff`, `push-check --branch`, `leakcheck` (head as the candidate) and the line scan clean; the document gate over the statements' added sentences `proceed` 4 of 4. Merged by squash as W `f3d256e5fe1f774e189a9850056559b39b995a27` (single parent C). On a fresh clone: `check-workspace --tag` exit 0, `workspaces --check` exit 0, `workspace-diff` exit 0, `leakcheck` with W as the candidate exit 0, the line scan from C to W exit 0. **W accepted; the freeze of `spec-frozen-B-20261118` is accepted** (`inputs/specs.d/spec-states.json`, written by `freeze.mjs record`: the three clean `leakcheck` results — C 2026-11-18, W″ 2026-11-18, W 2026-11-19 — `provisionalFrom` and `acceptedOn` 2026-11-19). `inputs/hashes-lang.txt` is W's (700 entries), and `PINNED_TASK_SHA` is W.

**The private line scan's reviewed list during F2.S2 (2026-11-18 and 2026-11-19).** 7 exact field lines of the public registries (a stack name, a case count, a load duration) marked reviewed with the owner's approval of 2026-11-18, which also made a class rule: the team lead may add such public-value field lines of `packages.json`, `heldout-frozen.json` and `memory-limit.json`, exact lines only, each read, counts in the pull request; and 4 single-line runner idioms (60 lines) under the class rule of 2026-11-13; on 2026-11-19 two more such idioms (a Node standard-library import and a JavaScript catch clause) for this pull request's code and tests. The list holds 85 entries (hashes and reasons only, outside git).
