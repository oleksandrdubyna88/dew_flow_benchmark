# T3-t5 — review notes (the E5 plan: status line and E5.5–E5.6 deviations)

Reviewed commit `659504c6` against `vllm/work/T3/t5.md`. One commit added by the reviewer (deviations 44–45; hash in the final answer).
Docs only: no build, no test run (nothing to build; the task has no test class).

## Right as it was
1. **Scope.** Only `todo/PLAN_vibe_and_kimi_runtimes_E5.md` changed.
2. **Status block.** The text equals contract 1. The date is 2026-10-26 (today). "(Opus)" is the contract's text; the coder was Haiku, but the contract wording was kept.
3. **Section place and heading.** `### E5.5–E5.6 as built (Opus, 2026-10-26)` is after `### E5.7 as built`, before `## Plan gate`. Numbered from 31.
4. **Bullets 31–34** follow the contract. Checked in code: `AdmissionFlag.Unconfined` text (`CapabilityMatrix.cs:33`), `MustCarryNoFlags` refuses `Flags` and `Leading` (`KimiConsultant.cs:60-67`).
5. **Bullet 34 (which host).** Right: t3 and t4 (coders and reviewers) ran on Linux only; every case skipped; no green observed anywhere.
6. **Bullets 35–43** agree with the t1–t4 code and review notes (S5.6.5 red, `Records` entry, `38ebb5f3` removed the guard — t4's review says so —, `OneShotEntries`, failures list in 40, `dotnet format` in 41, no `.exe` in 42).
7. **`plan-lifecycle.mjs`** → `plan-lifecycle: clean.`, exit 0 (before and after my change).

## Fixed
1. **Missing deviations.** Contract 2 asks for one bullet per further deviation the coders reported. These were not in the section:
   - t1: `:273` and `:166` went red though the spec did not list them; `ConfinementPlannerTests.cs:167` stayed green; the `why` string was split → new bullet 44.
   - t3: `_data` in the constructor, private nested `Scopes`, `Marked()` helper, `Check` returns a tuple, no `fakecli-env` collection → new bullet 45.
   - t4: the `asking` local (a lambda cannot take `.Should()`) → added to bullet 43.
   Status line now says `deviations 31–45`. `plan-lifecycle` is clean after the change.

## Not done / noted
- Commit trailers of the earlier commits name Haiku 5.5 (coder's model); history left as it was.
