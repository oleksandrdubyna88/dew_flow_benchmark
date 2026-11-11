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
