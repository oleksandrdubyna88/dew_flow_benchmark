# T4 t1 — review notes

Reviewed commit `b3dae445` (branch `tmp/T4-t1`) against `vllm/work/T4/t1.md`. Result: no change needed, nothing committed.

## Findings (all "right as it was")

- Only the six task files changed (seed, vectors, `RuntimeCapabilities.cs`, generated TS, `CapabilityMatrixTests.cs`, `capabilityAdmission.test.ts`). Right.
- Seed: `runtimes` ends `"vibe", "kimi"`; the four `why` strings equal the document; three `kimi` rows after `vibe` × `web` in the right order. Every string (`resultRef`, `cells`, `note`, date, version) matches §1 exactly. One record per row, all cells start with `M-K`, nothing about `~/.agents`. Right.
- Vectors: three `kimi` vectors between `vibe` × `web` and `gemini`, text equal to §2. Right.
- `RuntimeCapabilities.Runtimes` list ends `"vibe", "kimi"`; nothing else in the file changed. Right.
- Generated TS: `--check` prints `up to date` (21 rows over 7 runtimes × 3 capabilities). Diff is only the union, `RUNTIMES` and three rows. Right.
- C# tests: `Records` has the Kimi record third, summary clause added; `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows` equal §5 b/c and sit right after the Vibe test. Right.
- TS tests: line 143 list and `kimi rows admit flagged, web refuses naming its record` equal §6, placed after the unmeasured-pair test. Right.
- Red reasons: the coder's notes show every red message for steps 1–3, and they match the document (null `Of("kimi", None)`; `Admitted` vs `Refused`; TS list deepStrictEqual and "not a runtime … can launch"; `TheVectorsActuallyLoaded…` naming `kimi × none`). I did not re-run them at `b4e2ea96`.

## Test results (my run)

- Build (`-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- `CoaiMcp.Tests.CapabilityMatrixTests`: 39 of 39 green.
- `npm test`: 6270 pass, 1 fail — the known `the baseline names no release that was never tagged`. The three kimi vectors and the new kimi test are green.
- `npm run typecheck`, `npm run lint`: pass.
- Not run (as ordered): the whole MCP suite and `dotnet format`. The coder reported only the two known MCP failures, and `dotnet format` on the whole solution fails in `src_server` (IL2026/IL3050), files this task did not touch.

Note: on this Linux host the test binary is `CoaiMcp.Tests` (no `.exe`).
