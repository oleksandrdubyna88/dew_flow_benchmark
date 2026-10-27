# O3 t5 — the E5 plan: status and as-built deviations (E5.5–E5.6)

Commit: `f0ad3f76` on `exp/kimi-e56-opus-split-haiku-3` (not pushed). File: `todo/PLAN_vibe_and_kimi_runtimes_E5.md` only.

## Done
- Status line (`:3`): `E5.5–E5.6 not started` replaced with `E5.5–E5.6 implemented 2026-10-27 (deviations 47–54, *E5.5–E5.6 as built*), not yet reviewed`.
  The sentence `The live check and question rows (J2, RESULTS §31) are not run.` added as a line of the status block (after the E5.7 paragraph).
- New section `### E5.5–E5.6 as built (2026-10-27)` after `### E5.7 as built`, before `## Plan gate`. Deviations 47–54, one bullet each.
- `node .agents/conventions/tools/plan-lifecycle.mjs`: clean (exit 0).
- `rules.mjs check` resolved before editing.

## Deviations from the contract text (the document asked for facts the t1–t4 notes do not support)
- Contract 50 said S5.6.5 was green on arrival. t2's notes say it was NOT: its `disk` row failed until the planner arm existed (its `none` row was green). The plan now says so (bullet 50).
- Contract 52 said the refused arm of S5.6.7 is a changed sign-in slot key (S5.4.16's way). That arm is RED on the branch (t4): a fresh question row answers, because `KimiHome.Config` (`KimiHome.cs:239`) checks config only against an existing home. The plan says so (bullet 52). I did not change the test.
- Contract 50 said the refused plan checks `Leading` too. Production does (`KimiConsultant.MustCarryNoFlags`); the test sets `Flags` only. The plan says so (bullet 50).
- Status line: I kept the contract's wording exactly. A reader of that line alone will not see that S5.6.7's refused arm is red; bullet 52 says it. Consider adding that to the status line on review.
- Bullet 51 adds one change t1 made and the document did not name: a Records entry in `CapabilityMatrixTests.cs`.

## Red messages checked against the notes
- t3 (S5.5.1–S5.5.4): all green on arrival; the teeth breaks and reds are in bullet 47. Break (a)'s red came from the production refusal, not the `--cwd` assertion the document predicted.
- t4 (S5.6.7 refused arm): red, quoted in bullet 52.
- t2: S5.6.5's disk-row red and S5.6.6's red as the document predicted. S5.6.6's red matches the document.

## Not done / open
- The S5.6.7 refused arm needs a decision (name another refusal, or drop the arm). Until then E5.6's "both suites green" in the Done-when is not met.
- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` is not on the known-failure list. Not checked against a pre-change tree. Owner of that test needs to look.
- The `Leading` refusal has no test (bullet 50).
- No whole-suite run here (the task said not to). Test results in the plan come from the t1–t4 notes, not from a run by t5.
- The live check (J2) is not run.
- Tree was clean before the edit; only the plan file is in the commit.
