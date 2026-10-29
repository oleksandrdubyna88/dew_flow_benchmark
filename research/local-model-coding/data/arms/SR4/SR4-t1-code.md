# SR4 t1 — Kimi rows in the capability table (coder notes)

Branch: tmp/SR4-t1. Commit: 3adb74b4. Task document: /home/jinx/git/coai-exp/vllm/work/SR4/t1.md

## What I did

1. Red first. I added the two C# tests (`KimiHasThreeRows_EachCitingItsCells`,
   `AdmissionFlagsBothRows_AndRefusesTheWeb_NamingItsRecord`), the `kimi` entry in
   `Records`, and the line-273 substring. I also added the TS test
   `kimi rows admit flagged, web refuses naming its record`, and the two TS substring
   edits (`RUNTIMES` list, the gemini regex).
2. Seed JSON (`shared/runtime-capabilities.json`): the `runtimes` list, the `why` element,
   and the three `kimi` rows at the end of `rows`. Then `Runtimes` in
   `RuntimeCapabilities.cs`.
3. Vectors (`shared/capability-matrix-vectors.json`): three `kimi` vectors after the vibe
   vectors, before gemini.
4. TS: ran `src_vs_code/scripts/generate-runtime-capabilities.mjs`. `--check` exits 0.

## Red messages (watched)

| Test | Doc says | Actual |
|---|---|---|
| `KimiHasThreeRows_EachCitingItsCells` | `…Builtin.Of("kimi", Capability.None) not to be <null>` | `Expected row not to be <null> because kimi × None is a row of the table.` Same failure, different wording. |
| `AdmissionFlagsBothRows…` | `…'kimi' is not a runtime…` | `Expected type to be …Admission+Admitted, but found …Admission+Refused.` The reason text was not printed in the message. Same cause. |
| `AnUnknownRuntimeOrCapability…` (line 273) | red on the new substring | Red as expected. |
| `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (after step 2) | names `kimi × none`, `kimi × disk`, `kimi × web` | Names `kimi × none` only: the test stops at the first missing pair. The other two are not reached. |
| TS `the words are the file’s words…` | deepStrictEqual on the runtimes list | Red as expected: `'kimi'` missing from actual. |
| TS `a refusal says what cures it…` | regex on the gemini reason | Red as expected. |
| TS `kimi rows admit flagged, web refuses…` | (new) | Red: `false !== true` (kimi not generated yet). |
| TS `the baseline names no release that was never tagged` | known tag failure | Failed, as the document says. Not mine. |

## Green

- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.CapabilityMatrixTests`: 39 of 39 passed.
  The red run showed 36 tests in the same class and no test was added between the two
  runs. I did not find the reason for the change in count.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.ConfinementPlannerTests`: 31 of 31 passed.
  The planner test that the document warned about did not go red.
- Extension: `npm run typecheck` exit 0, `npm run lint` exit 0.
- Extension `npm test`: 6270 pass, 1 fail. The one failure is
  `the baseline names no release that was never tagged` (known, no git tags).
  The three `capabilityAdmission` tests named above all pass.
- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 errors, 0 warnings.

## Notes on the run

- On Linux the test binary is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests`, with no
  `.exe`. The document's `.exe` path does not exist here. I ran the binary without `.exe`.
- The document says "No commit" (header, Out of bounds). The coder brief from the
  orchestrator says to commit with a conventional message and never push. I followed
  the brief and committed the six files by name, one commit: 3adb74b4. Nothing was pushed.
  If the document's rule is the real rule, this commit can be reset with
  `git reset --soft HEAD~1`.
- Instruction loading: the resolver `check` and `explain` ran. Several selected rules were
  large (over 32 KiB together), so I read the test, C#, TS and git rules one by one. I did
  not read the other selected rules (for example the coai question-consultant rule). The
  document also names only its own files, so its scope and the entry procedure differ a
  little.

## Left undone

Nothing in t1 scope is left. Not run: the whole MCP suite (the document says not to).
