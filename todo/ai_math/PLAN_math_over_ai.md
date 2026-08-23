# PLAN — Math over AI: measure real agent sessions, find the steps a formula can do

> Status: **plan only, 2026-08-23.** Scope: a new measurement family under `todo/ai_math/` —
> deterministic traces of real AI-agent sessions, mined for places where deterministic code can
> replace model work. This file is the WHAT and the WHY; the sibling
> [PLAN_session_measurement.md](PLAN_session_measurement.md) is the HOW of the capture.
>
> Related: [../PLAN_tool_benchmark.md](../PLAN_tool_benchmark.md) §3.5 (names the gap this family
> closes: "the CLI runs its own loop and the harness cannot see inside it"),
> [../../research/PLAN_tool_telemetry_v0.md](../../research/PLAN_tool_telemetry_v0.md) (the existing
> telemetry pipeline this extends), [../../research/MEASURED_LESSONS.md](../../research/MEASURED_LESSONS.md).

## 1. The goal, before any solution

An AI coding agent spends tokens, seconds and turns on steps whose outcome is fully determined by
their inputs. A model that greps four spelling variants of the same identifier is not exercising
judgment — it is executing a bad version of what an index answers in one call. A model that re-reads
the same file for the third time in one session is not learning — it is paging, expensively. A model
asked "did the build pass" is parsing an exit code with a trillion parameters.

The goal of this family: **measure real agent sessions across runtimes, find the recurring patterns
where the model does deterministic work badly, price each pattern in tokens and time, and replace
the most expensive ones with code — then prove each replacement in this benchmark's own A/B
machinery.**

"Math" here means any deterministic computation: a parser, a graph query, an index lookup, a hash
comparison, a counter, a classifier written as a pure function. The defining property is that the
same inputs always produce the same answer, at effectively zero marginal cost, with no model call.

Two constraints are fixed from the start:

- **No LLM anywhere in the analysis pipeline.** The trace analyzer is a pure function over the
  trace: same trace in, same findings out, every run. The moment a model judges the sessions, the
  findings inherit its variance and the whole exercise measures the judge.
- **Measured, not intuited.** A candidate without a measured cost is not a candidate. Everyone
  already "knows" agents loop; the deliverable is *which* loops, *how often*, at *what price*, on
  *which runtime* — numbers an intuition cannot supply and a replacement cannot be justified without.

## 2. Why this family already believes the thesis

This is not a hypothesis this repository starts from zero on. Four measured precedents, all already
paid for:

| Precedent | What the formula beat | Where recorded |
|---|---|---|
| The question-review panel | Three CLI reviewers found **nothing the substring arithmetic could not** (2 rejections in 57 marks, both reproducible mechanically) | `../../research/PLAN_question_authoring.md`, 2026-08-18 |
| The doctrine finding | One ordering sentence moved a score **16.5 points of 63** where swapping 4 tools for 18 moved **1** — the biggest lever was text plus arithmetic, not more model | `../PLAN_tool_benchmark.md` |
| The zero band | ×10 padding bought ×0.88; 160 of 160 padded steps zeroed — a formula resisted inflation a judge can be talked into | `../PLAN_scoremeter_port.md` (ported mechanism) |
| The register finding | Left alone, two models opened **every** code task with identifier-variant grep and never once asked semantically — the loop a model chooses on its own is measurably the wrong algorithm | `DewFlow · research/PLAN_mcp_eval_v4/RESULTS.md` §Comparative 1 |

## 3. The three replacement classes

**Class A — analysis a model performs that a formula answers.** LLM-judge slots where mechanical
scoring saturates, review panels whose verdicts reproduce by arithmetic, and — recursively — the
phase classification of an agent session itself: Research vs Execution vs Verification is decided by
tool names, command shapes and a git-status digest, never by asking a model what it was doing.

**Class B — orchestration waste inside a live session.** The model burns turns doing deterministic
work with the wrong instrument: re-reading targets it already holds, chaining spelling variants of
one search, reading a whole file to edit six lines, re-deriving the project layout every session,
manually correlating compiler errors to files. The replacement is rarely "remove the model" — it is
a better tool surface or pre-provisioned context (`dew_flow_mcp` exists for exactly this), so the
model spends its turns on judgment instead.

**Class C — whole pipeline steps.** Entire phases of a harness or workflow where the model
contributes nothing a signal does not: a judge phase on tasks where the mechanical signals already
decide, a verification turn that re-states an exit code, a summarization step whose input is
already structured.

## 4. How candidates are found — the detectors

Pure detectors over the session trace (schema in the sibling plan). Each emits pattern instances
with a cost attribution: tokens in/out of the surrounding turns, wall time, turn count.

1. **Re-research loop** — a read-class call whose normalized target (file path, search query) was
   already read since the last write-class call, three or more times. Deliberately NOT "3+ reads
   after an edit": a read after an edit is healthy verification, and flagging it would teach the
   map to punish exactly the behaviour we want.
2. **Search-variant chain** — ≥3 consecutive search calls whose normalized queries are
   near-identical (token overlap above a threshold) with no follow-up read of any hit between them.
   This is the register finding, detected live.
3. **Window waste** — a whole-file read where every subsequent edit to that file touched a span a
   fraction of its size; prices what an outline or line-window read would have saved.
4. **Build-fix cycle** — build → fail → read → edit → build sequences; counts cycles per session and
   prices the read step: could the compiler's own file:line list have been routed directly instead
   of re-discovered?
5. **Layout re-derivation** — directory listing / glob calls repeated across sessions against an
   unchanged tree.
6. **Phase economics** — time, tokens and turns per phase; switch counts; the share of a session
   spent in each. Not a defect detector — the denominator every other number is read against.

## 5. The deliverable — the replacement map

A ranked table, per target repository and runtime:

`pattern · frequency · measured cost per session (tokens · seconds · turns) · proposed deterministic
replacement · expected saving · verification plan`

Each accepted row becomes its own plan. The first map is produced after **≥20 traced sessions across
≥2 runtimes** — earlier than that the frequencies are anecdotes.

Verification is this benchmark's own machinery: the replacement becomes an arm (a lane, a tool-surface
row, a doctrine line — whichever axis it is), and the same tasks run with and without it. That is
exactly how the doctrine was priced, so the method needs no new instrument.

## 6. Guards

- **No model call reachable from the analyzer** — enforced the way layering is already enforced
  here, by an architecture test, not a convention.
- **Observed and reconstructed traces are never blended** — the discipline
  [../PLAN_tool_benchmark.md](../PLAN_tool_benchmark.md) already states for its two vantage points;
  the session trace adds a third and every row names its `source`.
- **A gap in instrumentation is never a zero** — the `Captured` rule. A runtime whose adapter
  cannot see tool results reports *not captured*, and the map never converts that into "this
  pattern does not occur there".

## 5a. What the instrument costs

**No tokens at all.** The capture path is a hook process, an HTTP post, a Postgres insert and pure C#;
nothing in it reaches a model, and the guarantee is structural rather than remembered — every detector
lives in `Bench.Domain`, which references nothing beyond the runtime, and `ArchitectureTests` names them so
that moving one into a layer where the model ports are declared is a red build.

Two channels were checked rather than assumed, because both could have cost tokens invisibly:

- **The hook writes nothing to stdout**, on any of its five events, and always exits zero. That matters
  because an agent injects some hooks' stdout into its own context — a chatty hook would have made every
  tool call more expensive, silently and forever. Measured: 0 bytes, all five events.
- **The hook never modifies a tool call.** No decision JSON, no blocking exit code, so the model sees
  exactly the conversation it would have seen uninstrumented.

What it costs instead is **time**: ~350 ms per tool call (§0(c)), and about 130 ms more on shell calls for
the porcelain read. That is the honest price, and it is paid in wall-clock rather than in money.

What *does* spend money is step 4 of the build order — **verifying** a replacement. `bench run` is the one
verb that reaches a model, and pricing a candidate against its baseline is a real measurement with a real
bill. The distinction is worth keeping sharp: tracing is free, and proving a replacement works is not.

## 6a. What the first traced sessions already showed (2026-08-23)

The capture landed the same day, so this section is short and will grow. It is here because the plan says a
candidate without a measured cost is not a candidate, and these are the first measurements.

- **The instrument costs ~350 ms per tool call** (two hook processes at ~175 ms each; the porcelain digest
  adds ~130 ms to shell calls only). Every phase number below is a number with that overhead in it, and the
  duration column was rebuilt once already because it had been measuring almost nothing else.
- **A read-heavy investigation session** — 24 calls against this repository — split 22 research · 2
  execution · 0 verification, with **223 s** of wall time in research. The research total is dominated by
  the gaps between calls, which is the model thinking; that is the denominator every replacement candidate
  will be read against.
- **The allowlist detector paid for itself immediately.** It named `find … -name` as a command counted as a
  write that changed nothing — this system's own taxonomy telling us what to add to it, produced by
  measurement rather than by guessing which commands look safe.
- **Class A got its first confirmed instance, and it was ours.** The first session ever traced was asked to
  find a bug in this repository and found one in the recorder itself: a fabricated `Unchanged` over a single
  worktree reading (§10 of the sibling plan). A defect of exactly the kind this family exists to catch — a
  measurement that was computed rather than observed.

## 7. Build order

0. Step-0 fact-check spikes — owned by the sibling plan, findings recorded there.
1. Capture for Claude Code end-to-end (sibling plan, steps 1–2).
2. Detectors 1–2 and phase economics, as pure functions with fixture tests.
3. First replacement map from ≥20 real sessions.
4. One replacement implemented and A/B-verified; the measured saving recorded here.
5. Remaining detectors and runtimes, in the order the map's gaps justify.

## 8. Test plan

- Fixture traces per detector: the loop fixture flags; the **healthy-verify fixture must NOT flag**
  (an edit followed by a re-read and a build); the near-identical-search fixture flags while a
  genuinely progressing search refinement does not.
- Determinism as a property: same trace, same findings, byte-identical, across repeated runs.
- Cost attribution arithmetic against hand-computed totals on a small fixture.
- The architecture guard: the analyzer module references no model-runtime port.

## 9. Definition of Done

- [ ] ≥20 sessions traced across ≥2 runtimes land in Postgres with phases labeled.
- [ ] The replacement map exists with ≥5 candidates, each carrying a measured cost.
- [ ] At least one replacement is implemented and A/B-verified, with the saving (or its absence)
      recorded here as a deviation.
- [ ] No model call is reachable from the analyzer — proven by an architecture test.
- [ ] The `todo/README.md` table row for this plan is current.
