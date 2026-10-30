# Analysis contract, bare scope

The blocks below are quoted word for word from `todo/PLAN_minigit_stack_bench.md` at `85c54b790a37479a32fc88b7b0b4353652fed601`, a commit of origin/main; `prereg/extract-contract.mjs` wrote them and a test checks them. Section 5.3's acceptance is quoted whole; its `delivered` clause is the process arm's and is **process-only**: a bare session is never judged on it.

<!-- quote: Unit of analysis (parent section 11) -->
> **Unit of analysis = the cell**, not the session: v2 inherits v1, so the 24 sessions are 12 observations with two phases
> each. Every cell is reported, delivered or not — no successful-only comparison; a capped, undelivered or budget-truncated
> cell appears with its outcome.
<!-- /quote -->

<!-- quote: Censored and mixed outcomes (parent section 11) -->
> **Censored and mixed outcomes, pre-registered.** A session killed at its cap has a cost censored at the cap: it is
> reported as `≥ cap`, and any ratio it enters is reported as a bound, not a value. A pair whose two cells did not both
> deliver (one merged, the other capped, undelivered or on `call_human`) is shown in every pair-ratio table twice — included
> as a bound, and excluded — and the medians are given both ways. If the ceiling leaves the process arm at n = 2, the report
> says "in k of 2 pairs" and nothing about a third.
<!-- /quote -->

<!-- quote: What n = 3 licenses (parent section 11) -->
> **What n = 3 licenses:** "in k of 3 pairs", and whether ranges overlap (overlap = not distinguished). A predicted direction
> held in 3 of 3 pairs is the strongest available result and still has 1/8 probability under no effect — so no p-values, no
> confidence intervals, no effect sizes as findings. It does not license other tasks, languages, models or CLI versions, the
> value of the process on real work, or any mechanism.
<!-- /quote -->

<!-- quote: Void list (parent section 11.1) -->
> - **Void — instrument causes only, never quality:** `rate limit` (the CLI's own 429 markers, `ratelimit.mjs:12`);
>   `no reviewer` (`call_human` because zero reviewers answered, `dew_flow_connect_other_ais ·
> src_mcp/core/Rounds/RoundMachine.cs:404-416`); `contaminated` (a
>   high-severity audit hit, a canary, a token leak); `harness` (a crash before the model's first turn); `alias drift` (D12)
>   — the last stops the series rather than voiding one cell.
<!-- /quote -->

<!-- quote: Data points, not voids (parent section 11.1) -->
> - **Data points, not voids:** killed on wall, cap or stall; `call_human` with ≥1 reviewer answering; `model_refusal_fallback`
>   (flagged, medians with and without); OTel missing (time decomposition "not captured").
<!-- /quote -->

<!-- quote: A void session is re-run once (parent section 11.1) -->
> - **A void session is re-run once** in a fresh namespace `.a<k+1>`: a v1 void from the template tag, a v2 void from the
>   recorded v1 base (v1 is kept). Its concurrent partner is not stopped. Re-runs are scheduled after the block's own pairs
>   have finished and before the next block starts, so the arms still never overlap; a void v1 is re-run before its cell's
>   v2 (the barrier holds the partner's v2 meanwhile only if both v1s voided). A re-run is paired with a re-run of the
>   partner only if the partner also voided, otherwise it runs alone and is labelled `ran unpaired`. A second void of the same
>   session leaves the cell missing, reported as such. Void spend counts against the ceiling (§13).
<!-- /quote -->

<!-- quote: Acceptance (delivered is process-only) (parent section 5.3) -->
> A session is **accepted** when: clean build, visible 100 %, held-out primary 100 % (blocked cases count against it), and
> (process) **delivered** = merged into its base. An undelivered process session is scored on its PR head and marked so.
<!-- /quote -->

<!-- OPERATIONALIZATIONS: everything below this line is the pre-registration's own text; extract-contract.mjs never rewrites it -->
