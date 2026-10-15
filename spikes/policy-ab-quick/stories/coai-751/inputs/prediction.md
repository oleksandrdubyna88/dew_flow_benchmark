# Prediction — policy A/B series on coai #751 (written before any cell, 2026-10-15)

Written by the main session (Opus) before the seed, the draw, the probe or any cell. Six cells: three A (every subagent on
Opus), three B (role table + the test-only main-session edit bound), three adjacent A/B blocks with a seeded lead, effort
`high` for every model, the same prompt (incl. "independent work may be launched concurrently"), cap $30 per cell.

1. **Quality.** Every cell passes the two materialisable spec-implied hold-out files; codex reads 10–13 of 14 R-items met in
   every cell. No A–B quality difference is detectable at n = 3 per arm; the A–A spread is as large as any A–B gap.
2. **Delegation.** A: the main session writes most of the code itself (0–2 children, Opus). B: 2–4 Sonnet coders, at least
   one message launching two or more of them concurrently (stories 2.1/2.2 vs 2.3/2.4 are partly independent), and at least
   one Opus review child.
3. **Speed.** B's median wall time within ±25 % of A's median — concurrency recovers what the pilot lost to sequential
   children, the briefs and the review eat the rest.
4. **Cost.** B's median cost between −15 % and +15 % of A's — a story twice the pilot's size gives Sonnet's cheaper tokens
   more weight, but each child's cold start (~100 k cached tokens) and the Opus orchestrator's reading remain.
5. **Effort.** Every logged level is `high`.
6. **Leaks / tamper.** No canary appears; no instrument path is edited.

What would surprise me: B cheaper than A by more than 25 %, or B's quality below A's on a spec-implied hold-out in two or
more of its three cells.
