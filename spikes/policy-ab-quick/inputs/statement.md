### E1 — Foundations: the table is the one list, the type exists, the harness runs

**Why the cut is here.** After E1 nothing returns a `StoredSecret` yet: a column on the table, a type
nobody returns, a harness with three fixtures. Merged alone it is invisible to a person and to the PIN
plan's suite, and it is the ground the other two stand on — E2's funnel test and E3's per-slot fixtures
land in E1's harness, and T5 walks E1's table. The cut is after T2 rather than after T1 because a harness
without a type has no positive control and a type without the harness has no teeth.

- [ ] **T1 (E1) — The slot table is the one list.** `SecretSlot.bundleKey`; `RevisionSecrets` typed from
      `SMALL_FIELDS`; `snapshotForRevision` walks `SECRET_SLOTS`; `SEALABLE_MAPS` asserted against the
      table; the coverage test. Files: `entitySlots.ts`, `revisionHistory.ts`, `revisionSnapshot.ts`,
      `syncPinRule.ts`, `test/slotTable.test.ts`, the `syncPinRule` tests.
      **RED:** *every bundle key the vault stores is a slot's `bundleKey` or one of the two outside the PIN,
      and no slot names a key the vault does not store* — red as TS2339 (`bundleKey` does not exist on
      `SecretSlot`): a compile failure that names the missing column IS the symptom. **Guard (green before
      and after, and said so):** *`snapshotForRevision` reads exactly the table's getters* — the recording
      storage of `pinReaderBoundary.slotGetters` (`:95-110`) against what the snapshot calls.
      **DoD:** no behaviour change; `slotTable`,
      `pinSlotMatrix`, `pinReaderBoundary`, `syncMerge` green unchanged; ratchet unchanged.
- [ ] **T2 (E1) — `StoredSecret` exists, and the compile-fail harness runs in `npm test`.** `storedSecret.ts`;
      `test/typedFixtures.test.ts` with its first fixtures — `stored_is_not_a_string.ts` (TS2322),
      `a_string_is_not_stored.ts` (TS2322), `carried_is_a_string.ts` (compiles); the `tsconfig` / ESLint
      exclusions; the program-build timing for §3 item 2. Nothing returns a `StoredSecret` yet.
      **RED / teeth:** new infrastructure has no failing predecessor, so its teeth are proven the other
      way round (`testing.md`, *if the fix already landed*): with `type StoredSecret = string` planted, the
      harness must fail *"expected TS2322 at line 7 of stored_is_not_a_string.ts — the fixture compiled"*;
      restored, green. Both observations in the commit.
      **DoD:** harness green with its positive
      control; `npm run compile` unaffected by the fixtures; timing recorded in the commit.

**E1 DoD:** T1's and T2's DoDs; no behaviour change — the whole suite green with no assertion edited;
ratchet unchanged; the harness's teeth proven both ways in the commit; `review_plan` (epic 1/3) and
`review_code` over E1's diff against `main` both `proceed`, the cadence consultation closed with an outcome
before T1 began; the PR merged into `main`.

---

**SCOPE NOTE.** Epic E1 only — T1 and T2; later epics (E2, E3) are not in scope. The plan's
`review_plan` / `review_code`, consultation and PR steps are unavailable — skip them and say so.

---

**Struck (identical for both arms)**

1. Heading suffix ` (Opus)` — original heading: `### E1 — Foundations: the table is the one list, the type exists, the harness runs (Opus)`.
2. Sentence at the end of the *Why the cut is here* paragraph: `Opus: no judgement is made in either story.`
3. T1 marker: `**Model: Opus** — one table widened, no behaviour.` (removed before `**DoD:**`).
4. T2 marker: `**Model: Opus** — a type, a harness, two config lines.` (removed before `**DoD:**`).
