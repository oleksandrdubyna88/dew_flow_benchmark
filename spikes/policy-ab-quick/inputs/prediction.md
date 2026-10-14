# Prediction — quick policy A/B pilot (written before any cell, 2026-10-14)

Written by the main session (Opus) before the seed, the draw or any cell. Story: creds #176 E1 (T1 + T2),
three cells A1, B, A2, every model at effort `high`, the same cap ($40) and wall (120 min).

1. **Quality.** All three cells pass the 3 spec-implied hold-out tests and their own suite; R-coverage by the
   codex reading 10–12 of 12 in every cell. No quality difference between A and B is visible at n = 1 per arm.
   The A1–A2 spread is as large as any A–B difference.
2. **Delegation.** A's main session writes most of the code itself or with 1–2 Opus children (the old rule:
   Opus everywhere, Fable only for security — this story is a type for stored secrets, so a Fable launch in A
   is plausible). B launches ≥ 2 Sonnet children (scouts and/or a coder) and at least one Opus review.
3. **Speed.** B's wall time is within ±30 % of A's — the delegation overhead (brief, review, fixes) eats most of
   Sonnet's speed advantage on a 400-line story.
4. **Cost.** B is 20–40 % cheaper than the mean of A1/A2, mostly because Sonnet's cache writes and output
   replace Opus's; all cells stay under $25.
5. **Effort.** Every logged level is `high`; the effort-pin hook denies nothing (no session sets `effort`).
6. **Leaks.** No canary token and no reference-only identifier appears in any stream or candidate.

What would surprise me: B slower than both A cells by > 50 %, or B failing a spec-implied hold-out test that
both A cells pass.
