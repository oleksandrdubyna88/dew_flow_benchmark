# Statement, bare scope

The pre-registration of the **bare arm** of the minigit stack bench (E3b.S3, `todo/PLAN_minigit_stack_bench_E3_bare.md`), written on 2026-10-30 by Opus before any paid minigit
session of the task. It says what is measured, under which conditions, and why. How the numbers are computed is in `analysis-contract.md`; the prediction is in `prediction.md`.
Quotes of the parent plan are from `todo/PLAN_minigit_stack_bench.md` at `85c54b790a37479a32fc88b7b0b4353652fed601`, a commit of origin/main.

**Frozen by the tag.** These files are committed in one commit, and the annotated tag `prereg/minigit-bare-<yyyymmdd>` is put on that commit and pushed to origin before the A/A's first
launch. The tag is never moved or deleted. `init` records the tag and its commit in `run-map.json`, and the pre-registration guard refuses every paid launch whose registered files
differ from the tagged ones (the exceptions are `amendments.md`, append-only; `seed.txt`, revealed after the A/A; and `crosscheck-policy.json`, changed only by the dated T3 commit).

## 1. The question

Parent §1, part (a); part (b) is the process arm and is registered with the process scope:

> On the minigit task (SPEC-v1 then SPEC-v2), with **Claude Code** (Opus team lead, effort `high`), how do **C# 14 /
> .NET 10** and **TypeScript 7.0.2 on Node 26** compare in **correctly counted** cost, decomposed wall time, and
> **held-out** correctness — (a) for a bare agent with the upstream prompt, […part (b), the process arm — registered with the process scope…]

> It is not a replication of banteg's run (other agent, other model, other date); only ratios are compared with it, with
> that caveat in the same sentence.

## 2. Why: the claim being re-measured

A widely shared result says coding agents are slower and more expensive in C# than in TypeScript on the small greenfield task "minigit". The C# claim comes from
`banteg/minigit-bench` @ `83644e0` (Codex CLI 0.111.0, gpt-5.4, `fast` tier, 3 trials per language, Node v25.8.0, .NET SDK 10.0.103). Parent §2 read its code and re-priced its own
`results/results.json`:

- **Its cost is wrong.** OpenAI's `input_tokens` already contains `cached_input_tokens`; banteg's formula prices all input at the full rate and then the cached part again
  (`benchmark.rb:162-167`). So cached input is billed twice. Re-priced from its own tokens at its own rates, C# $2.75 → $0.60 and TypeScript $1.61 → $0.46: C#/TS 1.71× → **1.32×**.
- **Its time mixes two things.** The timer covers the agent's whole process, every build and test run included (`benchmark.rb:243-245`), so model work and tool work cannot be told apart.
- **Its quality signal is weak.** Two visible tests pass whether the parent pointer is right or wrong, a build failure is swallowed, the test script can be edited by the agent, and
  `set -e` reports a crash as "no tests" (parent §2, O3–O7).

This measurement counts the same thing correctly:

- **Cost, four views.** **E** = the CLI's result envelope; **O** = the OTel `api_request` records; **T** = the transcripts; **B** = the ledger of the host-side credential broker, which
  forwards and prices every request the box sends, so it also prices a session that was killed before it wrote an envelope. The registered **primary** figure is the headline
  recompute (parent §9: tokens from E, the cache-write split from T, published prices). E and B stand beside it; checks T1–T5 compare the views on every session. Every spend figure a stop
  rule reads is the largest of E, the recompute and B, and a session without an envelope is priced by B. All figures are list-price API equivalents, not the subscription's bill.
- **Time, separated.** Wall (spawn to exit, scoring outside it), `model_api` (time in model calls), and `active − model_api` (the agent's tool and hook time with no model call active) are
  the primary time measures. `build_test` against `other_tool` is descriptive only, because one Bash call is classed as one category (V16).
- **Quality, held out.** The fixed visible tests, plus an encrypted held-out suite scored after each session, never in the agent's tree: `percent` (primary) and `defects`.

## 3. The arms

Two arms are planned (D1). **The bare arm runs first and alone** (D20, operator, 2026-10-29); the process arm is built later and registers its own scope as a dated amendment (E3.S3).

**Bare** = the task prompt only. No conventions and no instruction files, no MCP server (`--strict-mcp-config`, empty), no review gate, no consultants, no GitHub. The agent works in an
empty directory that holds only the phase's pinned inputs (v1: `SPEC-v1.txt` and the fixed `test-v1.sh`; v2: the v1 tree minus `bin/ obj/ node_modules/`, plus `SPEC-v2.txt` and the fixed
`test-v2.sh`). The session ends when the agent ends it, or at the cap, the wall or the stall limit.

## 4. The conditions

| Condition | Pinned value |
|---|---|
| Agent | Claude Code CLI **2.1.310**. `init` copies the pinned version to a read-only file (sha256 recorded); every session runs that copy, never the `claude` on PATH |
| Model and effort | `--model opus` (the alias; D12: the id each used alias resolves to is recorded by the first session, and a new id stops the series); effort `high` for Opus, Sonnet and Fable, pinned in the session's own settings file (D17) |
| Limits per session | cap **$6** (the broker's budget and the CLI's `--max-budget-usd`), wall **60 min**, stall **45 min** |
| C# | C# 14 on .NET 10, **.NET SDK 10.0.114**. The pin was 10.0.113 until 2026-10-30, when an unattended `apt` upgrade replaced it on the host; it was changed to 10.0.114 that day, before this pre-registration was tagged (`research/module_minigit_bench.md`, section 6). Build servers and node reuse stay at the .NET defaults, as in banteg's cells |
| TypeScript | **Node 26.12.1** with native type stripping, **TypeScript 7.0.2** (`tsc --noEmit` as the type check), installed read-only on PATH; the Node cache snapshot holds `@types/node` 26.6.6 |
| The box | a `bwrap` agent box with its own pid, ipc and network namespaces; the operator's home hidden except the session's own directories; `gh` hidden. Network only through a host-side CONNECT proxy to `registry.npmjs.org` and `api.nuget.org` (D22). The API is reached only through the host-side credential broker: the real token never enters the box, which holds a per-session dummy (D23). Permissions come from an allow-list; no permission bypass (D11) |
| Offline caches | one cache snapshot per stack, made once through `cache-recipe.json` and registered by tree hash in `cache-snapshots.json`. **Both** snapshots are copied into **every** session, so the two stacks' boxes differ only in the prompt; each copy is re-hashed against the registered value. The scorer builds offline from the session's own caches |
| CPU | the host shows 14 vCPUs as 7 cores × 2 SMT threads. The two cells of a pair run on masks **0-5** and **8-13** (swapped each pair; the seed decides pair 1), at `nice 19`; CPUs **6-7** are left to the harness and the OS. A phase does not start on a loaded machine; load during a phase is flagged (`quiet.json`) |
| Machine | one WSL2 VM, Linux 6.18.54.1-microsoft-standard-WSL2, 14 vCPUs, 43 GB memory, on the operator's PC; every session of the series runs on it |
| Prices | `prices-2026-10-29.json` (the pricing page; the Fable row read on 2026-10-29) |

**Pairs.** A pair is one C# cell and one TypeScript cell started in the same tick on their masks; the v2 sessions start only after both v1 sessions are collected and scored. Before
any data pair, an A/A (two identical C# v1 sessions at once) and one Node v1 pilot prove the instruments on the real task (`analysis-contract.md`, section 10).

## 5. The prompt and the stack label

The exact prompt bytes per stack and phase, with their sha256, are `prompts-bare.json`, quoted from parent §4.3. The two stacks' prompts differ only in two slots: `{STACK}` and
`{STACK_SENTENCE}`.

- **What banteg's prompt said.** In `benchmark.rb` (`banteg/minigit-bench@83644e0`, lines 41 and 147; checked by the team lead on 2026-10-30), `csharp` has `prompt_name: 'C#'`, so
  its prompt said `C#`; `typescript` has no `prompt_name`, so its prompt said `lang.capitalize`, which is `Typescript`. banteg had no `extra_prompt` for either language.
- **What ours says.** Parent §4.3 change (1) makes the label `TypeScript`, deliberately: the product's own spelling. Change (2) puts a stack sentence into upstream's own
  `extra_prompt` slot, as `{STACK_SENTENCE}`, because it pins the toolchain under test (upstream's TypeScript ran `tsx`). The sentences are, verbatim:
  - C#: *"Use C# 14 on .NET 10 (SDK 10.0.113 is installed)."*
  - TypeScript: *"Use TypeScript run directly by Node 26 (native type stripping, no build step), with `tsc --noEmit` as the type check."*
- Changes (3) and (4) of §4.3 are the same for both stacks: "the v1 implementation" instead of "a passing v1 implementation", and banteg's v2 wording, not mame's.

## 6. What is measured

Per session: how it ended (the outcome class), cost (the primary recompute, with E, B, per model and per token kind), time (wall, `model_api`, `active − model_api`, and the descriptive
categories), and quality per phase (build, the visible result per script, held-out `percent` and `defects`, the blocked count; the upstream-original score and the per-invocation latency
as references). Per pair: the C#/TS ratio of the cell values (v1 + v2) for the four primary measures, as a value or, when a side was cut short, as a bound. Over the pairs: the ratios, their
median and "in k of n pairs", each also without `ran unpaired` cells and without flagged pairs. `analysis-contract.md` fixes every one of these before any cell.

**Classification by how a session ended.** A session is classed by its ending, never by the exit code: a transient cap refusal or upstream 429 that the session survived is a flag, not a
class. Three endings parent §11.1 does not name are **registered extensions of §11.1**, each with its reason in `analysis-contract.md`, section 3: `void: upstream` (the provider ended
the session), `void: harness (interrupted | disposed)` (the harness or the host ended it), and `stop: auth` (a 401 or 403: the credential, not the stack, so the series stops).

## 7. What n = 3 licenses

The result is **descriptive**: three pairs, reported as "in k of 3 pairs", with medians and ranges, and whether ranges overlap. No significance is claimed. Parent §11, verbatim:

> **What n = 3 licenses:** "in k of 3 pairs", and whether ranges overlap (overlap = not distinguished). A predicted direction
> held in 3 of 3 pairs is the strongest available result and still has 1/8 probability under no effect — so no p-values, no
> confidence intervals, no effect sizes as findings. It does not license other tasks, languages, models or CLI versions, the
> value of the process on real work, or any mechanism.

If a named stop or the $40 bare budget ends the arm early, the report gives "in k of n pairs" for the n reached and names the missing cells.

## 8. What the bare-first deviation weakens

From E3b.S3, verbatim:

1. **The interaction** (prediction 3) compares windows days apart instead of interleaved blocks: server-side model changes,
   API latency and time of day differ between arms. D12 catches an alias resolving to a new id, not a change behind one id.
2. **No arm-lead draw** for the bare cells: order between arms is confounded with time.
3. **T3 is armed for bare cells on bare evidence** (the A/A and the E2.S2 smoke); the process arm re-decides at P1.
4. **The process treatment is finalized after bare results are visible** (E2.S4). Parent §6 already fixes it; any change after
   the first bare pair is a dated amendment applied to both stacks, never motivated by one stack's bare result.
5. **The prediction is registered in two parts**; the process part is public since `01b7459` with its dated edits, all
   before any cell, so the split costs no evidence.

The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.

## 9. Money

The bare arm (A/A, Node pilot, three pairs, and every re-run) stops at **$40**, inside the series ceiling of **$500**. The $40 may be raised once, only to finish the re-runs of a pair
already started and only by their worst case, as a dated amendment. **Pairs 1–3 run unless a named stop fires**: the operator's go chooses when a pair runs, never whether.
