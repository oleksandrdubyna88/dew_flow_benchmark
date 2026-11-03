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
