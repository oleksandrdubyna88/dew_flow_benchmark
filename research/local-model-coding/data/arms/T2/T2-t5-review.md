# T2 t5 review — plan status and E5.5–E5.6 as built

Scope: `todo/PLAN_vibe_and_kimi_runtimes_E5.md` only (the diff d88bd305..63682c45 touches only it). No code, so no build or test classes were run (the task changes none).

## Right as it was
- Status line: bold part exact (`E5.1–E5.7 implemented (E5.5–E5.6 on 2026-10-26); the live J2 rows and RESULTS §31 not yet recorded.`); date is today UTC. Every other sentence kept. The new sentence names 47–53.
- New subsection `### E5.5–E5.6 as built (2026-10-26)` is after item 46 and before `## Plan gate 2026-10-16`. Items 47–52 match the task document word for word.
- Item 53 is true: `CapabilityMatrixTests.Records` gains `RESULTS_kimi_confinement.md` / `2026-10-13` / `M-K` (checked in the diff b4e2ea96..HEAD), and the plan's S5.6.2 does not mention that list. No other invented item. Other differences I checked (extra theory row `kimi`/web in `ConfinementPlannerTests`; Kimi planner `_` arm for none) are behaviour the plan describes, so no new item.
- E5.5 and E5.6 Decisions and scenario tables are unchanged.
- `plan-lifecycle.mjs`: clean.

## Fixed
- The appended status sentence was one 268-char line in a block wrapped at ~140. Wrapped it onto its own `>` line (commit "docs(plan): wrap the E5.5–E5.6 status sentence …"). Wording unchanged.

## Not changed on purpose
- Status still says E5.4 is "not yet reviewed": the task says keep every other sentence.
