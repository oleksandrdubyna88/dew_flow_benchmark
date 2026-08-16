# PLAN — the compute backend becomes an axis, and its first question is WSL against Windows on one card

> Status: **plan only, 2026-08-16 — nothing implemented.** Scope: a MEASUREMENT first (phase 1 needs no code
> in this repository), then the engine's backend echo in `Bench.Domain`/`Bench.Contracts`, then the run
> matrix. **Three of phase 1's five arms run today; the two WSL ones do not** — they are blocked on the
> prerequisite named in §6, which is the same blocker as
> `dew_flow_sidecar_rust · research/PLAN_reliability_tail.md` item 1.
>
> **Read §1b before the rest.** The host and the execution provider cannot be separated on this hardware —
> MIGraphX exists only under Linux/WSL and DirectML only on Windows — so "WSL against Windows" is
> unavoidably also "MIGraphX against DirectML", and the plan is shaped around saying so rather than around
> hiding it.
>
> Related: [PLAN_variant_matrix.md](PLAN_variant_matrix.md) (step 4 wires an engine into a leg; phase 3 here
> cannot land before it), [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §5.1 (the accelerator lease this
> measurement is void without), [PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md) (the
> echo-and-block discipline this copies wholesale),
> [../research/architecture.md](../research/architecture.md) (the measurement contract the axis extends).
> The measured party: `dew_flow_rag_qln` owns the card and the sidecar; the sidecar is
> `dew_flow_sidecar_rust`.

---

## 1. The question — and why it does not have one answer

The operator's question is: **on one AMD Radeon AI PRO R9700, is the sidecar faster launched under WSL
(ROCm/MIGraphX) or on Windows (DirectML)?**

It is a good question and it is under-specified, in a way that decides the answer rather than decorating it.
At least three different quantities can be called "faster" here, and the two flavours are known to rank
differently on them:

| # | What "faster" means | Why the two flavours differ on it |
|---|---|---|
| **A** | **Steady-state throughput** — an already-compiled shape, warm engine, repeated identical batches | Raw kernel quality. This is the only one that is a fair fight between the two EPs. |
| **B** | **Time to the first usable result after a cold start** | MIGraphX compiles **per distinct input shape**: 2–4 minutes and ~2.5 GB of on-disk cache each, measured on an R9700 (`dew_flow_sidecar_rust · src/inference.rs:24-27`). DirectML takes dynamic shapes natively, so the same transition is a session rebuild of tens of seconds. |
| **C** | **A whole index pass, end to end** | Where A and B compose with the rung ladder and with VRAM behaviour — including a failure mode that has no throughput number at all (§2b). |

**So the deliverable is three numbers per flavour, reported side by side and never averaged into one.** That
is this repository's existing rule applied to a new subject: observed and reconstructed tool calls are stored
beside each other and never folded together ([../research/architecture.md](../research/architecture.md), *Two
vantage points*), for exactly the reason that folding them would produce a single figure nobody can act on.

A single "WSL is 1.4× faster" headline would be the wrong artefact here even if it were true, because the
operator's real decision — which flavour serves indexing, which serves search — is decided by B and C far
more often than by A.

### 1b. The host and the execution provider cannot be separated — a property of the hardware, not of the harness

**MIGraphX exists only under Linux/WSL.** No prebuilt ONNX Runtime ships that EP at all; the library this
machine uses was built from source inside the distro, and the sidecar's `migraphx` flavour therefore loads a
machine-local `libonnxruntime.so` through `ORT_DYLIB_PATH` at runtime rather than linking anything
(`dew_flow_sidecar_rust · Cargo.toml:83-88`). On Windows it is not present to be selected. DirectML is the
mirror image: a Windows API with no Linux counterpart.

So **there is no WSL+DirectML arm and no Windows+MIGraphX arm.** "WSL against Windows" and "MIGraphX against
DirectML" are one comparison with two names, and every result of it must be labelled with both — a table
column headed *WSL* invites the reading "Linux is faster", which this measurement cannot support and did not
test.

One arm can separate them, and it is the reason to run it: **the CPU execution provider exists on both
hosts** — `compiled_providers` always contains `cpu`, because ort falls through to it when no EP is
registered (`dew_flow_sidecar_rust · src/provider.rs:161-162`). A CPU arm on each host holds the EP constant
and measures the pure host cost: weights read over DrvFs against ext4, WSL2 localhost forwarding, process
launch. It will be slow in absolute terms and that is irrelevant — it is the only arm in this plan that
isolates the operating system, and without it the two headline arms have a confound nobody can quantify.

---

## 2. What is already known, so that it is not re-measured

This family has already paid for several of these findings. They are listed as **inputs**, not as results to
reproduce; a probe that rediscovers them has spent a day learning what was written down.

- **DirectML on this exact card held 30.6 GB of 32 and produced three driver timeouts in an hour**, each one
  removing the D3D12 device and silently voiding every batch still queued
  (`DewFlow · src/v2/v2.AppHost/AppHost.cs:240-246`). This is measurement **C**'s dominant term and it is
  unbounded: a flavour that loses the device mid-pass cannot be characterised by a mean.
- **The MIGraphX workaround stack exists because that EP compiles per input shape**: shape pinning with ruler
  rows, the settling retry, the per-(engine × shape) `.mxr` cache, and rung transitions measured at 156–173 s
  before the rung cache absorbed them (`DewFlow · todo/PLAN_bge_sidecar_igpu_cpu_offload_probe.md:37-44`).
- **`should_pin_shape` is true only for `migraphx`** (`dew_flow_sidecar_rust · src/inference.rs:28-34`). This
  is a design fact, not a knob to equalise — see §3.
- **`bge-reranker-v2-m3` never ran under WSL/MIGraphX on this stack until the unfused export landed**: the
  compile finished and every kernel launch failed, the broken session stayed resident, and searches silently
  degraded — 177 HTTP 500s across a day, invisible until someone read the sidecar log
  (`DewFlow · src/v2/v2.Shared/Services/RerankSidecarPlan.cs:11-18`). Any rerank arm of this comparison must
  therefore prove it actually reranked, not that it returned 200.
- **The mixed WSL+Windows deployment exists, it shipped, and for two days it was the DEFAULT** — which is
  why it is an arm of this comparison rather than a hypothetical. Between 2026-07-30 and 2026-07-31 the
  embed fleet ran WSL/MIGraphX on the discrete card while a **separate Windows/DirectML sidecar served
  `/rerank` on the integrated GPU**: `RerankSidecarPlan` was opt-**out** (`IsOff`) with `Host` defaulting to
  `windows` and `DeviceId` to `1`, "the integrated GPU in both numbering schemes, which keeps the discrete
  card entirely for embedding passes" (`DewFlow · git show 47235cd0^:src/v2/v2.Shared/Services/RerankSidecarPlan.cs`).
  It became opt-**in** on 2026-07-31 (`DewFlow · 47235cd0`) once the MIGraphX rerank defect was fixed at its
  root, and the mechanism is still there today (`DewFlow · src/v2/v2.AppHost/AppHost.cs:397-415`).
  What has **never** existed is a mixed *embed fleet*: one key picks the flavour for every spawned embed
  sidecar (`:235-238`, `AddBgeSidecar` at `:285-320` — one exe path, one distro for the whole loop), and that
  is recorded as unbuilt Phase 2 work at `DewFlow · todo/PLAN_bge_sidecar_igpu_cpu_offload_probe.md:33-35`.
  So the topology axis is real but asymmetric: **rerank can cross hosts per instance, embedding cannot.**
- **One cross-host number already exists, and it is 15–25×.** At the shape the application actually sends
  (batch 64, pinned `(64, 1024)`, 50 candidates): **3.06 s** reranking on the discrete R9700 against
  **45–75 s** on the integrated 890M (`DewFlow · 47235cd0`, commit message). That single figure confounds
  host, EP and card all at once — which is exactly what §1b says is unavoidable, and exactly why this plan
  adds a CPU control rather than pretending otherwise. It is the number any new measurement must first
  reproduce before its other columns are believed.

### 2b. What the benchmark cannot express today — the structural gap

A result row is identified by `engine = (kind, endpoint, version, indexFingerprint)`
([../research/architecture.md](../research/architecture.md), *The measurement contract*). Two qln engines
differing only in which sidecar they call have the **same** kind, possibly the same version, and — since the
corpus is unchanged — the same index fingerprint. They differ in `endpoint`, which is a machine-local address
and not a description of anything.

So today the two arms of this comparison would be stored as two rows that no report can tell apart, and a
sweep that merged them would produce a mean over two different pieces of hardware. That is the same defect
`PLAN_corpus_axis_integrity.md` was written for — two variants resolving to one configuration and being
reported as a comparison of nothing — and it takes the same fix: **the engine echoes the compute backend it
actually served on, and a cell whose echo contradicts its arm is blocked with both named.**

Also missing, and both already have owners:

- **No hardware sampler** ([../research/architecture.md](../research/architecture.md), *What does NOT exist
  yet*; founding-plan step 7b at [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §5.1). Phase 1 does not wait
  for it — see §4, the instrument is the sidecar's own report.
- **A test's engine is one value per run, not an axis** — that is
  [PLAN_variant_matrix.md](PLAN_variant_matrix.md) step 4, and phase 3 here rides it rather than duplicating
  it.

---

## 3. The controls — skip one and the comparison is void

- **One at a time on the card.** Both arms target the same physical R9700; run them concurrently and every
  number is a contention measurement. The accelerator lease ([PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md)
  §5.1, [PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.4b) is not optional here even for a hand-run
  probe, and Ollama is a third consumer on this machine that never evicts (`OLLAMA_KEEP_ALIVE=-1`).
- **The same weights.** The WSL flavour seeds its ext4 model cache from the repo copy at startup
  (`dew_flow_sidecar_rust · src/preflight.rs:54-79`). A stale or partial seed is a different model, and
  comparing two models is not comparing two backends. Verify the seed reported "already seeded" before
  measuring.
- **The same shapes, read from the sidecar rather than from the launch line.** `/health` reports
  `loaded_embed_max_length` and `loaded_max_batch` — what it *ran*, not what was configured. This family
  already has the rule (`.claude/rules/shared/common/coding-style.md:53`): a value a service REPORTED and a
  value someone CONFIGURED are different kinds of fact. The comparison quotes the reported ones.
- **Shape pinning is NOT equalised.** Forcing it on for DirectML would charge that flavour the padding cost of
  a workaround it does not need; forcing it off for MIGraphX would measure a configuration nobody deploys.
  The flavours are compared **as they are actually deployed**, and the padding overhead belongs to the
  MIGraphX column. `PIN_INPUT_SHAPE=0` under MIGraphX is a legitimate *third arm*, labelled as such — never a
  fairness correction to the first two.
- **Warm-up is excluded from A and reported as B.** It is not noise to be discarded; on the MIGraphX arm it is
  the most expensive thing in the measurement and the single number most likely to decide the deployment.
- **Provenance, not labels.** Each arm records the serving process's `/health`: `active_provider`,
  `compiled_providers`, `adapter`, and the two build hashes (`exe_sha256`, `runtime_manifest_sha256` —
  `dew_flow_sidecar_rust · src/provider.rs:241-245`). The string "wsl" in a config file is not evidence that
  WSL answered. The precedent is exact: the iGPU/discrete split was proved by reading each sidecar's
  `/health.adapter` — `requested 0 → "AMD Radeon AI PRO R9700"`, `requested 1 → "AMD Radeon(TM) 890M
  Graphics"`, the **reverse** of Windows' own PnP order — rather than by trusting the device number
  (`DewFlow · research/PLAN_eval_v6/RESULTS.md:455-465`).
- **Repeats ≥ 3, and the spread is published.** A single pass per arm cannot separate a backend difference
  from a thermal one.
- **Never label a result with the host alone.** Per §1b the host and the EP move together on this hardware,
  so every column, every chart axis and every sentence names **both** — `wsl/migraphx`, `windows/dml` — and
  the CPU control is what any claim about the operating system has to rest on. A column headed *WSL* is a
  claim this measurement cannot make.

---

## 4. Phase 1 — the probe, which needs no code in this repository

The instrument already exists and is better than a stopwatch: every `/embed` and `/rerank` response carries
`PassTimings` (`dew_flow_sidecar_rust · src/wire.rs:72-82`), which splits the wall clock the caller measures
into four attributable parts:

| field | what it isolates |
|---|---|
| `queue_wait_ms` | waiting behind another request — infrastructure, never backend speed |
| `session_build_ms` | building and canary-checking the session; `0` on a warm engine |
| `inference_ms` | the forward pass(es), settling re-runs included |
| `compile_cache_grew_mb` | `> 0` ⇒ **MIGraphX compiled this input shape during this pass** |

That last field is what makes measurement **B** honest rather than inferred, and it became trustworthy only
recently: it is now scoped to the engine's own cache slice, so a rerank compile running concurrently is no
longer charged to an embed pass (`dew_flow_sidecar_rust · src/compile_cache.rs:9-24`, the `CompileWatch` doc).
A comparison built on the pre-2026-08-16 field would have attributed one engine's compile to the other's
throughput.

**The arms.** Named as `host/provider/card`, because §1b says none of the three may be dropped from a label:

| arm | what it is | why it is here |
|---|---|---|
| **W** `wsl/migraphx/R9700` | the indexing flavour | the discrete card's only GPU EP under Linux |
| **D** `windows/dml/R9700` | the same card, the other host | the only other way to reach it at all |
| **I** `windows/dml/890M` | the integrated card | the search half of the shipped mixed topology; the 15–25× figure of §2 lives here |
| **C₁** `wsl/cpu/—` · **C₂** `windows/cpu/—` | the control pair | the ONLY arms that hold the EP constant across hosts, and therefore the only evidence about the operating system itself |

W and D answer the operator's question; I is what makes the answer actionable, since the real decision is
which flavour serves indexing and which serves search; C₁/C₂ are what keep the answer from being mislabelled.

**The probe:** for each arm, against identical payloads, one at a time on the card —

1. **Warm-up** — one call at the production shape. Excluded from A, recorded whole as **B**, with
   `session_build_ms` and `compile_cache_grew_mb` reported separately from the inference.
2. **Steady state (A)** — N ≥ 10 calls at a fixed `(max_batch, max_length)`; report the median and the spread
   of `inference_ms`, and assert `compile_cache_grew_mb == 0` throughout. A non-zero value there means the
   shape was not actually pinned and the arm measured compilation, not throughput.
3. **Shape churn (B′)** — the same total row count delivered in batches of *varying* length. This is the
   measurement that separates the two flavours most sharply and the one closest to a real incremental pass;
   on the MIGraphX arm every new shape is a fresh compile, on DirectML it is nothing.
4. **Rerank, separately** — never folded into the embed numbers, and gated: the arm must show real scores, for
   the reason recorded in §2.

Output: a table under `research/`, plus an entry in
[../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) — the lessons file is where a finding goes
that later plans should be able to cite without re-running anything.

**This phase produces the operator's answer.** Phases 2 and 3 exist so the answer keeps being produced
automatically, and so the *next* backend question (iGPU against discrete, CPU EP, CUDA on other hardware) does
not need a hand-run probe of its own.

---

## 5. Phase 2 — the backend echo · Phase 3 — the axis

**Phase 2 (this repository, small).** `EngineDescriptor` gains a `ComputeBackend` echo — host (`windows` |
`wsl` | `linux`), provider (`migraphx` | `dml` | `cuda` | `cpu`), device, adapter name, and the serving
binary's hash — populated from what the engine REPORTS, never from what the run requested. Three states, not
two, copying [PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md): **matched · mismatched · not
declared**. An arm that names a backend and gets a different one back is **blocked with both named**; an
engine that declares nothing is *not declared*, which is honest for a third-party engine and must not read as
agreement.

**Phase 3 (blocked on `PLAN_variant_matrix` step 4).** The backend becomes a planned axis: one leg per
`(subject × backend × repeat)`, so the comparison is a run rather than an afternoon. Nothing here may land
before an engine is wired into a leg — planning legs against an axis no leg reads would produce cells that
cannot be executed.

---

## 6. The prerequisite that blocks phase 1 today — stated, not worked around

**The WSL arm cannot be launched on this machine as it stands.** The `migraphx` flavour loads a machine-local
`libonnxruntime.so` at runtime, and `ort` rc.12 requires ONNX Runtime ≥ 1.24; the build present under `/opt`
is 1.23.2, whose mismatch path *deadlocks* inside `ort`'s own version check rather than erroring — which is
why the sidecar preflights the dylib through the stable C ABI and exits with the required version instead
(`dew_flow_sidecar_rust · src/preflight.rs:11-21` and the verdict at `:184-198`).

So phase 1 needs, first: **ONNX Runtime v1.24.x built from source with `--use_migraphx`**, then
`cargo build --release --no-default-features --features migraphx --target-dir target-wsl` inside the distro.
Until that exists, three of the five arms of §4 are runnable — **D**, **I** and **C₂**, all Windows-hosted —
and neither of the two the operator's question is actually about. **W** is the question, and **C₁** is what
keeps its answer from being mislabelled as a statement about the operating system. Running the Windows three
early is still worth doing: they are the arms whose absolute numbers the WSL pair will be read against, and
measuring them now costs nothing and removes a variable later. What must not happen is publishing them as a
partial answer — a table with the WSL column empty reads as a result, and it is a prerequisite.

This is the same prerequisite that blocks `dew_flow_sidecar_rust · research/PLAN_reliability_tail.md` item 1 (the
MIGraphX cache-path race), so the two unblock together — and item 1 should be verified on the wire during the
same session the toolchain first works, because it is the only opportunity that costs nothing extra.

---

## 7. Build order

1. **The Windows-hosted arms (D · I · C₂)** — runnable today, and the baseline the rest is read against.
   Recorded, not published as an answer (§6).
2. **(prerequisite)** ONNX Runtime 1.24.x `--use_migraphx`, and the WSL sidecar built against it. Not this
   repository's work; named here because arms W and C₁ — the operator's actual question and its control —
   do not exist without it.
3. **Phase 1 completed (W · C₁)** — the operator's answer, published whole. No code here; a script and a
   discipline.
4. **Phase 2, the echo** — small, independent of the matrix, and useful the moment two arms exist.
5. **Phase 3, the axis** — after [PLAN_variant_matrix.md](PLAN_variant_matrix.md) step 4.

## 8. Test plan

| what | how |
|---|---|
| The echo is read, never assumed | An engine reporting a backend different from the arm's produces a **blocked** cell naming both — asserted, since a silently accepted mismatch is the whole defect. |
| *Not declared* is its own state | A third-party engine declaring nothing must not compare equal to a matching declaration. |
| Two arms are distinguishable | Two results identical but for the backend echo do not fold into one aggregate — the direct regression of §2b. |
| The probe's own arithmetic | Steady-state selection excludes any pass whose `compile_cache_grew_mb > 0`; a fixture with one compiling pass among ten must not silently raise the median. |
| Rerank actually reranked | The rerank arm asserts distinct, ordered scores, not HTTP 200 (§2, the 177 silent 500s). |

Phase 1 asserts nothing in code and must therefore say so in its write-up, per this family's honesty clause:
which numbers were observed, on which binaries by hash, and what could not be run.

## 9. Definition of Done

- [ ] Phase 1 published three separate quantities per arm (steady state · cold start · shape churn), each with
      its spread, never averaged into one headline.
- [ ] Every arm's flavour, card and binary are quoted from the serving process's `/health` and build hashes —
      not from the configuration that launched it.
- [ ] **No column, chart or sentence is labelled by host alone.** Host and EP are confounded by construction
      on this hardware (§1b); the CPU control pair is what any claim about the operating system rests on, and
      a report published without it says so.
- [ ] The controls of §3 are each either satisfied or recorded as unsatisfied with the effect on the reading.
- [ ] The finding is written into `research/MEASURED_LESSONS.md` so a later plan can cite it without re-running
      anything.
- [ ] Phase 2's echo blocks a mismatched cell with both backends named, and distinguishes *not declared* from
      *matched*.
- [ ] The plan is promoted to `research/` with its deviations recorded, and the *Currently open* table in
      [README.md](README.md) is updated in the same task.
