# T1-t4 — review notes (E5.5, the consultant check for Kimi)

Reviewed `git diff b4e2ea96..HEAD`: one file, `src_mcp/tests/AKimiConsultantCheckTests.cs`. No production code, no other file.

## Findings
- Class, attributes, fields, `Dispose`, `OnKimi`, `StandIn`, `Scopes`, `Answering`, `Check`, `Text`, `HomeOf`, `After`, `OneShotRoot`,
  `TheHomesAreGone`, `TheDeleterNeverMetTheJunction`: match the document. Right as it was.
- The four tests (S5.5.1, S5.5.2 theory with true/false, S5.5.3, S5.5.4): names, `<summary>` with scenario, and each *Then* match
  the document. The extra `using CoaiMcp.Runners.Acp` is needed for `AcpWords`. Right as it was.
  S5.5.2 also asserts `code == 0`, which the document does not list; it holds for both cases. Kept.
- Bite checks: the coder's notes show the J3 red (S5.5.1) and the `NotBeEmpty` red (all cases). Accepted.
- FIXED — S5.5.3 had a branch "if `Shims` is empty, assert the folder was reserved" with a comment saying a cancelled launch is
  not kept. That is false: `PlayedKimi.ShimAsync` enqueues the launch in `finally` (`PlayedKimi.cs:68-72`), so a cancelled launch is
  always kept. The branch was dead and, if `Shims` were ever empty, it would have passed a weaker check. Replaced by
  `played.Shims.Should().ContainSingle(...)`. The test is green, which shows the single launch is recorded.

## Results (host: Linux, WSL2)
- Build: succeeded, 0 warnings, 0 errors.
- `AKimiConsultantCheckTests`: total 5, passed 5, skipped 0.
- `ConsultantCheckTests`: total 32, passed 30, skipped 2 (Windows-only tests).
