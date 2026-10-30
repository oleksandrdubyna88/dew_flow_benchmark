# Part 6 — three schemes on a real task, each ending in the coai code gate — recorded before the arms run (2026-10-28)

## The task and the plan

- Issues #878 (a spent model is still launched and runs into its timeout) and the quota half of #765 (agy's structured
  quota error, `/quota` reset times, a remembered cooldown, the card's countdown) of `dew_flow_connect_other_ais`.
- The plan `todo/PLAN_a_spent_quota_cools_the_model_down.md` was written by Fable (the project's plan role) from a Sonnet code
  map, then gated and consulted:
  - plan gate round 1 (session 95dd15f6, on 8b1eef7a): `proceed`, gating 5 of 6, 2 of 2 reviewers (codex, antigravity);
    1 finding rejected (its premise false in the code), 5 accepted;
  - a consultation (codex-2 / gpt-6-astra), each point verified in the code: the prefix grouping would park ordinary Gemini
    when only Argon is spent — replaced by a measured table; a cross-process lock; edge cases;
  - real `agy` 1.4.9 captures (`/quota`, `agy models`) taken once by the operator and committed as fixtures;
  - plan gate on the revision (session b227f8cf, on a2b9609d): `proceed`, gating 4 of 6, 2 of 2; 4 rejected with reasons,
    2 minors accepted (6786582e).
  - The operator's gate commands to split into epics were declined for this plan (recorded in the plan, §11): it is the
    frozen spec every arm implements whole.
- The frozen plan: **6786582e**, 43 scenarios (39 C#, 4 node), four stories, ~1,300–1,500 changed lines estimated.
- Baseline on this host at the base: the whole MCP suite (13,807) and the extension tests pass — 0 failures.

## The arms (3 schemes × 3 runs, rounds of three at the same time: C_n, CR_n, SRS_n)

| Scheme | Steps (model, budget) |
|---|---|
| **C** | code (Sonnet, 180 min) → review (Opus, 40, read-only) → fix (Sonnet, 60) → coai gate (Sonnet, 150) |
| **CR** | code (Sonnet, 180) → fresh Haiku review + fix (60) → review (Opus, 40) → fix (Sonnet, 60) → coai gate (Sonnet, 150) |
| **SRS** | split (Sonnet, 40) → per task code (Haiku, 45 each; parallel worktrees) → fresh Haiku review + fix (60) → review (Opus, 40) → fix (Sonnet, 60) → coai gate (Sonnet, 150) |

- **The coai gate step:** the arm's own coai server (its own data folder, a copy of the operator's `settings.json`: the same
  5 providers and switches). A plan round on the frozen plan only unlocks the code gate (its findings are rejected as
  "frozen"); then code rounds until `proceed` / `good_enough` / rounds spent / `call_human`. Only the gate tools are allowed
  (no `consult`, no `ask_*`). Gate steps run ONE ARM AT A TIME (a lock), for the providers' quotas.
- **Isolation:** each arm under `r/<random token>/` (clone, notes, work, results, temp, coai data); the same branch name in
  every arm; no brief names the arm or its scheme; while arms run, the old experiment is closed (000) and the folders that
  would list another arm (the experiment root, `r/`, `pids/`, `~/.claude/{projects,file-history,session-env,shell-snapshots}`)
  cannot be listed (300); `Read` of `~/.claude`, the vendors' homes, `/proc`, the old experiment and the coai store is denied.
  Known residual path: a deliberate `/proc` walk through `node` or `ls`; every transcript is scanned for it afterwards.
- **Limits:** a step that ends on a usage limit, rate limit or overload is resumed in its own session after 15 min (up to 40
  times); a gate that cannot run is resumed after 30 min (up to 8 times). Waiting is not counted against a budget.

## What is measured

Everything earlier parts measured (cost, time, the Opus review's findings, the objective checks with the 43 scenarios by name,
the absolute blind judge), plus per arm: the coai gate's rounds, verdicts, reviewer counts, findings by severity, accepted vs
rejected, and fixes; and a blind judge (Opus) that scores every coai finding (real or not, severity right, useful) and the set
per arm from 1 to 10, with a hand-checked sample. Mutation checks: designed after the run on the final code, frozen before
they are applied.

## Predictions

| # | Prediction |
|---|---|
| 1 | Cost per arm (Claude only): C $18–33, CR $20–35, SRS $16–30; CR's mean about $1–3 above C's. |
| 2 | Elapsed per arm, gate included: 3–6 h. The gate step 30–120 min. |
| 3 | The Opus review's blocking+major per arm: C 1–3; CR at most C's; SRS at least C's. |
| 4 | The coai gate's first code round finds ≥ 3 findings in every arm; the blind judge rates at least half of all coai findings real; in at least 2 of the 3 schemes coai finds a real defect the Opus review did not name. |
| 5 | Absolute judge: C and CR 7–8 in every arm; SRS 6.5–8. |
| 6 | Scenarios by exact name: C and CR ≥ 40 of 43 in every arm; SRS ≥ 38. |

---

# Part 7 — CHH: classic with two focused Haiku reviewers (owner's request, 2026-10-29)

**Scheme CHH** (3 arms, CHH1–CHH3, run at the same time): Sonnet codes (180 min, brief `1-code`) → **Haiku A**, read-only,
the decision tracer (40 min, `hA-trace`: each plan decision at every site — subclasses, callers, readers/writers, the
issue's sample data, mixes and orders, failure paths) → **Haiku B**, read-only, test truth (40 min, `hB-tests`: scenario
names, does each test fail if its decision breaks, does it pass on every CI job — Linux, Windows, macOS) → **Sonnet fixes
both** (60 min, `hfix`) → Opus reviews (40, `2-review`) → Sonnet fixes (60, `3-fix`) → the coai code gate (150, `gate`).
Same task, plan (`6786582e`), base, budgets for the shared steps, isolation and limit handling as Part 6.

**Why these two focuses.** coai's code gate ran four roles in Part 6 (Conventions, Architecture, Security & Reliability,
UX-DX & Performance). The real defects it and the Opus review missed fell into two kinds no role names: a plan decision not
held at one site (a subclass inheriting a default, an order of results, real sample data), and a test that does not test
what it claims or fails on another CI system.

**Bias, stated before the run.** The two focuses were chosen after seeing Part 6's misses ON THIS SAME TASK. The briefs
name no Part 6 defect and no file, but CHH has a hindsight advantage over C, CR and SRS. A fair test of the focuses needs
another task.

**Changes to the runner.** Read-only steps now refuse the Agent/Task tools (Part 6: SRS3's Haiku step started Opus and
Sonnet subagents). The C, CR and SRS arms are not re-run. The lock also closes (000) every Part 6 result: the nine arm
folders, the judges and hand-check, the mutation runs, the metrics, the logs, this file and the write-up worktree.

**Predictions (written before the run).**
- P7-1: Haiku A reports at least one finding that the later judge rates real in at least 2 of 3 arms.
- P7-2: Haiku B reports at least one CI-portability or no-teeth test finding in at least 2 of 3 arms.
- P7-3: the Opus review after the fix finds 0–1 blocking+major per arm (CR in Part 6: 1 / 2 / 2).
- P7-4: the blind judge scores every CHH arm 8 or more in both runs, with no hand-checked major in at least 2 of 3 arms.
- P7-5: Claude-only cost per arm $17–21 (CR: $15.88–17.49); active time 170–210 min (CR: 119–166).
- P7-6: the coai gate's findings stay mostly not real (judge's score 2–4 per arm).
- P7-7: the mutation checks kill 8 of 8 in every arm (no separation, as in Part 6).
