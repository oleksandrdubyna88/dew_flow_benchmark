# PLAN — the compute backend becomes an axis, and its first question is WSL against Windows on one card

> Status: **PHASE 2 IMPLEMENTED 2026-08-19 on this side; phase 1 is two arms of five and phase 3 is open.**
> The backend is a value (`ComputeBackend`), a three-state declaration (`BackendDeclaration`), part of the
> engine identity (`EngineRef.Canonical` — the §2b fix), an optional axis on a recipe, a rule that blocks a
> mismatched cell before any cell exists (`IndexReadiness`), a flag that allows what it cannot verify and
> keeps saying so (`--allow-undeclared-backend`), and a stored column (`runs.EngineBackend`).
>
> ~~What remains is **not this repository's**: qln must SEND the field.~~ **It does, since 2026-08-20** —
> `dew_flow_rag_qln`'s `/index-state` composes the arm with `ComputeArm.Of(...)` and serialises it as
> `backend`, which is exactly the property `QlnRetriever` already reads. The contract connects end to end;
> the blocking dependency this plan was waiting on is closed, and what an engine declares is now a question
> about the machine rather than about a missing field.
>
> **Read the arm's own rule before trusting a blank**, because an empty value is still common and still
> means *not declared*: qln sends nothing unless all three parts are known and the whole sidecar fleet
> agrees. Three corrections on that side (2026-08-20/21) were each a value that named hardware it could not
> vouch for — the sidecar's `auto` REQUEST published as the active provider on the default configuration,
> the console badging it green, and a CPU arm naming the idle card DXGI happens to resolve. So a run
> recorded before 2026-08-21 that declares `windows/auto/…` is naming a provider nobody established.
>
> Scope: a MEASUREMENT first (phase 1 needs no code in this repository), then the engine's backend echo in
> `Bench.Domain`/`Bench.Contracts`, then the run matrix.
>
> **The toolchain prerequisite of §6 is SATISFIED** (verified 2026-08-19): ONNX Runtime 1.24.4 built
> `--use_migraphx` has been installed at `/opt/onnxruntime-migraphx/lib/` since 2026-07-27, and
> `dew_flow_sidecar_rust · target-wsl/release/bge-sidecar` was built with the `migraphx` feature on
> 2026-08-18. The claim that the WSL arms cannot be launched is this document reading its own history as its
> present.
>
> **And phase 1 is half-run — outside this harness.** Arms **D** and **W** were measured on 2026-08-18 and
> written up in `dew_flow_rag_qln · research/GPU_BACKEND_WSL_VS_WINDOWS.md`, using the instrument §4
> prescribes (`PassTimings` off the `/embed` response, never a client stopwatch) and this plan's own arm
> naming. Its answer: **per token MIGraphX is ~1.5× faster, per pass the two tie, and what reconciles them is
> our own `ChunkBatcher`** — the sort is worth ~33 % to DirectML and nothing to MIGraphX, which pins its input
> shape. Arms **I**, **C₁** and **C₂** were not run, and C₁/C₂ are the only evidence about the operating
> system itself (§3), so **phase 1 is not published as an answer** — per §6's own rule, a table with the
> control column empty reads as a result.
>
> **Phase 3's blocker is also gone:** `PLAN_variant_matrix.md` step 4 landed 2026-08-17, so an engine is wired
> into a leg. What still blocks phase 3 is phase 2.
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

**So the deliverable is these quantities reported side by side per arm, never averaged into one.** That
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

So today the arms of §4 would be stored as rows no report can tell apart, and a sweep that merged them would
produce a mean over several different pieces of hardware — and over two operating systems. That is the same defect
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

- **One at a time on the card.** W and D target the same physical R9700 and I shares the machine with it; run
  any two concurrently and every number is a contention measurement. The CPU control pair is not exempt — it
  competes for the same cores an EP arm needs to feed the card. The accelerator lease ([PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md)
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
  MIGraphX column. `PIN_INPUT_SHAPE=0` under MIGraphX is a legitimate arm of its own — **W′** in §4's naming, labelled as such —
  never a fairness correction applied to W.
- **Warm-up is excluded from A and reported as B.** It is not noise to be discarded; on the MIGraphX arm it is
  the most expensive thing in the measurement and the single number most likely to decide the deployment.
- **A cold start means an EMPTY compiled-model cache, and each arm gets its own.** `ORT_MIGRAPHX_MODEL_CACHE_PATH`
  pointed at a directory a previous arm filled turns measurement **B** from "how long does a compile take"
  into "how long does a cache load take" — the same number the field reports either way, differing by two
  orders of magnitude. Give every arm its own directory, wipe it before any B measurement, and record the
  directory's size afterwards: that size **is** the per-shape cache cost, and it is half of what the operator
  is choosing between.
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
**W′** `wsl/migraphx/R9700, PIN_INPUT_SHAPE=0` is optional and belongs to §3's pinning control — it prices
the workaround, and it is never a substitute for W.

### 4a. The modes that exist — four independent layers, and only some are per-arm

Written out because three of the four have a way of being set to something that quietly measures the wrong
thing, and because a probe run twice from memory is a probe run twice differently.

| layer | choices | selected by | the trap |
|---|---|---|---|
| **build flavour** *(compile-time)* | `dml` (default) · `cuda` · `migraphx` · none = CPU-only | `cargo` features | An EP that is not compiled in cannot be chosen at runtime. It is refused **by name** with a rebuild instruction (`preflight_provider`), which is the one failure here that cannot be mistaken for slowness |
| **runtime provider** | `auto` \| `cuda` \| `dml` \| `migraphx` \| `cpu` | `ORT_PROVIDER` | Empty ⇒ the first request's hint, else `auto` (cuda → migraphx → dml → cpu). **Always set it explicitly for a measurement** — an explicit choice fails hard, `auto` silently ranks |
| **which card** | — | **Windows/DirectML:** `ORT_DEVICE_ID`, DXGI **high-performance order** (0 = fastest), translated internally to the plain-enumeration index the legacy DML EP wants. **WSL/ROCm:** `HIP_VISIBLE_DEVICES` on the launch line — `ORT_DEVICE_ID` does **not** select the card there | the single most common way to measure the wrong card. Two sidecars once landed on card 0 together because one configured value was baked into both launch lines |
| **shape pinning** | `auto` (⇒ on for `migraphx` only) \| `1` \| `0` | `PIN_INPUT_SHAPE` | leave it `auto` on every arm but W′ — §3 |

The envelope rides on top and is **per request**, not only per launch: `max_batch` and `max_length` on the
`/embed` body override `MAX_BATCH` / `EMBED_MAX_LENGTH`. So the launch values only decide the window before
the first call — the probe states the envelope in the payload and reads back what `/health` says it ran.
`EMBED_ENGINE_CACHE_RUNGS` (default **1**) decides whether crossing an envelope boundary evicts;
`ORT_THREADS` matters only on the CPU arms.

**MIGraphX-only, and all three are mandatory rather than optimisations:** `ORT_DYLIB_PATH` (the flavour is
`load-dynamic` — nothing is linked), `ORT_MIGRAPHX_MODEL_CACHE_PATH` (unset ⇒ the EP saves to `""`, the write
fails and takes the kernel call with it — every `/embed` answered 500 after a two-minute compile the day this
was diagnosed), and `MODEL_CACHE_DIR` on ext4 with `MODEL_CACHE_SEED_DIR` pointing back at the repo copy —
DrvFs reads the 2.27 GB of weights at ~123 MB/s, about 19 s of every session build.

**The trap that catches C₁.** Both startup preflights are compiled in by the `migraphx` **feature**, not by
the chosen provider, and both `std::process::exit(1)` on failure (`dew_flow_sidecar_rust · src/main.rs:121-124`).
So the WSL **CPU** arm — which touches neither ROCm nor the cache — still refuses to start without
`ORT_DYLIB_PATH` and `ORT_MIGRAPHX_MODEL_CACHE_PATH` set. That is correct behaviour for the deployed flavour
and a surprise for a control arm; set them and move on.

### 4b. How each arm is launched

**Two builds cover all six arms** — `cpu` needs no flavour of its own, because ort falls through to CPU when
no EP is registered and `compiled_providers` therefore always lists it:

```bash
cargo build --release                                    # D · I · C₂  (dml is the default feature)
# inside the distro, separate target dir so the two OS flavours never clobber each other:
cargo build --release --no-default-features --features migraphx --target-dir target-wsl   # W · W′ · C₁
```

```powershell
# D — windows/dml/R9700 ; 0 = fastest card in DXGI high-performance order
$env:PORT='5321'; $env:ORT_PROVIDER='dml'; $env:ORT_DEVICE_ID='0'
.\target\release\bge-sidecar.exe

# I — windows/dml/890M
$env:PORT='5322'; $env:ORT_PROVIDER='dml'; $env:ORT_DEVICE_ID='1'; .\target\release\bge-sidecar.exe

# C2 — windows/cpu ; same binary, no EP registered
$env:PORT='5323'; $env:ORT_PROVIDER='cpu'; $env:ORT_THREADS='0'; .\target\release\bge-sidecar.exe
```

```bash
# W — wsl/migraphx/R9700 ; HIP_VISIBLE_DEVICES is what picks the card, ORT_DEVICE_ID is not
SIDE=/mnt/d/rsd/dew_flow_sidecar_rust
HIP_VISIBLE_DEVICES=0 ORT_PROVIDER=migraphx PORT=5324 \
ORT_DYLIB_PATH=/opt/onnxruntime-migraphx/lib/libonnxruntime.so \
ORT_MIGRAPHX_MODEL_CACHE_PATH=$HOME/.cache/bench-W ORT_MIGRAPHX_CACHE_PATH=$HOME/.cache/bench-W \
MODEL_CACHE_DIR=$HOME/.cache/bge-sidecar-models MODEL_CACHE_SEED_DIR=$SIDE/.model-cache \
exec ./target-wsl/release/bge-sidecar

# W' — the same, plus PIN_INPUT_SHAPE=0 and its OWN cache dir (bench-Wp)
# C1 — wsl/cpu ; ORT_PROVIDER=cpu, but the two MIGraphX env vars are still required to start (4a)
```

**Gate zero, before any number is recorded.** `GET /health` on each arm must report the card it was supposed
to get: `active_provider` matching the arm, and `adapter` naming the right one. On this box the DXGI mapping
is the **reverse** of Windows' PnP listing — requested `0` → *AMD Radeon AI PRO R9700*, requested `1` →
*AMD Radeon(TM) 890M Graphics* — so verify, never assume. An arm that fails gate zero is not re-labelled; it
is fixed or dropped.

### 4c. The payload, stated so two people run the same probe

- **Corpus:** the sidecar repository's own `src/**/*.rs`, split into ~500-character chunks, sorted by path
  and taken from the top. No randomness, no external download, and the same bytes visible to both hosts (the
  WSL side reads it over `/mnt`). The absolute numbers are not the point; identity across arms is.
- **Envelope:** `max_batch = 64`, `max_length = 256` — the shipped defaults, which mirror the host's own
  (`SidecarMemory.DefaultMaxLength`). Sent **in the request body**, not only as launch env, and read back
  from `/health`.
- Every step below is preceded by a cache wipe where §3 requires one, and repeated ≥ 3 times.

**The probe:** for each arm, one at a time on the card —

1. **Cold start (B)** — empty cache, one call of 64 texts. Record `session_build_ms`,
   `compile_cache_grew_mb` and total wall separately; then record the cache directory's size on disk. On W
   this is the 2–4 minutes and the ~2.5 GB; on D it is a session build of tens of seconds and no cache at all.
2. **Steady state (A)** — 10 calls of 64 texts at the same envelope. Median and spread of `inference_ms`, and
   `compile_cache_grew_mb == 0` asserted throughout: a non-zero value means this is not steady state and the
   number measures a compile.
3. **Row churn (B′)** — 10 calls whose row counts vary (8, 61, 64, 17, 40, …) at the SAME envelope. This does
   **not** trigger a recompile on W, and that is the finding rather than a flaw: `pin_shape` absorbs the
   variation into ruler padding, so W pays a full 64-row batch for an 8-text call while D pays only for what
   it was sent. It prices the workaround. `compile_cache_grew_mb` must stay 0 on W — if it does not, pinning
   is not doing its job and the arm is misconfigured.
4. **Envelope change (B″)** — the actual recompile trigger, and the one closest to a real Fast pass, which
   crosses the boundary twice: alternate `max_length` between 256 and 1024 across four calls. On W each new
   envelope is a fresh compile and a fresh ~2.5 GB; `EMBED_ENGINE_CACHE_RUNGS` decides whether returning to
   the first one pays again. Run it at the shipped **1** and note what **2** would change rather than
   measuring both, unless the numbers make it interesting.
5. **Rerank, separately** — never folded into the embed numbers, and gated: the arm must return distinct,
   ordered scores, for the reason recorded in §2. This is where the existing 3.06 s / 45–75 s figure is
   reproduced or contradicted.

**Which step answers which question of §1.** A ← step 2, with step 3 as the realistic-input version of it.
B ← step 1, plus step 4 for the envelope boundary a real pass actually crosses. **C is not measured here and
must not be claimed**: its dominant term is a driver timeout that removes the D3D12 device, which has no mean
and cannot be provoked on demand — the honest treatment is to report it as a hazard of arm D with its
observed frequency (three in an hour) beside the timings, not to fold it into one.

Output: a table under `research/`, plus an entry in
[../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) — the lessons file is where a finding goes
that later plans should be able to cite without re-running anything.

**This phase produces the operator's answer.** Phases 2 and 3 exist so the answer keeps being produced
automatically, and so the *next* backend question (iGPU against discrete, CPU EP, CUDA on other hardware) does
not need a hand-run probe of its own.

---

## 5. Phase 2 — the backend echo · Phase 3 — the axis

**Phase 2 (this repository, small).** The type is `EngineRef` (`src/Bench.Domain/Runs/Axes.cs:104` — the plan
said `EngineDescriptor`, which does not exist; corrected 2026-08-19). It gains a `ComputeBackend` echo — host
(`windows` | `wsl` | `linux`), provider (`migraphx` | `dml` | `cuda` | `cpu`), device, adapter name, and the
serving binary's hash — populated from what the engine REPORTS, never from what the run requested. Three
states, not two, copying [PLAN_corpus_axis_integrity.md](PLAN_corpus_axis_integrity.md): **matched ·
mismatched · not declared**. An arm that names a backend and gets a different one back is **blocked with both
named**; an engine that declares nothing is *not declared*, which is honest for a third-party engine and must
not read as agreement.

### 5a. Phase 2, made buildable (2026-08-19)

There is no need to invent the shape: this repository already solved the same problem one field over, and
phase 2 is that solution applied to a second axis.

**The template is `CorpusIdentity` + `IndexCommit`** (`src/Bench.Domain/Retrieval/IndexState.cs`). `CorpusIdentity.Refuse(recipe)`
is a comparison returning *why this is not the recipe's, or empty when it is*, with a normalisation rule whose
comment states which direction of error is unacceptable (a false accept, because it is indistinguishable from
a correct measurement afterwards). `IndexCommit` is the three-state closed hierarchy — `At(sha)` |
`Unstamped` — written precisely because *"not 'a different commit': nothing is known"*, refused by default and
passable with `--allow-unstamped-index` that keeps printing the warning. Phase 2 needs both of those shapes and
neither of them is new work to design.

**What lands:**

1. `src/Bench.Domain/Retrieval/ComputeBackend.cs` — `ComputeBackend(Host, Provider, Device, Adapter, BinaryHash)`
   with `Parse` returning `Outcome<T>` (never a throwing constructor, CLAUDE.md §3) and a canonical
   `host/provider/device` per §1b, so an arm's name in a report is the arm's name in this plan. Plus
   `BackendDeclaration` = `NotDeclared` | `Declared(ComputeBackend)`, the `IndexCommit` shape.
2. `EngineRef` carries the declaration, and **`Canonical` includes it** — that single line is the §2b
   structural fix: today `Kind|Endpoint|Version|IndexFingerprint` makes two arms one row.
3. `VariantDefinition.RetrievalRecipe` gains an optional declared backend. Optional is load-bearing: every
   variant that exists today declares none, and they must keep running unchanged.
4. The rule, four rows, and the third is the whole point:

   | recipe declares | engine declares | verdict |
   |---|---|---|
   | nothing | anything | runs, and the engine's value is **recorded** — so a report can group by an axis nobody planned |
   | a backend | the same one | runs |
   | a backend | a different one | **cell blocked, both values named** (`LegRunner.BlockAsync`, the path `EngineAxes.AssertAppliedIn` already takes) |
   | a backend | nothing | refused by default; `--allow-undeclared-backend` proceeds and keeps printing that the arm is UNVERIFIED — the `--allow-unstamped-index` / `--no-checkout` precedent |

5. One migration for the echoed value on the funnel row. **Not in the same task as another session's
   migration** — two migrations against one `BenchDbContextModelSnapshot` conflict by construction.

**The producer half is cheaper than it looks, and it is not in this repository.** `IndexStateWire`
(`src/Bench.Infrastructure/Engines/QlnRetriever.cs:560-588`) has no such field, so qln must send one — but qln
already holds the value: `dew_flow_rag_qln · src/Rag.Infrastructure/Runtime/RuntimeInspector.cs:142-144` reads
`active_provider` and `compiled_providers` off the sidecar's `/health`, and the host and device are properties
of the sidecar URL it was configured with. The addition there is a field on an existing response, not a new
capability.

**Until that half lands, everything here reads *not declared*.** That is the design working, not the design
waiting: a run against an engine that says nothing about its backend must record *nothing known* rather than
agreement, and every engine on earth is in that state today.

**Phase 3 (unblocked as of 2026-08-17 — `PLAN_variant_matrix` step 4 landed; now gated on phase 2 only).** The
backend becomes a planned axis: one leg per `(subject × backend × repeat)`, so the comparison is a run rather
than an afternoon. It may not land before phase 2, for the reason §2b gives: planning legs along an axis no
result row can distinguish produces a comparison of nothing.

---

## 6. The prerequisite that blocked phase 1's WSL half — SATISFIED, and what it cost to find out

> **Resolved.** This section is kept as history because its reasoning is still the reason the preflight exists,
> and because the plan carried it as a live blocker for three days after it had stopped being one. Rewritten
> 2026-08-19 with the evidence that closes it.

**What it was.** The `migraphx` flavour loads a machine-local `libonnxruntime.so` at runtime, and `ort` rc.12
requires ONNX Runtime ≥ 1.24; the build then present under `/opt` was 1.23.2, whose mismatch path *deadlocks*
inside `ort`'s own version check rather than erroring — which is why the sidecar preflights the dylib through
the stable C ABI and exits with the required version instead (`dew_flow_sidecar_rust · src/preflight.rs:11-21`
and the verdict at `:184-198`). That preflight stays: it is the reason the failure is now a message rather than
a hang.

**Why it is closed.** Verified 2026-08-19:

- `/opt/onnxruntime-migraphx/lib/` holds `libonnxruntime.so.1.24.4` and
  `libonnxruntime_providers_migraphx.so`, built from source with `--use_migraphx` on **2026-07-27**, with the
  old 1.23.2 kept beside them as `.bak`. ROCm is at `/opt/rocm`; the compiled-model cache survives at
  `~/.cache/bge-sidecar-migraphx/device-0`.
- `dew_flow_sidecar_rust · target-wsl/release/bge-sidecar` exists, built **2026-08-18**, and it is genuinely
  the migraphx flavour: `libloading` is in its dependency graph, and that crate enters the build through the
  `migraphx` feature and no other.
- Arms **D** and **W** were then actually measured — `dew_flow_rag_qln · research/GPU_BACKEND_WSL_VS_WINDOWS.md`,
  2026-08-18. A blocker that has been run past is not a blocker.

**The lesson, which is the reason this section was not simply deleted.** The toolchain was fixed on 2026-07-27
and this plan was written on 2026-08-16 declaring it the thing that blocks the operator's own question. Nothing
was wrong with the reasoning; the premise had expired. Its cost was measurable in the shape of the document —
the arms this plan calls impossible had already been run by the time anyone re-read it, and the numbers went
into a *different repository's* measurement log because this one said they could not be taken.

`dew_flow_sidecar_rust · research/PLAN_reliability_tail.md` item 1 (the MIGraphX cache-path race) shipped
independently — that repository's plan is `IMPLEMENTED, 2026-08-16` with all eight items closed.

**What is genuinely outstanding in phase 1** is therefore not a toolchain but three arms: **I** (the integrated
card) and **C₁ · C₂** (the CPU control pair, the only evidence about the operating system itself, §3). Until
they are run, §4's table has an empty control column and must not be published as an answer — the rule this
section always carried, now applied to a different empty column than the one it was written about.

---

## 7. Build order

Revised 2026-08-19: steps 2 and 3 of the original order are done or partly done, and what is left is not in
the order the plan first put it.

1. ~~**The prerequisite** — ONNX Runtime 1.24.x `--use_migraphx` and the WSL sidecar built against it.~~
   **DONE** 2026-07-27 / 2026-08-18 (§6).
2. ~~**Arms D and W**~~ — **MEASURED** 2026-08-18, `dew_flow_rag_qln · research/GPU_BACKEND_WSL_VS_WINDOWS.md`.
   Recorded, not published as an answer, because of step 3.
3. **The three remaining arms — I · C₁ · C₂.** No code here; a script and a discipline. C₁/C₂ are what keep
   the answer from being mislabelled as a statement about the operating system, so phase 1 publishes only
   after them.
4. ~~**Phase 2, the echo** (§5a).~~ **IMPLEMENTED 2026-08-19**, in three slices, each with its RED test
   watched failing first. Deviations, all argued in the code:
   - **`Parse` is structural, not an allow-list.** Refusing `macos/coreml/M3` would record a real
     declaration as *nothing known* — a claim about the engine rather than about this build's vocabulary,
     in a benchmark whose premise is any engine. A typo in a recipe is caught better by the mismatch, which
     names both values.
   - **`EngineRef.Backend` and `IndexState.Backend` are `init` members, not positional parameters.** A
     positional default must be a compile-time constant, so the alternative was a nullable — a null in the
     domain, which this project refuses. A test caught that on the first attempt.
   - **`IndexReadiness.Of` takes the whole RECIPE**, not just its corpus. Two of the three things it checks
     are not corpus properties. Its refusals are ORDERED — corpus, arm, commit — because an operator acts
     on the first line they read.
   - **`ReadinessAllowances` replaced the loose `bool`.** Each allowance is a promise the run then keeps
     repeating, and two allowances are owed two sentences.
   - **The catalog carries the axis** (`RetrievalRecipe.On`, `DefinitionWire.Backend`), pinned literally so
     a recipe naming no arm hashes exactly as before and stores no field at all — an added axis must
     relabel no number already measured.
   - **A stored arm this build cannot read is REFUSED**, the opposite of how an engine's echo is read: one
     is a configuration somebody wrote down, the other is a fact about the engine.
   - The migration waited one slice for another session's `LaneCatalog` to land, exactly as §5a item 5 says.
5. **Phase 2's producer half** in `dew_flow_rag_qln` — one field on an existing response (§5a). Everything
   here reads *not declared* until it lands, which is correct rather than blocked. **This is now the only
   thing between the arms measured on 2026-08-18 and a result row that can hold them.**
6. **Phase 3, the axis** — gated on step 4 only; `PLAN_variant_matrix.md` step 4 landed 2026-08-17.

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

- [ ] Phase 1 published its measurements per arm separately — cold start · steady state · row churn ·
      envelope change · rerank — each with its spread, never averaged into one headline, and **C reported as
      a hazard rather than as a number** (§4c).
- [ ] Every arm names its build (`cargo` line), its launch (env, verbatim) and its cache directory, so the
      probe can be re-run without reconstructing it from a summary.
- [ ] **Gate zero passed on every arm**: `/health` reported the intended `active_provider` and `adapter`
      before any timing was recorded. An arm that failed it was fixed or dropped, never re-labelled.
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
