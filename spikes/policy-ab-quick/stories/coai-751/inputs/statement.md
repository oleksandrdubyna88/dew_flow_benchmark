*Applies to every story of the plan — the "Epics and stories" preamble, `todo/PLAN_paths_per_side.md` lines 271-272 at P, verbatim:*

Each story: RED first, the failure message recorded, then green, then break-it on the line that decides. Page tests
RUN the page through `src/test/panelPageHarness.ts` (`runPanel`) and `src/test/pageTree.ts` — never a regex over HTML.

---

### E2 — the sides this profile has been used on · `feat/paths-per-side-e2-sides-seen`

- **2.1** `sidesSeen.ts` (pure): `stampSide(stored, side, family, label, now)` and `knownFamilies(records, now)`, with
  the 90-day window and pruning. RED: `knownFamilies` of a Windows and a WSL record within 90 days is `{windows, posix}`;
  a 91-day-old WSL record is pruned.
- **2.2** Stamped at activation, at most once a day per side (`extension.ts`), never awaited by activation.
- **2.3** `coai.pathFieldsPerSide` (`auto`/`always`/`never`, `application` scope), declared, described in the five help
  languages, never exported (`configTransfer.ts` `NEVER_TRANSFERRED`).
- **2.4** Setup › This side lists the sides seen (label, family, last seen) with **Forget** and the switch, and a local
  window names itself (`thisSideLabel`, not *"this machine"*). Page test: two seeded records draw two rows; Forget posts
  the side key. `test:host` scenario: activation leaves a `coai.sideSeen@…` record for the test window's side.

---

**SCOPE NOTE.** Epic E2 only — stories 2.1–2.4; other epics (E3, E4, E5) are not in scope. The plan's `review_plan` /
`review_code`, consultation and PR steps are unavailable — skip them and say so.

---

**Struck (identical for both arms)**

None. The E2 section at P (`git show af7f2856:todo/PLAN_paths_per_side.md`, lines 458-468) carries no model line, no
model tag and no model suffix on its heading; the text above is byte-identical to P's lines 458-468 (and the preamble to
lines 271-272).

**Other whole-word mentions of Opus / Sonnet / Fable / Haiku in P's `todo/PLAN_paths_per_side.md`**

None (case-insensitive whole-word search over all 593 lines: 0 hits; the only "model" hits are product words — the
Settings page's *Models* card, `chatModelPresets`, "One model, `PathFinding`", `--check-model`, "the agy model list" — at
lines 7, 75, 76, 203, 316, 327, 329, 333, 335, 363, 431, 487, 496; none names an AI model and none is to be replaced).
The preparer therefore has nothing to replace with `[model withheld]` in this file.

*Provenance:* P = `af7f2856ecfaa732159db7dacda3255dfbde2334`; the plan's status line at P says *"E5, E2, E3 and E4 not
started"*.
