# S3-t1 — code notes (kimi in the capability table)

Commit: f0d3eecc on branch tmp/S3-t1 (not pushed).

## What I did

1. Red first: wrote the two C# tests (KimiHasThreeRows_EachCitingItsCells, AdmissionFlagsBothRows), changed the list at the unknown-runtime test, and wrote the TypeScript test and its two expectations.
2. Changed the runtime list in RuntimeCapabilities.cs and the JSON rows (not the vectors). S5.6.1 and S5.6.4 went green. S5.6.2 went red with the documented sentence.
3. Added the three kimi vectors.
4. Ran the generator, then --check (exit 0). Ran the extension suite.
5. Teeth run: set kimi × none standing to `confined`. AdmissionFlagsBothRows went red. Reverted. Rebuilt. 39 of 39 green again, and --check still exit 0.

## Test results

- CapabilityMatrixTests: 39 of 39 green (final).
- Extension suite: 6290 tests, 6270 pass, 1 fail. The one fail is the known tag test "the baseline names no release that was never tagged". S5.6.8 is green.
- Not run: the full MCP suite, ConfinementPlannerTests (t2 owns it; its AnUnknownRuntime test is expected to be red until t2 lands).

## Red messages that differed from the document

- S5.6.1: the first try gave a NullReferenceException (I dereferenced the null row before asserting). Fixed by asserting `.Should().NotBeNull()` on the call expression. The red now reads: `Expected RuntimeCapabilities.Builtin.Of("kimi", Capability.None) not to be <null>.` — matches.
- S5.6.4: the red reads `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.` The document's text with the refusal reason is not produced by `BeOfType`. Not changed.
- S5.6.8 (TS deep-equal): matched.
- TS kimi test: red reads `AssertionError: kimi × none is admitted` (the test's own message), not the refusal reason from `admit`. Not changed.
- S5.6.2: matched — `the table's kimi × none row must have a vector deciding it`.

## Changes outside the document's steps

- `AnUnknownRuntimeOrCapability_IsRefused_NamingWhatIsKnown` (C#) went red after step 1, because its expected text now lists `kimi`. This is the expected result of the list change; it is in my own file. Green after step 2.
- `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` (C#) went red after step 2: the kimi rows cite a record the test's `Records` list does not know. I added `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` to that list. Data only; the document does not mention it.
- TS `a refusal says what cures it` went red until the gemini reason listed `kimi`. Green after step 2.

## Deviations from the brief

- The brief says the branch is `exp/kimi-e56-sonnet-split-haiku-3`. The worktree is on `tmp/S3-t1`. I committed on `tmp/S3-t1`.
- The brief's rules say "Do NOT commit". The coordinator's message says to commit. I followed the coordinator's message.
- The brief's command path `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe` does not exist on this Linux host (only the .dll). I ran `dotnet ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll --filter-class ...` through low-priority.mjs.

## Left undone

- Nothing in my scope.
- The Linux MCP host failures named in the brief (KimiRuntimeTests, KimiHomeTests) were not run; they are outside my class.
