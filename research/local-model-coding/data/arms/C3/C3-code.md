# C3 coder notes — E5.5 and E5.6 (Sonnet, 2026-10-26)

Branch `exp/kimi-e56-classic-3`. Commits: `feat(kimi): … E5.6` (matrix, planner arm, flag refusal), `test(kimi): … E5.5`, `test(kimi): … S5.6.7`,
`docs(plan): … deviations 50-58`. No push, no `kimi`/`vibe` run, no research/ file touched.

## Instruction loading
Read AGENTS.md, ENTRY.md, PROJECT.md, README.md, the first 704 lines of `research/architecture.md` (the rest, lines 705–1505, NOT read —
time), and in full: `csharp.doctrine`, `common.test-leftovers`, `common.scenario-tests`. The other ~30 rules the resolver selected
(`common.testing`, `common.reuse-first`, `common.git-workflow`, `typescript.doctrine`, `common.generated-code-tests`, …) were NOT read in full.
Instruction loading is therefore INCOMPLETE by ENTRY.md's own standard. The coai review gate was not run (not asked; the brief names no gate).

## Per scenario
| Scenario | Red seen | Red message vs the table | Green |
|---|---|---|---|
| S5.5.1 | by break (HomesFolder empty) | differs: the table's red was the pre-E5.4 one | yes |
| S5.5.2 (2 arms) | by break (OneShotHome bare deleter) | as the table | yes |
| S5.5.3 | by break (finally skipped on cancel) | new wording | yes |
| S5.5.4 | by break (KimiHomesDisposed skipped) | as the table | yes |
| S5.6.1 | yes | as the table | yes |
| S5.6.2 | yes (vectors missing) | the loader's own message | yes |
| S5.6.3 | yes | first red differs (matrix refused first); then as the table | yes |
| S5.6.4 | yes | "Admitted vs Refused" | yes |
| S5.6.5 | yes | first red differs; then as the table | yes |
| S5.6.6 | yes | first red differs; then as the table | yes |
| S5.6.7 (3 arms) | by break (bare deleter; Release; Remove(scratch)) | as the table | yes |
| S5.6.8 (TS) | yes (generated file restored) | as the table | yes |

Why S5.5.x and S5.6.7 were green on arrival: E5.4 (`KimiConsultant`) and E5.3 g (junction-first one-shot disposal) are already in. Details: plan deviations 50–58.

## Decisions
- Seed rows: none/disk `unconfined`, web `unmeasured`; date 2026-10-13; cells as in plan deviation 52. Notes carry the "WebSearch/FetchURL announced and unmeasured" sentence (M-K17 decides).
- `MustCarryNoFlag` in `KimiConsultant.Build`: refuses a plan with `Leading` or `Flags`; `AddDirs` not judged.
- The generator ran only for `runtimeCapabilities.generated.ts`; no hand edit.
- S5.6.7's refused arm: root outside a Git work tree (a copy refusal).

## Results
- Build: 0 warnings, 0 errors.
- Whole MCP suite: 10555 total, 10469 passed, 84 skipped, 2 failed — exactly the two known failures
  (`KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`). No new failure.
- `dotnet format --verify-no-changes --no-restore` on the whole slnx prints IL2026/IL3050 errors for `src_server` (AOT analyzers on this host, untouched files);
  limited with `--include` to my 7 changed C# files: clean.
- `generate-runtime-capabilities.mjs --check`: up to date (21 rows, 7 runtimes).
- Extension `npm test`: all pass except the known `the baseline names no release that was never tagged` (no tags in this clone).
- `plan-lifecycle.mjs`: clean.

## What remains
- Review by Opus (the plan's rule). Live checks (J2), M-K17, RESULTS §31: out of scope.
- E5.7's `consultant.test.ts` is already merged on this base; nothing owed there.
- Rest of `research/architecture.md` and the unread rules (see above) — a reviewer should read them.
- The three module docs (`research/module_*.md`) are out of scope and not updated for E5.5/E5.6.
