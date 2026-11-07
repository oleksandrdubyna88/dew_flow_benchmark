# PLAN — language comparison v2: C# / .NET 10 against TypeScript / Node 26 coding agents on ten harder tasks, bare and under the family's process

> Status: **2026-11-08: still stopped for re-review — the whole-plan revision for epoch 10 is written** (the epoch-10 lines in *Operative plan for epoch 9*, and the record *Re-review for epoch 10* in the ledger). **Pending: the Opus judge's verdicts, the re-review gate, and the owner's approval of item 12 of *What got smaller* (the task repository's pull requests merge without CI); the epoch 10 line is written only after them.** F1 is merged (#179); its tail, the bench pins of FORMAT's second amendment (`chore/lang-v2-format-d72-pins`), merges before F2.S1's first conformance run. *Before the revision:* **2026-11-08: stopped for re-review — changes 9.1 and 9.2 (the ledger); no new code on any epic of this plan and no merge of a pull request carrying its work until epoch 10 opens. F1 is merged (#179) and FORMAT's second amendment is on the task repository's main (its #4).** **2026-11-07: epoch 9 open, narrowed by the owner — the re-review is done. Stage 1 is one bare series on six tasks; the process arm waits behind a dated decision after the bare report. Next: F1.S1, the carry-ins.** (Before: 2026-11-07: still stopped for re-review — the re-review is in progress. The whole-plan revision for epoch 9 is written (the section *Operative plan for epoch 9*, right after the ledger) and the Opus judge's verdicts are in (15 rows, all `fits`, no `cut`). Pending: the re-review gate on this revision, then the owner's approval of the list *What got smaller* in that section** (the plan is narrowed by the owner's decision of 2026-11-06: bare first, process later). Only then is the epoch 9 line written and the revision in force; until then no new code on any epic of this plan and no merge of a pull request carrying its work. After it: F1.S1, the carry-in merges. The revision's shape: Stage 1 is one prospective bare series on the six daemon and web tasks (04, 05, 06, 07, 08, 10), five pairs per task, every session scored ten times; Stage 2, the process arm, waits behind a dated decision after the bare report. *Before the revision:* **2026-11-06: stopped for re-review — changes 8.1 and 8.2 (the ledger); no new code on any epic of this plan and no merge of a pull request carrying its work until epoch 9 opens. The quick run is done and merged (#173), and its analysis is `research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md` (#175).** **2026-11-05: paused by the owner for a two-day quick run (entry 8.1); the gates were off while that work was written and run before its code reaches `main` (the owner, the same day), and its results are exploratory.** **Epoch 8 open since 2026-11-05 with 0 changes, approved by the reviewers** (the re-review of epoch 7: the stop #168; the consult `d2e1e648`; the whole-plan revision D65–D68; the consultants `fa11693d` on D68 and on Q-I; the Opus judge of the done work — every row `fits` or `—`, no `cut`, `Narrowed: no` confirmed — and its findings J1–J11; the coai plan round `40233ced`, PROCEED, 2 of 2 reviewers, its findings K1–K6 applied; L1–L4 read for narrowing by the team lead). Work resumes at §14 A1's step 0: the marker test's two failures are read and the diagnosis is recorded. *Before the resume:* **2026-11-05: stopped for re-review — changes 7.1 and 7.2 (the ledger); no new code on any epic of this plan and no merge of a pull request carrying its work until epoch 8 opens.** **The whole-plan revision (step 4) for epoch 8 is written, 2026-11-05**, from the consult `d2e1e648` (step 3; advice, its citations checked), `research/module_lang_bench.md` on `feat/lang-v2-e1-s3` and the briefs of E1.S3 and E1.S8, with the team lead's decisions: **D65**, the submission and build contract — what is scored is what the agent submitted, the frozen files restored and nothing regenerated or refused, every deviation counted and never a verdict, `ignore-scripts=true` (measured), the contract in FORMAT, one shared module before A's freeze; **D66**, one acceptance policy across arms, with warnings as errors and ESLint as process-arm CI jobs beside `accepted`, and D6's claim corrected; and this plan's **D67**, the two faults that stopped this epoch and an audit of the whole plan for them (§6.2: 20 rows), and **D68**, the merge condition under load-sensitive tests. New in E1: **S3b, S4b, S8c**, all before the first D40 gate. T23, T-L60–T-L68, §14's A1 and A2, §16, §17 Q-I and Q-J, the E1 stories document and the re-review record for epoch 8 below — **`Narrowed: no`**. **Done since, 2026-11-05:** the Opus judge's verdicts (step 5, `650c049a`: every row `fits` or `—`, no `cut`) and the fix round on its report: D68 rewritten, since a re-run in place of a whole green run was a smaller merge condition; D65's folders per stack, the lock row and the identity's edges; S4b before S3b; Q-I and Q-J settled; T24. **Pending before epoch 8 opens:** the re-review gate on this revision, the resume. Until then the stop stands. *Before the revision:* **2026-11-04: E1.S4 merged (#159, both snapshots registered), E1.S9 done (FORMAT v2 gated, task repository PR #2; its tools #160); next: E1.S8, E1.S3 phase A.** **Epoch 7 open since 2026-11-04 with 0 changes, approved by the reviewers** (the re-review of epoch 6: the stop #156; the consult `722b1e81`; the whole-plan revision D60–D64; the Opus judge of the done work — every row `fits`, no `cut`, `Narrowed: no` confirmed — and its findings J1–J6; the owner's TypeBox decision; the coai plan round `fae12fd9` on the revision, PROCEED, 2 of 2 reviewers, its six findings accepted and applied). Work resumes at §14 A1: E1.S9 (FORMAT v2 with CP1's fixes, the classes per heading and T-L56 green before its gate round) ∥ E1.S4 (package sets; its plan round passed) ∥ S3 phase A after S4; the D40 gates wait for CP2's conditions (D63). *Before the resume:* **Epoch 6 STOPPED for re-review 2026-11-03 (change 6.2). The whole-plan revision (step 4) for epoch 7 is written, 2026-11-03**, from the CP1 and CP2 readings (`spikes/lang-bench/inputs/checkpoints.md`), the consultants' answer `8b776383` on change 6.2 and the re-review's consult (step 3, `722b1e81`; its advice verified against the files it names), with the team lead's decisions **D60–D64**: FORMAT's fifth section class `daemon-web` for sections 4 and 5, and B's check reads A's frozen classification — a section cannot leave the protected set by a relabel in B, a protected section cannot be deleted, the header rows are each group's own and the changelog only grows (D60); the grammar is pinned per task — A's four keep A's `GRAMMAR.md` bytes when B replicates them, B's six get B's, recorded per group in `inputs/specs.d/format.json` (D61); CP1's contract-level findings settled in FORMAT before its gate — F-STDERR's 256 MiB stop apart from its content (F5), F-ENV's `TZ` and the base keys an `env` line may not set (F7), `Format version: 2` (F6); CP1's F1–F4 required before S9's gate, F8–F9 before A's freeze, F10–F13 in part ii-b or E1.S8 (D62); CP2's conditions 1–4 and its F5 and F6 each with an owning story and a blocking milestone, `pids.max` 512 named in FORMAT (`F-PROCS`) and in each group's manifest (D63); one rule for every freeze — shown with one permitted and one refused change against real files before the freeze (D64); T-L56–T-L59; §14's A1 and B1; §16; change 6.1 carried into the E1 stories' probe text and Definition of Done; the E1 stories document revised in the same commit; and the re-review record for epoch 7 below — **`Narrowed: no`**. **Done since, 2026-11-03:** the Opus judge's verdicts (step 5, `afc21c2c`: nine `fits`, one `—`, no `cut`, so no revert; `Narrowed: no` confirmed) and its findings applied in the baseline, not as entries — **J1** (it blocked the resume): the class is given per `###` subsection where a `##` section mixes kinds, so FORMAT's daemon-and-web text in 2.4, 2.5, 3.3, 6.2 and 6.3 is `daemon-web` and 2.3, 2.6, 3.2 and 6.1 are `olympiad` (D60, T-L56, E1.S9); **J2**: F-ENV states exactly what the layer refuses, and the conformance check refuses the same keys from the layer's own lists (D62, T-L58, E1.S8); **J3**: a freeze rule without its pair blocks the freeze commit (D64); **J4**: every freeze rule's pair, owner and milestone (D64); **J5**: the changelog append-only, the preamble protected but for the header rows and appended class rows, headings matched by number and text with A's numbers fixed (D60); **J6**: T-L56's cases named, T-L59's record fields, T-L58's `pidsMax` half in E4.S3, D45's grammar hash per task. **The owner's decision of the re-review, 2026-11-03** (after `ask_consultants` `40498063`, the consultants split 1–1): the TypeScript web set is `@fastify/type-provider-typebox` 6.3.0 with **`typebox` 1.1.x** (the renamed TypeBox 1.x line; the exact patch resolved once at the snapshot build), and `@sinclair/typebox` is dropped — the planned pair with `@sinclair/typebox` 0.34.52 did not fit (6.3.0's peer is `typebox ^1.1.14`; 5.2.0 is the last on `@sinclair/typebox`); D6, §2's npm row, §4.6 and E1.S4 carry it, the old text marked superseded. *Pending as written at step 4:* **Pending before epoch 7 opens:** the Opus judge's verdicts (step 5), the re-review gate on this revision, the resume — the epoch 7 line. Until then the stop stands: no new v2 code, no merge of a pull request carrying v2 work. *Before the revision:* **Epoch 6 STOPPED for re-review 2026-11-03 after its second change (6.2: FORMAT's sections that serve daemon and web tasks alone get a class of their own, so D45's amendment path before B's freeze applies to them; found by CP1, decided after the consultants). No new code on any epic and no merge of a pull request carrying v2 work until the re-review closes (`common.plan-change-control`).** *Before the stop:* **Epoch 6 open since 2026-11-02 with 1 change (6.1: Node's medium-buffers at 256 MiB is recorded, not asserted, in T-L43), approved by the reviewers** (the re-review of epoch 5: the whole-plan revision D55–D59 from consult `ee6c5a9a`, the coai document gate in two parts PROCEED with its findings applied, the Opus judge's verdicts — one row `cut`, OP-K at 100 %, fixed forward, no revert — and its findings applied; `Narrowed: no`). Work resumes at §14 A1: the fix forward of E1.S8a and E1.S8b to D55–D59, then CP2. **Done 2026-11-03:** E1.S8b merged with the fix forward of D59 (#153, `ae144e1f`; 1389 of 1389 `spikes/lang-bench` tests, plain and with `BENCH_REQUIRE_SANDBOX=1`); next: CP2. **CP2 recorded 2026-11-03 — passes with conditions ([`inputs/checkpoints.md`](../spikes/lang-bench/inputs/checkpoints.md)): S3 phase A may start; the D40 gates wait for its conditions 1–4 (the gate harness passes `data: true` and the stack and records the identity, the policy and the stack; the runner manifest is checked before the first gate; an exit 3 is re-run, a `fork` flag never fails a case; the leftovers follow-up S1 stays outside the layer identity); next: E1.S9, S8, S4 and S3 phase A.** *Before the resume:* Epoch 5 stopped for re-review on 2026-11-02, after its second change (5.2: Node gets `--max-old-space-size` = 90 % of the limit and .NET's hard limit becomes 90 % too, against D15's "no `--max-old-space-size` flag"); no new code on any epic until the re-review closes (`common.plan-change-control`). **The whole-plan revision (step 4) for epoch 6 is written, 2026-11-02**, from the coai consult `ee6c5a9a` (advice, verified against `spikes/lang-bench/inputs/opk-experiment-2026-11-02.json` and `inputs/exec-probe-2026-11-02.json`) with the team lead's decisions **D55–D59**: the memory policy A at 90 % for both stacks, pinned as an identity (D55); the `memory limit` verdict with two causes, its 50 % evidence boundary tested at the byte, and the child case (D56); MiB everywhere and the floor settled by one measurement — not raised on this evidence (D57); E2's calibration sequence, so a registered limit's evidence is a run under that limit and its heap settings (D58); E1.S8a and E1.S8b fixed forward and re-pinned, part ii-b inheriting the abort rule, the historical records kept with their identities (D59); threats T21–T22; T-L43 revised and T-L49–T-L55 (T-L53–T-L55 from the gate's and the judge's findings); G-17 widened; §14's A1, A2 and B1; §16; the E1 stories document revised in the same commit; and the re-review record for epoch 6 below — **`Narrowed: no`**. **Done since, 2026-11-02:** the document gate in two parts (both proceed) and the Opus judge's verdicts (`445298c9`: one `cut` — OP-K at 100 % in D15 and the layer, fixed forward on the branch, no revert; every other row `fits`; `Narrowed: no` confirmed); the gate's findings R1–R6 and the judge's J1–J11 applied in the commit after it. **Pending before epoch 6 opens:** the resume (step 8) — the epoch 6 line, written by the team lead. Until then the stop stands: no new v2 code, no merge of a pull request carrying v2 work; the fix forward of D59 is epoch 6's first work (§14 A1), not the re-review's. *Before the stop:* Epoch 5 open since 2026-11-01 with 1 change (5.1, 2026-11-02: a runtime's own out-of-memory abort is `memory limit`), approved by the owner — narrowed (D10's arm alternation, the T16 control, is lost; §17 Q-H approved); the bare anchor round in C added (§17 Q-G, D54). Work may start on group A (§14 A1).** *Superseded status of the stop:* STOPPED 2026-11-01 by change 4.2 (the owner's staged order: group A, the four olympiad tasks in the bare arm;)
> then B, the bare arm for all ten; then C, the process arm) — the second change of epoch 4; no v2 code until its whole
> re-review closes (`common.plan-change-control`).** **The whole-plan revision (step 4) is written, 2026-11-01**, from
> the Fable consult of change 4.2 (its § 7 as the backbone) and four coai consults (cadence `f25ad04b`; risks `b3a3e5d6`,
> `b11c7e5a`, `19ae7208`), verified by the team lead, with the team lead's eight decisions: D44–D53 (the staged series;
> freeze and pre-registration tags per group; CPC per group with A replicated in B; one model, one alias baseline and one
> CLI copy for the series; E0 before C only; interim records; two runner manifests with three guards; E1.S8b whole before
> A; the publication and comparison guards; no new spend halt), §14 rewritten as steps per group, the epics' group map, and
> the re-review record for epoch 5 below — **`Narrowed: yes`: D10's arm alternation, the control of T16, is lost; nothing
> else got smaller.** The E1 stories document is re-cut to match (its own commit). **Pending before epoch 5 opens:** the
> plan round on this revision, then the owner's approval of the narrowing (step 8; §17 Q-H) and the owner's answer to
> §17 Q-G (the optional bare anchor round in C). Until then the stop stands: no new v2 code, no merge of a pull request
> carrying v2 work. Before the stop: E1.S5 (toolchains) merged (#135); E1.S1's bench
> side and E1.S8a part i built and in review; the task repository's history joined to GitHub (task repository PR #1).
> Operator answers of 2026-11-01, recorded as status (not changes): the credit terms of the known tasks (the rest of Q1 —
> CC BY-NC-SA 4.0 for specs 01 and 02 by a per-file exception, 09 in own words credited "based on IOI 2018 Werewolf",
> 06 with RealWorld's MIT notice, 05 in own words never taking the product's name; `inputs/credits-lang.json`); the
> fine-grained PAT and the deploy key exist (E1.S7; the key authenticates over `ssh.github.com:443`).
>
> Status before the stop: **plan only, nothing implemented yet; epoch 4 open since 2026-10-31 with 1 change (4.1, the known-task needle
> contract: case-sensitive word-boundary matching, the task-9 spec path renamed; found by the spec drafts 06 and 09 — the
> one change this epoch allows, no stop; all ten specs are drafted), approved by the owner —
> narrowed: the process arm's GitHub capability (D42, the run-bound broker with an isolated bare repository per run), as
> the epoch 4 re-review record says.** The owner's four decisions at that approval (2026-10-31, late evening), part of the
> approved baseline: (1) Q-F — the narrowing approved, D42 as written; every repository stays public, privacy was never
> the aim; the broker exists because the process agent could otherwise read the trap history in the public bench
> repository, which the bare agent cannot reach. (2) TypeScript **6.0.x**, not 7.0.2 (D6): the newest line the pinned
> `typescript-eslint` 8.72.x supports, so the TS agents get type-aware lint at the C# analyzers' strength; 7.0.2
> superseded. (3) Q-A — **no total ceiling**: the $1000 is removed, the six rounds of both arms run as planned; a fixed
> **$50 per-session cap** stays as a runaway guard (a capped session is censored, never hidden); the pilot's cap formula
> is a reported cross-check (D7; D30 superseded). (4) D35 confirmed: `de_DE.UTF-8` in both boxes, every task, both stacks.
> The third epoch (opened 2026-10-31) changed twice the same day — 3.1, the D40 pre-freeze gate needs the final scoring
> environment that E3 builds, and the driver↔program network layout is designed nowhere; 3.2, the bench repository is
> public and its plan carries the §4.3 trap table that a process agent can reach through GitHub — both found by the E1
> stories writer (`feat/lang-v2-e1` `51a80ba`, §9 OP-1 and OP-12) — and was closed by a whole re-review
> (`common.plan-change-control`): consult `16e5f408`, the step-4 revision (D41–D43, §6.1, E1.S8b, E3.S7a, T18–T19, G-20,
> T-L33–T-L36, the trap aid moved to the private area with its sha256 in §4.3), the E1 stories judged `fits` (`5995560`),
> the plan round PROCEED (codex) with its one finding applied (the per-task riskiest-scenario details moved to the private
> aid's sibling `PLAN-risky-scenarios.md`), the second full reading (the document gate, four reviewers) PROCEED with five
> findings applied (the isolated bare repository per run, D3 on the memory contract, two more fragments moved, the record's
> `Narrowed: yes`), and the owner's approval above. Work may start on E0 and E1. Operator actions the E1 stories found, recorded as status (not changes): DONE 2026-10-31 — `locale-gen de_DE.UTF-8`
> on the series host by the owner (`locale -a` now lists `de_DE.utf8`; D35); the licence decision Q1 for the new
> repository's own content: MIT (the owner); the public repository `dew_flow_ai_language_comparison` created, MIT, with
> only its LICENSE commit — the local drafts are pushed through E1.S1's pre-push check, not before (D2, D31). OPEN: the
> credit terms of the known tasks' sources (the rest of Q1); the fine-grained PAT and the deploy key for the process arm
> (E1.S7). Epoch 2 (opened 2026-10-31
> after epoch 1's re-review) changed three times the same day — 2.1, 2.2 and 2.3, the daemon and web limits and timing
> contract, the agent's scenario runner and grammar, and the `exec` constraint on `run.sh`, all found by FORMAT.md v1
> (E1.S8) — and was closed by a whole re-review (`common.plan-change-control`): the two further pilot specs (task 5
> `30af434`, task 4 `0b69a50`) read, consult `3ea9ef36`, the step-4 revision (D35–D40), the four drafts judged `fits`
> (`1591786`), the plan round PROCEED (codex) and the document gate PROCEED (four reviewers) with its five findings applied
> as the baseline (one memory contract; the pre-freeze gate on tasks 4 and 5; the runner as an early E1 deliverable; draft
> references written in E1). Epoch 3 is approved by reviewers, not narrowed; work may start on E0 and E1; the budget frame
> of §12 is the operator's decision before round 1 (Q-A). Epoch 1 was approved
> 2026-10-31 by the coai plan gate (PROCEED on `aa5e413`, its five findings applied in `8e1a39a` as the baseline), changed
> twice the same day (1.1 D34 metrics only added; 1.2 the spec-format contract from the pilot spec 01) and closed by a whole
> re-review (`common.plan-change-control`): consult `68a33be2`, the step-4 revision, the done work judged (both `fits`),
> the plan round PROCEED on `c62514c` and the document gate PROCEED on `b0fb0f1` (interim path, four reviewers), their
> findings applied as the baseline. Epoch 2 is approved by reviewers, not narrowed; the one pilot artefact, the spec
> `01-descent-to-one.md` (`dew_flow_ai_language_comparison` `60e468a`, local), is kept as a draft. Work may start on E0 and
> E1; the budget frame of §12 is the operator's decision before round 1 (Q-A).
> Scope: a new public GitHub repository `dew_flow_ai_language_comparison` (gated and frozen task specs, task workspaces,
> visible samples, run bases of the process arm); a conventions parity audit and release in `dew_flow_conventions`
> (prerequisite, other repository); a new spike `spikes/lang-bench/` in this repository that imports the v1 harness
> `spikes/minigit-bench/` and extends it for two arms, long sessions, daemons and web APIs; shared black-box acceptance
> suites with a held-out part and raw records outside every git tree under `~/rsd/lang-v2/`; the series' record under
> `research/lang-v2/`.

## Plan changes

> Epoch 1 · approved 2026-10-31 · by reviewers · state: closed

- 1.1 · 2026-10-31 · owner · contract — metrics are only added, never replaced or dropped; D34 and T-L26 (commit `d8da443`)
  (An owner's request counts exactly like one the agent finds. Every v1 metric stays in every v2 record beside the new
  ones; a v2 table may lead with a new metric and never omits a v1 one.)
- 1.2 · 2026-10-31 · agent · contract — the spec-format contract, found by the pilot spec 01 (`dew_flow_ai_language_comparison` `todo/tasks/01-descent-to-one.md`, `60e468a`): the format was underdetermined
  (One cause, three parts. (a) Limits in the spec: the spec states D14's floors — 1 s, 256 MB — and the 3 × / 2 × rule
  as guarantees; the exact values are registered at calibration and given to the runner, never to the author, so a spec
  never needs an amendment to carry them; §4.1, §4.3 and the prompts of §4.5 changed. (b) The visible-sample format is
  fixed now, `samples/<id>.in` and `samples/<id>.out`, and E2.S2's suite case format reads it, so it is no longer decided
  in E2.S2; E1.S6, E2.S2 and §14 changed. (c) Spec conventions adopted from the pilot: byte-exact output as the series
  default; stable requirement ids R1…Rn that held-out cases cite; header rows (kind, gate verdict, freeze tag, the frozen
  sentence, amendments) with "not yet gated" / "none yet" placeholders; no mention that a task is known; frozen-list
  wording for a stack-neutral spec; the 600 s build bound stated; invalid input "does not occur; unspecified; never
  tested" unless the task defines errors; no plan story ids in agent-visible text; the `⏎` glyph for sample bytes.
  This second change stopped the plan.)
  (Not an entry, recorded here so the diff is explained: one trap row of §4.3 carried a wrong fact — a consultant claim of
  2026-10-31, refuted by probe — and was corrected; the row itself lives in the private aid (D41). A corrected fact changes
  no scope, goal, contract or approach. *Reworded 2026-11-01: the earlier wording of this note named the trap; it is not
  kept here, by D41 — T18 discloses it.*)

### Re-review for epoch 2

Narrowed: no — both changes add (a metric-retention rule and a spec-format contract); no scope, Definition of Done, merge condition or release got smaller, checked against §9, §16 and the epics

| Work | Where | Verdict | Action |
|---|---|---|---|
| pilot spec 01 `todo/tasks/01-descent-to-one.md`, `60e468a` (E1.S2's first spec, not yet gated) | `dew_flow_ai_language_comparison`, local commit, no remote | fits | kept as a draft; brought to `FORMAT.md` (E1.S8) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| D34 and T-L26, `d8da443` | `dew_flow_benchmark` main (merged in #124, `f369e66`) | fits | kept: merged and compatible with the revised plan (judged by the team lead, Opus, 2026-10-31) |

> Epoch 2 · approved 2026-10-31 · by reviewers · state: closed

- 2.1 · 2026-10-31 · agent · contract — the daemon and web limits and timing contract is absent; found by FORMAT.md v1 (`dew_flow_ai_language_comparison` `d7c73ff`, `28bf777`)
  (One cause: D14 defines limits only for the olympiad kind. §4.1's spec rules say the spec states "the RSS floor for a
  daemon", but D14 names no daemon or web floor or rule; nothing in D13, §4.3 or T-L4 gives a source for the readiness
  deadline or the SIGTERM grace period, and no rule exists for scenario-step deadlines. FORMAT v1 used 256 MB as a
  placeholder RSS floor and made readiness and grace per-task spec values (F-READY `{T}`, F-STOP `{G}`, F-LIMITS-D,
  F-DEADLINES). The step-4 revision writes the contract for both kinds.)
- 2.2 · 2026-10-31 · agent · contract — the agent has no scenario runner and no grammar; found by FORMAT.md v1
  (One cause: D19 and §5's workspace listings put neither a runner nor the grammar in the workspace, while §4.3 says the
  agent "may run" the samples — for a daemon or web task it cannot. FORMAT v1 made every daemon and web spec copy a
  reading block of about twenty lines so the agent can read a sample by hand. Also open: where CI's visible-sample runner
  lives in the task repository (§5), given that D22 makes the suite the bench's, not the repository's. This second change
  stopped the plan.)
- 2.3 · 2026-10-31 · agent · contract — the stop and leftover-process checks assume `run.sh` ends with `exec`; found by FORMAT.md v1
  (One cause: D13's SIGTERM stop and D12's fixed `run.sh` are specified without the constraint that `run.sh` must `exec`
  the program, so that the signal reaches the program and not a bash parent; E1.S3's templates and F-RUN-D need it
  stated. Recorded after the stop as the rule asks: independent causes count one each, even when found in one reading.)
  (Resumed 2026-10-31 after the re-review: the four drafts judged `fits`, the plan round PROCEED, the document gate PROCEED
  with its five findings applied as the baseline; epoch 3 opens below at 0 changes.)

### Re-review for epoch 3

Narrowed: no — against the epoch 2 baseline every revision adds (a limits and timing contract for daemons and web APIs, the runner and grammar in the workspace, the `exec` rule, systemd-scope memory certification required before launch, the declared culture, attribution moved out of the agent's tree, the pre-freeze execution gate, FORMAT v2); no scope, Definition of Done, merge condition or release got smaller, checked against §9, §16 and the epics

| Work | Where | Verdict | Action |
|---|---|---|---|
| FORMAT.md v1, `d7c73ff` (E1.S8) | `dew_flow_ai_language_comparison`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| spec 01 conforms to FORMAT v1, `28bf777` (E1.S2) | `dew_flow_ai_language_comparison`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| pilot spec 05 with four scenario samples, `30af434` (E1.S2, E1.S6) | `dew_flow_ai_language_comparison` branch `docs/spec-05`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |
| pilot spec 04, `0b69a50` (E1.S2) | `dew_flow_ai_language_comparison` branch `docs/spec-04`, local, ungated draft | fits | kept as a draft; re-conformed to FORMAT.md v2 (E1.S9) by a fix forward before its gate (judged by the team lead, Opus, 2026-10-31) |

> Epoch 3 · approved 2026-10-31 · by reviewers · state: closed

- 3.1 · 2026-10-31 · agent · build-order, approach — the D40 pre-freeze gate needs the final scoring environment in E1, which the plan builds in E3, and the driver↔program network layout is designed nowhere; found by the E1 stories (`feat/lang-v2-e1` `51a80ba`, §9 OP-1)
  (One cause: D40, §14 step 1, E1.S2, S2a and S6 run the gate "in the final sandbox environment — the box, D15's
  environment with the culture, the program's own systemd scope" and need a layout in which the driver reaches a boxed
  daemon or web program, while the scoring box, the scope, the culture check and the drivers are E3 deliverables (E3.S2,
  S3, S5; T-L14, T-L30), and the network layout exists in no story: v1's scoring box has no network (`--unshare-net`,
  `spikes/minigit-bench/lib/bwrap.mjs:15`) and D36 puts the driver outside the program's scope, so a driver outside the
  box cannot reach the program's port. The options the stories name — a minimal launch layer built in E1 and widened by
  E3; a narrower E1 gate plus a second execution check in E3; the freeze waiting for E3 — are one change whichever is
  chosen. Until the step-4 revision, E1 proceeds except the gate run and the freeze.)
- 3.2 · 2026-10-31 · agent · contract — the bench repository `dew_flow_benchmark` is public and its plan carries the §4.3 trap table, which a process agent can reach through GitHub; found by the E1 stories (§9 OP-12)
  (One cause: D25 makes the trap list an internal test-design aid, "kept with the held-out material, never in a
  statement", yet this plan's §4.3 trap table and §4.1's per-task trap rows sit in a public repository — verified
  2026-10-31 with `gh repo view`: visibility PUBLIC — and are in merged history (#124, #127, #128); the process arm's
  egress reaches the GitHub hosts by SNI (§6, `EGRESS_HOSTS.langProcess`), so a process agent could read the aid. The
  options for the step-4 revision — move the aid to a private place and accept that the published history keeps the old
  rows; route the process agent's GitHub access through a broker that allows only the task repository's paths; accept
  and disclose it — go to a consult first. This second change stopped the plan.)
  (Step 4, 2026-10-31, after consult `16e5f408`: the aid moved to the private area with its hash in the plan (D41) and the
  process agent's GitHub access run-bound by a broker (D42) — both, with the public history disclosed as T18; the
  execution layer moved into E1 with the shared loopback namespace (D43, §6.1). Baseline, not entries.)

### Re-review for epoch 4

Narrowed: yes — the measured treatment of the process arm got smaller: by D42 it no longer has the family's open GitHub access (it cannot read other repositories' history, the task repository's `main` or other runs; it works in an isolated bare repository per run), so the process-arm result applies to "two stacks under the same run-bound GitHub policy", not under the family's open one; what did not get smaller: the tasks, the arms, the metrics, the acceptance criteria, the Definition of Done, the merge conditions and the release; the execution layer moves earlier and is shared (more is built in E1), and the trap aid changes place, not content (D1, D34); the merged public history cannot be unpublished, and making the bench repository private or rewriting its history is the owner's call — so the owner approves epoch 4 first (plan-change-control step 8), or chooses the alternative of keeping open GitHub access with the residual exposure accepted and disclosed, or a private bench repository

| Work | Where | Verdict | Action |
|---|---|---|---|
| the E1 stories document `todo/PLAN_lang_comparison_v2_E1.md`, `51a80ba` (planning, not on main) | `dew_flow_benchmark` branch `feat/lang-v2-e1`, pushed, unmerged | fits | kept; rewritten for E1.S8b, E3.S7a, OP-2 to OP-11 as text and the D41 rule (no trap content) before its own gate (judged by the team lead, Opus, 2026-10-31) |

> Epoch 4 · approved 2026-10-31 · by owner · state: closed

- 4.1 · 2026-10-31 · agent · contract — the known-task needle contract (D18, D33, D39) was underdetermined; found by the spec drafts 06 and 09
  (One cause, two symptoms. (a) D33 named the spec path `todo/tasks/09-werewolf.md`, but "Werewolf" is a D18 needle and
  `todo/tasks/` is in the run base that E1.S1 greps; the spec is now `todo/tasks/09-switched-routes.md`. (b) The needle
  matching rule was unspecified: a case-insensitive match — the maintenance draft's reading — makes "RESP" match
  "response" in FORMAT's own fixed sentence F-BYTES-W in every web spec, and "IOI" match `iOiJ`, the base64 of every
  JSON `":"` in task 6's JWT outputs, so every run-base cut would be refused. Decided: needles match case-SENSITIVELY on
  word boundaries (`\b<needle>\b`, ASCII letters and digits as word characters), plus an explicit list of exact
  substrings for product names that are not words (for example `redis.io`); base64 and JWT payloads in sample transcripts
  are excluded from the scan only where FORMAT marks them as a normalised field, otherwise scanned; the task-5 needles
  stand in D18; T-L37 tests the rule. The one change epoch 4 allows; no stop.)
- 4.2 · 2026-11-01 · owner · build-order — the series is built and run in groups, each group to evidence before the next: (A) the four olympiad tasks in the bare arm, (B) the bare arm for all ten tasks, (C) the process arm
  (One cause: the owner's choice of 2026-11-01, "A, then B, then C, group by group", after the team lead's estimate that
  the plan as written needs 1–2 weeks before its first paid round while group A needs 2–3 days: the olympiad tasks need no
  daemon or web runner (E1.S8a part ii, the drivers of E3.S2) and no process arm (E3.S7, E3.S7a), so the stdin/stdout path
  can reach a pre-registered paid round first. Nothing is dropped: every epic, task, arm, metric and test stays; the order
  of building and running changes, and each group gets its own pre-registration, A/A where the arm needs it, pilot and
  analysis. The whole-plan revision decides what that means for §14's steps, the epics' order, the pre-registration (D16,
  E4), the rounds and their seeding (D9), the cross-group comparability of results run on different days (model and CLI
  drift), and the budget frame. This second change stopped the plan.)

### Re-review for epoch 5

Narrowed: yes — D10's arm alternation, the control of T16, is lost: under D44 the bare arm (groups A and B) runs before the process arm (group C), so the process effect and the interaction of §9 are read across time windows; nothing else got smaller — the tasks, the arms, the metrics, the acceptance criteria, the Definition of Done, the merge conditions and the release stay, only the order changes; so the owner approves epoch 5 first (plan-change-control step 8; §17 Q-H)

| Work | Where | Verdict | Action |
|---|---|---|---|
| E1.S5 toolchains and the preflight, `ab470e06` (#135) | `dew_flow_benchmark` main | fits | kept: group A needs it unchanged, and its pins hold for the whole series (D47) (judged by Opus, the re-review's plan writer, 2026-11-01, on the consult's § 6) |
| E1.S1 bench side — `push-check`, `generateRunBaseTree`, `needleScan`, `workspace-diff` — `feat/lang-v2-e1-s1` at `dc2d91a4` | `dew_flow_benchmark`, local branch, never pushed, in its code review | fits | kept; a fix forward on the branch before its merge: the two push-check exemptions (a six-word window inside one whitespace token; any Markdown text published at `origin/main`) become exact approved values at approved places (D52); `workspace-diff` and T-L1 run over the task pairs that exist in each group (D44) (judged by Opus, 2026-11-01) |
| E1.S8a part i — lexer, parsers, matcher, olympiad path, report, `--parse-only`, the pin — `feat/lang-v2-e1-s8a` at `644ccc6a` | `dew_flow_benchmark`, local branch, never pushed, in its code review | fits | kept as group A's olympiad path; a fix forward on the branch before its merge: the test that two distinct 30-digit numbers stay unequal (V2 never calls `Decimal.normalize()` before equality, D52, T-L44); part ii is re-cut into ii-a (group A) and ii-b (group B) in the stories document, and no code of part i is reworked (judged by Opus, 2026-11-01) |
| the task repository joined to GitHub, `f5b9247` (task repository PR #1) | `dew_flow_ai_language_comparison` main | fits | kept (judged by Opus, 2026-11-01) |
| the FORMAT v2 draft and the ten spec drafts — branches `docs/format-v2`, `docs/spec-04`, `docs/spec-05`, `docs/specs-olympiad`, `docs/specs-daemon`, `docs/specs-web` | `dew_flow_ai_language_comparison`, local branches, never pushed, ungated | fits | kept as drafts; 01, 02, 03 and 09 run E1's per-task chain first (group A), the six others in group B; FORMAT's sections are classed before its gate (D45) (judged by Opus, 2026-11-01) |
| the exploratory pilots — task 1 (`cdd3349b`, #131; `research/lang-v2/EXPLORE_task1_2026-11-01*.md`) and the olympiad plan `todo/PLAN_lang_explore_olympiad.md` at `b45b9458` | `dew_flow_benchmark` main (task 1); branch `feat/lang-explore-olympiad`, local (the olympiad plan) | fits | kept as exploratory only and never v2 data; their materials reach v2 only through v2's own chain (the D40 gate, the joint validation, the mutants, the bias review), and group A's `prediction.md` discloses them (D44) (judged by Opus, 2026-11-01) |
| the E1 stories document `todo/PLAN_lang_comparison_v2_E1.md`, `08bc7a63` (#134) | `dew_flow_benchmark` main | fits | kept and reworked as text, not code (the consult's § 6 says "rework"; this record's verdicts are `fits`, `cut` or `—`): its waves and checkpoints re-cut for 01, 02, 03 and 09 first, E1.S8a part ii split into ii-a and ii-b, E1.S7 deferred to group C except the tag ruleset, one freeze tag per group, its Definition of Done per group — in the commit after this one (judged by Opus, 2026-11-01) |

  (Pending, 2026-11-01: the plan round on this revision, then the owner's approval of the narrowing and the answer to
  §17 Q-G. The line of epoch 5 is written only after them. Nothing was cut, so no revert is owed.)
  (Confirmed 2026-11-01 by the team lead (Opus, main session), judging apart from the writer: every verdict above checked
  against the code and the branches — fits, nothing cut. Two facts for the record: operator step O1b was done and read
  back the same day (the task repository allows squash and rebase merges only); D44's reading "group B is built while
  group A's rounds run, and B's paid sessions start only after A's interim record" is the team lead's, confirmed.)
  (Resumed 2026-11-01 after the re-review: the plan round PROCEED (codex; Gemini could not take the whole plan), the
  second full reading (the document gate, four reviewers) PROCEED with its six findings applied (`fc84e1f`, `2a1749e`),
  the owner's approval of the narrowing (§17 Q-H, answered "approve") and the owner's answer to §17 Q-G ("yes": the bare
  anchor round is part of group C, D54). Epoch 5 opens below at 0 changes.)

> Epoch 5 · approved 2026-11-01 · by owner · state: closed
- 5.1 · 2026-11-02 · agent · contract — under the memory view decided for OP-K (D15), a genuinely over-limit .NET program ends in the runtime's own abort ("Out of memory.", exit 134) before the kernel kills it, so `memory limit` (read from `oom_kill` alone) would call it a runtime error; found by E1.S8b's OP-K experiment (`spikes/lang-bench/inputs/opk-experiment-2026-11-02.json`)
  The rule: a run that ends in a runtime's own out-of-memory abort is `memory limit`, the same as a kernel `oom_kill`: .NET's native "Out of memory." abort and V8's "JavaScript heap out of memory" abort, both exit 134, recognised by their exact stderr line; the record names which. It is the same rule for both stacks. It changes the runner (E1.S8a), fixed forward on E1.S8b's branch before group A runs, with its tests and a re-pin. The first change of epoch 5: no stop.
- 5.2 · 2026-11-02 · agent · contract — under OP-K's own-cgroup view V8 caps Node's JS heap at about half of a 512 MiB limit while .NET may use all of it, so an object graph at 70 % of the limit is a false `memory limit` for Node only; found by E1.S8b's OP-K experiment (`followUp3`–`followUp5` of `spikes/lang-bench/inputs/opk-experiment-2026-11-02.json`)
  The fix the measurements support (policy A at 90 %, after `ask_consultants` `bf619f55`, 2 of 2 for A): each program sees its own cgroup read-only; .NET gets `DOTNET_GCHeapHardLimit` = 90 % of the limit (100 % failed a mixed workload at 256 MiB) and Node gets `NODE_OPTIONS=--max-old-space-size=<90 % of the limit in MiB>`, which D15 forbids today. Node's typed arrays outside V8's heap fail a mixed workload under every setting: a runtime property, recorded, not tuned. The second change of epoch 5: stopped for re-review.

### Re-review for epoch 6

Narrowed: no — against the epoch 5 baseline every revision adds (one memory policy for both stacks pinned as an identity, a `memory limit` rule with two causes and a tested evidence boundary, MiB units with the floor settled by a measurement that can only raise it, a calibration sequence whose evidence is a run under the registered limit, the fix forward of E1.S8a and E1.S8b with their re-pins); no task, arm, metric, acceptance criterion, Definition of Done item, merge condition or release got smaller — checked against §9, §16 and the epics; T-L43 keeps its churn and over-limit promises and adds the object-graph regression, and the mixed workload was never promised

| Work | Where | Verdict | Action |
|---|---|---|---|
| E1.S1 (#142, `6321cd24`) with the D52 fix forward (exact exemptions, T-L47) | `dew_flow_benchmark` main | fits | kept — merged and compatible: the push-check, the run-base tree and `workspace-diff` read no memory setting, so D55–D59 do not touch them (checked at #142, `6321cd24`) (judged by Opus, 2026-11-02) |
| E1.S8a part i (#141, `ced49afb`) and part ii-a (#147, `4f2cf79d`; the test-race fix and docs #149, `ba7c610b`) | `dew_flow_benchmark` main | fits | kept — merged and compatible: main's runner judges `oom_kill` as D56's cause (i) and contradicts no rule; cause (ii), its byte boundary and the flags `abort-below-evidence` and `child-abort-line` are additions, fixed forward on `feat/lang-v2-e1-s8b` (its own new pull request; nothing on main is reverted) and re-pinned as A's runner manifest (D59); part ii-b inherits the rule unchanged (T-L52) (judged by Opus, 2026-11-02) |
| the runner's abort rule 0.2.1 (`aa38d4b5`, change 5.1) and its evidence check (`ab321756`, fix batch 1 O7) | `feat/lang-v2-e1-s8b`, pushed, unmerged | fits | kept — `rs_abort.py` already reads exit 134 with the exact lines and needs `peakBytes · 2 ≥ maxBytes`, which is D56's rule; the boundary test (T-L50) and the two flags are added on the branch before its merge (judged by Opus, 2026-11-02) |
| E1.S8b — the layer, the delegated scope, the netns holder, the overlay view, the hardening, the identity and its pin (`e2c43546` … `f20e3b38`) | `feat/lang-v2-e1-s8b`, pushed, unmerged | fits | kept — the box, the scope, the netns holder, the overlay view, the hardening and the identity do not depend on the heap settings and fit D55–D59; their 100 % parts (`env.mjs`'s .NET key, the policy name, `memory.mjs`'s assertions, all from `cba07f92`) are judged in the next row; the probe's fixtures of T-L43 are added and the layer is re-pinned after the fix forward (D59) (judged by Opus, 2026-11-02) |
| OP-K decided at 100 % in D15 and in the layer (`ab51a33d`, `cba07f92`; `inputs/specs.d/memory-policy.json` = `cgroup-view-own+gchl-limit`) | `feat/lang-v2-e1-s8b`, pushed, unmerged | cut | the 100 % value contradicts D55; pushed and unmerged, so a fix forward on the branch, no revert and no history rewrite: `env.mjs` derives both stacks' keys at ⌊0.9·L⌋, `memory-policy.json` and `memory.mjs` name and assert `cgroup-view-own+gchl-90+node-old-space-90`; the own-cgroup view of `cba07f92` is kept; D15's 100 % text of `ab51a33d` stays under D55's superseded note; the 100 % evidence stays cited by D55 (judged by Opus, 2026-11-02) |
| the OP-K experiment and the probe records (`9f026617` … `9f297db7`; `inputs/opk-experiment-2026-11-02.json`, `inputs/exec-probe-2026-11-02.json`) | `feat/lang-v2-e1-s8b`, pushed, unmerged | fits | kept unchanged with their identities (D59); the next probe is a new dated file (judged by Opus, 2026-11-02) |
| the stop commit `443c8532` (ledger entries 5.1 and 5.2, the stopped status) | `feat/lang-v2-e1-s8b`, pushed | fits | kept — the ledger is append-only; entries 5.1 and 5.2 are unchanged by the revision `69c8fcf5` (judged by Opus, 2026-11-02) |
| docs on main since epoch 5 opened: #139 (`b7e48767`, the exempt public texts), #140 (`393533a3`, the D41 note), #144 (`9684c1f4`, the E1 status) | `dew_flow_benchmark` main | fits | kept — merged and compatible; none of them states a memory setting (judged by Opus, 2026-11-02) |
| the exploratory olympiad pilot part 2 and its records (#145 `1ceb9d2a`, #146 `280995cd`, #150 `bcd0971e`) | `dew_flow_benchmark` main | fits | kept as exploratory, never v2 data (D44); their 512 MB judge limit is not evidence for v2's floor (D57) (judged by Opus, 2026-11-02) |
| the task repository: nothing pushed since 2026-11-01 (`git log --all --since=2026-11-01` empty) | `dew_flow_ai_language_comparison` | — | nothing was built there in epoch 5; checked 2026-11-02 after a fetch: `git log --all --since=2026-11-01` is empty (judged by Opus, 2026-11-02) |
| the E1 stories document, revised in this commit for D55–D59 | `feat/lang-v2-e1-s8b`, local commit | fits | kept — text only; the judge's review findings on it go to the plan round before the resume (judged by Opus, 2026-11-02) |

  (Written 2026-11-02 by the re-review's plan writer (Opus). The Verdict column is the Opus judge's to fill at step 5 — `fits` or `cut` per row; the check `plan-lifecycle.mjs` accepts only `fits`, `cut` or `—`, so `—` stands as the placeholder and each Action cell says `pending (Opus judge)` with the writer's proposal. Nothing is proposed for cutting, so no revert is expected. Pending before epoch 6 opens: the judge's verdicts, the plan round and the second full reading of this revision, the resume. The epoch 6 line is written only after them.)
  (After the judge, 2026-11-02, commit `445298c9`: every row is judged — one `cut`, OP-K at 100 % in D15 and the layer, fixed forward on the branch, no revert; every other row `fits`; `Narrowed: no` confirmed. The note above is kept as written at step 4 — "nothing is proposed for cutting" and "pending (Opus judge)" are its words of that moment. The document gate passed in two parts (both proceed); its findings R1–R6 and the judge's J1–J11 are applied in the commit after `445298c9`. Pending: the epoch 6 line, written by the team lead at the resume.)

> Epoch 6 · approved 2026-11-02 · by reviewers · state: closed
- 6.1 · 2026-11-02 · agent · contract — T-L43 asserted Node's medium-buffers workload at 256 MiB, which ends in a kernel `oom_kill` at peak = L under every setting measured (typed arrays outside V8's heap, T21); found by the E1.S8b fix forward's T-L43 run and its Opus re-review
  The cell is run and recorded, never asserted, beside the mixed workload (T21); every other T-L43 cell stays asserted. Also, without counting as a change, D56's and T-L50's "without the flag" now say which flag: `abort-below-evidence` (a wording fix; `child-abort-line` still applies under any non-zero exit other than 134). The first change of epoch 6: no stop.
- 6.2 · 2026-11-03 · agent · contract — FORMAT's section classes gain a fifth class for the sections that serve daemon and web tasks and no olympiad task (the scenario script and the expected transcript); under D45 and E1.S9's "a section that serves two kinds is `common`" every section was `common`, so after A's freeze D45's daemon/web amendment path applied to nothing; found by CP1 (finding F14, `spikes/lang-bench/inputs/checkpoints.md`)
  Option chosen after `ask_consultants` `8b776383` (2 of 2 answered, both for it; the CP1 judge recommended it too) and decided before FORMAT's gate round, because a class change after the gate changes FORMAT's sha and forces every open spec to re-conform. This second change stopped the plan.

### Re-review for epoch 7

Narrowed: no — nothing is removed: change 6.2 restores D45's amendment path before B's freeze (a fifth class, D60) and adds the guards that make it hold (A's frozen classification; the grammar pinned per task, D61); change 6.1 records one Node cell instead of asserting it, and T-L43 never promised that every bounded workload passes (the epoch-6 record says the same of the mixed workload); D62–D64 add text, owners and tests; no task, arm, metric, acceptance criterion, Definition of Done item, merge condition or release got smaller — checked against §9, §16 and the epics

| Work | Where | Verdict | Action |
|---|---|---|---|
| the rules pin to release `5863c60` (#151, `0ea28fbe`, 2026-11-02) | `dew_flow_benchmark` main | fits | kept — merged and compatible: `0ea28fbe` changes only the `.agents/conventions` gitlink (`105d53c8` to `5863c60d`); no v2 file, and none of the rules it brings is one D60–D64 rely on; merged 2026-11-02 11:03 UTC, before the epoch 6 line (`4da00556`), and missing from the epoch-6 record, so judged here (judged by Opus, 2026-11-03) |
| the rules pin to release `cc24f9e` (#152, `c4666527`, 2026-11-02) | `dew_flow_benchmark` main | fits | kept — merged and compatible: `c4666527` changes only the gitlink (`5863c60d` to `cc24f9e3`), head `c98b5a02` on `chore/pin-cc24f9e`; no v2 file (judged by Opus, 2026-11-03) |
| E1.S8b whole with the fix forward of D59, the epoch 6 line and entry 6.1 (#153, `ae144e1f`, 2026-11-03; 1389 of 1389 `spikes/lang-bench` tests) | `dew_flow_benchmark` main | fits | kept — merged and compatible (head `846bfae5`, merged 2026-11-03 16:59 UTC in epoch 6 at one change): checked at HEAD — `lib/exec-layer/env.mjs:17` `TZ: 'UTC'`, `:69-82` refuses a base key in any case with only `TZ` replaceable; `lib/exec-layer/scope-entry.sh:27` writes 512 to `pids.max`; `runner/rs_pump.py:156-158` fails past `STDERR_KILL_BYTES` = 256 MiB (`runner/rs_exec.py:21`); `lib/exec-layer/run.mjs:191-198` refuses a data run off the identity, the policy or a stack; `lib/exec-probe/memory.mjs:45-53` records Node medium-buffers at 256 MiB (change 6.1). D63 (2) puts the manifest check in the gate harness, not in `runCases`, so the identity CP2 read stays. D62's F7 sentence is wider than `boxEnv` (finding J2): fix the FORMAT text, never the layer (judged by Opus, 2026-11-03) |
| the plans after E1.S8b's merge, the owner's two answers and the test-leftovers follow-up plan (#154, `4940d1ff`, 2026-11-03) | `dew_flow_benchmark` main | fits | kept — merged and compatible: `4940d1ff` (head `555ffcb6`) changes status lines of both v2 plans, one line of `research/module_lang_bench.md` and adds `todo/PLAN_bench_test_leftovers_followups.md`, which has no ledger and is "plan only" with each story's plan round still to come (its `:3`, `:74`), so the S1 boundary paragraph added by `4132c621` is not a change there; E1.S8b's text and that S1 now name the same boundary (CP2 condition 4, D63 (4)) (judged by Opus, 2026-11-03) |
| CP2 recorded — passes with conditions (#155, `2ba5bc16`, 2026-11-03) | `dew_flow_benchmark` main | fits | kept — merged and compatible: `2ba5bc16` (head `24c70834`) appends CP2 at `spikes/lang-bench/inputs/checkpoints.md:7-45`; the revision does not touch that file; D63 (1)–(4) match CP2's conditions (1)–(4), D63 (5) its condition (5) and F5, D63 (6) its F6; D63 (2) takes the gate harness, one of the two places CP2 allows (judged by Opus, 2026-11-03) |
| the stop commit `506e7d2f` (entry 6.2, the stopped status, the CP1 record) | `dew_flow_benchmark` main, #156 `ec8fc46a` (the squash of `506e7d2f`, branch `docs/lang-v2-stop-6-2`, merged 2026-11-03 19:08 UTC; `git diff 506e7d2f ec8fc46a` is empty) | fits | kept — merged and compatible (the writer's cell said "pushed, unmerged"; `gh pr view 156` says merged): the merge is the stop record itself (step 1), not v2 work, so it breaks no halt; the revision inserts after the ledger (hunk `@@ -245,0 +246,18 @@`) and changes no entry and no line of `checkpoints.md` (judged by Opus, 2026-11-03) |
| FORMAT v2.2 — the section classes and F-MEMORY's two causes (`28b93ba`, after the merge of the task repository's `main`, `b9d54d1`) (E1.S9) | `dew_flow_ai_language_comparison` branch `docs/format-v2` (worktree `/home/jinx/git/wt/tasks-format-v2`), local, never pushed, ungated | fits | kept on the branch as a draft — local, never pushed (no remote branch; worktree clean; `git log --all --since=2026-11-01` lists only `28b93ba` and `b9d54d1`), ungated, never v2 data, so no revert: `FORMAT.md:3` reads `Format version: 2.2`, `:17-30` has four classes and every section `common` — what D60–D63 fix forward. The fix forward is E1.S9 work after epoch 7 opens (§14 A1), never during the stop, and it must also carry the answer to finding J1 (daemon/web text inside `common` sections 2, 3 and 6), since a class change after the gate changes FORMAT's sha (judged by Opus, 2026-11-03) |
| E1.S4's branch `feat/lang-v2-e1-s4` — no commits beyond `main` `2ba5bc16`; its plan round passed; its brief is being written outside git | `dew_flow_benchmark`, local branch | — | nothing built — `feat/lang-v2-e1-s4` = `2ba5bc16`, 0 commits ahead of `origin/main`, worktree `/home/jinx/git/wt/bench-lang-e1-s4` clean. The brief `/home/jinx/rsd/lang-v2/briefs/s4-brief.md` (756 lines, 2026-11-03 20:14) is outside git and is not code; it names nothing D60–D64 change, but its line references are E1 at `2ba5bc16` (§ E1.S4 "1183–1222"; at this revision the story starts at `:1247`). Before the coder starts: rebase on the `main` that carries the epoch-7 revision, refresh the brief's references, then the interim path the writer names (judged by Opus, 2026-11-03) |
| the E1 stories document, revised in this commit for D60–D64 and change 6.1 | `dew_flow_benchmark` branch `docs/lang-v2-epoch7-revision`, local commit `815aabb3`, never pushed | fits | kept — text only, local, so nothing to revert; the word diff from `ec8fc46a` only adds text (each removed line comes back longer: `recorded;`, `workload;`), no story, test or Definition of Done item is removed; findings J1 and J3–J6 apply to its E1.S9, §3 and E1.S2a text and are fixed in the re-review gate round, before the epoch 7 line (the writer's cell named branch `docs/lang-v2-stop-6-2`; corrected) (judged by Opus, 2026-11-03) |
| the whole-plan revision itself — the parent plan (status, D60–D64, T-L56–T-L59, §14, §16, the epic map, this record), `todo/README.md`, and the leftovers plan's S1 boundary paragraph (`815aabb3`, `4132c621`) | `dew_flow_benchmark` branch `docs/lang-v2-epoch7-revision`, local, never pushed | fits | kept — text only, local; settles change 6.2's three parts (A's frozen classification read by B, D60, T-L56; the grammar per task, D61, T-L57; FORMAT's permitted and refused pair on real files, D64, T-L56), CP1's F5/F6/F7 (D62, T-L58) and CP2's conditions (1)–(4) (D63, T-L59, E1.S8b) with owners and milestones; `Narrowed: no` confirmed. **Finding J1 blocks the resume**: fixed in this revision, before the epoch 7 line; J2–J6 before the stage each names (judged by Opus, 2026-11-03) |

  (Written 2026-11-03 by the re-review's plan writer (Opus). The Verdict column is the Opus judge's to fill at step 5 — `fits`, `cut` or `—` per row; `—` stands as the placeholder, and each Action cell says `pending (Opus judge)` with the writer's proposal. Nothing is proposed for cutting, so no revert is expected. Not a row: #148 (`59851c4b`, 2026-11-02, the R9700 speed options) changes only `research/vllm-r9700-speed-options/`, its results file and `research/README.md`, outside this plan, and was merged before epoch 6 opened. #151 was also merged before the epoch 6 line (`4da00556`, 2026-11-02 16:27, on `feat/lang-v2-e1-s8b`), but the epoch-6 record does not list it, so it is a row here. The task repository's other branches (`docs/spec-04`, `docs/spec-05`, `docs/specs-olympiad`, `docs/specs-daemon`, `docs/specs-web`) have no commit since 2026-10-31. Pending before epoch 7 opens: the judge's verdicts, the re-review gate on this revision, the resume. The epoch 7 line is written only after them.)
  (The Opus judge, 2026-11-03, step 5: ten rows — nine `fits`, one `—` (S4, nothing built), no `cut`, so no revert and no fix forward of done work. Two Where cells corrected (the stop commit was merged as #156; the revision is on `docs/lang-v2-epoch7-revision`) and a row added for the revision itself. Not a row: nothing else since epoch 6 opened — `origin/main` since 2026-11-02 is #151–#156, and the task repository's work is `28b93ba` and its merge `b9d54d1`. `Narrowed: no` confirmed: the epoch-6 baseline of T-L43 never named medium-buffers and promised no bounded workload, and the diff removes no task, arm, metric, Definition of Done item, merge condition or release. Findings: **J1 (blocks the resume)** — classes per `##` section leave daemon/web-only text inside `common` sections (FORMAT 2.4 and 2.5, the daemon and web sentences and the timing contract; 3.3; 6.2 and 6.3), so after A's freeze B cannot amend them: F14 again, one level down; class the mixed sections per `###` (or move that text) in D60, E1.S9 and T-L56 before the epoch 7 line. J2 — D62's F7 "any documented runtime setting of either stack" is wider than `boxEnv` refuses (`env.mjs:34,69-82,101`); narrow the text or refuse it in the conformance check, never in the layer, with a T-L58 case. J3 — D64 "not relied on at that freeze" against E1 §3 "blocks the freeze commit"; use the second. J4 — D64 names a pair only for FORMAT (T-L56); name each rule's pair, owner and milestone. J5 — the changelog is section 7 (`common`) yet must grow, the preamble with the class table is in no section, and a B section inserted before section 7 renumbers its heading; say how the check treats each. J6 — tests without a name or owner: T-L56's added-`common`, changed-A-changelog and header-row cases, T-L59's record fields, T-L58's `pidsMax` in E4.S3; D45 still says one grammar hash per group.)

> Epoch 7 · approved 2026-11-04 · by reviewers · state: closed

- 7.1 · 2026-11-04 · agent · contract — OP-7 of E1.S3: `Microsoft.AspNetCore.Mvc.Testing` joins the offered C# set and supersedes the C# snapshot, because Fastify's `inject()` gives Node an in-memory test host and C# had none
  (The consultants settled it, 2 of 2, `4ee42a91`. Not built.)
- 7.2 · 2026-11-04 · reviewer · contract — E1.S3's reviews: the same slip failed the C# build and passed the Node build, so the C# build line of E1.S3's brief changes and the templates change with it
  (This second change stopped the plan. The C# build fails only on `nullable`, `NU1603`, `NU1605` and `SYSLIB0011`, forced on the command line of the frozen `build.sh`, with `--no-incremental`; `global.json` selects the test platform; `tests/` and nested `bin`, `obj` and `node_modules` leave the compile; the Node `.npmrc` gains `update-notifier=false`. Consultants `ae1528d1`, 2 of 2. Built on `feat/lang-v2-e1-s3`, unmerged, #167, closed for the stop. E1.S8, #164, also unmerged, holds no change of its own and waits with it.)
  (Recorded late: the team lead first judged these decisions as brief-level and told the owner that no plan change was used; the rule says "in doubt, it is a change", and the brief's build line is plan text. Work done on 2026-11-05 before this commit was the fix rounds of those same decisions. Nothing merged in that time.)
  (Evidence for the re-review: T15 says the process arm's C# bundle carries warnings as errors, and the frozen line now forces that setting off; a proposed E3 scorer rule regenerates `App.csproj` and `package.json`; FORMAT 1.2's tag form differs from D45's.)

### Re-review for epoch 8

Narrowed: no — nothing is removed: change 7.1 adds a package and shows the offered set works; change 7.2's narrower C# failure list is the acceptance policy of both stacks (D66), and the warnings as errors of T15's bundle stay in the process arm as a required CI job, with no template byte changed for it; D65 scores more of what the agent submitted (no regeneration, no refusal) and adds a metric; D67 adds tests, three stories and an order; D68, as rewritten after the judge's finding, keeps the old merge condition (one whole green run, CI's checks) and only adds a record and a stop; no task, arm, metric, acceptance criterion, Definition of Done item, merge condition or release got smaller — checked against §9, §16 and the epics. Q-J is settled as required, so the process arm's treatment stays whole

| Work | Where | Verdict | Action |
|---|---|---|---|
| E1.S4, the package sets and both snapshots (#159, `796e7c3a`, 2026-11-04) | `dew_flow_benchmark` main | fits | kept: merged and compatible. E1.S4b supersedes the C# snapshot as new work; the Node snapshot and every licence record stand. Checked: the recipe installs with `--ignore-scripts` (`inputs/cache-recipe-lang.json`) |
| E1.S9's bench tools, the FORMAT tools (#160, `98141a98`, 2026-11-04) and its gate record (#161, `df2e7a74`, 2026-11-04) | `dew_flow_benchmark` main | fits | kept: T-L56 and T-L58 stand; `inputs/specs.d/format.json` moves forward with FORMAT's amendment and the gate record appends; nothing is rewritten |
| FORMAT v2, gated (task repository PR #2, `40e678f`, 2026-11-04) | `dew_flow_ai_language_comparison` main | fits | kept, fixed forward by FORMAT's amendment, never a revert: no spec is gated on it and no D40 gate has run on it |
| E1.S3 phase A (branch `feat/lang-v2-e1-s3`, head `6f768772`, 25 commits; PR #167 closed for the stop, unmerged) | `dew_flow_benchmark`, pushed, unmerged | fits | kept, fixed forward; no code changes before its merge. **Before the merge, on the branch, a doc commit:** merge `main` (it conflicts in the E1 table, at the E1.S3 row); mark "the rule for E3's scorer" and "Input for E3's plan round" in `research/module_lang_bench.md` superseded by D65; the module's frozen list and build line, and the E1 status row, cite D65 and D66. The 21-row table stands as D66's evidence. **After the merge, each in its story:** E1.S3b, `NPMRC_WORKSPACE` and the three `.npmrc` (`test/templates.test.mjs` follows the constant, and `stack-files.json` does not move, as no new file name joins the list); FORMAT's amendment, also `PINNED_TASK_SHA` (`lib/task-repo.mjs:16`) and FORMAT's line of `inputs/hashes-lang.txt`, which pin the old FORMAT and which the plan does not name yet; E1.S4b, the module's known limit on `JwtBearer`; E1.S8c, the module's sentence that the runner is not scanned |
| E1.S8, the conformance check (branch `feat/lang-v2-e1-s8`, head `3f56ac24`, 42 commits; PR #164 closed for the stop, unmerged) | `dew_flow_benchmark`, pushed, unmerged | fits | kept; no code changes before its merge; it merges `main` with no conflict and reads FORMAT's sentences from FORMAT. In FORMAT's amendment: `lib/conformance-check-structure.mjs:47` still accepts a tag with no group letter, so it then requires one |
| The stop commit (entries 7.1 and 7.2, the stopped status; `b631c03d`) | `dew_flow_benchmark` branch `docs/lang-v2-epoch7-stop`, PR #168 open | fits | kept: step 1's record, not v2 work; #168 is still open, and its merge breaks no halt |
| The rules pin to release `bd79584` (PR #166, head `7a7a0f18`, the gitlink only) | `dew_flow_benchmark` main, #166 `a37f31c7`, merged 2026-11-05 | fits | kept: merged 2026-11-05 as `a37f31c7`, after this row's Where cell was written; the gitlink only, no v2 file. Read at `bd79584`: one rule changes (`common.test-leftovers-baseline`) and F3's guard reads `package.json` scripts; neither touches D65–D68, and the templates' `package.json` have no `scripts`. This revision and both story branches get it by merging `main` |
| This revision: the parent (status, D65–D68, the dated lines, §6.2, T23, T-L60–T-L68, §14, §16, §17, the epic map, this record), the E1 stories document, the leftovers plan's S5, `todo/README.md` | `dew_flow_benchmark` branch `docs/lang-v2-epoch8-revision`, local, never pushed | fits | text only, local, nothing to revert; it is the baseline once the judge's findings are answered. `Narrowed: no` is not confirmed for D68: the line or D68 changes before epoch 8 opens |
| The review extract of this revision (`892e5a7a`, branch `review/lang-v2-epoch8`, one file `review/epoch8-revision.md`, on top of `c32643fc`) | `dew_flow_benchmark`, local, never pushed | — | added by the judge; nothing was built: the text for the gate round, never merged; it may be dropped after the gate |
| The second review extract of this revision (`c09157b4`, branch `review/lang-v2-epoch8b`) | `dew_flow_benchmark`, local, never pushed | — | nothing was built: the text for the gate round, never merged; it may be dropped after the gate |

  (Written 2026-11-05 by the re-review's plan writer (Opus). The Verdict column is the Opus judge's to fill at step 5. Nothing is proposed for cutting, so no revert is expected. Not a row: #158, #162, #163 and the open #165 change only the model-arms plan and its documents, outside this plan; the scratch measurement of D65 was made under `~/.cache/lang-v2/e8/` and removed. Consult `d2e1e648` is step 3; its citations were checked against the files: plan lines 350 and 491, FORMAT's `NU1603` case, §14's order. Step 7: the epics' boundaries did not change, three stories join E1, so no re-split by Fable is proposed, and the cornerstones stay 3 of 3. Pending before epoch 8 opens: the judge's verdicts, the fix forward the judge orders, the re-review gate on this revision, the resume. The epoch 8 line is written only after them.)
  (The fix round on the judge's report, 2026-11-05, from `650c049a`: the verdicts above are the judge's and are not changed; #166's Where cell is corrected. Step 7: no Fable re-split, because no epoch's goal, scope or boundary moved; S3b, S4b and S8c are stories inside E1, written by Opus, no new area is a cornerstone, and the ledger stays 3 of 3. The pin: release `bd795847` is on `main` (#166), and nothing changed for this plan except its row, by the judge's reading. Q-J is settled as required and Q-I as the shorter sentence, neither needing the owner. D68 is rewritten and `Narrowed: no` rests on that rewrite: no text of this revision is smaller than before, checked in the fix round's report. Pending before epoch 8 opens: the re-review gate on this revision, the resume.)
  (Dated 2026-11-05, what moved after the judge's cells were written; no Verdict is changed. `Narrowed: no` is confirmed by the judge at `0bde4418`, and K1 to K6 and L1 to L4 were read for narrowing by the team lead. The stop row's head is now `9e245190`, with two merges of `main` and no v2 content. #167's plan pins are now named, its "no code changes" holds, and D68 also needs its marker failures read and a whole green run before its merge. #164 needs its own whole completion run at its head. #165 merged; it is model-arms only and is not a row. The gate and the resume named as pending above are done: the epoch 8 line stands below.)

> Epoch 8 · approved 2026-11-05 · by reviewers · state: closed

- 8.1 · 2026-11-05 · owner · scope, build-order, merge-conditions — the owner orders a two-day quick run of the daemon and web tasks with the gates off: run it, look, fix from what really happened
  (The owner's words: cut it to 2 days; temporarily remove all the bureaucracy; run it, see, and if there are errors finish it from the facts. Results are exploratory, labelled so. The series plan below is paused, not dropped. Brief: `~/rsd/lang-v2/briefs/quick-run.md`.)
  (2026-11-05, the same cause, counted as this one change: the owner then said to run the gates before this code reaches `main`, and lifted the spend stop for the quick run, so each pair's record keeps the owner's words through `--allow-over`. The landing's plan round passed on 1 of 2 reviewers, the second vendor being out of quota, and its two findings were accepted. Whether the quick run counts as a further change to the paused series is decided before that series resumes. Brief: `~/rsd/lang-v2/briefs/quick-landing.md`.)
- 8.2 · 2026-11-06 · agent · contract, approach — one scoring pass cannot be the verdict of a program with a race, so the plan's scoring must repeat and must say what a mixed result means
  (This second change stopped the plan. The evidence: one quick-run C# session has a real use-after-release of a pooled request buffer. On the two scoring CPUs it failed 19 of 24 whole-set re-scores, so it passed about 1 in 5 by chance; on all CPUs it passed 93 of 93. It was scored once and happened to fail. The owner said on 2026-11-06 to fix everything and go on; the rule counts an owner's request like any other.)
  (The consultants, `e66d6061`, 2 of 2 answered: ending the pause is not itself a change; what the quick run taught is independent causes, not one; a whole re-review with all the evidence together comes before any more series work; the re-review's revisions are the next epoch's baseline, not entries.)
  (Evidence for the re-review, each judged there against the plan as written: the stated memory bound against the enforced limit, which drove most of task 07's cost gap; the time partition's `build_test` missing `bash build.sh`, so C# reads 0; all scoring on CPUs 6 and 7; #173's runner, now 0.3.9 with the daemon and web path, against E1.S8 and later stories and the pins; plan ids in agent-visible comments, removed in #173, against E1.S8c and T-L64; defects in the draft specs; FORMAT's amendment and the stopped pins branch; the O3 rulesets not yet applied; the items in `~/rsd/lang-v2/briefs/s3b-carry.md`. Analysis: `research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md`.)

### Re-review for epoch 9

Narrowed: yes — the owner narrowed the scope on 2026-11-06 (bare first, process later); everything that got smaller is the list *What got smaller* in the operative part, for the owner's approval: Stage 1 is six tasks, not ten (the olympiad four become exploratory controls); the process arm goes to Stage 2, behind a dated decision; E0's conventions parity audit and release go to Stage 2; D57's and D58's calibrated limit is replaced by one fixed 512 MiB value; five pairs per task in one arm (60 data sessions) instead of three per task per arm in two (120); the staged groups go, with A's replication in B, the drift table and the interim records; one pilot and one freeze instead of one per group, CP3, CP4 and CP4-B replaced by six document gates; D65's lexical deviation taxonomy and the per-case CPU and RSS curve leave Stage 1 as follow-up work; G-15 and G-19 lapse; the publication of the references, the trap table and the specs waits for the Stage 2 decision line (at cancel) or Stage 2's end

| Work | Where | Verdict | Action |
|---|---|---|---|
| #170, the marker test waits, with a bound, for its two races in its own checks (`cbfad3a2`, merged 2026-11-05 12:33 UTC; head `930c7174`, branch `fix/lang-v2-marker-test-race`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked with `gh pr view 170`: 5 files, two lang-bench tests and their support file, `research/module_tests.md` and the leftovers plan's S5 lines; head `930c7174`, merged as `cbfad3a2`; no harness code and nothing of Stage 2. Nothing follows. The writer's reading: test and doc only (5 files, two tests, the leftovers plan's S5 lines); it is D68's first case, diagnosed before #167 merged; nothing in F1–F4 contradicts it |
| #164, E1.S8, the spec conformance checker (`8dcbdee2`, merged 2026-11-05 13:08 UTC; head `7b4d29ea`, branch `feat/lang-v2-e1-s8`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 57 files, the checker, its tests and synthetic fixtures under `spikes/lang-bench/`, the module docs; D68's whole run is at `081ace5f`, and the head `7b4d29ea` adds only a merge of `main` that carries #171 (research files outside this plan); CI's checks are present and green on the head. It builds no scorer, so D70 leaves it standing. What follows: the required group letter and `V<n>` in any case come with F1.S1 (e). The writer's reading: merged in epoch 8 before entry 8.1; F2.S1 uses it as each package's conformance check; the group-letter check it still lacks is on `feat/lang-v2-format-amend-pins` (below) |
| #167, E1.S3 phase A, the templates, the builder and the hash-checked fetch (`c8595952`, merged 2026-11-05 13:45 UTC; head `0c67730c`, branch `feat/lang-v2-e1-s3`, with the doc commit `714176db` marking E3's scorer rule superseded by D65) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 28 commits, 79 files; the head `0c67730c` holds `714176db` (one file, `research/module_lang_bench.md`, 4 lines changed); D68's whole run is at the head and CI's checks are green there. Its last fix round had no SAFE verdict from Opus, as its PR says; no defect beyond the carry items is known. It builds templates and the builder, not a scorer, so nothing in it contradicts D70 to D74. What follows: F1.S4 adds the two `.npmrc` keys. On the letter case, carry item 1 records that the build in place and on the clean copy already agree, so F1.S1 (c) is a doc line and a pinning test, not a change of the copy (the judge's report). The writer's reading: the quick run's workspaces come from these templates; phase B is F2.S2; the case-blind C# exclusion and the Node `.npmrc` keys are fixed forward in F1.S1 and F1.S4, never reverted |
| #168, the stop of epoch 7, entries 7.1 and 7.2 (`7a5720bf`, merged 2026-11-05 10:32 UTC; head `9e245190`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 3 commits, one file, the plan, 9 lines added and 2 removed; step 1's record. Nothing follows. The writer's reading: step 1's record; merged before the epoch 8 line, where it was a row as an open pull request; listed here for its merged state |
| #169, the epoch-8 revision, D65–D68 and the epoch 8 line (`b3bbb741`, merged 2026-11-05 11:16 UTC; head `87436600`, branch `docs/lang-v2-epoch8-revision`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 4 files, all plan text (the parent, the E1 stories document, the leftovers plan, `todo/README.md`), merged as `b3bbb741`; the word diff of this revision against `main` removes no word of D65, D67 or T-L64 and only adds dated lines. Nothing follows. The writer's reading: text only; the parts of D65, D67 (4), T-L64, E1.S3b and E1.S8c that Stage 1 does not build are marked superseded by D70 and D74, never removed |
| the task repository's PR #3, FORMAT's first amendment (`4ddb8f8`, merged 2026-11-05 13:46 UTC, branch `docs/format-v2-amend`) | `dew_flow_ai_language_comparison` main | fits | kept (the Opus judge, 2026-11-07). Checked with `gh pr diff 3` on the task repository: one file, `todo/tasks/FORMAT.md`, 13 lines added and 7 removed, 4 commits; F-OFFLINE now says the clean copy is the tree as left with the frozen inputs put back, as D65 and F1.S4 say; the `Freeze tag` row needs a group letter A or B, which F2.S2 uses; F-FROZEN names the pin files by `build.sh`; MB became MiB. The F-LIMITS-D sentence that D72 contradicts was there before #3 (#3 changed only its unit), so it is not this pull request's to cut; F1.S3's second amendment changes it. No other branch of the task repository is ahead of its `main` since 2026-11-05. The writer's reading: F-OFFLINE, the freeze tag's group letter, the plain freeze-rule row, 256 MiB, three document rounds; the bench's pins do not follow it yet (the pins branch below); F-LIMITS-D's bound "given to the test runner, not to the program's author" contradicts D72 and gets a second amendment in F1.S3 |
| #173, the quick run: lang-explore's daemon and web path, the runner 0.3.9 (`rs_net`, `rs_session`, `rs_conn`), the re-score and the replay (`00d88233`, merged 2026-11-06 16:39 UTC; head `ac165f02`, branch `feat/lang-v2-quick-run-land`, deleted on the remote) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 2 commits; 164 files by `git diff --stat` (`gh pr view` lists only the first 100): `spikes/lang-explore/`, the runner and its tests, 9 files of `spikes/minigit-bench/lib/`, the pins (`runner-versions.json` holds 0.3.9), the plan's entry 8.1 and four olympiad notes; the tree of `f3b0df19` equals `feat/lang-v2-quick-run` (`33f258c2`) and `ac165f02` adds the CodeQL fixes; the plan-id test is `spikes/lang-explore/test/quick-agent-visible.test.mjs`, and its pattern also catches the F ids; CI's checks are green on the head; no process-arm code. Its gates were a partial panel (plan round 1 of 2 reviewers, code rounds 4 of 8), as its PR and entry 8.1 say. What follows: fix forward in F1.S1 (b) and (d) and in F1.S3, never a revert. The writer's reading: the base of Stage 1 (D70); built with the gates off under entry 8.1 and gated before its merge; its results stay exploratory; it removed the plan ids from the agent-visible runner files with a test (T-L64's text half, F1.S1 (f)); it re-pinned the runner manifest at 0.3.9 |
| #174, the rules pin to release `37b9aa5` (`08b78dd2`, merged 2026-11-06 17:03 UTC; head `40688df6`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: one file, the `.agents/conventions` gitlink, one line changed, merged as `08b78dd2`. Nothing follows. The writer's reading: the `.agents/conventions` gitlink only; no v2 file |
| #175, the quick run's analysis and its six task notes (`1a0fa0d4`, merged 2026-11-06 17:12 UTC; head `b07ae3d7`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: 8 files, `research/README.md` and seven new files under `research/lang-v2/`, each labelled exploratory; the line scan's needles, run over every line added to `main` since 2026-11-04, find no held-out or reference line in them (its only matches are three generic lines in other files). Nothing follows. The writer's reading: research only, labelled exploratory; D72 and T26 cite its task 07 reading |
| #176, the stop of epoch 8, entries 8.1 and 8.2 (`9336a774`, merged 2026-11-06 17:23 UTC; head `de69dabb`) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-07). Checked: one file, the plan, 6 lines added and 2 removed; its commit was made after #173, #174 and #175 had merged, and nothing merged to `main` after it, so the halt was kept. Nothing follows. The writer's reading: step 1's record; this revision's base |
| `feat/lang-v2-format-amend-pins`: the pins follow FORMAT's first amendment, its gate record, the checker's freeze-tag group letter and `V<n>` in any case, FORMAT-MAINTENANCE, a plan note (`92e5dca8` … `b7bba0d3`, 5 commits on `c8595952`, 2026-11-05 13:49–13:56 UTC) | `dew_flow_benchmark`, local, never pushed (worktree `/home/jinx/git/wt/bench-format-pins`) | fits | kept as a fix forward (the Opus judge, 2026-11-07). Checked: `git ls-remote` has no such branch; 5 commits on `c8595952`: the pins and `lib/task-repo.mjs` at `4ddb8f80`, the gate record, the checker's tag test now `spec-frozen-[AB]-` with the letter required, FORMAT-MAINTENANCE, and `b7bba0d3` (3 files, 5 lines: D65's row and one §14 line of the parent, one line each of the E1 document and the module doc). `git merge-tree` is clean against `main` and against this revision's head. What follows: F1.S1 (e) merges `main` into it, runs D68's whole run and the two `--check` tools, and opens its pull request with the gates. The writer's reading: F1.S1 (e) carries it as a fix forward after merging `main`; its plan note `b7bba0d3` edits D65's row and one line of §14, which this revision leaves untouched, so the two merge without a conflict there |
| `feat/lang-v2-e1-s7-rulesets`: no commit (the branch is `c8595952`); five untracked files in its worktree (`github-rulesets.mjs`, `lib/github-lang.mjs`, two tests, a fixture; 570 lines, 2026-11-05 14:01–14:04 UTC) | `dew_flow_benchmark`, local branch, uncommitted (worktree `/home/jinx/git/wt/bench-e1-s7`) | fits | kept, local (the Opus judge, 2026-11-07). Checked: the branch is `c8595952`, and the five untracked files are a red step: the two production files (14 lines) are stubs that say NOT BUILT YET, and 556 lines are tests and a fixture; the tests still name group A and group C, which D69 removes. Nothing is committed or applied: the task repository's rulesets list is empty. What follows: F2.S2 builds the code from this red step, renames the groups to Stage 1's terms, and applies only the tag ruleset and `main`'s protection, with read-back. The writer's reading: F2.S2 applies the task repository's tag ruleset and `main` protection from these files once they are committed and gated; no ruleset exists there yet (`gh api repos/{owner}/{repo}/rulesets` lists none, 2026-11-07) |
| `feat/quick-run`: the olympiad re-run's notes, the rehearsal spend fix, task 01's second series, `--accept-noisy`, the 8.1 entry (`1458beff` … `4ba1e934`, 5 commits, 2026-11-05) | `dew_flow_benchmark`, local, never pushed (worktree `/home/jinx/git/wt/bench-quick`) | fits | nothing to keep apart from #173 (the Opus judge, 2026-11-07). Checked: `git merge-base --is-ancestor` puts its tip `4ba1e934` under `feat/lang-v2-quick-run`; `git diff 33f258c2 f3b0df19` is empty; its 8.1 entry reached `main` through #173 (`git log -S`). Local; it may be dropped. The writer's reading: every commit is an ancestor of `feat/lang-v2-quick-run`, whose tree #173's first commit squashed (`git diff 33f258c2 f3b0df19` is empty); nothing on it is outside #173 |
| the quick run's sibling branches: `feat/q-suites` (`1458beff`), `feat/q-runner` (`5f064e89`), `feat/q-harness` (`4b45098d`), `feat/q-fix` (`55fa09d1`), the replay worktree's detached `e9dabf5a`, and `feat/lang-v2-quick-run` (`33f258c2`, 77 commits) | `dew_flow_benchmark`, local, never pushed | fits | nothing to keep apart from #173 (the Opus judge, 2026-11-07). Checked: each of `1458beff`, `5f064e89`, `4b45098d`, `55fa09d1` and `e9dabf5a` is an ancestor of `feat/lang-v2-quick-run`, and none of these branches is on the remote. Local; they may be dropped with their worktrees. The writer's reading: each is an ancestor of `feat/lang-v2-quick-run`, and that tree is #173's; local, so they may be dropped once judged |
| this revision: the operative part for epoch 9 (D69–D76, F1–F4, T25–T28), this record, the epoch lines, the status, the superseded lines in the older sections, the E1 stories document's line, `todo/README.md`, `research/module_lang_bench.md` | `dew_flow_benchmark` branch `docs/lang-v2-epoch9-revision`, local commits, never pushed | fits | text only, nothing to revert (the Opus judge, 2026-11-07). Checked: 5 local commits on `9336a774`, 4 files, 389 lines added and 8 removed; the word diff of the parent removes one word, `stopped`, in the epoch 8 line; `leakscan-lines.py . history` exits 0, and the operative part holds no held-out case id and no trap word of the private aid. What follows, before epoch 9 is read as open: the judge's findings of 2026-11-07 are answered, the owner sees what else got smaller, and the re-review gate runs. The writer's reading: text only; no D, T, T-L row, result or history is deleted |

  (Written 2026-11-07 by the re-review's plan writer (Opus). The Verdict column is the Opus judge's to fill at step 5. The brief asked to leave it empty, but `plan-lifecycle.mjs` refuses an empty cell (it accepts only `fits`, `cut` or `—`), so `—` stands as the placeholder in every row, as in the epoch-6 record, and each Action cell says `pending (Opus judge)` with the writer's reading. Nothing is proposed for cutting. Not a row: #171 and #172 change only `research/vllm-r9700-speed-options/`, outside this plan. Step 7: the epics changed, so Fable re-split them (the split of 2026-11-07, `~/rsd/lang-v2/briefs/epoch9-split.md`) after consult `0a205591`; the cornerstone ledger is recounted as 2 of 2 for E = 4. The epoch 9 line below is written on the team lead's instruction, approved by the owner's narrowing of 2026-11-06; the judge's verdicts and the re-review gate on this revision come after this commit.)
  (2026-11-07, after the judge's verdicts: the epoch 9 line that the writer's commit `554d84a1` put here is removed, and epoch 8 is `stopped` again. Step 8 writes the epoch 9 line only after the re-review gate and the owner's approval of the narrowing; the record may stand without it until then. The judge's cells above are unchanged.)
  (2026-11-07, after the plan gate (`proceed`, 2 of 2 reviewers): its operator ordered 2 or 3 epics of 2 or 3 stories each, so F1 to F4 are now three epics, every story's content and each epic's done-when kept. The ids in the cells above are the ones the judge read: old F1.S3 is now part (b) of F1.S2; old F1.S4 and F1.S5 are F1.S3; old F3.S1 and F3.S2 are F3.S1 (a) and (b); old F3.S3, F3.S4 and F3.S5 are F3.S2 (a), (b) and (c); old F4.S1, F4.S2 and F4.S3 are F3.S3 (a), (b) and (c); F1.S1, F2.S1 and F2.S2 keep their ids. The cornerstone ledger is recounted for E = 3: Q = 2, 2 of 2, F1.S2 and F2.S1. The gate's accepted findings are in the operative part: no scoring while a session runs, with the 8-CPU profile named; one terminal rule for a functional failure; the growth budget measured and a disk rule before each pair; an enforcement check of the 512 MiB limit; the affinity read-back. The judge's cells are unchanged.)
  (2026-11-07, the gate and the owner: the re-review gate on this revision, through a review extract on the local branch `review/lang-v2-epoch9` because the plan is 499 KB, answered `proceed` with 2 of 2 reviewers; 6 of its 7 findings were accepted and written in, and the seventh, that the freeze tag would break without group metadata, was rejected because the tag is checked only by its pattern. The owner approved the list *What got smaller* on 2026-11-07, every item as written, after approving "bare first" on 2026-11-06 with the consultants `b5b61bbb`, 2 of 2.)

> Epoch 9 · approved 2026-11-07 · by owner · state: stopped

- 9.1 · 2026-11-07 · agent · dod — F1's done-when asked both references of task 06 to pass 10 of 10, but each task has one reference today and the per-stack references are made in F2.S1, which already requires that check; F1 instead proves the rule on accepted quick-run sessions of both stacks
  (Found by the Fable writer of F1.S2's spec. The check is moved, not dropped: F2.S1's done-when holds it unchanged. Counted as a change because a done-when moved; this is the epoch's first.)
- 9.2 · 2026-11-08 · owner · dod, scope — the six frozen specs and their workspaces go public in the task repository at F2's freeze; only the references and the trap table stay private until the Stage 2 decision
  (This second change stopped the plan. Item 11 of *What got smaller*, approved on 2026-11-07, held the specs back with the references and the trap table, while F2.S2 freezes them in the public task repository; the owner resolved the contradiction on 2026-11-08. The consultants, `939e2e40`, 2 of 2, counted it: resolving one approved clause against another is not a fix that satisfies the plan as written, and 9.1 is the precedent.)
  (Evidence for the re-review, all found while F2's stories were written: FORMAT's second amendment landed outside F1's merge, and the bench's pins wait on branch `chore/lang-v2-format-d72-pins` (`2b0947ef`, green); `inputs/memory-limit.json` was written before its registration condition; `leakcheck` reads only the bench repository; the six workspaces must come from the one engine; the six spec pins live in two places; "sealed" has no meaning yet for Stage 1's held-out folders; whether FORMAT's protected sections bind with no group A freeze; the bias question's material sent to vendors; F1's records lag #179. The owner also gave the go, on 2026-11-08, to push the freeze tag and apply the two approved rulesets once all six packages pass and a dry run is clean.)

### Re-review for epoch 10

Narrowed: yes — one merge condition got smaller: merge condition 2 asked every Stage 1 pull request for CI's required checks, and the task repository's pull requests now merge without CI, on the document gate and the bench-side checks run locally and recorded (merge condition 5, item 12 of *What got smaller*); nothing else got smaller: change 9.2 publishes the specs and workspaces sooner and keeps the references and the trap table private, change 9.1 moves the references' check from F1 to F2.S1 unchanged, `leakcheck`'s reach, the sealed archives, the witness rule, the bias review and the F-LOAD rule add, and no task, epic, goal, metric or Definition of Done item is removed

| Work | Where | Verdict | Action |
|---|---|---|---|
| #179, epic F1: ten-pass scoring with one verdict rule, one memory value, the carry-ins and the submission contract (`57ccc379`, merged 2026-11-07 21:19 UTC; head `b4c1422b`, branch `feat/lang-v2-f1`; its story branches `feat/lang-v2-f1-s1` `1575b1ac`, `-s2` `d907c214`, `-s3` `1018926c`, `docs/lang-v2-f1-stories` `f425ebcb` and the pins branch `feat/lang-v2-format-amend-pins` `b7bba0d3` are local ancestors of its head; `git diff b4c1422b 57ccc379` is empty) | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-08). Checked: 51 commits, 186 files by `git diff --stat`: `spikes/lang-explore/` (the pass driver and child, `lib/verdict.mjs`, `lib/memory-limit.mjs`, the affinity read-back, growth), `spikes/lang-bench/` (one frozen list in `lib/frozen-set.mjs`, the package matrix, the runner text scan and its `ast` proof, the runner at 0.3.12), the plan's entry 9.1 and F1's stories document; the story branches and `feat/lang-v2-format-amend-pins` are ancestors of `b4c1422b`; the whole runs are at `40d3fdc6`, and the head adds only their record in `research/module_tests.md`; CI's checks are green. Against the revision: one engine holds (no second scorer; the passes extend `score-quick.mjs` and `rescore`); D71 holds in `lib/verdict.mjs` (profiles `6-7` and `0-7` on odd and even passes, three attempts per pass, a failing completed pass decides before anything else, `unscored: environment` only with no failing pass, and a record that does not hold exactly the expected cases is an environment attempt, `lib/attempt-record.mjs`); the copy still drops only the exact folder names, and F1.S1 (c) is recorded with its pin test. D72's split is contradicted only in words: the note of `inputs/memory-limit.json` says "registered", and no code reads a registration; F2.S1 fixes the note, a fix forward, not a cut. `explore.json` keeps the same figure only as the olympiad calibration floor, outside the six tasks' path. What follows: the d72 pins (*Build order* 2a), then F2.S1's note fix. The writer's reading: F1's three stories as the epoch-9 part asked, with change 9.1's done-when; its tail, the second amendment's pins, is the row below; `inputs/memory-limit.json` says "registered" before D72's condition holds, fixed forward in F2.S1 |
| the task repository's #4, FORMAT's second amendment: the memory bound is the one the statement states, registered once every daemon and web reference passes under it (`c204726`, merged 2026-11-07 21:30 UTC; head `68c181e`, branch `docs/format-amend-d72`) | `dew_flow_ai_language_comparison` main | fits | kept (the Opus judge, 2026-11-08). Checked with `gh pr diff 4`: one file, `todo/tasks/FORMAT.md`, 3 lines added and 2 removed, 3 commits: F-LIMITS-D's bound is the one the statement states, registered only once the references of every daemon and web task pass under it in both stacks, and section 1.5 and the changelog say so, which is D72 with its epoch-10 split. Its description records two document-gate rounds at `proceed`, 4 of 4. It does not record what merge condition 5 asks: no bench-side command, exit or head sha is in the pull request or its comments; the bench checks ran after the merge, against `c204726`, and are recorded only on the unpushed pins branch (`research/module_tests.md`, `inputs/gates/FORMAT-v2.md`). It merged on 2026-11-07, in epoch 9, while merge condition 2 still asked for CI's checks. What follows: the checks recorded on #4 (commands, exits and the sha they ran on), or merge condition 5's last sentence corrected (the judge's report). The writer's reading: F1.S2 (b)'s amendment; it merged after #179 on its document gate and the bench-side checks run locally, with no CI (merge condition 5 writes that down) |
| `chore/lang-v2-format-d72-pins`: the bench's pins follow the second amendment (`2b0947ef`, 1 commit on `57ccc379`, 2026-11-07, 10 files) | `dew_flow_benchmark`, local, never pushed (worktree `/home/jinx/git/wt/bench-d72-pins`) | fits | kept as F1's tail (the Opus judge, 2026-11-08). Checked: not on the remote; 1 commit on `57ccc379`, 10 files, 33 lines added and 17 removed: `specs.d/format.json`, `specs.json`, `hashes-lang.txt` and `PINNED_TASK_SHA` at `c204726`, the gate record, the six quick tasks' FORMAT pins in `explore.json` (the olympiad pins stay), one test literal, three module docs; its `research/module_tests.md` line records the whole runs and the one red it explains; the line scan finds nothing in it; `git merge-tree` is clean against `main` and against this revision. What follows: merge conditions 1 to 3 (it changes `lib/task-repo.mjs`, so the gates apply), then its merge before F2.S1's first conformance run. The writer's reading: F1's outstanding tail; it merges before F2.S1's first conformance run (*Build order* 2a) |
| `feat/lang-v2-f2`: F2's stories document and its `todo/README.md` row (`196ec280`, 1 commit on `57ccc379`, 2026-11-07, 530 lines) | `dew_flow_benchmark`, local, never pushed (worktree `/home/jinx/git/wt/bench-f2`) | fits | kept, local, to fix forward before F2 starts (the Opus judge, 2026-11-08). Checked: not on the remote; 1 commit on `57ccc379`, `todo/PLAN_lang_comparison_v2_E8_F2.md` (529 lines) and its README row, text only; the line scan finds nothing in it. It conflicts with the revised F2 here, each to rewrite: §7 item 5 makes "sealed" a tree hash and counts, where F2.S2 now asks for one encrypted archive per task; (d)'s `leakcheck --repo <dir>` scans the task repository instead of the bench, where merge condition 3 asks for both repositories, their branches and history, the candidate publication commits and the planted-needle check; §7 item 7 leaves `memory-limit.json` alone, where F2.S1 fixes its note; §7 item 6 says FORMAT's protected classes bind nothing in Stage 1, where F2.S2 binds them from B's tag; F2.S2 (a) takes the d72 pins, which stay F1's tail (*Build order* 2a); §2 still says #4 is open; the first task is "04 or 05", where the revision names 05. On the mutants it defers to F2.S1's Fable spec, outside git (`~/rsd/lang-v2/briefs/f2s1-spec-public.md`, P5 and scenarios 9, 10 and 13), which catches a mutant in one pass on the 2-CPU profile and records an extra failure without a stop, where the revised F2 gives a race witness five passes under each profile and makes an extra failure a mapping to fix; the spec follows the revision when it is copied in. The writer's reading: text only, no code; it found the evidence of 9.2; it is revised to this part before F2 starts (the shared epoch-10 points: publication at the freeze, "sealed", FORMAT's protection from B, the witnesses, the bias review, F-LOAD, task 05 first) |
| #180, the stop of epoch 9: entries 9.1 and 9.2 (`7143588e`, branch `docs/lang-v2-epoch9-stop`) | `dew_flow_benchmark`, pushed, pull request open (merging) | fits | kept (the Opus judge, 2026-11-08). Checked: open, one commit, one file, the plan, 5 lines added and 2 removed: entry 9.2, `state: stopped`, the status; CodeQL, `contract` and `plans` passed, and `build-test` was still running when read. Step 1's record; it carries no Stage 1 work, so its merge breaks no halt. Nothing follows but its merge with its checks present and green. The writer's reading: step 1's record; this revision's base |
| this revision: the epoch-10 lines in the operative part (items 11 and 12, the Definition of Done, merge conditions 3 and 5, F1.S1 (e), F1.S2, F2.S1, F2.S2, the build order, D71, D72, D76, *Growth*), this record, the status, F1's records and `todo/README.md` | `dew_flow_benchmark` branch `docs/lang-v2-epoch10-revision`, local commits, never pushed | fits | text only, nothing to revert (the Opus judge, 2026-11-08). Checked: 1 commit on `7143588e`, 3 files, 112 lines added and 13 removed; the word diff of the parent removes only two line ends, `file;` and `06.`, where dated lines go in; `leakscan-lines.py . history` exits 0, and the added lines hold no case id and no trap word of the private aid. No epoch 10 line is written, as step 8 asks. What follows before epoch 10 opens: the judge's findings of 2026-11-08 answered, the re-review gate, and the owner's approval of item 12. The writer's reading: text only; every replaced sentence stays, marked by a dated line; no D, T, T-L row, result or history is deleted |
| #178, the epoch-9 revision and the epoch 9 line (`0214a3eb`, merged 2026-11-07 12:28 UTC; head `3bafffd8`, branch `docs/lang-v2-epoch9-revision`); added by the judge | `dew_flow_benchmark` main | fits | kept (the Opus judge, 2026-11-08). Checked: 11 commits, 4 files, plan text only (the parent with 514 lines added and 5 removed, the E1 document, `todo/README.md`, `research/module_lang_bench.md`); CI's checks green. Four commits and a merge of `main` came after the epoch-9 judge's cells (`6f0b22d0`): the epoch 9 line taken out until the gate, the judge's findings, three epics, the gate's findings and the epoch 9 line; the gate (2 of 2) and the owner read them. It is epoch 9's baseline, so nothing in it is cut; this revision builds on it. Nothing follows |
| `review/lang-v2-epoch9`: the epoch-9 gate's extract (`570674c8`, one file `review/epoch9-revision.md`, 445 lines, on `609db0c3`); added by the judge | `dew_flow_benchmark`, local, never pushed | — | nothing was built (the Opus judge, 2026-11-08): the text the epoch-9 gate read, never merged; it may be dropped |

  (Written 2026-11-08 by the re-review's plan writer (Opus). The Verdict column is the Opus judge's: `—` stands as the placeholder that `plan-lifecycle.mjs` accepts, as in the records of epochs 6 and 9. Not a row: #177 (the leftovers plan's boundary with another plan's E6) and #178 (the epoch-9 revision and the epoch 9 line itself, its baseline) merged before or as epoch 9 opened; the local branch `review/lang-v2-epoch9` is the epoch-9 gate's extract. Step 3 is consult `b52e8663`; the decisions are the team lead's. Step 7: no epic changed, so no re-split; the cornerstones stay 2 of 2. **No epoch 10 line is written here:** step 8 writes it after the judge, the re-review gate and, because this record says `Narrowed: yes`, the owner's approval of item 12.)
  (2026-11-08, after the judge's verdicts, at its findings; its cells are unchanged: build step 2b builds `leakcheck` over both repositories before F2.S1's first spec pull request; merge condition 5 and item 12 now say what happened at #3 and #4 (neither recorded bench-side checks; #4 merged on its document gate while merge condition 2 still asked for CI) and that the condition applies from epoch 10 on, with the checks run on a checkout of the head and run again by the bench pins pull request after the merge; the bias review's leak rule is in D25's dated line and F2.S1; "fails exactly its registered cases" is written for a deterministic and for a race witness; "registered" is a field of `memory-limit.json` that F3.S1's guard reads; D70 and the disposition table name runner 0.3.12; F2.S2's checker is on `main`. `Narrowed: yes` stands, with item 12 as its one item.)
  (2026-11-08, after the plan gate on the revision, `proceed`, 2 of 2 reviewers; its five accepted findings are written in, the judge's cells unchanged: the race witness rule exactly, in F2.S1; the reserve before the first pair and the recovery path that archives before freeing, in F3.S1 and *Growth*; no task-repository pull request before build step 2b; the clear held-out folder's lifecycle, in F2.S2 and F3.S1. None makes anything smaller.)

## Operative plan for epoch 9 (2026-11-07): Stage 1, one bare series on six tasks

**This part is the revision for epoch 9; from the epoch 9 line on, it is the plan in force.** Until that line, the stop stands
(the status line). Where an older section below says something else, this part holds. The older sections
stay as the record, and as the reference for the details this part cites by number (D, T, T-L and G rows, § numbers). This
part was written for the epoch-9 re-review (`common.plan-change-control`, the full stop, step 4) from the owner's decisions
of 2026-11-05 and 2026-11-06, the consult `0a205591`, the quick run's analysis
(`research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md`) and the Fable planner's epic split with the team lead's decisions
of 2026-11-07.

*(2026-11-08, the epoch-10 revision: the whole re-review of epoch 9's stop (changes 9.1 and 9.2; consult `b52e8663`; the
team lead's decisions) is written into this part, each place marked with a dated line "(2026-11-08, epoch 10)". No epic and
no goal changes. The text it replaces stays, marked. What the revision makes smaller is the short list after item 11 of
*What got smaller*.)*

### What changed, in short

The owner narrowed the plan on 2026-11-06: **bare first, process later.** The next deliverable is one clean, prospective bare
series on the six daemon and web tasks. The quick run (entry 8.1) showed two things. First, the harness written for it,
`spikes/lang-explore/`, already scores a daemon or web session end to end, so the stories that would build that again are dead
scope. Second, one scoring pass cannot judge a program with a race (entry 8.2), so every session is now scored ten times. The
agents are also told the real memory limit, which the quick run's task 07 showed they need. The process arm becomes Stage 2:
its stories stay written, and nothing of it is built before a dated decision right after the bare report.

The owner's words that set the size: *"cut it to 2 days ... run it, see, and if there are errors finish it from the facts, not
write for a month"* (2026-11-05), *"fix everything and go on"* (2026-11-06). All the development rules and the coai gates stay
on the harness's own development (see *Merge conditions*). Actual costs matter; predicted costs do not; there is no spending
limit beyond the $50 per-session runaway cap (D7).

### What got smaller (for the owner's approval)

Everything this revision makes smaller than the epoch-8 plan, in one place. Nothing else got smaller.

1. **Six tasks, not ten.** The claim covers the daemons 05, 07, 10 and the web APIs 04, 06, 08. The four olympiad tasks
   become exploratory controls outside the claim.
2. **The process arm goes to Stage 2,** behind a dated decision after the bare report: no process sessions, no process
   pilot, no anchor round, no process-arm tooling in Stage 1.
3. **E0's conventions parity audit and release go to Stage 2.**
4. **One fixed memory limit:** 512 MiB for all six tasks replaces the limit calibrated per task from the references (D57's
   floor probe, D58's calibration sequence).
5. **Fewer sessions:** five pairs per task in one arm (30 pairs, 60 data sessions) instead of three pairs per task in each
   of two arms (120 sessions). The claim says plainly that it cannot show a 20 % difference.
6. **No staged groups:** groups A, B and C go, with A's replication in B, the drift table between them and the interim
   records per group.
7. **One pilot and one freeze** instead of one per group; the checkpoints CP3, CP4 and CP4-B are replaced by one document
   gate per spec.
8. **Smaller deviation count:** only changes to frozen files are counted; the lexical classes (project edits, package
   items, new configuration files, suppression markers) become follow-up work.
9. **Two planned per-case fields become follow-up work:** CPU time and the RSS curve. Neither was ever measured.
10. **Two hand checks lapse:** G-15 (the olympiad judge by hand, outside the claim) and G-19 (the process pilot).
11. **Publication waits:** the references, the trap table and the specs publish when the Stage 2 decision line says
    *cancel*, or at Stage 2's end, not after the bare series. Until then they stay private.
    *(2026-11-08, change 9.2, epoch 10: item 11 now covers **the references and the trap table only**. The six frozen specs
    and their workspaces go public in the task repository at F2's freeze (F2.S2). The text above, approved on 2026-11-07,
    is kept.)*

**What the epoch-10 revision makes smaller (2026-11-08), for the owner's approval.** One item; nothing else got smaller:

12. **The task repository's pull requests merge without CI.** Merge condition 2 asked every Stage 1 pull request for CI's
    required checks, and the task repository has no CI in Stage 1 (E1.S7's CI is Stage 2's). Its pull requests now merge on
    the document gate and on the bench-side checks run on a checkout of the pull request's head and recorded in the pull
    request (merge condition 5), from epoch 10 on. It also covers what already happened: neither #3 nor #4 recorded
    bench-side checks in its pull request; #4 merged on 2026-11-07 on its document gate (two rounds, 4 of 4 reviewers)
    while merge condition 2 still asked for CI, and the bench checks ran afterwards against its merge `c204726`, on the
    pins branch `chore/lang-v2-format-d72-pins`.

### The goal, narrowed

> On six tasks of two kinds — the daemons **05, 07 and 10** and the web APIs **04, 06 and 08** — each with the same functional
> requirements and the same black-box checks for both stacks, with **Claude Code** (Opus, effort `high`), how do **C# 14 / .NET
> 10** and **TypeScript 6.0.x strict on Node 26 with Fastify** compare in **cost per correct solution** — and beside it success
> rate, tokens and decomposed time — for a **bare agent** with the task prompt only?

**The claim and its stated limit (D73).** Five pairs per task, 30 pairs in all. With the within-stack spread the quick run
showed (two to three times between the cheapest and the dearest session of one stack on one task), five pairs separate
per-task medians only at about **1.5 times**, and the 30 pairs read the **pooled direction**. **Neither reading can show a
difference of 20 %**; the pre-registration says so, and the report repeats it beside every ratio. Two of the six tasks are
known (05, 06) and four are original (04, 07, 08, 10); they are shown apart as a listing, not as a summary (D29).

**Outside the claim.** The four olympiad tasks (01, 02, 03, 09) are exploratory controls: their quick-run re-run (24 of 24
accepted) is cited in the report as such and is never pooled. The process arm is Stage 2. Nothing here is a claim about the
languages in general.

### Definition of Done (Stage 1)

- [ ] One scoring engine: every Stage 1 session is scored by `spikes/lang-explore/`'s path (D70), with ten passes and the rule
      of D71; no second scorer was built.
- [ ] The memory limit is one registered value; the statement, the visible load test and the scorer read it, and a test proves
      they agree (D72).
- [ ] Six validation packages passed (F2.S1), each with its own document gate; one freeze tag holds the six specs and FORMAT;
      `leakcheck` is clean and the private aid lists nothing uncaught.
- [ ] One pre-registration tag precedes the first launch (the pilot included); `prediction.md` was committed before the pilot
      and `seed.txt` revealed after the A/A pair.
- [ ] The pilot is GO; then 30 data pairs ran (five rounds of six tasks), every data session scored ten times, every attempt
      kept, no round started before the round before it was scored.
- [ ] T1–T5 held for every data session, or the breach is in the report with what was done.
- [ ] The bare report is generated from the data alone (no number typed): CPC or the undefined outcome per task, the six-task
      median, the geometric mean and the count, every v1 metric (D34), `deviations` beside `accepted`, the counts of
      `intermittently incorrect` and `unscored: environment` per stack, the claim's limit, and the sentence that the quick
      run's task 07 cost is confounded (T26).
- [ ] Every failing or mixed case was adjudicated blind (F3.S3).
- [ ] Every input but the stack is pinned in the harness; one pair per round in rounds 1 and 2 was read back by hand to prove
      the stacks differ only there (F3.S2; measurement rule 1). One alias baseline and one CLI copy for the series (D47).
- [ ] The instruments were hand-checked before their numbers appear (rule 2): the drivers on the real pilot trees (G-16) and
      the suites through F2.S1's references and mutants. G-15 (the olympiad judge by hand) and G-19 (the process pilot) lapse:
      the first checks an instrument outside the claim, the second is process only.
- [ ] The declared culture `de_DE.UTF-8` held in both boxes for every session (T-L32; D35), and `run.sh` exec'd the program in
      every workspace (T-L31; D38).
- [ ] Memory was certified (D36, T-L29, T-L30): every data session ran on the certifying host named in the pre-registration,
      each program in its own systemd user scope, with `memory.peak` and `memory.events` kept, and `accepted` includes the
      certified memory claim; the halt `memory-not-certifiable` refused every data launch on a host without `memory.peak`; the
      flag `memory bound not certified` appears on non-data runs only, and no CPC denominator depends on it.
- [ ] One memory policy (D55) for every case, pinned as an identity; the per-case heap keys are never in the agent box or the
      build box (T-L14), and the statement only names them (D72).
- [ ] Every `memory limit` names its cause; the 50 % evidence threshold and the flags `abort-below-evidence` and
      `child-abort-line` are in the pre-registration, counted per stack and read by the blind adjudication (D56; T-L50, T-L52,
      T-L54). Every memory figure is in MiB.
- [ ] Every cost carries its basis; the pilot, the A/A pair, the data sessions and the voids are separate spend lines; every
      attempt, void and re-run included, is in the record (D26; no total ceiling, D7).
- [ ] Time is partitioned (the spans sum to the wall); no span is summed twice; scoring time is kept apart from the agent's
      wall (§8).
- [ ] One acceptance policy for both stacks (D66, T-L65): the frozen build is the acceptance build; what was scored is what was
      submitted, the frozen files restored, no project file regenerated, no edit refused, the registered snapshot read-only;
      the Node `.npmrc` holds `ignore-scripts=true` and `save-exact=true` (T-L62).
- [ ] FORMAT's amendments (the first, and D72's memory amendment) were gated before the freeze, their pins moved with them;
      FORMAT's F-STDERR, F-ENV, 4.6 and `F-PROCS` state the runner's and the layer's real bounds (T-L58), and `pidsMax` is in
      the frozen identities.
- [ ] Every freeze or immutability rule had one permitted and one refused change shown on real files before the freeze
      (D64); the freeze names its workspace commit (D67 (3), T-L67).
- [ ] The probe and experiment records stay unchanged with their identities; every later probe is a new dated file (D59,
      T-L53).
- [ ] The report names every identity: the task repository sha and freeze tag, the pre-registration tag, the harness
      identity, the runner manifest, the layer, FORMAT's sha, the contract's identity, the memory value, the CLI version, the
      model id, the toolchain checksums, the cache hashes, the machine and the dates (rule 6); its conclusion is no wider than
      the claim and its limit.
- [ ] The public repository holds no secret, transcript, held-out case or link in a workspace: `leakcheck` clean on every push
      and pull request; credentials torn down after the series.
- [ ] **Publication, decided so that nothing is lost:** the references, the trap coverage table and the specs' final state are
      published in the task repository when the Stage 2 decision line is written as *cancel*, or at Stage 2's end. Until then
      they stay private (`~/rsd/lang-v2/`), because publishing them would leak into a possible process arm. A *defer* keeps
      them private until the next dated line. *(2026-11-08, change 9.2, epoch 10: this item covers the references and the
      trap coverage table only. **The six frozen specs and their workspaces are published in the task repository at F2's
      freeze** (F2.S2), with the freeze tag; the text above is kept.)*
- [ ] `research/module_tests.md` lists the harness's runner command and every scenario flow as covered or not covered, with a
      reason, and says what the harness does not prove.
- [ ] v1's parent plan [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) carries a STATUS line saying its process
      arm moved to this plan, and v1 is closed after its bare arm (D32; a follow-up outside this plan's commits).
- [ ] `research/lang-v2/` and `research/README.md` are updated; `research/module_lang_explore.md` describes the one engine.
- [ ] Every merge of a Stage 1 story met the merge conditions below (D68 among them).
- [ ] The Stage 2 decision line is written and dated (F3.S3).
- [ ] `plan-lifecycle.mjs` is clean. If Stage 2 is cancelled, this plan is promoted with its deviations; otherwise it stays in
      `todo/` with Stage 2 as its open part.

### Merge conditions (every pull request of Stage 1)

1. **The gates stay on the harness's development.** A pull request with harness code has a coai plan round at `proceed`
   before its code, then a coai code round and an Opus review on the committed branch, and every finding resolved.
   **These gates are the project's way of building the instrument. They are not the process treatment arm, and their
   records are never read as Stage 2 data.**
2. **D68's condition, unchanged:** one whole completion run of every suite the pull request touches (`spikes/lang-explore/`,
   `spikes/lang-bench/`, `spikes/minigit-bench/` as touched), plain and with `BENCH_REQUIRE_SANDBOX=1`, every test passed and
   nothing skipped; CI's required checks present and passing for the head sha. The same test failing in two consecutive whole
   runs stops the story until it is diagnosed.
3. **D41:** no held-out content, trap name or case id in any public text. Two checks, both against the real private side:
   - **`node spikes/lang-explore/explore.mjs leakcheck`** exits 0 for every merge that touches a spec, a sample, a package
     (F2.S1), a document under `research/lang-v2/` or an agent-visible file (the runner, the templates, `STATEMENT.md`,
     `PACKAGES.md`), and at the freeze (F2.S2). It reads the case lines, the trap ids, the reference, mutant and oracle lines
     and the private notes, so it is the check that sees a trap name or a case id.
   - **The line scan** (`python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py . history`) exits 0 on the branch before every
     merge, as the second check. It matches whole lines only, so it cannot see a trap name or a case id on its own.
   Both read the same private folders, where F2.S1's packages live: for task NN, the held-out cases in
   `~/rsd/lang-v2/explore/heldout-NN/cases/`, its trap ids in `heldout-NN/traps.txt`, the mutants in `heldout-NN/mutants/`, the
   references in `~/rsd/lang-v2/explore/refs-NN/`, and the test-design notes in `~/rsd/lang-v2/test-design/NN.md` (read by
   `leakcheck`; the line scan reads `heldout-NN` and `refs-NN`). A package file kept anywhere else is seen by neither check, so
   F2.S1 puts nothing anywhere else.
   *(2026-11-08, epoch 10:)* **`leakcheck` covers both repositories and the candidate publication commits:** the bench
   repository, the task repository (its branches and its history), and every commit made to publish specs or workspaces
   (F2.S2) before it is pushed. A check proves it: an invented needle, planted only in a temporary task repository (never in
   the real one or the private folders), must make the publication check fail. **Build step 2b builds this, before F2.S1's
   first spec pull request** (D41: the check is ready before it is needed); F2.S2 uses it at the freeze.
4. During Stage 1, no pull request carries Stage 2 code (D76).
5. *(2026-11-08, epoch 10)* **The task repository's pull requests.** The task repository has no CI in Stage 1 (E1.S7's CI is
   Stage 2's), so merge condition 2's CI clause cannot apply to it. A task-repository pull request merges when: its document
   gate is at `proceed` with every finding resolved; the bench-side checks that read it (the conformance check, `leakcheck`
   over both repositories, `workspace-diff` and the push check where workspaces change, the pin checks) were run against
   a checkout of the pull request's head, so they run before the merge, and their commands, exits and the head sha are
   recorded in the pull request; and the bench pins that follow it are on a bench branch of their own, merged by merge
   conditions 1 to 3. **After the merge**, the bench's pins name the task repository's merge commit, and the bench pins
   pull request runs the same checks again against that commit. **This applies from epoch 10 on.** It is not what #3 and
   #4 did: neither recorded bench-side checks in its pull request; #4 merged on 2026-11-07 on its document gate (two rounds,
   4 of 4 reviewers) while merge condition 2 still asked for CI, and the bench checks ran afterwards against `c204726` on
   the pins branch. Item 12 of *What got smaller* covers that. *(2026-11-08, after the plan gate:)* **No task-repository
   pull request opens before build step 2b has merged**, so `leakcheck` over both repositories exists for every one.

### Epics in force: F1 to F3

Three epics of two or three stories each, in build order — the shape the plan gate's operator ordered on 2026-11-07 (never
more than three epics, never more than three stories in one). The F names keep the new epics apart from E0–E6, whose ids
stay in the history. Each epic's stories document is written by Opus when the epic starts and links back here, under a name
the lifecycle check knows as a stories document of this plan: **`todo/PLAN_lang_comparison_v2_E7_F1.md`, `_E8_F2.md` and
`_E9_F3.md`** — the epic numbers go on after E6, and the F name follows. The check finds a stories document's parent by
`_E<digits>` followed by `_` or `.md` (`.agents/conventions/tools/lib/plan-ledger.mjs`, `parentPlanOf`); a plain `_F1.md`
name would escape it. Proved on 2026-11-07 with a throwaway local file carrying a ledger, deleted afterwards:
`PLAN_lang_comparison_v2_E7_F1.md` gave the finding "a stories document carries a ledger; its changes count in the parent,
todo/PLAN_lang_comparison_v2.md", and `PLAN_lang_comparison_v2_F1.md` gave none. **Cornerstones: 2 of 2** (Q = ⌈2·3/5⌉ = 2):
F1.S2 (the verdict: repeated scoring and the one memory value, one story under one Fable spec) and F2.S1 (the validation
package).

**The scoring CPUs.** Two scoring profiles, fixed for the series and the same for both stacks: the **2-CPU profile is CPUs
`6-7`** (core 3, where the quick run scored) and the **8-CPU profile is CPUs `0-7`** (cores 0 to 3). The session masks are
A = `0-5` and B = `8-13` (`spikes/lang-explore/lib/series.mjs` `MASKS`). **Scoring never overlaps a session:** a pair is
scored only after both its sessions have ended, and the next pair launches only after that scoring has ended, so no session
runs while either scoring profile is in use. **Every mask is read back:** before each scoring pass and each session launch, a
preflight reads back the effective CPU affinity of the process it started (its `Cpus_allowed_list`) and refuses a data launch
when the required mask cannot be applied exactly; a refusal halts the series resumably and is recorded, never scored.

#### F1 — The harness and the verdict

- **Goal:** `spikes/lang-explore/` is the base; nothing is built twice; a session's verdict comes from ten scoring passes
  under two CPU profiles with one stated rule; the memory limit is one registered value, enforced and stated alike.
- **Needs:** the stop lifted (the epoch 9 line).
- **F1.S1 — the carry-in merges** (replaces §14 A1's steps 0 to 2; D75):
  (a) #164 (E1.S8) and #167 (E1.S3 phase A) are **already merged**, on 2026-11-05 (`8dcbdee2`, `c8595952`), #167 with its
  doc commit marking E3's scorer rule superseded by D65; they are recorded as done, nothing is merged again;
  (b) `isBuildTest` (`spikes/minigit-bench/lib/classify.mjs:107-116`) counts `bash build.sh`, `bash run.sh` and
  `run_samples.py` as `build_test`, a RED test first; the quick run's time partitions are recomputed from the retained streams
  into a new dated file beside the old ones, never over them;
  (c) **record, never change, the C# compile's letter-case behaviour** (`~/rsd/lang-v2/briefs/s3b-carry.md` item 1). The copy
  stays exactly as D65 says: it drops only folders named exactly `bin`, `obj` or `node_modules` in C# and `node_modules` in
  Node, so the scored copy never differs from the tree as left. What is recorded is that MSBuild's own compile leaves out a
  folder of those names in any letter case on Linux: one line in `research/module_lang_bench.md` (the S3 section's
  "agree for the default compile items" sentence) and a pin test in `spikes/lang-bench/test/`, `Bin/Extra.cs` holding text
  that does not compile builds to exit 0, in place and on the copy. **Not done yet:** on 2026-11-07 neither the line nor a
  test exists (a search of `research/module_lang_bench.md` and `spikes/*/test/` for `Bin/` and "letter case" finds none). No
  code changes; the `build.sh` comment line about it rides with F1.S3's template edit, so the templates' identity moves once.
  Carry items 2 to 4 (doc lines only) go with it;
  (d) the held-out reader (`spikes/lang-explore/lib/heldout-quick.mjs`, `casesTreeHash`) refuses any entry that is not a
  regular file or a directory, so the freeze never hashes through a link (carry item 6);
  (e) FORMAT's first amendment is on the task repository's `main` (its PR #3, `4ddb8f8`); the bench's side, on
  `feat/lang-v2-format-amend-pins` (the pins, the gate record, the checker's group letter and `V<n>` in any case), is merged
  as a fix forward after merging `main`; F1.S2 adds the memory amendment and moves the pins once more;
  *(2026-11-08, epoch 10: the memory amendment is the task repository's #4, merged after #179; the bench pins that follow
  it, on `chore/lang-v2-format-d72-pins` (`2b0947ef`), are F1's outstanding tail and merge before F2.S1's first
  conformance run (*Build order*).)*
  (f) T-L64's text half: no plan id in an agent-visible runner file is done by #173, with its test; the rest of T-L64's text half is still owed and is built here: no stack name in an agent-visible runner comment, the scan run inside the push check, and its `ast` proof (dated 2026-11-07, from F1's stories writer);
  (g) the C# wall-clock test of carry item 9 goes to the test-leftovers plan as a dated line; it is not built here.
  *Accepted when:* (b) and (d) each have a test watched failing first, and (c)'s pin test passes on today's code; the
  recomputed partitions file exists; `pin-runner.mjs --check` and `specs-record.mjs --check` exit 0 on the moved pins.
- **F1.S2 — repeated scoring and the one memory value** (new; **cornerstone**, one Fable spec; extends `score-quick.mjs` and
  `rescore`; D71, D72). Two parts, one story, because together they are the verdict.
  *(a) Repeated scoring.* Per session one build, then ten passes over every visible and held-out case, five on the 2-CPU
  profile and five on the 8-CPU profile; a fresh program for every case of every pass; the same requests and deadlines; the
  affinity read-back before every pass; the verdict rule of D71, including its terminal rule; every attempt kept;
  `replay --times` stays a diagnostic, never a verdict. **Measured first**, before any freeze, on the quick run's defective
  C# tree of task 06 and both references of task 06: the wall time of ten passes, and the **retained size of ten passes plus
  the most retries and re-runs the rule allows** (every pass re-run twice, and the session's one void re-run), which is the
  growth budget's number (*Growth*). *(2026-11-08, change 9.1, epoch 10: in F1 these are measured on the defective tree and
  on accepted quick-run sessions of both stacks; task 06's references do not exist per stack until F2.S1 makes them, so
  their ten passes, time and retained size are measured there, before the freeze. The text above and below is kept.)*
  **These inputs are private:** the defective tree, task 06's references and every output
  of measuring on them stay under `~/rsd/lang-v2/`, outside git; the commit records only counts, times and sizes. The tests
  use invented fixtures only (a toy program that fails on purpose, never a quick-run tree or a reference).
  *(b) The one memory value.* One registered value, 536,870,912 bytes, in one input file *(2026-11-08, epoch 10, D72:
  chosen now, registered only when all twelve references pass under it in both stacks, F2.S1)*; the statement's memory sentence,
  each stack's heap sentence, the visible load test's configuration and the scorer's `MemoryMax` are generated from it;
  FORMAT's second amendment makes F-LIMITS-D say the bound is the one the statement states (today it says the bound is
  registered at calibration and given to the runner, not to the author), with one document gate, and the pins move.
  **Enforcement, not only agreement:** an integration check on the certifying host reads the actual cgroup limit of a case
  (the case scope's `memory.max`, which must read 536870912) and shows, in both stacks, that an invented workload over
  512 MiB is stopped and classified by the memory-failure rules (D56: `memory limit` with its cause).
  *Accepted when:* the defective tree reads `intermittently incorrect` on the scoring CPUs; both references read `accepted`
  *(2026-11-08, change 9.1, epoch 10: in F1, accepted quick-run sessions of both stacks read `accepted`; both references
  of every task read `accepted` in F2.S1)*;
  a test on invented fixtures drives each branch of the rule (all clean; mixed; all failing; a functional failure in one pass
  beside an environment failure in another, which rejects; **a functional failure in a completed pass of a session whose
  other pass stays environment after its re-runs, which is still rejected, never `unscored: environment`, never re-run**;
  an environment failure re-run whole and then clean; an environment failure that stays after two re-runs with no functional
  failure anywhere, which is `unscored: environment`); a test of the affinity read-back refusing a mask it cannot apply; a
  test reads the generated statement, the load test's configuration and the scorer's limit and finds one number; the figure
  has exactly one source in the tree; the enforcement check passes in both stacks on the certifying host.
- **F1.S3 — the submission contract and the offered set** (E1.S3b, reduced to what exists; E1.S4b, small):
  `copy-quick.mjs` and `build-quick.mjs` are the contract; the frozen list and the excluded folders live in one exported place
  that the copy, the restore and the module doc read; the Node `.npmrc` of the templates gets `ignore-scripts=true` and
  `save-exact=true` (D65's measurements: without the first, a `preinstall` script rewrites a frozen file during `npm ci`); the
  contract's identity (a sha256 over those modules, the templates and the build box) is in every `record.json`;
  `frozenChanges` is reported as `deviations`, count and paths, beside `accepted`; D65's lexical taxonomy is follow-up work
  (D74). The offered set: `Microsoft.AspNetCore.Mvc.Testing` joins the C# snapshot (change 7.1), the snapshot is registered
  again, `PACKAGES.md` is generated from the matrix. *Accepted when:* a test that the frozen list has one source; the identity
  in the record; the D65 script case exits non-zero with the frozen file unchanged; T-L63.
- **Done when:** lang-explore's tests are green, plain and with `BENCH_REQUIRE_SANDBOX=1`; the defective quick-run tree is
  `intermittently incorrect` under F1.S2 on the scoring CPUs, and accepted quick-run sessions of both stacks read `accepted`
  10 of 10 under both profiles at the registered limit *(2026-11-08, epoch 10: read "at the chosen value"; the value is
  registered only in F2.S1, D72)* (both references of task 06 pass 10 of 10 in F2.S1, where they are made; change 9.1); `build_test` is not zero for a C# session of a retained stream; the memory figure has one source
  and the enforcement check passes in both stacks; `research/module_lang_explore.md` describes the one engine (E3.S10's part).

#### F2 — Six validation packages (cornerstone)

- **Goal:** for each of 04, 05, 06, 07, 08 and 10, one package that makes a failure adjudicable.
- **Needs:** F1.
- **F2.S1 — a package per task, six** (**cornerstone**; merges E1.S6, E1.S2, E1.S2a, E2.S1, E2.S2, E2.S3 and E2.S4): the draft
  spec's defects fixed; the visible samples, the load test generated from F1.S2's value; **both references pass every visible
  and held-out case 10 of 10 under F1.S2 at its one memory limit**, which is D40's gate in the final environment; targeted
  mutants, one per trap per stack, each caught (named only in the private aid); every case cites an R-id; one bias question
  per task (D25); the held-out link refusal of F1.S1 (d) green on the task's tree; the conformance check (#164) green; **one
  document gate per spec**, because a defect in one of six hides in a joint review (the team lead). One task (04 or 05) goes
  end to end first, then the other five in parallel. *Accepted when:* per task, the package record in the private area and a
  public line of counts only.
  *(2026-11-08, epoch 10 — carried here and made exact:)*
  - **The references' runs (change 9.1).** Both references of every task pass every case 10 of 10 under F1.S2 at the one
    memory value; their wall time and retained size of ten passes, with the most retries, are measured here before the
    freeze and feed *Growth*. **The memory value is registered** (D72) only when all twelve references pass under it in
    both stacks, as FORMAT's F-LIMITS-D says; F2.S1 then fixes `spikes/lang-explore/inputs/memory-limit.json`, whose note
    calls the value registered before that condition held. **"Registered" is a state in that file:** a field that F2.S1's
    tool writes, and nothing else writes, once all twelve references pass under the value in both stacks, holding the date
    and the twelve references' identities (task, stack and tree hash). Until the field is there the value is only chosen.
  - **Defect witnesses, the mutants of §4.3.** One isolated defect witness per distinct trap per stack the trap table lists
    for it (no trap-and-stack row of the table goes without one), stored as an overlay on its stack's reference, never as a
    whole tree, under `heldout-NN/mutants/`. A witness must build; its stack's reference must pass; and a case mapped to
    that trap must reject it for the intended defect. §4.3's *"fails exactly its registered cases"* stands: an extra failure
    is a mapping to fix and record, never silently allowed. A deterministic witness needs one demonstrated kill. A race
    witness gets the ten passes of a session, five under each profile, every miss kept; it counts as caught when a mapped
    case rejects it, and a witness caught in no pass is a trap with no catching case, a failure of the table. *(2026-11-08,
    after the judge:)* **"Fails exactly its registered cases", checkable for each kind:** a *deterministic* witness, in
    its one pass, fails every case registered for its trap and no other case; a *race* witness, over its ten passes, fails
    in at least one pass, and every pass that fails fails only cases registered for its trap. *(2026-11-08, after the
    plan gate, exactly:)* a **deterministic** witness fails, in its one pass, every case registered for its trap and no
    other case. A **race** witness is judged only on its failing passes: each failing pass fails at least one case
    registered for its trap and no case outside them; a timeout or a hang of a registered case counts as that case
    failing; and at least one of its ten passes fails. Its clean passes are allowed and kept. **The cost is
    measured on task 05 first** (task 05 is the first task end to end) and recorded before the other five start.
  - **The bias question (D25).** Not counts: each spec and its suite (visible and held-out) go to the external vendors'
    review as §4.3 says — the coai document gate and a consultant, with D25's question — under the exposure already
    disclosed in Q-C and T17. A list of counts cannot answer whether a requirement or a check favours one stack.
    **Its leak rule (D41):** the suite goes to the vendors from a scratch folder outside every repository, and the gate
    records in `inputs/gates/` hold no case text, case id or trap name, only the verdict, the reviewer count and the
    findings in words that name no case.
  - **F-LOAD's duration.** A load scenario's duration must leave measured room inside the 600 s test bound (FORMAT 4.6):
    the slowest whole-scenario wall (start, readiness, the load, the stop), measured over every pass of both references
    under both profiles, is at most 600 s minus a margin of at least 30 s and at least twice the measured non-load part.
    A duration that leaves less is shortened before the freeze. The spec's proposed 540 s stands only if its measurement
    meets this rule; the measurement is recorded in the package.
- **F2.S2 — one freeze** (replaces the per-group freezes and CP3/CP4): one tag, `spec-frozen-B-<yyyymmdd>`, over the six specs
  and FORMAT; FORMAT and the checker allow only the letters A and B (FORMAT 1.2's `Freeze tag` row,
  `lib/conformance-check-structure.mjs` on the pins branch *(2026-11-08: that branch merged in #179, so the check is on
  `main`, `lib/conformance-check-structure.mjs:47`)*), and B was these six tasks' group under D44, so neither changes,
  and no `-A-` tag is made in Stage 1. The spec hashes in `inputs/specs.json`; the held-out tree hashes and the trap table's
  hash registered; the six workspaces written (E1.S3 phase B) and their workspace commit named (D67 (3)); `workspace-diff` and
  the push check clean; the tag ruleset and `main`'s protection on the task repository (the owner's go of 2026-11-05 covers
  the task repository only, from data with read-back). **F2.S2 builds that code.** What `feat/lang-v2-e1-s7-rulesets` holds is
  a red step only: five uncommitted files, stubs and tests whose wording still names groups. F2.S2 starts from them, the tests
  lose the group wording (one freeze, one tag), and the code is written, gated and merged before the rulesets are applied;
  incremental chunk parsing in `rs_conn.py` only if a frozen task's bodies need it (carry item 5). *Accepted when:* the tag
  exists; the rulesets read back; `leakcheck` clean.
  *(2026-11-08, epoch 10:)* **Publication at the freeze (change 9.2):** the six frozen specs and their workspaces go public
  in the task repository with the freeze tag; the references and the trap table stay private (item 11, D76). **Accepted
  also when:**
  - the six workspaces are made by the one engine (D70; the workspace commit of D67 (3)), and the published workspace bytes
    equal, by tree hash, the bytes a session is launched with and restored from;
  - the two spec pin records, `spikes/lang-bench/inputs/specs.json` and `spikes/lang-explore/inputs/explore.json`, are
    derived from one source, or a test cross-checks them;
  - **"sealed" keeps its inherited meaning** (§4.3, §6): one encrypted archive per task (`heldout-tool.mjs seal`,
    AES-256-GCM, the key a mode-600 file outside every git tree and box), its hash registered; cases are in clear only in a
    scoring temp dir. Hashes alone would weaken it. A clear working folder kept beside the archive is checked against the
    archive's hash at every pair; *(2026-11-08, after the plan gate:)* **its lifecycle:** that clear folder is mode 700, is
    removed after each pair, and is removed again at the start of any run after an interruption, while the archive and its
    identity stay; a check that needs the clear cases makes the folder again from the archive;
  - FORMAT's protection starts at this freeze: with no group A freeze, B's is the first real freeze, so
    `FORMAT-MAINTENANCE.md` §3's "after the freeze of the first group" binds from B's tag, and the class check reads B's
    frozen classification;
  - D64's demonstrations stay: one permitted and one refused change shown on the real frozen files before the tag;
  - `leakcheck` over both repositories and the candidate publication commits is clean, and its planted-needle check fails
    as it must (merge condition 3); *(2026-11-08, after the judge: build step 2b builds that check; F2.S2 runs it at the
    freeze and keeps the rest of its freeze tools)*
  - the owner's go of 2026-11-08 holds: the tag is pushed and the two approved rulesets are applied only once all six
    packages pass and a dry run is clean.
- **Done when:** six packages validated and sealed; the tag exists; `leakcheck` is clean; the private aid lists nothing uncaught.

#### F3 — The series and the report

- **Goal:** one pre-registered bare series over six tasks, isolated, accounted, balanced, with frozen identities, run and
  scored; then a generated record on six tasks with the claim's limit stated, and Stage 2 decided on a date.
- **Needs:** F2's tag.
- **F3.S1 — the series over six tasks and one pre-registration** (E3.S1 thin, E3.S5 reduced; E4.S1, S2 and S3).
  *(a) The series.* `series.mjs` grows from one task to a round over six in a seeded order, each round every task once; the
  two stacks of a pair start together; each pair is scored only after both its sessions end, and the next pair launches only
  after that scoring ends (*The scoring CPUs*). **The mask rule**, which replaces v1's swap per pair and a test checks over
  the whole schedule: number the tasks by id, k = 0 for 04, 1 for 05, 2 for 06, 3 for 07, 4 for 08, 5 for 10, and the rounds
  r = 1 to 5. In round r, task k's C# session runs on mask A when r + k is odd and on mask B when it is even; the Node session
  takes the other mask. So the tasks at k = 0, 2 and 4 start C# on A and give C# mask A 3 times and mask B 2 times; the tasks
  at k = 1, 3 and 5 start C# on B and give the reverse; over the 30 pairs each stack has each mask exactly 15 times. The seed
  decides the task order inside a round, never the mask. **The disk rule:** before each pair, if free disk under
  `~/rsd/lang-v2/` is below twice the pair's measured worst case (F1.S2's number for two sessions with every retry and re-run),
  the series halts resumably and records why; it resumes only when the space is there. *(2026-11-08, after the plan
  gate:)* **The reserve:** before the first pair, free disk must be at least the whole series' measured bound — the
  sessions times the measured worst case, every retry and re-run included — and the per-pair check above still runs.
  Other sessions that write to the same disk are outside this series' control; the per-pair check is the defence against
  them. **Recovery keeps the data:** when the disk rule halts the series, the operator first archives the finished series
  to the private backup, by copy and a verified hash, and records the archive's identity; only then is anything local
  freed, and nothing is deleted that is not in the verified archive. **The clear held-out folder** (F2.S2) is removed after
  each pair and again at the start of any run after an interruption. The barrier and the one void re-run
  stay; the pre-registration guard reads the frozen identities (the runner manifest, the layer, the CLI copy, the snapshots,
  FORMAT, the specs, the held-out hashes, the one memory value, the contract's identity) *(2026-11-08, epoch 10: and it
  refuses a series while the memory value is not registered, that is, while `memory-limit.json` lacks F2.S1's
  registration field)*; D15's environment and isolation
  checks (T-L14's half); the accounting checks T1–T5; the record schema carries `arm` and `model` (D76).
  *(b) One pre-registration.* The price re-read, the CLI pin, `prediction.md`, the analysis contract (D73's claim and limit;
  per task CPC with D24's undefined outcome; the median of the six per-task ratios, the geometric mean and the count of tasks
  where C# is higher, D28; the outcome classes, where `rejected` and `intermittently incorrect` are data attempts that add
  their cost to CPC's numerator and nothing to its denominator, and `unscored: environment` is a void: its cost goes on D24's
  void line, it is in neither part of the primary CPC, and it is re-run once by the void schedule), the two scoring profiles
  and the two session masks, the pilot's tasks, `seed.sha256`; one tag, `prereg/lang-v2-<yyyymmdd>` (§9's first form).
  *Accepted when:* a stub series over six tasks shows the order, the mask rule (each pair's masks as the rule says; 15 of each
  mask per stack; 3 and 2 per task), no scoring while a session runs, the disk rule halting below its threshold, and the
  guard refusing a changed identity; the tag precedes the first launch.
- **F3.S2 — the pilot, the paid rounds and the read-back** (E4.S4 reduced; E5.S1; E5.S2).
  *(a) One pilot:* one pair per kind (a daemon and a web task) as void spend, and one bare A/A pair (two identical bare
  sessions; it sets the noise floor); G-1 to G-14 and G-16 by hand on the pilot trees, G-17 (read for D72's one value: both
  references validated at it before the freeze), G-18; the wall and the stall set once, by a dated amendment; the seed
  revealed after the A/A.
  *(b) The paid rounds:* five rounds over the six tasks under the $50 per-session cap, the quiet check per pair, every attempt
  kept, resume after any halt.
  *(c) The read-back:* in rounds 1 and 2, one pair read back by hand by an Opus judge (argv, binds, cache hashes, package list,
  environment keys with the heap keys, the effective CPU affinity, the spec blob), as dated lines.
  *Accepted when:* the pilot is GO; every session is classed; no round k+1 starts with round k unscored; the read-back lines
  exist before round 3.
- **F3.S3 — the record, blind adjudication and the Stage 2 decision** (E6.S1, from `note.mjs` to a series rollup; E6.S2 with
  E2.S5's packets; new): *(a)* record tools — per task CPC or the undefined outcome, success rate, tokens, time, `deviations`
  beside `accepted`, the counts of `intermittently incorrect`, every attempt, the quiet flags; the olympiad notes cited as
  exploratory controls outside the claim; schemas that can hold `model` and `arm`; *(b)* blind adjudication of every failing
  or mixed case, stack masked; *(c)* the line of *Stage 2: the dated decision* below, written and dated.
- **Done when:** the tag preceded the first launch; the pilot is GO; every data session is scored by F1.S2 with its
  `record.json`; no round k+1 ran with round k unscored; the record renders from data alone, types no number, and carries the
  sentence that the quick run's task 07 is confounded (T26); the decision line is written and dated.

### Build order

1. **F1.S1**, the carry-in merges, one small pull request per item or per group of items.
2. **F1.S2**, on the existing harness, measured on the defective tree and the references of task 06: the first scoring run,
   which gives the real time and the retained size of ten passes before any freeze. **F1.S3** beside it.
   *(2026-11-08, change 9.1, epoch 10: F1 measures on the defective tree and accepted quick-run sessions of both stacks;
   the references' runs are F2.S1's. F1 is merged as #179.)*
   **2a.** *(2026-11-08, epoch 10)* F1's tail: the bench's FORMAT pins for the second amendment
   (`chore/lang-v2-format-d72-pins`, `2b0947ef`) merge before F2.S1's first conformance run.
   **2b.** *(2026-11-08, after the judge; D41)* After the pins and before F2.S1's first spec pull request: `leakcheck` over
   both repositories, with the publication-commit scan and the planted-needle check (merge condition 3), built, gated and
   merged, so every task-repository pull request can run it (merge condition 5). *(After the plan gate:)* **no
   task-repository pull request opens before 2b has merged.**
3. **F2.S1 for one task** (04 or 05) end to end, then the other five in parallel. *(2026-11-08, epoch 10: task 05, whose
   witness cost is recorded before the other five start.)*
4. **F2.S2**, the freeze.
5. **F3.S1**: the series code, then the pre-registration's tag.
6. **F3.S2**: the pilot, which gives the first scored sessions; then five rounds, with the read-back in rounds 1 and 2.
7. **F3.S3**: the record and the blind adjudication, the bare report; then the Stage 2 decision line, dated the day the
   report is generated.

### Decisions D69–D76 (2026-11-07)

| # | Decision | Who, when |
|---|---|---|
| D69 | **The scope: Stage 1 and Stage 2.** Stage 1 is one prospective bare series on the six daemon and web tasks (04, 05, 06, 07, 08, 10) and its report. Stage 2 is the process arm on the same six tasks, behind the dated decision of D76. For Stage 1 this supersedes: D1's "10 tasks" and D3's ten-task list as the claim (the tasks stay defined); §1's part (b); D9's and D20's three runs per arm (D73); D10's and D27's arm interleave; D44's groups A, B and C; D45's freezes and pre-registrations per group; D46's CPC per group and A's replication; D49's interim records; D53's budget per group. The development rules and the coai gates stay on the harness's development, and are not the process treatment arm (*Merge conditions*) | the owner, 2026-11-06 (bare first, process later); 2026-11-05 and 2026-11-06 for the size; the shape: the team lead, 2026-11-07 |
| D70 | **One harness, one scoring engine.** `spikes/lang-explore/` — its scoring (`score-quick.mjs`, `copy-quick.mjs`, `build-quick.mjs`), workspace, records, leak check, series, machine check and read-back — is the base of Stage 1, and drives `spikes/lang-bench/`'s runner 0.3.9 and execution layer through `runCases`. Verified by reading on 2026-11-07. Dead as new code: E1.S3b's `lib/submission.mjs` and `lib/deviations.mjs` (F1.S3 names what exists); E1.S8a part ii-b (the runner 0.3.9 of #173 has the daemon and web path); E3.S2 (the drivers); E3.S4 (the scorer); E3.S3 for Stage 1 (the olympiad judge exists); E3.S1's group dimension; E3.S5's G-15 and G-19 parts. Merged work stays. No large move of files for tidiness; consolidation only where it removes a second engine. This supersedes D43's "consumed by E3's scoring" and D65's sentence that E1.S2a, E2.S1, E2.S3, E3.S4 and E3.S8 import `lib/submission.mjs`: they are replaced by F2.S1 and F1.S2 on lang-explore's copy and build. D40's gate holds and is run by F2.S1 *(2026-11-08, epoch 10: the runner is 0.3.12 since #179, `runner/rs_report.py` `RUNNER_VERSION` and `inputs/specs.d/runner.json`; "0.3.9" above is the version of 2026-11-07)* | the team lead, 2026-11-07, from consult `0a205591`; verified by the Fable planner and in this revision |
| D71 | **Repeated scoring and the verdict (change 8.2).** Pre-registered: ten scoring passes per session, five on the 2-CPU profile (CPUs `6-7`) and five on the 8-CPU profile (CPUs `0-7`), the same for both stacks, each pass after a read-back of its effective CPU affinity, never while a session runs, with a fresh program for every case and the same requests and deadlines. **`accepted`** = every visible and held-out case passed in all ten passes. **A pass with a genuine functional failure rejects the session, whatever the other passes did, environment failures included:** a real failure is evidence, and an environment failure is only the absence of evidence. A rejected session with at least one clean pass is labelled **`intermittently incorrect`**, never a majority pass; every attempt is kept. **The terminal rule, as one sentence a test checks:** *if any completed pass of a session holds a genuine functional failure, the session is `rejected` (or `intermittently incorrect` when some pass is clean), permanently, whatever any other pass did, with or without retries; it never enters the void or re-run schedule, and its cost counts in CPC's numerator like any rejected session.* **The retry unit is the pass:** a pass with an environment failure (D65's list: a box that cannot start, a holder that fails, a missing or wrong snapshot, a host fault) is re-run whole, at most twice, each attempt recorded. A session is **`unscored: environment`** only when no pass shows a functional failure and some pass is still environment after its two re-runs; it is counted per stack, never accepted or rejected, and enters CPC as a void (F3.S1: its cost on D24's void line, outside the primary CPC, re-run once by the void schedule). The references are validated under both profiles before the freeze. **Ten passes are a practical budget, not a proof**: a rarer race can pass all ten (T25). The real time of ten passes is measured on the known defective tree and both references before the freeze (F1.S2). *(2026-11-08, change 9.1, epoch 10: F1.S2 measures on the defective tree and accepted quick-run sessions of both stacks; the references are measured in F2.S1, where they are made.)* Acceptance stays the same for both stacks (D66) | the team lead, 2026-11-07, from consult `0a205591` and the quick run's data |
| D72 | **One memory value, stated to the agent.** The registered value is 536,870,912 bytes (512 MiB) for all six tasks. Each statement says: *"The program and its descendants run under a 512 MiB (536,870,912-byte) cgroup memory limit, including runtime overhead. Build resources are separate."* Each stack's statement also discloses its heap setting under D55: for C#, `DOTNET_GCHeapHardLimit` = 0x1CCCCCCC (483,183,820 bytes, 90 % of the limit); for Node, `--max-old-space-size=460` (MiB, 90 % of the limit). The statement, the visible load test's configuration and the scorer's limit are generated from the one value (F1.S2), and the references are validated at it before the freeze (F2.S1). A reference that cannot pass at it is fixed, or the value changes for all six tasks alike before the freeze, never for one. For the six tasks this supersedes D14's daemon and web memory rule, D57's floor probe and D58's calibration sequence; D55's policy and D56's rule stay. FORMAT's F-LIMITS-D is amended to match (F1.S2). **The quick run's task 07 cost gap stays confounded** — its agents worked against a stated 256 MB floor while 512 MiB was enforced — and is never read as a language result (T26) *(2026-11-08, epoch 10: **the value chosen and its registration are two steps.** 512 MiB is chosen now, for all six tasks. It is registered only when all twelve references (six tasks, two stacks) pass every test under it, as FORMAT's amended F-LIMITS-D says; "registered" above reads "chosen" until then. `spikes/lang-explore/inputs/memory-limit.json`'s note, which says "registered", is fixed in F2.S1. After the judge: "registered" is a field of that file, written by F2.S1's tool with the date and the twelve references' identities; F3.S1's guard refuses a series without it.)* | the team lead, 2026-11-07, from consult `0a205591` and the quick run's streams |
| D73 | **Five pairs per task, and the claim's limit.** 5 pairs per task, 30 pairs, 60 data sessions. From the quick run's within-stack spread (task 07 Node $3.02 to $6.24, task 05 Node $1.37 to $4.33: two to three times), 3 pairs read a per-task cost ratio only above about two to three times; 5 pairs separate per-task medians at about 1.5 times and read the pooled direction over 30 pairs. Neither shows a 20 % difference, and the pre-registration says so as a limit on the claim. Spend is no limit; the machine time of ten passes is measured in F1.S2 before the freeze | the team lead, 2026-11-07 |
| D74 | **What is reduced, and what stays.** One validation package per task (F2.S1) instead of E1.S6, E1.S2, E1.S2a and E2.S1–S4 apart; one pre-registration, one pilot (with one bare A/A pair, kept by the team lead because it sets the noise floor honestly), one freeze; the per-group freezes go, and no group is a prerequisite of another. **Follow-up work, not built in Stage 1:** D65's lexical deviation taxonomy (its classes (a) to (d)) and E1.S8c's per-case `cpuMs` and RSS curve, because none of them decides `accepted` or CPC, and memory is certified by `memory.peak` (D36). **Stays:** the corrected specs, two-stack reference validation, targeted mutants, common resource limits, isolation, the accounting checks, frozen identities, a balanced order, generated reporting, one pre-registration; changed-input provenance (`frozenChanges` as `deviations`), memory enforcement and failure evidence. The cut fields were planned and never measured, so no record loses a value: D34 holds for every metric that exists (dated lines under D34, D67 (4) and T-L64) | the team lead, 2026-11-07, from consult `0a205591` |
| D75 | **Carried in now** (F1.S1, F1.S2, F2): the `build_test` classifier fix with the partitions recomputed from retained streams; MSBuild's case-blind compile exclusion recorded by a doc line and a pin test, with no change to the copy, which stays exactly D65's (the judge's finding, 2026-11-07); the held-out reader refusing non-regular entries; FORMAT's amendment and pins as a fix forward, then the memory amendment; the owner's approved O3 rulesets for what publishes tasks (the task repository's tag ruleset and `main` protection), the process-only rulesets deferred to Stage 2; the draft-spec defects fixed before the freeze; incremental chunk parsing only if a frozen task's bodies need it. The C# wall-clock test goes to the test-leftovers plan | the team lead, 2026-11-07 |
| D76 | **Stage 2 as a dated decision.** Stage 2's stories stay written, moved behind the decision of F3.S3: E0, E1.S7's process half, E3.S6, S7a, S7, S8, S9, the process pilots, the anchor round, E6.S3. T16's window sentence and the bare anchor schedule (D54, T-L55) stay as written: a later process arm needs a bare anchor to compare fairly. Stage 1 builds only the `arm` and `model` fields of the record schema and the accounting, so Stage 2 is not a rewrite. **Publication (E6.S3) is decided so that nothing is lost:** the references, the trap coverage table and the specs' final state publish when the decision line says *cancel*, or at Stage 2's end; before that they stay private, because publishing them would leak into a possible process arm; a *defer* keeps them private until the next dated line. *(2026-11-08, change 9.2, epoch 10: this covers the references and the trap coverage table only; the six frozen specs and their workspaces publish at F2's freeze, F2.S2.)* Further deferral or cancellation needs an explicit, recorded decision that does not depend on which stack won Stage 1. The first Stage 2 step, if it goes, is a small engagement pilot (*Stage 2* below), because the earlier policy pilot showed that loading rules does not mean the process ran | the owner, 2026-11-06; the shape: the team lead, 2026-11-07 |

### Threats this change touches

The rows of §10 stay; these are read as follows from 2026-11-07, and T25–T28 are new.

| # | Stage 1 reading | The sentence the report carries |
|---|---|---|
| T3 | n = 5 pairs per task (D73) | *"Five pairs per task separate per-task cost medians only at about 1.5 times; over 30 pairs the pooled direction is read; a 20 % difference is not distinguished."* |
| T5 | one memory value for all six tasks, stated to the agent (D72); the deadlines are the frozen spec's; both references pass ten times under both CPU profiles at the limit | *"Both references passed every case ten times, under both CPU profiles, at the stated 512 MiB limit."* |
| T6, T9 | one engine; ten passes on two fixed CPU profiles, the same for both stacks; environment failures unscored and counted per stack (D71) | *"Every session was scored ten times on two CPU profiles; environment failures are counted per stack and never scored."* |
| T11 | the classifier counts `bash build.sh` (F1.S1); the partitions are recomputed; `explore.buildTest` stays beside | — |
| T16, T20 | the staged groups are gone: Stage 1 is one window; Stage 2 compares across windows and keeps the bare anchor (D54, T-L55) | T16's window sentence, in any Stage 2 table |
| T21, T22 | the memory policy of D55 is unchanged and is now told to the agent with each stack's heap key | as written |
| T23 | Stage 1 counts only frozen-file changes as `deviations` (count and paths); project edits and suppression markers are not counted (D74) | *"A session that goes around the type check by a project edit or a suppression can be accepted; Stage 1 does not count such edits."* |
| T25 | **A race rarer than ten passes can catch.** Ten passes under two profiles are a budget, not a proof (D71) | *"Ten scoring passes under two CPU profiles catch a race that shows often on the scoring CPUs; a rarer one can pass all ten and be accepted."* |
| T26 | **The quick run's memory confound.** Its task 07 agents worked against a stated floor that differed from the enforced limit (D72) | *"The quick run's task 07 cost gap partly measures a memory statement that differed from the enforced limit; it is never read as a language result."* |
| T27 | **The claim covers six tasks of two kinds.** Two known, four original; the olympiad tasks are exploratory controls outside it | *"The result holds for these six daemon and web tasks only; the olympiad results are exploratory controls."* |
| T28 | **The plan was written after an exploratory run of the same tasks.** The quick run's results were seen before the specs' fixes, the pre-registration and `prediction.md`; it is never data and never pooled, and `prediction.md` says when it was written | *"The pre-registration was written after an exploratory run of the same six tasks; that run is not data."* |

### Stage 2: the dated decision

The line, written at F3.S3 and dated the day the bare report is generated:

> Stage 2, the process arm on the same six tasks, is decided on `<date>`: go, defer to `<date>`, or cancel, with a reason that
> does not depend on which stack won Stage 1.

Until that line is written, nothing of E0, E1.S7's process half, E3.S6 to S9, the process pilot or the anchor round is built.
If it says go, the first step is a small engagement pilot: one process session on one task, whose only check is that the
treatment engaged — the rules were loaded and a gate round actually ran, read from `coai.db` and the stream — before any
Stage 2 epic is split. T16's window sentence and the bare anchor schedule stay as written.

### What happens to every earlier epic and story

| Earlier | In epoch 9 |
|---|---|
| E0.S1–S3 | Stage 2 |
| E1.S1, E1.S4, E1.S5, E1.S8b, E1.S9 | merged, kept |
| E1.S8 (#164), E1.S3 phase A (#167) | merged on 2026-11-05, kept; F1.S1 records them |
| E1.S3 phase B | F2.S2 |
| E1.S2, E1.S2a, E1.S6 | merged into F2.S1 |
| E1.S3b | dead as new code: `copy-quick.mjs` and `build-quick.mjs` are the contract; the identity, the `.npmrc` keys and the `deviations` count go to F1.S3; the lexical taxonomy to follow-up |
| E1.S4b | F1.S3 |
| E1.S7 | the tag ruleset and `main`'s protection to F2.S2; the process-only rulesets, CI, the strict jobs, the deploy key and the run bases to Stage 2 |
| E1.S8a part i, part ii-a | merged, kept; **part ii-b dead**: the runner 0.3.9 (#173; 0.3.12 since #179) has the daemon and web path; only chunk parsing stays, conditional (F2.S2) |
| E1.S8c | the comment half done by #173 (recorded in F1.S1); `cpuMs` and the RSS curve to follow-up (D74) |
| E2.S1–S4 | merged into F2.S1; E2.S3's D58 calibration replaced by D72's one value and the validation at it |
| E2.S5 | F3.S3 |
| E3.S1 | thin: F3.S1 (no `group` dimension, no group manifest) |
| E3.S2 | dead: the runner 0.3.9 (0.3.12 since #179) through `exec-layer/run.mjs`, driven by `score-quick.mjs`; the RSS curve to follow-up |
| E3.S3 | dead for Stage 1: the olympiad judge exists (`calibrate.mjs`, `score.mjs`); the olympiad tasks are outside the claim |
| E3.S4 | dead as written: `score-quick.mjs`; repeated scoring is F1.S2 |
| E3.S5 | reduced to F1.S1 (the classifier) and F3.S1 (the environment checks, the accounting); G-15 and G-19's `aaVerdicts` dead |
| E3.S6, S7a, S7, S8, S9 | Stage 2 |
| E3.S10 | F1: the one engine's module doc, `research/module_lang_explore.md` |
| E4.S1, S2, S3 | F3.S1 (b) |
| E4.S4 | F3.S2 (a) |
| E5.S1, E5.S2 | F3.S2 (b) and (c) |
| E6.S1, E6.S2 | F3.S3 (a) and (b) |
| E6.S3 | after the dated decision: published when the line says *cancel*, or at Stage 2's end (D76); before that, publishing the references and the trap table would leak them into a possible process arm |
| T16, the anchor round (D54, T-L55) | Stage 2, kept as written |

No merged work contradicts this part (the Fable planner's reading; the Opus judge decides in the re-review record).

### Growth

One surface grows: the retained session trees and the scoring attempts under `~/rsd/lang-v2/`, outside git. Measured
2026-11-07: the quick run's folder is 50 GB for its 36 sessions, the olympiad re-run, the re-scores and the replays, about
1.4 GB per session. Stage 1 has 66 sessions (60 data, 6 pilot and A/A), so about 90 GB before the ten passes' own records;
the disk has 632 GB free (2026-11-07). **The budget's number is measured, not guessed:** F1.S2 measures, before the freeze,
the retained size of ten passes plus the most retries and re-runs the rule allows (every pass re-run twice, and the
session's one void re-run), on the known defective tree and both references of task 06 *(2026-11-08, change 9.1, epoch 10:
in F1 on the defective tree and accepted quick-run sessions; the references' number comes from F2.S1, before the freeze)*.
The worst case of a pair is two
sessions' trees plus that size twice; the pre-registration states the projection for 66 sessions from it. **Before each
pair, the disk rule (F3.S1):** if free disk under `~/rsd/lang-v2/` is below twice the pair's measured worst case, the
series halts resumably and records why, and resumes only when the space is there. *(2026-11-08, after the plan gate:)*
**Before the first pair, the reserve:** free disk is at least the whole series' measured bound (the sessions times the
measured worst case, every retry and re-run included); the per-pair check still runs, because other sessions that write to
the same disk are outside this series' control. **The recovery path keeps the data:** after a halt, the operator archives
the finished series to the private backup by copy and a verified hash, records the archive's identity, and only then frees
local space; nothing is deleted that is not in the verified archive. **Kept for ever**, append only (D34;
the owner's rule); nothing is pruned. **Interrupted:** a halted pair is resumed by `series.mjs`, which settles what a crash
left open; an interrupted scoring pass is an environment attempt and is re-run (D71).

### Test plan (Stage 1)

Each story's *Accepted when* names its tests, and each bug fix starts with a test watched failing (`common.testing`). The
tests of merged stories (T-L rows of §15) stay and must stay green. The tests that guard the comparison: F1.S2's verdict
branches and the defective tree; F1.S2's one-value test; F1.S3's single frozen list and the script case; F2.S1's
references 10 of 10 and every mutant caught; F3.S1's mask balance and the identity guard; F3.S3's record rendered from data
alone.

## 1. The question, before any solution

*(2026-11-07, epoch 9: superseded by the operative part's *The goal, narrowed* — Stage 1 asks part (a) for six daemon and web tasks, with five pairs per task (D69, D73); part (b) is Stage 2's. The text below is kept.)*

Experiment v1 ([PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md), record
[research/minigit/RESULTS_bare_2026-10-30.md](../research/minigit/RESULTS_bare_2026-10-30.md)) compared C# and TypeScript
coding agents on the tiny minigit task. Its bare arm found: cost C#/TS median **0.92** (C# > TS in 1 of 3 pairs); wall
C#/TS median 0.93; `model_api` C#/TS 0.86; `active − model_api` (mostly build and test time) C#/TS **1.70**, C# slower in
3 of 3 pairs; **every scored session of both stacks was accepted** (22 of 22, lowest held-out percent 100 %). Every session
ran about one minute. The record's own threat section says it: *"On this task the comparison mostly measures how many model
tokens each stack's agent used, not how the toolchains behave at scale"* and *"Quality is at the ceiling: it does not
separate the stacks here"* (§12). v1's process arm was designed (its §4.2, §5.4, §6, §7.2, §8) and not run.

So v1 answers the cost question for a tiny task and does not reach the question people argue about: does one stack make a
coding agent produce **wrong programs** more often, and at what **cost per correct program**, when the task has traps the
compiler does not catch? The question of v2, and nothing wider:

> On ten tasks of three kinds (olympiad stdin/stdout, daemon, web API), each with the same functional requirements and the
> same black-box checks for both stacks, with **Claude Code** (Opus, effort `high`), how do **C# 14 / .NET 10** and
> **TypeScript 6.0.x strict on Node 26 with Fastify** (D6; TypeScript 7.0.2 was the first pin, superseded by the owner on 2026-10-31) compare in **cost per correct solution** — and beside it success rate,
> tokens and decomposed time — (a) for a bare agent with the task prompt only, and (b) for the same task done end to end
> under this family's process (pinned conventions, coai gates, an Opus team lead, Sonnet coders, PR, CI, merge)?

It licenses a per-task reading (3 repeats per arm per stack) and a pre-registered overall reading over the ten tasks
(§9). It is not a claim about the languages in general.

## 2. What exists — verified 2026-10-31 at `de8479b` (this worktree's base)

*(2026-11-07, epoch 9: kept as verified on its dates. What Stage 1 builds on is D70's: `spikes/lang-explore/` and the runner 0.3.9 of #173, read on 2026-11-07.)*

v2 **reuses** the v1 harness and v1's process-arm design. The table names what is there and where; §6 says what is reused
as is, what must change, and what v1 still has to build.

| Fact | Where |
|---|---|
| The agent box: `bwrap`, root read-only, home/`/mnt`/`/run`/`/tmp` hidden, an explicit bind list, own network/pid/ipc namespaces, no `--setenv`; `sessionLayout(root, rid)`, `agentEnv({ layout, dummyToken, cellId, nodeDir, tsDir, extra })`, `agentBoxArgv(opts)` | `spikes/minigit-bench/lib/sandbox.mjs:72`, `:93`, `:181`; `research/module_minigit_bench.md` §5 |
| The CONNECT proxy with the SNI check (exact host, one `host_name` entry, ECH refused, bytes per tunnel); `EGRESS_HOSTS.bare` | `lib/egress.mjs`, `lib/tls-hello.mjs`, `lib/egress-hosts.mjs:11` |
| The credential broker: the real token never enters the box; `B` ledger; cap refusals as 429 | `lib/broker.mjs`, `lib/budget.mjs`; v1 D23 |
| `launchSession(given, deps, { signal })` — one session end to end, `meta.json` with `stopped` | `lib/session-launch.mjs:252` |
| Four cost views E/O/T/B and checks T1–T5: `sessionCost({ E, O, T, table })`, `crosscheck({ E, O, T, B, table, policy, cliVersion })`; policy `t1 stop`, `t3 stop` for bare data cells | `lib/session-cost.mjs:36`, `lib/crosscheck.mjs:260`, `inputs/crosscheck-policy.json` |
| Time: `spansFrom(...)`, `partition(wall, spans)` — the exclusive work-before-wait partition with the process categories `coai_gate`, `consultant`, `ci_wait`, `sleep300` already defined; primary time measures wall, `model_api`, `active − model_api` | `lib/spans.mjs:179`, `lib/timeline.mjs:68`; v1 §10 |
| Held-out suite sealed with AES-256-GCM, key a mode-600 file, clear only in a scoring temp dir; `heldout-tool.mjs seal\|calibrate\|leakcheck\|counts\|sweep`; the Python runner `runSuite` (`/usr/bin/python3`, `-I`), `heldoutScore`, `clearCases` | `heldout-tool.mjs`; `lib/heldout-run.mjs:19`, `:64`, `:107`, `:163`, `:180` |
| The scorer on a pristine copy, offline from seeded caches, bounded steps (`buildS 600`, `perInvocationS 10`) | `score-session.mjs`; `lib/limits.mjs:3`; `lib/score-steps.mjs` |
| The bare runner: `ridOf`, `maskFor` (masks `0-5` / `8-13`, swapped per pair), `promptFor`, pair phases, the barrier, the outcome table (12 rows), the schedule of voids and the one re-run, resume, records | `bare-runner.mjs`; `lib/bare-plan.mjs:12`, `:28`, `:40`; `lib/bare-outcome.mjs:124`; `lib/bare-schedule.mjs`; `lib/bare-record.mjs` |
| The spend gate: `spendDecision(spent, ceiling, caps, { liveUsd, factor })`, `spendGate({ ledger, liveUsd, caps, ceilingUsd, bareBudgetUsd })` | `spikes/policy-ab-quick/lib/spend.mjs:18`; `lib/spend-gate.mjs:32` |
| The machine-quiet check, the CPU sampler, the spin probe; `quietVerdict`, `waitForQuiet` | `lib/cpu-sampler.mjs:120`; `lib/quiet.mjs`; `inputs/quiet.json` |
| The pre-registration guard and harness identity; the tag `prereg/minigit-bare-20261030`; the commit-reveal seed | `lib/prereg-guard.mjs`; `lib/seed.mjs`; `inputs/seed.sha256`, `inputs/seed.txt` |
| The A/A checks G-1 … G-14 and `aaVerdicts` | `lib/aa-verdicts.mjs:39`; [PLAN_minigit_stack_bench_E3_bare.md](PLAN_minigit_stack_bench_E3_bare.md) E3b.S4 |
| The contamination scan and its 18 instrument defects, 3 of which voided sessions of one stack (defects 1, 14, 16) | `lib/contamination*.mjs`, `lib/session-scan.mjs:80`; RESULTS §11 |
| Cache snapshots per stack, hashed, seeded into every box; `makeSnapshot`, `seedCaches` | `lib/cache-snapshot.mjs:162`, `:58`; `inputs/cache-recipe.json` |
| The generated research record: `extract.mjs` → `data/<series>.json`, `render.mjs` rewrites `<!-- generated:… -->` blocks, `replay.mjs` | `research/minigit/tools/` (11 files, 1402 lines) |
| **v1's process arm, designed and NOT built:** coai in its own box behind the stdio↔socket bridge (E2.S3), the one-source templates, snapshot branch, rulesets, PATs, tag (E2.S4), the process runner with blocks and the arm-lead draw (E3.S1), the 17-item compliance checker over GitHub, `coai.db` and the stream with its hand-check protocol (E3.S2), P0 and the model smoke (E3.S3), P1 (E3.S4) | `research/module_minigit_bench.md` § *What it does not do yet*; v1 §4.2, §5.4, §6, §7.2, §8 |
| v1's rule-asymmetry measurement: for `--task implement --task test --task git --task pr`, C# gets 34 rules / 331,833 B, TS 32 / 324,781 B (run 2026-10-29) | v1 §12 V1 |
| v1 pins: claude **2.1.310**, node 26.12.1, dotnetSdk 10.0.114, typescript 7.0.2; the TS toolchain read-only under `~/rsd/minigit/toolchains/typescript-7.0.2`, the CLI copy `claude-2.1.310` | `inputs/stacks.json`; `ls ~/rsd/minigit/toolchains` |
| Host today: claude 2.1.310, dotnet SDK 10.0.114 (apt), node v26.12.1; `/usr/bin/python3` is v1's held-out runner | `claude --version`, `dotnet --list-sdks`, `node --version`, `lib/heldout-run.mjs:19`, 2026-10-31 |
| Release indexes, read 2026-10-31: .NET 10 `latest-sdk` **10.0.403**, `latest-runtime` 10.0.14, released 2026-10-25, support phase active; Node `v26.13.0` 2026-10-28, lts "Lithium"; v26.12.1 is not marked LTS; v27.0.0-alpha.0 exists | `dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json`; `nodejs.org/dist/index.json` |
| npm, read 2026-10-31: fastify 5.12.7, @fastify/jwt 10.2.6, @fastify/type-provider-typebox 6.3.0, @sinclair/typebox 0.34.52, pino 10.4.0, typescript 7.0.2, express 5.3.0, @nestjs/core 12.3.2, hono 4.13.14 *(Superseded 2026-11-03 by the owner's decision of the epoch-7 re-review, after `ask_consultants` `40498063`, the consultants split 1–1:)* the pair 6.3.0 + `@sinclair/typebox` 0.34.52 does not fit: read on npm 2026-11-03/04, `@fastify/type-provider-typebox@6.3.0` has the peer dependency `typebox ^1.1.14` (the renamed TypeBox 1.x line), and 5.2.0 is the last version on `@sinclair/typebox` (`>=0.26 <=0.34`); `typebox` 1.1.39 is the highest 1.1 patch and 1.4.3 the latest | `npm view <pkg> version`; `npm view @fastify/type-provider-typebox@6.3.0 peerDependencies` |
| ASP.NET Core 10 Minimal APIs have built-in validation: `builder.Services.AddValidation()`, DataAnnotations on query, header and body, a source generator, 400 on failure, `.DisableValidation()` per endpoint; the APIs live in `Microsoft.Extensions.Validation` | Microsoft Learn, *What's new in ASP.NET Core 10.0*, read 2026-10-31 (**verified**) |

What does **not** exist: a task other than minigit; a task spec gated and frozen; a driver that starts a program, talks to it
over TCP or HTTP, samples its memory and stops it with SIGTERM; a judge for stdin/stdout tasks with time and memory limits;
a session longer than 60 min; a package cache that holds a web framework; references in two languages per task; the public
task repository; a parity audit of the C# and TypeScript rules in `dew_flow_conventions`; any process-arm code.

*(2026-11-05, read at the stop `b631c03d`; the table above stays as verified on its date.)* **Exists now, merged:** the runner 0.2.4 with parts i and ii-a (#141, #147), the execution layer with its memory policy, CP1 and CP2 (#153, #155), toolchains (#135), the skeleton and push check (#142), both cache snapshots (#159), FORMAT v2, gated (task repository PR #2), and its tools (#160, #161). **Exists, unmerged, closed for the stop:** E1.S3 phase A (#167) and E1.S8 (#164). **Does not exist yet:** a gated spec, a draft reference, a D40 gate record, a hidden suite, a registered limit, the scorer, the drivers, any process-arm code, the record tools for v2.

## 3. Decisions

*(2026-11-07, epoch 9:)* **D69–D76 are in the operative part** (*Operative plan for epoch 9*, *Decisions D69–D76*). Every row below stays as written; where a row differs from D69–D76, D69–D76 hold. For Stage 1 they supersede, in whole or in part: D1's and D3's ten tasks as the claim, D9, D10, D14's daemon and web memory rule, D20's process arm, D27's arm interleave, D43's "consumed by E3's scoring", D44, D45, D46, D49, D53, D57's floor probe and D58 for the six tasks, D65's `lib/submission.mjs`, `lib/deviations.mjs` and its classes (a) to (d), and D67 (4). D65's row is not edited here, so that the unmerged branch `feat/lang-v2-format-amend-pins`, which edits that row, merges without a conflict.

Rows marked "operator, 2026-10-31" are the operator's decisions of that day, recorded as given (D1–D8 from the morning,
D20–D31 from the afternoon's instructions A–G). Rows marked "team lead, 2026-10-31, from the coai plan gate" settle
questions the gate round on `aa5e413` closed (D32, D33). Rows marked "this plan" are proposals for the operator's approval;
the open questions (§17) name what they do not settle. Rows D44–D53 (2026-11-01) are the epoch-5 re-review of change 4.2:
the team lead's decisions of that day and this plan's shape around them, for the plan round and the owner's approval. Rows **D55–D59 (2026-11-02)** are the epoch-6 re-review of changes 5.1 and 5.2 (consult `ee6c5a9a`): the memory policy, the `memory limit` rule and its evidence boundary, the units and the floor, E2's calibration sequence, the fix forward. Rows **D60–D64 (2026-11-03)** are the epoch-7 re-review of changes 6.1 and 6.2 (CP1, CP2, `ask_consultants` `8b776383`): FORMAT's fifth class and A's frozen classification, the grammar per task, CP1's contract-level findings, CP2's conditions with owners, and one rule for every freeze. Rows **D65–D68 (2026-11-05)** are the epoch-8 re-review of changes 7.1 and 7.2 (consult `d2e1e648`): the submission and build contract, one acceptance policy across arms, the audit of what is frozen against who reads it, and the merge condition under load-sensitive tests.

| # | Decision | Who, when |
|---|---|---|
| D1 | **v2 compares C# and TypeScript coding agents on 10 harder tasks**, not the tiny minigit task. v1's finding (cost C#/TS ≈ 0.92, C# slower only in build and test time, every session accepted) motivates harder tasks with **runtime traps**: defects a compiler does not catch, which show in held-out tests | operator, 2026-10-31 |
| D2 | **A new public GitHub repository `dew_flow_ai_language_comparison`** holds the task workspaces: `csharp/` and `node/` (one sub-folder per task in each), `tasks/` (statements, visible samples, reference notes), and the pinned conventions as in the other `dew_flow` repositories. **Held-out tests and raw records stay outside git** (as v1). Because it is public: no secrets, no raw transcripts, no held-out content; known-problem statements are **paraphrased with credit and a link; the link only in the repository README**, never in the agent's prompt or workspace. The bench harness stays in `dew_flow_benchmark` | operator, 2026-10-31 |
| D3 | **Tasks: 5 known + 5 original; 4 olympiad, 3 daemons, 3 web APIs; difficulty from a few minutes to about one hour of agent work.** Traps are runtime traps on both sides across the suite. The list is §4.1 (order = expected difficulty; the times are guesses, calibrated by the pilot). Daemons and web APIs are scored by a harness that starts the program, drives held-out scenarios and checks replies, cgroup-accounted memory over time (`memory.peak`; RSS only as a descriptive curve — D13, D14, D36) and a clean SIGTERM stop. Consultant review on 2026-10-31 (codex gpt-6-astra, gemini) shaped the list: CSES 1712 dropped (a one-sided JS trap), CSES 1736 dropped (sums stay below 2^53 at CSES limits, ≈ 4e15, so its JS trap does not fire); gemini's claim that .NET console apps default to Server GC was **rejected** (they default to Workstation GC) — still, the harness fixes GC settings explicitly per stack and records them | operator, 2026-10-31 |
| D4 | **Method rules from the consultants, adopted:** the agent never sees a CSES/IOI id or title (paraphrased statement only); workspace samples are small and clean and the traps live in held-out tests, so the first-submission pass rate can show a difference; time and memory limits are calibrated on reference solutions in **both** languages, so no limit rejects one runtime's baseline footprint; a **paid pilot (tasks 1, 3, 10 at least)** measures real times before the full series; the bare session wall limit rises from v1's 20 min to **about 75 min**; v1's $6 per-session cap is **re-derived from the pilot** *Superseded in part by D44 (2026-11-01): "a paid pilot (tasks 1, 3, 10 at least)" is now §11's pilot table per group (A: bare 1, 3, 9; B: bare 4, 5, 10; C: process 1, 3); the text above stays.* | operator, 2026-10-31 |
| D5 | **Packages: option A — a pinned offline package cache per stack**, the same discipline as v1's cache snapshots; **the agent has no general network** (the process arm reaches GitHub, as v1's D21–D22) | operator, 2026-10-31 |
| D6 | **Stacks: both current LTS lines, latest patch, checked 2026-10-31 against the official release indexes.** C#: .NET 10, SDK **10.0.403** (released 2026-10-25; the host has 10.0.114, so it must be upgraded and pinned), runtime 10.0.14, C# 14 (the default for .NET 10; C# 15 ships with .NET 11, RC3 on 2026-10-31, not stable, so out). Web APIs use ASP.NET Core **Minimal API**; daemons the Generic Host (`Microsoft.Extensions.Hosting`); JWT `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x; tests the agent's choice from the cache (xUnit v3 offered). TypeScript: Node **26.13.0** (LTS "Lithium", released 2026-10-28; the host has 26.12.1, so it must be upgraded and pinned), TypeScript **6.0.x** with `strict` — the newest line the pinned `typescript-eslint` 8.72.x supports (its peer range is `<6.1`), so the TS agents get **type-aware lint at the same strength as the C# analyzers**; this matches the conventions plan's D7 (`dew_flow_conventions · todo/PLAN_node_rule_parity.md`); the exact patch is pinned in E1.S5 from npm. **7.0.2 was the earlier pin (v1's toolchain line), superseded by the owner on 2026-10-31 (D1)**: under 7.0.2 `typescript-eslint` cannot parse `.ts` and the lint runs without types (v1's D4 deviation), which would have handicapped TS in the process arm. The Node web stack is the team lead's choice, with the reasons recorded (§4.6): **Fastify 5.12.7** with TypeBox 0.34.x (`@fastify/type-provider-typebox`) *(Superseded 2026-11-03 by the owner's decision of the epoch-7 re-review, after `ask_consultants` `40498063`, the consultants split 1–1:)* with `@fastify/type-provider-typebox` 6.3.0 and **`typebox` 1.1.x** (the renamed TypeBox 1.x line, imported as `typebox`; `@sinclair/typebox` dropped), `@fastify/jwt` 10.x, pino, `node:test`, `node:net`. Toolchains are pinned in read-only copies with checksums (as `~/rsd/minigit/toolchains/`) and recorded in the pre-registration *(2026-11-05, D66, the text above kept:)* "type-aware lint at the same strength as the C# analyzers" has no evidence and is corrected: the TS agents have **type-aware lint available**; the record reports the actual invocations and outcomes per stack and arm. *(2026-11-05, change 7.1, D65:)* the C# set adds `Microsoft.AspNetCore.Mvc.Testing` (E1.S4b), because Fastify's `inject()` gives Node an in-memory test host with no package and C# had none | operator, 2026-10-31 (stacks); team lead, 2026-10-31 (the Node web stack, reasons in §4.6) |
| D7 | **Budget: no total ceiling.** The $1000 total of the morning is **removed by the owner on 2026-10-31 (late evening)**; the six rounds of both arms run as planned (D10, D27) *(2026-11-02: three rounds per group under D44, nine in the series plus C's anchor round, D54)*. **A per-session cap of $50 stays as a runaway guard**, a fixed owner value ("I don't think $6 is enough; let's do 50"): a session is stopped at $50 and recorded as censored (`killed: cap`, `≥ $50`), never hidden; the pilot's cap formula (§12) is superseded for the cap's value and stays as a reported cross-check that does not bind. Pilot, A/A, data, void and re-run spend are still counted on separate lines. *The $1000 ceiling, the arm split, the `budget-frame` halt over the total and the budget approval step are superseded (D1); their text stays in §12 marked so* *(2026-11-01, D53:) the $50 cap holds for every group; no new halt is added; per group a projection and a settled ledger line are information.* | operator, 2026-10-31 (the ceiling, the morning); owner, 2026-10-31 (no ceiling; the $50 cap) |
| D8 | **Same agent as v1: Claude Code (Opus, effort `high`), CLI pinned.** v1 pinned CLI **2.1.310** (`inputs/stacks.json`); v2 **re-pins at its pre-registration** to the newest CLI version installed on that day, copied read-only as v1's `init` does (today that is still 2.1.310); the v1 pin is not inherited. *Superseded in part by D20: v2 runs two arms, not the bare arm alone* *(2026-11-01, D47:) the re-pin is taken once for the series, at group A's pre-registration, and kept through C.* | operator, 2026-10-31; the re-pin shape: this plan |
| D9 | **Unit of analysis = the task**, with repeated measures: per task, stack and arm, 3 sessions (D20). The 120 sessions are not 120 independent samples (D28) | this plan, per instruction E5 |
| D10 | **Order: six rounds, arms alternating.** A round runs every task once in one arm; the arm of round 1 is drawn from the seed and the arms alternate (B P B P B P or P B P B P B), so after any even number of rounds both arms hold the same n; the task order inside a round is drawn from the seed; a task's second repeat in an arm runs only after every task has run once in that arm. The two cells of a pair (C# ∥ TS, same task, same arm) start in the same tick on masks `0-5` / `8-13`, swapped per pair as `maskFor` does; the two arms never overlap. The ceiling (D7) cuts whole round pairs (one bare + one process), never one task's repeats *Superseded for the staged series by D44 (2026-11-01): the arms no longer alternate — the bare arm runs in groups A and B, the process arm in group C. Inside a group the rule stands: a round runs the group's tasks once in a seeded order, a task's second repeat only after every task of the group ran once, pairs as above. The lost alternation is the narrowing of the epoch-5 re-review (T16). The text above stays.* | this plan, per instruction E4 |
| D11 | **The bare agent's workspace is a copy of one task folder** (`csharp/<task>/` or `node/<task>/`) at a pinned commit of the task repository, fetched and hash-checked as v1's `fetchInputs` does. The repository's root files, the other stack's folder, `tasks/` and `todo/` are never in the bare box. The bare agent gets **no** conventions. **The process agent's workspace is a single-branch clone of its run base `run/<rid>/main`** in the task repository (v1 §4.2), which carries the conventions, the hooks, the frozen spec and both stack folders; its prompt names its stack's folder | this plan |
| D12 | **Fixed per-stack scripts in every workspace, frozen inputs with hashes:** `build.sh` and `run.sh` (olympiad: reads stdin, writes stdout; daemon and web API: the long-running program, `PORT` and the task's inputs from the environment). **`run.sh` ends with `exec` of the program, so the started process IS the program (one process; SIGTERM reaches it, never a bash parent)** — change 2.3, D38. The agent fills the code; the harness drives only these two scripts, identically for both stacks. A `PACKAGES.md` lists the offered package set of the stack (D5); the spec tells the agent what is frozen *(2026-11-05:)* the two scripts' bytes, the frozen list and what the agent may edit are D65's | this plan |
| D13 | **Scoring per task kind** (§4.3): olympiad — held-out input/expected pairs under calibrated time and memory limits, each case `pass` / `wrong answer` / `time limit` / `memory limit` / `runtime error`; daemon and web API — held-out scenarios driven by the harness, readiness within T, a SIGTERM stop that must end the process within the grace period G with no child left (D14's series defaults and overrides), the **cgroup-accounted memory bound** certified by the program's own cgroup scope (D36). **Accepted** = clean build, every held-out functional case or scenario passed, every lifecycle check passed (readiness, the SIGTERM stop within G, no process left), **and the certified memory claim** (`memory.peak` of the program's own scope within the registered limit, no `oom_kill`). **One memory contract (D36): for a data session a readable `memory.peak` is a precondition — the runner refuses to start one on a host without it, with the named halt `memory-not-certifiable` — so `accepted` always includes the certified memory claim and no CPC denominator is ever touched by certification.** *(2026-11-02, D56:) "no `oom_kill`" reads "no `oom_kill` and no recognised runtime out-of-memory abort with evidence": `memory limit` has two causes and one rule, the same for both stacks, and the record names the cause.* *(2026-11-05, D65, D66:) "clean build" is the frozen `build.sh` exiting 0 on the submitted tree with the frozen files restored; the policy is one for both arms and both stacks, and deviations and the strict CI job stand beside `accepted`, never in it.* The flag `memory bound not certified` exists only for **non-data runs** (the smoke, the stub fixtures, and the pilot where the operator runs it on a host that cannot certify — allowed, since pilot sessions are void by D16): such a run is reported on its functional, lifecycle and stop checks with the flag and no memory claim. One suite per task runs unchanged against both stacks (D22) | this plan |
| D14 | **Limits, by kind (change 2.1).** *Olympiad:* time limit = **3 ×** the slower of the two reference solutions' wall, floor 1 s; memory limit = **2 ×** the larger reference peak, floor 256 MB. *Daemon and web API:* the **cgroup-accounted memory bound** = **2 ×** the larger reference peak over **all** the task's load scenarios (task 5 has two), floor **256 MB** — the same floor as olympiad; §4.1's "memory floor for a daemon" is this. **Semantic bounds live in each spec** (max connections, requests, payload bytes, clock semantics, expiry tolerance — spec 05's R32, R33, R23 are the shape). **Timing has series defaults with explicit per-task overrides:** readiness **T = 10 s**, grace **G = 5 s** (provisional), scenario-step deadlines as `FORMAT.md` defines; a spec states its T and G and may override with a reason. **Validation before the freeze:** T, G, the step deadlines and the semantic bounds are run against both draft references through the runner before the spec freezes (D40); a value a reference cannot meet is changed before the freeze, never after. **Calibration after the freeze:** execution time and memory by this rule (E2.S3). The spec states the floors and the rule as guarantees; the exact values go to the runner, never into the spec (change 1.2a). Peak memory is `memory.peak` of the program's own cgroup scope (D36; cgroup accounting includes page cache and kernel memory); **only that certifies a bound**. RSS is sampled — 100 ms for olympiad, 1 s for daemon and web — as a **descriptive curve only**, the one statement of the sampling rates; never `RLIMIT_AS`. **Where a spec states a tolerance around a time boundary, the held-out checks are placed against that tolerance as the private aid says** (D41; the placement rule moved there). The numbers are computed by E2 and pre-registered; a change after the pilot is a dated amendment applied to both stacks *(2026-11-02, D57, D58:) the floor reads **256 MiB** (268,435,456 B; every memory figure of the series is in MiB), settled as F — 256 or 512 MiB by a rule fixed now — by the floor probe before the first registration, never raised on the OP-K toys alone; the 2 × rule is applied by D58's sequence (a provisional run, the candidate, a confirming run under the candidate and the heap settings derived from it), so a registered limit's evidence is a run under that limit itself; the time limit is measured under the registered memory limit and its settings* | this plan |
| D15 | **GC and runtime settings, fixed and recorded per stack:** C# `DOTNET_gcServer=0` (Workstation, set explicitly), `DOTNET_TieredCompilation` default, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`; Node: no `--max-old-space-size` flag from the harness (the memory limit is the cgroup's); `NODE_OPTIONS` empty. **Culture (D35): `LANG=de_DE.UTF-8` and `LC_ALL=de_DE.UTF-8` in both boxes, both stacks, every task; no `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` and no other invariant switch.** **Time zone: the base `TZ` of both boxes is `UTC`**; a test may set another `TZ` for one program through the scenario's `env` where the spec defines time-zone behaviour, and the record names it. Every value is in `agentEnv`'s allow-list, the same in the agent box and the scoring box, and in every session record *(2026-11-02, D55: except the two per-case heap keys, which are the scoring box's alone — never in the agent box or the build box — and in the record)*. **This row is the source of the base environment:** both stacks get the same base plus these documented per-stack variables; `FORMAT.md`'s "the same for both stacks" sentence follows this row (FORMAT v2, E1.S9) *(2026-11-01, D51:) OP-K — whether to set `DOTNET_GCHeapHardLimitPercent`, or to expose a read-only cgroup view in the box — is decided by the team lead on E1.S8b's asserted memory probe before E2-A calibrates, as a dated line here, the same for every task and both stacks.* **OP-K, decided 2026-11-02 by the team lead (Opus)** on the asserted probe (`inputs/exec-probe-2026-11-02.json`) and the OP-K experiment (`inputs/opk-experiment-2026-11-02.json`), after `ask_consultants` `857f781b`: **each program sees its OWN cgroup, read-only** (`--unshare-cgroup` and `prog/` bound at `/sys/fs/cgroup`; nothing else of the host), and **.NET gets `DOTNET_GCHeapHardLimit` = the case's memory limit in bytes** (hexadecimal; the percent form cannot express 100 %), so both stacks know the limit and may use all of it. Why: with no view, .NET sized its GC to the host (46 GB) and got a false `memory limit` on live byte arrays at 45 % of the limit, at 256 and 512 MiB; with the view alone, .NET caps itself at 75 % of the limit. Node: no flag (its `constrainedMemory()` reads the view; V8's heap limit is its own). With a runtime's own out-of-memory abort judged `memory limit` (change 5.1). **Superseded in part by D55 (change 5.2; the epoch-6 re-review, 2026-11-02), the text above kept:** both runtimes are capped at **90 %** of the case's limit L — `DOTNET_GCHeapHardLimit` = ⌊0.9·L⌋ bytes (hexadecimal) and `NODE_OPTIONS=--max-old-space-size=⌊0.9·L / 1 MiB⌋` — as per-case **scoring-box** keys the layer derives from L; "Node: no `--max-old-space-size` flag from the harness; `NODE_OPTIONS` empty" stays true for the **agent box** only, and "the hard limit = the case's limit" is superseded everywhere — the agent box has **no** hard limit and the scoring box has 90 %; this row's base stands unchanged in the agent box, which gets neither key (the agent's own `node` and `dotnet` processes are not the program under test; T-L14). *(2026-11-05, the epoch-8 audit, §6.2 row 10:)* the build box of `lib/workspace-box.mjs` sets keys this row does not name — C#: `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`, `UseSharedCompilation=false`, `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1`, `NUGET_CERT_REVOCATION_MODE=offline`, `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`, and `NUGET_PACKAGES`; Node: `NPM_OFFLINE_ENV` and `npm_config_cache`. They are the **build box's keys**, named here, frozen at A with the allow-list (D47), and listed in the record; they select no behaviour of the program under test. **The agent box needs none of them added for the build:** `The_same_templates_build_in_the_agent_box` passes with no extra environment in both stacks, and in particular E3.S5 sets no `npm_config_update_notifier`, because the template's `.npmrc` sets `update-notifier=false` (E1.S3's finding 1) | this plan |
| D16 | **The pilot's sessions are void (never data)**, as v1's A/A and pilot were; they fix the stall timeouts, the time guesses of §4.1 and the limits of D14, and they measure real session costs (the formula's cap as a cross-check beside the owner's fixed $50 cap, D7; no split, D30 superseded). Bare pilot: tasks 1, 3, **4**, **5** and 10 as pairs (C# ∥ TS) plus one A/A pair (two identical bare C# sessions of task 3) — task 4 (the Rule API, the smallest web task) for the HTTP driver, task 5 (the RESP server) for the TCP/RESP driver, task 10 (the DAG scheduler) for the SIGTERM, RSS and timer checks, so G-16 checks every driver on a real tree before round 1; the pilot option is taken over a hand-check on a reference tree. **Pilot cost, re-derived at the §12 guesses:** 10 bare pilot sessions + 2 A/A at $5–20 → $60–240; 4 process pilot sessions at $30–70 → $120–280; **≈ $180–520 in all**, reported as pilot spend (no data share is computed since the owner removed the total ceiling, D7). Process pilot: tasks 1 and 3 as pairs (v1's P1 conditions, §11). The go/no-go table is v1's G-1 … G-14 plus §11's G-15 … G-19 *Superseded in part by D44 (2026-11-01): the pilot and the A/A run per group — §11's table; the cost line above is replaced by §12's per-group projection; the text above stays.* | this plan |
| D17 | **Egress:** bare sessions — no registry, no host; the only road out is the API broker's door. Process sessions — **no direct `github.com` egress either (D42)**: `EGRESS_HOSTS.langProcess` is empty and the GitHub operations go through the local GitHub broker's door, bound to the current run; package installs in both arms resolve from the seeded offline caches (D5). A refused CONNECT is flagged `wanted network`, not voided | this plan |
| D18 | **Contamination needles for the known tasks** (id, title, site names; `CSES`, `IOI`, `Werewolf`, `RealWorld`, `Conduit`, the public repository's name and URL; **for task 5: the product name `Redis`, the protocol name `RESP` with its versions, `redis.io`, `redis-cli`, and the common client libraries `StackExchange.Redis`, `ioredis`, `node-redis`, `redis` as a package name**) join the scan's needle list, in tool inputs and outputs, and are run over every generated run base and its reachable history (D39). **The matching rule (change 4.1):** a needle matches **case-sensitively on word boundaries** — `\b<needle>\b` with ASCII letters and digits as word characters — so `RESP` does not match "response" and `IOI` does not match the base64 `iOiJ`; product names that are not words (`redis.io`, `redis-cli`, `StackExchange.Redis`, `node-redis`) are an explicit list of exact substrings; base64 and JWT payloads in sample transcripts are excluded from the scan only where FORMAT marks the field as normalised, otherwise they are scanned (T-L37). A hit is **read by hand**: the agent recalling a known problem from memory is a threat of the design (T1), not a void; a fetch or a path reaching outside the box is a void, as v1 | this plan |
| D19 | **Known-task statements name no source.** The link and the credit live in the task repository's README (D2), in an attribution section that is **outside every agent-visible tree** (D39); `NOTES.md` is dropped. The bare workspace holds the statement, the samples, `PACKAGES.md`, `build.sh`, `run.sh`, the stack's pin files, **and the read-only runner and grammar (`runner/run_samples.py`, `runner/GRAMMAR.md` — D37, change 2.2)**, nothing else | operator, 2026-10-31 (D2); the file list: this plan |
| D35 | **One declared non-invariant culture for the whole series: `de_DE.UTF-8`** (`LANG` and `LC_ALL`), in **both** the agent box and the scoring box, for both stacks and every task, and in CI's visible-sample runner — **confirmed by the owner, 2026-10-31** (en_US was considered and `de_DE` kept). Recorded in the pre-registration and in D15's allow-list; the locale is generated in the box image (`locale -a` lists it). No `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` and no other invariant switch: a missing `LANG` is not .NET's globalization-invariant mode, and neither is wanted. **Fairness is a declared environment plus the protocol's stated numeric syntax** (spec 04 states its grammar, R15 and 3.6), not equal trap counts. E3's acceptance runs a check inside the actual boxes (T-L32). Closes Q8 | this plan (epoch 3 re-review; consult `3ea9ef36`) |
| D36 | **Memory certification by the program's own systemd user scope, required before launch.** The scorer runs each program in its own `systemd-run --user --scope` with `MemoryMax=<limit>` and `MemorySwapMax=0` (the bound is on memory, never memory plus swap), launched **outside** `bwrap`; the driver and the build run outside that scope. It verifies that the program and every forked child belong to the scope (`cgroup.procs`), and it keeps `memory.peak`, `memory.events` (`oom_kill`, `max`) and the lifecycle evidence before the scope is removed. The host was probed on 2026-10-31: cgroup2 with the memory controller; `systemd-run --user --scope -p MemoryMax=64M` gives a readable `memory.peak` (2,396,160 B) and `memory.max` — **this host can certify**. The pre-registration records the certifying host; **a host that cannot certify runs no data session: the runner's certification probe runs at start and refuses with the named halt `memory-not-certifiable` (exit 3, resumable on a certifying host)** — so for every data session `accepted` includes the certified memory claim (D13). "RSS bound" is replaced by the **cgroup-accounted memory bound** everywhere, calibration included. The flag `memory bound not certified` is for non-data runs only (smoke, fixtures, a pilot the operator allows on a host that cannot certify); T-L29 is the refusal test; T-L30 adds the fixtures *(2026-11-02, D55, D56:) `MemoryMax` = the registered limit L bounds the CASE — the program and every child; the runtimes' own heap budgets inside it are D55's, derived from L by the layer per process; the program sees its own cgroup read-only; a kernel kill of any process of the scope is `memory limit` (D56)* | this plan (epoch 3 re-review; the probe by the team lead) |
| D37 | **The agent has the scenario runner and the grammar.** The visible-sample runner — the bench's own Python standard-library program, **built early in E1 (E1.S8a) and pinned there**, implementing FORMAT v2's grammar and transcript comparison (D22) — and `FORMAT.md`'s grammar sections are in every workspace as read-only frozen files, `runner/run_samples.py` and `runner/GRAMMAR.md`; the task repository carries the pinned copy with its hash in `inputs/specs.json`; CI's visible-sample runner is that same file; FORMAT v2's examples and every pre-freeze check (D40) run through it; E2.S2 reuses it for the held-out suites and owns no runner; the agent may run the samples with it (`python3 -I runner/run_samples.py samples/`). The ~20-line reading block FORMAT v1 put in every daemon and web spec goes once the runner is present (FORMAT v2, E1.S9). Resolves change 2.2 *(2026-11-01, D50:) groups A and B run on two pinned runner manifests — A's of parts i and ii-a, B's with part ii-b — under D50's three guards; each group's manifest hash is in its pre-registration.* | this plan (epoch 3 re-review) |
| D38 | **`run.sh` ends with `exec` of the program.** One process: the pid the harness starts is the program's, SIGTERM reaches it, and the stop and leftover checks are about the program. E1.S3's templates carry it; T-L31 checks it. Resolves change 2.3 *(Wording, 2026-11-01, the E1 stories' § 12 item 4:) under D43's layered launch the harness starts `systemd-run`; "the pid the harness starts" reads "the process `run.sh` becomes", which the runner finds by its host pid in the scope's `prog/cgroup.procs` (E1.S8a, consult B1). No change of intent.* | this plan (epoch 3 re-review) |
| D39 | **Attribution lives outside every agent-visible tree.** The cross-repository boundary (`common.planning-docs` § *Across repositories*) is named in the task repository's README attribution section — cut from every run base — and in this plan, never in a `todo/PLAN_*` of the task repository, which would enter every run base (OP-11). The public README keeps its attribution section (D2), and **the run base is an orphan commit of a generated tree**: the frozen tag's tree minus the README's attribution section and minus any credit file, so the process agent's single-branch clone holds exactly one commit of history before its own work. The check: the needle scan (D18, with its case-sensitive word-boundary rule and exact-substring list — change 4.1) runs over the exact generated tree **and** `git rev-list` of the base (one commit), and the cut is refused on any hit of that rule; a spec path, a fixed FORMAT sentence or a normalised sample field never counts as a hit by construction. `NOTES.md` is dropped: credit is the README's section, design reasons live in the bench's internal test-design notes. `FORMAT.md`'s daemon example must not name the real protocol (FORMAT v2) | this plan (epoch 3 re-review) |
| D41 | **No new trap content anywhere public before the series ends** — plans, stories documents, review bodies, PR descriptions. The test-design aid lives in the private area `~/rsd/lang-v2/test-design/`; this plan's §4.3 trap table and the per-task trap columns are **replaced by a pointer** to `~/rsd/lang-v2/test-design/PLAN-trap-table.md` (mode 0600), moved verbatim on 2026-10-31, sha256 `4acd751a8c0514f8742ad3ecf60eed417a12c53d2eb07e785355b62f2b0a228c`; **and its sibling `PLAN-risky-scenarios.md` (mode 0600), sha256 `84b692d21199cba3ff2783880fba866985ba930b63380db195dad87601a9c5d6`**, which holds, word for word, the per-task riskiest held-out-shaped scenarios that D40, E1.S2, D14's expiry placement, §4.3's web row and G-16 named until the epoch 4 round, §4.3's task-1 trap-balance sentence and D40's per-task count and kind breakdown (added at the second full reading), and the breakdown updated after all ten spec drafts (a dated line, the earlier text kept); the content is kept, only its place changes (D1, D34). The merged history (#124, #127, #128) keeps the earlier rows — a disclosed residual risk (T18), named in the record. Resolves change 3.2's first half | this plan (epoch 4 re-review; consult `16e5f408`) |
| D42 | **The process agent gets NO direct `github.com` egress.** Its GitHub operations go through a **local GitHub broker** (the shape of the coai bridge) that **serves an isolated bare repository per run**: for each run the broker creates a local bare repository holding **only the orphan run-base commit (D39) and the run's own refs**; the agent's `git clone`, `fetch` and `push` go to that bare repository through the broker's smart-HTTP endpoint and never to GitHub; the broker **mirrors** the run's branch to GitHub and, on the agent's behalf through the GitHub API with the PAT, creates and updates the run's PR and reads its checks and comments — the allowed operations only. Ref filtering alone cannot hide objects that exist in the task repository's object database (a fetch by SHA, an unshallow fetch), so **no object outside the run base can reach the agent**: the objects are not there. **Allowed:** clone and fetch of the run base and the run's own branch; push to its own branch; create and update its own PR; read its own PR's checks and comments. **Denied (the retrieval probes):** the bench repository's history; the task repository's `main` attribution; another run's branch or PR; a fetch by the SHA of a task-repository `main` commit; an unshallow fetch (which gives only the run base); any redirect to a host or path outside the broker. The normal clone, push, PR and checks workflows must succeed while every denied probe fails (T-L35, pilot-tested in G-20). **This narrows the measured treatment:** the process arm no longer has the family's open GitHub access (it cannot read other repositories' history, the task repository's `main` or other runs) — a contract change recorded here, disclosed in T19 and in the record as *"two stacks under the same run-bound GitHub policy"*, and **a narrowing of the plan (the epoch 4 re-review record)**; tasks, arms, metrics and acceptance criteria stay. Resolves change 3.2's second half | this plan (epoch 4 re-review; consult `16e5f408`, verified on this host by the team lead; the bare-repository shape from the second full reading) |
| D43 | **One reusable execution layer, built in E1 (E1.S8b)** and consumed unchanged by E2's validation and calibration and by E3's scoring: launch; D15's environment with the culture; the program-only systemd scope (D36); the network layout below; deadlines; signals; cleanup. E3 keeps orchestration, scoring aggregation and records. **Network layout:** a private, loopback-only network namespace shared by the driver and the program, with separate filesystem and PID isolation (bwrap, as v1) and separate cgroups (the driver outside the program's scope). Probed on this host 2026-10-31: unprivileged `unshare -rn` gave `net:[4026532221]` against the host's `4026531833`; in it `ip link set lo up` worked and a Python server and a driver exchanged `ping→PING` over `127.0.0.1` with identical `/proc/<pid>/ns/net`; `--mount-proc` was refused, so PID and filesystem isolation come from bwrap. **D40 is not weakened:** the pre-freeze gate runs on this layer in E1. The producer/consumer table of §6.1 names the shared artefact, its probes and what invalidates earlier evidence. Resolves change 3.1 | this plan (epoch 4 re-review; consult `16e5f408`, verified on this host by the team lead) |
| D40 | **The freeze order (the root cause of the two stops):** before a spec freezes, **both draft references execute the visible samples through the distributed runner in the final sandbox environment**; draft references are allowed before that gate; calibration with held-out cases stays after the freeze. So E1.S6's sample files exist before each spec's conformance gate. **The draft references are written in E1 (E1.S2a) by the team lead with Sonnet coders per the role table, one per stack per task; they live in the bench's private area `~/rsd/lang-v2/refs/` — outside the task repository's git, beside the held-out material — and E2.S1 finalises, validates and calibrates them.** **The D40 gate also runs each task's riskiest held-out-shaped scenarios** through the runner, kept out of git and **listed in the private aid** (`~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md`, D41): some tasks have private pre-freeze scenarios, listed in the private aid with their counts; the public text names none of them | this plan (epoch 3 re-review; consult `3ea9ef36`; the scenario list moved to the private aid at the epoch 4 round) |
| D20 | **Two arms, as v1's parent plan designed them.** **Bare:** 3 runs per task per stack on pure Opus with the task prompt only. **Process:** 3 runs per task per stack under our rules — the harness, the pinned conventions, the coai gates (plan gate, code gate, consultant), an Opus team lead and Sonnet coders. So each task has **6 repeats per stack**, 120 data sessions. v1's parent plan is reused for how the process arm is run and measured (§4.2, §6, §8); "bare first, process out of scope" is withdrawn everywhere *(2026-11-01, D44:) the 120 are groups B and C; group A adds 24 bare sessions of four tasks as the pre-registered replication, so the series has 144 data sessions.* | operator, 2026-10-31 (instruction A) |
| D21 | **Task specs are written by the team lead**, not by the agents under test (two kinds of "spec", OP-10: the team lead writes the *task* specs; a cornerstone story's *story* spec — the brief a Fable writes per the role table — is a different document). Each of the 10 specs goes through the **coai gates by all the rules, with a consultant**; the gated specs are stored in the new repository at **`todo/tasks/NN-slug.md`**; after the gate they are **FROZEN** (a tag `spec-frozen-<yyyymmdd>` and the hashes in this repository's pre-registration; the harness refuses a session whose spec blob differs). Then, per language and in isolation, the **process-arm agent makes its own plan** from the frozen spec; that plan goes through the gates in its run; **the task spec is never changed**. One story (E1.S2) covers the ten specs with per-task acceptance criteria *(2026-11-01, D45:) per group — one freeze tag per group (`spec-frozen-<group>-<yyyymmdd>`), and "after the pre-registration never" reads "after its first pre-registration never": a spec frozen for A never changes in B or C.* | operator, 2026-10-31 (instruction B) |
| D22 | **Cross-language tests: one shared black-box acceptance suite per task** that runs unchanged against both implementations — stdin/stdout for olympiad tasks, HTTP for web APIs, TCP/RESP and signals for daemons. Written once, in one neutral harness language, **owned by the bench**, not by either stack. A **visible part** (samples in the workspace) and a **held-out part** (outside git). The harness language is **Python 3, standard library only, run with `-I`** — v1's held-out runner — because it is third to both stacks; the argument is §4.3 | operator, 2026-10-31 (instruction C); the language choice: this plan |
| D23 | **The conventions are pinned in the new repository, as in the other `dew_flow` repos, at a release that passes a conventions parity audit:** every C#/.NET rule in `dew_flow_conventions` has an equivalent Node/TypeScript rule adapted to Node, and the other way round. The audit **runs now, in parallel** (E0.S1); its result is a prerequisite story: missing counterpart rules are written and released in `dew_flow_conventions` **before the v2 pre-registration** (E0.S2), and the new repository pins that release (E0.S3). Threat T14 names the bias the audit controls *(2026-11-01, D48:) "before the v2 pre-registration" means before group C's pre-registration.* | operator, 2026-10-31 (instruction D) |
| D24 | **Primary metric: cost per correct solution.** For task t, stack s and arm a: `CPC(t, s, a) = Σ cost of all data attempts of (t, s, a) / number of accepted solutions among them`. Beside it: success rate (accepted / attempts), tokens per model and kind, wall, `model_api`, `active − model_api`. A cheap unfinished run never looks like a winner: it adds to the numerator and nothing to the denominator. A (t, s, a) with **0 accepted** shows **"no correct solution"** plus its attempts and its total spend (labelled spend, not CPC) and is **not dropped**; its C#/TS ratio is **undefined** and reported as an outcome, never as a bound (§9, consult `68a33be2`). A second line counts void attempts in the numerator too (§9) | operator, 2026-10-31 (instruction E1); the void line: this plan |
| D25 | **The same functional requirements and the same checks for both stacks.** Statements describe the required behaviour neutrally. **The trap list is an INTERNAL test-design aid**, kept with the held-out material in the private area (D41), never in a statement **and, from 2026-10-31, never anywhere public before the series ends — not in this plan, a stories document, a review body or a PR description**; **every held-out test checks behaviour the statement requires**. **Review step:** a reviewer — the coai document gate plus a consultant — checks each statement and its tests for a bias toward or against one language's specifics, before the freeze (D21) and before the suite is sealed. The trap table is kept, on the test-design side (§4.3) *(2026-11-08, epoch 10: the review stays with the external vendors, under the exposure disclosed in Q-C. **Its leak rule:** the suite goes to the vendors from a scratch folder outside every repository, and the gate records in `inputs/gates/` hold no case text, case id or trap name, only the verdict, the reviewer count and the findings in words that name no case; F2.S1.)* | operator, 2026-10-31 (instruction E2) |
| D26 | **Freeze before any paid data run:** tasks (specs), tests (suites, hashes, counts), budgets (the $50 per-session cap, D7; no ceiling or split since the owner's decision), the analysis (§9), the exclusion grounds (voids, stops) and the re-run grounds — all in the pre-registration, tagged. **Every attempt is kept**, including failed, voided and re-run ones, in the records and the report *(2026-11-01, D45:) per group — each group's pre-registration freezes its own tasks, tests, budgets, analysis and grounds before that group's first paid data run, and adds to the earlier groups' tags, never alters them.* | operator, 2026-10-31 (instruction E3) |
| D27 | **Spread the 6 repeats per task over time:** a seeded random task order per round; the paired concurrent C# ∥ TS start of v1 stays; the CPU masks alternate per pair as v1 did; **bare and process rounds interleave** (D10), so one good or bad API day does not decide the result; the start and end time of every session is recorded *Superseded in part by D44 (2026-11-01): "bare and process rounds interleave (D10)" no longer holds — the arms run in different groups; the spread over time holds inside each group; the cost is T16.* | operator, 2026-10-31 (instruction E4) |
| D28 | **Analysis is per task:** 10 tasks with repeated measures, not 120 independent samples. Each task is shown; the **overall result is the median of the per-task C#/TS ratios** (primary), the **geometric mean of the per-task ratios** beside it, and the **count of tasks where C# is higher** — per arm and per measure; pre-registered. No expensive task may hide the others: no pooled sum across tasks is a headline | operator, 2026-10-31 (instruction E5); the choice of the median as primary: this plan |
| D29 | **Known and original tasks are shown separately**, and the report discloses that the five public tasks may be familiar to the model | operator, 2026-10-31 (instruction E6) |
| D30 | *Superseded by the owner on 2026-10-31 (D1, D7):* the $1000 total is removed, so no split between arms is derived and Q-A is closed. Was: "The $1000 is not changed by this plan. With two arms (120 data sessions) and dearer process sessions, the split between arms is re-derived from the pilot (§12); whether $1000 covers both arms or the process arm needs its own limit is open (Q-A)." What stays: the pilot still measures real costs and reports the formula's cap as a cross-check beside the owner's $50 | operator, 2026-10-31 (instruction F); superseded by the owner, 2026-10-31 |
| D31 | **The repository name stays `dew_flow_ai_language_comparison`, public from day one.** Held-out material and raw records stay outside git | operator, 2026-10-31 (instruction G) |
| D32 | **v2 builds the process-arm pieces itself** (E3.S6–S9): the coai box and the stdio↔socket bridge, the process templates, rulesets and PATs (the repository side in E1.S7), the process runner, the compliance checker, P0 and the model smoke. They do not exist: v1's E2.S3, E2.S4 and E3.S1–S3 are not built. **v1 is closed after its bare arm; v2 is the active plan.** v1's parent plan gets only a STATUS line saying its process arm moved to this plan — a status update, not a plan change — as a follow-up outside this commit (§16). v1's design (its §4.2, §5.4, §6, §7.2, §8) stays the specification v2 builds to | team lead, 2026-10-31, from the coai plan gate (closes Q-B) |
| D33 | **Task 9 I/O: option (a).** The spec `todo/tasks/09-switched-routes.md` (the path carries no needle word — change 4.1; `09-werewolf.md` was the earlier name) defines an exact stdin/stdout grammar and the agent implements the parser. There is no frozen helper or adapter code in `csharp/09-*` or `node/09-*`; the workspace holds the same files as every other olympiad task | team lead, 2026-10-31, from the coai plan gate (closes Q3) |
| D34 | **Metrics are only added, never replaced or dropped.** Every v1 metric stays in every v2 record, per session and per pair: the four cost views (headline recompute, E envelope, O OTel, B broker) with the checks T1–T5; tokens per model and kind, with the 5m / 1h cache-write split; the exclusive time partition (wall spawn to exit, `model_api`, `active − model_api` and the per-category spans); visible and held-out quality as counts, `percent` and `defects`; outcome class, labels, flags and charges; the process arm's compliance items. v2 ADDS beside them: the cost per correct solution (D24, and its void line), the success rate, the per-task medians and geometric means (D28), and the known/original split (D29). A v2 table may lead with a new metric; it never omits a v1 one *(2026-11-07, D74: superseded in one reading only. The per-case CPU time and RSS curve that D67 (4) planned for v2 were never measured, and they leave Stage 1 as follow-up work. Every v1 metric and every metric Stage 1 measures stays in every record, so no collected value is dropped; "no metric is dropped" in T-L64 and §16 no longer covers those two planned fields. The text above is kept.)* | operator, 2026-10-31 ("only add metrics") |
| D44 | **The staged series (change 4.2).** The series is built and run in **three groups, each to its own evidence before the next starts**. **Group A:** the four olympiad tasks 01, 02, 03 and 09, bare arm only. **Group B:** the bare arm for all ten tasks — the six others **and A's four again**: B's runs of 01, 02, 03 and 09 are a **pre-registered replication**, registered at A in that role (not a replacement, not extra repeats), so A's and B's figures for those four stand side by side with the drift table and are never pooled (D46). **Group C:** the process arm for all ten. **Nothing is dropped:** every task, arm, metric, test and acceptance criterion stays; the order of building and running changes. A group's **evidence** is its pre-registration (its own tag, D45), its pilot and, where its arm needs one, its A/A (§11), its three rounds (§4.4) and its interim record (D49). B's building may overlap A's rounds, because nothing of B can change A's pinned manifest (D45); B's paid sessions start only after A's interim record, and C's after B's. Data sessions: A 24 (4 tasks × 2 stacks × 3), B 60 bare, C 60 process — 144, of which D20's 120 are B's and C's. **The exploratory pilots** (task 1 on main; the olympiad plan `todo/PLAN_lang_explore_olympiad.md`) are never v2 data: their references, generators, brute-force checkers and cases may seed E1.S2a and E2.S2 **only through v2's own chain** (the D40 gate, the joint validation, the mutants, the bias review), and A's `prediction.md` discloses that an exploratory run of the same four tasks on another harness came first. For the staged series this supersedes D10's alternation, D27's interleave of the arms, D16's one pilot, D4's pilot set and §14's single order — each marked in place | owner, 2026-11-01 (the staged order); the shape: this plan (epoch-5 re-review; consult Fable § 1–2, § 5; coai cadence `f25ad04b`); B re-runs A's four: the team lead, 2026-11-01 |
| D45 | **A spec freeze and a pre-registration per group; append only.** Tags `spec-frozen-A-<yyyymmdd>` and `spec-frozen-B-<yyyymmdd>` (C freezes no spec: it runs the ten frozen ones), and `prereg/lang-v2-A-<yyyymmdd>`, `-B-`, `-C-`, each committed before its group's first A/A or pilot launch (E4.S3 per group). Each pre-registration pins an **immutable group manifest**: the task membership; per task the spec and sample hashes and the freeze tag's commit and tag-object shas; FORMAT's sha; the runner manifest and the grammar's hash *(judge J6, 2026-11-03: per task, D61 — A's four carry A's grammar hash in every group)*; the execution layer's identity; the D40 gate records; the registered limits; the certifying host (host, kernel, cgroup, the probe — a WSL kernel update between groups is recorded and re-probed, never a void); the culture and `TZ`; the pins of D47; the package sets and cache hashes; the $50 cap; the group's analysis contract (§9); `prediction.md`; `seed-<group>.sha256`. **Append only across groups:** a later tag may add and may never alter what an earlier tag fixed for a shared task (spec, suite, limits, prompt, the environment allow-list); A's tag is never moved and A's pinned FORMAT is never replaced. **D21's "after the pre-registration never" reads "after its first pre-registration never"**: a spec frozen for A never changes in B or C, even when A's paid rounds find a defect — blind adjudication handles that (§4.3). **FORMAT's sections are classed** `common`, `olympiad`, `daemon` or `web` (E1.S9, one line per section): after A's freeze the `common` and `olympiad` sections are immutable for the series; a `daemon` or `web` section may be amended before B's freeze, and such an amendment touches no olympiad suite (G-17 stands as written); `inputs/specs.json` records FORMAT's sha per group, and an olympiad spec must pass the conformance check under both shas (T-L39). A later FORMAT change is a new dated version naming the tasks it touches; a group that adopts a changed contract for an earlier group's task needs fresh evidence for it (the D40 gate, calibration) before any launch, and its sessions under the two contracts are separate cohorts — never relabelled, never pooled (T-L48). A launch is checked against the selected group's manifest *(Second full reading, 2026-11-01, finding 1:) the "limits" an earlier tag fixed include A's registered limits of 01, 02, 03 and 09: B's replication runs under them unchanged; D50's guard (2) re-runs the gate and the calibration only as a check, and a different value is a drift finding, never a new registration.* *(2026-11-03, D60, change 6.2:) the classes are five — `common`, `olympiad`, `daemon`, `web` and **`daemon-web`** (serves daemon and web tasks and no olympiad task; `common` is a section shared with olympiad tasks, or global); a `daemon`, `web` or `daemon-web` section may be amended before B's freeze; the protected set is read from A's frozen FORMAT, never from B's class table (T-L56); the grammar is pinned per task (D61).* *(2026-11-05, D65, D67 (3):) the manifest also pins the group's **workspace commit** and the hash of `inputs/hashes-lang.txt`, the **build contract's identity** and the list of the `deviations` metric; a launch is checked against them; "the freeze tag's commit" stays the anchor of the spec blobs (T-L20, T-L67).* | this plan (epoch-5 re-review; consult Fable § 2.1, § 5 items 1–2; coai risk `b11c7e5a`) |
| D46 | **CPC per group; A is the replication; never pooled.** `CPC(t, s, a)` (D24) is computed **per group**: for 01, 02, 03 and 09 the bare CPC of A and of B stand side by side with a **drift table** — per task and stack the two groups' CPC, success rate, tokens and time, and every identity that differs between them — and are **never pooled**. **The ten-task bare headline (§9) is B's**; A licenses its four per-task lines and the olympiad per-kind line and is reported as the pre-registered replication of those four. **Never pooled:** (1) A's and B's sessions of one task into one CPC, success rate or time figure; (2) bare and process sessions; (3) sessions under different model ids; (4) sessions under different CLI copies or price tables without the per-version line; (5) A's per-task ratios into B's ten-task median; (6) pilot, A/A, smoke and anchor sessions with data. The drift table is a required section of the record (§9, E6) | team lead, 2026-11-01 (B re-runs A's four, the replication role registered at A — which meets coai cadence `f25ad04b`'s condition); the rule: this plan (consult Fable § 2.5, § 3.2) |
| D47 | **One model, one alias baseline and one CLI copy for the series.** The Claude CLI is re-pinned **once, at group A's pre-registration** (D8's re-pin, taken once) and kept through C. Pinned for the series the same way: the toolchain checksums (D6's "latest on 2026-10-31" is fixed for the series; no newer patch between groups), effort `high`, the tool list, `--strict-mcp-config`, a fresh `CLAUDE_CONFIG_DIR`, the bare prompt template (finalised at A, byte-stable for B and C; §4.5), D15's allow-list (frozen at A; a key needed later is added for every task or for none), the cache snapshot hashes (the full set built before A, E1.S4) and the layer identity (D51). **The alias baseline is inherited across groups:** v1's alias store lets the FIRST session of a series set the baseline (`spikes/minigit-bench/lib/alias-store.mjs:1`), so three separate series could each accept a different resolved model and no drift would be reported; v2 keeps **one** baseline, set by **the first session of group A**, and every later session of A, B and C is checked against it — a new id stops the series as v1's alias record does (T-L40; duplicate enrollment T-L41; a resume against the wrong manifest T-L42). A forced change of the CLI or the model is an **instrument amendment**: dated, the A/A re-run, reported per version. What `lib/aliases.mjs` cannot see stays as T8 says: a change behind an unchanged id. Closes Q10 *(2026-11-02, D55:) the allow-list frozen at A includes the two per-case heap keys, and the memory policy's identity (`inputs/specs.d/memory-policy.json`) is pinned for the series beside the layer identity* | team lead, 2026-11-01 (the inherited baseline, on coai cadence `f25ad04b`); the CLI and pins: this plan (consult Fable § 3.1) |
| D48 | **E0 is a prerequisite of group C only.** The parity audit and release (D23, E0) control the process arm (T14); the bare box holds one task folder and no conventions (D11, T-L2). So **E0.S3's pin is a prerequisite of group C's pre-registration only**, not of A's or B's; E0 still runs now, in parallel; E4's dependence on E0.S3 is E4-C's *(Second full reading, 2026-11-01, finding 3:) during groups A and B the task repository's `.agents/conventions` is E1.S1's provisional pin — the then-current `release`, recorded by sha; moving it to the parity release on a snapshot branch (E0.S3) is a prerequisite step of group C, done before any run base is cut.* | this plan (consult Fable § 1.1) |
| D49 | **Interim records, and publication after C.** Each group is analysed as soon as its rounds are scored (E6.S1–S2 per group). Its **interim record** (`research/lang-v2/RESULTS_lang_v2_<group>_<date>.md`, generated) carries **counts and rates only** — no case text, no per-case adjudication narrative, no trap coverage table, no reference. A's blind-adjudication packets and verdicts stay in the private area and are published with the final record. **"Published after the series" (§4.3, §5, T2, E6.S3) means after group C.** `leakcheck` on every push uses **every group's** held-out needles, the frozen groups' included. Sealing is one archive per task (§4.3). The series' headline exists only after C | this plan (consult Fable § 5 item 3; coai cadence `f25ad04b`) |
| D50 | **The runner between A and B: two manifests, three guards.** Group A runs on the runner manifest of E1.S8a parts i and ii-a; part ii-b (networking and lifecycle) grows `runner/` before B, so B has a second manifest. The guards: (1) **an AST and hash test** that the olympiad modules are byte-identical across the two manifests and give identical verdicts and JSON results on the olympiad fixtures (T-L38); (2) **B re-runs the D40 gate and E2.S3's calibration for 01, 02, 03 and 09 on B's identities** before B's pre-registration — no model, no spend; T-L36 marks A's records `stale` for B and the new records are B's; a limit that comes out different is a finding, recorded and resolved before B's tag and never applied to A's sessions; (3) the record's drift table names both manifests. `GRAMMAR.md` stays identical while FORMAT's `common` and `olympiad` sections stay (D45); a FORMAT amendment of a `daemon` or `web` section takes the same guard. The other choice was option (i): build part ii-b before A, one runner identity for the series, at about a day of A's time **Guard (2) is superseded in part — (Second full reading, 2026-11-01, finding 1, accepted; the team lead's ruling.)** Group B's replication of 01, 02, 03 and 09 runs under **A's registered limits, unchanged**, as D45 says. B's re-run of the D40 gate and of E2.S3's calibration on B's identities is a **check, never a registration**: a limit that comes out different is a **drift finding**, recorded in the drift table (D46) with its cause, and is never registered for those tasks. If the difference means that a reference no longer passes under A's limit, **B's replication of that task is suspended** and named an instrument finding — the task is never re-limited. The words "the new records are B's" hold for the D40 gate records only, never for limits. *(2026-11-02, D59:) `rs_abort.py` and the evidence rule are olympiad-path modules in guard (1): part ii-b inherits them unchanged for daemon and web lifecycles (T-L52), and a change it needs there is an instrument amendment for A; guard (2)'s check runs D58's steps 3 and 5 under A's registered values.* *Superseded in part (2026-11-03, D61):* "`GRAMMAR.md` stays identical while FORMAT's `common` and `olympiad` sections stay" is not true — `GRAMMAR.md` carries FORMAT's whole-file sha256 in its header (E1.S9's extractor) and sections 4 and 5 are `daemon-web` (D60), so every FORMAT edit for B changes it. The grammar is pinned per task: A's four keep A's `GRAMMAR.md` bytes when B replicates them, B's six get B's (D61, T-L57). "A FORMAT amendment of a `daemon` or `web` section takes the same guard" reads "of a `daemon`, `web` or `daemon-web` section"; its guard is T-L56 on A's frozen classification. | team lead, 2026-11-01 (option (ii) of consult Fable § 3.1) |
| D51 | **The execution layer whole before A; the runner's part ii re-cut; the memory view asserted.** **E1.S8b is built whole before group A, the netns holder included:** CP2 fixes the series' verdict machinery once; an olympiad program needs no network and gets the holder's loopback-only namespace without a second code path; and a holder added in B would change the layer identity and re-date every A gate and limit (§6.1). **E1.S8a part ii is re-cut:** **ii-a**, needed for A — `--launch-prefix`, `--launch-status` (consult B5), the pidfd waits and the wall rule (B6), `--case-view` (B7), the cgroup memory read and the kill through `cgroup.kill` (B4), V11, V12; **ii-b**, needed for B — `rs_net`, the daemon and web lifecycle, readiness within T, `signal TERM` and G, V3–V5, V7. **Before E2-A calibrates, E1.S8b's probe ASSERTS** — not only prints — what each runtime believes its memory limit is, **through the final execution prefix at `MemoryMax` 256 MiB and 512 MiB**: whether the cgroup is visible inside the box (the `--tmpfs /` base binds no `/sys/fs/cgroup`), .NET's `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`, Node's `process.constrainedMemory()` and V8's heap limit, against the host's `prog/memory.max`; allocation-churn fixtures with bounded live data complete in both stacks, and over-limit fixtures fail with their memory evidence kept (T-L43). **OP-K (D15: `DOTNET_GCHeapHardLimitPercent`, or a read-only cgroup view in the box) is decided by the team lead on that asserted record before E2-A calibrates** *(2026-11-02:) decided on the asserted record at 100 % (the own-cgroup view, .NET's hard limit = the limit) and re-decided at 90 % for both stacks by change 5.2 — **D55 is the decision**; T-L43 (revised) asserts the settings and the object-graph regression and records the mixed workload* | team lead, 2026-11-01 (on consult Fable § 1.2; the probe from coai risk `19ae7208`) |
| D52 | **Publication and comparison guards.** (1) **The GitHub broker has a host-side publication gate** (E3.S7a): every outgoing object and API payload — commits, commit messages, PR titles and bodies, comments — is scanned on the host before the broker mirrors or posts it, because mirroring and API-created PRs bypass the task repository's client-side hook; tested with that hook disabled: zero outbound publication calls for forbidden content (T-L45). (2) **The run base is bound** (E1.S7): the approved frozen commit is bound to the generated tree and to the final orphan sha; the push check scans **that exact orphan**, proves locally that its parent list is empty, and refuses a build from a wrong commit before any push (T-L46). (3) **The push check's exemptions are exact** (E1.S1): an exemption names an approved value at an approved place — a path, or a reviewed passage pinned by its hash — never a class of text; the rules "a six-word window inside one whitespace token is not a hit" and "any Markdown text published at `origin/main` is public" give way to such entries, because a passage joined with `/` or `-` is one token and published fragments can be recombined (T-L47). Paraphrase defeats six-word matching, so the semantic review of an aid-informed spec stays with its gate and bias review (D25). (4) **V2 never calls `Decimal.normalize()` before equality** (E1.S8a): under the default context it rounds to 28 digits, so two distinct 30-digit values would compare equal (confirmed by the team lead's re-run; the runner does not call it); numbers compare by exact `Decimal` equality only (T-L44) | team lead, 2026-11-01 (coai risks `b3a3e5d6`, `19ae7208`) |
| D53 | **The budget per group: information, no new halt.** The $50 per-session cap (D7) holds for every session of every group; for an olympiad session it is a runaway guard only. **No new halt is added:** the owner removed the total ceiling on purpose, and the exploratory plan's $60 pause point is that plan's rule, not imported here. Per group the plan adds **information**: a projection line in each group's pre-registration and a settled ledger line per group (`A: data / pilot / A/A / void / re-run`, the same for B and C), so the owner sees each group's cost before the next group's paid sessions; the pilot formula's cross-check cap (G-18) is computed per arm per group and reported beside $50 (§12) *(Second full reading, 2026-11-01, finding 4:) group C's ledger categories are `C: data / pilot / A/A / anchor / void / re-run`; the `anchor` line is used only if the owner answers Q-G yes.* | team lead, 2026-11-01 (consult Fable § 4) |
| D54 | **The bare anchor round in group C (the owner's answer to Q-G, 2026-11-01).** One bare round over the ten tasks (n = 1 per task and stack, ≈ 20 sessions), interleaved among C's three process rounds under C's pins, model and CLI copy; never pooled with bare data (D46's never-pool list); reported only as a drift check — the anchor against B per task, beside C's process tables, so the B→C drift that T16 can no longer control is measured, not assumed. Its spend is C's `anchor` ledger line (D53); its sessions carry `role: anchor` in the record; C's pre-registration registers it with its seed and order. | owner, 2026-11-01 |
| D55 | **The memory policy of the series — OP-K closed: policy A at 90 % (changes 5.1 and 5.2; the epoch-6 re-review).** For every case of every task, both stacks, every group: the program runs in its own systemd scope with `MemoryMax` = the registered limit L (D36) and **sees its own cgroup read-only** (`--unshare-cgroup`, `prog/` bound at `/sys/fs/cgroup`, nothing else of the host); .NET gets `DOTNET_GCHeapHardLimit` = **⌊0.9·L⌋ bytes** (hexadecimal — the percent form cannot express the value: `0x64` is ignored by .NET and 75 % stays); Node gets `NODE_OPTIONS=--max-old-space-size=⌊0.9·L / 1 MiB⌋`. The two keys are **per-case scoring-box values the layer derives from L** (`env.mjs`), never typed by hand and never in the agent box, where D15's base stands (`NODE_OPTIONS` empty, no hard limit), **nor in the build box** (`build.sh` runs without them; T-L14). **Why 90 %:** at 100 % the GC's budget equals the cgroup, so the runtime's own memory (code, stacks, GC bookkeeping, off-heap buffers) has no room and the kernel kills before the GC compacts — .NET's mixed workload (live objects 45 % + byte arrays 35 % of L + 1 GiB short-lived) was oom-killed at 256 MiB under A at 100 % and passed at 90 % and 85 % (peaks 238 and 226 MiB); 90 % is kept as the larger of the two that passed (`followUp5`). **Why the flag for Node:** with the view alone V8 sizes its heap to about 268 MiB whatever the cgroup (`heap_size_limit` 281 MB at 512 MiB), so an object graph at 70 % of a 512 MiB limit aborts at 54 % — a false `memory limit` for Node only (`followUp3`); with the flag the graph passes at both limits in both stacks. **Recorded, not tuned around (T21):** Node's typed arrays (`Buffer`, `Uint8Array`) live outside V8's heap, so the mixed workload is oom-killed in Node at 256 and 512 MiB under every setting it was run with (the view alone — policy C — and A at 100, 90 and 85 %; `followUp5`; it was never run with no view); it is a runtime property the record names, and a reference that meets it is calibration's to find (D58). *(2026-11-03, change 6.1:) Node's medium-buffers workload at 256 MiB is the second recorded cell: a kernel `oom_kill` at peak = L under every setting measured; run and recorded, never asserted (T-L43, T21).* The policy is **one identity of the series**: `inputs/specs.d/memory-policy.json` (`cgroup-view-own+gchl-90+node-old-space-90`, decided, by, evidence) is pinned at A, read by the probe (T-L43), the layer (a data run refuses a policy other than the pinned one) and every group's manifest (D45, D47); a change is an instrument amendment (§6.1). Evidence, kept unchanged with its identities: `inputs/opk-experiment-2026-11-02.json` (layer `79b85a37…`, the pre-view box; its `followUp` to `followUp5`) and `inputs/exec-probe-2026-11-02.json` (layer `2697…`, A at 100 %). Supersedes D15's "Node: no `--max-old-space-size` flag; `NODE_OPTIONS` empty" for the scoring box and D15's OP-K line of 2026-11-02 (100 %) | the measurement: the team lead, 2026-11-02; the 90 % after `ask_consultants` `bf619f55` (2 of 2 for A); the shape: this plan (epoch-6 re-review; consult `ee6c5a9a` (a)) |
| D56 | **The `memory limit` verdict has two causes, one rule, and a tested evidence boundary (change 5.1).** A case is `memory limit` when **(i)** the program's cgroup reports `oom_kill ≥ 1` in `prog/memory.events`, whatever the exit status — a kernel kill of the program or of any child; or **(ii)** the PROGRAM ends in its runtime's own out-of-memory abort — exit status 134 **and** .NET's exact stderr line `Out of memory.` (or an `Unhandled exception.` line with `System.OutOfMemoryException` in the tail), or exit status 134 and a V8 line containing `JavaScript heap out of memory` — **with evidence: `memory.peak` ≥ 50 % of `memory.max`**. The record carries `memory: { peakBytes, maxBytes, oomKill, runtimeAbort: "dotnet" \| "v8" \| null }`, and the report counts `memory limit` **by cause** per task, stack and arm (added metrics, D34). **The 50 % threshold is a pre-registered heuristic, not a measurement of the runtimes:** the measured abort points, each with the setting it came from: .NET at 52 % of L (134 of 256 MiB) and 64 % (326 of 512 MiB) with the cgroup view alone (`followUp`, policy 3, .NET capping itself at 75 %); .NET at 77 % (198 of 256 MiB) and 89 % (455 of 512 MiB) with the view and a hard limit equal to L (`followUp2` 3c, `followUp4` 1c; the probe at 100 %: 208 of 268 MB and 477 of 537 MB); V8 at 54 % (280 of 512 MiB) with the view alone (`followUp3`). **None was measured under D55 (90 %):** a .NET abort and a V8 abort near 0.9·L are predictions, so D59's re-run of T-L43 records each over-limit fixture's peak/L per stack and limit under D55, and a ratio within 10 points of 50 % is a finding before CP2. The threshold sits below every measured point so that a genuine abort is not missed; its boundary is tested at the byte (T-L50: L is a whole number of MiB by D58, so L/2 is whole; a peak of L/2 − 1 byte with the line is not evidence, L/2 is). **Below the threshold** an abort with the line is `runtime error` **flagged `abort-below-evidence`** (the line, the status and the peak kept) — never silently a runtime error: the flag is a required column of the record, counted per stack, and such a case goes to the blind adjudication (§4.3) like every failing case; a program that prints the line and exits 1, or exits 134 with another message, is `runtime error` without the `abort-below-evidence` flag; the line under exit 1 (or any non-zero exit other than 134, with no kernel `oom_kill`) carries `child-abort-line`, because the runner cannot tell whether the program or a child printed it. **A runtime abort in a CHILD whose parent exits 0** is not a `memory limit`: the exit status and the stderr line the rule reads are the program's, and the one evidence that crosses processes is the kernel's `oom_kill` (cause i); the case is judged on its output — pass or fail — and **flagged `child-abort-line`** when a runtime's abort line stands in the stderr file beside exit status 0, so adjudication sees it (T-L30). **A non-zero parent exit with no runtime out-of-memory line of the program's own** is `runtime error` — flagged `child-abort-line` when a child's abort line stands in the stderr file — unless the scope saw a kernel `oom_kill`, which makes it `memory limit` by cause (i) whatever the exit status. The heap settings of D55 are per PROCESS (a forked runtime inherits the same keys) while the cgroup bound is per CASE: a program that forks runtimes is bounded by the cgroup, and a kill of any of them is `memory limit` by (i). The rule, its lines and its threshold are the same for both stacks, frozen in each group's pre-registration; a change is a dated amendment applied to both stacks and, after a group's tag, an instrument amendment (§6.1's cohort rule). FORMAT's F-MEMORY sentence states both causes as "the program's memory reaches the bound" in stack-neutral words and names the heap settings of D55 (E1.S9) | this plan (epoch-6 re-review; consult `ee6c5a9a` (d); the evidence rule from the Opus review of fix batch 1, O7) |
| D57 | **Units, and the floor as a measured value — not raised on this evidence.** Every memory figure of the series is in **MiB** (1 MiB = 1,048,576 B): D14's floor is **256 MiB = 268,435,456 B** (`MemoryMax` takes bytes; the probe and the experiment ran at 256 and 512 MiB; "256 MB" in earlier text reads MiB, and §4.1's spec sentence says "at least 256 MiB"); time stays in seconds. The exploratory olympiad pilots judged at 512 MB (`research/lang-v2/EXPLORE_task*.md`): their own calibration, never v2's, and no evidence for the floor. **The floor is not raised on the OP-K data:** the toy workloads are not fixed-size references, and what they show is a runtime property (T21), not a floor. **The floor is settled by one measurement — E2-A step S3.0, the floor probe — before the first registration:** a workload **sized by its PEAK, per stack** (judge J5: D14's 2 × rule is a rule on the peak, and a live set plus churn is stricter than any reference given the floor): at 512 MiB, through the final prefix under D55, each stack's live set is tuned — and the tuning recorded — so that `memory.peak` is **128 MiB ± 8 MiB**, half of 256 MiB, the most a reference's peak may be and still get the floor; two compositions, **objects-only** and objects with a byte-array share of one quarter, each with 1 GiB of short-lived churn; then the same programs run at 256 MiB; three runs per cell, both stacks. **The rule, fixed now:** the floor F is the smaller of 256 and 512 MiB at which the **objects-only** composition passes in both stacks; its failing at 256 MiB and passing at 512 MiB in either stack makes **F = 512 MiB** for every task of the series, written to `inputs/limits-lang.json` as `floorMiB` with the probe record — a registered value by this rule, not a plan change; one that fails at 512 MiB too is a finding for the team lead before any registration (the composition or the policy is then wrong), not a floor. A failure of the byte-array composition alone, in Node, is recorded as a T21 finding (off-heap typed arrays), never as a floor raise. The spec's guarantee "at least 256 MiB" holds under either F (T-L51) | this plan (epoch-6 re-review; consult `ee6c5a9a` (b)) |
| D58 | **The calibration sequence of E2.S3 — a registered limit's evidence is a run under that limit (G-17 widened).** Under D55 a runtime's heap budget follows the limit, so a peak measured under one limit is not the peak under another: the GC lets the heap grow with its budget. So per task, after S1 is final: **(1)** both references run every held-out case under a **recorded provisional limit** L0 = F (D57) with the heap settings derived from L0, three runs under the quiet check; the peaks P_cs and P_ts (`memory.peak`) are recorded as provisional; a reference that fails under L0 repeats the step at L0 = 2·F (recorded; at most twice, then a finding). **(2)** The candidate L1 = max(F, 2 × max(P_cs, P_ts)), rounded up to a whole MiB. **(3)** Both references run again under L1 **and the heap settings derived from L1** (⌊0.9·L1⌋), three runs; both must pass every case; the peaks P′ are recorded. **(4)** If 2 × max(P′) ≤ L1 the limit is registered as L = L1 with the step-3 run as its evidence; else one more iteration with L2 = 2 × max(P′) (rounded up) and its own settings, then registration if it holds — a third iteration is a finding (the task's memory is GC-elastic), resolved by the team lead before registration and recorded. **(5)** The **time limit** is then measured under the registered memory limit and its settings (GC settings change wall time): 3 × the slower reference's wall, floor 1 s, three runs. Every step's limit, settings, peaks and walls are in `inputs/limits-lang.json` (`calibration: [{ step, limitBytes, heapSettings, peaks, walls }]`), so a registered value can be traced to a run under itself; `register-limits` refuses a registration whose evidence run was under another limit or other settings (T-L25 widened, T-L49). B's check of A's four (D50 guard 2) runs steps 3 and 5 under A's registered values, never steps 1–2 | this plan (epoch-6 re-review; consult `ee6c5a9a` (b)) |
| D59 | **Fix forward, re-pin, re-run; the historical evidence kept.** Nothing of E1.S8a or E1.S8b is cut. **E1.S8b** (`feat/lang-v2-e1-s8b`, pushed) is fixed forward to D55 — `env.mjs` derives both stacks' keys at 90 % from L, the policy file names the policy, the probe asserts the settings and runs the fixtures of T-L43 (revised) — and **re-pinned** (a new layer identity). **E1.S8a** (merged, #141, #147) is fixed forward to D56 on E1.S8b's branch — the evidence boundary tests, the flags `abort-below-evidence` and `child-abort-line` — and **re-pinned** (A's runner manifest); **part ii-b inherits the abort rule unchanged** for daemon and web lifecycles (the same `rs_abort.py`, the same evidence from `prog/`; D50's guard 1 names it an olympiad-path module, byte-identical across the manifests — T-L38, T-L52). **No D40 gate, calibration or CP2 has run on any identity yet** (verified 2026-11-02: no `inputs/checkpoints.md`, no `inputs/limits-lang.json`), so nothing is re-dated now and §6.1's rule stands for later. The probe is re-run through the final prefix at both limits under D55 and recorded as a **new dated file** `inputs/exec-probe-<date>[-<n>].json` — a record is never overwritten (T-L53); `inputs/exec-probe-2026-11-02.json` (identity `2697…`, A at 100 %) and `inputs/opk-experiment-2026-11-02.json` (identity `79b85a37…`, the pre-view box, "not re-runnable as written") stay as they are, cited by D55 with their identities. CP2 is recorded after the fix forward, on the re-pinned identities. `research/module_lang_bench.md`'s OP-K paragraph (100 %) and `todo/README.md`'s rows are brought to the merged state by the fix forward's own commit; this revision updates the plans' status claims only. The fix forward is epoch 6's first work (§14 A1), after the resume | this plan (epoch-6 re-review; consult `ee6c5a9a` (c)) |
| D60 | **FORMAT's fifth section class, `daemon-web`, and A's frozen classification (change 6.2).** Every `##` section of FORMAT carries one of five classes: `common` (shared with olympiad tasks, or global), `olympiad`, `daemon`, `web`, and **`daemon-web`** — it serves daemon and web tasks and no olympiad task. In v2, **sections 4 (the scenario script) and 5 (the expected transcript) are `daemon-web`**; every other section stays `common`. *(Superseded plan round `fae12fd9`, 2026-11-04, finding 0: this first summary is read through the per-heading rule below — sections 4 and 5 are `daemon-web` whole, and every other heading has the class of the per-heading list; nothing else in this row classes a section.)* E1.S9's "a section that serves two kinds is `common`" reads: a section that serves olympiad tasks and another kind, or every kind, is `common`. After A's freeze the `common` and `olympiad` sections are immutable for the series; a `daemon`, `web` or `daemon-web` section may be amended before B's freeze (D45). **B's check reads A's frozen classification, never B's:** the protected set is the sections that FORMAT at `spec-frozen-A-…` classes `common` or `olympiad`, matched by heading; B's FORMAT holds each of them byte-equal, under the same heading and with the same class row — so a section cannot leave the set by a relabel in B, and a protected section deleted in B is refused; a section B adds is `daemon`, `web` or `daemon-web` (a new `common` or `olympiad` section would change the olympiad contract after A's freeze, and is refused). **Two parts outside the sections, decided so that the rule can be met at all:** the header rows (`Format version`, `Gate verdict`, `Freeze tag`) are each group's own; the changelog (section 7, `common`) only grows — A's lines are a byte-equal prefix of B's. T-L56 shows the rule on A's real FORMAT with one permitted and two refused changes before A's freeze (D64). Decided before FORMAT's gate round: a class change after the gate changes FORMAT's sha and makes every open spec re-conform. *(Judge J1, 2026-11-03 — superseded in part: "every `##` section carries one class" left daemon-and-web text inside `common` sections, F14 one level down.)* **The class is given per `###` subsection wherever a `##` section mixes kinds**; a `##` section that serves one class keeps one class row. In v2 (the headings of the draft `docs/format-v2` `28b93ba`): **`common`** — 1 (all of 1.1–1.5), 2.1 How they are used, 2.2 Every kind, 3.1 Files and ids, 3.4 Showing samples; **`olympiad`** — 2.3 Olympiad, 2.6 Olympiad output normalisations, 3.2 Olympiad samples, 6.1 Olympiad; **`daemon-web`** — 2.4 Daemon and web, 2.5 The timing contract of daemon and web tasks, 3.3 Daemon and web samples, 6.2 Daemon, 6.3 Web, and sections 4 and 5 whole; 7 Changelog is `common` and append-only (below). The unit of the check is the classed heading — a `##` with one row, or a `###` under a mixed `##` — matched against A's frozen classification. *(Judge J5, 2026-11-03 — superseded in part: "two parts outside the sections" was wrong for the changelog, which is section 7.)* The check treats three places apart: (a) **the changelog**, section 7 (`common`), is append-only — A's lines are a byte-equal prefix of B's, a changed or removed A line is refused; (b) **the preamble** (the lines before `## 1.`, with the class table) is protected: byte-equal to A's except the header rows `Format version`, `Gate verdict` and `Freeze tag`, which are each group's own, and except class rows that B appends after A's rows for subsections B adds; (c) **headings are matched by the whole heading line, number and text, and the numbers of A's headings may not change**: protected text cites FORMAT's own numbers (2.1's table names "the closed list of 2.6"; 4.2 names "bounds in 4.6"), so a renumbered A heading would make a frozen citation false. So B adds no `##` section (the preamble's protected sentence names sections 1 to 7); B adds a `###` subsection only inside a `daemon-web` section or at the end of a mixed `##` section, with the next free number, classed `daemon`, `web` or `daemon-web`; a section inserted before `## 7.` renumbers an A heading and is refused (it reads as a protected heading deleted). (d) **2.1's table of fixed sentences** (`common`) has rows for every kind: a row whose sentence stands in a `daemon-web` subsection may change with that sentence before B's freeze (its id, kinds, place and slots); every other row is byte-equal to A's — found by the epoch-7 plan writer after J1, 2026-11-04. The cases are T-L56's | the class: the team lead, 2026-11-03, after `ask_consultants` `8b776383` (2 of 2 for it) on CP1's F14; A's frozen classification, the header rows and the changelog: this plan (epoch-7 re-review) |
| D61 | **The grammar is pinned per task, not per series.** `runner/GRAMMAR.md` is FORMAT's sections 3–5 under a header that names the format version and FORMAT's whole-file sha256 (E1.S9's extractor), so every FORMAT edit — one changelog line included — changes it, and sections 4 and 5 may change for B (D60). So a task's workspace carries the `GRAMMAR.md` of the FORMAT its group froze it under: **A's four tasks keep A's `GRAMMAR.md` bytes when B replicates them** (their workspaces are not regenerated in B, E1.S3; T-L39), and **B's six tasks get B's**. `inputs/specs.d/format.json` records per group `{ version, sha256, gate, grammarSha256, tasks }` (`format: { A: …, B: … }`); each task's row names the group whose FORMAT and grammar it carries; each group manifest pins the grammar hash per task (D45). The header stays as E1.S9 writes it: it names the bytes the grammar was cut from. An agent in B's replication of 01, 02, 03 or 09 reads what A's agent read (T-L57). Supersedes D50's sentence "`GRAMMAR.md` stays identical while FORMAT's `common` and `olympiad` sections stay". *(plan round `fae12fd9`, 2026-11-04, finding 4):* `grammarSha256` is the sha256 of the `GRAMMAR.md` file's own bytes, its header included; FORMAT's `sha256` is over FORMAT's whole file. A workspace's `GRAMMAR.md` is compared with the `grammarSha256` of the group its task row names, never with the other group's; B's `format.json` holds both groups' rows, and a different version or header between A's and B's rows is expected, not a failure (T-L57) | this plan (epoch-7 re-review) |
| D62 | **CP1's contract-level findings, settled in this baseline** — they are FORMAT text that every spec copies word for word, so they are settled before FORMAT's gate round (E1.S9) and no spec re-conforms after it. **(F5) F-STDERR** says two things apart: the *content* of standard error is never compared and may hold anything; the *resource stop* — a test in which the program has written more than 256 MiB to standard error is stopped and fails (the runner checks `>` 256 MiB: `spikes/lang-bench/runner/rs_pump.py:156-158`, the constant at `runner/rs_exec.py:21`); 4.6 states the same bound beside the 16 MiB that is kept. **(F7) F-ENV and 4.2** say that `TZ` is `UTC` in the base environment and that an `env` line may add a variable or replace `TZ`, and may not set `LANG`, `LC_ALL`, `HOME`, `TMPDIR`, `PATH` or any documented runtime setting of either stack, in any spelling of its case (the layer: `spikes/lang-bench/lib/exec-layer/env.mjs:17` `TZ: 'UTC'`, `:69-82` the refusal, only `TZ` replaceable); the stack keys are named only as "the documented runtime settings of each stack", because FORMAT names no stack word (E1.S9's check). *(Judge J2, 2026-11-03 — superseded in part: "any documented runtime setting of either stack" is wider than the layer refuses.)* F-ENV and 4.2 say exactly what `boxEnv` refuses (`lib/exec-layer/env.mjs:34` the allow-list, `:69-82` the refusal, `:101` the heap-limit keys): an `env` line may add a variable or replace `TZ`; it may not set any other variable of the base environment, in any spelling of its case, nor the per-case setting by which the test machine caps a runtime's heap — in stack-neutral words, with no stack key named. The layer is not changed. **The conformance check's part (E1.S8):** it reads each sample's `env` lines and refuses a key the layer would refuse, from the layer's own lists (`ALLOW_LIST`, `CASE_KEYS`, imported from `env.mjs`, never retyped), so a sample that the layer would stop is found before the spec's gate; a key outside those lists passes both (T-L58). **(F6)** the gated text says `Format version: 2`; the drafts 2.1 and 2.2 stay as changelog lines; `format.json`'s `version` is `2`. **Not plan changes:** CP1's F1–F4 are FORMAT text fixes that make FORMAT say what the pinned runner does — a fix that satisfies the plan as written; E1.S9 lists them as required before its gate, each with a changelog line, the runner and its pin unchanged. F8 and F9 before A's freeze; F10–F13 in part ii-b or E1.S8, as CP1 says. T-L58 ties the figures to the code | the team lead, 2026-11-03 (F6 decided at CP1); the rest: this plan (epoch-7 re-review) |
| D63 | **CP2's conditions, each with an owning story and a blocking milestone** (`spikes/lang-bench/inputs/checkpoints.md`, CP2). **(1)** The D40 gate calls the layer with `data: true` and a named stack, and records `treeSha256`, the memory policy and the stack beside the runner's JSON — **E1.S2a's pre-freeze tool** (`prefreeze.mjs`, its gate record); blocks the first D40 gate (§14 A1, task 01). **(2)** The runner manifest is checked against the pin before the first case of every gate run — **E1.S2a's tool**, in the gate harness, never in `runCases` (that would change the layer identity after CP2); the record's `runnerManifest` is the checked hash, never the version string; blocks the first D40 gate. **(3)** A gate's exit 3 (the holder or the launch) is re-run and never counted as a verdict, and a `fork` flag alone never fails a case — **E1.S2a's tool** (each exit-3 attempt kept as `environment` with its re-run beside it); E2.S3's calibration runs and E3.S3's judge read the same rule when they are built; blocks the first D40 gate. **(4)** The test-leftovers follow-up S1 (`todo/PLAN_bench_test_leftovers_followups.md`) changes nothing under `lib/exec-layer/` or in the identity's files, or the probe is re-run and a CP2 addendum is recorded — **E1.S8b**, the identity's keeper (`pin-exec-layer.mjs --check` on the commit that lands S1; the addendum if it fails); blocks the first D40 gate. **(5)** CP2's F5 — T-L31's runner `signal TERM` step and its per-stack TERM test — **E1.S8a part ii-b**; blocks B's first D40 gate (§14 B1). **(6)** CP2's F6 — `pids.max` 512 in the program's cgroup (`spikes/lang-bench/lib/exec-layer/scope-entry.sh:27`) is named in **both** places, because a program that meets it fails a test: in FORMAT, the fixed sentence **`F-PROCS`** (every kind, in the same R as F-MEMORY: the program and every process it starts run at most 512 processes and threads together; the same for both stacks), written by E1.S9 before its gate; and in **each group's pre-registration**, `pidsMax: 512` in the group manifest beside the registered limits, read from the layer and equal for both stacks (E4.S3 per group; T-L58) | this plan (epoch-7 re-review; CP2's conditions as recorded) |
| D64 | **Every freeze or immutability rule is shown on real files before its freeze.** Changes 6.1 and 6.2 had one cause: a rule written before the thing it governs had run (T-L43's cell before the probe ran it; D45's classes before FORMAT's sections were classed). So each rule that freezes something or makes it immutable — the spec freeze (D21, T-L20), FORMAT's classes (D45, D60), the append-only tags and A's resolved artefacts (D45, T-L39, T-L48), the runner manifests (D50, T-L38), the layer identity (§6.1, T-L36), the memory policy (D55) — is shown with **one permitted and one refused change against the real files it governs** before the freeze it guards: a named test, or, where the files exist only at the freeze, a recorded dry run read at CP4 (CP4-B in B). A rule without its pair is not relied on at that freeze. T-L56 is the pair for D45's FORMAT half. *(Judge J3, 2026-11-03 — superseded in part: "is not relied on at that freeze" reads, as E1 §3 says: **a freeze rule without its permitted-and-refused pair blocks the freeze commit**.)* *(Judge J4, 2026-11-03:)* each rule's pair, owner and milestone — **the spec freeze** (D21): T-L20 — refused, a session whose spec blob differs from the frozen tag; permitted, a run base that differs from the tag only outside the spec's blob (a workspace's stack files) — the guard half by E1.S1/E1.S3 (E1's done-when), the launch half by E3.S1; read at CP4 before `spec-frozen-A-…`, on A's real specs. **The tags and A's resolved artefacts** (D45): T-L39 and T-L48 — refused, a planted edit to an A spec, sample or protected FORMAT text at the B tag; permitted, a B-only spec frozen at the B tag beside A's unchanged four — E1.S2 (the freeze) and E3.S1 (the group manifest); read at CP4-B before `spec-frozen-B-…`. **The runner manifests** (D50): T-L38 — refused, a planted change in an olympiad module; permitted, a change in `rs_net` alone — E1.S8a part ii-b; before B's first D40 gate. **The layer identity** (§6.1): T-L36 — refused, a changed identity marks the gate records and limits `stale` and `register-limits` and `run` refuse; permitted, a change to a file outside the identity's file list (a test file) leaves every record valid — E1.S2a (`gate-staleness.mjs`, the gate half) and E2.S3 (the limits half); read at CP4 before A's freeze. **The memory policy** (D55): refused, a data run under a policy name other than the pinned one is exit 3 `memory-view-mismatch`; permitted, a data run under the pinned policy at another limit L passes with the keys derived from that L — E1.S8b (built, T-L43's record); read at CP4 before A's freeze. **FORMAT's classes** (D45, D60): T-L56 — E1.S9; shown on the draft before S9's gate round, on A's real FORMAT at CP4 and at B's tag at CP4-B. *(plan round `fae12fd9`, 2026-11-04, finding 1):* **each freeze waits for its pairs, present and green:** `spec-frozen-A-…` is created only after T-L20, T-L56 and the dry runs of T-L36 and D55 are green at CP4; `spec-frozen-B-…` only after T-L39, T-L48 and T-L56 are green at CP4-B; B's first D40 gate only after T-L38. A pair that is missing or red at its milestone blocks that freeze or gate *(2026-11-05, D67, found by the epoch-8 audit — superseded in part: T-L20's launch half is built by E3.S1 in A3, after A's freeze, so it cannot be green at CP4; and the words above say "the dry runs of T-L36", whose limits half has no code until E2.S3 in A2:)* at CP4 the pairs are T-L20's guard half (`specBlobMatches`, E1.S2, on A's real specs), T-L36's dry run on its gate half (`gate-staleness.mjs`, E1.S2a), T-L56 and the dry run of D55; **T-L20's launch half blocks `prereg/lang-v2-A-…`, and T-L36's limits half blocks E2-A's first `register-limits`**; the freeze rule of the build contract (D65) has its pair too, T-L60 | this plan (epoch-7 re-review) |
| D65 | **The submission and build contract (changes 7.1 and 7.2; the epoch-8 re-review).** **What is scored is what the agent submitted, as submitted.** The scorer takes the submitted tree: in the bare arm the workspace as the session left it; in the process arm the stack's task folder at the run base tip or the PR head (§4.2). It leaves out the stack's folders at any depth, by v1's per-stack list (`excludedDirs`, `spikes/minigit-bench/lib/score-deps.mjs:10`; `lib/workspace.mjs:38-39`): **in C#, every folder named `bin`, `obj` or `node_modules`; in Node, every folder named `node_modules` and no other**, because a Node tree keeps a real `bin/` of source (all three names for both stacks fail an honest Node agent with `src/bin/util.ts`: measured by the judge, exit 2, TS2307, every case). What is true on each stack: C#'s `Directory.Build.props` leaves the same three names out of its compile, so a build in place and on the copy agree for sources, while a file in such a folder that the program reads at run time is lost on the copy (reasoned, not measured). Node's rule covers the root `node_modules`, which `npm ci` remakes from the lock, and any nested one, such as a hand-made `src/node_modules/foo`, which nothing remakes (E1.S3's known limit: exit 0 in place, TS2307 on the copy). So **each stack's frozen `build.sh` comment names the folders its copy drops** (E1.S3b), as F-OFFLINE cannot: FORMAT names no stack word. It **restores every frozen file** from the task's workspace at its group's workspace commit (D67 (3); verified against `inputs/hashes-lang.txt`, T-L1), so a frozen file that the session or a script changed, removed or replaced with a link cannot help; `samples/` and `runner/` are replaced whole. Then it runs the frozen `build.sh` once, in the build box of `lib/workspace-box.mjs` (no heap key, T-L14), over the **registered snapshot, read-only, never the session's own cache**: an agent can write its own cache (v1 scores from `layout.nuget` and `layout.npm`, `spikes/minigit-bench/lib/bare-series.mjs:115`, and these are writable binds, `spikes/minigit-bench/lib/sandbox.mjs`), so §4.3's "offline from the session's own caches" is superseded for v2. **A failed build is classified.** `build.sh` exiting nonzero, or the 600 s bound (F-BUILD), is the submission's result. A box that cannot start, a holder that fails, a registered snapshot that is missing or does not match its hash, and a host fault are **environment**: `verdict: none, reason: environment`, a bounded re-run at most twice, each attempt recorded, never a rejection and never a selective re-run of a submission. The record is D63's: `attempts` (`[{ exit, class }]`, T-L59), in the gate record and in `score.json`. **The scorer does NOT regenerate `App.csproj` or `package.json`, and it refuses no project edit.** This replaces the paragraph "Input for E3's plan round" and the "rule for E3's scorer" in `research/module_lang_bench.md` on `feat/lang-v2-e1-s3`, which said to regenerate. Why: regeneration can fail a correct program that needs `AllowUnsafeBlocks`, an embedded resource or another legitimate item (reasoned, not measured); rebuilding `package.json` against a kept lock file can make `npm ci` reject a valid submission (consult `d2e1e648`); and a refusal punishes an edit nobody told the agent was illegal. **The contract's bytes stand here, and E1.S3 cites this row, so a build flag is plan text in one place only.** C# `build.sh`: `dotnet build App.csproj -c Release --nologo -m:4 --no-incremental -p:TreatWarningsAsErrors=false "-p:WarningsAsErrors=nullable%3BNU1603%3BNU1605%3BSYSLIB0011" -p:Nullable=enable -p:WarningsNotAsErrors= -p:WarningLevel=10 "-p:NoWarn=1701%3B1702"`. Node `build.sh`: `npm ci --offline`, then `npx --offline tsc --noEmit -p tsconfig.json`. `run.sh` ends with `exec dotnet bin/Release/net10.0/App.dll` or `exec node src/main.ts` (D38). **Frozen** (F-FROZEN's list; OP-4 of E1.S3, which this row now owns): `STATEMENT.md`, `samples/`, `runner/`, `build.sh`, `run.sh`, `PACKAGES.md`, and the pin files `global.json`, `nuget.config`, `Directory.Build.props` (C#) and `.npmrc`, `tsconfig.json`, `eslint.config.mjs` (Node). **The agent may edit** `App.csproj`, `package.json` and `package-lock.json`, because it may add a listed package. Tests go in `tests/`, outside the build (C#: `<DefaultItemExcludes>`; Node: `tsconfig.json` includes `src/**/*.ts` only). **Every deviation is recorded, reported per stack, and is never a verdict.** A pure function over the submitted tree against the workspace lists: (a) project-file content other than package items (`App.csproj` apart from `<PackageReference Include Version>`; `package.json` outside `dependencies` and `devDependencies`); (b) a package item that is not an exact version of the registered set (`file:`, `link:`, a tarball, a range): measured, a `typescript` item at `file:./faketsc` with a false `tsc` builds `const s: string = null;` to exit 0, with `ignore-scripts` on, the Node peer of a `<Target>` in `App.csproj`; and **a `package-lock.json` with any `resolved` that is not the registry, or any integrity outside the registered set**: measured by the judge, `package.json` keeps `"typescript": "6.0.3"`, the lock's `resolved` is `file:faketsc`, and `npm ci --offline` and `npx tsc` pass on `const s: string = null;`. The function runs `lockProblems` (`lib/workspace-build.mjs:37` on `feat/lang-v2-e1-s3`) and a `resolved` check of its own, since `lockProblems` skips `link: true` entries and never reads `resolved`. This row is recorded and read by hand, like the others, never a verdict. An added package item at an exact registered version is **not** a deviation; (c) new configuration files outside `tests/`, at any depth (`Directory.Build.*`, `*.user`, `*.props`, `*.targets`, `*.rsp`, `.editorconfig`, `.globalconfig`, and `runtimeconfig.template.json`, the last from knowledge, not measured; Node: `.npmrc`, `tsconfig*.json`, `eslint.config.*`, `.eslintrc*`); (d) source-level suppressions, counted per stack (C#: `#nullable disable`, `#pragma warning disable`, `[SuppressMessage]`, `dynamic`, `*.g.cs` files, `<auto-generated/>` markers; Node: `@ts-ignore`, `@ts-expect-error`, `@ts-nocheck`, `any`, `eslint-disable`; both stacks, by one rule: a postfix `!`, `null!` included), a lexical count whose limit the record states; (e) frozen files that the restore had to change, and symbolic links in the tree. No walker follows a link to a folder; a cyclic or dangling link in the submitted tree is a deviation under (e), never an error of the scorer. The result is the metric `deviations`, reported beside `accepted`, like the compliance score, in the record of every session of both arms, and added to each group's pre-registration before any paid session. Append only (D34). **A plain install writes an exact version.** The Node `.npmrc` also sets `save-exact=true`, measured 2026-11-05: without it, plain `npm install --offline pino` writes `"pino": "^10.4.0"`, and even `fastify@5.12.7` writes `^5.12.7`, so (b) would count a range on nearly every honest Node session and skew the per-stack number against Node; with it, the item is `"pino": "10.4.0"`, build exit 0. A range stays in (b), since an agent can still write one by hand. That `dotnet add package` writes an exact version is from knowledge, not measured: T-L61 measures it. **Scripts cannot change the build.** The Node `.npmrc` sets `ignore-scripts=true`, by this measurement (2026-11-05, the build box, a read-only copy of `feat/lang-v2-e1-s3` at `6f768772`, load 2 to 5, the copy removed afterwards): with the current `.npmrc`, a `preinstall` that runs `sed` on `tsconfig.json`, and `const s: string = null;`, the build exits 0 and the file is rewritten during `npm ci`, **although the frozen file was in place when the build started, so restoring before the build does not stop this case**; with `ignore-scripts=true`, the same `sed` as `preinstall`, `install`, `postinstall` or `prepare` exits 2 with TS2322 and the file stays; all three kinds pass `npm ci --offline`, `tsc`, ESLint and `npx --offline`; the whole offered set (10 names, 166 lock entries) has no `hasInstallScript`; a listed package added by `npm install --offline` still builds; `npm test` still runs its own script, and `pretest` does not run (a fact for T15). **The agent can see the contract, in `FORMAT.md`, not in the `build.sh` comment:** F-OFFLINE (2.2, `common`) says, in words the gate may refine: *"The tests run offline. They use a pristine copy of the workspace as you left it: build output and installed packages are removed, and the frozen inputs are put back as they were given. Your other files, the project files included, are built as you left them, by `bash build.sh`. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run."* Why FORMAT: the sentence is stack-neutral and is one text in every `STATEMENT.md`, the file the agent reads first; a comment would be two texts in six templates; and FORMAT needs this amendment anyway. The folder names, which are stack words, stand in the `build.sh` comment. **One amendment, one document gate, before the first D40 gate:** F-OFFLINE; 1.2's tag form, `spec-frozen-<group>-<yyyymmdd>` (D45); **256 MiB** in F-LIMITS-O and F-LIMITS-D, which still say `256 MB` against D57; and 1.2's `Freeze rule` row, to *"frozen: changes only by a dated amendment"* (§17 Q-I, settled). Each with a changelog line; `Format version: 2` stays. **Its pins move with it:** `inputs/specs.d/format.json` (the new sha and grammar hash), `PINNED_TASK_SHA` (`lib/task-repo.mjs:16`) and FORMAT's line in `inputs/hashes-lang.txt`; the gate record appends. **The checker follows:** `lib/conformance-check-structure.mjs:47` then requires the group letter (today `(?:-[AB])?` accepts a tag with none), and the upper-case-only `V<n>` exception of `lib/conformance-check-text.mjs:31-33` goes, as no `v2` stands in the sentence; a lower-case case beside `V12` in `test/conformance-batch3b.test.mjs:22` pins that, and the fixture of `test/conformance-checks.test.mjs:21` follows the new sentence. The real cutoff stays in the operator-facing pre-registration rules (`inputs/FORMAT-MAINTENANCE.md`, D21, D45). All ten draft specs hold the old row and re-conform in their own chains. **Implemented before A's freeze, following D64:** the shared module `spikes/lang-bench/lib/submission.mjs`, owned by the new story **E1.S3b**, with its permitted and refused pairs on the real templates (T-L60, T-L61, T-L62). `spec-frozen-A-…` waits for them green. It exports the frozen list, the copy, the restore and the build; the deviation function is its own file, `lib/deviations.mjs`. **E1.S2a's `prefreeze.mjs`, E2.S1, E2.S3 and E3.S4 and E3.S8 import it**, so the gate, calibration and scoring build one way. What stays in E3: which tree is taken (the `delivered` mark, the PR head), the orchestration, `score.json`, the aggregation and the rendering. **The contract has an identity:** `inputs/specs.d/build-contract.json`, the sha256 over the template trees, `lib/submission.mjs`, `lib/workspace-box.mjs` (the build box's keys and seed script), the value of `NPM_OFFLINE_ENV` in `lib/cache-offline.mjs`, and the registered snapshots' tree hashes. **`lib/deviations.mjs` is outside it, with its own recorded hash** in `inputs/specs.d/deviations.json`, which D45's manifest pins: a fix to a pure counting function changes no build, so it must not make a D40 gate stale; after a group's tag it is a dated amendment, and the kept trees are counted again. **The identity's input is the bench's `templates/*` and the files named here, not the workspaces phase B generates.** Phase B copies those bytes and has its own hash list (T-L1, T-L39), so phase B cannot make a D40 gate record stale. The identity stands in the D40 gate record, `gate-staleness.mjs`, D45's group manifest and §6.1's table; a change before a group's pre-registration re-runs the gates and the calibration it touches, and after it is an instrument amendment (§6.1) *(2026-11-05, the amendment's document gate, task repository PR #3, `4ddb8f80`: the gate refined D65's draft; **FORMAT is the source of the final words**, and the draft above is kept as history. F-OFFLINE reads: "The tests run offline. They use a clean copy of the workspace as you left it: build output and installed packages are removed, and the frozen inputs are put back as they were given. Every other file is yours, including the project files that declare or lock your packages, and is built as you left it, by `bash build.sh`. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run." The `Freeze rule` row reads `this text changes only by a dated amendment from its authors` (not "frozen: changes only by a dated amendment"), and the `Freeze tag` value is `` `spec-frozen-<group>-<yyyymmdd>` ``, `<group>` being `A` or `B`. F-FROZEN's first sentence now ends "`PACKAGES.md` and the toolchain pin files named in `build.sh`." FORMAT's sha256 is `06c36608…`, the version stays 2, the grammar's body is unchanged; the pins and the checker followed in the bench (`feat/lang-v2-format-amend-pins`).)* | the contract: the team lead, 2026-11-05 (epoch-8 re-review; consult `d2e1e648`; consultants `ae1528d1` on the command line); the snapshot, the identity, deviation (b) and the `dynamic` count: this plan; the per-stack folders, the lock row, the identity's edges and the lists' additions: the team lead, 2026-11-05, on the judge's measurements |
| D66 | **One acceptance policy across arms.** The frozen build is the acceptance build for both arms and both stacks, and it is the C# policy of change 7.2: `nullable`, `NU1603`, `NU1605` and `SYSLIB0011` fail the build; other warnings are shown and the build passes; Node's peer is `tsc` under `strict` (D65's lines). **Why:** F-BUILD fails every test on a nonzero build, so a broader policy on one stack is a treatment difference, not a quality measure: the same slip (an unused local) must not cost one stack the whole task. **The long command line protects against an accidental override; it is not the enforcement boundary** (21 measured rows, `research/module_lang_bench.md` § Workspaces on `feat/lang-v2-e1-s3`: 10 are stopped, 11 are not, and of the 10 four are stopped only in the form measured): what is not stopped is reported by D65's deviations. **The doctrine's `TreatWarningsAsErrors` does not reach the frozen build.** T15 says so, and the process arm's workspace instructions (the generated tree's `.agents/PROJECT.md`, E1.S7) say: *the frozen acceptance build takes precedence; do not change it to meet a stricter rule; the stricter rule is met in CI's strict job.* **Process CI, a process-arm check, symmetric, reported beside `accepted`, never in it (§17 Q-J, settled 2026-11-05: required):** one more required job per stack, `strict · csharp` (`dotnet build App.csproj -c Release --nologo -m:4 --no-incremental -warnaserror`, the exact form fixed by E1.S7; the judge measured `-warnaserror` on the hello programs, exit 0 in all three kinds) and `strict · node` (`npx --offline eslint src`, the frozen type-aware config; all three kinds exit 0 on a clean tree, measured 2026-11-05). Required, because the family's process merges only on green and T15 names warnings as errors as part of the bundle: **if these checks left, the treatment would be narrowed** (consult `d2e1e648`). A session's `accepted` never reads them; a session that does not merge is scored on its PR head, as §4.2 says. The result is one new column, `strict job`, beside the 18 compliance items; **the score k/18 does not change** (§9's prediction 6 stands). **What group C must build:** E1.S7 the two jobs and the instruction sentence, and the checks in `template-frozen`; E3.S8 reads the jobs' result per head; E3.S9's "planted lint or analyzer violation red per stack" is read on the strict job, with the same tree green in the frozen build; E6's renderer gets the column (T-L26). No template byte changes for this, so nothing of A or B waits for it. **D6's "at the same strength as the C# analyzers" is corrected:** the TS agents have **type-aware lint available**. No evidence shows equal strength, and the two differ in shape: the C# analyzers run inside every build, and ESLint runs only when it is called. The record reports the actual invocations and outcomes per stack and arm (from the stream, added to the classifier's literals; T-L15, T-L65). The bare agent has no instruction about either, and what it runs is what is reported | the policy: the team lead, 2026-11-05 (change 7.2; consultants `ae1528d1`, 2 of 2; consult `d2e1e648` items 1 and 2); the CI job and the column: this plan; required: the team lead, 2026-11-05 (Q-J) |
| D67 | **What epoch 7 showed, and the audit before A's freeze.** Epochs 2 to 7 each stopped at a second change. This one showed two faults, in plain words. **Fault 1: a contract lived in a brief.** The real C# build line, the frozen list and the `.npmrc` facts were decided in E1.S3's brief (§§ 16 to 19) and not in a decision, so a change of plan text looked like a change of a brief; the team lead first said no plan change was used (entry 7.2's free text). **Fault 2: a consumer was designed after the bytes it reads were frozen.** §14 froze the workspace in A1 and designed the scorer in A3, so the first real question about the scorer ("may it regenerate a project file?") came from a template review. That is the same mistake as change 3.1. **Rule from now on:** (1) a value that an agent or a consumer reads stands in a decision or in a story's acceptance, and a brief cites it; a judge who reads a brief's "decisions" section classes each item as contract or not, and in doubt it is a change. (2) Before a freeze, every consumer of the frozen bytes is named, and either it is built, or the part it reads is shown end to end on a synthetic case (T-L66). **The whole plan was checked for the same two faults on 2026-11-05; §6.2 lists every case found, and what was done.** The ones that need a decision: **(3) the workspace commit.** A's tag holds specs and FORMAT, and S3 phase B writes the workspaces after the tag (E1's W-A5), so T-L39's "A's four workspaces' `GRAMMAR.md` at the two tags" cannot hold, and §5's "the last freeze tag (B's)" does not hold B's six workspaces when E1.S7 cuts a run base from it. Each group manifest (D45) therefore also pins the **workspace commit** (phase B's commit, a descendant of the tag, whose spec blobs equal the tag's) and the hash of `inputs/hashes-lang.txt`. The bare harness fetches at that commit; D65 restores from it; a run base is cut from B's workspace commit, and T-L20 still compares the spec blob with the tag. (4) **The per-case fields.** §4.3 records per case wall, CPU, peak, `memory.events`, the verdict and a descriptive RSS curve; runner 0.2.4 and the layer give no CPU and no RSS (`runner/rs_report.py:251-258`; `grep` over `runner/*.py` and `lib/exec-layer/*.mjs` finds none). Both are built before the first D40 gate, when a re-pin re-dates nothing (E1.S8c, T-L64): CPU from the program's cgroup, and the RSS sampler in the runner, which already loops on `cgroup.procs`, so no layer change is needed. (5) **The stack sentence.** §4.5's C# sentence says "the project files in this folder are pinned", which contradicts OP-4 and D65, and it reaches `STATEMENT.md` at S3 phase B, before E4.S2 registers it. It reads *"Use C# 14 on .NET 10 (SDK 10.0.403 is installed)."*; E4.S2 registers the bytes that phase B wrote and may not change them *(2026-11-07, D74: **(4) is superseded for Stage 1.** `cpuMs` and the RSS curve are follow-up work, not built before the freeze: they decide neither `accepted` nor CPC, and memory is certified by `memory.peak` (D36). (1), (2), (3) and (5) stand; (3)'s workspace commit is F2.S2's. The text above is kept.)* | the two faults: the team lead, 2026-11-05 (consult `d2e1e648` item 3); the audit and (1)–(5): this plan (epoch-8 re-review) |
| D68 | **The merge condition of a v2 story meets load-sensitive tests.** The rule stays: CI's required checks exist and pass for the head sha, and the story's local completion run (`BENCH_REQUIRE_SANDBOX=1`, E1 §3) ends with every test passed. Under a host load from other sessions, named timing tests fail and then pass (`todo/PLAN_bench_test_leftovers_followups.md` S5, 2026-11-04, and 2026-11-05's runs on `feat/lang-v2-e1-s3`, 1910 of 1911 in the sandbox run). The fix is S5, a todo, not a build for this plan (the owner's rule). **Until S5 lands:** a failure of a test **on S5's list** does not block a merge when all of these are in the pull request: its name; `/proc/loadavg` at the run; and the same test, then its whole file, passing in a re-run. The load is recorded and is not a condition, because the evidence does not support one: `The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed` failed in E1.S3's final runs at a load of 4 to 14 on 14 CPUs, and it checks at once that a SIGKILLed child is gone, so it may be a race and not a margin. **Everything else blocks:** a test not on S5's list, a second failure of the re-run, and any failure in a D40 gate (E1 §3's own rule). A test joins S5's list only by a line in that plan with its evidence, never by a pull request's say; each new failure of a listed test adds a dated line there. No test is weakened or skipped to pass. **CP4 and CP4-B read S5's list:** a listed test on the layer's or the runner's verdict path whose cause S5 has not decided is written at that checkpoint as an accepted risk with its reason, or S5's line for it is done first, because a real race there would be hidden in the instrument. **This is not a narrowing:** every test still has to pass, the assertion is unchanged, the list is closed and named, and S5 still decides for each test whether the cause is a margin or a race *(2026-11-05, the Opus judge's finding, consultants `fa11693d`, 2 of 2 — **superseded whole; the text above is kept and does not hold:** a re-run of one test or one file in place of a whole green run lowered the merge condition, and "this is not a narrowing" was wrong.)* **The merge condition: a story's merge needs one whole completion run with every test passed, nothing skipped, and CI's required checks present and passing for the head sha. No test or file re-run replaces it.** Every failure on the way is named and recorded with `/proc/loadavg`, and goes to S5's list by a dated line. **The retry is bounded, not hoping: the same test failing in two consecutive whole runs stops the story, and it is diagnosed before any further run.** The fix may stay small or land in S5, but a later green run does not erase the evidence. A failure in a D40 gate blocks, as E1 §3 says. **CP4 and CP4-B read S5's list:** a listed test on the layer's or the runner's verdict path whose cause S5 has not decided is written at that checkpoint as an accepted risk with its reason, or S5's line for it is done first. **This keeps the old condition and only adds the record and the stop.** The two-run stop applies `common.testing`'s *"**Do not re-run it hoping.** Read what the two runs share"*. A test that fails once and is green in the next whole run is recorded, not yet traced: S5 does the tracing, and that rule's Definition of Done, *"No failure was dismissed as flaky: a test that passes alone and fails in the suite was traced to the state the two runs share"*, is met when it has. **First case:** `The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed` failed in two consecutive whole runs on `feat/lang-v2-e1-s3` (`s3a-j1.txt`, `s3a-j2.txt`, 2026-11-05), so it is diagnosed before #167 merges (§14, A1's step 0) | this plan (epoch-8 re-review; the team lead's question); the rewrite: the team lead, 2026-11-05, on the judge's finding, consultants `fa11693d` |

## 4. Design

*(2026-11-07, epoch 9: kept as the design reference. For Stage 1: §4.1's claim is the six tasks 04, 05, 06, 07, 08 and 10; §4.2's process arm is Stage 2; §4.3's scoring is D71's ten passes and its per-case CPU and RSS curve are follow-up work (D74); §4.4's cells are five pairs per task in one bare arm (D73). The operative part holds where they differ.)*

### 4.1 The ten tasks

K = known (paraphrased with credit), O = original. Times are the operator's guesses for one bare agent session, to be
calibrated by the pilot (D4). The runtime traps each task is designed around are on the test-design side, §4.3 (D25).

| # | K/O | Task | Kind | Guess (bare) |
|---|---|---|---|---|
| 1 | K | *Weird algorithm* (CSES 1068): the Collatz-like sequence of n ≤ 10^6 | olympiad | 2–5 min |
| 2 | K | *Counting rooms* (CSES 1192): connected components in a grid up to 1000 × 1000 | olympiad | 5–10 min |
| 3 | O | *Deep expression evaluator*: input to 2·10^6 chars, nesting depth to 10^6, signed 64-bit wrap-around; the full grammar is published; `MinValue / -1` and the literal 2^63 under unary minus are defined | olympiad | 20–30 min |
| 4 | O | *Rule API*: HTTP; rules call members by name over a supplied registry, with string arguments; a rule token and `GET /rules/<token>` (the spec's design, `0b69a50`) | web API | 20–30 min (flagged likely low by the pilot spec; the pilot calibrates) |
| 5 | K | *Key-value server with expiry* (a paraphrased subset of a well-known in-memory store): a framed TCP protocol, keys with expiry, counters, many clients; the command list is the spec's (`30af434`: nine commands), not this table's | daemon | 25–35 min |
| 6 | K | *RealWorld "Conduit" API subset*: users, JWT, profiles, articles, comments, pagination | web API | 30–45 min |
| 7 | O | *Stream-processor daemon*: an event stream, a sliding window per key, a watermark that expires keys that never return, a cgroup-accounted memory bound (D36), graceful SIGTERM; the spec fixes event-time semantics, ordering, bounds, output cadence and EOF | daemon | 30–45 min |
| 8 | O | *Ledger API with idempotency keys*: money, concurrent retries of the same key | web API | 35–50 min |
| 9 | K | *Werewolf* (IOI 2018): the spec defines an exact stdin/stdout grammar, identical for both stacks, and the agent implements the parser; no adapter or helper code is supplied (D33) | olympiad | 45–60 min |
| 10 | O | *DAG job scheduler daemon*: retries with capped backoff, per-job timeouts, cancellation, one process-wide concurrency limit k over overlapping submitted DAGs, cycle detection, failed-dependency propagation, a deterministic final report; a supplied virtual clock and job fixture for deterministic grading plus a real-time smoke | daemon | 45–60 min |

Sum of the upper guesses: 370 min per stack per bare round; a process round is longer (v1 predicted 2–5 h per process
cell against minutes for bare); the pilot replaces both guesses.

**Spec rules (D4, D19, D21, D25).** One spec per task, `todo/tasks/NN-slug.md` in the task repository, written by the team
lead, stack-neutral: the required behaviour, the input and output contract, **the limits as guarantees** (change 1.2a: the
spec states D14's floors — 1 s of wall time and **at least 256 MiB** per run for olympiad; the **cgroup-accounted memory floor of at
least 256 MiB** for a daemon or web API (the registered floor F is 256 or 512 MiB, settled by the floor probe and its rule, D57) —
and the registration rule, at least 3 × the slower reference's wall and 2 × the larger
reference's peak over all load scenarios; the exact values are registered at calibration, E2.S3, and given to the runner,
never to the author, so a spec never needs an amendment to carry them; *2026-11-02, D57: the unit is MiB — the spec says "at least 256 MiB" and "at least 1 s" — and the floor F the floor probe registers may be 512 MiB with the guarantee still true*), **the timing contract** (readiness T and grace G
— the series defaults 10 s and 5 s or a stated override — and the step deadlines of `FORMAT.md`; change 2.1), **the
semantic bounds** (connections, requests, payload bytes, clock semantics, expiry tolerance), **the protocol matters**
(HTTP statuses, content types and persistent connections for a web task; frame grammar and numeric syntax for a daemon —
these are the spec's, not the plan's), the start contract (`build.sh`, `run.sh` with `exec`, `PORT`), the frozen list,
"hidden tests exist", the visible samples named. A known task is paraphrased: no id, no title, no site, **and no mention
that the task is known**; the constraints may keep the original numbers. It never names a trap or a language specific. The
workspace's `STATEMENT.md` is the spec's text plus the stack sentence (as v1's `{STACK_SENTENCE}`), generated,
byte-identical across the two stacks except that sentence and, for a web task, the framework sentence (§4.5).

**Spec format (change 1.2c, adopted from the pilot spec 01, `dew_flow_ai_language_comparison` `60e468a`), binding for
all ten, and held in one versioned document — `todo/tasks/FORMAT.md` in the task repository (E1.S8), a prerequisite of
every spec (E1.S2): the specs conform to it; its sha256 is frozen with the specs in `inputs/specs.json`; a format change
after the freeze is an amendment of `FORMAT.md`, dated and recorded like a spec amendment, and voids the limits of every
task whose suite it touches (G-17). `FORMAT.md` carries one worked example per kind — the olympiad example from the pilot
spec 01; a daemon scenario over a framed TCP protocol that names no real product (D39: a split frame, pipelined requests, a slow reader — FORMAT's own illustration, public by construction); a web scenario (HTTP concurrency,
two clients racing on one idempotency key) — and defines the scenario grammar of `<id>.in` and the comparison semantics of
`<id>.out` (§4.3). The conventions:** a header table with the rows kind (what the program is: reads stdin once / a long-running server …), gate
verdict, freeze tag, the frozen sentence ("frozen: changes only by a dated amendment before the v2 pre-registration") and
amendments, with the placeholders "not yet gated" and "none yet" until the gate and the tag; every requirement is a
numbered sentence with a **stable id R1 … Rn**, and held-out cases cite these ids (§4.3, E2.S2) — nothing outside them is
tested; **byte-exact output is the series default** (compared byte for byte, nothing normalized; a spec that normalizes
says so in a requirement); the frozen list is written for a stack-neutral spec ("`STATEMENT.md`, `samples/`, `runner/`,
`build.sh`, `run.sh`, `PACKAGES.md` and the toolchain pin files of the stack" — `runner/` by D37); the **600 s build bound** is stated; invalid input
"does not occur in any visible or hidden test; what the program does with it is unspecified and never tested" unless the
task itself defines error behaviour; **no plan story id, epic id or decision id in agent-visible text**; sample bytes are
shown with the `⏎` glyph for the single line feed, and the same cases exist as files (1.2b). An original spec is precise enough that two references written
apart agree on every held-out case (E2.S1). **Gate and freeze:** each spec goes through the coai document gate with
`purposeText` naming who acts on it (the agents under test, in both stacks, and the suite's author), plus a consultant, plus
the bias review of D25; every finding resolved; then the tag `spec-frozen-<date>`; the spec's sha256 in `inputs/specs.json`
here. After the tag the spec changes only by a dated amendment applied to both stacks **before** the pre-registration; after
the pre-registration never. *(2026-11-01, D45:) per group — the tag is the group's `spec-frozen-<group>-<date>`, and "the
pre-registration" is the spec's first one, so a spec frozen for A never changes in B or C. FORMAT's sections are classed
`common`, `olympiad`, `daemon` or `web`; after A's freeze the `common` and `olympiad` sections are immutable for the series;
`inputs/specs.json` records FORMAT's sha per group.* *(2026-11-03, D60, D61:) a fifth class, `daemon-web`, for the sections that
serve daemon and web tasks and no olympiad task (sections 4 and 5); B's check reads A's frozen classification; the grammar is
pinned per task — A's four keep A's `GRAMMAR.md`.* *(Judge J1, 2026-11-03:)* the class is given per `###` subsection where a
`##` section mixes kinds — 2.4, 2.5, 3.3, 6.2, 6.3 are `daemon-web`, 2.3, 2.6, 3.2, 6.1 `olympiad` (D60). *(2026-11-05, D65, §17 Q-I:)* one FORMAT amendment before the first D40 gate carries F-OFFLINE's statement of what is scored, the tag form `spec-frozen-<group>-<yyyymmdd>` in FORMAT 1.2, `256 MiB` in F-LIMITS-O and F-LIMITS-D, and the frozen sentence quoted above without the words "before the v2 pre-registration" (Q-I, settled); the text above is kept as the plan's history.

### 4.2 The two arms (D20; v1 §4.2 reused)

| | BARE | PROCESS |
|---|---|---|
| Where | a copy of `csharp/<task>/` or `node/<task>/` at the pinned sha, in an empty box dir | a fresh single-branch clone of `run/<rid>/main` in the public task repository (v1 §4.2, §6) |
| Instructions | none; the task prompt | the repository's `AGENTS.md` → ENTRY → the pinned conventions (D23); all hooks |
| Who works | Opus, effort `high` | an Opus team lead; Sonnet coders, Opus judges, Fable cornerstone specs as the subagent-model rule says; effort `high` pinned for every model (v1 D17) |
| MCP | none (`--strict-mcp-config`, empty) | coai in its own bwrap behind the stdio↔socket bridge (v1 §7.2, E2.S3): plan gate, code gate, consultant, `ask_consultants`; `COAI_ON_EXHAUSTED=good_enough` (v1 D5) |
| GitHub | none; `gh` hidden | **no direct egress (D42)**: `git` and `gh` in the box reach a local GitHub broker's door that serves an isolated bare repository per run (the orphan run base and the run's refs only), mirrors the run's branch to GitHub and does the own-PR operations on the agent's behalf with the fine-grained PAT (Contents/PR RW, Actions/Checks/Statuses R; no Workflows, v1 §4.2); no object outside the run base exists to be fetched; redirects outside the broker refused |
| The spec | `STATEMENT.md` in the workspace | `todo/tasks/NN-slug.md`, frozen; the agent writes **its own plan** in `todo/` of its run branch and takes it through the plan gate (D21) |
| Packages and toolchains | the offline caches and pinned toolchains (D5, D6) | the same caches and toolchains, seeded into the box |
| Done when | the session ends | merged into the run base with green required checks, or the session ends; an undelivered session is scored on its PR head and marked so (v1 §5.3) |
| Session cap and wall | from the pilot (D4); wall ≈ 75 min | from the pilot; v1 planned $35 / 240 min for a far smaller task — the pilot sets v2's |
| Scored how | the workspace as left (§4.3) | the run base tip as left (or the PR head); plus the 17-item compliance score of v1 §5.4, recorded, not part of `accepted` |

Templates, run bases, rulesets, CI, PATs and the deploy key follow v1 §6 with these changes: there is one repository with
two stack folders, so the per-stack template difference is the folder the prompt names and the stack's pin files; the CI
workflow has one job per stack filtered by path; the required check for a run base is the stack's `build · test` job and
the family checks; the acceptance job (the visible suite) is not required, for v1's reason (several PRs allowed). The
conventions pin is the parity release (D23), on a snapshot branch as v1's D9 so `pin-check` stays green while `release`
moves. v1's 17 compliance items apply unchanged (plan file, rules read, worktree, plan gate before code gate, every finding
resolved, Sonnet coder and Opus judge children, test-first, CI, docs, PR, 300 s, inputs unchanged, pin unchanged), plus
one v2 item: **the frozen spec unchanged** (its blob equals the tag's).

*(2026-11-05, D65, D66:)* "Scored how" in both columns reads D65's contract: the submitted tree, without the stack's excluded folders, the frozen files restored, the frozen `build.sh`; `deviations` beside `accepted` in both arms. The process column adds CI's strict job per stack, required and reported beside `accepted` (a column, not a 19th item). `App.csproj`, `package.json` and `package-lock.json` are the agent's to edit in both arms; "inputs unchanged" and "pin unchanged" among the compliance items read D65's one frozen list.

### 4.3 Shared acceptance suites, scoring per kind, and the test-design side (D13, D14, D22, D25)

**One suite per task, in Python 3 (standard library only, `-I`), owned by the bench.** Why Python and not Node: Node is one
of the two runtimes under test, so a Node harness shares its number model (doubles, `JSON.parse` on 64-bit ids), its string
and Buffer semantics and its event loop with the TypeScript implementation — the exact places several traps live — and a
check written in Node could pass a TS defect the harness itself shares, or encode a JS-shaped expectation a C# program
meets differently. Python is third to both: arbitrary-precision integers (exact 64-bit checks), bytes apart from text,
`socket`, `asyncio`, `http.client`, `signal` and `subprocess` in the standard library, no package to pin, and it is already
v1's held-out runner (`lib/heldout-run.mjs:19`). Its cost: Python's own client speed bounds the load a scenario can apply;
load scenarios are sized so the reference solutions of both stacks pass them with margin (D14), and the limit is on the
program under test, not on the driver. The Python version is pinned by checksum at pre-registration.

**Visible part** (`samples/` in the workspace): a few small clean cases per task that the traps do not reach; the agent may
run them; CI's acceptance job runs them in the process arm. **The format is fixed (change 1.2b): one input file and one
expected-output file per case, `samples/<id>.in` and `samples/<id>.out`**; for an olympiad task `.in` is the program's
stdin and `.out` its expected stdout, bytes exact. For a daemon or web task the same two files hold a **scenario** in the
grammar `FORMAT.md` defines (E1.S8): `.in` is the scenario script — steps in order; named connections (TCP or HTTP clients)
and which step runs on which; synchronisation and barriers (a step that waits for others, a start-together group for a
race); timing (a pause, a deadline per step, the readiness wait); **dynamic-value capture and reuse** (an id, a token or a
timestamp read from a reply, bound to a name, used in later steps); the SIGTERM step and the grace period. `.out` is the
expected transcript, with the **comparison semantics** named per step or field: byte-exact by default; a normalised field
(a captured id, a timestamp within a window, a numeric field with a tolerance the spec names); an **order-insensitive set**
where the spec says order is not defined (a listing endpoint, the replies of concurrent clients). **Held-out part** (outside
git, sealed as v1): the cases and scenarios that decide `accepted`, in the same `<id>.in` / `<id>.out` shape plus a
**`cases.txt` manifest** — one line per case naming the requirement ids (R-ids) it asserts and its kind of comparison. The
Python runner of E2.S2 **reads this format**; it is no longer E2.S2's to decide. Both parts use the same case format and
**the same runner file**: the bench's `run_samples.py` at a pinned version is also in every workspace as `runner/` (D37),
so the agent runs the visible samples the way the harness runs the held-out ones.

All scoring happens on a pristine copy of the result, in a scoring box with no network, offline from the session's own
caches, with temp HOME, as v1. `build.sh` first (bounded, 600 s; a failure fails every case; **the build box gets none of D55's heap keys** — a GC hard limit sized for a case would break `dotnet build`; the probe's own build carries none today, `lib/exec-probe/programs.mjs`; T-L14). *(2026-11-05, D65, superseded in part:)* "a pristine copy of the result" is D65's submitted tree with the frozen files restored, and "offline from the session's own caches" reads "offline from the registered snapshot, read-only": the agent can write the session's own caches. Then by kind:

| Kind | The harness does | A case passes when | Also recorded |
|---|---|---|---|
| Olympiad | runs `run.sh` once per held-out case with the case's stdin, in its own systemd user scope (D36) under the registered time limit and the registered memory limit (`MemoryMax`) | stdout equals the expected bytes **byte for byte** (the series default, change 1.2c; a normalization applies only where a requirement of the spec names it), exit 0, within the time limit, no `oom_kill`, no recognised runtime out-of-memory abort with evidence (D56) and `memory.peak` within the limit | per case: wall, CPU, certified peak memory, `memory.events`, verdict class; RSS every 100 ms as a descriptive curve |
| Daemon | starts `run.sh` (which `exec`s the program, D38) in its own scope with the task's environment; waits for readiness within T; drives scenarios over TCP or stdin with the step deadlines of the scenario; sends SIGTERM; waits G | every scenario reply equals the expected one (byte for byte unless a requirement names another comparison); the process exits 0 within G; no process of the scope is left; `memory.peak` within the cgroup-accounted bound, no `oom_kill` and no recognised runtime out-of-memory abort with evidence (D56) | certified peak memory and `memory.events`, RSS every 1 s descriptive, exit code and time, stdout/stderr tails |
| Web API | as daemon, over HTTP; scenario steps are requests with expected status, the headers the spec names and body; concurrency scenarios send requests from k clients at once | as daemon; concurrent scenarios check the invariant the spec states as a requirement (the per-task invariants are the specs' own text; which scenarios target them is the private aid's, D41) | as daemon, plus the request log |

HTTP statuses, content types, persistent connections, frame grammars and numeric syntax are **spec matters**: the plan
fixes none of them, every spec states its own (spec 04's R12–R15, spec 05's R8–R10), and the runner compares what the
spec names.

**The memory bound is the cgroup-accounted bound, certified by the program's own scope (D36).** The scorer starts each
program in a `systemd-run --user --scope` with `MemoryMax` at the registered limit and `MemorySwapMax=0`, outside `bwrap`,
the driver and the build outside that scope; it verifies that the program and its forked children are in the scope, reads
`memory.peak` and `memory.events` before the scope is removed, and keeps them with the lifecycle evidence. **Certification
is required before launch:** the pre-registration records the certifying host (this host was probed on 2026-10-31 and
can certify); **for a data session a readable `memory.peak` is a precondition** — the runner's probe runs at start and a
host without it gets the named halt `memory-not-certifiable` (exit 3, resumable on a certifying host), so no data
session is ever scored without its memory claim and `accepted` always includes it (D13); CPC denominators are never
touched by certification. **Non-data runs only** (the smoke, the stub fixtures, a pilot the operator allows on a host that
cannot certify — pilot sessions are void by D16) may run without `memory.peak`: the harness then samples RSS (100 ms
olympiad, 1 s daemon and web; descriptive only, never a verdict), records **no memory pass**, and the run carries the flag
`memory bound not certified` and is reported on its functional, lifecycle and stop checks alone. The pre-registration
names the certifying host (host, kernel, cgroup version, the scope probe). E3's acceptance checks the refusal and the flag
(T-L29).

**The memory verdict under the policy (D55, D56; 2026-11-02).** Every program the series judges runs under one policy: its own
cgroup visible read-only, `MemoryMax` = L, .NET's GC hard limit and V8's old space both at ⌊0.9·L⌋ — per-case keys the layer
derives, pinned as an identity beside the layer's. `memory limit` has two causes and one rule: the kernel's `oom_kill` in the
program's cgroup (any process of it), or the program's own out-of-memory abort recognised by its exit status and exact stderr
line **with the evidence `memory.peak` ≥ 50 % of L**; the record names the cause, and the report counts both per task, stack and
arm. An abort below the evidence threshold is `runtime error` flagged `abort-below-evidence`; a runtime's abort line in the
stderr of a program that exited 0 is flagged `child-abort-line`; both flags are counted per stack and read in the blind
adjudication. Node's typed arrays outside V8's heap are a recorded runtime property (T21), not a tuning target. Figures are in
MiB; the floor F (256 or 512 MiB) is registered by the floor probe before the first registration (D57), and every registered
limit's evidence is a run under that limit and its settings (D58).

**Measures per session:** `accepted` (D13); `percent` = cases passed / N (a blocked case counts as not passed); `defects` =
distinct **trap classes** failed, from the trap coverage table (one trap failing in three cases counts once); verdict class
counts. **First-submission** means: what the session left when it ended, scored once; no retry.

**The test-design side (internal, D25, D41).** The operator's trap design aid for the suites — the per-task, per-stack trap
rows that stood here through `b5582d7` — **is no longer in this public plan.** On 2026-10-31 it was moved **verbatim**
into the private area, `~/rsd/lang-v2/test-design/PLAN-trap-table.md` (mode 0600, outside every git tree), sha256
`4acd751a8c0514f8742ad3ecf60eed417a12c53d2eb07e785355b62f2b0a228c`; the content is kept and only its place changed (D1,
D34). Nothing of it is in any spec; every held-out case that catches a trap asserts a behaviour the spec requires. The
authoritative **trap coverage table** (task × stack × trap → the cases that catch it) is sealed with the suites and
published after the series, together with the moved aid. **The per-task riskiest held-out-shaped scenarios** that the
D40 gate runs were moved the same way at the epoch 4 round, into the sibling `PLAN-risky-scenarios.md` (mode 0600,
sha256 `84b692d21199cba3ff2783880fba866985ba930b63380db195dad87601a9c5d6` after the second full reading added the task-1
balance sentence and D40's counts and the ten spec drafts updated the breakdown, 2026-10-31); the public text says only
that the gate runs them. **The merged public history (#124, #127, #128) keeps the earlier
rows: a disclosed residual (T18).** Per-task trap balance is reported after the series (T2); the one per-task balance fact
that stood here moved to the private aid at the second full reading.

**Calibration before any cell (measurement rule 2):** two references per task, one in C#, one in TypeScript, each pass
100 % under the registered limits in the scoring box; planted mutants — one per trap per stack, each a plausible version
of that trap — each fail exactly their registered cases. **Bias review (D25):** each spec and its suite (visible and
held-out, in clear from a scratch dir, through `documentText`) go to the coai document gate plus a consultant with the
question *"does any requirement or check favour or disfavour one of C# / .NET 10 and TypeScript / Node 26 beyond what the
required behaviour needs?"*; every finding resolved; the verdict and the reviewers' notes recorded in `inputs/bias-review/`
(no case text). The vendors' reviewers see held-out cases before sealing; the agents under test never do (no network;
the suite is sealed; Q-C names the residual).

**Adjudication, pre-registered as v1 §5.3:** the primary score is the unrevised one; every failing held-out case is classed
blind (product defect / spec ambiguity / harness defect) in two steps, stack and arm masked in step 1; a case classed
ambiguous yields a revised score beside the primary.

**Per group (D44, D45, D49; 2026-11-01).** Sealing is **one archive per task**, `heldout/suite-<task>.enc`, so sealing B's six
tasks never touches A's sealed bytes. The trap coverage table and the sealed counts are registered per group
(`inputs/heldout-prereg-lang.json` gains a `group` field; the table's hash per group). The same held-out suites of 01, 02, 03
and 09 serve A and B, so a group's interim record carries counts and rates only, A's adjudication packets and verdicts stay
in the private area, and the table, the references and the adjudication detail are published only after group C (D49). A
defect A's rounds find in a frozen spec is not fixed in the spec: blind adjudication classes the case (D45).

### 4.4 Cells, pairs, rounds, order (D9, D10, D27)

- Session = (arm, task, stack, repeat n), n ∈ 1..3; 120 data sessions in 60 pairs. `rid = sha256(seed:<arm>:t<task>:<stack>:r<n>:a<attempt>)[0:6]`,
  distinct across every field (v1's T-R1 widened).
- A pair = C# ∥ TS of one task in one arm, started in one tick, masks `0-5` / `8-13` (`maskFor`, swapped per pair), core 3
  (CPUs 6–7) for the harness, coai and the vendor CLIs, nice 19 as v1. Both sessions of a pair are collected and scored
  before the next pair starts; scoring runs on all CPUs after the cells end.
- Six rounds, arms alternating, the leading arm and the task order per round from the seed (D10); `seed.sha256` committed
  with the pre-registration, `seed.txt` revealed after the A/A and before round 1 (v1's commit-reveal). Every session's
  spawn and exit time is in its record (D27). *(Superseded for the staged series, 2026-11-01: the last item of this list.)*
- The void and re-run rules of v1 (§9) apply per session; a void is re-run once, after the round's own pairs and before the
  next round; a re-run whose partner stands runs alone and is labelled `ran unpaired`. The process arm adopts v1's
  `void: no reviewer` (`call_human` with zero reviewers answering) and keeps `call_human` with ≥ 1 reviewer as a data point.
- The machine-quiet check before every pair; load during a pair is flagged, never a void (v1 E3b.S2).
- **Per group (D44; 2026-11-01).** **A:** 3 bare rounds; a round runs the four tasks once each in a seeded order; pairs
  C# ∥ TS in one tick with swapped masks (`maskFor`); a task's second repeat only after every task of the group ran once.
  **B:** 3 bare rounds over the ten tasks, the same rules. **C:** 3 process rounds over the ten. **One seed per group**,
  committed and revealed per group (`seed-A.sha256` at A's tag, `seed-A.txt` revealed after A's A/A; the same for B and C);
  `rid = sha256(seed:…)` stays distinct because the seeds differ. **`group` is in every session record and in T-L11's list of
  known fields**; `resume` refuses a series directory whose group manifest is not the selected one (T-L42), and a session
  already enrolled is refused a second enrollment (T-L41). The six alternating rounds above are superseded for the staged
  series (D10); the void and re-run rules, the quiet check and the pair rules stand inside each group.

### 4.5 Prompts

One template per arm, byte-identical across stacks except `{STACK}`, `{STACK_FOLDER}` and `{STACK_SENTENCE}`, registered
as `prompts-lang.json` with sha256 per arm, task and stack. Shape (final wording fixed in E4.S2):

**Bare:**

```
Implement the program described in STATEMENT.md in this directory, in {STACK}. {STACK_SENTENCE}
STATEMENT.md, samples/, runner/, build.sh, run.sh, PACKAGES.md and the toolchain pin files of the stack are frozen inputs:
do not change them. The program is built by `bash build.sh` and started by `bash run.sh` exactly as STATEMENT.md says;
`python3 -I runner/run_samples.py samples/` runs the visible samples the way the hidden tests will. Hidden tests will run
it under registered time and memory limits, never below the floors STATEMENT.md guarantees. Only the packages listed in
PACKAGES.md are available;
there is no network. Work autonomously; nobody answers questions. End with a summary of what you built and the checks
you ran.
```

**Process** (v1 §4.3's process prompt, with the task slot):

```
Implement the program specified in todo/tasks/{TASK}.md of this repository, in {STACK}, inside {STACK_FOLDER}/. {STACK_SENTENCE}
The spec, the samples, build.sh, run.sh and PACKAGES.md in that folder are frozen inputs: do not change them. The work is
done when, from a clean checkout of the base branch, `bash build.sh` succeeds and the visible samples pass under the
registered time and memory limits, which the test runner enforces and which are never below the floors the spec
guarantees. Hidden tests will run the program the same way.
Follow this repository's own instructions — its CLAUDE.md, AGENTS.md, .agents/PROJECT.md and the rules they point to — for
how work is done here, in full: loading the rules, your own plan in todo/, the review gate on the plan and on the code,
consultants, subagents, tests first, the research/ documents, the pull request, CI and the review that follows it.
The base branch of this task is the branch checked out when this session starts. Branch from it, open every pull request
against it — never against main — and finish only when your work is merged into it with green CI.
Work autonomously. Nobody answers questions in this session. Where you would ask a person, decide, and record the decision
and its reason in your plan and in the pull request description. If the review gate returns call_human, stop shipping:
leave the pull request open, record why, and end the session.
Stay inside this task: do not read other run/* branches, other pull requests or issues of this repository, and do not use
the web. End with a summary: the pull request(s) and merge commit, the tests you ran and their results, the gate rounds,
and every decision you made.
```

Stack sentences: C# — *"Use C# 14 on .NET 10 (SDK 10.0.403 is installed; the project files in this folder are pinned)."*
TypeScript — *"Use TypeScript 6.0 with `strict`, run by Node 26 with native type stripping (no build step), with `tsc
--noEmit` as the type check."* (6.0.x by the owner's decision of 2026-10-31, D6; the exact patch from E1.S5 is written into
the registered sentence) **Web API tasks add a second varying sentence, the framework sentence**, one per stack with the
same shape: *"Use {FRAMEWORK} ({PACKAGE} {VERSION}, listed in PACKAGES.md) for the HTTP server."* — C#: `ASP.NET Core
Minimal API` (the shared framework of SDK 10.0.403); TypeScript: `Fastify` (`fastify` 5.12.7). So a web workspace's
`STATEMENT.md` differs across stacks in exactly two sentences, both registered in `prompts-lang.json` (E4.S2); every other
task differs in one. *(2026-11-01, D47:) the bare template is finalised in group A's E4.S2 and stays byte-stable for B and
C; the process template is finalised in group C's E4.S2.*

*(2026-11-05, D67 (5), §6.2 rows 5 and 12, superseded in part; the text above kept:)* the C# stack sentence reads *"Use C# 14 on .NET 10 (SDK 10.0.403 is installed)."*: the old words "the project files in this folder are pinned" contradict D65, under which the agent edits `App.csproj`. The stack and framework sentences reach `STATEMENT.md` at S3 phase B, before E4.S2, so E4.S2 registers the bytes phase B wrote and changes none. The process prompt's frozen list ("The spec, the samples, build.sh, run.sh and PACKAGES.md") leaves out `runner/` and the pin files; group C's E4.S2 writes it as F-FROZEN's list, as the bare prompt does.

### 4.6 Stacks and packages (D5, D6)

**Both stacks get first-party or de-facto-standard pieces at the same level of abstraction:**

| Need | C# / .NET 10 | TypeScript / Node 26 |
|---|---|---|
| HTTP routing, binding | ASP.NET Core Minimal API (in the shared framework) | Fastify 5.12.7 |
| Request validation and serialization | Minimal API built-in validation (`AddValidation()`, DataAnnotations, source-generated; verified §2) + System.Text.Json | TypeBox 0.34.x schemas through `@fastify/type-provider-typebox`; Fastify's JSON-schema validation and fast serialization *(Superseded 2026-11-03 by the owner's decision of the epoch-7 re-review, after `ask_consultants` `40498063`, the consultants split 1–1:)* TypeBox 1.1.x schemas (the package `typebox`) through `@fastify/type-provider-typebox` 6.3.0 |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.x | `@fastify/jwt` 10.x |
| Long-running process, lifecycle, graceful stop | Generic Host (`Microsoft.Extensions.Hosting`), `IHostApplicationLifetime` | Fastify lifecycle hooks and `close()`; `process.on('SIGTERM')` for daemons |
| Structured logging | `ILogger` (Microsoft.Extensions.Logging) | pino (Fastify's logger) |
| TCP | `System.Net.Sockets` | `node:net` |
| Tests | xUnit v3 offered (the agent's choice) | `node:test` |

**Why Fastify, and what was rejected (team lead, checked 2026-10-31 on npm):**

| Candidate | Version | Verdict | Reason |
|---|---|---|---|
| **Fastify** | 5.12.7 | **chosen** | Node-native and mature; built-in JSON-schema validation and fast serialization (the counterpart of Minimal API's built-in validation and System.Text.Json); lifecycle hooks and graceful close (the counterpart of `IHostApplicationLifetime`); a structured logger (the counterpart of `ILogger`); first-party JWT and TypeBox plugins |
| Express | 5.3.0 | rejected | no built-in schema validation or serialization: a weaker tool than Minimal API; choosing it would handicap TS |
| NestJS | 12.3.2 | rejected | decorators and a DI framework — the analogue of ASP.NET MVC controllers, not of Minimal API; heavier |
| Hono | 4.13.14 | rejected | modern and light, but multi-runtime with a Node adapter layer; its Node ecosystem for JWT and validation is younger |
| raw `node:http` | — | rejected | would make TS write its own framework while C# gets one |

**Package caches (D5).** One hashed snapshot per stack, made in a box through the proxy from a registered recipe
(v1's `makeSnapshot`), then the registries leave the egress list (D17). The C# recipe restores a project that references the
exact set (`Microsoft.Extensions.Hosting` 10.0.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, `xunit.v3`, the
test SDK pieces; the ASP.NET Core shared framework comes with the SDK) and captures `NUGET_PACKAGES` and the first-run
state; `nuget.config` in every C# workspace points at the offline folder only. The Node recipe fills the npm cache with
`fastify@5.12.7`, `@fastify/jwt@10.2.6`, `@fastify/type-provider-typebox@6.3.0`, `@sinclair/typebox@0.34.52`, `pino` (the
version Fastify 5.12.7 resolves), *(Superseded 2026-11-03 by the owner's decision of the epoch-7 re-review, after `ask_consultants` `40498063`, the consultants split 1–1:)* `typebox@1.1.x` in place of `@sinclair/typebox@0.34.52` — the exact patch resolved once at the snapshot build and pinned in the recipe as `1.1.<p>` (the peer range `^1.1.14` alone would let npm take 1.4.3); `@types/node` for 26.x, `typescript@6.0.x` (the exact patch pinned in E1.S5; 7.0.2 superseded, D6), `typescript-eslint@8.72.x` with type-aware rules, and the lint packages the process arm's
template needs (v1 D4's shape); every Node workspace's `.npmrc` sets `offline=true`. **The exact package set per stack, with
pinned versions and the hashes in the cache manifest, is E1.S4's deliverable** and is pre-registered. `PACKAGES.md` in each
workspace lists it (D12).

*(2026-11-05, changes 7.1 and 7.2, D65:)* every Node workspace's `.npmrc` holds `offline=true`, `update-notifier=false`, `ignore-scripts=true` and `save-exact=true`. The C# set adds `Microsoft.AspNetCore.Mvc.Testing`, which supersedes the C# snapshot. **The offered set is shown to work, not assumed to:** at that supersede (E1.S4b) every offered package is built in each applicable template, one at a time, and a minimal offline test-host example runs in the web kind, `WebApplicationFactory` in C# and Fastify's `inject()` in Node, which needs no package. Where a combination is unsupported — `Microsoft.AspNetCore.Authentication.JwtBearer` in the olympiad kind hits `NU1603` (measured on `feat/lang-v2-e1-s3`: it pulls `Microsoft.Extensions.Logging.Abstractions` 8.0.0, whose dependency is in the cache only at 10.0.14) — the recipe may change so that it works, and if it does not, `PACKAGES.md` says so, derived from the matrix's record and still byte-equal across a stack's kinds (D47) (T-L63).

**Toolchains**, pinned read-only with checksums under `~/rsd/lang-v2/toolchains/` and checked inside the box by the
preflight: .NET SDK 10.0.403 installed from the official install script into that directory (not apt, so an unattended
upgrade cannot move it, as happened to v1 on 2026-10-30), Node 26.13.0 from the official tarball, TypeScript 6.0.x (D6), the
Claude CLI copy (D8), Python 3 by checksum of `/usr/bin/python3` (D22). `global.json` pins `10.0.403` with `rollForward:
disable`; `package.json` `engines.node` pins 26.13.0; `tsconfig.json` is v1's (`strict`, `noEmit`, `erasableSyntaxOnly`,
`verbatimModuleSyntax`, `module nodenext`).

## 5. The public task repository (D2, D11, D21, D31)

*(2026-11-07, epoch 9: kept. Stage 1 freezes the six daemon and web specs under one tag, `spec-frozen-B-<yyyymmdd>` (F2.S2); run bases and the process arm's repository side are Stage 2's.)*

```
dew_flow_ai_language_comparison/              public from day one (D31)
  README.md            what this is; the task list; credits WITH links (CSES, IOI 2018, RealWorld, the RESP spec) — the only place links appear; the Node stack rationale (§4.6)
  LICENSE              the repository's licence (open question Q1)
  AGENTS.md CLAUDE.md .agents/PROJECT.md .agents/conventions   the family's pinned conventions at the parity release (D23), on a snapshot branch; the hooks
  .github/workflows/   ci.yml (one job per stack, path-filtered; acceptance = visible samples, not required), family-checks, pr-title (v1 §6)
  todo/README.md       the index; todo/tasks/FORMAT.md — the versioned spec and sample format (E1.S8); todo/tasks/NN-slug.md — the ten gated, FROZEN specs (D21); the process agent's own plans land in todo/ of its run branch
  research/            stubs (architecture, module_tests) as v1's templates; the references and the trap table arrive here after the series
  tasks/<nn>-<slug>/   samples/ (the visible part of the suite, D22: <id>.in / <id>.out); no NOTES.md (D39: credit is the README's attribution section, design reasons are the bench's internal notes)
  runner/              run_samples.py (the bench's visible-sample runner, Python stdlib, pinned version, hash in the bench's inputs/specs.json — D37) and GRAMMAR.md (FORMAT.md's grammar sections); CI's visible-sample runner is this file
  csharp/<nn>-<slug>/  the C# workspace: STATEMENT.md (generated from the spec + the stack sentence, + the framework sentence for a web task), samples/ (a copy), runner/ (a copy, read-only), PACKAGES.md, build.sh, run.sh (ends with exec, D38), global.json, nuget.config, Directory.Build.props, an empty .csproj skeleton
  node/<nn>-<slug>/    the Node workspace: STATEMENT.md, samples/, runner/, PACKAGES.md, build.sh, run.sh (ends with exec), package.json, package-lock.json, tsconfig.json, .npmrc, eslint.config.mjs
  run/<rid>/main       process-arm run bases: an ORPHAN commit of the generated tree (the frozen tag's tree minus the README's attribution section, no credit file — D39), cut by the deploy key (v1 §6); rulesets `run-main`, `template-frozen`, tag rulesets
```

Rules: no secrets, no transcripts, no held-out case, no reference solution before the series ends (references and the
trap table are published beside the report, as a separate commit, after every data session is scored); a `leakcheck`
with the suites' needles runs on every commit range before it is pushed. The bare harness fetches the repository at the
frozen sha into `~/.cache/lang-v2/<sha>/` and checks every workspace file against `inputs/hashes-lang.txt`, as v1's
`fetchInputs`; the process harness cuts run bases from the same sha. A `workspace-diff` check proves the two stacks' folders
of one task differ only in the stack sentence and the stack's pin files (v1's `template-diff`). The lifecycle checker reads
only `todo/PLAN_*.md`, so `todo/tasks/*.md` needs no status line; each spec carries a header naming its gate verdict, its
freeze tag and the sentence "frozen: changes only by a dated amendment before the v2 pre-registration".
*(2026-11-01, D45:)* the bare harness fetches each group's frozen sha; the process harness of group C cuts its run bases from
the last freeze tag (B's), which holds all ten specs with A's four byte-equal to A's tag (T-L39); the specs' "frozen" sentence
reads per group as D45 says. *(Second full reading, 2026-11-01, finding 3; D48:)* during groups A and B the task
repository's `.agents/conventions` is E1.S1's provisional pin (the then-current `release`, recorded by sha); moving it to the
parity release on a snapshot branch (E0.S3) is a prerequisite step of group C, before any run base is cut. The tree above
names the parity release for that reason; it holds from group C on. *(2026-11-05, D67 (3), superseded in part:)* "each group's frozen sha" is the group's **workspace commit**, phase B's commit after the tag; and a run base is cut from **B's workspace commit**, because B's tag precedes B's six workspaces; T-L20 still compares every spec blob with its tag. The repository's root gets a `.gitignore` by D65's per-stack list: `csharp/**/bin/`, `csharp/**/obj/` and `node_modules/`, never a bare `bin/`, which would keep a Node agent's `src/bin/` out of its commit (OP-6 of E1.S3, on E1.S7's list; it is in the run base, in no stack folder, and the scorer does not read it).

## 6. The harness — `spikes/lang-bench/`, importing `spikes/minigit-bench/`; the boundary with v1

*(2026-11-07, epoch 9: superseded where it plans new scoring code. Stage 1's harness is `spikes/lang-explore/`, which drives `spikes/lang-bench/`'s runner and execution layer (D70). The text below is kept.)*

The precedent is v1 itself: `spikes/minigit-bench/` imported `spikes/policy-ab-quick/lib/*` and changed those files only
through named seams with tests. v2 does the same one level up: **new modules in `spikes/lang-bench/`, imports from
`spikes/minigit-bench/lib/`, and a short list of named widenings of v1 files, each with a test**; never a second copy.

| v1 piece | v2 use |
|---|---|
| The box, hide list, bind list, `agentEnv`, the supervisor | **as is**; `extraRo` binds the task toolchains and Python; `agentEnv({ extra })` adds D15's keys |
| The CONNECT proxy and SNI check | **as is**; `EGRESS_HOSTS.langBare = []` **and `EGRESS_HOSTS.langProcess = []`** (D17, D42): no session reaches any host; the API broker's door and, in the process arm, the GitHub broker's door are the only roads out |
| The credential broker's shape (a host-side door that holds the secret, forwards an allowed request, refuses the rest, meters it) | **reused for the GitHub broker** (D42, E3.S7a): the same door pattern with a run-bound allow-list over refs, PRs, objects and redirects; the PAT never enters the box |
| The broker, `B`, cap refusals | **as is**; the caps per arm come from the pilot |
| `launchSession` | **as is** |
| E/O/T/B, T1–T5, `sessionCost`, `crosscheck`, the alias record (v1 D12) | **as is**; the v2 policy starts from v1's `t3 stop` and is re-decided by the v2 A/A |
| `spansFrom`, `partition`, the categories | **widened:** `build_test` learns `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, `npm test`, the drivers' commands; the process categories (`coai_gate`, `consultant`, `ci_wait`, `sleep300`, `hooks`) as v1 defined them; a test per new literal |
| Held-out sealing, calibration, leakcheck, adjudication, the Python runner | **as is**, pointed at `~/rsd/lang-v2/`; the archive holds ten suites' held-out parts, the trap coverage table and the mutants' ids; the runner gains the driver modes (D22). *(2026-11-01, D49: one sealed archive per task, `heldout/suite-<task>.enc`; the table registered per group)* *(2026-11-05, §6.2 row 7, the words above superseded by D37:)* the sealing, the key and the clear folder are v1's; the runner of a held-out case is `run_samples.py` on the layer, never v1's `runSuite`, and it gains no mode outside part ii-b |
| The scorer pipeline on a pristine copy | **widened:** a `kind` step after the build — the olympiad judge, the daemon driver, the web driver (new `lib/judge-stdio.mjs`, `lib/driver-process.mjs`, `lib/driver-tcp.mjs`, `lib/driver-http.mjs`, `lib/rss-sampler.mjs`, `lib/cgroup-limits.mjs`, each a thin Node wrapper over the Python suite runner) *(2026-11-05, D65:)* the copy, the restore and the build are `lib/submission.mjs`'s, built in E1.S3b and imported; the pipeline's first steps call it and add `deviations` to `score.json` |
| The bare runner: pairs, masks, voids, re-run, resume, records, prereg guard, spend gate | **widened:** an arm and a task dimension and a `single` phase (no barrier, no v2 copy); six alternating rounds *(2026-11-02: three rounds per group under D44, nine in the series plus C's anchor round)*; the schedule drops the v1→v2 rows; the record gains per-kind and per-arm fields; `PREREG_FILES` is the v2 list; the spec-freeze guard. *(2026-11-01, D44, D45: a `group` dimension and the group manifest; three rounds per group, not six alternating; `PREREG_FILES` per group; resume checked against the group's manifest)* |
| The quiet check, CPU sampler, spin probe | **as is** |
| Cache snapshots | **as is**, with the v2 recipes |
| `aaVerdicts` G-1 … G-14 | **widened** with G-15 … G-19 (§11) |
| `research/minigit/tools/` extract, render, replay | **widened** to arms, tasks and kinds; CPC and the per-task ratio tables (E6) |
| Watchdogs | wall per arm from the pilot (bare ≈ 75 min, D4), stall as v1 (45 min, the pilot may change), cost at the derived cap |
| **v1's process-arm design, never built** (coai box + bridge; templates, snapshot branch, rulesets, PATs; process runner; compliance checker; P0 and model smoke) | **built here (D32):** the repository side in E1.S7, the coai box and bridge in E3.S6, the process runner in E3.S7, the compliance checker with the `spec unchanged` item in E3.S8, P0 and the model smoke in E3.S9 — to v1's design, for one repository with two stack folders |

**Boundary with v1** (`common.planning-docs` § *a boundary … on BOTH sides*). By D32 v1 closes after its bare arm, so the
table below is the division; v1's parent plan gets only a status line pointing here (a status update, not a plan change;
a follow-up outside this commit, §16):

| Item | v1 | v2 |
|---|---|---|
| The process arm: coai box and bridge, templates, rulesets, PATs, process runner, compliance checker, P0, smoke | designed (its §4.2, §5.4, §6, §7.2, §8); E2.S3, E2.S4, E3.S1–S3 never built; closed | **built**: E1.S7, E3.S6–S9, to v1's design, plus the `spec unchanged` item |
| The bare runner's pair, masks, voids, re-run, spend gate | built (E3b) | widened with arm, task, rounds (E3.S1) |
| Minigit's inputs, suite, references, fork, the bare record | v1 only | none — disjoint |
| Task specs, shared suites, drivers, judge, CPC analysis, the task repository, the parity audit | none | v2 only — disjoint |

Order: E1.S7 (the repository side), then E3.S6–S9. Launch argv as v1

### 6.1 The shared execution layer — producers and consumers (D43)

One implementation, `spikes/lang-bench/lib/exec-layer/` (E1.S8b), runs every program the series judges: launch of
`run.sh` in a bwrap box with D15's environment and the culture, the program-only systemd scope with `MemoryMax` and
`MemorySwapMax=0`, the private loopback-only network namespace shared with the driver, the readiness deadline T, the step
deadlines, SIGTERM and the grace G, cleanup of the scope and the namespace. Its identity is the sha256 over its tree
(part of the harness identity); every record names it. *(2026-11-02, D55:)* the memory policy file `inputs/specs.d/memory-policy.json` is an identity beside the tree's: a data run refuses a policy other than the pinned one, and a changed policy re-dates evidence exactly as a changed tree does.

| Consumer | When | Uses | Acceptance probes it relies on | What invalidates earlier evidence |
|---|---|---|---|---|
| **E1's freeze** (the D40 gate) | before each spec freezes | the layer with the pinned runner (S8a): both draft references, every visible sample, the riskiest-contract scenarios | T-L33 (netns shared, cgroups separate), T-L34 (sentinel unreadable; scope kill removes descendants, driver survives; identical for both stacks), T-L31 (`exec`), T-L32 (culture) | a change to the layer or the runner **re-runs the D40 gate for every spec frozen on the old one** before the pre-registration; a spec amendment re-runs it for that task |
| **E2's validation and calibration** (S1, S3) | after the freeze | the same layer, the same runner, the held-out suites | the same probes, plus T-L7/T-L25 (the registration order) | a change to the layer **voids every registered limit** (G-17's rule widened: the calibration ran on another environment) and re-runs calibration; a spec or `FORMAT.md` amendment voids that task's |
| **E3's scoring** (S4) | every session | the same layer, orchestrated by the runner's phases; E3 adds only aggregation and records | the same probes, run once on the series host before the A/A (G-15/G-16 read them by hand) | a change to the layer after the pre-registration is an instrument amendment: dated, the A/A re-run (v1's rule); after round 1 it stops the series until the A/A is GO again |
| **Each later group** (B, then C) — added 2026-11-01 (D44, D50) | before that group's pre-registration | the same layer; in B the second runner manifest (part ii-b), and for A's four tasks the D40 gate and E2.S3's calibration re-run on B's identities | T-L36 (A's records `stale` for B), T-L38 (olympiad byte-identity across manifests), T-L39 (A's spec blobs equal across the tags) | the group contract rule below |

**The group contract rule (D44, D45, D50; 2026-11-01).** After a group's pre-registration, a change to a shared artefact —
FORMAT, the runner, the layer, the record shape, the prompt, the environment allow-list — is one of two things, and the
change's record says which: **(a) an instrument amendment** — behaviour on an earlier group's path changes: dated, that
group's A/A re-run, its tasks re-gated and re-calibrated, both identities in the record, the sessions under the two
identities separate cohorts (D46); or **(b) an addition on another kind's path only** — proven by T-L38's byte-identity and
verdict-identity test (for the runner) or by FORMAT's section classes (D45). Part ii-b between A and B is case (b) with D50's
three guards. Nothing in a later group moves an earlier group's tag or replaces its pinned artefacts.

Disjoint: the layer owns how a program runs; the runner owns what is sent and compared; E3 owns which sessions run, in
what order, and what is recorded. A defect found in the layer by any consumer is fixed in the one place and every
consumer's evidence is re-dated by the table's rule. (`claude -p --model opus … --max-budget-usd <cap> --tools
Task,Bash,Read,Edit,Write,Glob,Grep,TodoWrite[,ToolSearch]`), MCP only in the process arm, effort `high` pinned in the
session settings file, fresh `CLAUDE_CONFIG_DIR` per session, `gh` in the box only in the process arm.

*(2026-11-05, D65:)* the build contract's identity (`inputs/specs.d/build-contract.json`) is an identity beside the layer's and the runner's in this table's rule: a change before a group's pre-registration re-runs the D40 gates and the calibration it touches, and after it is an instrument amendment. The same module builds in all three consumers of the first column, so no consumer has a build path of its own.

### 6.2 What is frozen, and who reads it (D67; the audit of 2026-11-05)

Every row is a place where something is fixed before its reader is built, or where a contract stood in a brief. *Checked:* §§ 4 to 16 and the Epics of this plan at `b631c03d`, E1's §§ 3, 5, 9, 11, S2a, S3, S4 and S7, `s3a-brief.md` §§ 15 to 19, `s8-plan.md`, `runner/rs_report.py`, `runner/rs_samples.py`, `runner/run_samples.py`, `lib/exec-layer/run.mjs`, v1's `sandbox.mjs`, `bare-series.mjs`, `prereg-guard.mjs`, and FORMAT at task repository `40e678f`. *Not checked:* a live run of any row's fix; E3's and E4's stories documents, which do not exist yet. Consult `d2e1e648` (item 3) names row 1 and the method; rows 2 to 18 are this audit's or the team lead's; rows 19 and 20 are the Opus judge's, on this revision's own text. "Fixed" means fixed in this revision's text; the code comes after epoch 8 opens.

| # | What is fixed, and where | Its reader | What was not defined | What was done |
|---|---|---|---|---|
| 1 | The workspace's bytes (E1.S3, A1) | the scorer (E3.S4, A3) | what is scored; how an edit is treated | D65; the module before A's freeze (E1.S3b) |
| 2 | The frozen build line (E1.S3) | the process agent's rules (E0), CI (E1.S7, group C) | which policy wins; lint's place | D66; `strict` jobs; no template byte needs to change for them |
| 3 | The freeze tag (A1, B1), while the workspaces come after it (W-A5, W-B3) | the bare harness, D65's restore, T-L39, T-L57, E1.S7's run base | no name for the commit that holds the workspaces; B's tag has none of B's six | D67 (3), D45's manifest, §5, T-L67 |
| 4 | The runner's and the layer's record (pinned at CP2) | E3.S3, E3.S4, §4.3's "Also recorded", G-16 | per-case CPU and the RSS curve have no source | D67 (4): named before the first D40 gate (E1.S8c, T-L64). CPU: the runner reads `cpu.stat` beside `memory.peak`. RSS: a sampler in the runner, which S8c re-pins anyway and which already loops on `cgroup.procs` (`runner/rs_pump.py:169`, `rs_track.py`), so no layer change is needed |
| 5 | The stack sentence (§4.5, written into `STATEMENT.md` at phase B) | E4.S2, which "finalises" it later; the agent | it contradicts D65 ("the project files … are pinned"); it is frozen before its registrar runs | D67 (5) |
| 6 | D64's rule: `spec-frozen-A-…` waits for T-L20 and "the dry runs of T-L36" at CP4 | E3.S1 (T-L20's launch half, A3), E2.S3 (T-L36's limits half, A2) | T-L20's launch half comes after the freeze that waits for it; T-L36 was a dry run by D64's own words, but its limits half has no code until E2.S3 | D64's dated reading: each half has its own blocking milestone |
| 7 | The visible runner, pinned in the workspace (A1) | the hidden suites (E2.S2, A2) | the format IS defined (`cases.txt`, FORMAT 3.1, `rs_samples.py:267`), but no hidden-shaped case has gone through seal, clear, build, limits and record; §6's held-out row still named v1's runner; `--limits` takes `timeS`, `readyS`, `graceS` while `inputs/limits-lang.json`, which does not exist yet, is planned in another shape (D58), and no story converts | T-L66, a synthetic dry run read at CP4; §6's row marked; E2.S3's `register-limits` writes the runner's limits file per task, and a test keeps the two equal |
| 8 | The sample format (FORMAT, A1) | the scorer | olympiad: no fault, the scorer reads the runner's JSON, and the runner refuses anything but `bytes` (`run_samples.py:187-193`), as E1.S8 refuses F-BYTES-O-N. Daemon and web: E3.S2's drivers come in B3, after B's freeze in B1, the same shape as row 1 | T-L66's daemon and web half at CP4-B, on hello programs, before `spec-frozen-B-…` |
| 9 | The pre-registration (E4.S3) | the broker's accounting (`lib/broker.mjs`, `lib/budget.mjs`) | the broker reads no byte E1 freezes, its cap is T-L10's one constant, and E3 is built before the tag (A3 before A4): no fault of this kind. One gap: §6 and §11 let the A/A set the T3/T5 policy, and §9's list of amendments did not name it (v1's guard allows it, `POLICY_FREE`). And v1's `PREREG_FILES` and `HARNESS_DIRS` name v1's folders | §9's sentence; E3.S1's v2 lists name `spikes/lang-bench/` |
| 10 | D15's allow-list, frozen at A (D47) | E3.S5's agent box, S3's build box | S3's build box sets keys D15 does not name; the agent box's need was unknown | D15's dated line; the agent box needs none (measured by `The_same_templates_build_in_the_agent_box`), the notifier included |
| 11 | The offline-only templates (A1) | E1.S7's required `build · test` job on GitHub (group C) | `nuget.config` has one source, `%NUGET_PACKAGES%`, and `.npmrc` says `offline=true`, so `bash build.sh` on a hosted runner needs a filled cache; nothing says how. Reasoned, not measured | one recorded dry run with no GitHub write, before the first D40 gate (T-L68): a clean environment, a seed step from the registered recipe's pinned versions with each package's integrity checked against the manifest, never mutable resolution, and with no change to any template file, then the unchanged `bash build.sh`, every kind and stack. A failed seed is an environment failure of the job, not a verdict on the submitted tree. If a template byte must change, it changes before the first D40 gate |
| 12 | F-FROZEN's list (FORMAT) | the process prompt (§4.5, group C) | the prompt's list is shorter | §4.5's dated line |
| 13 | D65's frozen list | E3.S8's "inputs unchanged" and "pin unchanged", `workspace-diff`, the `build.sh` comment, `templates/stack-files.json`, OP-4 | four lists for one fact | the module exports the list; a test compares the comment and the patterns with it |
| 14 | The trap table and counts (E2.S2, A2) | `defects` in E3.S4 (A3), the tag (A4) | **no fault:** nothing is frozen before its reader; the table's format is E2.S2's and E3.S4 is built after it | recorded; no change |
| 15 | FORMAT's `256 MB` in F-LIMITS-O and F-LIMITS-D, gated 2026-11-04 | D57's "every memory figure in MiB"; T-L51's "at least 256 MiB" | the plan and the gated text differ; the text's guarantee is true but smaller | D65's amendment |
| 16 | The registered snapshot, which a supersede can move (OP-7 gave it a deadline at A's tag) | the D40 gate's evidence, `gate-staleness.mjs` | a gate record did not name the snapshot, so a later supersede would not mark it stale | the snapshot's hash is in the build contract's identity; E1.S4b runs before the first gate |
| 17 | Contracts in briefs: OP-1, C1–C3, G1, H1 (the build line, the warning policy, the test folder); OP-4 (the frozen list); F2 (`.npmrc`) | every later story | fault 1 | D65 holds them; E1.S3's text cites it |
| 18 | More in briefs: OP-2 (the builder refuses any worktree, against E1.S3's "generate into a worktree"); OP-3 (the runner's plan ids); OP-6 (`.gitignore`); `s8-plan.md`'s refusal of F-BYTES-O-N | E1.S3 phase B; A's freeze; E1.S7; every spec | fault 1 | E1.S3's text and §5 changed; E1.S8c; E1.S7's list; F-BYTES-O-N stands in `inputs/FORMAT-MAINTENANCE.md` and CP1's F12, which is its right place |
| 19 | D65's own folder rule (the first text of this revision; found and measured by the judge) | a Node agent with `src/bin/util.ts`; the process arm's root `.gitignore` | one list for both stacks failed an honest program: exit 2, TS2307, every case | v1's per-stack list in D65, T-L60 and E1.S3b; the `build.sh` comments name what each copy drops; §5's `.gitignore` per stack |
| 20 | `package-lock.json`, which the agent may edit (found and measured by the judge) | `npm ci`, `tsc`, `deviations` | a lock can point at a false compiler with `package.json` unchanged, and nothing counted it | D65's (b), T-L61 |

**Why the audit stops here.** A consumer in group C is not pulled forward into A except for the one part that can move a frozen byte (rows 2 and 11). E3's orchestration, drivers, process runner and broker read files whose shape E1 already defines; T-L66 is the test that this is true. The consult found no reason to cut tasks, metrics or the measurement of actual cost, and this audit found none.

## 7. Isolation and contamination

*(2026-11-07, epoch 9: kept for the bare arm; the process arm's parts are Stage 2's.)*

Prevented, as v1: the operator's home and `/mnt` hidden; the bare box has no network but the broker door; the held-out
suite sealed, its key never in any box's environment; the real token never in the box; coai's vendor sign-ins outside the
agent box in coai's own box. Audited, as v1: every tool call scanned for paths outside the box, walkers, links, token
shapes, canaries; in the process arm, pushes and PRs outside the session's `run/<rid>/` namespace, other `run/*` reads
(the repository is public, but the process agent has no direct GitHub egress: the GitHub broker **prevents** reads of other runs' branches and PRs, the task repository's `main` attribution and the bench's history, and the audit still records every attempt — D42; v1 §7.3's "audited, not prevented" no longer applies), the pin and the frozen spec
unchanged. New in v2:

- **Known-task needles (D18)** in the scan; a hit is read by hand and recorded as a dated line; it voids only when it shows
  a reach outside the box.
- **The bare box holds one task folder (D11)**; the process clone holds the **generated tree** of the frozen tag as one
  orphan commit (D39): both stack folders (empty templates), all ten specs, `FORMAT.md`, `runner/`, and a README without its
  attribution section — so a process agent can read the other stack's empty template and the other tasks' specs (recorded
  as a property of the arm, not a contamination) and **cannot** reach a credit, a link or the attribution history; the
  needle scan over the generated tree and its one-commit history guards the cut. It cannot read the held-out suite, the
  references or another run's branch without a fetch, which the audit flags.
- **The reference solutions** live only in `~/rsd/lang-v2/refs/` (outside git, outside every box) until publication.
- **Session memory:** `CLAUDE_CONFIG_DIR` is fresh per session; coai's `COAI_DATA_DIR` is per session; question consultants'
  disk-reading rows are off (v1 D14).
- **The 18 instrument defects of v1** (RESULTS §11): the fixes of 16–18 are on `feat/minigit-bench-e3b-fix2`, which no paid
  series has run. v2 runs on a harness that carries them; the v2 A/A is the first paid run of that scan. Every void in v2 is
  replayed with the current scan at report time, as v1's `replay.mjs` does, and the record says which voids were the scan's.
- **Across groups (D44, D49; 2026-11-01).** `leakcheck` uses every group's held-out needles on every push, the frozen
  groups' included. At A's freeze the four specs and their workspaces are public; the six others stay local until B's
  freeze (the push check already refuses an un-credited known task). `workspace-diff` and T-L1 run over the task pairs that
  exist in each group. OP-J (the run branches without the attribution section) stays a question for group C, when the first
  run base is cut.

## 8. Accounting and time

*(2026-11-07, epoch 9: kept; the `build_test` classifier is fixed and the partitions recomputed in F1.S1 (D75).)*

As v1 §9–10, unchanged: headline USD = the published-price recompute from `modelUsage` with the 5m/1h split from T; the CLI's
total and `B` beside it; every stop-rule figure is the largest of the three; T1 breach stops the series, T2 `otel incomplete`,
T3/T5 per the policy the A/A sets; a killed session without an envelope is priced from `B`. **Process arm:** the coai vendors'
consumption from `coai/usage.jsonl` and `coai.db` — tokens per provider, USD a number or "unknown (subscription)", never 0
and never added to Claude's USD (v1 §9). **CPC's cost is the Claude headline USD** of the session; the vendors' tokens are
reported beside it, bounded as v1 bounds them. Time: wall from spawn to exit, the exclusive partition (`build_test >
model_api > other_tool > hooks`, then `coai_gate > consultant > ci_wait > sleep300`, then `unattributed`); primary measures
wall, `model_api`, `active − model_api`; in the process arm **active time** (work categories only) stands beside wall (v1
§10). `build_test` against `other_tool` descriptive only (v1's V16). Prices: a dated copy re-read on the series' first day.
New in v2: per-session **scoring time** recorded apart from the agent's wall; per-case runtime of the olympiad tasks as a
secondary measure; every session's spawn and exit timestamp (D27). *(2026-11-01, D44, D46:)* prices: a dated copy per
group, re-read on that group's first day; a group's CPC uses its own prices; when the prices differ between groups, every
cross-group cost line carries a sentence saying so and shows tokens beside the cost, which do not depend on prices.

## 9. Pre-registration and analysis (D24, D26, D28, D29)

*(2026-11-07, epoch 9: the per-group form below is superseded. Stage 1 has one pre-registration, `prereg/lang-v2-<yyyymmdd>`, before the pilot (F3.S1), with D71's outcome classes, D72's one value and D73's claim and limit; the overall reading is the median of the six per-task ratios. The text below is kept.)*

Committed and tagged `prereg/lang-v2-<yyyymmdd>` **before the pilot** (E4.S3) and amended only by dated lines after it;
**frozen before any paid data run (D26):** the ten specs' hashes, `FORMAT.md`'s hash and the freeze tag; the suites' hashes and counts; the runner's manifest hash (`runner/`, D37) and the execution layer's identity (D43, §6.1); the GitHub broker's allow-list shape (D42); the private trap aid's sha256 (D41); **the certifying host** (host, kernel, cgroup version, the systemd scope probe — D36) on which every data session runs; the declared culture `de_DE.UTF-8` and the base `TZ=UTC` (D35, D15); the series defaults T = 10 s and G = 5 s with every per-task override (D14); the
trap coverage table's hash; the limits of D14 *(2026-11-02: with the memory policy (D55), the `memory limit` rule, its lines and its 50 % evidence threshold (D56), the floor F and every calibration step (D57, D58))*; the prompts; the pins and checksums (CLI, SDK, Node, TS, Python); the
package sets and cache hashes; the conventions parity release sha (D23) *(per group, second full reading 2026-11-01, finding 2: frozen only in `prereg/lang-v2-C`; A's and B's tags freeze instead the task repository's provisional conventions pin of E1.S1, recorded by sha — D48)*; the owner's fixed per-session cap of $50 and the pilot formula's cross-check cap per arm (D7; no ceiling, no split)
(D30, from the pilot, as a dated amendment before round 1); this section as the analysis contract; the outcome classes, the
void (exclusion) grounds and the re-run grounds; `prediction.md`; `seed.sha256`. After the pilot, amendments may change the
caps, the stall timeouts, the limits (both stacks alike, by D14's formula), the time guesses and the split — never the
treatment of one stack. `seed.txt` is revealed after the A/A.

**Per group (D44–D47, D49; 2026-11-01) — the form in force for the staged series.** The one tag `prereg/lang-v2-<yyyymmdd>`
above is superseded by **three tags**, `prereg/lang-v2-A-<yyyymmdd>`, `-B-` and `-C-`, each committed before its group's
first A/A or pilot launch (E4.S3 per group); each freezes the paragraph above **for its group's tasks**, plus D45's group
manifest, its projection line (D53) and its seed. Later tags add; they never alter an earlier one (D45).

- **The analysis contract per group.** **A** licenses its four per-task lines and the olympiad per-kind line ("median over k
  of 4 olympiad tasks"); the known/original split inside A is 3 : 1 and is shown as a listing, not a summary. **B** licenses
  the ten-task bare lines — the bare headline (D46) — and the A–B drift table of 01, 02, 03 and 09. **C** licenses the process
  lines, the process effect and the interaction, each with T16's window sentence: they compare across windows and are
  historical comparisons, not concurrent ones. The series' headline exists only after C.
- **Predictions.** The whole ten-task prediction below is registered **at A**, before any data. B's and C's `prediction.md`
  may add lines, each marked "informed by group A" (or "by groups A and B"), and never retract one. A's discloses that
  exploratory runs of the same four tasks on another harness came before it (D44); measurement rule 4 holds because the
  prediction precedes A's paid rounds.
- **The drift table** (D46) is a required section of B's interim record and of the final record: per task of A and per stack,
  A's and B's figures side by side, and every identity that differs between the groups (runner manifest, FORMAT sha, the
  prices, the host's kernel). The bare anchor round in C (D54, approved by the owner 2026-11-01) is part of E5-C: its sessions are a drift check against B
  per task and never data (T-L55).
- **Identities across groups** are pinned once (D47) and recorded per group: an identity that differs is named in the drift
  table, never silently absorbed.
- **Memory causes (D56; 2026-11-02).** The record counts `memory limit` by cause — kernel `oom_kill`, .NET abort, V8 abort —
  and the flags `abort-below-evidence` and `child-abort-line`, per task, stack and arm; added metrics beside every v1 one (D34; T-L54).
  The drift table names the memory policy identity and the floor F among the identities. The report carries T21's and T22's
  sentences wherever a `memory limit` is a kernel kill in one stack and not the other.
- **The submission contract (D65, D66; 2026-11-05).** Each group's tag also freezes D65's contract: the build contract's identity, the frozen list, the workspace commit, the `deviations` list and how it is counted, and D66's acceptance policy. `deviations` is a reported metric per session, shown per task, stack and arm beside `accepted`, and never decides it; the same for the process arm's `strict job` column and for the lint invocations and outcomes (added metrics, D34). The T3/T5 policy of §8 is among the amendments allowed after the A/A (§11): instrument-only and for both stacks (§6.2 row 9).

**Outcome classes:** v1's twelve rows (`stop: alias drift`, `stop: security`, `stop: auth`, `void: contaminated`,
`void: harness`, `void: harness (interrupted | disposed)`, `killed: cap`, `void: rate limit`, `void: upstream`, `killed: wall`,
`killed: stall`, `completed`, `crashed`), same precedence; the process arm adds v1's `void: no reviewer`. A `killed:` or
`crashed:` session is a **data attempt**: its cost is in CPC's numerator (censored at the cap, `≥`), its quality is what it
left. A void is **kept** in the records and re-run once (§4.4); **every attempt appears in the report** (D26).

**Primary metric, exactly (D24):** for task t, stack s, arm a, over the data attempts A(t, s, a) (completed, killed, crashed —
every session that was not void):

`CPC(t, s, a) = Σ_{x ∈ A} cost(x) / |{ x ∈ A : accepted(x) }|`

with cost = the headline recompute (§8). If no attempt of (t, s, a) is accepted, CPC(t, s, a) is **"no correct solution"**,
shown with its attempts and its total spend so far in a descriptive column labelled **spend**, never as a CPC. **Second
line, pre-registered:** `CPC_all`, the same with void attempts' cost added to the numerator — the "what the series actually
paid" view. Beside both: success rate = accepted / |A|; tokens per model and kind; wall; `model_api`; `active − model_api`;
in the process arm active time and the compliance score k/18. A single session's cost that a cap censored stays a bound on
**spend** (`≥` the cap) in the session tables; that is the only `≥` in the report.

**The CPC ratio and its undefined cases (consult `68a33be2`):** the spend of a stack with zero accepted solutions does not
bound the cost of a correct solution that was never produced, so **no ratio is derived from it**. For task t and arm a,
the C#/TS CPC ratio is **defined** only when both stacks have ≥ 1 accepted attempt. When either has none, the ratio is
**undefined** and reported separately as an outcome: which stack had no correct solution, its attempts, its spend. The
cases, orientation C#/TS fixed:

| Case | Reported as |
|---|---|
| both stacks ≥ 1 accepted | the ratio `CPC(C#) ÷ CPC(TS)`, a value |
| TS 0 accepted, C# ≥ 1 | `undefined (TS: no correct solution, k attempts, spend $x)`; CPC(C#) shown beside it |
| C# 0 accepted, TS ≥ 1 | `undefined (C#: no correct solution, k attempts, spend $x)`; CPC(TS) shown beside it |
| both 0 accepted | `undefined (neither: C# spend $x, TS spend $y)` |
| a stack with no attempt at all (a round that did not run — a series stop, never a ceiling since D7) | `not run`; the task is undefined for that arm and says so; a spend of $0 is "no attempt", never a zero cost |

**Per task (D28):** for each arm, the C#/TS ratio of CPC and of every secondary measure where defined, the two success
rates (k of 3), "C# > TS" per measure. **Overall, per arm and per measure, over the defined ratios only:** the **median of
the per-task ratios** (primary), the **geometric mean of the per-task ratios**, and the **count of tasks where C# is
higher**; every summary states its denominator — "median over k of 10 tasks; m tasks undefined: …" — and never stands for
all ten; the count reads "C# higher in j of k". The undefined outcomes get their own line: **"C# alone had no correct
solution in x tasks, TS alone in y, both in z"**, with the spends. Per kind (olympiad 4, daemon 3, web API 3), and **known
(5) against original (5) separately (D29)**, the same figures with their own denominators. The **process effect** per stack
(process/bare CPC per task, median over the tasks where both arms' CPC of that stack is defined) and the **interaction**
(C#/TS process ÷ C#/TS bare, median over the tasks where the ratio is defined in **both** arms) as v1 §11 defined them;
**a task whose ratio is undefined in either arm leaves the interaction for that measure, and the interaction line names
it** ("interaction over k of 10 tasks; left out: t4 (C# no correct solution in the process arm)"). Every line is given with
and without pairs holding a `ran unpaired` session or a `foreign load` flag.

**What this licenses:** per task, "accepted k of 3", "C# > TS in j of 3 pairs" where defined, whether ranges overlap
(overlap = not distinguished), and for an undefined task the outcome itself ("TS produced no correct solution in 3
attempts"). Over the tasks, the median and geometric mean over the k defined ratios and "C# higher in j of k", always with
k and the undefined line — descriptive summaries of a chosen set, not a sample; no p-value, interval or effect size is a
finding (whether a sign test is shown as a secondary line is Q6). The report discloses that the five known tasks may be familiar to the model (D29). Nothing licenses
a claim about other tasks, models, CLI versions or the languages in general.

**Prediction (draft, to be written verbatim into `prediction.md` in E4.S2 before the pilot; the pilot may amend the cost
and time ranges by a dated line, nothing else; 2026-11-01, D44: registered whole at group A, later groups add only):**

1. Correctness, bare: success rate differs between the stacks on at least 2 of the 10 tasks; over 30 bare sessions each,
   the stacks are within 4 accepted of each other. Basis: the traps are balanced by design (D3, D25); v1 saw no quality
   difference at all.
2. The traps fire: at least 6 of the 20 (task, stack) trap sets catch at least one bare session. Basis: a suite where nothing
   fires measures nothing (the v1 ceiling).
3. CPC, bare: the median of the per-task C#/TS ratios in 0.8–1.4; C# higher in 4–7 of 10 tasks; the daemon and web tasks
   pull C# above 1, the olympiad tasks do not. Basis: v1's 0.92 on a tiny task; C# build loops multiply with task size.
4. Time, bare: `active − model_api` C#/TS > 1 on 9 of 10 tasks (v1: 3 of 3 at 1.70); `model_api` C#/TS in 0.8–1.2.
5. Process arm: success rate at or above bare on 7 of 10 tasks per stack; CPC process/bare 5–20× per stack; the
   interaction (C#/TS process ÷ C#/TS bare) between 0.9 and 1.3 — the process bundle adds more to C# (T14, T15) and the
   gates catch more C# build defects, so the direction is unclear and both are named.
6. Delivery, process: ≥ 24 of 30 process sessions per stack merge into their base; compliance median ≥ 14/18.
7. Surprises named in advance: TS accepted fewer than C# by 4 or more in either arm; a task where no session of either
   stack and arm is accepted (a floor); a task where every session is accepted (a ceiling, as v1); a bare session killed at
   the wall; a process session stopped at the $50 cap; the settled total of the series above the morning's $1000.

## 10. Threats to validity

*(2026-11-07, epoch 9: every row stays. T3, T5, T6, T9, T11, T16, T20, T21, T22 and T23 are read as the operative part's *Threats this change touches* says, and T25–T28 are there.)*

| # | Threat | Control, and the sentence the report carries |
|---|---|---|
| T1 | **Contamination of the known tasks.** Five tasks are public problems with public solutions in many languages; the model may recall a solution from training. The recall is not equal across stacks: more TypeScript and JavaScript solutions of CSES and RealWorld exist than C# ones | No network in the bare box, no id or title anywhere the agent reads (D4, D18, D19); the needles are read by hand; known and original tasks reported apart (D29). *"The five known tasks may be familiar to the model; a known-task result compares recall as much as reasoning, and the recall is not symmetric."* |
| T2 | **One-sided traps.** A task whose traps fire only in one stack measures the trap, not the stack | Every task's test design names traps on both sides (§4.3); the trap coverage table shows the count per stack per task; two consultant-rejected tasks (CSES 1712, 1736) were dropped for this reason (D3); the bias review of D25. *"Trap counts per stack are published; where they differ, the task's result is read with that difference."* |
| T3 | **n = 3 per task per arm.** Three pairs cannot separate a small difference from noise; v1's within-condition spread on a one-minute task was already wide | Paired concurrent design, ranges, "in k of 3 pairs"; the per-task reading first and the ten-task median second (D28). *"Differences smaller than the within-task range are not distinguished; the over-tasks figures are descriptive."* |
| T4 | **The agent's framework knowledge differs per stack.** Minimal API and Fastify are not equally represented in training data; TypeBox and `AddValidation()` (new in .NET 10) less so | The pieces are at the same level of abstraction (§4.6) and the rationale is public; `PACKAGES.md` names them to both; the record shows per-task tool and build loops. *"A web-API difference is a difference between agent-plus-framework pairs, not between languages."* |
| T5 | **Limits favouring one runtime.** A tight time or memory limit rejects the runtime with the larger baseline (JIT warm-up, V8 heap) | D14: limits from both references, 3 × and 2 ×, floors; G-17 re-checks on the series machine; per-case runtime recorded. *"No limit is below 3 × the slower reference or 2 × the larger reference peak."* *(2026-11-02, D55–D58:)* both stacks get the same percentage and the same cgroup bound, while what each setting bounds differs (T22, T21) — and the references are measured under the registered limit itself (D58); the floor is a measured value (D57); Node's off-heap typed arrays stay a disclosed property (T21). |
| T6 | **Harness-induced noise.** v1's 18 instrument defects voided sessions of one stack at a time (RESULTS §11); longer sessions, drivers, cgroups, coai and GitHub add failure modes; a driver hiccup reads as a product defect | The scan's fixes carried; every void kept, replayed and reported (D26); drivers and judge hand-checked (G-15, G-16); a scenario failure is adjudicated blind as product / ambiguity / harness (§4.3); `scored under load` flagged. *"Every void is named with its cause; harness defects are classed apart."* |
| T7 | **Spec ambiguity in the originals.** Two correct readings give two correct programs and one failing suite | The team lead's specs go through the document gate, a consultant and the bias review before the freeze (D21, D25); two references written apart agree on every case before sealing (E2.S1); blind adjudication; the revised score beside the primary. *"Cases classed ambiguous are dropped for both stacks in the revised score."* |
| T8 | **The model moves under a long series.** Six rounds take days; a model change behind one id is invisible | v1's alias record (D12) stops on a new id; rounds interleave arms and tasks (D10, D27) so a change hits both stacks, both arms and every task alike; every session's time is in the record. *"A change behind the same model id is not controlled; the interleave spreads it."* *(2026-11-01, D44, D47:) under the staged series the interleave no longer spreads a change across the arms (T16); the controls are one alias baseline for the series, set by group A's first session, one CLI copy, the per-group drift table, and A's four tasks replicated in B.* |
| T9 | **Prompt-cache warmth and CPU contention.** The two cells of a pair share the machine and the cache state | Same-tick start; masks swapped per pair; the quiet check; load flagged; first-turn cache read per session. *"Warmth and load meet both stacks of a pair alike; flagged pairs are shown apart."* |
| T10 | **Floor and ceiling effects.** A task nobody passes or everybody passes separates nothing (v1 was at the ceiling) | The difficulty ladder of §4.1; prediction 7 names both cases; CPC shows "no correct solution" with its cost (D24); `percent` and `defects` carry information where `accepted` is at a floor. *"A task at a floor or a ceiling is reported as such and does not count as a difference."* |
| T11 | **Time partition limits.** One Bash chain takes its highest category (v1's V16); longer sessions have more chains | The mixed-chain count per session; `build_test` against `other_tool` descriptive only; the primary time measures are wall, `model_api`, `active − model_api`. |
| T12 | **The time guesses and the cap.** A fixed $50 cap (D7) may cut a long process session of a task the pilot did not run, and cuts both stacks alike only if their session costs are alike | the pilot formula's cap per arm is reported beside $50 as a cross-check (§12, G-18); a `killed: cap` session is a censored data attempt in CPC's numerator, never dropped; the report counts them per stack and arm. *"Capped sessions are reported as bounds on spend; no CPC ratio is derived from a bound."* |
| T13 | **Public repository, secret or held-out leak.** A link in a workspace or a case in a sample would reach the agent or the public; the process agent's PR bodies are public | D2's rules; `leakcheck` on every push and on every PR body and comment (v1 §7.6 token scan widened); `workspace-diff`; the needles. |
| T14 | **Rule asymmetry biases the process arm.** v1 measured 34 rules / 331,833 B for C# against 32 / 324,781 B for TS; a rule that exists for one stack and not the other (a build flag, a test runner shape, a logging sink) is a treatment difference the process arm applies to one side only | **The conventions parity audit (D23, E0):** every C#/.NET rule has a Node/TypeScript counterpart and the reverse, released before the pre-registration; rule bytes actually read per session are recorded as v1's V1 asks. *"The process arm compares C#-under-the-family-rules with TS-under-the-family-rules; the rules were audited for parity at release `<sha>`, and the bytes read per session are in the record."* |
| T15 | **The process arm is a bundle and its wall is mostly waiting** (v1 V1, V3): gates, consultants, CI, `sleep 300`; the bundle's C# half carries the analyzers, `-m:4`, MTP, warnings as errors | The partition; active time beside wall; the compliance score beside `accepted`; the interaction reported with the sentence v1 wrote. *"The process arm's C#/TS difference is C#-under-the-process against TS-under-the-process, not a language effect; process wall time is largely not agent time."* *(2026-11-05, D66:)* the bundle's warnings as errors do **not** reach the frozen acceptance build: the frozen line forces that setting off, the process arm's workspace instructions say the frozen build takes precedence, and the doctrine is met in CI's required `strict` job per stack (warnings as errors in C#, ESLint in Node), reported beside `accepted`. *"Warnings as errors and lint were process-arm checks, reported beside acceptance; both arms and both stacks were accepted by one frozen build."* |
| T16 | **Arm against time.** The two arms never overlap; v1's bare-first run confounded arm with window | D10's strict alternation of arm per round and the seeded lead; after every even round both arms have equal n and spanned the same days. *"Arms alternate by round; a window effect meets both arms."* **Superseded for the staged series (D44, 2026-11-01), the text above kept:** the arms run in different windows (groups A and B before C), so the process effect and the interaction are read across windows, and a model or price change between B and C is not separated from the arm. Control: one model id and one alias baseline (D47), one CLI copy, the drift table (D46); the sentence in every process-arm table: *"The process arm ran after the bare arm, in a later window; the process effect and the interaction compare across windows and are historical comparisons, not concurrent ones."* The lost alternation is the narrowing of the epoch-5 re-review; the optional bare anchor round in C (Q-G) would measure the B→C drift instead of assuming it. |
| T18 | **The public history keeps the trap rows.** This plan's §4.3 trap table and the per-task trap columns were public in #124, #127 and #128 before they were moved to the private area (D41); history is not rewritten | No new trap content anywhere public (D41); the process agent has no direct GitHub egress (D42), so the only road to that history is the model's memory of a public repository; the record discloses it and names the three merges. **Also disclosed (2026-11-01):** the explanatory note under ledger entry 1.2 named one task-01 trap in plain words in every version of this plan on `main` from #127 to #138; found by E1.S1's re-review (the pre-push check's always-exempt plan texts), reworded on that day, history not rewritten. *"The trap design aid was public for one day in the bench's history; sessions could not fetch it, and whether a model recalls it is not controlled."* |
| T19 | **The GitHub broker narrows the process arm.** A developer under the family's real process reads anything on GitHub; the process agent reads only its run's refs, PR and objects (D42) | Disclosed as a contract (D42), in the record and in every process-arm table; the compliance score and the delivery check are unchanged; the pilot (G-20) shows the normal clone, push, PR and checks workflows succeed under it. *"The process arm's GitHub access is run-bound by a broker serving an isolated bare repository per run; the process-arm result applies to two stacks under the same run-bound GitHub policy, not under the family's open one."* |
| T17 | **Vendor reviewers saw the held-out cases** (the bias review of D25 sends suites to coai's vendors before sealing) | The agents under test have no network and the suite is sealed; the vendor models are not the agent under test; recorded as a residual (Q-C). *"Reviewers of other vendors read the suites before sealing; the agent under test never did."* |
| T20 | **Staged groups** (D44, added 2026-11-01). The groups ran on different days; a later group's pre-registration was written knowing the earlier groups' results; interim records existed before the series ended | The whole prediction registered at A, later lines add-only and marked (§9); every group's identities and dates in the record; A's four tasks replicated in B with the drift table (D46); interim records carry counts and rates only (D49). *"Groups A, B and C ran in that order on different days; later pre-registrations knew earlier results, and the predictions they added are marked so."* |
| T21 | **Node's typed arrays live outside V8's heap.** *(2026-11-02, change 6.1:) the measured cases are the mixed workload under every setting and medium-buffers at 256 MiB (both recorded, never asserted in T-L43).* `Buffer` and `Uint8Array` are not counted by V8's heap limit or its heap-driven GC: a workload with 35 % of the limit in typed arrays plus churn is killed by the kernel in Node at 256 and 512 MiB under every setting it was run with (the view alone, A at 100, 90 and 85 %; never run with no view), while .NET's byte arrays are GC-managed and pass (D55, `followUp5`). A task whose natural solution holds large byte buffers meets a Node property, not a Node defect | Recorded, not tuned around: the heap settings of D55 are the same percentage for both stacks; calibration measures the real references under the policy (D58), so a limit is never below 2 × the larger reference peak; the record names the cause of every `memory limit` (D56). *"Node's off-heap typed arrays are bounded by the cgroup alone; a `memory limit` in Node by a kernel kill where .NET passed is reported with that property named."* |
| T22 | **The heap settings are part of the treatment.** Both stacks get 90 % of L, but the two settings bound different things: .NET's hard limit bounds the whole GC heap; V8's flag bounds the old space only (the young generation and off-heap memory are outside it). The 50 % evidence threshold of the abort rule is a heuristic (D56) | One policy for every task, both stacks, every group, pinned and asserted through the final prefix (T-L43); the threshold's boundary tested at the byte (T-L50); the flags `abort-below-evidence` and `child-abort-line` counted per stack and read in the blind adjudication. *"Each runtime's heap was capped at 90 % of the memory limit by its own mechanism; the settings and the abort rule are in the pre-registration."* |
| T23 | **The frozen build can be gone around, and acceptance does not refuse it (D65).** A project edit or a source marker can silence the build's type-safety failure: 11 of the 21 C# rows measured are not stopped by the command line (a `<Target>` in `App.csproj`, a pragma, a `*.g.cs` file), and in Node a suppression comment, or a package item or a lock's `resolved` at `file:` with a false `tsc`, does the same. A session that goes around the check can be `accepted`. The two stacks do not offer the same ways | Nothing is refused, because no agent was told an edit is illegal, and a refusal could fail a correct program. The frozen files are restored, scripts do not run (`ignore-scripts`), and every deviation is counted per stack beside `accepted`. Rows (a), (b), (c) and (e) are read by hand wherever a count is above zero in an accepted session; (d) is reported only, because the lexical `any` would fire on most Node sessions. *"The build's type-safety check protects against an accident, not against an agent that sets out to pass it; every deviation from the given project is counted per stack beside the result."* |
| T24 | **The agent can tell that it is being tested (Q-I).** The amendment takes "the v2 pre-registration" out of every spec's header, but this reduces explicit prompting and is not blinding: `Gate verdict`, `spec-frozen-A-…`, the `runner/`, "Hidden tests exist" and "reference solutions in both stacks, before any session" all stay, and every agent reads them | Both stacks read the same words; the report claims no unawareness. *"The specs carry a gate verdict, a freeze tag and a statement that hidden tests exist; the agents could infer a test, and this was not controlled."* |

## 11. Pilot and A/A (D16)

*(2026-11-07, epoch 9: the per-group form below is superseded. Stage 1 has one pilot, a daemon pair and a web pair as void spend, and one bare A/A pair, gated by G-1 to G-14, G-16, G-17 and G-18 (F3.S2). The G rows and the stops stand. The text below is kept.)*

**Per group (D44; 2026-11-01) — the form in force for the staged series.** The pilot and the A/A run per group, each before
that group's first data pair; the paragraph after this table (one pilot before any data pair) is superseded for the staged
series and kept; its GO table and the stops stand, each G-row in the group that exercises it.

| Group | A/A | Pilot pairs (void, D16) | G-rows that gate it | Sessions |
|---|---|---|---|---|
| A | the bare A/A as D16 says: two identical bare C# sessions of task 3 (an olympiad task); it sets the T3/T5 policy (§6) and the noise floor of the v2 harness's first paid run | bare 1, 3 and **9** — task 9 so that the stall timeout (G-18: the longest quiet gap seen in any pilot session) and the bare wall (≈ 75 min, D4) are also set from a 45–60 min task | G-1 … G-14, G-15, G-17, G-18 | 6 pilot + 2 A/A |
| B | re-run **only if an instrument identity changed** since A's — the runner manifest (expected, D50), the layer, the CLI copy, the model id, the toolchains (§6.1); the drivers' own control is G-16 on the real pilot trees | bare 4, 5 and 10 — D16's driver tasks | G-1 … G-14, G-16, G-17, G-18 | 6 pilot (+ 2 A/A) |
| C | none: v1 designed the process arm with P0 and the model smoke, not an A/A, and the three paired repeats give the noise | process 1 and 3 (Q7), after P0 and the model smoke on this repository's templates | G-1 … G-14, G-17, G-18, G-19, G-20 | 4 pilot + P0 + smoke |

Walls and stalls per arm, set by the group's pilot: A's pilot sets the bare wall and stall; B inherits them unless its pilot
on 4, 5 and 10 shows a longer quiet gap (then a dated amendment, both stacks alike); C's pilot sets the process wall (Q-E
stays open). The pilot formula's cross-check cap (G-18) is computed per arm per group (D53).

Void spend, before any data pair: the bare A/A (two identical bare C# sessions of task 3, concurrent), then the bare pilot
pairs of tasks 1, 3, 4, 5 and 10 (task 4 for the HTTP driver, task 5 for the RESP driver, task 10 for the daemon checks — G-16), then the **process pilot pairs of tasks 1 and 3** (v1's P1 conditions: merged with green
checks, coai rounds in its db, the scorer's numbers reproduced by hand, T1–T5, `num_turns` > 1, audit and canaries ran,
read-back matches the pins), after v1's P0 (no model) and model smoke have passed on this repository's templates. GO =
every row of v1's G-1 … G-14 holds (G-6 may be `explained`, G-9 never gates, the two `not exercised` cases as v1 with their
replacing evidence), plus:

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-15 | the olympiad judge by hand: one C# and one TS pilot tree, run by hand on three held-out cases, give the judge's verdicts and times within 10 % | the judge misreads a real tree | fix, re-score (no re-run) |
| G-16 | the drivers **each checked on the real pilot tree of the task that exercises them** (all three tasks are in the bare pilot set, D16): **task 5** (the daemon with a framed TCP protocol) for the TCP driver — the driver's scenario kinds for that task, driven by hand, give the driver's replies; **task 10** (the DAG scheduler) for the daemon checks that are its own — the cgroup `memory.peak` read by hand from the program's scope agrees with the driver's record, the RSS curve by hand (`ps` every second) agrees with the descriptive curve within 10 %, SIGTERM by hand ends the process within G as the driver says, the task's timer scenarios give the driver's final report; **task 4** (the web task) for the HTTP driver — one scenario of each kind the driver has (a concurrency scenario among them), and a captured dynamic value reused as `FORMAT.md` defines; which scenarios, and what they target, is the private aid's (D41); the culture check of T-L32 passes inside both boxes | a driver misreads a real program | fix, re-score |
| G-17 | **calibration order held:** every task's limits were registered only after the joint validation of that task (drafted samples, references and suites run against each other; both references then pass every visible and held-out case — E2.S1 final — with no spec or `FORMAT.md` amendment pending), and no spec or `FORMAT.md` amendment touching that task is dated after its limit registration; both references of every task pass 100 % under the registered limits **in the scoring box on the series machine**, under the quiet check; each mutant fails exactly its cases | a limit was registered before the joint validation, or an amendment voided it, or a limit rejects one runtime's baseline, or a trap is not caught | a spec or `FORMAT.md` amendment after registration **voids that task's limits**: E2.S3 runs again for it and re-registers (dated, both stacks) before the pre-registration; otherwise recompute by D14 or fix the suite; all before round 1 *(2026-11-02, D58:)* "pass 100 % under the registered limits" reads: in the calibration run under the registered limit itself and the heap settings derived from it (D58's steps 3 and 5), recorded in `inputs/limits-lang.json`; a limit whose evidence run was under another limit or other settings was never registered (T-L49); the floor F was registered by the floor probe first (D57). |
| G-18 | the pilot formula's cap per arm (§12) computed from the pilot's settled costs and written beside the owner's fixed **$50** cap as a reported cross-check (D7; no split, no ceiling); the stall timeout per arm larger than the longest quiet gap seen in any pilot session; every pilot session that reached $50 recorded `killed: cap` | the stall would cut sessions the task needs, or the $50 cap was not enforced | set the stall from the pilot; fix the cap enforcement; before round 1 |
| G-20 | **the GitHub broker in the process pilot (D42):** in both process pilot pairs the clone of the run base, the fetch and push of the run's own branch, the PR creation and update and the reads of its checks and comments all succeeded through the broker (the stream and the broker log agree); the denied probes — a fetch of the bench repository, a read of the task repository's `main` README, a fetch of the other pilot run's branch and PR, a redirect outside the allowed set — were each attempted by the harness from inside a pilot box and refused with the broker's named error; no `github.com` CONNECT appears in the egress log | the broker lets a forbidden read through, or blocks a needed workflow | **stop**; fix (E3.S7a); one more process pilot pair |
| G-19 | the process pilot: both pilot pairs delivered (merged, green), every compliance item computable, the frozen spec unchanged in both runs, the plan gate reached `proceed` before any code gate, CPC computed by hand for one pair equals the tool's | the process arm's instruments misread a real run | fix; one more process pilot pair before round 1 |

Stops as v1: G-2, G-8, G-12 refuted, and alias drift, stop the series until fixed and an A/A is GO.

*(2026-11-05, D65, D66:)* G-15's and G-16's by-hand builds go through `lib/submission.mjs`, so the hand reads what the scorer read, and each by-hand line also reads the session's `deviations`; G-19 also reads CI's `strict` job for both pilot pairs; the T3/T5 policy that §6 leaves to the A/A is a dated amendment line (§9).

## 12. Budget (D7; D30 superseded)

*(2026-11-07, epoch 9: the $50 per-session cap stands; there is no total ceiling, and actual costs are reported, never predicted ones (the owner). The per-group budget lines (D53) are superseded.)*

**The owner's frame (2026-10-31, late evening): no total ceiling; a fixed per-session cap of $50.** The six rounds of both
arms run as planned *(2026-11-02: the pre-staged wording, kept; under D44 read three rounds per group — A, B and C, nine data rounds
in the series — plus C's anchor round, D54)*. Every session carries the cap **$50** (the broker's budget and the CLI's `--max-budget-usd`, as v1 put
the cap on both): a session that reaches it is stopped and recorded as **`killed: cap`, censored at `≥ $50`**, a data
attempt in CPC's numerator (§9), never hidden. Pilot, A/A, data, void and re-run spend stay on separate lines in the
ledger and the report; the spend gate before every pair keeps only its **per-session** part (the live O sum of a running
session against its cap) and the **round rule** of D10 (round k+1 starts when round k is scored); it has no ceiling line.
The pilot's cap formula below is **kept as a cross-check that is reported and does not bind**: the record shows, per arm,
the formula's cap beside the owner's $50, so a reader can see whether $50 was generous or tight for the task.

**Superseded (D1; the text stays so the decision's history is readable):** the $1000 total ceiling and the gate's ceiling
line; the data share and the split between arms; the `budget-frame` halt over the total; the budget approval step and its
options table. None of them binds anything. The paragraphs below are that history.

*Superseded — the split and the caps, derived from the pilot (D30, G-18), pre-registered as a formula (now a reported
cross-check only):* per arm, per task,
`cap = min(2 × the larger settled cost of its pilot pair, capMax(arm))` for the piloted tasks; for the others, the piloted
task of the nearest guess scaled by the ratio of guesses (the process arm has two pilot points, so its scaling is marked
weaker); `capMax(arm)` is set so that 1.1 × Σ over the arm's planned data sessions (60 across 6 rounds, or the amended round count of the accepted budget frame) of their caps ≤ the arm's share of the **data
share** = $1000 − the settled pilot and A/A spend (16 sessions, ≈ $180–520 at the guesses, D16) − a **re-run reserve** of
1.1 × 2 × (`cap(bare)` + `cap(process)`) (two void re-runs per arm at the largest derived cap); the **share** is proposed
as proportional to the arms' expected totals from the pilot (so neither arm is starved by the formula), and is the
operator's to confirm (Q-A). At the guesses the data share is ≈ $400–800 before the re-run reserve, so the frame check
below is expected to fire unless the pilot comes in low.

*Superseded — the deterministic default when the frame breaks* (the cap rule fails — `capMax(arm)` < 1.5 × any pilot
cost of that arm — or the projected total, Σ expected costs + 1.1 × the re-run reserve, exceeds the ceiling) *and the
operator has not answered:* the runner **refuses to start round 1**. It exits with the resumable-halt code **3** and the named halt
`budget-frame`, prints the measured numbers (per arm: every pilot session's settled cost, the derived caps, `capMax`, the
projected total, the ceiling) and the exact resume command, `lang-runner.mjs resume --series <dir> --amendment <date>`,
which is accepted only when a dated budget line of that date exists in `amendments.md`. **It never cuts rounds, tasks or
arms on its own.** Any cut — fewer rounds, a smaller task set, a process-arm limit of its own, a raised ceiling — is the
operator's dated amendment, applied before round 1 and recorded in the pre-registration's amendments. The gate's own
round-pair cut (D10) applies only inside a frame the operator has accepted.

*Superseded — the budget approval step.* At this plan's own estimates — bare $300–1,200, process $1,800–4,200, pilot and
A/A $180–520 — six rounds of both arms did **not** fit $1000; the owner's answer on 2026-10-31 was to remove the total
(D7), so no decision line is needed and the options below were never exercised. Was: after the pilot and before round 1,
the operator records a dated budget decision in `amendments.md`: either **(i)** a raised ceiling with its amount, or **(ii)** a named reduction
— the rounds per arm, the task set, or the arms — or both. The runner's `budget-frame` halt stays the default until that
line exists; the plan does not pick an option (Q-A). The options and what each does to the analysis:

| Option | Effect on n per task | Effect on the analysis (§9) |
|---|---|---|
| (i) raise the ceiling to the projected total + the re-run reserve | n = 3 per task per stack per arm, as planned | the full contract: per task "j of 3 pairs", overall "k of 10" per arm, process effect and interaction over 10 tasks |
| (ii-a) fewer rounds per arm: 4 (two per arm) | n = 2 | per task "j of 2 pairs", no within-task range worth the name; overall still "k of 10" per arm; the interaction keeps 10 tasks |
| (ii-a') fewer rounds per arm: 2 (one per arm) | n = 1 | one pair per task per arm: no repeat, no range; "C# > TS" per task is one observation; the median over tasks is the only summary |
| (ii-b) a smaller task set (named tasks dropped; kinds and the known/original balance stated) | n = 3 on the kept tasks | overall "k of m" with m < 10; per-kind and known/original lines shrink or vanish with the kinds dropped; the dropped tasks are `not run` |
| (ii-c) one arm only (the bare arm, the operator's choice of arm) | n = 3 in the kept arm | no process effect, no interaction; the question of §1 is answered for one arm; the process arm is `not run` |
| (ii) combinations, for example 4 rounds on 8 tasks | as the parts | as the parts, each stated in every summary's denominator |

Every option leaves the pre-registered measures and classes untouched; it changes only how many observations each has,
and every summary names its denominator anyway (D28). The decision line names the option, the resulting round count, task
set and arms, and the new `capMax` per arm.

**Projection, as a guess only:** v1's one-minute bare sessions cost ≈ $0.35 each; v2's bare sessions run 5–75 min with many
build and test loops, so $5–20 each → 60 bare sessions $300–1200. v1 predicted $30–70 per **process** cell on a far smaller
task → 60 process sessions $1800–4200 at that rate; the pilot itself ≈ $180–520 (D16). With no total ceiling (D7) the
projection is information for the owner, not a gate: the record shows the settled spend per arm against it. The $50 cap
bounds any one session; at the guesses no bare session and few process sessions approach it, so censoring should be
rare — the pilot shows whether that holds (Q-A closed).

**Per group (D53; 2026-11-01).** The cap and the frame above hold for every group; **no new halt**. What is added is
information: per group a **projection line** in its pre-registration and **settled ledger lines** — `A: data / pilot / A/A /
void / re-run`, the same for B and C *(finding 4, 2026-11-01: C's are `data / pilot / A/A / anchor / void / re-run`, the
`anchor` line used only if Q-G is answered yes — answered yes on 2026-11-01, D54, so it is used)* — so the owner sees each group's settled cost before the next group's paid sessions; the
pilot formula's cross-check cap (G-18) is computed per arm **per group** and reported beside $50. **The projection, with both
priors named** (neither is evidence; each group's pilot replaces them):

| Group | Sessions | At the exploratory pilot's rates (task 1 ≈ $0.17 a session; the olympiad plan predicts $0.2–6) | At this section's guesses ($5–20 a bare session, $30–70 a process session) |
|---|---|---|---|
| A | 24 data + 6 pilot + 2 A/A, bare | ≈ $20–90 | ≈ $160–640 |
| B | 60 bare data + 6 pilot (+ 2 A/A if an identity changed) | the 24 replication sessions ≈ $20–100; the 36 daemon and web sessions have no exploratory rate — at the guesses $180–720, so ≈ $200–800 for the data (a mixed figure, named so) | ≈ $300–1,200 for the data; the pilot and the A/A ≈ $30–160 more |
| C | 60 process data + 4 pilot + P0 + the model smoke | — (no process run yet) | ≈ $1,900–4,500 |
| C, the anchor round (D54, approved; mandatory) | ≈ 20 bare sessions | less than the guesses for its olympiad part | ≈ $100–400 |

## 13. Growth

*(2026-11-07, epoch 9: Stage 1's growth is the operative part's *Growth*. The text below is kept.)*

| Surface | Size at this plan's volumes | Retired by / interrupted |
|---|---|---|
| Session dirs `~/rsd/lang-v2/<series>/s/<rid>/` with seeded caches (≈ 150–300 MB each), the stream and transcripts (bare ≈ 50–150 MB; process ≈ 300 MB raw as v1 §14 measured for a process cell, plus the coai dir ≤ 20 MB), the scoring copy | bare ≈ 0.4–0.6 GB, process ≈ 0.7–1 GB per session; 120 data + 16 pilot/A/A (one A/A pair, five bare and two process pilot pairs) + re-runs ≈ 145 sessions ≈ **70–110 GB** on `/home` (745 GB free, 2026-10-30); the pilot's measured sizes replace this estimate | `bin/ obj/ node_modules/` shed after scoring (safe-rm under the series root, by name); the rest kept until E6's archive and `retire.mjs` after the record is promoted |
| Toolchains `~/rsd/lang-v2/toolchains/` (SDK ≈ 1 GB, Node ≈ 100 MB, TS, the CLI copy 292 MB), two cache snapshots (≈ 0.5–1 GB) | ≈ 3 GB | kept for the series; `retire.mjs` |
| Held-out source, references, mutants and the test-design aid `~/rsd/lang-v2/{heldout-src,refs,mutants,test-design}/` (mode 0700 / 0600; `test-design/PLAN-trap-table.md` is D41's moved table) | MBs | kept (the operator's record); never in git |
| `spikes/lang-bench/heldout/suite.enc`, `inputs/*`, `research/lang-v2/data/*.json` | a few MB, committed | kept |
| The public task repository: specs, workspaces, ≈ 60 process run bases + pilot + re-runs ≈ 70 `run/*` branches and ≈ 70–200 PRs | small; the branches and PRs are the audit trail | kept permanently (v1 §14) |
| coai session dirs, vendor CLI stores | as v1 §14 | with the session dir; the vendor stores are the operator's, growth measured and named |
| Credentials: one fine-grained PAT, one deploy key | live for the series | **teardown** after the last cell is scored and archived, as v1 §14 |
| Scoring cgroups and the clear suite in the scoring temp dir | one per scoring run | removed by the scorer's finalizer; `heldout-tool.mjs sweep` for leftovers older than 24 h |
| Session dirs per group — added 2026-11-01 (D44) | A 24 data + 8 pilot and A/A ≈ 32 bare; B 60 data + 6–8 pilot and A/A ≈ 68 bare; C 60 data + 4 pilot ≈ 64 process; with re-runs ≈ 170 sessions; at the sizes above ≈ **85–130 GB** on `/home`, replacing the ≈ 70–110 GB line above for the staged series; the anchor round (D54, approved) ≈ 8–12 GB more | as the row above, per group; a group's session dirs are kept until E6's archive |
| Held-out archives per task — added 2026-11-01 (D49) | ten `heldout/suite-<task>.enc` instead of one `suite.enc`; a few MB in all, committed | kept |
| In-flight states | `launching` sessions, a scorer run, a daemon left by a driver, an open PR of a void session | `retireStale` at every `run`/`resume`; the walls, the scorer's outer limit (≤ 90 min) and the quiet wait (30 min) bound every non-terminal state; the janitor ends any process carrying the cell's marker; an open PR of a void session is closed by the harness with a comment (v1 §14) |
| The probe and experiment records `spikes/lang-bench/inputs/exec-probe-<date>[-<n>].json` and `inputs/opk-experiment-*.json` — added 2026-11-02 (D59) | KB per record, committed; one file per probe run, never overwritten | kept in git with their layer identities; nothing to sweep |
| The superseded C# snapshot and the matrix's record — added 2026-11-05 (change 7.1, D65) | the old folder ≈ 174 MB (2026-11-04) until it is pruned, the new one a few MB more; the record `inputs/package-matrix.json`, KB, committed; 3 kinds × about 15 offered names × 2 stacks, about 90 builds of about 3 s, nothing kept | `make-snapshots.mjs --prune-superseded` under the gate lock; a killed matrix run leaves only owned test folders, swept as every test's are |
| `deviations` in every session record — added 2026-11-05 (D65) | below 10 KB per session (counts and paths, no file text); ≈ 170 sessions | kept with the record; no sweep; a pure function, so an interrupted scoring writes nothing |

## 14. Build order

*(2026-11-07, epoch 9: **superseded whole.** The order in force is the operative part's *Build order*, F1.S1 first; §14 A1's steps 0 to 2 are F1.S1. Everything below is kept as history.)*

**The staged build order (D44; 2026-11-01) — the order in force.** Steps 0–6 further below are superseded for the staged
series and kept for their history; every step they name still happens, in the group that needs it (the epics' group map,
§ *Epics*). Each group runs to its evidence before the next group's paid sessions (D44); building the next group may overlap
the current group's rounds, because nothing of it can change a pinned manifest (D45).

0. **E0, now and in parallel** — needed before group C's pre-registration only (D48).

**Group A — the four olympiad tasks 01, 02, 03 and 09, bare arm.**

- **A1, E1's slice for A.** E1.S1 (in review; the exact exemptions, D52) and E1.S5 (merged); E1.S8a part i and **part ii-a**;
  **E1.S8b whole, the netns holder included, with its memory probe asserted at 256 and 512 MiB (D51)**, then **CP2**; E1.S9
  FORMAT v2 with its section classes (D45), gated; E1.S8 the conformance check; E1.S4 **the full package sets of both stacks,
  web packages included** (`PACKAGES.md` lists the stack's offered set in every workspace, so the set must be the same in A and
  B for the same task); E1.S3 phase A, then phase B for the four; **E1.S7's tag ruleset and `main` protection only**, applied
  and read back before A's freeze; per task 01, 02, 03, 09: S6 → S2 draft → conformance → S2a → the D40 gate on the layer →
  the document gate, the consultant, the bias review → gated; **CP3 after task 01**; **CP4**; the freeze `spec-frozen-A-<date>`;
  OP-K decided on the asserted probe before E2-A calibrates (D51).
  *(2026-11-02, the epoch-6 re-review — D55–D59:)* A1 reads: **E1.S8b fixed forward to the policy at 90 % and re-pinned; E1.S8a
  fixed forward to the abort rule's evidence boundary and flags on S8b's branch and re-pinned (A's runner manifest); the probe
  re-run under D55 through the final prefix at 256 and 512 MiB and recorded as a new dated file; T-L43 (revised) and T-L50 green, and
  `rs_abort.py` named in T-L38's module set (its byte-identity runs when B's manifest exists); then CP2.** OP-K is closed by D55 — no
  decision is left for CP2. *(Judge J1, 2026-11-02:)* T-L49 is E2.S3's and belongs to A2; T-L52 needs part ii-b's daemon and web
  lifecycle and B's manifest and belongs to B1 — neither gates CP2.
  *(2026-11-03, the epoch-7 re-review — D60–D64:)* A1 adds, in this order of blocking: **before S9's gate round** — FORMAT
  fixed forward on `docs/format-v2`: sections 4 and 5 `daemon-web` and D60's class sentence; CP1's F1–F4 (text only, each with a
  changelog line); F5's F-STDERR, F7's F-ENV and 4.2, F6's `Format version: 2` (D62); `F-PROCS` (D63 (6)); T-L56, T-L57's
  extractor half and T-L58 green; **before the first D40 gate (task 01)** — CP2's conditions (1)–(3) in E1.S2a's pre-freeze tool
  (T-L59) and (4) checked by E1.S8b's identity keeper (D63); **before A's freeze** — CP1's F8 and F9 in FORMAT; every freeze
  rule's permitted and refused pair read at CP4 (D64). CP1's F10–F13 go to part ii-b or E1.S8.
  *(2026-11-05, the epoch-8 re-review — D65–D68; **this order is the one in force for what is left of A1**; the done work is
  E1.S1, S4, S5, S8a part i and ii-a, S8b, S9 and CP2, merged:)*
  0. **The marker test, the first small item after epoch 8 opens (D68).** `The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed`
     failed in two consecutive whole runs, so it is diagnosed before #167 merges. *Read, not run:* `test/support/probe-scope.mjs:56` sends `SIGKILL`
     without waiting, `test/exec-probe-stop.test.mjs:229-232` scans `/proc` at once, and the test runs no layer code: a race in the test helper, not a
     defect of the stop path. **First the two existing failures (`s3a-j1.txt`, `s3a-j2.txt`) are read and the diagnosis is recorded: under D68 no further whole run comes before it.**
     Only then, if the reading shows the helper's race: a test that forces the race, red first; then the helper waits with a bound, with no change to the
     layer; then a fresh whole green completion run before #167 merges. If the reading shows a layer defect instead, it stops and goes to the team lead. S5's line says so.
  1. **E1.S8 and E1.S3 phase A merge**, after the judge's sort. Before #167's merge, on its branch, a doc commit: merge `main` (a conflict in the E1
     table, at the E1.S3 row); mark "the rule for E3's scorer" and "Input for E3's plan round" in `research/module_lang_bench.md` superseded by D65; the
     module's frozen list and build line, and the E1 status row, cite D65 and D66. No code changes before these merges. E1.S8 merges `main` with no conflict.
  2. **Then, each with its own review, all before step 3:**
     **FORMAT's amendment** (D65: F-OFFLINE, the tag form, MiB, the `Freeze rule` row, one document gate), with its pins and the checker's two lines; *(2026-11-05: done. The task repository's PR #3 at `4ddb8f80`, three document rounds `proceed`; the bench's pins and the checker's two lines on `feat/lang-v2-format-amend-pins`; D65 records the refined words.)*
     **E1.S4b**, the `Mvc.Testing` supersede and the matrix (T-L63; change 7.1), with the module's known limit on `JwtBearer`;
     **E1.S3b, after S4b**, the submission contract: `lib/submission.mjs` and `lib/deviations.mjs` with their pairs, `NPMRC_WORKSPACE` and the three `.npmrc`, the `build.sh` comments (T-L60, T-L61, T-L62). S3b owns the build contract's identity: its writer runs last among these because S4b moves the snapshot first, and nothing later moves it;
     **E1.S8c**, the runner's comments and the case record's fields, re-pinned, the probe re-run, CP2 addended (T-L64; D67 (4)), with the module's sentence that the runner is not scanned;
     **T-L68, after E1.S3b**, the team lead's one recorded dry run of the CI seed, because it reads S3b's `.npmrc` and `build.sh` bytes.
  3. **E1.S2a's pre-freeze tool**, importing the module, with CP2's conditions (T-L59), `buildContract` in its record; then the
     **first D40 gate, task 01**; S2's document gate, the consultant, the bias review; **CP3**; then 02, 03, 09.
  4. **T-L66's dry run, E1.S2a's, here because it reads what S8c re-pinned; CP4 (with D64's pairs and T-L66 read), the freeze `spec-frozen-A-<date>`, S3 phase B for the four, the
     workspace commit** (D67 (3), T-L67). Group A's manifest names it.
  Side findings stay todos (the owner's standing rule): S5 of the test-leftovers plan is not built for this; the merge condition is D68.
- **A2, E2 for the four tasks** (olympiad mode): S1–S4 per task; S5 once, before A's record; one sealed archive per task.
  *(2026-11-02, D57, D58:)* **S3.0, the floor probe, first** — the fixed-size workload at 256 and 512 MiB in both stacks, F
  registered (T-L51) — then per task S3 by the sequence: the provisional run at F, the candidate, the confirming run under the
  candidate and its heap settings, the time limit under the registered memory limit (T-L49).
  *(2026-11-05, D65, D64's dated reading:)* every reference, mutant and calibration build goes through `lib/submission.mjs`, so its
  evidence is about the path that scores; T-L36's limits half is green before the first `register-limits`, and `register-limits`
  writes the runner's limits file (§6.2 row 7).
- **A3, E3's bare slice:** S1 (the `group` dimension and manifest, arm = bare, three rounds over the group's tasks, the inherited
  alias baseline — T-L40–T-L42), S3 (the judge on the layer), S4 (olympiad scoring), S5 (D15's environment in the agent box,
  the classifier, G-15/G-17/G-18 in `aaVerdicts`, CPC in the record, the ≤ $0.20 bare smoke on task 1), S10 (the module
  document for the slice). *(2026-11-05, D65:)* S4 imports `lib/submission.mjs` and adds `deviations` to the record; T-L20's launch
  half is green here, before A's tag.
- **A4, E4-A:** the price re-read and the CLI pin (once for the series, D47); the whole ten-task prediction and A's documents;
  the tag `prereg/lang-v2-A-<date>`; the bare A/A; the pilot pairs 1, 3 and 9; the G-rows of §11; the amendments; `seed-A`
  revealed.
- **A5, E5-A:** three bare rounds over the four tasks.
- **A6, E6-A:** the interim record, counts and rates only (D49); A's settled ledger line before B's paid sessions (D53).

**Group B — the bare arm for all ten tasks.**

- **B1:** E1.S8a part ii-b (the second runner manifest, D50; it inherits the abort rule unchanged — D59, T-L52); the six spec chains (04, 05, 06, 07, 08, 10 in §4.1's order, each
  S6 → S2 → S2a → D40 gate → gated); the freeze `spec-frozen-B-<date>`; **the D40 gate and E2.S3's calibration re-run for 01,
  02, 03 and 09 on B's identities** (D50; T-L36, T-L38, T-L39). *(Finding 1, 2026-11-01: a check only — A's registered
  limits stay; a different value is a drift finding; a reference failing under A's limit suspends that task's replication.)*
  *(2026-11-03, D60, D61, D63:)* part ii-b carries T-L31's runner `signal TERM` step and its per-stack TERM test before B's
  first D40 gate (CP2's F5); a B amendment of FORMAT touches only `daemon`, `web` or `daemon-web` sections, checked against
  A's frozen classification (T-L56 at B's tag); A's four keep A's `GRAMMAR.md` (T-L57); CP4-B reads D64's pairs for B.
  *(2026-11-05, §6.2 rows 3 and 8:)* CP4-B also reads T-L66's daemon and web half on hello programs before the freeze, so E3.S2's
  drivers, built in B3, read a shape already shown; B's workspace commit follows the freeze and is the one a run base is cut from.
- **B2:** E2 for the six tasks; one sealed archive per task.
- **B3:** E3.S2, the drivers (cornerstone); E3.S4's daemon and web scoring; G-16.
- **B4, E4-B:** the tag `prereg/lang-v2-B-<date>`; the A/A only if an identity changed (§11); the pilot pairs 4, 5 and 10;
  `seed-B`.
- **B5, E5-B:** three bare rounds over the ten tasks.
- **B6, E6-B:** the interim record with the ten-task bare lines and the A–B drift table (D46, D49).

**Group C — the process arm for all ten.**

- **C1:** E0's release pinned (E0.S3); E1.S7 whole (templates, CI, the remaining rulesets, the deploy key and PAT in use, run
  bases bound to the frozen commit, D52); E3.S6 (the coai box and bridge); E3.S7a (the GitHub broker with its host-side
  publication gate, D52); E3.S7 (the process runner); E3.S8 (the compliance checker); E3.S9 (P0 and the model smoke).
  *(2026-11-05, D66:)* E1.S7 also builds the two `strict` jobs, the root `.gitignore` and the instruction that the frozen build takes
  precedence; E3.S8 reads the jobs' results; E3.S9 reads its planted violation on the strict job.
- **C2, E4-C:** the tag `prereg/lang-v2-C-<date>`; the process pilot pairs 1 and 3; G-19, G-20; `seed-C`.
- **C3, E5-C:** three process rounds over the ten tasks, plus the bare anchor round (D54, approved by the owner; scheduled and reported as T-L55 checks).
- **C4, E6 final:** the blind adjudication of every group's failing cases; the final record; publication of the references,
  the trap table and the adjudication detail (D49); teardown, archive, retire; this plan and its stories documents promoted
  with their deviations.

**Group A's critical path, stated so that the estimate can be checked, not promised** (consult Fable § 1.4): S8a ii-a →
S8b whole → CP2 → S3 phase A → per task 01, 02, 03, 09: S6 → S2 draft → S2a → D40 gate → document gate + consultant + bias
review, with CP3 after 01 → CP4 → freeze A → E2-A (four tasks) → E3's A slice → E4-A (pre-registration, A/A, pilots) →
round 1. The team lead's 2–3 days hold only if S8b and CP2 take about one day and the four spec chains run in parallel after
CP3. No gate — CP1–CP4, the document gates, the consultant, the bias review — is shortened for the estimate.
*(2026-11-05, at the stop of epoch 7:)* what is left of A's path starts at step 1 of A1's dated order above: E1.S8 and S3 phase A
merged, the marker test first → FORMAT's amendment, S4b then S3b then T-L68, S8c → S2a's tool → task 01's chain. The old estimate is no longer a claim; no new one is made.

*Superseded for the staged series (2026-11-01), kept for its history — the single order:*

0. **Conventions parity audit (E0, other repository, now, in parallel):** the audit table; the missing counterpart rules
   written and released in `dew_flow_conventions`; the snapshot branch cut at that release — before step 4.
1. **Task repository (E1):** the operator creates `dew_flow_ai_language_comparison` (public); **the baseline runner
   `run_samples.py` built and pinned first (E1.S8a), and the execution layer built and probed on the series host
   (E1.S8b, D43: the box, the culture, the program-only scope, the shared loopback namespace, T, G, cleanup)**;
   `FORMAT.md` v2 gated (E1.S8, S9) against the runner before any spec gate;
   the runner copied into `runner/` (D37); per task, in order: the samples drafted (E1.S6), the spec written by the team
   lead, **the draft references written in E1 by the team lead with Sonnet coders into `~/rsd/lang-v2/refs/` (E1.S2a)**,
   **the pre-freeze execution gate** — both draft references run every sample and the task's riskiest-contract scenarios
   through the runner in the final sandbox environment with T, G and the step deadlines in force (D40) — then the gate
   (document gate + consultant + bias review) and the freeze (tag, hashes); the workspaces with `build.sh`,
   `run.sh` ending with `exec` (D38), pins, `PACKAGES.md`, `runner/`; the Node stack rationale and the attribution section
   in the README (D39); toolchains pinned; the locale `de_DE.UTF-8` in the box image (D35); the two cache snapshots; the
   process-arm templates, CI, rulesets, PAT and deploy key, with run bases cut as orphan generated trees (E1.S7, D39, built
   here to v1 §6's design, D32).
2. **References and suites (E2), in this order per task (the circular "references final before suites, suites before
   references" of the first draft is gone):** `FORMAT.md` v2, the runner (E1.S8a), the frozen spec, the samples and the
   two draft references that passed the execution gate (E1.S2a) → **drafts** of the suite, mutants and trap table (E2.S2)
   from the frozen spec, run by the same runner →
   **joint validation**: the references run against the suite and the samples;
   every disagreement is traced to the spec, the suite or a reference and fixed where the fault is (a spec fault by a dated
   amendment) → **references final** (E2.S1: both pass every visible and held-out case, nothing pending; E1.S6's sample
   validation finishes here) → only then **calibration and limit registration** (E2.S3, by D14 on the series machine); a
   spec or `FORMAT.md` amendment after a task's registration voids that task's limits and its calibration runs again before
   the pre-registration; the bias review of each suite; sealed; `leakcheck` clean.
3. **Harness (E3), on E1's execution layer and runner, never a second launch path (D43, §6.1):** `spikes/lang-bench/`;
   drivers and judge as scenario logic over the layer, and `research/module_tests.md` updated with their flows; the
   GitHub broker (E3.S7a, D42) before the process runner; the
   runner's arm, task and round dimensions; D15's env; the widened categories and records; CPC in the record; G-15 … G-19;
   the process arm built here (the coai box and bridge, the process runner, the compliance checker with the `spec unchanged`
   item, P0 and the model smoke — E3.S6–S9, D32); stub tests; a ≤ $0.20 bare smoke on task 1.
4. **Pre-registration (E4.S3)** committed and tagged before the A/A; then the bare A/A, the bare pilot pairs, the process
   pilot pairs, the cap and split derivation (dated amendment), the seed reveal.
5. **Series (E5):** six alternating rounds under the gate.
6. **Record (E6):** `research/lang-v2/RESULTS_<date>.md` generated from the records; blind adjudication; the references,
   the trap table and the specs' final state published; teardown, archive, retire; this plan promoted with its deviations.

## 15. Test plan

*(2026-11-07, epoch 9: the T-L rows of merged stories stay and must stay green; rows owned by dead or Stage 2 stories wait with them; Stage 1's tests are the operative part's *Test plan* and each F story's *Accepted when*.)*

`node spikes/lang-bench/run-tests.mjs` (v1's runner shape: lowered priority, bounded workers; `BENCH_REQUIRE_SANDBOX=1`
turns host skips into failures).

| Id | Guarantee |
|---|---|
| T-L1 | the task repository fetch: a workspace file not matching `hashes-lang.txt` is refused; `workspace-diff` passes on the ten task pairs and fails on a planted extra file |
| T-L2 | the bare workspace of a session holds exactly the task folder's files (no root files, no other stack, no `todo/`) |
| T-L3 | the olympiad judge: fake programs that print wrong output, sleep past the limit, allocate past the limit (on a host with a readable cgroup `memory.peak`), exit non-zero, each get their class; a right one passes; a `memory limit` verdict needs a readable `memory.peak` — without it the allocating program gets no memory verdict, its sampled peak is recorded as descriptive data only and the session carries `memory bound not certified` (T-L29); never `RLIMIT_AS` *(2026-11-02, D56:)* the allocating program is `memory limit` whether the kernel killed it or its runtime aborted with evidence (peak ≥ 50 % of the limit); a stub that fakes the abort line at a low peak is `runtime error` with `abort-below-evidence`. |
| T-L4 | the process driver: a stub daemon that ignores SIGTERM is killed after G (default 5 s, or the spec's override) and reported `stop failed`; one that exits 0 passes; a stub that leaves a child is reported `leftover`; a stub not ready within T (default 10 s) is `not ready`; a step past its deadline is `deadline`; RSS samples are every second ± 100 ms and descriptive |
| T-L5 | the TCP driver against a stub framed-TCP server (the driver's generic capabilities, not a task's scenarios): split frames, pipelined requests, a slow reader (backpressure), each scored by the scenario's expectation |
| T-L6 | the HTTP driver: a scenario with k concurrent identical requests checks the invariant once; a 5xx or a timeout is `fail`, never `not run` |
| T-L7 | limit calibration: given two reference measurements, D14's formula gives the registered limits; a reference failing under them is a calibration failure, not a passing suite |
| T-L8 | trap coverage: every mutant fails exactly its registered cases and no other; both references pass every case; a trap with no catching case is a failure of the table |
| T-L9 | the order: six rounds with arms strictly alternating and the lead drawn from the seed; every task once per round; rids distinct over arm, task, stack, repeat and attempt; masks swap per pair; a task's second repeat in an arm never precedes another task's first *Superseded for the staged series (D44, 2026-11-01): within a group — three rounds, every task of the group once per round in a seeded order, rids distinct over group, arm, task, stack, repeat and attempt, masks swapped per pair, a task's second repeat never before another task's first; no arm alternation across groups; nine data rounds in the series, three per group, plus C's anchor round (D54, T-L55).* |
| T-L10 | the spend gate with the owner's fixed per-session cap of **$50** for every session of both arms (the runner's one cap constant; the broker's budget and the CLI's `--max-budget-usd` both read it) and **no ceiling line**: a session whose settled spend reaches $50 is stopped and classed `killed: cap`, censored at `≥ $50`, and stays in every table; the round rule refuses round k+1 while round k has an unscored pair; six rounds are planned whatever the settled total; the pilot formula's cap is computed and recorded as a cross-check and binds nothing (a fixture where the formula gives $12 still runs the session to $50) *Superseded in part by D7, D53 and D44 (second full reading, 2026-11-01, finding 6): "for every session of both arms" and "six rounds are planned" read per group — the $50 cap for every session of every group, **three rounds per group** (bare in A and B, process in C), each group's registration, projection line and settled ledger lines (D53); a fixture series of one group plans three rounds whatever its settled total.* |
| T-L11 | the record has every per-kind and per-arm field known, `not captured` with a reason, or `n/a (<kind \| arm>)`; spawn and exit timestamps present *(2026-11-01, D44:) `group` is a known field of every session record.* |
| T-L12 | the contamination needles for the known tasks hit in tool inputs and outputs and are reported for a hand reading, never auto-voided |
| T-L13 | offline caches (effect, in a box): with the registries refused by the proxy, `npm install fastify@5.12.7` and `dotnet restore` of the pinned project succeed; a package outside the set fails naming it |
| T-L14 | D15's env keys are in the box and in the scoring box and in the record; `agentEnv` refuses a clash in any case *(2026-11-02, D55:)* D15's base keys are in both boxes; the per-case keys `DOTNET_GCHeapHardLimit` and `NODE_OPTIONS=--max-old-space-size=…` are in the scoring box and the record only, derived from L, and never in the agent box nor in the `build.sh` box — a test plants one in the agent box's environment and fails, and the build step's environment is asserted to carry neither key (judge J7). |
| T-L15 | the classifier: `bash build.sh`, `bash run.sh`, `dotnet publish`, `node --test`, the drivers' commands land in `build_test`; the process categories as v1; the chain rule unchanged |
| T-L16 | `aaVerdicts` G-15 … G-19: each broken fixture refutes exactly its G; G-17 with one reference over a limit is NO GO; G-19 with a changed spec blob is NO GO |
| T-L17 | the record render: the generated blocks come from `data/*.json` only; a typed number is a failure; the licensing paragraph of §9 is present verbatim; known and original tasks are separate tables |
| T-L18 | **CPC:** three attempts costing 2, 3, 4 with one accepted → 9; none accepted → "no correct solution", spend 9, ratio undefined (T-L27); a void attempt is excluded from CPC and included in `CPC_all`; a `killed: cap` attempt counts its censored cost |
| T-L19 | **the overall figures:** ten per-task ratios → the median, the geometric mean (over the defined ratios only, the denominator stated) and the count of C# higher; one task at 100× moves the geometric mean and not the median, and the test asserts both numbers |
| T-L20 | **the spec-freeze guard:** a session whose spec blob at the run base differs from the frozen tag is refused before launch; a process session whose PR changed the spec gets the compliance item `spec unchanged: not met` and the flag |
| T-L21 | the Python suite runner runs unchanged against a C# and a TS stub implementation of one fixture task and reports the same case list; it imports nothing outside the standard library (a test greps the imports) |
| T-L22 | the process-arm compliance checker (built here, E3.S8) computes 18 items for a synthetic run from GitHub JSON, a `coai.db` copy and a stream, the 18th from the spec blob |
| T-L23 | the coai bridge refuses a `tools/call` whose `repoPath`, `committedPlan` or `suspectedFiles` resolves outside the session (symlinks and `..` included) with a JSON-RPC error naming the rule, and passes one inside (v1's T-B1) |
| T-L24 | the process runner: a run base cut from the frozen tag, a session's clone single-branch with a restricted refspec and `--no-tags`, a push outside `run/<rid>/` flagged, `delivered` read from the merge into the base, an undelivered session scored on its PR head and marked |
| T-L25 | the calibration order: `register-limits` for a task is refused while that task's joint validation has not passed (a reference failing any visible or held-out case, or a sample still in draft) or a spec or `FORMAT.md` amendment touching the task is dated after the last validation; an amendment after registration voids that task's limits in `inputs/limits-lang.json` (a test on the pure function) |
| T-L26 | **metric retention (D34):** the v2 record renderer, run over a fixture series, emits every v1 metric column — the four cost views, T1–T5, tokens with the cache split, wall, `model_api`, `active − model_api` **and every category span of the partition**, **visible quality per script** and held-out counts/percent/defects, class, labels, flags, charges, **and the process arm's compliance items** — beside the new ones; a test removes one v1 column from the renderer's list and fails |
| T-L27 | **undefined CPC ratios (§9):** fixtures for the three cases — C# alone 0 accepted, TS alone 0 accepted, both 0 — each gives the task line `undefined (<stack>: no correct solution, k attempts, spend $x)` with no `≥`/`≤`, the overall median and geometric mean over the defined tasks only with the stated denominator, the count "j of k", the undefined line "C# alone … x, TS alone … y, both … z", and the interaction leaving that task out by name when either arm is undefined; the consultant's example (C# CPC = 2, TS spend 9 with 0 accepted) gives `undefined (TS: no correct solution, spend $9)`, never a bound; a stack with no attempt gives `not run` |
| T-L28 | **the registered limit, not the floor, judges a run:** a stub program taking 1.2 s against a registered 3 s limit (floor 1 s) passes; the same program against a registered 1 s limit is `time limit`; the limits come from `inputs/limits-lang.json`, never from the spec text |
| T-L29 | **memory certification refusal (D13, D36):** with no readable cgroup `memory.peak` (a fake cgroup root without the file), `run --what round:1` and every data-session launch are **refused** before any process starts, with the named halt `memory-not-certifiable`, exit 3, the probe's output printed; a **non-data run** (smoke, fixture, a pilot the operator allows) on the same host proceeds, carries the flag `memory bound not certified`, and its record holds **no** memory pass (the memory field is `not certified`, the sampled curve present and labelled descriptive); with `memory.peak` present, a data session records a certified memory pass or a `memory limit` verdict and `accepted` includes it; the series record states the certifying host; a CPC fixture with and without the flag on non-data runs gives the same denominators (data sessions only) |
| T-L30 | **the scope (D36), effect tests on this host:** the scorer starts a stub in its own `systemd-run --user --scope` with `MemoryMax` and `MemorySwapMax=0`, outside `bwrap`, and reads back that the stub and a child it forks are in the scope's `cgroup.procs` and that the driver is not; fixtures — an **allocating child** (the parent stays small, the child allocates past the limit: `memory.peak` and `memory.events` show it, the run is `memory limit`); an **OOM-killed child whose parent exits 0** (`oom_kill` ≥ 1 in `memory.events`: the run is `memory limit`, never a pass, whatever the exit status); a **lingering child** (the parent exits, the child stays: `leftover`, and the scope's `cgroup.procs` is the evidence); `memory.peak`, `memory.events` and the lifecycle record are kept before the scope is removed *(2026-11-02, D56:)* a child that prints a runtime's abort line and exits 134 while the parent exits 0 with the expected output is a pass flagged `child-abort-line`; the same child killed by the kernel (`oom_kill ≥ 1`) is `memory limit`. |
| T-L31 | **`run.sh` execs (D38):** for each stack's template, the pid the harness starts is the program (the process's `comm`/`exe` is `dotnet` or `node`, not `bash`), it has no bash parent inside the scope, and a SIGTERM sent to that pid is received by the program (a stub that logs the signal); a template whose `run.sh` lacks `exec` fails the test |
| T-L33 | **the execution layer's network layout (D43):** a driver and a stub program started by the layer have equal `/proc/<pid>/ns/net` ids, different from the host's, and different cgroup membership (`/proc/<pid>/cgroup`: the program in its scope, the driver not); `lo` is up and the driver reaches the program's port on `127.0.0.1`; no other interface exists in the namespace; the same test body runs for a C# stub and a TS stub and reports the same facts |
| T-L34 | **the execution layer's isolation and cleanup (D43):** the program cannot read a driver-only sentinel file (a path outside its bwrap binds); killing the program's scope removes every descendant (a forked child included) while the driver survives and collects `memory.peak`, `memory.events`, the exit status and the transcript; after cleanup no process, scope or namespace of the run is left; identical for both stacks |
| T-L35 | **the GitHub broker (D42):** from inside a process box, clone of the run base, fetch and push of the run's own branch, PR create and update, and reads of the own PR's checks and comments succeed through the broker, and the pushed branch is mirrored to GitHub; a fetch of the bench repository, a read of the task repository's `main` README (the attribution), a fetch of another run's branch, a read of another run's PR, and a redirect to a host or path outside the broker are each refused with a named error and logged; **a fetch by the SHA of a task-repository `main` commit fails (the object is not in the run's bare repository); an unshallow fetch (`--unshallow`, `--depth` widened) gives only the run base**; the PAT appears in no box environment or argv; a direct CONNECT to `github.com` is refused by the proxy |
| T-L36 | **the shared layer's invalidation rule (§6.1):** a changed layer identity marks every D40 gate record and every registered limit calibrated on the old identity as `stale`, and `register-limits` and `run` refuse until the gate is re-run and calibration re-registered (a test on the pure function over fixture records) *(2026-11-01, D50:) exercised for real at group B, for A's four tasks on B's identities.* |
| T-L37 | **the needle matching rule (D18, change 4.1):** `RESP` does not match "response", "responses" or `resp_code`; `IOI` does not match the base64 `eyJiOiJ`; `Werewolf` matches "Werewolf" and "the Werewolf problem" but not "werewolf" (case) nor "Werewolves" (boundary); `redis.io` matches as an exact substring inside a URL; a sample transcript field FORMAT marks as normalised (a JWT) is skipped while the same bytes in an un-normalised field are scanned; the generated run-base tree with the spec `09-switched-routes.md` and FORMAT's F-BYTES-W sentence gives zero hits, and the same tree with a planted "Werewolf" in a spec gives one |
| T-L32 | **the declared culture (D35), inside the actual boxes:** in the agent box and the scoring box, for both stacks, `LANG` and `LC_ALL` are `de_DE.UTF-8` and `locale -a` lists it; a .NET probe prints `CultureInfo.CurrentCulture.Name` = `de-DE`, `double.Parse("1.5")` = 15 under the current culture and 1.5 under `CultureInfo.InvariantCulture`; a Node probe prints `Intl.DateTimeFormat().resolvedOptions().locale` starting `de`; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` is unset; the same values appear in the session record |
| T-L38 | **the olympiad path across runner manifests (D50):** the olympiad modules of A's manifest and B's are byte-identical (an AST and hash comparison) and give identical verdicts and identical JSON results on the olympiad fixtures; a planted change in one olympiad module fails the test, while a change in `rs_net` alone passes it |
| T-L39 | **A's frozen inputs across the tags (D45):** A's spec blobs, samples and FORMAT's `common` and `olympiad` sections are byte-equal at the A and B freeze tags; an olympiad spec passes the conformance check under both FORMAT shas; a planted edit to an A spec, or to an `olympiad` section, at the B tag fails *(2026-11-03, D60, D61:)* "FORMAT's `common` and `olympiad` sections" are those A's frozen FORMAT classes so (T-L56), never B's class table; A's four workspaces' `GRAMMAR.md` is byte-equal at the two tags (T-L57) |
| T-L40 | **cross-group alias drift (D47):** a fixture series whose first group-B session resolves the alias to a model id other than the one group A's first session set stops with the alias-drift class — though a separate series for B would have taken that id as its own baseline; with equal ids it passes |
| T-L41 | **duplicate enrollment (D44, D45):** a (group, task, stack, repeat, attempt) already enrolled is refused a second enrollment, and a session record carrying two groups is refused; A's replication sessions in B are distinct records with group B |
| T-L42 | **resume against the wrong manifest (D45):** `resume` over a series directory whose group manifest differs from the selected one is refused before any launch, naming both; a task whose identities changed (the layer, the runner, FORMAT) refuses launch until its D40 gate and calibration were re-run under them |
| T-L43 | **the asserted memory view (D51):** through the final execution prefix at `MemoryMax` 256 MiB and 512 MiB, the probe asserts — by the policy D15 records — whether the cgroup is visible in the box and what .NET (`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`) and Node (`process.constrainedMemory()`, V8's heap limit) believe their limit is, against `prog/memory.max`; an allocation-churn fixture with bounded live data completes in both stacks; an over-limit fixture is `memory limit` with `memory.peak` and `memory.events` kept; a probe that only prints a value it should assert fails the test *Revised 2026-11-02 (D55; the text above kept):* the probe asserts, against the pinned policy, that the cgroup visible in the box is only the program's own (`/proc/self/cgroup` is `0::/`), .NET's `TotalAvailableMemoryBytes` = ⌊0.9·L⌋, Node's `constrainedMemory()` = L and V8's `heap_size_limit` lies in [0.9·L, L); the churn fixture completes in both stacks; the over-limit fixture is `memory limit` with `memory.peak`, `memory.events` and `runtimeAbort` kept, and its peak/L is recorded per stack and limit (a ratio within 10 points of 50 % is a finding before CP2 — judge J2); the **object-graph regression** (live ordinary objects at 70 % of L plus 1 GiB churn) passes in both stacks at both limits; the mixed workload (objects 45 % + typed arrays 35 % + churn) is **run and recorded, never asserted** — it fails in Node under every setting (T21); so is Node's medium-buffers at 256 MiB, run and recorded, never asserted (kernel `oom_kill` at peak = L in every measured setting: T21, change 6.1); a policy other than the pinned one is exit 3 `memory-view-mismatch`; no promise that every bounded workload passes. |
| T-L44 | **exact decimals (D52, V2):** two distinct 30-digit values compare unequal; a matcher that calls `Decimal.normalize()`, or compares under a 28-digit context, before equality makes the test red; `2` and `2.0` still compare equal |
| T-L45 | **the broker's publication gate (D52):** with the task repository's client-side hook disabled, a run that tries to publish forbidden content — in a commit, a commit message, a PR title or body, a comment — makes zero outbound publication calls; each attempt is refused with a named error and logged; clean content is mirrored or posted |
| T-L46 | **the bound run base (D52):** the cut binds the approved frozen commit, the generated tree and the final orphan sha; the needle scan runs over that exact orphan; its parent list is empty, checked locally; a build from any other commit is refused before any push |
| T-L47 | **exact push-check exemptions (D52):** an aid shingle joined by `/` or `-` into one whitespace token is refused; a recombination of published fragments that is not on the approved list is refused; an approved exemption passes only at its approved place and with its pinned hash |
| T-L48 | **the group manifests (D45):** four specs freeze under A's tag while the six others are drafts; a FORMAT update for B cannot change A's resolved artefacts; a fresh D40 gate pass supersedes a stale record while the stale one stays in the file; results under two contracts never pool silently — the record renderer refuses it |
| T-L49 | **the calibration sequence (D58):** a pure-function test over fixture measurements — a provisional run at F gives peaks, the candidate is max(F, 2 × the larger peak) rounded up to a MiB, a confirming run under the candidate with heap settings ⌊0.9·L⌋ registers only when 2 × its peak ≤ the candidate; a confirming peak above half the candidate gives one more iteration; a third is refused as a finding; `register-limits` refuses an evidence run whose limit or settings differ from the registered value; the time limit's evidence names the registered memory limit it ran under |
| T-L50 | **the abort evidence boundary (D56):** through the runner and the real prefix, with L a whole number of MiB (D58, so L/2 is whole and `peak · 2 ≥ max` is exact), a program that prints .NET's line and exits 134 at a peak of L/2 is `memory limit` (`runtimeAbort: dotnet`); the same at L/2 − 1 byte is `runtime error` with `abort-below-evidence`; the V8 line the same way; the line with exit 1, or exit 134 with another message, is `runtime error` without the `abort-below-evidence` flag (a child's abort line under any non-zero exit other than 134 still carries `child-abort-line`, D56); `oom_kill ≥ 1` is `memory limit` at any peak; the two flag columns are in the JSON result and in the record renderer, and removing one from the renderer's list fails `A_removed_metric_column_fails_the_renderer_list`, the assertion inside T-L26's metric-retention suite |
| T-L51 | **the floor probe (D57):** the peak-sized workloads (each tuned per stack to a `memory.peak` of 128 MiB ± 8 MiB at 512 MiB, the tuning recorded; objects-only, and objects with a quarter byte arrays; 1 GiB churn) run in both stacks through the final prefix at 256 and 512 MiB, three times each, and the probe records every verdict and peak; a tuned peak outside ± 8 MiB is refused; the rule's pure function reads the objects-only composition — 256 MiB when every run passes, 512 MiB when a stack fails at 256 and passes at 512, a finding when a stack fails at 512 — and a byte-array-only failure in Node is a T21 finding, never a floor; `register-limits` refuses while `floorMiB` is absent from `inputs/limits-lang.json` |
| T-L52 | **the abort rule in part ii-b (D59):** a daemon stub that aborts with .NET's line (exit 134) at a peak above half of L after readiness is `memory limit` with `runtimeAbort: dotnet`, and a web stub with V8's line the same; `rs_abort.py` and its evidence function are byte-identical across A's and B's manifests (T-L38's module set names them) |
| T-L53 | **a probe record is never overwritten (D59):** the probe writes `inputs/exec-probe-<date>.json`, a second run on the same day writes `-2`, a third `-3`; a run pointed at an existing path refuses (exit 3) and changes no byte of it; after a probe run the records of 2026-11-02 hash-equal their committed bytes |
| T-L54 | **the memory causes reach the record and the adjudication (D56, §9):** over a fixture series the record renderer emits the `memory limit` counts by cause (kernel `oom_kill`, .NET abort, V8 abort) and the counts of `abort-below-evidence` and `child-abort-line` per task, stack and arm, from `data/*.json` only (a typed number fails, as T-L17); E2.S5's adjudication packets show the cause and the two flags of a flagged case with stack and arm masked |
| T-L55 | **the bare anchor round in C (D54):** `run --dry-run` for group C prints three process rounds and one bare anchor round over the ten tasks (n = 1 per task and stack) interleaved among them from `seed-C`; every anchor session carries `role: anchor` and the `anchor` ledger line; the renderer reports them only as the drift check against B per task and refuses to pool them with bare data (D46's list); a fixture series for C without the anchor round fails the schedule test |
| T-L56 | **A's frozen classification (D60, D64), on A's real FORMAT:** with FORMAT at A's freeze commit as the reference, (a) a B FORMAT that edits a line in section 4 (`daemon-web`) and adds a changelog line passes; (b) a B FORMAT that relabels section 3 from `common` to `daemon-web` in its class table and edits one line of it is refused, naming the section and A's class; (c) a B FORMAT with section 1 deleted is refused, naming it; a B section added as `common` is refused; a changelog in which an A line changed is refused; the header rows may differ. The check reads the protected set from A's FORMAT — a test that reads it from B's class table makes (b) pass, and that is the point. *(Judges J1, J5, J6, 2026-11-03: the cases named, owner E1.S9 — `tools/format-classes.mjs`, `test/format-classes.test.mjs` — green before S9's gate round on the draft, at CP4 on A's real FORMAT and at CP4-B at B's tag:)* `A_daemon_web_subsection_edited_in_2_5_passes` (J1); `An_olympiad_subsection_edited_in_2_3_is_refused` (J1); `A_common_section_added_in_B_is_refused`; `A_changed_A_changelog_line_is_refused`; `The_header_rows_may_differ_between_A_and_B`; `A_class_row_appended_for_a_new_B_subsection_passes` (J5); `A_B_section_inserted_before_section_7_is_refused` (J5, the renumbered heading). *(plan round `fae12fd9`, 2026-11-04, finding s 2 and 3):* **before A's freeze exists** the reference is a fixture — the draft FORMAT committed as a stand-in A inside the test's own temporary repository — so the cases run on real FORMAT bytes at S9's gate round; at CP4 the reference is A's freeze candidate commit, and at CP4-B it is `spec-frozen-A-…` itself. Added case `A_B_subsection_inserted_inside_a_protected_mixed_section_is_refused`: a `###` placed between two A subsections of section 2 is refused; one appended at the end of section 2 with the next free number and a `daemon-web` class row passes (the appended-row case) |
| T-L57 | **the grammar per task (D61):** the extractor's output for FORMAT at A's freeze and at B's differs (the header names each FORMAT's sha); A's four workspaces at B's tag hold A's `GRAMMAR.md` byte for byte; B's six hold B's; `format.json` names per group the grammar's sha and its tasks, and a task whose workspace grammar is not its group's is refused at the freeze |
| T-L58 | **FORMAT's bounds equal the code's (D62, D63):** the figures in F-STDERR and 4.6 equal `STDERR_KILL_BYTES` (`runner/rs_exec.py`) and the runner's `>` comparison; F-ENV's `TZ` equals the layer's `BASE_ENV.TZ`, and every key F-ENV names as not settable is refused by `boxEnv` while `TZ` is accepted; `F-PROCS`'s figure equals `pids.max` in `lib/exec-layer/scope-entry.sh`, and the group manifest's `pidsMax` equals it for both stacks; a planted different figure on either side fails *(Judges J2, J6, 2026-11-03:)* the `env` case — a sample with `env lc_all C` or with a heap-limit key in any case spelling is refused by the conformance check (E1.S8) and by `boxEnv`; `env TZ Europe/Berlin` and a key outside the layer's lists pass both; the check reads the lists from `env.mjs` (a retyped list in the checker makes the test red) — E1.S8 and E1.S9, before S9's gate round. The `pidsMax` half — the group manifest's `pidsMax` equals `pids.max` in `scope-entry.sh` for both stacks, a planted 1024 fails — is E4.S3's, green before each group's `prereg/lang-v2-<group>-…` tag |
| T-L59 | **the gate harness's CP2 conditions (D63 (1)–(3)):** a pre-freeze run without `data: true` or without a stack is refused before any case; its gate record holds `treeSha256`, the policy and the stack; a runner whose manifest differs from the pin (one byte changed, version unchanged) is refused before the first case; an exit 3 of the holder or the launch is re-run and recorded as `environment`, never `fail`; a case whose only fault is a `fork` flag line is not failed *(Judge J6, 2026-11-03:)* the gate record's fields, owner E1.S2a: `treeSha256` (the layer's identity as `runCases` returns it), `memoryPolicy` (the pinned policy's name), `stack` (`csharp` or `node`), `runnerManifest` (the hash the tool checked against `inputs/specs.d/runner.json`, never the version string), and `attempts` (`[{ exit, class }]`, an exit 3 as `class: environment`); a record missing any of them is refused at the freeze; green before the first D40 gate *(2026-11-05, D65:)* the record also holds `buildContract` (T-L60) |
| T-L60 | **The submission contract (D65, D64's pair), on the real templates in the build box:** permitted — `<AllowUnsafeBlocks>` in `App.csproj` with a program that needs it builds and passes, with a deviation listed; a listed package added passes; refused in effect — a frozen `tsconfig.json` with `strict` off, or a C# `Directory.Build.props` that changes the policy, is restored, and `const s: string = null;` or `string s = null;` fails the build; a `build.sh` of `exit 0`, a removed frozen file, and a frozen file replaced by a link are each restored and the real build runs; the stack's folders at any depth are not in the copy, while a Node `src/bin/util.ts` stays and builds (`A_single_folder_list_for_both_stacks_would_fail_a_node_bin_folder_and_that_is_the_point`); a package planted in the session's own cache is not found (`NU1101`, `ETARGET` or `ENOTCACHED`), since the box binds the registered snapshot; a nonzero `build.sh` or the 600 s bound is the submission's result, while a box that cannot start, a failed holder, a missing or mismatched snapshot and a host fault give `verdict: none, reason: environment`, at most two re-runs, each in `attempts`, and no rejection (`An_environment_fault_in_the_build_is_never_a_rejection`). `A_regenerated_project_file_would_fail_a_correct_program_and_that_is_the_point`; `A_refused_project_edit_would_fail_a_correct_program_and_that_is_the_point`. The identity changes when a template file, `lib/submission.mjs`, `lib/workspace-box.mjs`, `NPM_OFFLINE_ENV` or a snapshot hash changes, and `gate-staleness.mjs` marks a record stale (T-L36); a change to `lib/deviations.mjs` changes its own recorded hash and marks nothing stale. Owner E1.S3b; green before the first D40 gate, read at CP4 |
| T-L61 | **The deviations (D65), a pure function over fixtures:** each of the 21 rows of E1.S3's table, and each Node peer (a `scripts` block, a `file:` item, a second `tsconfig.json`, `@ts-nocheck`), gives its category and path; a clean hello gives none, and a project with only a listed package added at its exact version gives none, as the added item is not a deviation; `package.json` unchanged with a lock whose `resolved` is `file:faketsc` gives a row, and so do a `link: true` entry and an integrity outside the registered set (`lockProblems_alone_would_pass_a_linked_compiler_and_that_is_the_point`); `null!` and a postfix `!` are counted by one rule in both stacks, and `!=` and a prefix `!` are not; plain `npm install --offline pino` under the template's `.npmrc` writes an exact version and gives no row, a range written by hand gives one, and `dotnet add package` is measured the same way in the build box; a link to a folder is not followed, and a cyclic link and a dangling link each give a row under (e) and no scorer error (`A_cyclic_link_in_the_tree_is_a_deviation_not_a_scorer_error`, `A_dangling_link_in_the_tree_is_a_deviation_not_a_scorer_error`); the counts are per stack; a fixture with deviations and all cases passed stays `accepted`, and the function has no path to `accepted`; a metric removed from the renderer's list fails T-L26. Owner E1.S3b; the renderer's half is E6.S1 |
| T-L62 | **Scripts cannot change the build (D65):** with the template's `.npmrc`, a `sed` on `tsconfig.json` as `preinstall`, `install`, `postinstall` or `prepare` leaves the file as it is and TS2322 fails the build; `The_control_without_ignore_scripts_still_exits_0_and_that_is_the_point`, with the frozen file in place at the start; all three kinds pass `npm ci --offline`, `tsc`, ESLint and `npx --offline`; no entry of the three locks and of the whole offered set's lock has `hasInstallScript`; `npm test` runs its own script. Owner E1.S3b |
| T-L63 | **The offered set works (D65, change 7.1), at the supersede:** every offered package, built one at a time in each applicable template in the box with no registry, gives a row of `inputs/package-matrix.json` (`works` or `unsupported`, with the code); `JwtBearer` in the olympiad kind has its row; `PACKAGES.md`'s sentence on unsupported combinations is derived from that record, and a typed sentence fails; a minimal offline test-host example passes in the web kind in both stacks (`WebApplicationFactory` with `Microsoft.AspNetCore.Mvc.Testing`; Fastify's `inject()` with no added package), through `dotnet test --project tests/Tests.csproj` and `node --test`. Owner E1.S4b; green before the first D40 gate |
| T-L64 | **The runner's text and fields (D67 (4), OP-3):** the push check's agent-visible scan over `runner/*.py` finds no plan id and no stack name, with the detection strings of `rs_abort.py` kept by exact approved entries (D52 (3)), and it now runs in the workspace check, where E1.S3 left it out; a comment-and-docstring change is proved: `ast` of every module, docstrings removed, equals the old one but for the version constant, read at part 1's commit, because part 2 changes `rs_report.py`; the case record holds `cpuMs` from the program's cgroup and the RSS curve from a sampler in the runner; `not captured` stands only for one case at run time, with its reason, never a zero and never a story's outcome (D34 stands; no metric is dropped); `pin-runner.mjs --check`, `specs-record.mjs --check` exit 0 on the new manifest; the probe is re-run as a new dated file (T-L53), and CP2's addendum names old and new manifests. Owner E1.S8c; before the first D40 gate *(2026-11-07, D74: superseded in part. The comment-and-docstring half is done by #173, with its test (recorded in F1.S1 (f)). The `cpuMs` and RSS-curve half, with its "no metric is dropped" clause for those two fields, moves to follow-up work: they were never measured, so no record loses a value. The text above is kept.)* |
| T-L65 | **One acceptance policy (D66):** the same slip (an unused local; a call not awaited) in a hello program of each stack exits 0 in the frozen build and fails no case; the same type-safety error (`string s = null;`, `const s: string = null;`) fails the build in each; a planted lint or analyzer violation is green in the frozen build and red in `strict · <stack>` (E1.S7; live in E3.S9's P0); the generated tree's instructions hold the precedence sentence, a test greps it; a session's `accepted` does not read the strict column (the same fixture with it red and green); the record has the `strict job` column in the process arm and the lint invocations and outcomes in both arms. Owners E1.S3 (the build half, built), E1.S7, E3.S8, E6.S1 |
| T-L66 | **The contract read end to end, before a freeze (D67):** on synthetic hello programs of both stacks and a synthetic suite, with no held-out content: seal, clear, `lib/submission.mjs`, build, `runCases` under a limits file and `data: true`, the runner's JSON, then a record of every field that §4.3's "Also recorded", D13's `accepted`, T-L3's verdict classes and T-L11's list name. Each field has a source now, or a named story that builds it before that group's tag; a field with neither fails, and `not captured` is no answer here. **Owner E1.S2a**, no new story: it spans A and B, builds through the module, runs `runCases`, and merges after S8c's re-pin. It runs at §14 A1's step 4, not in E1.S3b: a dry run read at CP4 for olympiad, and at CP4-B for daemon and web. It does not build E3: what it needs of the scorer's aggregation is a test helper, and E3.S4 replaces it |
| T-L67 | **The workspace commit (D67 (3), D45):** a group manifest without it is refused; it is a descendant of the group's tag and its spec blobs equal the tag's, and a planted different blob is refused; `inputs/hashes-lang.txt`'s hash matches; D65's restore reads at that commit; at B, A's four workspaces at B's workspace commit equal A's workspace commit byte for byte (T-L39's and T-L57's "at the two tags" read so); a run base from a commit other than B's workspace commit is refused (T-L46); C freezes no spec, so C's manifest names B's tag and B's workspace commit. Owners E1.S2 and E1.S3 phase B (the commit), E3.S1 (the manifest) |
| T-L68 | **CI can run the frozen build (§6.2 row 11), one recorded dry run with no GitHub write, not a test, since a network test does not fit "nothing skipped":** the CI job's environment is named, `NUGET_PACKAGES` and the npm cache path, and the dry run uses exactly those, in a clean environment with a network; a seed step that changes no template file fills them from the registered recipe's pinned versions, with each package's integrity checked against the manifest, never mutable resolution; then the unchanged `bash build.sh` exits 0, every kind and stack, and `strict` passes on a clean tree. A failed seed is an environment failure of the job, not a verdict on the submitted tree. The real hosted run is E1.S7's acceptance, not this dry run. The team lead owns it, and it runs after E1.S3b, because it reads S3b's `.npmrc` and `build.sh` bytes; recorded in `inputs/checkpoints.md` before the first D40 gate (§14 A1's step 2); a failure that needs a template byte changes that byte then, not after A's freeze; E1.S7 builds the real job from it |

*(2026-11-05, the rows above kept:)* **T-L14** also lists the build box's keys of D15's dated line, and the agent box has no notifier key. **T-L15**'s literals gain `dotnet test --project`, `python3 -I runner/run_samples.py`, `eslint` and `npx --offline eslint`, the last two also counted as lint invocations (D66). **T-L26**'s list gains `deviations`, `strict job` and the lint invocations. **T-L36**'s identities gain the build contract. **T-L39** and **T-L57** read "at the two tags" as T-L67 says. **T-L1**'s workspace list includes the workspaces at the workspace commit. **T-L20**'s halves have the milestones of D64's dated reading. **T-L46** is bound to B's workspace commit.

Live: the bare smoke (E3.S5), P0 and the model smoke (v1's, on this repository), the A/A and the pilots (E4), the by-hand
G-15/G-16/G-19 lines, the round-1 read-back of one pair per arm.

## 16. Definition of Done

*(2026-11-07, epoch 9: **superseded whole** by the operative part's *Definition of Done (Stage 1)*; the items below that concern the process arm return with Stage 2. The text below is kept.)*

- [ ] Every input but the stack and the arm is pinned in the harness; one session per stack per arm in round 1 was read
      back by hand to prove the arms differ only there (measurement rule 1).
- [ ] The judge, the drivers, the suites and the process instruments were hand-checked (G-15 … G-19) before their numbers
      appear (rule 2); the specs were gated and frozen before any paid session.
- [ ] The conventions parity audit ran and its release is pinned in the task repository before the pre-registration (D23).
- [ ] `prediction.md` was committed before the pilot and `seed.txt` after the A/A; the report puts observation beside
      prediction (rule 4).
- [ ] T1–T5 held for every data session, or the breach is in the report with what was done.
- [ ] Every cost carries its basis; pilot, A/A, data and void spend on separate lines per arm; spend ≤ $1000 or the stop and
      the missing rounds named; every attempt, void and re-run included, is in the record (D26). *("Spend ≤ $1000 or the stop"
      is superseded by D7 — no total ceiling — and by D53: the ledger carries the settled spend per group and per arm.)*
- [ ] One memory contract held (D13, D36, §4.3, T-L29): every data session ran on the certifying host named in the
      pre-registration, in its own systemd user scope, with `memory.peak` and `memory.events` kept (T-L30), and its
      `accepted` includes the certified memory claim; the halt `memory-not-certifiable` refused every data launch on a
      host without `memory.peak`; the flag `memory bound not certified` appears on non-data runs only, never on a data
      session, and no CPC denominator depends on it.
- [ ] The declared culture `de_DE.UTF-8` held in both boxes for every session (T-L32; D35), and `run.sh` exec'd the
      program in every workspace (T-L31; D38).
- [ ] CPC is the headline, computed as §9 says; "no correct solution" outcomes are present as spend, never as a CPC or a
      ratio bound; the overall figures are the median, the geometric mean and the count over the defined ratios with their
      denominators stated and the undefined line beside them, per arm; known and original tasks are shown apart.
- [ ] Every v1 metric is in every v2 record beside the new ones (D34, T-L26 green in E6.S1): the four cost views and T1–T5,
      tokens with the cache split, the time partition with every category span, visible and held-out quality, class, labels,
      flags, charges, the compliance items.
- [ ] Time is partitioned (Σ = wall); no span sums; scoring time apart from agent wall; active time beside wall in the
      process arm.
- [ ] Every failing held-out case was classed blind; the trap coverage table, the references and the specs are published
      after the series.
- [ ] The report names the task repository sha and freeze tag, the parity release sha, the harness identity, the prereg tag,
      the CLI version, the toolchain checksums, the cache hashes, the machine and the dates (rule 6); its conclusion is no
      wider than §9.
- [ ] The public repository holds no secret, transcript, held-out case or link in a workspace (`leakcheck` clean on every
      push and PR); credentials torn down after the series.
- [ ] `research/module_tests.md` lists the harness's runner command and every scenario flow as covered or not covered with
      a reason, and says what the harness does not prove (E3.S2).
- [ ] Limits were registered only after the joint validation made the references final, and every spec or `FORMAT.md`
      amendment after a registration voided and re-ran that task's calibration before the pre-registration (G-17).
- [ ] **Follow-up outside this plan's commits:** v1's parent plan [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md)
      carries a STATUS line saying its process arm moved to this plan (a status update, not a plan change), and v1 is closed
      after its bare arm (D32).
- [ ] `research/lang-v2/` and `research/README.md` updated; the module document describes the extension; this plan and its
      stories documents promoted with deviations; the worktree removed.
- [ ] *(Added 2026-11-01, D44–D53.)* Three pre-registration tags exist, `prereg/lang-v2-A-…`, `-B-…`, `-C-…`, each committed
      before its group's first paid session, and one spec freeze tag per group; no later tag altered an earlier one (T-L39).
- [ ] One alias baseline for the series, set by group A's first session, and one CLI copy (D47); every forced change is a
      dated instrument amendment with its A/A.
- [ ] The ledger carries settled lines per group (data, pilot, A/A, void, re-run) beside each group's projection (D53).
- [ ] No figure of the record pools groups (D46); the drift table is present; the ten-task bare headline is B's; the process
      effect and the interaction carry T16's window sentence.
- [ ] Every interim record carries counts and rates only, with no trap content (D49).
- [ ] The final record names every group's identities — the group manifest, the layer, both runner manifests, FORMAT's shas,
      the CLI copy, the model id, the toolchains, the host — and its dates.
- [ ] *(Added 2026-11-02, D55–D59.)* One memory policy for the series — the own-cgroup view, .NET's hard limit and Node's old-space
      flag at 90 % of the registered limit — pinned at A as an identity, asserted by the probe through the final prefix at 256 and
      512 MiB (T-L43), the same in every group; the per-case keys never in the agent box (T-L14).
- [ ] Every `memory limit` names its cause; the 50 % evidence threshold and the flags `abort-below-evidence` and `child-abort-line`
      are in the pre-registration, counted per stack and read by the blind adjudication (T-L50, T-L54); the rule is the same in daemon and
      web scoring (T-L52).
- [ ] The floor was settled by the floor probe before the first registration (D57, T-L51), and every registered limit's evidence is
      a run under that limit and its settings (D58, G-17, T-L49); every memory figure is in MiB.
- [ ] The probe and experiment records of 2026-11-02 are unchanged with their identities; every later probe is a new dated file
      (D59, T-L53).
- [ ] *(Added 2026-11-03, D60–D64.)* FORMAT's sections carry five classes, sections 4 and 5 `daemon-web`; every B amendment was
      checked against A's frozen classification (T-L56); A's four ran in B on A's `GRAMMAR.md` (T-L57).
- [ ] FORMAT's F-STDERR, F-ENV, 4.6 and `F-PROCS` state the runner's and the layer's real bounds (T-L58); `pidsMax` is in every
      group manifest; CP2's conditions held before every D40 gate (T-L59; D63).
- [ ] Every freeze or immutability rule had its permitted and its refused change shown on real files before its freeze, read at
      CP4 and CP4-B (D64).
- [ ] *(Added 2026-11-05, D65–D68.)* What was scored is what was submitted: the frozen files restored, no project file regenerated,
      no edit refused, the registered snapshot read-only (T-L60); the gate, calibration and scoring built through one module
      (`lib/submission.mjs`), and its identity is in every gate record and group manifest.
- [ ] `deviations` is in every session record and in each group's pre-registration before its first paid session, reported per
      stack beside `accepted` and never in it (T-L61, T-L26), and the report carries T23's sentence.
- [ ] The Node `.npmrc` holds `ignore-scripts=true` and `save-exact=true`; no package of the offered set needs an install script (T-L62). The offered
      set was shown to work at the supersede, and `PACKAGES.md` says where a combination does not (T-L63).
- [ ] One acceptance policy for both arms and stacks (D66, T-L65); the process arm's `strict` jobs ran and are reported beside
      `accepted`; T15's text and the workspace instruction say the frozen build takes precedence; D6's claim is "available", and the
      record reports the lint invocations and outcomes.
- [ ] FORMAT's amendment is gated before the first D40 gate: what is scored (F-OFFLINE), the tag form, MiB, the `Freeze rule` row; its pins moved with it.
- [ ] The runner has no plan id and no stack name in comments, re-pinned, the probe re-run, CP2 addended; every field of §4.3's
      per-case record has its source (T-L64, T-L66); `not captured` stands only for one case at run time, with its reason, and no metric is dropped (D34).
- [ ] T-L66 was read at CP4 and CP4-B before each freeze; every group manifest names its workspace commit (T-L67); §6.2's rows
      each closed, or re-opened by a dated line.
- [ ] Every merge of a v2 story met D68: one whole completion run with every test passed, nothing skipped, and CI's required checks
      present and passing for the head sha; no test or file re-run replaced it; every failure on the way is on S5's list with its load;
      a test that failed in two consecutive whole runs was diagnosed before any further run; CP4 and CP4-B read S5's list.

## Deviations

None yet. Filled at promotion: what shipped differently from this plan, and for every "impossible", what was CHECKED.

## 17. Open questions

*(2026-11-07, epoch 9: the questions about the process arm, the process wall (Q-E) among them, are Stage 2's; Stage 1 opens none here, the pairs per task being settled by D73.)*

Not settled by this plan; the operator or the named epic decides. No answer is invented here. Removed by the afternoon's
instructions: the former Q2 (public from day one — D31; the conventions pin — D23) and the former Q12 (the ceiling cuts
whole round pairs — D10, now with two arms). Removed by the coai plan-gate round on `aa5e413`: **Q-B** (v2 builds the
process-arm pieces itself; v1 closes after its bare arm — D32) and **Q3** (Werewolf option (a) — D33). The budget frame's
default when the operator has not answered is now fixed (§12); Q-A keeps only the operator's choice itself.

- **Q-F — closed by the owner, 2026-10-31: the narrowing is approved; the run-bound GitHub broker (D42) stands as
  written.** The owner noted that every repository stays public — privacy was never the aim; the broker exists because the
  process agent could otherwise read the trap history in the public bench repository, which the bare agent cannot reach.
  Epoch 4 is approved `by owner` (narrowed: the process arm's GitHub capability).
- **Q-A — closed by the owner, 2026-10-31: no total ceiling.** The $1000 is removed; the six rounds of both arms run as
  planned; a fixed per-session cap of **$50** stays as a runaway guard (a capped session is censored, never hidden); the
  pilot's cap formula is a reported cross-check that does not bind (D7, §12).
- **Q-G The bare anchor round in C — ANSWERED by the owner 2026-11-01: yes (D54).** *Superseded heading: "the owner's call, not decided here (2026-11-01)".* One bare round over the ten
  tasks (n = 1 per task and stack, ≈ 20 sessions), interleaved among C's three process rounds, never pooled with bare data,
  reported as a drift check (the anchor against B, per task). It is the only way to **measure** the B→C drift instead of
  assuming it (T16). Cost: ≈ $100–400 at §12's guesses, less at the exploratory rates for its olympiad part (§12). *(Superseded by the answer, 2026-11-02 wording: it was offered as "a named option with its cost, not a recommendation to spend";
  since the owner's yes of 2026-11-01 it is a mandatory part of group C — D54, §14 C3, E5, T-L55.)*
- **Q-H The narrowing of epoch 5 — APPROVED by the owner 2026-11-01** (epoch 5's line says `by owner`). *Superseded heading: "the owner's approval, not decided here (2026-11-01)".* The staged order loses D10's arm
  alternation, the control of T16; nothing else got smaller (the re-review record for epoch 5). Under plan-change-control step 8
  the owner approves first, and epoch 5's line then says `by owner`.
- **Q-C The bias review sends held-out suites to coai's vendors** before sealing (D25). The agents under test never see
  them, but other vendors' models do. Accept the residual, or review the suites by an in-house Opus judge only and send the
  vendors the specs alone?
- **Q-D The process agent's clone** holds the generated tree of the frozen tag as one orphan commit (D39: no attribution,
  no credit history): both stack folders (empty templates) and all ten specs. Accept the other tasks' specs as a property
  of the arm (§7), or generate per-task run bases that hold only the task's spec?
- **Q-E Process-arm caps and walls.** v1 planned $35 / 240 min for minigit; v2's tasks are 10–60× larger in agent time. The
  pilot sets them (G-18); should the process wall have a ceiling of its own (for example 6 h) so one session cannot take a
  day?
- **Q1 Licences and credit.** The licence of the new public repository (our originals, the specs, the workspace templates,
  the visible suites), and the terms under which the known problems may be paraphrased: CSES statements, IOI 2018 Werewolf
  (the IOI's task licence to be read), RealWorld's spec (MIT), Redis's RESP (a protocol; the trademark). v1's D18 found
  upstream files with no licence; v2's credit text is written after these are read. **Answered by the owner, 2026-11-01**
  (E1 stories § 4; recorded in the status as an operator answer, not a change): specs 01 and 02 are CC BY-NC-SA 4.0, the
  CSES problem set's licence, by a per-file exception the README states; spec 09 is written wholly in our own words, credited
  "based on IOI 2018 Werewolf"; spec 06 is MIT, credited with RealWorld's MIT notice; spec 05 describes the protocol subset in
  our own words and never takes the product's name; the terms are the bench's `inputs/credits-lang.json`. The repository's own
  licence is MIT (the owner, 2026-10-31).
- **Q4 — answered (D36).** The host was probed on 2026-10-31: cgroup2 with the memory controller; `systemd-run --user
  --scope -p MemoryMax=64M` gives a readable `memory.peak` (2,396,160 B) and `memory.max`. **This host can certify.** The
  scorer runs each program in its own user scope launched outside `bwrap` (D36); certification is required before launch
  and the pre-registration records the host. What stays to check in E3 (T-L30): that the scope holds the program and its
  forked children and not the driver, under the real `bwrap` layout.
- **Q5 The cap formula** of §12 is a proposal; the operator confirms the formula and the share rule or replaces them before
  the pre-registration.
- **Q6 Any inferential statistic?** §9 reports the median, the geometric mean and the count. A pre-registered sign test over
  the ten task medians is possible as a secondary line with the "chosen set, not a sample" sentence; wanted or not?
- **Q7 The process pilot's task set.** Bare: tasks 1, 3, 4, 5, 10 (D4, plus tasks 4 and 5 for the HTTP and RESP drivers'
  real-tree checks — decided, D16 and G-16). Process: tasks 1 and 3 proposed (D16). Adding task 10 to the process pilot
  would calibrate the dearest process sessions at a high cost. Which process set? *(2026-11-01, D44: unchanged — it is group
C's pilot set, §11.)*
- **Q8 — closed (D35).** One declared non-invariant culture, `de_DE.UTF-8`, in both boxes, both stacks, every task and CI's
  runner; no invariant switch; fairness is the declared environment plus the protocol's stated numeric syntax (spec 04's
  grammar), not equal trap counts; T-L32 checks it inside the boxes.
- **Q9 Offered test frameworks.** xUnit v3 for C# and `node:test` for TS are proposed; `node:test` is built in while xUnit
  is a package. Is a C# package-free option acceptable as equal footing, or is this asymmetry simply recorded (and read
  against the parity audit)?
- **Q10 Re-pin of the CLI (D8).** The plan re-pins at pre-registration to the then-current release. If the operator prefers
  to keep 2.1.310 for comparability with v1, say so before E4.S3. **Closed by D47 (2026-11-01):** one re-pin, at group A's
  pre-registration, kept through C; a forced change is an instrument amendment.
- **Q11 Publication of references and specs' final state.** After the series, in the public repository (proposed) — or only
  in the record?
- **Q-I FORMAT's `Freeze rule` sentence — settled 2026-11-05 by the consultants, 2 of 2, no owner needed: the row changes in the amendment.**
  Every spec's header holds *"frozen: changes only by a dated amendment before the v2 pre-registration"*, and `STATEMENT.md` is the
  spec's text, so every agent reads it. It tells the agent that there is a "v2" and a pre-registration, which is to say an
  experiment. **It changes in the same amendment to** *"frozen: changes only by a dated amendment"*. Why: the row holds
  plan words, against the rule that no plan id stands in agent-visible text (§4.1); D19 and D33 keep the workspace free of what the
  task does not need; the row has nothing an agent can use; the sentence is `common` and T-L1 hashes it, so this is the last cheap
  moment; the cost is one more changelog line in a gate that runs anyway. **What it does not do:** F-LIMITS-O still says "reference
  solutions in both stacks, before any session", and F-HIDDEN says hidden tests exist. The agent can still infer a benchmark; the
  change removes a plan word and hides nothing else, and the report does not claim the agent was unaware. No spec is gated yet, so
  no spec re-conforms but by this one row. The real cutoff stays in the operator-facing pre-registration rules, and T24 records the limit.
- **Q-J The `strict` jobs of D66 — settled 2026-11-05 by the team lead: required, as written, needing no owner.** Required keeps the family's
  merge rule, and T15's bundle, whole; reported-only would have narrowed the treatment. E1.S7 fixes the exact C# command. The lint invocation metric stays: it
  is cheap, and it is about the comparison.

## Epics

**In force from the epoch 9 line (revision of 2026-11-07): F1 to F3.** Each epic's goal, stories, needs and done-when are
written in full in the operative part (*Operative plan for epoch 9*, *Epics in force*), and its stories document,
`todo/PLAN_lang_comparison_v2_E7_F1.md`, `_E8_F2.md` or `_E9_F3.md` (named so the lifecycle check knows them), is written by Opus when the epic starts and links back here. What became of every
earlier epic and story is the operative part's table *What happens to every earlier epic and story*.

| Epic | Needs | Cornerstone area |
|---|---|---|
| F1 — the harness and the verdict | the stop lifted | F1.S2: the verdict (repeated scoring and the one memory value) |
| F2 — six validation packages | F1 | F2.S1: the validation package |
| F3 — the series and the report | F2's tag | none |

Cornerstones: 2 of 2 — F1.S2, F2.S1

*(Superseded 2026-11-07 by the table and the ledger line above: E0 to E6 below, their group map and the ledger line
"3 of 3". Everything below is kept as history.)*

Seven epics on the build order of §14, each one branch from the previous epic's commit (E0 in another repository, in
parallel), one reviewable diff and a usable state at its end. Stories are written one epic at a time into
`todo/PLAN_lang_comparison_v2_E<n>.md` when that epic starts (`common.planning-docs` § *A plan split into epics and
stories*); that document links back here. The intended stories with their acceptance criteria are listed per epic below so
the size is visible. Cornerstone areas are named per epic; the concrete marks are confirmed by the epics' stories documents,
at most Q = ⌈2·E/5⌉ = **3** for E = 7, at most one per epic. The three areas ranked first: (1) the gated, frozen specs with
the bias review — an ambiguous or biased spec makes every failure unadjudicable (E1); (2) the shared suites with the trap
coverage table and the limit calibration — the acceptance instrument of the whole experiment (E2); (3) the daemon and web
drivers with RSS and SIGTERM — a new instrument v1 never had (E3). The parity audit (E0) and the pilot's go/no-go (E4) get
strict specs without a mark. **Recounted after D32:** the process arm adds a fourth cornerstone area to E3 — the coai bridge
as the boundary on every `tools/call` (v1 named it so) — but E3 already holds its one mark and the ledger is full
(k = 3 = Q), so E3.S6 gets a strict spec without a mark; a fourth mark is the operator's to grant, as v1's rule says.
Nothing here or in a stories document names a held-out case, mutant or reference in clear.

Cornerstones: 3 of 3 — E1.S2, E2.S2, E3.S2

**The group map (D44; 2026-11-01).** The epic ids, their stories, the cornerstones and the ledger stay; each story is built in
the group that first needs it (§14's staged order). The consult's split (Fable § 1.1, § 7 item 11) is the re-split of
plan-change-control step 7; the E1 stories document is re-cut to it, and the later epics' stories documents are written
to it when they start.

| Story | Group | Note |
|---|---|---|
| E0.S1–S3 | runs now; needed by C | a prerequisite of C's pre-registration only (D48) |
| E1.S1 | A | merged (#142, `6321cd24`, 2026-11-01) with the exact exemptions (D52); `workspace-diff` over the pairs that exist per group |
| E1.S5 | A | merged (#135) |
| E1.S8a part i, part ii-a — merged (#141, #147); fixed forward to D56 and re-pinned (D59) | A | ii-a: launch prefix and status, pidfd waits and the wall rule, case view, cgroup memory read and kill, V11, V12 (D51) |
| E1.S8a part ii-b | B | `rs_net`, daemon and web lifecycle, readiness, `signal TERM` and G, V3–V5, V7; the second runner manifest (D50); the abort rule inherited unchanged (D59, T-L52); T-L31's runner `signal TERM` step and its per-stack test before B's first D40 gate (CP2's F5, D63) |
| E1.S8b | A, whole | the netns holder included; the memory view asserted at 256 and 512 MiB before E2-A calibrates (D51); built on `feat/lang-v2-e1-s8b`, fixed forward to the policy at 90 % and re-pinned (D55, D59); the identity's keeper for CP2's condition (4) (D63) |
| E1.S9, E1.S8 | A | FORMAT's sections classed; FORMAT's sha per group (D45); *(2026-11-03:)* five classes, `daemon-web` for sections 4 and 5, checked against A's frozen classification (D60, T-L56); the grammar per task (D61); CP1's contract findings and `F-PROCS` before the gate (D62, D63) |
| E1.S4 | A | the full package sets of both stacks, web packages included |
| E1.S3 | A (phase A; phase B for 01, 02, 03, 09); B (phase B for the six) | built on `feat/lang-v2-e1-s3`, #167 closed for the stop; its build line is D65's; phase B's commit is the group's workspace commit (D67 (3)) |
| E1.S3b *(added 2026-11-05)* | A, before the first D40 gate | **the submission contract**, after S4b: `lib/submission.mjs`, `lib/deviations.mjs`, `ignore-scripts`, the `build.sh` comments, the identity, T-L60–T-L62 (D65) |
| E1.S4b *(added 2026-11-05)* | A, before the first D40 gate | **the `Mvc.Testing` supersede and the matrix**, T-L63 (change 7.1, D65) |
| E1.S8c *(added 2026-11-05)* | A, before the first D40 gate | **the runner's comments and the case record's fields**, re-pinned, the probe re-run, CP2 addended, T-L64 (OP-3, D67 (4)) |
| E1.S6, E1.S2, E1.S2a | A for 01, 02, 03, 09; B for the six | one freeze tag per group (D45); A's four re-gated on B's identities in B (D50); CP2's conditions (1)–(3) in E1.S2a's pre-freeze tool before the first D40 gate (D63, T-L59); *(2026-11-05:)* E1.S2a owns T-L66, its olympiad half read at CP4, its daemon and web half at CP4-B |
| E1.S7 | A: the tag ruleset and `main` protection only; C: the rest | the bound run base (D52); *(2026-11-05:)* the `strict` jobs and the precedence sentence (D66), the root `.gitignore` (§5) |
| E2.S1–S4 | per task, in the task's group | one sealed archive per task (D49); S3.0 the floor probe once in A (D57); S3 by the calibration sequence (D58) |
| E2.S5 | A | once, before A's record |
| E3.S1 | A; widened in B and C | the `group` dimension and manifest, the inherited alias baseline (D45, D47) |
| E3.S2 | B | cornerstone |
| E3.S3, E3.S5 | A | — |
| E3.S4 | A (olympiad); B (daemon, web); C (process) | — |
| E3.S6, E3.S7a, E3.S7, E3.S8, E3.S9 | C | the broker's publication gate (D52) |
| E3.S10 | each group, the slice built | — |
| E4.S1–S4 | per group | the CLI pin once, at A (D47) |
| E5.S1–S2 | per group | C: the bare anchor round is part of E5-C, mandatory (D54; T-L55) |
| E6.S1–S2 | per group (interim records, D49); final after C | — |
| E6.S3 | after C | publication (D49) |

### E0 — Conventions parity audit and release (§14 step 0 · 3 stories · `dew_flow_conventions`, in parallel, now)

- **Goal:** every C#/.NET rule in `dew_flow_conventions` has a Node/TypeScript counterpart adapted to Node, and the reverse,
  released and pinned by the task repository before the v2 pre-registration (D23, T14).
- **Scope:** the rule inventories `csharp/*.md` and `typescript/*.md` at the current `release`, rule by rule (selection
  metadata, obligations, Definition of Done items); the always core and `common/*` read for stack-specific clauses; the
  gap table; the new or widened rules; a plan in `dew_flow_conventions/todo/` naming this boundary from that side
  (`common.planning-docs` § *Across repositories*).
- **Depends on:** nothing — it starts now.
- **Staged (D48, 2026-11-01):** its release is a prerequisite of group C's pre-registration only; nothing of A or B waits for it.
- **Done when:** the gap table is empty or every remaining gap is a recorded decision with a reason; the release tag exists;
  `rules.mjs explain` for the process arm's task set gives rule counts and bytes per stack within 10 % of each other, or the
  difference is explained rule by rule in the audit record.
- S1 — **the audit** (runs now): the inventory and the gap table, per rule: id, stack, counterpart id or `missing`, the
  obligation that has no twin, bytes. *Accepted when:* every rule of both stack folders appears once; the table is in
  `dew_flow_conventions/research/` (the record) and cited here by path.
- S2 — the counterpart rules written and released: each gap closed by a new rule, a widening of an existing one, or a
  recorded "no counterpart needed" with its reason; released to `release` by that repository's process. *Accepted when:*
  that repository's own checks are green; the release sha is in this plan's pre-registration.
- S3 — the task repository pins the release on a snapshot branch (v1 D9); `pin-check` green; the process arm's `rules.mjs
  explain` byte counts per stack recorded. *Accepted when:* the pin and the counts are in `inputs/conventions-lang.json`.
- **Cornerstone area:** none marked (strict spec, as v1's conventions rows).

### E1 — The task repository: the format document and its v2, the runner and the execution layer, specs gated and frozen after a pre-freeze execution gate, workspaces with the runner, toolchains, caches, process templates (§14 step 1 · 15 stories)

- **Stories:** [PLAN_lang_comparison_v2_E1.md](PLAN_lang_comparison_v2_E1.md) — written 2026-10-31 when the epic started (an
  epic's stories document, not a plan change); cornerstone E1.S2 (`model: fable`), the ledger unchanged; its open points
  (§9 there) are listed, not decided.
- **Goal:** ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
  across stacks but for the stack, on pinned toolchains and offline caches; the process arm's run-base machinery; in a
  public repository that leaks nothing.
- **Scope:** D2, D5, D6, D11, D12, D19, D21, D25 (the spec half of the bias review), D31; §4.1, §4.5 (the stack sentences),
  §4.6, §5.
- **Depends on:** the operator creating the repository; the licence answers (Q1); E0.S3 for the pin; v1 §6's template
  design for S7 (D32: built here, nothing imported).
- **Done when:** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots hashed; the
  preflight passes inside a box on SDK 10.0.403, Node 26.13.0, TS 6.0.x (the pinned patch), the pinned Python; `FORMAT.md` is gated and its
  three examples run (S8); the ten specs conform to it, carry a gate verdict and the freeze tag; the README carries the credits and the Node stack table; `leakcheck` clean.
- **Staged (D44, D45, D51; 2026-11-01).** *Depends on:* E0.S3 only for group C's conventions pin (D48). *Done when, per group:*
  **A** — the done-when above for 01, 02, 03 and 09 (`workspace-diff` on four pairs; the four specs gated and frozen under
  `spec-frozen-A-<date>`), E1.S8b whole with T-L43 (revised, D55; fixed forward and re-pinned, D59), part ii-a re-pinned, the full package sets of both stacks, the tag ruleset and `main`
  protection read back; **B** — the six other specs under `spec-frozen-B-<date>`, part ii-b, A's four re-gated and
  re-calibrated on B's identities (D50; *a check only, A's registered limits unchanged — finding 1, 2026-11-01*), T-L38, T-L39 and T-L52 green (the abort rule inherited by the daemon and web lifecycles, D59); **C** — E1.S7 whole. The stories document is re-cut to this
  (2026-11-01). *(2026-11-05, D65–D67:)* **A** also — FORMAT's amendment gated; T-L60, T-L61, T-L62, T-L63, T-L64 green; T-L68 recorded; T-L66's olympiad half read at CP4; A's workspace commit (T-L67). **B** also — T-L66's daemon and web half at CP4-B; B's workspace commit. **C** also — the `strict` jobs and T-L65's CI half.
- S1 — repository skeleton: layout of §5, conventions pin (E0.S3), README with credits and the §4.6 table, LICENSE (Q1), the
  `workspace-diff` check. *Accepted when:* the check passes on an empty task pair and fails on a planted difference; the
  README's links and attribution appear nowhere under `csharp/`, `node/`, `tasks/`, `runner/` or `todo/tasks/`, and the
  generated run-base tree holds none of them (a test greps the tree; D39). The tree generator is this story's
  (`generateRunBaseTree`); the orphan commit and its one-commit `git rev-list` are S7's, tested there after the push (OP-8).
  *(2026-11-01, D52:) the push check's exemptions are exact approved values at approved places, never a class of text
  (T-L47).*
- S2 — **the ten task specs, written by the team lead, gated and frozen** (cornerstone area; D21, D25). Per task, in the
  order of §4.1: the spec at `todo/tasks/NN-slug.md`; the coai document gate with `purposeText` (who acts on it: the agents
  under test in both stacks and the suite's author; what they must be able to do: build a program that passes the hidden
  suite from the text alone; what it does not cover: the tests, the traps), a consultant, the bias review question of §4.3;
  every finding `accept`ed or rejected with a reason; the verdict and notes recorded in `inputs/bias-review/NN.md` (no
  case text). **Per task the spec is gated** — verdict, hash, the amendment rule in force from the gate on — and **one tag
  freezes all ten together after the tenth** (OP-2's reading, now the text): the tag `spec-frozen-<date>` on the commit holding all ten and `FORMAT.md`, their sha256 in
  `inputs/specs.json`. *(Superseded for the staged series, 2026-11-01, D45: one tag per group — `spec-frozen-A-<date>` on the
  commit holding 01, 02, 03, 09 and FORMAT, then `spec-frozen-B-<date>` for the six others; a later tag never alters an
  earlier one.)* **Prerequisites:** `FORMAT.md` v2 (S8, S9) is gated before the first spec is gated; every spec
  conforms to it (a conformance check: header rows, R-ids, the fixed sentences with their slots, the sample table against
  the `samples/` files, the invalid-input and build-bound sentences, T and G stated where the kind has one — olympiad tasks have neither); **the task's `samples/` files (S6)
  exist before its conformance gate** (D40).
  *Per-task acceptance:* the gate's verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered; the
  bias question got an explicit answer per stack with no open finding; no id, title, site or trap in the text; a known
  task's needles (D18) find nothing; the spec conforms to `FORMAT.md` (the check passes); the limits as floors and rule,
  the timing contract (T, G, step deadlines) and the semantic bounds, I/O and start contracts, frozen list and "hidden
  tests exist" are present; **the pre-freeze execution gate (D40): both draft references (S2a, one per stack) execute
  every visible sample through the distributed runner (`runner/run_samples.py`, S8a) in the final sandbox environment (the
  box, the scope, the declared culture) and pass, with the spec's T and G and the step deadlines in force** — a value a
  reference cannot meet is changed before the freeze; the gate also runs **the task's riskiest held-out-shaped scenarios,
  listed in the private aid** (`PLAN-risky-scenarios.md`, D41), kept out of git, through the same runner — both draft
  references pass them (the count per task is in the aid; the public text names no scenario);
  two readers (an Opus judge and the consultant) list no open question, or
  every one is resolved by a text change before the gate round that passed. *Whole-story acceptance:* the ten verdicts and
  the tag exist before E2.S1 **finalises** a reference (the draft references exist earlier, S2a); after the
  tag a spec changes only by a dated amendment applied to both stacks and before the pre-registration.
- S2a — **the draft references, written in E1** (D40): one C# and one TypeScript reference per task, written by the team
  lead with Sonnet coders per the role table from the spec draft, living in the bench's private area `~/rsd/lang-v2/refs/`
  (outside the task repository's git, beside the held-out material; never in a workspace or a run base). They exist so
  the execution gate of S2 can run; E2.S1 finalises, validates and calibrates them. *Accepted when:* for each task both
  drafts build and run in the final sandbox environment through `runner/run_samples.py` (S8a) and pass every visible
  sample and the task's riskiest held-out-shaped scenarios listed in the private aid (D41); the drafts are hashed in `inputs/references.json` as
  `draft`; no draft or its hash leaks into the task repository (`leakcheck`).
- S3 — the workspaces: `STATEMENT.md` generated from the frozen spec plus the stack sentence (and the framework sentence
  for a web task), `build.sh`, **`run.sh` ending with `exec` of the program (D38)**, `PACKAGES.md`, **`runner/` — the
  pinned `run_samples.py` and `GRAMMAR.md`, read-only (D37)**, pin files per stack, the C# `.csproj` skeleton and
  `nuget.config` offline source, the Node `package.json`/lockfile/`tsconfig.json`/`.npmrc offline`/`eslint.config.mjs`.
  *Accepted when:* in a box with no registry, each stack's empty workspace builds and `run.sh` starts a hello program;
  T-L2, T-L31; `python3 -I runner/run_samples.py samples/` runs inside the box against the hello program and reports the
  sample verdicts; the `runner/` hash equals the bench's `inputs/specs.json` entry; the generated statements equal the frozen
  spec's text plus the sentence (a test); **task 9 (Werewolf, D33):** `csharp/09-*` and `node/09-*` hold exactly the same
  file set as every other olympiad workspace — no adapter, helper, parser or grader code — and the spec's stdin/stdout
  grammar is the only I/O contract (a test lists the files and greps for a parser).
- S4 — the package sets and cache snapshots: the recipes, the exact pinned sets with hashes in the cache manifest,
  `PACKAGES.md` generated from the manifest. *Accepted when:* T-L13; the manifest lists every package and version; a
  package outside the set fails naming it; both hashes in `inputs/cache-snapshots-lang.json`.
- S5 — toolchains pinned: SDK 10.0.403 and Node 26.13.0 installed from official artefacts into `~/rsd/lang-v2/toolchains/`,
  read-only, checksums recorded; Python's checksum; the preflight pins. *Accepted when:* the preflight refuses a box whose
  `dotnet --list-sdks`, `node --version` or Python checksum differs; the checksums are in `inputs/stacks-lang.json`.
- S6 — the visible samples (`tasks/<task>/samples/`, copied into both workspaces) as the visible part of E2's suites, in the
  **fixed format `samples/<id>.in` / `samples/<id>.out`, bytes exact** (change 1.2b; the spec's sample table shows the same
  bytes with the `⏎` glyph; a daemon or web sample is a scenario in `FORMAT.md`'s grammar). E2.S2's runner reads this
  format; `FORMAT.md` (S8) fixes it, E2.S2 does not choose it. The samples are **drafted** here, **exist before the spec's
  conformance gate**, run against both draft references through the runner in the final sandbox environment **before the
  freeze** (D40, S2's execution gate), and are **validated** again with the held-out cases in E2's joint validation (§14
  step 2). *Accepted when:* every spec's sample table and its `samples/` files hold the same bytes (a test); the runner
  runs them against both draft references in the box and both pass before the freeze; the same holds against the final
  references (closes in E2); no sample reaches a trap — checked now against the private test-design notes
  (`~/rsd/lang-v2/test-design/`, D41) and formally in E2 once the trap table exists (OP-9).
- S7 — the process-arm templates in this repository: CI with one job per stack, family checks, PR title; rulesets
  `run-main`, `template-frozen`, tags; the deploy key as the only bypass actor; the PAT; `GitHub settings` as v1 §6 —
  built here to v1 E2.S4's design (D32). *Accepted when:* v1's P0 checks pass here: a run base cut by the deploy key, a direct push
  with the PAT refused, CI green on a non-default base, a planted violation red per stack, merge blocked until threads
  resolve. Built here to v1 §6's design (D32), for one repository with two stack folders. *(2026-11-01, D44, D52:) deferred to
  group C except the tag ruleset and `main` protection, which are applied and read back before A's freeze; the run base is
  bound — the approved frozen commit, the generated tree and the final orphan sha, the push check over that exact orphan, an
  empty parent list proven locally, a wrong-commit build refused before any push (T-L46).*
- S8 — **`todo/tasks/FORMAT.md`, the versioned spec and sample format** (a prerequisite of S2): the spec conventions of §4.1
  (header rows and placeholders, R-ids, byte-exact default, the frozen-list wording, the limits as floors and rule, the
  build bound, the invalid-input sentence, no plan ids, the `⏎` glyph); the `<id>.in` / `<id>.out` pair; the **scenario
  grammar** of `.in` (steps, named connections, synchronisation and barriers, timing and deadlines, dynamic-value capture
  and reuse, the SIGTERM step) and the **comparison semantics** of `.out` (byte-exact default, normalised fields,
  order-insensitive sets where a spec says so); **one worked example per kind** — the olympiad example from the pilot spec
  01, a daemon scenario over a neutral framed TCP protocol (a split frame, pipelined requests, a slow reader; no real product named, D39), a web scenario (HTTP concurrency, two
  clients racing on one idempotency key); a version line. FORMAT v1 had no gate of its own: it is a draft re-conformed into
  v2, and **v2's gate (S9) is its gate** (OP-3). *Accepted when:* the
  three examples parse and run through S8a's runner (E2 owns no runner, D37) against stub programs (one per kind) with the expected verdicts; the
  conformance check of S2 is derived from this document; its sha256 is in `inputs/specs.json` and frozen with the specs; a
  change after the freeze is a dated amendment of `FORMAT.md` that names the tasks it touches (G-17 voids their limits).
  *Built so far (ungated drafts, sorted by the epoch-3 re-review):* v1 at `d7c73ff` with spec 01 conformed at `28bf777`;
  the pilot specs 05 (`30af434`) and 04 (`0b69a50`) written under it found the gaps S9 closes.
- S8a — **the baseline visible-sample runner, `runner/run_samples.py`** (D37, D22): the bench's Python standard-library
  program implementing FORMAT v2's grammar (`<id>.in`: steps, named connections, barriers, timing and deadlines, capture
  and reuse, the SIGTERM step) and its transcript comparison (`<id>.out`: byte-exact default, normalised fields,
  order-insensitive sets, exact-decimal JSON numbers, relational expectations), for the three kinds; **an early E1
  deliverable, built before FORMAT v2's gate so that FORMAT v2's examples and every pre-freeze check (D40) run through it**;
  pinned by version and sha256 in `inputs/specs.json`; the same file is CI's runner and E2.S2's held-out runner (E2 owns
  no runner). This breaks the FORMAT ↔ runner circularity: the runner is a program that exists, FORMAT v2 is checked
  against it, and the specs are checked against both. *Accepted when (superseded for the staged series by the per-group
  acceptance at the end of this item; kept):* T-L21's shape holds for the three kinds against
  stub programs (stdlib-only imports; identical case lists for a C# and a TS stub); FORMAT v2's three examples and the
  three pilot specs' samples (01, 05, 04 — "four drafts" counted FORMAT v1, OP-4) run through it with the expected
  verdicts; the pinned version and hash are recorded before FORMAT v2's gate; the workspace copy equals the pinned tree
  (S3's check). **`runner/` is a directory** (OP-5, the 800-line rule): standard-library modules with `run_samples.py` as
  the entry, hashed as one manifest in `inputs/specs.json`, the frozen list naming `runner/`; under `-I` Python leaves
  the script's directory off `sys.path`, so the entry adds its own frozen, read-only directory itself. *(2026-11-01, D50,
  D51, D52:) part ii is re-cut into ii-a (group A: the launch prefix and status, the pidfd waits and the wall rule, the case
  view, the cgroup memory read and kill, V11, V12) and ii-b (group B: `rs_net`, the daemon and web lifecycle, readiness,
  `signal TERM` and G, V3–V5, V7), so A and B run on two runner manifests with D50's guards; V2 never calls
  `Decimal.normalize()` before equality (T-L44).* **Scope and acceptance per group (second full reading, 2026-11-01,
  finding 5):** **group A's part** (part i and ii-a) is accepted on **olympiad cases and FORMAT's olympiad example only** —
  T-L21's shape for the olympiad kind against stub programs (stdlib-only imports; identical case lists for a C# and a TS
  stub), FORMAT v2's olympiad example and the olympiad pilot spec 01's samples with the expected verdicts, every daemon and
  web pair parsed by `--parse-only` (part i), the pinned version and manifest hash recorded before FORMAT v2's gate, the
  workspace copy equal to the pinned tree, T-L44. **Group B's part** (ii-b) carries the daemon and web grammar's execution:
  T-L21's shape for the daemon and web kinds, FORMAT v2's daemon and web examples, the daemon and web fixtures and the pilot
  specs 05 and 04's samples run with the expected verdicts, and T-L38 green before B's first D40 gate.
- S8b — **the execution layer** (D43; a prerequisite of the D40 gate, of E2.S1/S3 and of E3.S4): `spikes/lang-bench/lib/exec-layer/`
  — launch of `run.sh` in a bwrap box (v1's binds) with D15's environment and the culture `de_DE.UTF-8`; the program-only
  `systemd-run --user --scope` with `MemoryMax` and `MemorySwapMax=0` (D36); the **private loopback-only network namespace
  shared by the driver and the program** (`unshare -rn` from the harness, `lo` up, the box joins it; PID and filesystem
  isolation from bwrap, the driver outside the scope); the readiness deadline T, the step deadlines, SIGTERM and the grace
  G; cleanup of the scope and the namespace; one identity (the tree's sha256) in every record. Consumed unchanged by the
  gate, by E2 and by E3 (§6.1). *Accepted when:* T-L33, T-L34, T-L30, T-L31, T-L32 green on the series host with
  `BENCH_REQUIRE_SANDBOX=1` and nothing skipped — equal netns ids and different cgroups for driver and program; the
  driver-only sentinel unreadable from the program; killing the scope removes the descendants while the driver survives
  and collects the evidence; the probes run identically for both stacks; the pre-freeze gate of S2 runs on this layer
  (D40 not weakened); the layer's identity is in `inputs/specs.json` beside the runner's. *(2026-11-01, D51:) built whole
  before group A, the netns holder included; its probe asserts the memory view through the final prefix at 256 and 512 MiB
  (T-L43) before E2-A calibrates, and OP-K is decided on it.* *(2026-11-02, D55, D59:) OP-K is closed — policy A at 90 % for
  both stacks, the per-case keys derived by `env.mjs`; the story is fixed forward to it and re-pinned before CP2; T-L43 asserts
  the settings and the object-graph regression and records the mixed workload (T21).* *(2026-11-03, change 6.1:) and Node's
  medium-buffers at 256 MiB, recorded, never asserted (T21). CP2's condition (4) is this story's: the identity keeper (D63).*
- S9 — **`FORMAT.md` v2**, gated before any spec gate (a prerequisite of S2's gates, like S8), **after S8a exists**. The gaps found by the three
  pilot specs and the consult `3ea9ef36`, each closed in v2: **exact-decimal number comparison in JSON transcripts** (an
  integer and a decimal compared by exact value, `int`/`Decimal`, never float; `-0` equals `0`); an **F-LOAD fixed
  sentence with slots** for a load scenario's size and duration; **default request headers** of the HTTP driver and how a
  spec adds or overrides one; **connect retrying within the readiness deadline T**; **the runner's behaviour on a closed
  HTTP connection** (reconnect once within the step deadline, then `fail`); **relational expectations** (a final value
  equals the increments sent; a set of values is pairwise distinct) as comparison kinds in `.out`; **`{ref}` lists longer
  than two**; **tables belonging to their R paragraph** (a table is part of the requirement it follows); **the T and G
  binding** (the series defaults 10 s and 5 s, how a spec overrides, where the values are read by the runner — D14);
  **F-BYTES-W against F-JSON precedence** (which comparison wins when both apply to a reply); **`sleep` step deadlines**;
  **split offsets on joined bytes** (a partial-frame step names the byte offset of the split in the joined request bytes);
  **a neutral daemon example** that does not name the real protocol (D39); **removing the reading block** from every
  daemon and web spec once `runner/` and `GRAMMAR.md` are in the workspace (D37); **the base-environment sentence**
  following D15 — the same base plus the documented per-stack variables (not "the same for both stacks"); **the
  §1.5 credit sentence following D19 and D39 (no `NOTES.md`; credit is the README's attribution section — OP-7)**; **the
  maintainer sections 7–9 moved out of `FORMAT.md`** into an internal document (`FORMAT-MAINTENANCE.md` in the bench's
  `spikes/lang-bench/inputs/`), so no plan id stands in a file the process agent's run base carries; a version line `2`
  and a changelog entry per gap. *Accepted when:* every gap above has a v2 section or a fixed sentence, named in the
  changelog; the three examples and the three pilot specs' samples parse and run through the runner against stub programs
  with the expected verdicts; no plan story, epic, decision or test id remains anywhere in `FORMAT.md` (a grep); the
  daemon example names no real protocol or product (D18's needles find nothing); v2's gate verdict (document gate +
  consultant) is recorded before the first spec gate; its sha256 replaces v1's in `inputs/specs.json`; specs 01, 05 and 04
  are re-conformed to v2 before their own gates. *(Finding 5, 2026-11-01: in group A the daemon and web examples and the
  samples of 05 and 04 are parsed with `--parse-only` before the gate; they run in group B, through part ii-b.)* *(2026-11-01, D45:) every section of v2 is classed `common`, `olympiad`,
  `daemon` or `web`, one line per section; after A's freeze the `common` and `olympiad` sections are immutable for the series.*
  *(2026-11-03, D60–D63:) five classes — `daemon-web` for sections 4 and 5, the sections that serve daemon and web tasks and no
  olympiad task; B's check reads A's frozen classification (T-L56); `GRAMMAR.md` pinned per task (D61, T-L57); before the gate
  round, CP1's F1–F4 as text fixes, F-STDERR's 256 MiB stop (F5), F-ENV's `TZ` and the keys an `env` line may not set (F7),
  `Format version: 2` (F6) and `F-PROCS` (D63); FORMAT's figures equal the code's (T-L58).* *(Judges J1, J2, J5, 2026-11-03:)*
  classes per `###` subsection where a `##` section mixes kinds; F-ENV as narrow as the layer, with the conformance check's
  part; the changelog, the preamble and the heading numbers as D60 says.
- S3b *(added 2026-11-05, D65; after S4b)* — **the submission contract**: `lib/submission.mjs` (the frozen list, the copy, the restore, the build),
  `lib/deviations.mjs` with its own recorded hash, `ignore-scripts=true` in the Node templates, each stack's `build.sh` comment, `inputs/specs.d/build-contract.json`,
  whose writer S3b owns and runs after S4b. *Accepted when:* T-L60, T-L61 and T-L62 green on the real templates; `spec-frozen-A-…` waits for them.
- S4b *(added 2026-11-05, change 7.1, D65)* — **the supersede and the matrix**: `Microsoft.AspNetCore.Mvc.Testing` in the C# recipe,
  the C# snapshot superseded and pruned, `inputs/package-matrix.json`, `PACKAGES.md` derived from it. *Accepted when:* T-L63; T-L13
  still green; the template versions read from the new manifest. Node's peer is recorded: `inject()` needs no package.
- S8c *(added 2026-11-05, OP-3, D67 (4))* — **the runner's comments and the case record's fields**: the 12 plan ids in seven files
  and the stack names in `rs_abort.py`'s comments go, the detection strings stay; the proof is read at this part's commit; per-case CPU and the RSS curve get their source, in the runner.
  *Accepted when:* T-L64; the new manifest in `inputs/specs.d/runner.json` and `inputs/runner-versions.json`; the probe re-run as
  a new dated file; a CP2 addendum in `inputs/checkpoints.md`, before the first D40 gate.
- *(2026-11-05, D65, D67:)* **S2's freeze** waits for T-L60 and for T-L66's olympiad half, and the group's workspace commit follows
  it (T-L67). **S2a** builds through `lib/submission.mjs`, and its record holds `buildContract`. **S3**'s build line, frozen list and
  `.npmrc` are D65's, cited, and it builds outside every worktree, with a reviewed copy step into the task repository (OP-2), so
  "generate into a worktree" above is superseded. **S7** adds the root `.gitignore` (OP-6), the `strict` jobs and the precedence
  sentence (D66), and cuts a run base from B's workspace commit.
- **Cornerstone area:** the specs (S2).

### E2 — Shared acceptance suites, references, trap coverage, limits, bias review (§14 step 2 · 5 stories)

- **Goal:** the acceptance instrument calibrated before any model touches a task: one neutral Python suite per task that
  runs unchanged against both stacks, two references per task, suites that catch every designed trap, limits that reject
  neither runtime's baseline, and a bias review of each suite.
- **Scope:** D13, D14, D22, D25 (the test half); §4.3 calibration; the sealed archive; `leakcheck`.
- **Depends on:** E1.S2 frozen specs, E1.S2a draft references, E1.S5 toolchains, E1.S6 samples, **E1.S8a the runner**
  (E2 owns no runner: S2 reuses `run_samples.py` for the held-out suites and owns only the suites, the mutants, the trap
  table and the calibration).
- **Done when:** T-L7, T-L8, T-L21, T-L25 green; G-17's conditions hold on the series machine (recorded, re-checked at the
  pilot), including the calibration order; the archive sealed with hashes and counts in `inputs/heldout-prereg-lang.json`;
  the ten bias-review verdicts recorded; `leakcheck` clean on the branch.
- **Staged (D44, D49, D50; 2026-11-01).** Per task, in the task's group: A runs S1–S4 for 01, 02, 03 and 09 (olympiad mode)
  and S5 once before A's record; B runs S1–S4 for the six others and re-runs S3's calibration for A's four on B's identities *(as a check only, A's registered limits unchanged; finding 1, 2026-11-01)*
  (D50). *Done when, per group:* in A, T-L51 (the floor probe) and T-L49 (the calibration sequence) green first — they are E2.S3's, not E1's (D57, D58; judge J1) — then that group's tasks final, calibrated, bias-reviewed and sealed, **one archive per task**
  (`heldout/suite-<task>.enc`), the counts and the trap coverage table's hash registered with a `group` field.
- **The submission contract (D65; 2026-11-05).** S1's references, S2's mutants and S3's calibration build through `lib/submission.mjs`, so their evidence is about the path that scores. A reference's own `deviations` are recorded: a count above zero is read by the team lead before the reference is final. S3's `register-limits` writes the runner's limits file per task (`timeS`, and `readyS` and `graceS` where they apply), with a test that the two agree (§6.2 row 7), and T-L36's limits half is green before the first registration (D64's dated reading).
- **Order inside this epic, per task (§14 step 2, D40):** **before the spec freezes** — the samples (E1.S6) and the two
  draft references (S1) exist and the references execute every sample through the runner in the final sandbox environment
  (E1.S2's execution gate; T, G, the step deadlines and the semantic bounds validated on both) → the spec freezes → the
  suite, mutants and trap table (S2) drafted from the frozen spec and `FORMAT.md` → **joint validation**: the references
  against the suite and the samples, every disagreement traced to its fault (spec: dated amendment; suite or reference:
  fixed) → S1 **final** → S3 (execution time and memory calibrated by D14's rule). S3 registers a task's limits only when
  S1 is final for that task; a spec or `FORMAT.md` amendment dated after a task's registration voids that task's limits,
  and S3 runs again for it before the pre-registration (G-17).
- S1 — references **finalised**: the two draft references of E1.S2a (one C# and one TypeScript per task, written by the
  team lead with Sonnet coders from the spec, living in `~/rsd/lang-v2/refs/`) are validated jointly with S2's suite and
  calibrated. *Accepted when (= "final" for that task):* **after the joint validation** both pass every visible and every held-out case; every disagreement between
  them, or with the suite, was traced to the spec, the suite or a reference and fixed where the fault was (a spec fault by a
  dated amendment); **no spec or `FORMAT.md` amendment is pending** for the task; the dated list and the final mark per
  task are in `inputs/references.json`.
- S2 — **the shared suites and the trap coverage table** (cornerstone): per task one Python module (standard library only)
  with the stdin/stdout, HTTP or TCP/RESP + signals mode, **reading the fixed case format of E1.S6** (`<id>.in` /
  `<id>.out`, change 1.2b) and `FORMAT.md`'s scenario grammar and comparison semantics; the held-out cases (counts
  registered), drafted from the spec and validated jointly with S1's references; the table task × stack × trap → cases;
  mutants one per trap per stack; **every case cites the requirement ids (R1 … Rn) it asserts** (change 1.2c; v1's "names
  the spec sentence" rule, now by id). *Accepted when:* T-L8, T-L21; the joint validation passed (both references pass
  every case; every disagreement traced and fixed at its fault); every trap of the private aid (D41) has at least one catching case; every case
  cites at least one R-id that exists in the frozen spec (a test against `inputs/specs.json`'s id lists); the table and the
  mutants' ids are sealed; no case appears in git in clear.
- S3 — calibration and limit registration by D14, **only after S1 is final for the task**: reference measurements in the
  scoring box, the formula applied, the limits registered per task in `inputs/limits-lang.json` with the spec hash they were
  calibrated against. *Accepted when:* T-L7, T-L25, T-L28; the registration is refused for a task whose S1 is not final
  (joint validation not passed); both references pass under the limits in three consecutive runs under the quiet check; a
  spec or `FORMAT.md` amendment after registration voids that task's entry and the re-run re-registers it, before the
  pre-registration; every registered entry names a spec hash and a `FORMAT.md` hash equal to the frozen (or last amended)
  documents; the registered values go to the runner (`inputs/limits-lang.json`) and never into a spec. *(2026-11-02, D57, D58:) S3.0, the
  floor probe, runs once in group A before any registration (T-L51); each task's limits follow D58's sequence — the provisional
  run at F, the candidate, the confirming run under the candidate and its heap settings, the time limit under the registered
  memory limit — with every step in `inputs/limits-lang.json` (T-L49); B's check of A's four runs steps 3 and 5 only (D50).*
- S4 — the bias review of each suite (D25): spec + suite in clear from a scratch dir to the coai document gate with
  `documentText` and a consultant, the §4.3 question; findings resolved; verdict and notes in `inputs/bias-review/NN.md`
  (no case text). *Accepted when:* ten verdicts, each `proceed`/`good_enough`/`continue_anyway`, every finding
  resolved; a finding that changed a case is traced to a spec sentence; Q-C's answer recorded.
- S5 — adjudication packets for v2: v1's `adjudicate.mjs` pointed at the v2 series with stack and arm masked. *Accepted
  when:* a synthetic series produces packets that show case, output and spec sentence and nothing of the stack or arm. *(2026-11-02, D56:) a packet also shows the memory cause and the flags `abort-below-evidence` and `child-abort-line` of a flagged case (T-L54).*
- **Cornerstone area:** the suites and the trap table (S2).

### E3 — Harness extension: two arms, long sessions, drivers, judge, scoring per kind, CPC in the record, the process arm built with its GitHub broker (§14 step 3 · 11 stories)

- **Goal:** one command runs one pair of any task in either arm under the v1 rules, scores it by its kind, computes CPC's
  inputs, and leaves a record the report can be built from; v1's instruments untouched but for named widenings; the
  process arm **built here to v1's design** (D32), never copied from code that does not exist.
- **Scope:** §6; D10, D15, D17, D18, D24, D27, D32; §11's G-15 … G-19; the smoke; the coai box and bridge, the process runner,
  the compliance checker, P0 and the model smoke on the task repository; `research/module_tests.md`.
- **Depends on:** E1 workspaces, caches and the repository-side templates, rulesets, PAT and deploy key (E1.S7); **the
  execution layer (E1.S8b) and the runner (E1.S8a), consumed unchanged — E3 builds orchestration, aggregation and records
  on top of them, never a second launch path (D43, §6.1)**; E2's sealed archive for the stub tests' shape.
- **Done when:** T-L3 … T-L6, T-L9 … T-L12, T-L14 … T-L16, T-L18 … T-L20, T-L22 … T-L24, T-L28 … T-L32 green; the host's
  memory certification (the systemd user scope's `memory.peak` for the program and its children, D36) is checked first on
  the series host and recorded, and the runner refuses data sessions without it; the culture check (T-L32) passes inside
  both real boxes; a stub pair of
  each kind in each arm runs end to end in real boxes; the bare smoke (task 1, TS, ≤ $0.20) is read by E/O/T/B with T1–T5; P0 passes on
  the task repository's templates; the model smoke lists the vendors through the bridge; `research/module_tests.md` carries
  the harness's flow catalogue; the module document describes the extension.
- **Staged (D44, D45, D47; 2026-11-01).** *Depends on:* group A — E1's A slice and E2-A; E1.S7 only for group C. *Done when,
  per group:* **A** — T-L3, T-L9 (per group), T-L10, T-L11, T-L12, T-L14 … T-L16, T-L18 … T-L20, T-L28 … T-L32, T-L40 …
  T-L42, the bare smoke; **B** — T-L4 … T-L6, T-L38, E3.S2's module catalogue; **C** — T-L22 … T-L24, T-L35, T-L45, P0, the
  model smoke.
- **What this epic imports and what it builds (D65, D66, D67; 2026-11-05).** It imports `lib/submission.mjs`, never a copy, and replaces T-L66's helper with S4's aggregation. S1's group manifest holds the workspace commit and the build contract's identity (T-L67), its `PREREG_FILES` and harness folders name `spikes/lang-bench/`, and T-L20's launch half is green before A's tag. S4 takes the tree (the `delivered` mark, the PR head) and puts `deviations` in `score.json`. S5's classifier counts the lint invocations and adds T-L15's literals; S5's agent box sets no notifier key (D15's dated line). S8 reads D65's one frozen list for "inputs unchanged" and "pin unchanged", and reads the `strict` jobs into a column beside k/18. S9's planted violation is read on the `strict` job, with the same tree green in the frozen build (T-L65). Done when, added: **A** — T-L60's scorer half, T-L61's record half, T-L67; **C** — T-L65.
- S1 — the arm, task and round dimensions of the runner: `ridOf` with arm and task and a `single` phase, six alternating
  rounds from the seed, the pair without barrier, the void schedule without v2 rows, the prereg guard's v2 list and the
  spec-freeze guard, `EGRESS_HOSTS.langBare` / `langProcess`, timestamps in the record. *Accepted when:* T-L9, T-L10,
  T-L12, T-L20; `run --dry-run` prints six rounds with their arms. *(2026-11-01, D44, D45, D47:) plus the `group` dimension and
  the immutable group manifest; three rounds per group; one alias baseline for the series; `run --dry-run` prints the
  group's rounds; T-L40, T-L41, T-L42.*
- S2 — **the process, TCP and HTTP drivers with RSS sampling and the SIGTERM check** (cornerstone), as thin wrappers over the
  Python suite runner, **started through E1.S8b's execution layer** (the scope, the shared loopback namespace, T and G —
  the drivers add the scenario logic and the records, not a launch path; D43). *Accepted when:* T-L4, T-L5, T-L6; a driver never leaves a
  process (the janitor finds none after each test); **and `research/module_tests.md` is updated** (`common.knowledge-base`,
  the scenario-tests rule) with: (i) the exact runner command (`node spikes/lang-bench/run-tests.mjs`, with
  `BENCH_REQUIRE_SANDBOX=1` and the glob for the driver tests); (ii) every scenario flow this epic adds, each marked
  `covered` or `not covered` with a reason — stdin/stdout case under time and memory limits, each verdict class, HTTP
  scenario step, HTTP concurrency scenario (k clients at once), TCP scenario with split frames, pipelined requests and a
  slow reader (the driver's capabilities, not a task's held-out content), readiness wait within T, step deadlines, RSS sampling every second (descriptive), the cgroup-accounted memory bound in the program's own scope with `memory.peak` and `memory.events`, SIGTERM clean stop within G and the
  grace period, a program that ignores SIGTERM, a leftover child, cgroup `memory.peak` against the sampling fallback, the
  offline cache in the scoring box, the scoring-time record; (iii) **explicit limits on what the harness proves** — it
  proves a program's observable behaviour against the registered cases and scenarios on this machine under these limits,
  not correctness in general; a Python driver bounds the load it can apply; RSS is sampled, so a peak shorter than the
  sample interval can be missed unless `memory.peak` is present; the stop check sees the session's processes, not threads;
  a case classed ambiguous is a spec matter, not a harness proof. A test (T-L17's shape) asserts that every flow id named in
  the drivers' test files appears in the catalogue.
- S3 — the stdin/stdout judge with cgroup limits and verdict classes, **on E1.S8b's layer** (the scope is the layer's; the
  judge adds the case loop and the verdicts); memory certified only by `memory.peak`, else the
  `memory bound not certified` flag and no memory pass (§4.3). *Accepted when:* T-L3, T-L28, T-L29.
- S4 — scoring per kind in the pipeline, `accepted`/`percent`/`defects` from the trap table, scoring time recorded; the
  process arm scored on the run base tip or the PR head with the `delivered` mark. *Accepted when:* T-L11; a stub tree of
  each kind in each arm gets a `score.json` with every step known.
- S5 — D15's environment, the widened classifier, `aaVerdicts` G-15 … G-19, CPC and the overall figures in the record
  tools, the bare smoke. *Accepted when:* T-L14, T-L15, T-L16, T-L18, T-L19; the smoke's record is complete and its cost is
  in the ledger. *(2026-11-02, D55:) D15's base in the agent box — `NODE_OPTIONS` empty, no hard limit; the per-case heap keys
  are the scoring box's, derived by the layer, and T-L14 fails when one is planted in the agent box's environment.*
- S6 — **coai in its own box and the stdio↔socket bridge** (v1 §7.2 and E2.S3's design; a cornerstone area, unmarked —
  see the recount above): one `coai-mcp` per process session as the operator, in a second `bwrap` that binds only the
  session directory (ro, plus `<session>/coai` rw), the vendor CLIs' own homes, the coai binary and the toolchains, `/home`
  and `/mnt` otherwise hidden; `COAI_DATA_DIR=<session>/coai`; the series' coai settings snapshot with `COAI_QCONSULT_ROOTS`
  unset (v1 D14); the bridge inside the agent box parses each JSON-RPC `tools/call` and refuses any path-shaped argument
  that does not resolve inside the session directory, answering a JSON-RPC error naming the rule; `--strict-mcp-config` and
  `mcp.json` pointing at the bridge; `ToolSearch` in the tool list for deferred schemas. *Accepted when:* T-L23; a
  `providers` call through the bridge from inside the agent box lists the vendors with their own sign-ins while the agent
  box holds none of them; a `consult` whose `repoPath` is this repository is refused; the held-out key is in neither box's
  environment (a test reads both `environ`s); a reviewer round runs end to end against a stub repository in the session.
- S7a — **the GitHub broker with an isolated bare repository per run** (D42; a door of the credential broker's shape, E3.S7
  depends on it): a host-side HTTP(S) door the box reaches as its `github.com` (the proxy allows no direct CONNECT). Per run
  it **creates a local bare repository** holding only the orphan run-base commit (D39) and the run's refs; the agent's
  `git clone`, `fetch` and `push` are served from that bare repository by the broker's smart-HTTP endpoint (no object
  outside it exists to be fetched — by name, by SHA or by unshallowing); after each push the broker **mirrors** the run's
  branch to GitHub with the PAT; the REST and GraphQL calls that create and update the own PR and read its checks and
  comments are made **by the broker on the agent's behalf**, for the allowed operations only; everything else — another
  repository, another run's ref or PR, the task repository's `main`, any redirect outside the broker — is refused with a
  named error and a log line. *Accepted when:* T-L35 (the SHA and unshallow probes included); a stub process session
  completes the normal clone → branch → push → PR → checks → merge workflow through the broker against a `run/smoke-*`
  base while every denied probe fails; the mirrored branch on GitHub equals the bare repository's; the PAT is in no box
  environment or argv; the broker's log is in the session record; G-20 pilot-tests it on both process pilot pairs.
  *(2026-11-01, D52:) plus a host-side publication gate: every outgoing object and API payload is scanned before it is
  mirrored or posted, tested with the task repository's client-side hook disabled (T-L45).*
- S7 — **the process runner**: the process session verbs over v1's bare runner (E3.S1's dimensions) — run base `run/<rid>/main`
  cut from the frozen tag with the deploy key, the single-branch clone with a restricted refspec and `--no-tags`, the PAT in
  the session gitconfig through `gh auth git-credential` (never on an argv), `GH_CONFIG_DIR`, `GH_PROMPT_DISABLED=1`, the
  GitHub broker of S7a as the box's only road to GitHub (D42; no direct egress), `github.mjs` (PRs, checks, merges, intervals for `ci_wait`) on the harness side with the PAT, `delivered` read from the merge
  into the base, an undelivered session scored on its PR head and marked, the audit's foreign-namespace rules (pushes and
  PRs outside `run/<rid>/`, other `run/*` reads), the token scan over pushed files, PR bodies, comments and commit messages,
  the void `no reviewer`, the open PR of a void session closed with a comment. *Accepted when:* T-L24; a stub process pair
  runs end to end against the task repository on a `run/smoke-*` base with the stub CLI (free): base cut, clone, a
  harness-authored PR, CI, merge, `delivered`, the record complete; a direct push with the PAT to a foreign `run/*/main` is
  refused by the rulesets (read back).
- S8 — **the compliance checker** over GitHub, `coai.db` and the stream (v1 §5.4's 17 items plus `spec unchanged`), with the
  stratified hand-check protocol (≥ 20 verdicts, every item and every process cell, every "not met"; a disagreement above
  1 in 20 fixes the checker before any number). *Accepted when:* T-L22; the checker computes every item for the S7 stub run
  and for a synthetic run with each item broken once; C#-only items are excluded from the cross-stack score by configuration,
  not by hand; the hand-check sheet is generated.
- S9 — **P0 (no model) and the model smoke** on the task repository (v1 §15 step 8's checks, adapted): a harness-authored
  PR per stack folder to `run/smoke-*/main`, base creation by the deploy key, a direct PAT push refused, CI on a non-default
  base, a planted lint or analyzer violation red per stack, merge blocked until threads resolve and a thread resolved with
  the PAT unblocks it, push CI green; from inside the agent box the operator's home and the held-out suite are unreadable;
  then the model smoke (≈ $0.10): one headless process session whose only prompt is to call coai `providers` through the
  bridge. *Accepted when:* every P0 check passes on both stack folders and is recorded; the smoke's init event shows coai
  connected and the vendors listed; the smoke's cost is in the ledger as pilot spend.
- S10 — the module document: the extension described (entry points, exit codes, the drivers' contracts, the judge's
  classes, the arms, the bridge's rule, the process verbs). *Accepted when:* every command in it runs as written.
- **Cornerstone areas:** the drivers (S2, marked); the bridge on every `tools/call` (S6, unmarked — ledger full).

### E4 — Pre-registration, A/A and the two pilots (§14 step 4 · 4 stories)

- **Goal:** the analysis fixed in public history before the first paid task session; the instruments proven on real trees in
  both arms; the caps and the split derived; the seed revealed. *(Superseded by D7, D53 and D44 — second full reading,
  2026-11-01, finding 6: no cap is derived and there is no split; the goal is per group — each group's pre-registration
  registered, its projection line and settled ledger lines, its instruments proven on real trees in its arm, its seed
  revealed, before three rounds of that group: bare in A and B, process in C.)*
- **Scope:** §9, §11, §12; D4, D8 (the CLI re-pin), D16, D26, D30.
- **Depends on:** E0.S3 (the parity release pinned), E1–E3 complete.
- **Staged (D44, D45, D47, D48; 2026-11-01).** Per group: E4-A, E4-B, E4-C, each depending on that group's slices of E1–E3;
  E0.S3 only for E4-C. *Done when, per group:* its tag `prereg/lang-v2-<group>-<date>` before its first launch; its A/A as §11
  says (A: yes; B: only on a changed identity; C: none); its pilot; its seed revealed; its projection and settled lines in the
  ledger (D53). S1's CLI pin is taken once, at E4-A (D47).
- **Done when:** the tag `prereg/lang-v2-<date>` exists before the A/A's first launch and after E0's release; `AA.md` says
  GO; the bare and process pilot pairs ran; the stall and limit amendments are dated lines and the cap cross-check is in the record (the cap itself is the owner's $50, D7); `seed.txt` revealed;
  pilot spend in the ledger per arm. *(Superseded by D7, D53 and D44 — finding 6, 2026-11-01 — for the staged series: the
  per-group Done-when above holds — the group's tag before its first launch, its A/A and pilot as §11 says, its projection
  and settled ledger lines (D53), its seed; the cap is the owner's $50 and the pilot formula's cap is a reported cross-check,
  never a derived cap or a split.)*
- S1 — the day-1 price re-read and the CLI pin copy (D8, Q10). *Accepted when:* `prices-<date>.json` dated that day; the CLI
  copy's sha256 recorded.
- S2 — `prediction.md` (§9's draft, finalised), `statement.md`, `analysis-contract.md` (CPC, the per-task and overall
  figures, known/original, the void and re-run grounds), the outcome table, the limits, the package sets, the hashes, the
  specs' freeze tag and hashes, the parity release sha, `seed.sha256`. *Accepted when:* v1's prereg tests widened pass
  (verbatim-from-main, derived not typed, seed as hash only, no held-out needle).
- S3 — the commit and the tag, before any paid session. *Accepted when:* the guard peels the tag to the commit. *(2026-11-03, D63 (6), judge J6:)* the group manifest committed here carries `pidsMax: 512`, read from `lib/exec-layer/scope-entry.sh` and equal for both stacks; T-L58's `pidsMax` half (`The_group_manifest_pidsMax_equals_the_layer`) is green before each group's tag. *(2026-11-05, D65, D67 (3):)* it also carries the workspace commit, the build contract's identity and the `deviations` list; S2 registers the stack and framework sentences as phase B wrote them, and the T3/T5 policy stays a dated amendment after the A/A (§9).
- S4 — the bare A/A, the bare pilot pairs (tasks 1, 3, 4, 5, 10; D16), the process pilot pairs (tasks 1, 3; Q7), G-1 … G-19, the
  cap cross-check (§12: the pilot formula's cap per arm reported beside the owner's fixed **$50** cap, D7), the amendments,
  the seed reveal. There is no total ceiling and no `budget-frame` halt (superseded, D7); the runner's cap constant is
  **$50** for every session of both arms, and `run --what round:1 --dry-run` prints the six planned rounds with that cap.
  *(Finding 6, 2026-11-01, D44: per group the dry run prints that group's three planned rounds — bare in A and B, process in
  C — with the $50 cap.)*
  *Accepted when:* GO; every amendment is instrument-only or applied to both stacks; the pilot's settled costs and the
  formula's cross-check cap per arm are in the ledger and the record; no pilot session's settled cost exceeded $50
  without the stop recorded as `killed: cap` (T-L10).
- **Cornerstone area:** none marked (the go/no-go table is written strictly, as v1's E3).

### E5 — The paid rounds (§14 step 5 · 2 stories)

- **Goal:** the six alternating rounds run under the pre-registered contract, under the gate, every attempt kept.
- **Scope:** §4.4 order, §9 voids and re-runs, §12's gate and stop rules, the round-1 read-back per arm.
- **Depends on:** E4's GO and seed.
- **Staged (D44; 2026-11-01):** three rounds per group — A: four tasks, bare; B: ten tasks, bare; C: ten tasks, process (plus
  the bare anchor round — mandatory, D54, interleaved among C's three process rounds). A group's paid sessions start after the previous group's interim
  record; S2's read-back runs in each group's first two rounds. *(2026-11-02:)* the goal's "six alternating rounds", the
  done-when's "six rounds ran" and S1's "rounds 1–6" read: three rounds per group, nine data rounds in the series plus C's anchor
  round (D44, D54); the gate or a stop ends a group and the record names its missing round pairs. *Accepted, added:* T-L55 — C's
  schedule holds the anchor round, its sessions carry `role: anchor` and are never pooled with bare data.
- **Done when:** six rounds ran, or the gate or a stop ended the series and the record names the missing round pairs; every
  session has `record.json` with its timestamps, every pair its pair record; the read-back lines are dated.
- S1 — rounds 1–6 with the stop rules; resume after any halt. *Accepted when:* every session is classed; voids re-run by
  the schedule; spend lines settled per arm; no round k+1 started with round k unscored.
- S2 — the round-1 and round-2 by-hand read-back of one pair per arm (argv, read-back, binds, cache hashes, package list,
  env keys, the spec blob, in the process arm the pin and coai's `providers`) by an Opus judge, as dated lines; a mismatch
  reclassifies through `handVerdicts`. *Accepted when:* the lines exist before round 3.
- **Cornerstone area:** none.

### E6 — The research record (§14 step 6 · 3 stories)

- **Goal:** a generated record in `research/lang-v2/` that types no number, puts CPC first per task and the pre-registered
  overall figures second, says what §9 licenses, and publishes what the series held back.
- **Scope:** §8's views, §9's tables, the blind adjudication, the replay of every void, the references, the trap table and
  the specs published, teardown, archive and retire, promotion.
- **Depends on:** E5.
- **Done when:** every §16 box is checked.
- **Staged (D46, D49; 2026-11-01):** S1 and S2 run per group, each group's interim record carrying counts and rates only; B's
  carries the A–B drift table; S3 (publication) and the final record run after C.
- S1 — `research/lang-v2/tools/` from `research/minigit/tools/` widened to arms, tasks and kinds; `data/<series>.json`;
  `RESULTS_lang_v2_<date>.md` with generated blocks: per task (CPC or the undefined outcome, success, tokens, time, both
  arms), the overall median, geometric mean and count per arm over the defined ratios with their denominators and the
  undefined line, known against original, process effect and interaction with the tasks they leave out named, every
  attempt listed, **and every v1 metric beside the new ones (D34)**, **with the disclosures of D41 and D42**: the trap
  design aid was public in #124, #127 and #128 before it moved to the private area (T18), and the process arm's GitHub
  access was run-bound by the broker so that the process-arm result applies to "two stacks under the same run-bound
  GitHub policy" (T19, D42), each as a sentence in the record's threats and beside every process-arm
  table; *(2026-11-05, D65, D66:)* with `deviations` per task, stack and arm beside `accepted`, the `strict job` column, the lint invocations and outcomes, and T15's and T23's sentences. *Accepted when:* T-L17, T-L26 (metric retention:
  the four cost views and T1–T5, tokens with the cache split, the partition with every category span, visible and held-out
  quality, class, labels, flags, charges, the compliance items), T-L27; the record renders from the data alone.
- S2 — the blind adjudication of every failing case; the voids replayed with the current scan; the prediction beside the
  observation. *Accepted when:* every failing case has a step-1 and step-2 verdict; the revised score sits beside the
  primary.
- S3 — publication (references, the trap table and the specs' final state in the task repository, Q11), credential
  teardown, archive, `retire.mjs`, `research/README.md`, promotion of this plan and its stories documents with deviations.
  *Accepted when:* `plan-lifecycle.mjs` clean; PATs revoked and the deploy key deleted; the worktree removed.
- **Cornerstone area:** none.
