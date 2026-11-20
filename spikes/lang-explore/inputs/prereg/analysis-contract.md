# Analysis contract, Stage 1

The blocks below are quoted word for word from `todo/PLAN_lang_comparison_v2.md` at `2003e134d6ef0ac33f0c93d3832baeab0cca4ae3`, a commit of origin/main; `prereg/extract-contract.mjs` wrote them and a test checks them. What follows the marker is this pre-registration's own text.

<!-- quote: The goal, narrowed (parent the operative part of epoch 9) -->
> > On six tasks of two kinds — the daemons **05, 07 and 10** and the web APIs **04, 06 and 08** — each with the same functional
> > requirements and the same black-box checks for both stacks, with **Claude Code** (Opus, effort `high`), how do **C# 14 / .NET
> > 10** and **TypeScript 6.0.x strict on Node 26 with Fastify** compare in **cost per correct solution** — and beside it success
> > rate, tokens and decomposed time — for a **bare agent** with the task prompt only?
<!-- /quote -->

<!-- quote: The claim and its stated limit (D73) (parent the operative part of epoch 9) -->
> **The claim and its stated limit (D73).** Five pairs per task, 30 pairs in all. With the within-stack spread the quick run
> showed (two to three times between the cheapest and the dearest session of one stack on one task), five pairs separate
> per-task medians only at about **1.5 times**, and the 30 pairs read the **pooled direction**. **Neither reading can show a
> difference of 20 %**; the pre-registration says so, and the report repeats it beside every ratio. Two of the six tasks are
> known (05, 06) and four are original (04, 07, 08, 10); they are shown apart as a listing, not as a summary (D29).
<!-- /quote -->

<!-- quote: Outside the claim (parent the operative part of epoch 9) -->
> **Outside the claim.** The four olympiad tasks (01, 02, 03, 09) are exploratory controls: their quick-run re-run (24 of 24
> accepted) is cited in the report as such and is never pooled. The process arm is Stage 2. Nothing here is a claim about the
> languages in general.
<!-- /quote -->

<!-- quote: D24 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D24 | **Primary metric: cost per correct solution.** For task t, stack s and arm a: `CPC(t, s, a) = Σ cost of all data attempts of (t, s, a) / number of accepted solutions among them`. Beside it: success rate (accepted / attempts), tokens per model and kind, wall, `model_api`, `active − model_api`. A cheap unfinished run never looks like a winner: it adds to the numerator and nothing to the denominator. A (t, s, a) with **0 accepted** shows **"no correct solution"** plus its attempts and its total spend (labelled spend, not CPC) and is **not dropped**; its C#/TS ratio is **undefined** and reported as an outcome, never as a bound (§9, consult `68a33be2`). A second line counts void attempts in the numerator too (§9) | operator, 2026-10-31 (instruction E1); the void line: this plan |
<!-- /quote -->

<!-- quote: D28 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D28 | **Analysis is per task:** 10 tasks with repeated measures, not 120 independent samples. Each task is shown; the **overall result is the median of the per-task C#/TS ratios** (primary), the **geometric mean of the per-task ratios** beside it, and the **count of tasks where C# is higher** — per arm and per measure; pre-registered. No expensive task may hide the others: no pooled sum across tasks is a headline | operator, 2026-10-31 (instruction E5); the choice of the median as primary: this plan |
<!-- /quote -->

<!-- quote: D29 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D29 | **Known and original tasks are shown separately**, and the report discloses that the five public tasks may be familiar to the model | operator, 2026-10-31 (instruction E6) |
<!-- /quote -->

<!-- quote: D71 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D71 | **Repeated scoring and the verdict (change 8.2).** Pre-registered: ten scoring passes per session, five on the 2-CPU profile (CPUs `6-7`) and five on the 8-CPU profile (CPUs `0-7`), the same for both stacks, each pass after a read-back of its effective CPU affinity, never while a session runs, with a fresh program for every case and the same requests and deadlines. **`accepted`** = every visible and held-out case passed in all ten passes. **A pass with a genuine functional failure rejects the session, whatever the other passes did, environment failures included:** a real failure is evidence, and an environment failure is only the absence of evidence. A rejected session with at least one clean pass is labelled **`intermittently incorrect`**, never a majority pass; every attempt is kept. **The terminal rule, as one sentence a test checks:** *if any completed pass of a session holds a genuine functional failure, the session is `rejected` (or `intermittently incorrect` when some pass is clean), permanently, whatever any other pass did, with or without retries; it never enters the void or re-run schedule, and its cost counts in CPC's numerator like any rejected session.* **The retry unit is the pass:** a pass with an environment failure (D65's list: a box that cannot start, a holder that fails, a missing or wrong snapshot, a host fault) is re-run whole, at most twice, each attempt recorded. A session is **`unscored: environment`** only when no pass shows a functional failure and some pass is still environment after its two re-runs; it is counted per stack, never accepted or rejected, and enters CPC as a void (F3.S1: its cost on D24's void line, outside the primary CPC, re-run once by the void schedule). The references are validated under both profiles before the freeze. **Ten passes are a practical budget, not a proof**: a rarer race can pass all ten (T25). The real time of ten passes is measured on the known defective tree and both references before the freeze (F1.S2). *(2026-11-08, change 9.1, epoch 10: F1.S2 measures on the defective tree and accepted quick-run sessions of both stacks; the references are measured in F2.S1, where they are made.)* Acceptance stays the same for both stacks (D66) | the team lead, 2026-11-07, from consult `0a205591` and the quick run's data |
<!-- /quote -->

<!-- quote: D72 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D72 | **One memory value, stated to the agent.** The registered value is 536,870,912 bytes (512 MiB) for all six tasks. Each statement says: *"The program and its descendants run under a 512 MiB (536,870,912-byte) cgroup memory limit, including runtime overhead. Build resources are separate."* Each stack's statement also discloses its heap setting under D55: for C#, `DOTNET_GCHeapHardLimit` = 0x1CCCCCCC (483,183,820 bytes, 90 % of the limit); for Node, `--max-old-space-size=460` (MiB, 90 % of the limit). The statement, the visible load test's configuration and the scorer's limit are generated from the one value (F1.S2), and the references are validated at it before the freeze (F2.S1). A reference that cannot pass at it is fixed, or the value changes for all six tasks alike before the freeze, never for one. For the six tasks this supersedes D14's daemon and web memory rule, D57's floor probe and D58's calibration sequence; D55's policy and D56's rule stay. FORMAT's F-LIMITS-D is amended to match (F1.S2). **The quick run's task 07 cost gap stays confounded** — its agents worked against a stated 256 MB floor while 512 MiB was enforced — and is never read as a language result (T26) *(2026-11-08, epoch 10: **the value chosen and its registration are two steps.** 512 MiB is chosen now, for all six tasks. It is registered only when all twelve references (six tasks, two stacks) pass every test under it, as FORMAT's amended F-LIMITS-D says; "registered" above reads "chosen" until then. `spikes/lang-explore/inputs/memory-limit.json`'s note, which says "registered", is fixed in F2.S1. After the judge: "registered" is a field of that file, written by F2.S1's tool with the date and the twelve references' identities; F3.S1's guard refuses a series without it.)* | the team lead, 2026-11-07, from consult `0a205591` and the quick run's streams |
<!-- /quote -->

<!-- quote: D73 (parent the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3) -->
> | D73 | **Five pairs per task, and the claim's limit.** 5 pairs per task, 30 pairs, 60 data sessions. From the quick run's within-stack spread (task 07 Node $3.02 to $6.24, task 05 Node $1.37 to $4.33: two to three times), 3 pairs read a per-task cost ratio only above about two to three times; 5 pairs separate per-task medians at about 1.5 times and read the pooled direction over 30 pairs. Neither shows a 20 % difference, and the pre-registration says so as a limit on the claim. Spend is no limit; the machine time of ten passes is measured in F1.S2 before the freeze | the team lead, 2026-11-07 |
<!-- /quote -->

<!-- quote: Primary metric, exactly (D24) (parent 9) -->
> **Primary metric, exactly (D24):** for task t, stack s, arm a, over the data attempts A(t, s, a) (completed, killed, crashed —
> every session that was not void):
>
> `CPC(t, s, a) = Σ_{x ∈ A} cost(x) / |{ x ∈ A : accepted(x) }|`
>
> with cost = the headline recompute (§8). If no attempt of (t, s, a) is accepted, CPC(t, s, a) is **"no correct solution"**,
> shown with its attempts and its total spend so far in a descriptive column labelled **spend**, never as a CPC. **Second
> line, pre-registered:** `CPC_all`, the same with void attempts' cost added to the numerator — the "what the series actually
> paid" view. Beside both: success rate = accepted / |A|; tokens per model and kind; wall; `model_api`; `active − model_api`;
> in the process arm active time and the compliance score k/18. A single session's cost that a cap censored stays a bound on
> **spend** (`≥` the cap) in the session tables; that is the only `≥` in the report.
<!-- /quote -->

<!-- quote: The CPC ratio and its undefined cases (parent 9) -->
> **The CPC ratio and its undefined cases (consult `68a33be2`):** the spend of a stack with zero accepted solutions does not
> bound the cost of a correct solution that was never produced, so **no ratio is derived from it**. For task t and arm a,
> the C#/TS CPC ratio is **defined** only when both stacks have ≥ 1 accepted attempt. When either has none, the ratio is
> **undefined** and reported separately as an outcome: which stack had no correct solution, its attempts, its spend. The
> cases, orientation C#/TS fixed:
>
> | Case | Reported as |
> |---|---|
> | both stacks ≥ 1 accepted | the ratio `CPC(C#) ÷ CPC(TS)`, a value |
> | TS 0 accepted, C# ≥ 1 | `undefined (TS: no correct solution, k attempts, spend $x)`; CPC(C#) shown beside it |
> | C# 0 accepted, TS ≥ 1 | `undefined (C#: no correct solution, k attempts, spend $x)`; CPC(TS) shown beside it |
> | both 0 accepted | `undefined (neither: C# spend $x, TS spend $y)` |
> | a stack with no attempt at all (a round that did not run — a series stop, never a ceiling since D7) | `not run`; the task is undefined for that arm and says so; a spend of $0 is "no attempt", never a zero cost |
<!-- /quote -->

<!-- quote: Outcome classes (parent 9) -->
> **Outcome classes:** v1's twelve rows (`stop: alias drift`, `stop: security`, `stop: auth`, `void: contaminated`,
> `void: harness`, `void: harness (interrupted | disposed)`, `killed: cap`, `void: rate limit`, `void: upstream`, `killed: wall`,
> `killed: stall`, `completed`, `crashed`), same precedence; the process arm adds v1's `void: no reviewer`. A `killed:` or
> `crashed:` session is a **data attempt**: its cost is in CPC's numerator (censored at the cap, `≥`), its quality is what it
> left. A void is **kept** in the records and re-run once (§4.4); **every attempt appears in the report** (D26).
<!-- /quote -->

<!-- quote: Threat T3: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T3 | n = 5 pairs per task (D73) | *"Five pairs per task separate per-task cost medians only at about 1.5 times; over 30 pairs the pooled direction is read; a 20 % difference is not distinguished."* |
<!-- /quote -->

<!-- quote: Threat T5: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T5 | one memory value for all six tasks, stated to the agent (D72); the deadlines are the frozen spec's; both references pass ten times under both CPU profiles at the limit | *"Both references passed every case ten times, under both CPU profiles, at the stated 512 MiB limit."* |
<!-- /quote -->

<!-- quote: Threat T6, T9: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T6, T9 | one engine; ten passes on two fixed CPU profiles, the same for both stacks; environment failures unscored and counted per stack (D71) | *"Every session was scored ten times on two CPU profiles; environment failures are counted per stack and never scored."* |
<!-- /quote -->

<!-- quote: Threat T23: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T23 | Stage 1 counts only frozen-file changes as `deviations` (count and paths); project edits and suppression markers are not counted (D74) | *"A session that goes around the type check by a project edit or a suppression can be accepted; Stage 1 does not count such edits."* |
<!-- /quote -->

<!-- quote: Threat T25: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T25 | **A race rarer than ten passes can catch.** Ten passes under two profiles are a budget, not a proof (D71) | *"Ten scoring passes under two CPU profiles catch a race that shows often on the scoring CPUs; a rarer one can pass all ten and be accepted."* |
<!-- /quote -->

<!-- quote: Threat T26: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T26 | **The quick run's memory confound.** Its task 07 agents worked against a stated floor that differed from the enforced limit (D72) | *"The quick run's task 07 cost gap partly measures a memory statement that differed from the enforced limit; it is never read as a language result."* |
<!-- /quote -->

<!-- quote: Threat T27: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T27 | **The claim covers six tasks of two kinds.** Two known, four original; the olympiad tasks are exploratory controls outside it | *"The result holds for these six daemon and web tasks only; the olympiad results are exploratory controls."* |
<!-- /quote -->

<!-- quote: Threat T28: the Stage 1 reading and the report's sentence (parent the operative part of epoch 9, threats this change touches) -->
> | T28 | **The plan was written after an exploratory run of the same tasks.** The quick run's results were seen before the specs' fixes, the pre-registration and `prediction.md`; it is never data and never pooled, and `prediction.md` says when it was written | *"The pre-registration was written after an exploratory run of the same six tasks; that run is not data."* |
<!-- /quote -->

<!-- OPERATIONALIZATIONS: everything below this line is the pre-registration's own text; extract-contract.mjs never rewrites it -->

## The pre-registration's own operationalisations

These sentences state how the quoted parts above are applied to Stage 1. None of them changes what the parent fixes. They
are written before the pre-registration tag and before the first paid session; the gate reads them as they stand. Where
they and the two rule files beside this contract (`pilot-rules.json`, `adjudication-rules.json`) speak of one rule, both
say the same, and the rule files hold exactly the GO rows and the adjudication rules marked below.

"The cost checks" means the cost cross-checks T1 to T5 of the parent's section 8. "Threat T3" and the like mean the rows of
*Threats this change touches* quoted above. The two families are never one word.

### 1. The primary metric per task

- Per task t and stack s, CPC is computed over the **data attempts** of (t, s), as quoted (D24): the cost of every data
  attempt in the numerator, the number of accepted attempts in the denominator. The cost of a session is the headline
  recompute of the parent's section 8, priced from the registered price file of this folder.
- When no data attempt of (t, s) is accepted, CPC is **"no correct solution"**, shown with the number of attempts and the
  total spend, labelled spend and never CPC. It is never dropped.
- The C# over TypeScript ratio of a task is **defined only when both stacks have at least one accepted attempt**. The other
  outcomes are the five cases of the quoted table, word for word, and an undefined ratio is never a bound.
- `CPC_all` is the second line: the same, with the cost of the void attempts of (t, s) added to the numerator.

### 2. The overall reading and the reading per kind, for every measure

- **The measures** are CPC, `CPC_all`, the success rate (accepted over data attempts), tokens, wall, `model_api` and
  `active − model_api`. Every figure below is given for **every measure**, not for CPC alone.
- **Each measure's per-task value for one stack, and which attempts enter it.** The parent asks for the C# over TypeScript
  ratio "of CPC and of every secondary measure where defined" (§9) and does not say more; the rest is this contract's:
  - CPC: section 1, over the data attempts.
  - `CPC_all`: the cost of every data attempt and every void attempt of (t, s) over the number of accepted data attempts.
    It is undefined in the same five cases as CPC, read over the accepted data attempts.
  - The success rate: the accepted data attempts over the data attempts. A rate of 0 is a known value.
  - Tokens, wall, `model_api` and `active − model_api`: the **median** of the measure over the stack's **data attempts**
    of the task. Tokens are taken **per model and kind** (input, output, cache read and cache write, each per model, as the
    record holds them), and each of these is a measure of its own under every rule of this section; no sum over models
    or kinds is a measure. Wall is the record's registered wall; `model_api` and `active − model_api` are the record's
    time partition.
  - A void attempt enters only `CPC_all`. A pilot session enters no measure.
- **Missing, zero and undefined.** An attempt whose value of a measure is not known (`{ known: false }`) is **left out of
  that measure** and counted: a "missing" count is printed beside the value, and an unknown is never read as a zero. The
  C# over TypeScript ratio of a measure is **undefined** when either stack has no data attempt with a known value of it,
  or when the TypeScript value is 0; for CPC and `CPC_all`, the cases of section 1 hold. An undefined ratio stands in the
  line of undefined outcomes, as for CPC, and is never a bound.
- **Over the six tasks:** the median of the per-task C# over TypeScript ratios over the defined ones, the geometric mean of
  the same ratios, and the count of tasks where C# is higher. Each states its denominator in the form "median over k of 6
  tasks; m undefined", and the line of undefined outcomes stands beside it.
- **Per kind:** the same three figures for the three daemon tasks (05, 07, 10) and for the three web tasks (04, 06, 08), each
  with its own denominator ("median over k of 3", "C# higher in j of k").
- **Known and original tasks:** the known tasks (05, 06) and the original tasks (04, 07, 08, 10) are shown apart as a
  listing, never as a summary line (D29).
- **No sign test is shown** as a secondary line, and no p-value, interval or effect size is a finding. None is registered,
  so none is reported.

### 3. The outcome classes and the voids

- `rejected` and `intermittently incorrect` are data attempts. Their cost goes into CPC's numerator and nothing into its
  denominator.
- `unscored: environment` is a **void**. Its cost is on the void line, in neither part of the primary CPC, and it is re-run
  once.
- A killed or a crashed session is a data attempt. Where the cap ended it, its cost is censored at the cap (`≥`).
- **The void grounds** are v1's void classes of the quoted outcome classes and `unscored: environment`. A void ground
  applies **only through the harness's own classification at scoring, or through a dated `refuted` read-back line**
  (section 8), both before the adjudication. A step-2 `harness` class of the blind adjudication (section 9) is **no void
  ground**: it never reclassifies a collected session, never causes a re-run and never moves spend.
- **The re-run rule:** a void session is re-run **once**, **alone**, **before the next slot** runs. A second void of the same
  cell ends that cell as `missing`.
- **The stop grounds** are v1's `stop:` classes, a breach of the cost check T1 under its policy, a second contaminated void
  and alias drift. A stop ends the series' launches for good; the record names the slots that did not run.

### 4. The scoring and the masks

- Every session is scored by **ten passes**: the odd passes on the 2-CPU profile (CPUs `6-7`) and the even passes on the
  8-CPU profile (CPUs `0-7`), the same for both stacks (D71).
- The two sessions of a pair run at the same time on the two session masks, A = `0-5` and B = `8-13`. The mask of each
  (round, task) cell is the table of `schedule.json`. Each mask holds 15 sessions per stack, and each task's stack has
  one mask three times and the other twice over the five rounds.
- **No scoring runs while a session runs.** A pair is scored only after both its sessions ended.
- **Every mask is read back:** the launch's affinity before the start, the mask observed while the session runs, and each
  pass's affinity before the pass.

### 5. What may be amended, and how

- After the A/A, by a dated line of `amendments.md`, for both stacks alike, these may change:
  - the **instrument**: the harness identity, the quiet thresholds (`quiet.json`) and the package flags
    (`package-flags.json`);
  - the policy of the cost checks **T3 and T5**, in the policy's free fields `t3`, `t5`, `decidedOn` and `reason`, each
    change by its dated `input:crosscheck-policy.json:<12 hex>` line;
  - **once**, the wall and the stall, by one `caps` line.
- **The prices have an earlier deadline.** On the series' first day the pricing page is read again, as a check only. A
  changed price is recorded as a dated amendment line **before the first paid launch, which is the A/A's**. The
  registered figures of the price file never change.
- **Never** amended: the cap in dollars, a prompt, a spec, a limit, or the treatment of one stack.
- **Nothing of the instrument changes after the first data session** has been planned.

### 6. The spend lines

The spend is shown on **five lines**: pilot, A/A, data, void and re-run. The pilot line is the charges of the four
sessions of the two pilot pairs; the A/A line those of the two twins. Of the data sessions, the **data** and **void**
lines are the charges of attempt 1, and the **re-run** line is the charges of every session with attempt 2, whatever its
class. The re-run line is derived from the attempt number and is no ledger kind of its own.

### 7. The pilot's GO table

The pilot is six sessions: the **A/A**, two identical bare sessions of task 05 on Node (the twins), and **two pilot
pairs**, task 10 (daemon) and task 04 (web), each in both stacks. All six are void spend and never data (D16). A pilot
session that ends as a void is re-run once under the schedule's rule. A row asked of a session reads that session's last
attempt. The rows marked as a stop (G-2, G-8, G-12) are asked of every attempt, re-runs included.

**The row words** are v1's: `holds`, `refuted`, `explained`, `recorded`, `not exercised`. **`explained`** is one outcome of
its own: a check that would be refuted, turned by a dated line that names it (v1's `withExplain`). Only G-5, G-6 and a
refused host of G-8 may be `explained`, and `explained` is not `refuted`.

**A row passes** when its outcome is `holds`, `explained` (G-5, G-6, G-8 only) or `recorded` (G-9 only), or when it is
`not exercised`, the row below allows that, and its replacing evidence is named (v1's `passes` of
`spikes/minigit-bench/lib/aa-verdicts.mjs`). **GO** means every row passes. **NO GO** means a row does not pass: any
`refuted` row, a by-hand row (G-10, G-13, G-16) without its dated line, or a row `not exercised` without allowed and
named replacing evidence. **A STOP** is G-2, G-8 or G-12 refuted, or G-7 refuted by alias drift; it outranks NO GO. The
tool prints the GO line for the team lead to write and writes no line itself.

Where a row reads a stored file, it reads what the frozen harness wrote in the session's folder (`s/<rid>/`), and
`pilot-rules.json` names each file, field and v1 helper of the row exactly. Every helper it names is a file of the Stage 1
harness, so the tag freezes its blob through the manifest's `harness` identity; a fixture or a registry file is read at the
tag's commit. Missing evidence never passes: a computed row whose evidence is missing is not exercised for that session,
which is NO GO unless the row says otherwise. Two definitions the rows share: **the sampler's samples** of a session are
the lines of `<series>/pairs/p<pair>-cpu.ndjson` (lang-explore's sampler; a pilot session's pair is 0) whose time falls
in the session's window (`meta.json` `startedAt` to `exitedAt`) — v1's own fact loader reads another file name, which a
Stage 1 series does not write; **a session's longest quiet gap** is the largest difference between consecutive time
stamps `t` of its `stream.stamped.ndjson`, the gap from `startedAt` to the first line included.

<!-- go-row: G-1 -->
- **G-1, the read-back** (all six; computed). The session's `readback.json` says `ok` on every init field. Missing: NO GO.

<!-- go-row: G-2 -->
- **G-2, the cost check T1** (all six, every attempt; computed). The record's T1 status passes. Refuted: STOP.

<!-- go-row: G-3 -->
- **G-3, the cost check T2 per model and kind** (all six; computed). The record's T2 status passes.

<!-- go-row: G-4 -->
- **G-4, the cost checks T3 and T4 over matched sources** (the two twins; computed). The record's T3 and T4 statuses, which
  v1's `crosscheck.mjs` computes at collection from the session's OpenTelemetry sink (`otel/otel-raw.ndjson`) and its
  transcripts (`transcripts/`): an O record is matched when a transcript message carries its request id (the policy's
  `join`), T3 compares T at the CLI's rates with the matched O cost within 2 %, T4 compares the tokens within 0.1 %. The
  rule is v1's `coverage`, `t3Rule` and `g4`: main coverage holds when both twins pass T3 and T4; the subagent half holds
  on the twins when a twin also launched a subagent (`time.children`). `t3: stop` needs main coverage and the subagent half
  on the twins or in a committed smoke fixture whose `meta.json` `cliVersion` is the **pinned CLI version**; otherwise
  `fallback-t2`. The subagent half may be `not exercised` only with that fixture's B-T1 as its replacing evidence. At the
  tag no fixture of the pinned version exists, so if no twin launches a subagent the row is not exercised without
  replacing evidence (NO GO) and the T3 rule gives `fallback-t2`. The policy that follows is written by a dated
  `input:crosscheck-policy.json` line.

<!-- go-row: G-5 -->
- **G-5, the cost check T5** (all six; computed). T5 passes, and the broker's total equals the envelope's within
  $0.000001. May be `explained` by a dated line.

<!-- go-row: G-6 -->
- **G-6, the time partition** (all six; computed). The categories sum to the registered wall to the millisecond, and
  `unattributed` is under 5 % of the wall. May be `explained` by a dated line that names the cause and its evidence.

<!-- go-row: G-7 -->
- **G-7, the record kinds and the aliases** (the two twins; computed). Both twins carry every record kind of v1's `kindsOf`
  (stream, envelope, OpenTelemetry sink, transcripts, audit log, broker log, egress with bytes, sampler samples, spin,
  packages, a score with every step known, a ledger charge), each derived from the file `pilot-rules.json` names. The
  aliases a twin used are its record's `cost.aliases`; the series' alias baseline is `<series>/aliases.json`, written by
  v1's `aliasStore` from the series' first session. Every alias used must be in the baseline and resolve to its id.
  **Alias drift** is a used alias that resolves to another id than the baseline's (or an alias check that says stop): a
  STOP. Any other refutation (a missing kind, a missing alias record, an alias the baseline lacks) is NO GO.

<!-- go-row: G-8 -->
- **G-8, isolation** (all six, every attempt; computed). The allowed CONNECTs are within the bare host list, the refused
  ones within the known-refused list or named by a dated line, the real token is counted zero times, and there is no high
  contamination, canary or token hit. Refuted: STOP.

<!-- go-row: G-9 -->
- **G-9, the twin spread** (the two twins; recorded). The ratios of cost, wall, `model_api` and `active − model_api`, the
  differences of the held-out results and the CPU per mask. It is recorded, shown beside every ratio as a reference
  (n = 1, not a threshold), and **never gates**.

<!-- go-row: G-10 -->
- **G-10, the scorer by hand** (one twin and each of the four sessions of the two pilot pairs; by hand). In a clean copy of
  the session's tree, through the same frozen build the scorer uses, the build and the visible samples run by hand give
  the scorer's counts, and the hand also reads the session's `deviations`. One dated line per session. Missing: NO GO.

<!-- go-row: G-11 -->
- **G-11, the contention control** (all six; computed). The quiet check passed, the mask was observed, the spin record is
  there, and the sampler's samples (defined above) are at least two, never further apart than the interval of
  `spikes/minigit-bench/inputs/quiet.json` plus one second, each with its spin figure.

<!-- go-row: G-12 -->
- **G-12, the cap** (all six, every attempt; computed). v1's meaning: the cap stops a session that reaches it. An attempt
  reached the cap when its stop-rule figure (`cost.stopRuleUsd`) is at least the registered **$50** or its class is
  `killed: cap`. **The row is evaluated on the attempts that reached the cap.** An attempt below the cap gives no evidence
  for G-12: it is neither a pass nor a refutation. With at least one attempt at the cap, the row holds when every such
  attempt is `killed: cap` at a settled spend (`cost.censoredAtUsd`) of at least $50, and is refuted otherwise; a dated
  G-12 `refuted` line also refutes it. **An attempt whose stop-rule figure or class cannot be read** leaves the tool
  unable to tell whether the cap was reached: the row is NO GO, naming the attempt, and no replacing evidence is allowed.
  **Only when every attempt's figure and class were read and none reached the cap** is the row `not exercised`, and its
  replacing evidence is a dated G-12 `holds` line whose evidence names v1's broker proof P-6 and the budget tests green at
  the series' harness identity (its 12 hex). Refuted: STOP.

<!-- go-row: G-13 -->
- **G-13, each class by hand** (all six; by hand). Each session's class is read against its stream and the broker's log.
  One dated line per session. Missing: NO GO.

<!-- go-row: G-14 -->
- **G-14, the free rehearsal** (the free rehearsal series of step T; computed). It is read as the free rehearsal of one
  daemon and one web task passing in both stacks on the series' own harness. In the rehearsal series of step T (an `r-`
  series of stage 1 with no tag), for task 10 and for task 04 in round 1, each stack's last attempt has the run-map status
  `scored`, its `record.json` lacks no path of `missingPaths(record, true, true)` (`spikes/lang-explore/lib/records.mjs`),
  and its `run.harnessIdentity` is the manifest's `harness`. The stub programs' verdict is not read. Missing: NO GO.

<!-- go-row: G-16 -->
- **G-16, the drivers by hand** (one session of each pilot task; by hand). Three dated lines, one per driver, each on a
  real pilot tree of the task that exercises it. The task of the line's rid names the driver: task 05 the TCP driver, task
  10 the daemon checks (the program's `memory.peak` read by hand agrees with the driver's record; SIGTERM by hand ends the
  process as the driver says; the timer scenarios give the driver's final report), and task 04 the HTTP driver (one
  scenario of each kind, a concurrency scenario among them, and a captured dynamic value reused as `FORMAT.md` defines).
  The culture check passes inside both boxes, and the hand reads the session's `deviations`. Which scenarios the hand runs
  is the private aid's (D41). **The clause on the RSS curve lapsed with D74** and is no part of the row. A missing line
  is named: NO GO.

<!-- go-row: G-17 -->
- **G-17, the registry and the frozen record** (no session; computed; read at the tag's commit). Three checks, each passing
  as follows. **(1) The twelve references:** `memory-limit.json` `registration` has a real date and exactly twelve
  references, one per task and stack, each `treeSha256` equal to `packages.json` `refsTreeSha256` of its task and stack;
  `memory-limit.json` `bytes` is 536,870,912 and every `packages.json` `memoryBytes` equals it; every `referenceVerdicts`
  is `accepted`. **(2) The witnesses:** a witness is caught when the package tool ran it and the suite failed it; for each
  task, the private package record the registry row was written from (`package-NN/<run>/record.json`) has
  `items.witnesses.ok` true, `caught` equal to `witnesses` and above zero, `witnesses` equal to `packages.json`
  `counts.witnesses`, and `notCaught` empty. **(3) The frozen specs:** `spec-states.json` `freeze.state` is `accepted`
  and `freezeProblems` over the record is empty; every `specs.NN.state` is `frozen`; and the freeze's own check
  `gatedProblems` (`spikes/lang-bench/lib/freeze-pins.mjs`) is empty for every task: the spec's bytes at its frozen commit,
  with the Freeze tag row set to the placeholder, hash to `packages.json` `specSha256`.

<!-- go-row: G-18 -->
- **G-18, the cross-check, the stall and the cap** (all six; computed). The cross-check figure of a kind is **twice the
  larger settled cost of the pilot pair of that kind** (task 10 for the daemons, task 04 for the web tasks). It is shown
  beside the fixed $50 cap and **binds nothing**; a settled cost is the record's `cost.stopRuleUsd`. The stall in force
  (the registered one, or the one dated `caps` line's) is larger than the longest quiet gap (defined above) of any pilot
  session. Every pilot session that reached $50 is `killed: cap`.

### 8. The read-back of rounds 1 and 2

- **Which pairs:** in round 1 and in round 2, one slot of a daemon task and one of a web task, named from the seed by the
  schedule before any result exists (`schedule.json`, `readback`). Both sessions of each such slot are read back.
- **The fields, the stored value and the registered value it is compared with** (`pilot-rules.json` `readback` names the
  same files and fields; any extra, absent or different entry is a difference):
  - **the agent box's argv** (`argv.json` `claude`, with `stage-facts.json` `agent.argv` holding its sha256): the file at
    the first argv entry hashes to `explore.json` `cli.sha256` (the manifest's `cliSha256`) and its path is the home
    joined with `cli.pathParts`; the value after `--model` is `cli.model`; the value after `--max-budget-usd` is
    `caps.capUsd`; the value after `--tools` is v1's `BARE_TOOLS` joined by commas. The effort is no argv entry: the
    session's `harness/settings.json` `effortLevel` and `modelSettings.<model>.effortLevel` are `cli.effort`, and that
    file's sha256 is `argv.json` `settingsSha256`;
  - **the binds** (`stage-facts.json` `agent.binds`, the `{ host, box }` pairs of the bwrap argv): every pair outside the
    session's own folder `<series>/s/<rid>/` binds a folder to itself and is the pinned CLI copy or a pinned toolchain
    folder (`spikes/lang-bench/inputs/stacks-lang.json` `storeRoot` and `directories`); and, with each session's own
    folder written as one placeholder, the two sessions of the pair have the same set of pairs. A difference in that
    second check refutes both sessions;
  - **the cache snapshots' hashes** (`stage-facts.json` `cache.csharp` and `cache.node` `treeSha256`): the `treeSha256` of
    `spikes/lang-bench/inputs/cache-snapshots-lang.json`, equal to the manifest's `snapshot.csharp` and `snapshot.node`;
  - **the package list** (`stage-facts.json` `cache.packages`): the C# then the Node `packages` of that registry, entry for
    entry;
  - **the agent box's environment keys** (`stage-facts.json` `agent.envKeys` and `agent.culture`): as a set, the keys of
    v1's `agentEnv` (`spikes/minigit-bench/lib/sandbox.mjs`) for the session's own `plan.json`; `LANG` and `LC_ALL` are
    `explore.json` `locale`;
  - **the scoring box's keys, with the two heap keys and their values, of every completed pass** (each
    `score/passes/pNN-<profile>-aK.json` of kind `completed`, field `box`): the keys are `allowFor(stack)` and the heap keys
    and values are `heapKeys(stack, memory-limit.json bytes)` (both of `spikes/lang-bench/lib/exec-layer/env.mjs`); `LANG`
    and `LC_ALL` are `explore.json` `locale`. A difference in any one completed pass makes the field `refuted`. The
    retries of a pass that did not complete are not read. A session with no completed pass shows this field as "no
    completed pass, not compared"; that alone is **not** `refuted`, and the other fields are still compared;
  - **the effective CPU affinity**: `stage-facts.json` `launch.affinity` `mask` is the cell's mask for the stack in
    `schedule.json` `masks`, and `observed` (the mask observed in flight) is true; the launch's read-back file
    `<series>/readback/p<slot>-<utc>.json` shows that mask observed as itself; each completed pass's `observedCpus` is
    its profile's CPUs in `schedule.json` `scoring.profiles`;
  - **the spec blob**: `stage-facts.json` `workspace.statementSha256` is the statement's hash in `prompts-lang.json` (the
    hash list at W); the frozen spec's sha256 the session's task slice holds (`<series>/inputs.json`
    `tasks.<NN>.taskPins.spec.sha256`) is `spec-states.json` `specs.<NN>.sha256`, the manifest's `spec.<NN>`.
- **A difference is `refuted`.** A field that is not stored is a refusal of the tool, never a blank.
- An Opus judge writes one dated `G-1` line per session (`holds` or `refuted`). A `refuted` line makes the session
  `void: harness`; its one re-run is the schedule's next step and is read back the same way.
- **No later slot runs** before the lines of a read-back slot are on `main` and on the series' branch.

### 9. The blind adjudication

**Said plainly (section 3, condition 5): the adjudication's code is written after the data collection ends; its complete
rules were registered before the first paid session, and they are the rules below.**

<!-- adjudication-rule: packets-sessions -->
- **The sessions.** The packets come from the data attempt of each cell (one slot's one stack) of the 30 slots. A re-run
  happens only after a void, so at most one attempt of a cell is data. Every sequence of a cell's attempts:
  - [data]: that attempt gives the packets;
  - [void, data]: attempt 2 gives them;
  - [void, void]: no packet; the cell has no data attempt;
  - a data attempt that a dated `refuted` read-back line made void, then its re-run: the re-run when it is data, otherwise
    no packet;
  - a data attempt whose build failed: no packet (below).

  A pilot session never gives a packet.

<!-- adjudication-rule: packets-cases -->
- **The cases.** Every visible or held-out case that failed in at least one **completed** pass gives one packet. An
  environment attempt of a pass is no pass.

<!-- adjudication-rule: packets-evidence -->
- **The evidence and the pattern.** A packet shows the case, the sentences of the requirement ids the case cites (from the
  frozen spec's text), the evidence of the **first failing completed pass** by its number, as the runner recorded it, and
  the pattern: in how many completed passes of each profile the case failed.

<!-- adjudication-rule: packets-build-failed -->
- **A failed build** gives no packet. It is counted apart.

<!-- adjudication-rule: masks-order -->
- **The masks**, in this order: v1's mask rules (`spikes/minigit-bench/lib/adjudicate-mask.mjs`, held to the blob id that
  `adjudication-rules.json` names), then the added rules that file lists, in its order: the two frameworks' log shapes,
  the runtimes' own error messages, the runtimes' and frameworks' names, the heap keys, the abort shapes, the compiler
  error codes (TypeScript's of four and five digits, C#'s of four, anywhere in the text) and the file suffixes and stack
  file names. The tool adds no mask of its own.

<!-- adjudication-rule: memory-cause -->
- **A memory cause.** Before the reveal a `memory limit` packet shows its cause as `kernel kill` or `runtime abort`, with the
  flags `abort-below-evidence` and `child-abort-line`; the runtime's own name is shown only after the reveal.

<!-- adjudication-rule: step-one -->
- **Step 1.** An Opus judge that is given the packets folder and nothing else answers `spec-ambiguity` or `not-ambiguous`,
  with a reason, for every packet. The reveal is refused until every packet has its answer, and the reveal freezes the
  answers of step 1.

<!-- adjudication-rule: step-two -->
- **Step 2**, after the reveal. An Opus judge answers `product` or `harness`, with a reason, for every packet. An answer
  is final when it is written: the first answer of a packet stands, and the tool refuses a second answer for the same
  blind id, so every answer written stays in the record as written. There is no correction path, because a correction
  would need a public line that names a blind id, and the public text holds none (D41).

<!-- adjudication-rule: exclusion -->
- **The exclusion.** A case judged `spec-ambiguity` in any session is dropped from every session of its task, and from no
  session of another task.

<!-- adjudication-rule: revised-verdict -->
- **The revised verdict.** A session's revised verdict is the verdict rule over its build record and its passes without
  the dropped cases. The rule is `verdictOf` of `spikes/lang-explore/lib/verdict.mjs` (F1's ten-pass rule, D71), a file of
  the Stage 1 harness, frozen by the tag through the harness identity. In words: **accepted** when the build passed, all
  ten passes completed and every remaining case passed in all ten; **rejected** when the build failed, or when a
  completed pass holds a failing remaining case and no completed pass is clean; **intermittently incorrect** when a
  completed pass holds a failing remaining case and another completed pass is clean; **unscored: environment** when no
  completed pass holds a failing remaining case and some pass has no completed attempt after its three attempts. So:
  - a pass whose failing cases are all dropped counts as **clean** in the revised reading;
  - a session whose only failing cases are dropped is **accepted** in the revised reading;
  - a session whose build failed keeps its primary verdict;
  - if every case of a task's suite were dropped, that task's revised verdict and revised CPC are **"not defined"** and are
    shown as such.

  The revised reading stands beside the primary one and never replaces it. **The primary reading never changes.**

<!-- adjudication-rule: harness-class -->
- **A `harness` class** (step 2) is **listed, never removed** (listed in the private adjudication folder, below): it changes neither the revised verdict nor CPC; only the
  exclusions of step 1 recompute the revised reading. It is an annotation of the revised reading only: it **never
  reclassifies a collected session, never causes a re-run and never moves spend**, and it is no void ground (section 3).
  **The public report gives counts only**: per task and per class, the number of `harness` cases and how many of them
  were pivotal (the session's only failing case). The list that ties each `harness` case to its session and its evidence
  stays in the private adjudication folder (`<series>/adjudication/`), never in the report (D41; D49: counts and rates
  only, no per-case adjudication narrative). A corrected score exists only after an instrument fix and a re-score,
  recorded as a separate dated amendment beside the primary and the revised results.

### 10. The disk, the memory causes, deviations and flagged pairs

- **The disk reserve** is recomputed before every paid launch over what is still to run, a void's re-run and a repeat pair of
  the pilot included, and the disk rule is checked before every slot. **What a recovery may free** is one closed list: the
  three cache folders `box/nuget`, `box/npm` and `box/home/.dotnet` of a final session of a finished slot, and only after
  the archive holding them was verified. They are restored before the final archive and the record's hash list. Nothing
  else of the series is ever removed.
- **The memory causes** are counted per task and stack: the kernel kill and each runtime's own abort, with the flags
  `abort-below-evidence` and `child-abort-line` and the 50 % evidence threshold of D56.
- **`deviations`** (the frozen files the restore had to put back, as a count and the paths) stands beside `accepted` and
  never decides it.
- **Every line is shown twice:** with and without the pairs that hold a `ran unpaired` session or a `foreign load` flag.
