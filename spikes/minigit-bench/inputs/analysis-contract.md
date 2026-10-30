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

## Bare operationalizations, decided before any cell

Written on 2026-10-30 by Opus for E3b.S3 (`todo/PLAN_minigit_stack_bench_E3_bare.md`), before any paid minigit session of the task. This half says how the bare arm applies the quoted text above.
Where it adds to parent §11.1, the addition is marked **registered extension of §11.1** and carries its reason. After the tag `prereg/minigit-bare-<yyyymmdd>` nothing here changes except
through a dated line of `inputs/amendments.md`, and after the A/A only the instrument may change (parent §11; section 10 below).

### 1. Cell, pair and phase

- The **cell** is one stack in one pair k (k = 1, 2, 3): its v1 session and the v2 session that starts from that v1 tree. For **cost and time**, the value of a cell is v1 + v2.
  A cell whose v1 is `missing` (section 4) has no v2 and no value; it is reported as missing, with the reason.
- **Quality is per phase**, never summed over v1 and v2: prediction 5's medians are per phase.
- A **pair** is the C# cell and the TypeScript cell of one k. Their v1 sessions start in one tick on the CPU masks 0-5 and 8-13 (the masks swap each pair; the seed decides pair 1);
  their v2 sessions start only after both v1 sessions are collected and scored (the **barrier**). A void session is not scored, so the barrier waits only for data sessions.
- **The v2 workspace** is a copy of the v1 session's tree plus `SPEC-v2.txt` and the fixed `test-v2.sh`, leaving out, at any depth, per stack: C#: `bin/ obj/ node_modules/`; TypeScript: `node_modules/` only. The
  asymmetry is deliberate: a C# `bin/` and `obj/` are build output that the offline build regenerates, while a Node project's `bin/` conventionally holds source (the executable entry script), so leaving it out could delete the
  agent's own code. The list is the scorer's own (`excludedDirs`), so the workspace and the scorer agree. The cost, stated: a TypeScript tree that holds generated output in `bin/` or `obj/` keeps it into v2. Every v2
  session's record lists the directories that were left out. A v1 input the agent changed is kept and flagged `v1 inputs changed`.
- Only pairs 1–3 are data. The A/A and the Node pilot are instrument checks (section 10); they never enter a median, a range or a ratio. Their spend counts in the bare budget.

### 2. How a session ended: the outcome classes

`lib/bare-outcome.mjs` classifies each session by how it ENDED. The first row that matches wins. **The final request is the last one to SETTLE** (by the time of its broker line, a tie by `seq`): a concurrent request that opened later can settle earlier. The exit code never decides a class, and a scorer fault never voids a session.
"Data" says whether the session is a data point. A row marked † is a registered extension of §11.1 (section 3).

| # | Evidence | Class | Data |
|---|---|---|---|
| 1 | `checkAliases` says stop: an alias the session used resolved to another id than the series baseline (D12) | `stop: alias drift` | no |
| 2 | the real token counted by a sampler or by the session scan | `stop: security` | no |
| 3 | the final request was answered 401 or 403 BY THE UPSTREAM (a `settled` line of the broker log; the broker's own 403 for a refused path or body is never this row) | `stop: auth` † | no |
| 4 | a high-severity contamination hit, a canary found, or a token-shape hit | `void: contaminated` | no |
| 5 | `meta.stopped = 'readback'`, or no broker `settled` line with a 2xx status and parsed usage (the model never answered) | `void: harness` | no |
| 6 | `meta.stopped = 'cancelled'` (the abort reason is recorded), or `'disposed'` (a box ended with no recorded reason), or the session's state file retired by `retireStale` | `void: harness (interrupted \| disposed)` † | no |
| 6b | the session ended in an error or without an envelope, and its final request was a broker refusal of its own, `reason` `path`, `body` or `busy` (or no reason): the broker answers those 403 / 403 / 503, which are not the credential and not the provider | `void: harness` † (the reason names the refusal) | no |
| 7 | `meta.stopped = 'cost'` (settled spend ≥ cap), or the envelope's `subtype: 'error_max_budget_usd'` (the CLI's own cap), or the session ended in an error and its final request was a broker refusal with `reason: 'cap'` | `killed: cap` | yes, censored |
| 8 | `rateLimitSignal` says void AND the session ended in an error or without an envelope | `void: rate limit` | no |
| 9 | the session ended in an error or without an envelope, and its final request was an upstream 5xx or 529, or a transport error, after the CLI's own retries (or the envelope's `api_error_status` is 500 or more) | `void: upstream` † | no |
| 10 | `meta.stopped = 'wall'` or `'stall'` | `killed: wall` or `killed: stall` | yes, censored |
| 11 | the envelope says `subtype: 'success'` and `is_error` false | `completed` | yes |
| 12 | anything else after the model answered | `crashed: <subtype>` | yes |

- **Row 7 comes before row 8 on purpose.** The broker answers its cap refusal with HTTP 429 `rate_limit_error`. Read alone, `rateLimitSignal` would void that session and its re-run
  would pay for the same cap twice. A broker refusal without a `reason` field is `unknown` and is never a cap.
- **A watchdog kill is a data point** (§11.1: "killed on wall, cap or stall"), whatever `sessionOutcome`'s `void: stopped` label says.
- **A by-hand verdict** (a dated `refuted` line of `amendments.md`, for example the pair-1 read-back) can reclassify a session to `void: harness` and to nothing else. It is applied after
  row 4 and before row 5, and at the start of every run and resume to the sessions that already ended (a stop and a `void: contaminated` keep their class): the run-map and the session's record show the
  reclassification (what the class was, which line), the ledger gets a `reclassify` event line, the session's charge is unchanged, and the schedule plans the one re-run by its registered rule (section 4). A line names one session:
  the v2 session of a cell that ran from a refuted v1 is refuted by its own line.
- A `stop:` class halts the series (section 9). The session is kept and is not a data point.

**Flags, never classes.** A flag never changes a class; it is shown with the session and used by the "without flagged pairs" line (section 7).

| Flag | Meaning |
|---|---|
| `broker refused k` | k earlier cap refusals the session survived, with their summed backoff (from each refusal to the next admitted request) |
| `upstream 429 k` | k transient upstream 429 answers the session survived |
| `model_refusal_fallback` | the alias check saw a fallback model (§11.1: medians with and without) |
| `over cap` | settled spend above 1.1 × the cap (the broker's soft-cap overshoot) |
| `v1 inputs changed` | the v1 session changed `SPEC-v1.txt` or `test-v1.sh`; the v2 workspace keeps the edit |
| `start skew` | the two spawn stamps of a phase are more than 5 s apart |
| `foreign load` | during a phase, the 60 s mean of foreign CPU over 1.0 CPU, or a foreign agent process (section 8) |
| `harness core contended` | during a phase, the 60 s spin-probe median over `spinMaxRatio` × the calibrated idle median |
| `started not quiet` | the phase was started with `--accept-noisy <reason>` |
| `scored under load` | the check before scoring was not quiet (latency is load-sensitive) |
| package flags | the rules of `inputs/package-flags.json`; each one is read by hand in the pair-1 read-back |

### 3. The registered extensions of §11.1

§11.1 lists the voids and the data points of the series. The bare arm meets three endings §11.1 does not name. Each is registered here before any cell, with its reason; the process
arm adopts or declines them in E3.S3.

- `void: upstream` — **registered extension of §11.1.** Reason: the provider, not the stack, ended the session. The final request failed with a 5xx or 529, or the transport failed,
  after the CLI's own retries. §11.1 voids a session ended by the CLI's 429 markers for the same reason: a provider outage says nothing about C# or TypeScript, and as a data point it
  would charge one stack with an outage that happened to fall in its session. A transient 5xx or 429 that the session survived is a flag, not this class.
- `void: harness (interrupted | disposed)` — **registered extension of §11.1.** Reason: the harness or the host ended the session, not the agent. §11.1's `harness` void covers only
  a crash before the model's first turn; a session aborted by SIGINT or SIGTERM (the reason is recorded), a box the harness disposed of with no recorded reason, or a launch retired by
  `retireStale` after a crash can end after the model answered. Its partial cost, time and tree measure the interruption, not the stack. It is re-run once like every void.
- `void: harness` for the broker's own refusal (row 6b) — **registered extension of §11.1.** Reason: the harness's own door refused the last request (a path or body the broker does not allow, or its byte bound), so the harness ended the session. Its 403 is not the credential failing and its 503 is not the provider.
- `stop: auth` — **registered extension of §11.1.** Reason: an UPSTREAM 401 or 403 is the credential failing (the subscription token expired, was revoked, or lost a capability), not the stack.
  It would fail every later session in the same way, so it stops the series instead of voiding one cell, and it is never a data point. Resume needs the credential fixed and a new A/A.

These three classes, and the rule that a session is classed by how it ENDED (a transient cap refusal or upstream 429 is a flag), are parent §11.1's "Registered extensions, bare arm (D20)".

### 4. The re-run schedule

A void is re-run once, as attempt `a2` with a fresh rid. Re-runs come after the pair's own phases and before the next pair (`lib/bare-schedule.mjs`, `nextStep`).

| After the v1 phase | Next steps, in order |
|---|---|
| both data | the v2 pair |
| both void | the v1 re-run pair (paired), then the v2 phase for the cells with data |
| one void (say C#) | TS v2 alone (`ran unpaired`), then C# v1 `a2` alone (`ran unpaired`), then C# v2 alone (`ran unpaired`) |
| a void `a2` | that cell is `missing`; its v2 never runs |
| one v2 void | that v2 `a2` alone, from the recorded v1 tree (`ran unpaired`) |
| both v2 void | the v2 re-run pair |

A void spends money: its spend counts in the bare budget and the ceiling (section 9) and is shown on its own line.

### 5. The measures

**Primary cost: the headline recompute.** Parent §9's headline USD (`sessionCost`): Σ over models of the token kinds at the published prices of `inputs/prices-2026-10-29.json`, with the
tokens from the envelope's `modelUsage` and the 5m / 1h cache-write split per model from the transcripts. A model with no message-level split (the CLI's internal line) is priced at 5m
with 1h as the upper bound: an interval, never a guess. The other views stand beside it and never replace it:

- **E**, the CLI's `total_cost_usd`; **O**, the OTel `api_request` records; **T**, the transcripts; **B**, the credential broker's ledger, which prices every request the box sent,
  settled or not, from the same price table. Per model: main, children and internal; tokens per model and kind; the first-turn cache read (warmth).
- **Checks** T1–T5 run on every session under `inputs/crosscheck-policy.json`: T1 (the envelope disagrees with itself) stops the series; T2 marks the cell `otel incomplete` and keeps it;
  T3 follows the rule of section 11; T5 (B against E) is investigated.
- **The stop-rule figure** of a session is the largest of E, the headline recompute and B. A session without an envelope uses B. An interrupted session is charged its settled B plus
  every forwarded-but-unsettled request at its reservation. Every figure is a list-price API equivalent, not what the subscription is billed.

**Primary time: three measures.** From the exclusive partition of parent §10:

- **wall** — the launcher's monotonic clock, spawn to exit. Preparation, collection and scoring are outside it.
- **`model_api`** — the partition's model category (O `api_request` spans).
- **`active − model_api`** — active = `build_test + model_api + other_tool + hooks` (parent §10), so this is the work time in which no model call is active: the agent's tools and
  hooks.

**Descriptive only: `build_test` against `other_tool`**, and every other category, `unattributed`, `duration_api_ms` and agent-seconds. They are labelled "descriptive" wherever they appear.
The limit, stated with them (V16): the partition classifies one Bash call as one category, its highest, so `sleep 300 && dotnet build` counts its 300 s as `build_test` (a build anywhere in a
chain wins); the report shows the count of mixed chains per cell.

**Quality, per phase.** Build ok; the visible result per script (the fixed `test-v1.sh`; at v2 the fixed `test-v2.sh`); the held-out **`percent`** (cases passed / N, a blocked case
counted as not passed) — the primary score; **`defects`** (the number of distinct failed core cases or SPEC clauses, so one broken command counts once). Every comparison "in cases" uses
`defects`; every percentage uses `percent`. The blocked count is reported apart. The upstream-original scripts are a reference only; per-invocation latency is secondary. Held-out results
appear as counts, `percent` and `defects` only — never a case. The adjudication of parent §5.3 runs after the series; the primary score is the unrevised one.

**Acceptance** (`inputs/checklist.json`, parent §5.3 without `delivered`): build clean, visible 100 % per script, held-out primary 100 % with blocked cases counted against it. The
number of accepted sessions per stack and phase is reported. Acceptance is a label, never a filter: every cell is reported.

### 6. Censoring: bounds, not values

- A session in `killed: cap`, `killed: wall` or `killed: stall` is a data point that was cut short. Its cost is reported as `≥` its observed stop-rule figure (not as `≥` the cap), and
  its wall, `model_api` and `active − model_api` as `≥` their observed values. Its quality is scored on the tree it left. A cell is censored when either of its sessions is.
- A C#/TS ratio with the C# side censored and the TS side not is a lower bound (`≥`); with the TS side censored and the C# side not, an upper bound (`≤`); with both censored, it is neither
  a value nor a bound and is reported as "both censored".
- A median over values and bounds is given twice: with the bounds included at their observed figure and marked as bounds, and with the censored pairs excluded (parent §11).
- A per-stack median and range lists a censored cell with its `≥`.

### 7. Ratios, medians and what is reported

- **Per pair:** the C#/TS ratio of the cell values for cost (primary), wall, `model_api` and `active − model_api`, each a value or a bound (section 6). Over the pairs: the three
  ratios, their median, and "C# > TS in k of n pairs", where n is the number of pairs with both cells present. Missing cells are named.
- **Per stack:** median and range (n = 3) of every primary measure, and per phase of `percent` and `defects`.
- **Each ratio table and median is given three ways:** (a) every data cell; (b) without every cell that holds a `ran unpaired` session; (c) without **flagged pairs**. A pair is
  flagged when any of its sessions, in either phase, carries `foreign load`, `harness core contended`, `started not quiet`, `start skew`, `broker refused k`, `upstream 429 k` or
  `model_refusal_fallback`. The other flags are shown beside the session and do not remove a pair.
- **The A/A spread** (G-9) is shown beside every pair ratio as a reference at n = 1, never as a threshold.
- **Overlap of ranges = not distinguished.** No p-value, no confidence interval, no effect size as a finding (the n = 3 paragraph above).
- **The prediction beside the observation:** items 1, 2, 5 and 7 of `inputs/prediction.md`, each marked held, not held, or not decidable (with the reason).
- **banteg's numbers** are compared only as ratios, and always in the same sentence as the caveat that agent, model and date differ.

### 8. The machine-quiet rule

`inputs/quiet.json` holds the thresholds; `lib/cpu-sampler.mjs` and `lib/quiet.mjs` apply them.

- **Before every phase** (the A/A and the pilot included), a 60 s window of 5 s samples must be quiet: host CPU busy minus the harness's own ticks, mean ≤ 0.5 CPU and no sample > 1.5 CPU;
  zero foreign agent processes (a `claude -p` / `--print` or `codex exec` without this series' `BENCH_CELL`, another user's included); `MemAvailable` ≥ 25 % of `MemTotal`; no live session of any
  series; the spin-probe median ≤ 1.30 × the idle median calibrated by `init`. Not quiet: re-checked every 60 s for 30 min, then a resumable halt (exit 3) with the verdict.
  `--accept-noisy <reason>` starts anyway with the flag `started not quiet`; it is refused for a v2 phase whose v1 was quiet.
- **During a phase** load is flagged, never killed on (a kill would make a void §11.1 does not list): `foreign load` and `harness core contended` (section 2). The foreign-CPU figure is a
  lower bound: a process born and gone between two samples is missed.
- **Before scoring** the same check only flags (`scored under load`).

### 9. Money and the stop rules

`lib/spend-gate.mjs` checks both limits before every phase start (A/A, pilot, v1 pair, v2 pair, re-run). Each spend figure is the stop-rule figure of section 5.

1. **Ceiling (parent §13):** settled data + void + pilot spend + the live O sum of running sessions + 1.1 × Σ caps of the sessions about to start ≤ **$500**. The paid instrument work before E3b counts here: **$0.8263 over 8 paid instrument sessions**, the total of `inputs/spend-prior.json`
   (read from the capture and smoke meta files, not typed; the token check, $0.010, is approximate; the plan's earlier rough figure is not the registered one, and a test recomputes the total from the file).
2. **Bare budget:** bare spend (A/A, pilot, data and voids) + 1.1 × Σ caps of the sessions about to start ≤ **$40**. The prior spend does not count here.
3. **The one permitted raise of the $40:** only to finish the re-runs of a pair already started, only by the worst case of those re-runs (n × 1.1 × $6), and only as a dated line of
   `amendments.md` before the re-runs start: `YYYY-MM-DD | budget-raise | pair:<k> | n:<re-runs> | usd:<n x 1.1 x 6> | <reason>`. The runner accepts one such line, for a pair that has started,
   with exactly that amount, and applies it to the run of that pair only; it also checks at every run that the run-map's caps, bare budget and ceiling are the registered ones. Any other time the bare budget refuses a phase, the bare arm ends at the n it reached, and the report says "in k of n pairs" and names the
   missing cells.
4. A phase admitted within a limit can settle above it by at most the soft-cap overshoot, which the 1.1 factor budgets.

**Series stops** (recorded in the run-map as `stopped` with the rule `stop: alias drift`, `stop: security`, `stop: auth`, `T1 breach`, `T2 breach`, `T3 breach`, `T5 breach` or `second void: contaminated`; resume refused until the named fix and, for the first four, a new A/A): `stop: alias drift`; `stop: security`; `stop: auth`; the second
`void: contaminated` of the series; and **any check of T1, T2, T3 and T5 whose policy in `crosscheck-policy.json` is `stop` and that breaches** (T1 under `t1: stop`; T2 under `t2: stop`, the fallback of section 11; T3 under `t3: stop`, which section 11 chooses for the data cells; T5 under `t5: stop`): one rule for all of them. The session keeps its class and gets the flag `T<n> breach`; under any other policy value a breach is investigated, or for T2 under `t2: otel-incomplete` the flag `otel incomplete`. **Resumable halts** (exit 3): `void: rate limit` (the subscription's usage window), the quiet wait timing out, a scorer environment failure, and
`scorer-timeout`.

A series stop is recorded in the run-map only after every session of its phase is collected, charged and recorded; while a session of the phase is unsettled (its evidence could not be read), the run returns that failure (exit 5, resumable) and names the stop that is waiting, and the resume that settles the last session records it.

**Pairs 1–3 run unless a named stop fires.** The operator's go for a pair chooses WHEN it runs, never WHETHER. A pair is not skipped, repeated or re-ordered because of an earlier
pair's numbers.

### 10. The A/A, its GO definition, and what may change after it

Before any data pair: two identical bare C# v1 sessions at once on masks 0-5 and 8-13 (`run --what aa`); then a free v2 rehearsal on one twin's tree with a stub CLI and the real
scorer (`--what rehearse-v2`); then one bare Node v1 alone (`--what pilot-node`). `aa-verdicts` computes `AA.md` from the records and the dated verdict lines of `amendments.md`.
‡ = also required of the Node pilot.

| Id | Must show | A miss means | Then |
|---|---|---|---|
| G-1 ‡ | the read-back is ok on every init field | the launch is not the pinned treatment | fix; A/A again |
| G-2 ‡ | T1 | the envelope disagrees with itself | **stop**; fix; A/A again |
| G-3 ‡ | T2 per model and kind | O is incomplete | investigate; no data pair until T2 holds on an A/A |
| G-4 | T3 and T4 over matched sources: every main-role O record matched by a transcript message and none unmatched; every subagent-role record matched | T's coverage is not established | the T3 rule (section 11) |
| G-5 ‡ | T5: B = E within $0.000001 | the broker prices differently | resolved (fixed, or explained by a dated line) before pair 1 |
| G-6 ‡ | `partition` Σ = wall to the ms and `unattributed` < 5 % of wall — or `explained`: a dated line names the cause of the unattributed intervals with its evidence path and shows no span source is missing | the classifier or a span source is wrong | fix the reader and re-read the SAME records; re-run only if a record was missing |
| G-7 | the same record kinds for each twin (stream, envelope, O, T, audit, broker, egress with bytes, CPU and spin samples, packages, `score.json` with every step known, its ledger line); every alias either twin USED resolves to the baseline id (D12) | an instrument sees one twin and not the other; or alias drift | fix, A/A again; drift is a **stop** |
| G-8 ‡ | isolation: allowed CONNECTs ⊆ the bare host list; refused ⊆ the known-refused list or named by a dated line; the real token counted 0 times; no high contamination, canary or token hit | a hole in the box | **stop**; fix; A/A again |
| G-9 | the twin spread, recorded, never gating: cost, wall, `model_api`, `active − model_api` ratios, held-out `percent` and `defects` differences, CPU per mask | — | shown beside every bare pair ratio |
| G-10 ‡ | the scorer by hand: in a scratch copy of one twin's tree (and of the Node tree) the build and the fixed visible script run by hand give the scorer's counts — a dated line | the scorer misreads a real tree | fix the scorer, re-score (no re-run) |
| G-11 ‡ | the quiet check passed; the sampler and the spin probe wrote every 5 s; `maskObserved` true for every cell | the contention control is not in force | fix; A/A again |
| G-12 ‡ | the cap stops a session that reaches it, at settled spend ≥ $6 | the cap failed | **stop**; fix |
| G-13 ‡ | each session's class read by hand against its stream and broker log — a dated line | the classifier misreads an ending | fix, re-classify |
| G-14 | the v2 rehearsal: the v2 workspace is the v1 tree minus the stack's exclusion (C#: `bin/ obj/ node_modules/`; TypeScript: `node_modules/` only; section 1) plus the v2 inputs; the stub v2 session's records are complete; `score.json` at v2 has every step known | the v2 path is broken | fix, rehearse again (free) |

**GO** = every G `holds` (G-6 `explained` counts as holds; G-9 never gates), except these, which may be **`not exercised`**, each with its replacing evidence named in `AA.md`:
**G-4's subagent half** → the E2.S2 smoke fixture's B-T1 on the same CLI; **G-12** (no twin reached the cap) → E2.S2's broker proof P-6 and the budget tests green at the run's harness
identity. A by-hand G (G-10, G-13) without a dated line is `not exercised`, is not in that list, and so is **NO GO**. Any `refuted` is NO GO.

**Stops:** G-2, G-8 or G-12 refuted, or alias drift, stop the series until fixed and an A/A is GO. A second NO GO on the same G-id goes to the operator before a third A/A is paid for.

**After the A/A only the instrument may change** (the harness, the scorer, the counters, the read-back, the sampler, the quiet thresholds, the evidence reading of the outcome table): a
dated `amendments.md` line with its reason and G-id, committed and pushed before pair 1, named by `--accept-harness`. The runner reads only valid `instrument | harness:<12 hex>` lines for it, and refuses the acceptance once the series holds a data session.
A change to `inputs/quiet.json` or `inputs/package-flags.json` (the sampler, the quiet thresholds, the counters' flag rules) is an instrument change by the dated line
`YYYY-MM-DD | instrument | input:<file>:<12 hex of the sha256 of the new content> | <reason, with its G-id>` (one file and one content per line). `init` pins the sha256 of both files in the run-map; a run or a resume accepts a
changed one only through such a line and only while the series holds **no data session** (the A/A, the pilot and the rehearsal are not data sessions): after pair 1 starts the line is refused and a new series is needed. What was accepted is written to the run-map (`inputsAccepted`)
and to the ledger (an `accept-input` line), and each session's record names the hashes of the files that applied (`run.inputs`), so a record says which thresholds it was run under. **Frozen:** the prompts, cap, wall, model, effort, tools and
allow-list, toolchain pins, cache snapshots, build-server setting, egress host list and workspace contents. A change to any of them applies to both stacks and needs another bare pilot
**pair** (C# v1 with TS v1), paid from the $40.

### 11. The T3 rule

If the A/A shows T's main coverage (G-4's main half) AND subagent coverage, on the A/A or in the committed E2.S2 smoke fixture (B-T1, same CLI), then `t3: stop` for the bare data cells.
Otherwise `fallback-t2`: `t2: stop`, with O and E reconciled per model within 1 %. If O and E cannot be reconciled either, no data pair runs. The decision is one dated commit of
`inputs/crosscheck-policy.json` (only `t2`, `t3` and their dated reason may change) after the A/A and before pair 1.

### 12. The seed

The bare seed is committed as a hash only: `inputs/seed.sha256` holds the sha256 of a seed kept outside git (mode 600). After the A/A is GO and its amendments are pushed, and before pair
1, `bare-runner.mjs seed-reveal` writes `inputs/seed.txt`; the guard refuses `pair:<k>` unless it hashes to the tagged `seed.sha256`. The seed fixes the data rids, pair 1's masks and
the launch and scoring order of every phase. The process arm's draw gets its own seed.
