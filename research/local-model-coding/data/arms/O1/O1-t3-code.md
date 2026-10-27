# O1-t3 — code notes (E5.5, consultant check on a Kimi row)

Commit: b7826b2b (test file only). Production files: unchanged.

## Done
- New `src_mcp/tests/AKimiConsultantCheckTests.cs`: S5.5.1, S5.5.2 (Theory, answered true/false), S5.5.3, S5.5.4.
- Helpers copied from `ConsultantCheckTests` and `ConsultScenarioBase` (CallingAs, CallerVariables); nothing shared, no edits to other test files.

## Red / green
- First run at b7826b2b: all 5 cases GREEN (the path is already in place, decision 1).
- First attempt failed on my own assertion, not production: `KimiHome.AfterUnlink` fires on EVERY normal unlink of the
  home's `credentials` junction (it is a seam in the loss window), so `BeEmpty` was wrong. Changed to `ContainSingle()`
  ending in `home/credentials` (the one junction). The task document's "recorder never invoked" does not match the
  mechanism; this is a deviation to record.
- Mutation check: `held.Home.Dispose()` in `ConsultantCheck.TurnAsync` disabled -> S5.5.1, S5.5.2 (both arms), S5.5.3 RED.
  S5.5.4 stays green (its home is in the scratch, removed with it). Restored; `git diff` empty.

## Suite (whole MCP suite, after restore)
- total 10539, failed 4, succeeded 10451, skipped 84.
- Known (not mine): `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`,
  `KimiRuntimeTests.ALaunchThatNamesItsHome_...`.
- NOT on the known list: `AReviewerThatAsksForSourceIsAskedAgainTests` — 2 cases (`AConversationThatOutlivesItsCap_...`,
  `AConversationCapOnTheWork_...`). Run alone: 16/16 green. Likely load/timing under the full parallel run; not
  investigated further. Needs a look by whoever owns that class.

## Not done / left
- No production fix needed.
- Peer message (session "vllm fix"): this task did no GPU or vLLM work; no `vllmprtest-*` container touched.
