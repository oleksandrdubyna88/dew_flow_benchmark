# PLAN — investigation quality and implementation quality, measured apart

> Status: **steps 1–5 implemented 2026-08-20 (the arm axis; the diagnosis contract and its scoring;
> the harvest — FixDiff, FixHarvest, gates, bank landing; phases in the runner; investigate-only
> end-to-end with the contract in the prompt and `bench run --task-kind fix`); steps 6–8 open.**
> Scope: `Bench.Domain` (the
> arm axis, the diagnosis contract and its scoring), `Bench.Application` (phase execution, the harvest
> pass, the diagnosis judge), `Bench.Infrastructure` (the measured-CLI subject runtime, per-leg
> worktrees), `hosts/Cli`; one additive migration on `cells`.
>
> Sits ON TOP of [PLAN_code_lane.md](PLAN_code_lane.md): that plan owns the sandbox, the mechanical
> signals and the delivered-work score; this one decides that its phases are also **arms** — so the
> investigation and the implementation become two separately measurable tests instead of one composite.
> The two meet at `PhaseKind`/`PhasePlan` and at `CodeTaskJson`; a change that crosses is named in both.
> The tool loop and the CLI-agent telemetry reconstruction stay in
> [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) (§3.5, step 11); this plan consumes them and builds
> neither.
>
> Related: [PLAN_variant_matrix.md](PLAN_variant_matrix.md) (subjects, bank, matrix),
> [../research/architecture.md](../research/architecture.md),
> [../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) §4b–§4d.

## 1. The goal, before any solution

The operator's observation — *"Sonnet searches no worse than Opus and Fable"* — is real but has never
been one measurement. It is a synthesis of two series that do not overlap: Opus ≈ Fable on five graded
tasks with no Sonnet in the room (`DewFlow · research/PLAN_mcp_eval_v4/RESULTS.md:216-250`, Fable at
1.5–2× Opus's cost for equal correctness), and Sonnet at 74/77 against Opus's 77/77 with tools at
roughly **60 % of the price** (`DewFlow · research/PLAN_eval_v8/RESULTS.md:224-257`), with no Fable in
the room. The three-way head-to-head was planned and never ran: the reading material saturated for the
strongest model alone — 46/47 — and no lane could be ranked against another
(`DewFlow · research/PLAN_eval_v6/PLAN.md:10-14`). §4b of
[MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) is the same finding on this harness's own corpus.

So "which model searches better" is a question the reading instrument can no longer answer, and the
question the operator actually asks is sharper anyway: **which model is better, cheaper and faster at
INVESTIGATING a defect, and which at IMPLEMENTING the fix — separately.** Those are different skills, a
team would happily buy them from different models, and today no measurement can price either one alone:

- [PLAN_code_lane.md](PLAN_code_lane.md) runs `Investigate → Fix → Verify → Judge` as phases of ONE
  leg, so every mechanical signal scores the composite. A cheap model that investigates brilliantly and
  fumbles the diff is indistinguishable from one that guessed the diagnosis and got lucky editing.
- The `Investigate` phase produces **no scored artefact at all** — the arbiter's "was the diagnosis
  right, or was a symptom patched" (`PLAN_code_lane.md` §5.4) is the only reading of investigation
  quality, and it arrives last, filtered through whatever the implementation did to the evidence.

What this plan builds: the fix task becomes runnable as **three arms** — investigate-only,
implement-given-diagnosis, and the full leg — with the diagnosis as a first-class scored artefact, over
subjects that include **CLI-driven Claude models (Sonnet, Opus, Haiku) and local models**, against
`dotnet/aspnetcore` at pinned commits. The first series is the operator's matrix; Codex/Gemini/Fable
are later columns, not design changes (the registry already names their runtime kinds —
`src/Bench.Domain/Registry/ModelConfig.cs:7-20`).

**And the split is not only a measurement — it is the cheap half arriving early.** An investigate-only
leg reads a tree and writes a diagnosis; it builds nothing and runs no tests, so the
[PLAN_code_lane.md](PLAN_code_lane.md) §4.2 isolation gate does not bind it. Investigation quality can
be measured unattended while the sandbox work is still open; implementation quality waits for the
sandbox, exactly as that plan already requires.

## 2. What exists today, verified

| capability | state | where |
|---|---|---|
| `TaskKind.Fix`, phases `Investigate → Fix → Verify → Judge`, no-start-before-done, a cap stops the leg | **built, unused** — `LegRunner` never starts a phase | `src/Bench.Domain/Runs/LegPhase.cs:82-88, 95-108` |
| Per-phase time buckets and cost (`Tools`/`Thinking`/`InfrastructureWait`/`CostUsd`) | built | `src/Bench.Domain/Runs/LegPhase.cs:52-70` |
| Budgets with `AcceptedBy` — an unconfirmed ceiling is marked, never believed | built | `src/Bench.Domain/Runs/Budgets.cs:28-37` |
| `LegOutcome` closed union; capped/crashed legs never enter paired deltas | built | `src/Bench.Domain/Runs/Budgets.cs:45-82` |
| The matrix as question × repeat × subject × lane × variant, additive `Leg.Canonical` | built — the exact additive-axis pattern the arm axis mirrors | `src/Bench.Domain/Runs/Matrix.cs:8-21, 50-69` |
| One ask per leg, prompt-is-the-artefact, score-then-settle | built | `src/Bench.Application/LegRunner.cs:220-297` |
| Anchor matching and the *not applicable* fairness rule | built — the matching and honesty rules diagnosis scoring reuses | `src/Bench.Domain/Runs/AnswerScoring.cs:87-115` |
| Headless argv for Claude/Codex/Gemini CLIs, stdout-only reading, tail-not-head failures | built — as harness WORKERS (authoring/review), not subjects | `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:36-62, 119-151` |
| Claude CLI JSON envelope: `usage` tokens (cache read/creation summed into input), authoritative `total_cost_usd` | **built upstream, not here** — the parse shape to port | `DewFlow · src/v2/v2.Agents/Services/AiRunnerService.cs` (`BuildClaudeResult`/`TryParseClaudeJson`) |
| Judge: binary verdict, temp 0 + seed, per-arbiter series, `selfJudged` marked, refusal-never-NO | built | `src/Bench.Infrastructure/Models/ModelJudge.cs`, `src/Application/JudgeRunner.cs` |
| Read-only checkout: bare mirror + worktree per commit; author's git history handed via `GitHistory` | built | `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs` |
| Report axes + `Unproven`/spread discipline | built | `src/Bench.Application/RunReport.cs` |
| The sandbox executor, mechanical signals 0–4, delivered work | **plan only** | `PLAN_code_lane.md` §4.2, §5 |

Nothing below invents a new discipline; every piece extends a pattern this repository already enforces.

## 3. The design

### 3.1 The arm is an axis: `FixArm`

```csharp
// Bench.Domain/Runs/FixArm.cs
public enum FixArm
{
    Full,            // Investigate → Fix → Verify → Judge — the composite, PLAN_code_lane unchanged
    InvestigateOnly, // Investigate → Judge — the diagnosis is the deliverable
    ImplementOnly,   // Fix → Verify → Judge — the reference diagnosis is handed in the prompt
}
```

`PhasePlan.For(TaskKind, FixArm)` extends `PhasePlan.For` (`LegPhase.cs:82-88`); `TaskKind.Reading`
**refuses** any arm but `Full` by name — an arm the kind cannot honour must fail the plan, not load as
something else. Cells gain an `Arm` column defaulting `Full`; `Leg.Canonical` appends the arm **only
when it is not `Full`**, mirroring `VariantSelection` at `Matrix.cs:16-20`, so every identity stored
before this axis existed still means what it said. The arm joins `ReportDimension` the same way lane
and variant did — a column the cell carries, a `GROUP BY` away.

Why arms and not three task kinds: the three arms run **the same question** — same statement, same
`baseCommit`, same hidden tests, same reference fix. Three kinds would be three bank entries that must
agree byte for byte and will not; one question crossed with an arm axis is the matrix doing what it is
for.

### 3.2 The diagnosis is a contract, and the harness extracts it

The `Investigate` phase (standalone or inside `Full`) must end its answer with one fenced JSON block:

```json
{
  "anchors": [{ "path": "src/…", "member": "Type.Member", "lines": { "start": 120, "end": 141 } }],
  "mechanism": "why the defect happens — the causal chain, not the symptom",
  "fixIntent": "what should change and where; no diff required"
}
```

Extraction follows the authoring pass's own rule (`research/architecture.md`, *Where questions come
from*): the block is EXTRACTED from a fence or a prose preface, nothing repairs a malformed one, and
what the model said around it is kept — the judge reads the whole text, the mechanical scorer reads the
block. Three states, never two: **parsed**, **absent**, **malformed (with the parse error)**. A model
that cannot follow the output contract is a real, reportable fact — `Diagnosis parses` is its own
boolean metric — but it is a different fact from a wrong diagnosis, and the two must not share a zero.

### 3.3 Scoring a diagnosis — mechanical first, judge beside

`DiagnosisScoring` (pure domain, beside `AnswerScoring` — never merged into it):

| metric | how | why |
|---|---|---|
| `Diagnosis parses` | boolean, §3.2 | the contract itself is measurable |
| `Diagnosis anchor recall` | named anchors matched against the **reference fix's touched members/spans** — by canonical `Type.Member` identity or line overlap, the one matching rule the system already has (`AnswerScoring.cs:106`, `research/architecture.md` *Anchor recall*); never by name suffix | did it find the place |
| `Diagnosis precision` | matched anchors / named anchors | the shotgun guard: a diagnosis naming twenty places scores recall 1.0 and precision 0.1, and only the pair says which model actually knew. Without it, "name everything" is the winning strategy and the metric teaches models to be vague |
| `Symptom-only` | fired when every matched anchor is in the task's authored `symptomAnchors` and none is causal | the trap half. A code task MAY name the place the defect *manifests* (the failing call site, the surfacing exception) as distinct from where it is *caused*; naming only the symptom is precisely the failure the operator wants caught, and it is invisible unless something asserts it — the `AnswerExcludes 'consecutive'` lesson (§4c), one lane over |
| `Answer contains/excludes '…'` | the existing text expectations over the mechanism prose, unchanged | memorisation traps and required causal terms ride the machinery that already exists |
| `Diagnosis verdict · {modelId}` | the arbiter: binary, *"does this mechanism explain the actual defect, against the reference mechanism?"* — temp 0, seed, per-arbiter series, `selfJudged` marked, refusal-never-NO | the part no assertion covers, sitting BESIDE the mechanical rows exactly as `bench judge` already does |

**The mechanical ground truth is DERIVED, not authored.** The reference fix's touched members are
computed from its diff — the same principle as seed dates (*the author names the change and the
repository dates it*, `research/architecture.md`): an authored anchor list would be a second copy of
the diff that drifts. The authored parts are the `mechanism` reference text and the optional
`symptomAnchors`, and both go through the reviewers like everything else.

### 3.4 `ImplementOnly`: what is handed, and what is recorded

The prompt carries the statement plus the **reference diagnosis** — anchors and mechanism, never the
reference diff, never a hidden test. The stored prompt is the artefact, as everywhere. The leg records
`DiagnosisSource = Reference`; the type has a second case, `Leg(cellId)`, deliberately declared now and
used later — a stored investigate-only diagnosis fed to another model's implement leg is the *mixed
pipeline* ("Haiku investigates, Sonnet implements"), and it becomes one query the day both arms exist.
Scoring is [PLAN_code_lane.md](PLAN_code_lane.md) §5 unchanged: signals 0–4 plus delivered work. This
plan adds no implementation metric — the point is what is held fixed, not a new score.

### 3.5 Where the code tasks come from: harvest first, author second

[PLAN_code_lane.md](PLAN_code_lane.md) §3.2 authors tasks from open issues, with three gates that need
a sandbox and a build. This plan adds the cheaper door and takes it first: **harvest a merged bug-fix**.

Pick a real fix commit `F` in the target; then `baseCommit = F~1`, the statement is the linked issue's
symptom text (scrubbed of fix vocabulary), the **reference fix is F's own diff**, the **hidden tests
are the tests F itself added or changed** (held outside the corpus, per §3.1 of the code-lane plan),
the seed is F's commit date — derived from the repository, not authored. The three gates still run,
once, at harvest time: rebuild at `baseCommit`, hidden tests red; apply the reference diff, green,
neighbours intact. A fix whose tests were not red at its own parent is refused with that reason — it is
the *"bug already fixed on HEAD"* trap (§4b) caught structurally.

Harvesting is cheaper than authoring because the reference fix and its tests already exist and already
passed a real project's review; what remains model work is the mechanism text and the statement scrub,
vetted by the reviewer panel as usual. Memorisation is handled by the discipline the bank already has:
the seed date against each subject's cutoff (`may recall` is marked, never hidden), preference for `F`
newer than the newest subject cutoff, and the statement carrying the symptom, never the fix.

### 3.6 Subjects: local models now, measured Claude CLI next

**Local models** (`OpenAiEndpoint`, live today) run investigate-only in the shapes `LegRunner` already
serves: lane `no-tools` (the floor — a diagnosis from weights alone is the memorisation check, §4c) and
the single-shot retrieval variant (diagnosis from retrieved context). The agentic investigate for local
models waits for `ToolLoopRunner` ([PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) steps 2–4) and is
not duplicated here.

**Claude Sonnet / Opus / Haiku as measured subjects** — the new infrastructure, `CliSubjectRuntime`:

- **A subject, not a worker.** `ICliAgentRuntime` launches processes that work FOR the harness;
  measuring one AS a subject is the distinction `research/architecture.md` warns not to collapse. The
  subject path is its own adapter, and where it meets
  [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) step 11 (`CliAgentRuntime` + telemetry
  correlation), whoever lands second reads the other's diff.
- **Headless, model pinned, stdout only**: `-p --model <id>` plus `--output-format json`
  (`CliArgv.For`, `CliAgentRuntime.cs:36-62`; the envelope is why `--output-format json` is added for
  the subject path). Cost and tokens are read from the envelope — the parse shape ported from
  `DewFlow · src/v2/v2.Agents/Services/AiRunnerService.cs`: cache read/creation tokens summed into
  input so the total reflects everything billed, `total_cost_usd` authoritative when present and > 0.
  Unreported usage is **not captured**, never zero — the `Captured` discipline.
- **One worktree per leg, never the shared per-commit one.** An agent that writes into the shared
  worktree poisons every sibling leg at that commit. Each CLI leg gets a disposable worktree; the leg's
  settings deny write/edit tools (the `WorkspaceTrust` file discipline — only paths under the checkout
  root, siblings preserved); and after the leg the harness runs `git status --porcelain` there — a
  non-empty tree marks the leg `WroteToWorktree`, evidence rather than assumption.
- **Wall is the enforceable ceiling; turns are not.** A CLI's inner loop cannot be turn-capped from
  outside, so a `Turns` budget for this runtime is recorded **unaccepted** (`Budgets.cs:28-37`) and the
  leg is bounded by `LegDeadline` alone. Tool-call detail arrives later, reconstructed from telemetry
  (`PLAN_tool_benchmark.md` §3.5) — stored as reconstructed, never blended with observed.
- **Sampling is not controllable** over the CLI: `SamplingAsSent` records *not captured*, honestly.

Cross-presentation comparisons (a CLI agent's own loop against a local single-shot) carry the caveat
[PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) §3.2/§3.8 already states; same-presentation is the
trustworthy default, and the report prints the scope beside the number.

### 3.7 Better, cheaper, faster — the report

Every ingredient is already a per-leg fact: quality (the §3.3 diagnosis metrics for investigate arms;
signals 0–4 and delivered work for implement arms), cost (`LegPhase.CostUsd`, envelope-fed for Claude,
registry-priced for local — an unknown cost prints unknown), speed (wall, and the three time buckets
where observable). The report is `RunReport` with `Arm` as one more dimension: per arm, subjects side
by side, spread printed beside every difference, `Unproven` as a word, thin means shown and not ranked.
No composite "value" score is invented — a Pareto table reports; the operator judges.

The first series' own success criterion is **discrimination, not scores**: if every subject saturates
the investigate set, the finding is "these tasks do not separate these subjects — harden the tasks",
per the rule that nothing is pruned for being easy and a comparison owns its spread.

## 4. Sizing — pilot before series

Priors from the reading grid (`DewFlow · research/PLAN_eval_v8/RESULTS.md`): ≈ $1.63/leg Opus,
≈ $1.00/leg Sonnet, ≈ $0.33/leg Haiku with tools on a small corpus. An agentic investigate leg over
aspnetcore will cost more and nobody has measured how much, so the pilot comes first:

- **Pilot**: 1 harvested task × 7 arms (3 Claude CLI · 2 local × {no-tools, rag-variant}) × 2 repeats
  = 14 legs. Deliverable: per-leg cost and wall per subject, and the diagnosis contract exercised
  end-to-end. Nothing else is scheduled until these numbers exist.
- **First series**: 8 harvested tasks × the same 7 arms × 2 repeats = 112 investigate-only legs,
  unattended-safe. Budget projected from the pilot, confirmed before any cell exists
  (`BudgetConfirmation`).
- **Implement/full arms**: sized after the investigate series, gated on the code-lane sandbox
  (attended-only until its three isolation assertions pass — that gate is not weakened here).

## 5. Build order

Each step ships alone, tests green, before the next.

1. ~~**The arm axis**~~ **DONE 2026-08-20.** `FixArm` + canonical tokens, `PhasePlan.For(kind, arm)` and
   `Materialise(cell, kind, arm)` with the `Reading` refusal, `Leg.Arm`/`RunCell.Arm` as additive `init`
   members (the `EngineRef.Backend` pattern), the sixth matrix axis with its empty-list refusal, the
   `cells.Arm` column (migration `20260820073037_FixArmColumn`, default `Full`), and the report
   dimension. RED was recorded (CS0246 on `FixArm`), then 36 domain tests green and the full suite
   901/0/9 with every pre-existing matrix/phase/canonical test unchanged.

   **One deviation:** the report dimension is named `ReportDimension.FixArm`, not `Arm` — the report's
   own vocabulary already uses *arm* for "one compared configuration" (`ArmReading`, `ArmOf`), and a
   dimension named `Arm` beside it would make every reading ambiguous.

   **And one honesty note, recorded in `research/architecture.md`:** nothing RUNS a non-Full arm yet —
   no CLI flag plans one, deliberately, because `LegRunner` starts no phases and a cell whose arm the
   runner cannot honour must not be creatable. The flag arrives with step 4/5.
2. ~~**The diagnosis contract + scoring**~~ **DONE 2026-08-20.** `Diagnosis`/`DiagnosisAnchor` and the
   three-state `DiagnosisReading` (parsed · absent · malformed-with-the-error) in the domain;
   `DiagnosisScoring` emitting parses / anchor recall / anchor precision / the symptom-only trap, with
   a malformed reading scoring NO anchor numbers; `DiagnosisJson` in the Application layer over the
   authoring pass's own `AgentJson` extractor. 20 tests, RED first; the full suite 921/0/9.

   **Three decisions the plan had not fixed, decided in place:**
   - **The matching rule is shared, not copied.** `AnchorMatching` (SamePath, whole-string-ordinal
     SameMember) was EXTRACTED from `RetrievalScoring`'s privates and both scorers now call it — one
     definition of "found" in the system, per the reuse-first rule's widen-don't-duplicate step.
   - **Line claims match by OVERLAP, not coverage** — a hit returns code, a diagnosis only points —
     and a bare file claim (no member, no lines) reaches only a whole-file truth, so "somewhere in
     this file" cannot inflate recall on member-level ground truth.
   - **Unknown JSON members are tolerated.** The `Disallow` discipline guards configurations, where a
     stray field silently becomes a different arm; an agent's answer is a payload, and the measured
     half of the contract is what must be present (the mechanism), not what must be absent.
3. **The harvest pass** — `bench questions harvest --fix-commit <sha>`: derived `baseCommit`, seed,
   reference-diff ref and hidden-test extraction; the three gates run attended; candidates land
   `Proposed` and are vetted by the existing panel. Meets `PLAN_code_lane.md` §3 at `CodeTaskJson` —
   named there when this lands.

   **The pure half is DONE 2026-08-20**: `FixDiff` (`Bench.Domain/Authoring/FixDiff.cs`) parses a
   unified diff into per-file OLD-side change spans — the a-side, because the solver investigates the
   tree at `baseCommit` and a diagnosis names lines in THAT numbering — and derives the two things the
   arm needs: `CausalAnchors` (non-test files; a created file is no anchor at all, a deleted one is a
   whole-file claim, a rename anchors under its old name, far-apart hunks stay separate spans) and
   `TestFiles` (the hidden-test candidates, path-heuristic classification stated in place). 9 tests.
   Building it caught a real scoring defect before it shipped: `SourceAnchor.IsWholeFile` asks only
   about the member, so FixDiff-shaped truth (span, no member name) read as a whole-file claim and a
   bare file name reached it — watched RED (`recall 1` where the test demanded `0`), fixed with
   `IsWholeFileClaim` (member AND lines both absent), suite 942/0/9. What remains of this step is the
   verb itself: git extraction at `F`/`F~1`, the statement scrub, the gates, and the bank landing.

   **The git half is DONE 2026-08-20 too**: `FixHarvest.ReadAsync` (`Bench.Infrastructure/Git`) over
   `GitCommand`, the family's one launcher — resolves any ref to the full sha, derives `base = F~1`,
   reads the seed as the AUTHOR date kept as a calendar day (the `QuestionSeed.Written` lesson),
   subject/body, and the `base..fix` diff `FixDiff` parses. A ROOT commit and a MERGE commit are
   refused by name before anything is read — no buggy tree exists before the first commit, and a
   merge's diff lands a whole branch, which would ask a solver to rediscover a feature rather than one
   defect. Five integration tests over a temp repo and the real git.

   **And the verb exists in its report-only first form (2026-08-20)**: `bench questions harvest
   --repo <url> --commit <40-hex fix sha>` checks the fix out through the ordinary provider, reads it,
   and PRINTS the derived candidate — base, seed, causal anchors, hidden-test candidates — with its
   last line saying `printed only: no gate has run … nothing landed in the bank`, because a verb that
   looked like it had banked a task would be worse than no verb. A fix whose every change is in test
   files exits `NoReport`: an investigate arm would have nothing to score. Five command-level tests
   over a real temp repository (`DatedGitRepo`, beside the checkout cache's fixed-shape `TempGitRepo`
   with the read-only-delete lesson shared, not copied).

   **The tail is DONE 2026-08-20 — the step is complete.** `CodeTask` + `CodeTaskCodec` (a stored
   CONFIGURATION, so unknown members refuse the read — the `VariantJson` discipline, where
   `DiagnosisJson` tolerates extras because an agent's ANSWER is a payload; each codec says which trust
   shape it reads). The reference diff is stored once and whole — anchors and hidden-test files derive
   from it at use, never beside it. `HarvestGates` runs the two gates ATTENDED in a disposable scratch
   worktree (the shared per-commit tree is never built in): RED materialises the fix's own tests at
   base with `git checkout fix -- tests` and must fail — red-by-compilation is recorded as the weak
   kind and says so; GREEN moves the worktree to the fix and must pass; the worktree is removed on
   every path out, registry entry included. `bench questions harvest --statement-file … --statement-author …`
   lands the candidate `Proposed` in `code-writing` (the statement is the OPERATOR's — harvest derives
   the mechanical half and refuses to invent prose; `--statement-author` is required because a set's
   ceiling becomes its author's ceiling); gates run by default, `--no-gates` skips them out loud and
   the stored payload carries the warning label; **a failed gate refuses the landing with the gate's
   own verdict** — a bank row carrying a failed gate would read as a task somebody vouched for. The
   bank's `Kind`/`CodeTaskJson` columns had existed since the QuestionBank migration, waiting for an
   owner. 18 new tests (codec 5 · gates-over-real-git 4 · landing-over-real-Postgres 4 · verb 5);
   suite 1095/0/11.
4. ~~**Phases actually start**~~ **DONE 2026-08-20.** `LegRunner` materialises and drives the phase
   record for `TaskKind.Fix` legs: `leg_phases` (one row per phase, unique per cell+ordinal, enums as
   names), `IRunStore.EnsurePhasesAsync/SavePhasesAsync/PhasesAsync/CellAsync`. The investigate-only
   arm runs the ordinary ask INSIDE its Investigate phase — Done/Completed on success, the Judge row
   left Pending for the judge pass; a cap or crash stops the leg and the later phases go Stopped. The
   arms that produce a diff are refused BY NAME before any phase row exists (`blocked: … needs the
   sandbox executor … only investigate-only runs today`) and cost no completion. Shared prerequisite
   with `PLAN_code_lane.md` §7 step 1: built once, named in both plans.

   **Two decisions decided in place:** phases are CLOSED after the cell settles, from the stored
   outcome — every path out of a leg already settles the cell, so `PhasePlan.End` gained an overload
   taking the stored `(kind, detail)` pair rather than threading a phase handle through five exit
   paths (and rather than decoding a stored outcome, which `LegOutcomeCodec` deliberately refuses).
   And reading legs write NO phase rows — their single phase is the leg, and rows nobody transitions
   would be noise claiming to be record. The Investigate phase still carries the question's ORDINARY
   prompt: the diagnosis contract instruction and `DiagnosisScoring` wiring are step 5, which is also
   where `bench run` learns to plan a fix-kind run with `--arm`.
5. ~~**Investigate-only for local subjects, end-to-end**~~ **DONE 2026-08-20 — the first measurable
   milestone stands.** `DiagnosisPrompt` appends the contract to the ordinary leg prompt (the reading
   baseline is byte-identical — an arm that quietly reshaped it would no longer be comparable), and a
   test proves the contract's own example parses through the real `DiagnosisJson` — a contract whose
   example the reader refuses would teach every subject a broken shape. `LegRunner` scores fix legs
   with `DiagnosisScoring` beside the ordinary metrics: the causal truth is the question's own
   retrieval anchors (the harvest lands the reference fix's spans as exactly those — ONE set of
   anchors for the rag lane and the diagnosis), symptoms are not landed yet so the trap stays
   un-emitted, and an uncaptured answer reads *not answered*, never a failed parse. The front door:
   `bench run --task-kind fix [--arm investigate-only]` — the diff-producing arms are refused at PLAN
   time by name (a cell the runner can only block must not be creatable), an `--arm` on a reading run
   is a named refusal, and the full-CLI test shows a leg that died on transport still recording its
   phase attempt. What "judged" means today is unchanged honesty: code questions land with an empty
   reference answer, so `bench judge` reports them *not judgeable* — the diagnosis judge prompt is
   step 7's first half.
6. **`CliSubjectRuntime`** — per-leg worktree, deny-writes settings, envelope cost readback,
   write-detection, wall budget; then the **pilot** (§4).
7. **The diagnosis judge prompt + the first series** — 8 tasks × 7 arms × 2 repeats; report with the
   `Arm` dimension.
8. **`ImplementOnly`** — diagnosis-in-prompt, `DiagnosisSource`, running over the code-lane sandbox
   and signals once `PLAN_code_lane.md` steps 2–3 exist; attended until its isolation gate closes.
   Mixed pipelines (`DiagnosisSource.Leg`) as the follow-up query, not a new mechanism.

## 6. Test plan

- xUnit v3 executables only; `PostgresFixture` for anything touching a table.
- **Domain**: arm phase plans (including `Reading` refusing non-`Full` by name); canonical identity
  unchanged for `Full`; extraction's three states; recall/precision on hand-built diffs — the shotgun
  case (many anchors, one right) and the symptom-only case each pin their metric; a malformed block
  scores `parses = false` and no anchor numbers, never zeros.
- **Harvest**: a fix whose tests were green at its parent is refused with that reason (fixture repo);
  seed derived equals `F`'s commit date; hidden tests are provably absent from the worktree the solver
  sees.
- **Runtime**: envelope parsing from a real captured CLI JSON fixture (tokens summed, cost
  authoritative, absent usage → not captured); write-detection over a scratch worktree (clean tree
  passes, a written file marks the leg); a `Turns` budget for this runtime stays unaccepted.
- **Scoring fairness**: an investigate expectation in a lane that surfaces nothing follows the
  *not applicable* rule, never a miss.
- Every defect found while building gets its RED test first, watched failing for the real symptom.

## 7. Definition of Done

- [ ] One code task runs as three arms from the same bank row; the arm is on the cell, in the report,
      and absent from every pre-existing leg identity.
- [ ] An investigate-only leg runs end to end for a local subject AND a Claude CLI subject with no
      build step anywhere in its path — unattended-safe by construction, not by promise.
- [ ] A diagnosis is scored mechanically (parses / recall / precision / symptom trap) with the judge
      series beside it, self-judging marked; malformed and wrong are distinct states.
- [ ] Claude CLI legs carry envelope-fed cost and tokens; anything unreported prints as not captured,
      never as zero or as free.
- [ ] A CLI leg that wrote into its worktree is marked, and the shared per-commit worktree is never
      handed to a subject.
- [ ] `ImplementOnly` hands the reference diagnosis, records its provenance, and scores by the
      code-lane signals unchanged; it does not run unattended before that plan's isolation assertions
      pass.
- [ ] The pilot's per-leg cost and wall numbers exist before the first series is scheduled.
- [ ] The report answers better/cheaper/faster per arm with the spread beside every difference and
      `Unproven` as a word; no composite score is invented.
- [ ] `todo/README.md` updated; the boundary notes land in `PLAN_code_lane.md` and
      `PLAN_tool_benchmark.md`.

## 8. Open questions

1. **Fairness of wall-only ceilings for CLI subjects.** A CLI's inner loop cannot be turn-capped, so
   its arm is bounded differently from a future local tool-loop arm. The comparison-scope rule keeps
   the mixed comparison labelled; whether a shared wall is *fair* is decided with pilot data.
2. **How many harvested tasks discriminate.** Eight is a guess; the series' own `Discrimination`
   readings decide whether the bank's code group grows or hardens first.
3. **Fable and Codex as later columns.** Fable at 1.5–2× Opus for equal reading correctness earns a
   spot-check, not a standing column; Codex needs the envelope-less cost estimation path before its
   numbers mean anything. Both are registry rows away, by design.
4. **Whether the diagnosis contract shapes the investigation.** Forcing a JSON block may change how a
   model investigates (the register lesson, MEASURED_LESSONS §5). If pilot answers look
   contract-shaped, an A/B of contract wordings is one lane-doctrine-style arm, not a redesign.
