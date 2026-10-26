# C3 fix notes — E5.5 and E5.6 review (Sonnet, 2026-10-26)

Review verdict was `ready`: 0 blocking, 0 major, 2 minor. Both minor findings were checked against the code. Both are right and cheap, so both are fixed.
Both are gaps in a test, not defects in product code. So "red first" was shown by a mutation of the product code, as in the coder's notes.

## R1 (minor) — S5.6.7 never tests a refusal AFTER the one-shot home is made — FIXED
- Checked: right. `KimiLaunch.TakeAsync` makes the home in `Prepare` (`KimiHome.Made`) and can refuse later in `VerdictAsync` (the `--version` probe).
  Deviation 55 pointed to S5.4.16, which is about a conversation's `coai-kimi` home, not a one-shot `coai-vh` home.
- Change: `PlayedKimi` got a `Version` seam (default is the old measured answer). `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` got a fourth arm,
  "refused by the launch control": the probe answers exit 1. The test checks that `coai-vh\<8>` held the home when the probe ran (so the arm is not empty),
  that the shim never ran, and — through the shared `TheRowLeftNothingBehind` — that `coai-vh\<8>` is empty, the deleter's recorder was never called and the person's folder is unchanged.
- One thing the finding did not say: this ending is a row status `failed` ("not started: … answered no version"), not `refused`. The first run of the arm was red for that reason, and the
  test now expects `failed` and the reason text. (The test expectation was wrong, not the product.)
- Red for the right reason: mutation — `Release(homes)` removed from `QuestionRowLaunch.RunAsync`'s `finally`. The new arm failed with
  "Expected … OneShotFolder to be empty because coai-vh\<8> is deleted on every way out, but found … coai-vh/d28c3b05". The mutation was reverted.
- Deviation 55 in `todo/PLAN_vibe_and_kimi_runtimes_E5.md` was changed to match.

## R2 (minor) — the AGENTS.md fixture is never asserted, and the prompt check only looks for the cwd — FIXED
- Checked: right. `src/AGENTS.md` was in the fixture, but nothing read what the copy held, and `ThePromptNamesTheCopy` only checked for `WorkingDirectory`.
- Change: the `During` callback of `PlayedKimi` (it runs in all three shim-reaching arms, also on cancel) lists the files under the shim's working directory.
  `ThePromptNamesTheCopy` now asserts: one copy folder (`snapshot-<id>`), `Mailer.cs` in it, no `AGENTS.md` in it, the prompt contains `Path.Combine(cwd, <copy folder>)`, and not the original root.
  The "refused before the launch" arm also asserts that no `--version` was asked, so no home was made (deviation 55's claim is now tested).
- Red for the right reason: mutation — `"AGENTS.md", "agents.md"` removed from `SnapshotExclusions.Default`. The answered and cancelled arms failed with
  "Expected copied {…/Mailer.cs, …/AGENTS.md} to not have any items matching … AGENTS.md". The mutation was reverted.

## Commits
See `git log`: one `test(kimi)` commit for the two test files and one `docs(plan)` commit for deviation 55.

## Results
Filled in at the end of the run — see the section below.
