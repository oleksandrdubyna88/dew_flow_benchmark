# PLAN — part 6 of the cheaper-writers experiment: Qwen 3.8 27B on vLLM as the task coder

> Status: **plan only, 2026-10-26 — to run 2026-10-27 (owner).** Extends
> [research/local-model-coding/](../research/local-model-coding/README.md) (parts 1–5); nothing here has run yet.

## 1. Goal

Parts 1–5 asked whether a cheaper model can do part of a Sonnet coder's writing on one real task (coai stories E5.5 +
E5.6, 12 scenarios, from `b4e2ea96`). The local model tried there (Qwen3.6-35B-A3B) drafted badly when it saw only a
prompt (results.md §3–§4); Haiku, given exactly specified tasks, wrote almost everything and almost all of it stayed
(results.md §6). The owner's question for part 6: **with the same exactly specified tasks, can a local Qwen 3.8 27B be the
task coder — first tuned for the best code it can write — and what does that do to time, Claude cost, tokens and
quality?** Two schemes, four runs each:

| Arms | Split | Task coder | Check | Fixes |
|---|---|---|---|---|
| **QO1–QO4** | Opus | Qwen 3.8 27B (vLLM) | one Opus review of the whole stories | Haiku |
| **QS1–QS4** | Sonnet | Qwen 3.8 27B (vLLM) | one Opus review of the whole stories | Haiku |

They differ from part 5's **O** (Opus split, Haiku codes) and **S** (Sonnet split, Haiku codes) arms in ONE thing: the
task coder. So the comparison is QO against O and QS against S, with classic (C ×4) as the band.

## 2. What exists (verified 2026-10-26)

- **The model is on disk**: volume `hf-cache`, `/root/.cache/huggingface/local/Qwen3.8-27B-AWQ` — `Qwen3_5ForConditionalGeneration`
  (`qwen3_5_text`, dense, 27B, a vision-language checkpoint used for text), 64 layers, hidden 5120, 24 attention heads, 4 KV
  heads, head dim 256, **hybrid attention: 48 linear-attention and 16 full-attention layers**, 262 144 positions; weights
  int4, group 128 (compressed-tensors "pack-quantized"). Its own `generation_config.json`: temperature 1.0, top_p 0.95,
  top_k 20. Read with a read-only `wslc` container (no GPU).
- **The start script** (`C:\Users\strug\vllm\start-vllm.ps1`, changed 2026-10-26 by another session; we only read it)
  defaults to this model: `-Model …/Qwen3.8-27B-AWQ -ServedName qwen3.8-27b`, `-MaxModelLen 16384`, `-MaxNumSeqs 16`,
  `-ReasoningEffort medium` (passed as `--default-chat-template-kwargs.reasoning_effort`), MTP on (2 draft tokens) with two
  patches, `--reasoning-parser qwen3`, `--generation-config /genconfig`; it accepts `-ExtraArgs` and `-Env`. Its
  `genconfig\generation_config.json` OVERRIDES the model's sampling with temperature 0.3, top_p 0.9, top_k 20,
  repetition_penalty 1.05. `VLLM-PARALLEL.md` still documents only the 35B-A3B; **no measurement of the 27B exists yet**.
  The script, `genconfig\`, `vllm.env` and `patches\` are not ours to edit without asking (`VLLM-PARALLEL.md` §9).
- **vLLM is reached from WSL only through Windows `curl.exe`** (it binds `127.0.0.1` on Windows):
  [ask.mjs:15](../research/local-model-coding/harness/ask.mjs) (`URL`), `:16` (`MODEL` — the 35B's name, to change), `:17`
  (`LIMIT = 4`).
- **The task pipeline to reuse**: [t-lib.sh](../research/local-model-coding/harness/t-lib.sh) — the knobs `SPLIT_MODEL` `:8`,
  `TASK_REVIEW` `:9`, `FIX_MODEL` `:10`; `run_task` `:58` runs the coder as a Claude session at `:72` (`step "2-$id-code"
  haiku …`) and saves its diff at `:74` (`task-$id-haiku.diff`); `run_t_arm` `:102`. The O/S case in
  [run-arm.sh:154](../research/local-model-coding/harness/run-arm.sh), the D case `:159`. The split brief
  [x-split.template.md:18–19](../research/local-model-coding/harness/briefs/x-split.template.md) tells the splitter its coder is
  "an agent in the clone: it can read files, edit, build and run tests". The survival script reads the coder's diff by its
  Haiku name: [t-survival.mjs:24](../research/local-model-coding/harness/t-survival.mjs). The mutation checks
  ([mutate.mjs:34](../research/local-model-coding/harness/mutate.mjs), six frozen mutants) and the incremental absolute judge
  ([judge-abs.sh](../research/local-model-coding/harness/judge-abs.sh)) take new arms as they are.

## 3. Decisions

- **D1 — Qwen codes a task through a script loop, not as a tool-calling agent.** `qwen-task.mjs` (new): the prompt is the task
  document + the CURRENT content of every file the task owns + the content of the files the document names as "existing code
  to use" (bounded; refused past the context limit); the answer is the complete new content of each owned file, one fenced
  block per file with its path. The script writes ONLY owned files (anything else is rejected and logged — the rail),
  builds, runs the task's test classes, and on compile errors or failing tests sends the errors back for another
  iteration — **at most 3** — then commits by name what it has. Why not an agent: a tool-calling loop (Codex CLI with a custom
  provider, or vLLM's tool parser) needs `--enable-auto-tool-choice --tool-call-parser …` through `-ExtraArgs`, and WSL cannot
  reach `localhost:8000` except through `curl.exe` — untested, and a second variable. Haiku in O/S was a real agent that
  could read any file, so **the coder's access differs** too; that is recorded, not hidden.
- **D2 — Phase 0 tunes Qwen BEFORE any arm, and freezes the result.** On 3 real tasks taken from the T and O arms (their task
  documents are in `data/arms/T*/tasks/`; the start state is each task's `task-<id>.before` commit, reproducible from the
  arm's branch), run a small grid and score each cell by **the task's own tests passing after the loop** (then fewer
  iterations, then time):
  - thinking: off · on with `reasoning_effort` low · medium · high (per request: `chat_template_kwargs`);
  - sampling: the start script's (0.3 / 0.9 / 20 / 1.05) · the model's own (1.0 / 0.95 / 20) · 0.6 / 0.95 / 20;
  - system prompt: none · a fixed coder prompt (C# / .NET 10 / xUnit v3 / FluentAssertions; use only the APIs shown; return
    complete files; no prose);
  - `max_tokens` large enough for thinking + files (start 24 000; an empty answer with `finish_reason: length` is a failed
    cell, not a retry).
  Not every combination: thinking × effort first (4 cells × 3 tasks), then sampling and the system prompt on the best
  thinking setting (≤ 6 cells × 3 tasks). Sampling that the `genconfig` mount fixes is overridden per request
  (`temperature`, `top_p`, `top_k`, `repetition_penalty` in the body), so **no start-script file is edited**.
  `-MaxModelLen 65536` and `-MaxNumSeqs 4` through the script's own parameters: with only 16 full-attention layers the KV cache
  is small (estimate ~64 KB per token at fp16, ~4 GB per 64K sequence) — confirmed or corrected by vLLM's own start log
  before Phase 0 counts.
- **D3 — the arms reuse part 5 unchanged except the coder**: `x-split` (Opus or Sonnet), `tasks-plan.mjs`, parallel batches in
  worktrees, ONE fresh Opus review (`2-review.template.md`), Haiku fixes (`h-fix.template.md`). A new knob `CODER=qwen` in
  `t-lib.sh` replaces `:72` with `qwen-task.mjs`; the split brief gets one sentence changed (`q-split`): the coder sees ONLY the
  task document and the files it names, cannot search, and gets its build and test errors back up to 3 times — so the
  splitter knows it must put every needed signature into the document. The diff is saved as `task-<id>-coder.diff`
  (`t-survival.mjs:24` reads both names).
- **D4 — two arms at a time** (QO_n with QS_n), so both share the GPU and the time of day; their parallel tasks queue in vLLM's
  4 slots. Haiku fixes and the Opus steps are unaffected by the GPU.
- **D5 — measurements**: everything part 5 measures, plus per task: Qwen iterations, tokens (prompt / completion / thinking),
  GPU time, compile errors and failing tests per iteration, and whether the task ended green; survival of Qwen's lines;
  vLLM's own throughput under 1 and under 4 requests for this model.
- **D6 — owner approvals needed at the start (2026-10-27)**: a run without the GPU lease (`dew_flow_rag_qln`'s daemon was not
  running in parts 1–5 — check again), vLLM started with `-MaxModelLen 65536 -MaxNumSeqs 4`, and that no other session needs
  the GPU for the day.

## 4. Build order

| Epic | What | Done when |
|---|---|---|
| E1 | `qwen-task.mjs` (D1) + `ask.mjs` parameterised by model name and sampling | the toy repo test (§5) passes, including a forced compile error that the loop repairs |
| E2 | vLLM up with the 27B; warm-up; throughput at 1 and 4 requests; the KV capacity from the start log | the numbers are in the protocol |
| E3 | Phase 0 (D2): `explore-qwen.mjs` runs the grid on 3 tasks, scores each cell by the task's tests | the frozen configuration and its scores are in the protocol, BEFORE E4 |
| E4 | `CODER=qwen` in `t-lib.sh`, `q-split` brief, QO/QS cases in `run-arm.sh`, `after-q6.sh` (D4), the metrics/compare/tables/judge lists | a QO arm passes end to end in fake mode |
| E5 | the protocol entry: design, predictions (§7), owner approvals (D6) | written before the first arm starts |
| E6 | the 8 arms | all done, or each stop explained |
| E7 | objective checks, survival, mutation checks (`mutate.mjs`), the absolute judge (incremental), metrics | all hand-checked on a sample before they are reported |
| E8 | part 6 in `results.md`, `tables.md` regenerated, README and index rows; promote this plan | the docs PR (#106) updated |

## 5. Test plan

- **`qwen-task.mjs` on the toy repo** (the same toy used for `fill.mjs`): (1) a correct answer is written and committed;
  (2) an answer that writes a file the task does not own is rejected and logged; (3) a forced compile error (a wrong method
  name planted in the first answer) comes back to the model, and the second iteration builds; (4) the third failure stops the
  loop and commits what builds.
- **Phase 0 validity**: for each of the 3 tasks, the REFERENCE solution (the arm's final code for those files) must pass the
  task's tests, and an empty attempt must fail them — otherwise the scorer, not Qwen, is being measured.
- **The arm pipeline in fake mode** (as for T and O): a QO arm with fake tasks — batches, worktrees, carried back, branch name.
- **The checks of parts 1–5 unchanged** on the new arms: objective gate, mutation checks, absolute judge.

## 6. Risks

- **Speed**: a dense 27B is much slower than the 35B-A3B MoE (~3B active); thinking on a task can take many minutes. Phase 0
  measures it; an arm's task step keeps a hard limit (20 min per task, as Haiku had).
- **Context**: task documents (3.9–10.1k words in T) + owned files + named files + thinking may exceed 64K; the script refuses
  rather than truncates, and the refusal is a result.
- **Thinking runaway** (seen in part 1's warm-up): `max_tokens` bounded; a `length` finish is counted.
- **The start script belongs to another session**; its defaults may change overnight — read it again at the start, record the
  exact command used.
- **The disk**: SMART reported fine (2026-10-26); keep two arms at a time.

## 7. Predictions (to be finalised in the protocol after Phase 0, before the arms)

- Claude cost of QO ≈ O's minus Haiku's coding (< $0.6) plus more Haiku fixing — within ±15 % of O; QS the same against S.
- Qwen's tasks end green less often than Haiku's (some tasks will reach the 3-iteration limit red); survival of Qwen's lines
  below Haiku's 90–98 %.
- Elapsed time longer than O/S (local generation), unless Phase 0 finds a fast setting that still passes.
- Quality inside the classic band, with more open findings after the Haiku fixes than in O/S.

## 8. Definition of Done

- [ ] Phase 0 ran and its frozen configuration (thinking, effort, sampling, system prompt, `max_tokens`, context) is in the
      protocol with its per-cell scores, before any arm started.
- [ ] `qwen-task.mjs` passed the toy tests (§5) and a QO arm passed in fake mode.
- [ ] The 8 arms ran (or each stop is explained), two at a time; no start-script file was edited.
- [ ] Objective checks, survival, mutation checks, the absolute judge and the metrics exist for all 8, hand-checked on a sample.
- [ ] Part 6 is in `research/local-model-coding/results.md` with predictions against observations; `tables.md` regenerated;
      this plan promoted to `research/` with its deviations.
