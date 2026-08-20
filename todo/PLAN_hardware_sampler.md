# PLAN — the machine a number came off, recorded so the number stays readable

> Status: **plan only, 2026-08-19 — nothing implemented.** Scope: `Bench.Domain/Trace` (the sample shapes and
> the machine fingerprint), `Bench.Application` (the sampler port, already declared, and a run-start probe),
> `Bench.Infrastructure` (the adapters — WMI, `nvidia-smi`/`rocm-smi`, `/proc`, the qln runtime read),
> `hosts/Cli`, one migration. Founding-plan step 7 (`PLAN_rag_bench_repo.md` §5.1), raised by the operator
> 2026-08-19.
>
> Related: [PLAN_compute_backend_axis.md](PLAN_compute_backend_axis.md) (the arm this describes the inside
> of), [../research/architecture.md](../research/architecture.md) (*Guards that shape the API* — the
> captured-or-zero rule this plan is an application of).

---

## 1. The symptom

`IHardwareSampler` is declared (`src/Bench.Application/Ports.cs:188`) and nothing implements it.
`HardwareSample` exists (`src/Bench.Domain/Trace/LegTrace.cs:133`) with four bare numbers — GPU utilisation,
VRAM bytes, CPU utilisation, disk bytes per second — and nothing produces one.

So every result row this benchmark has ever written describes a measurement whose machine is unrecorded.
That is not a cosmetic gap on this hardware, and three incidents already in this family's record say why:

- **A card that fell off the bus.** `ConfigManagerErrorCode = 31` leaves the adapter enumerated while
  everything silently runs on the CPU. The only symptom is unexplained slowness; the local runtime logs
  `id=cpu` and says nothing, and a `size_vram` of 0 proves nothing either. A campaign run in that state
  produces real numbers that are CPU numbers wearing a GPU's label.
- **A card somebody else was holding.** Concurrent index passes co-loaded a coder and an embedder — 30 GB on
  a 32 GB card. A leg's latency is a function of what else was resident, and a bare "VRAM used" cannot tell
  *we used 20 GB* from *somebody else held 20 GB and we got the rest*.
- **A disk that filled.** 24.38 GB of Qdrant of which 22 GB was leaked corpora — reported as "no space"
  during an unrelated run ([PLAN_corpus_litter.md](PLAN_corpus_litter.md)).

## 2. What already exists, verified — most of the reading is written

| Fact | Where | Note |
|---|---|---|
| Adapter name, true VRAM size, **health code** | `dew_flow_rag_qln · src/Rag.Infrastructure/Gpu/GpuProbe.cs` | WMI `Win32_VideoController` plus the display-class registry key, because WMI's `AdapterRAM` is a uint32 and saturates at 4 GiB — a 32 GB card and a 4 GB integrated one both report "4 GB". Linux asks `nvidia-smi`. **No driver version yet.** |
| Resident models and their VRAM, per sidecar and per Ollama | `RuntimeInspector` → `RuntimeStatusVm` | This is the "who else is holding the card" read, already built |
| The arm — route/provider/device | `ComputeArm`, `/index-state` | [PLAN_compute_backend_axis.md](PLAN_compute_backend_axis.md); this plan describes what is INSIDE that arm |
| VRAM attribution as a DECISION | `dew_flow_sidecar_rust · src/vram.rs` | A figure is published only when the build was alone. Not a subtraction — the same rule this plan must follow |
| Captured-or-zero | `Captured` / `CapturedCount`, `src/Bench.Domain/Trace/LegTrace.cs:9` | "Not sampled" and "sampled zero" are different states, and `HardwareSample`'s bare doubles do not carry that |

## 3. The shape — decisions

### 3.1 STATIC belongs to the run; DYNAMIC belongs to the leg

The operator's list mixes two different lifetimes, and storing them in one place would make both useless.

A campaign is ten thousand cells over hours or days. A machine-wide VRAM min/max across all of it answers
nothing: the question is always *what did the card look like while THIS leg ran*. Conversely the driver
version does not change between legs, and writing it ten thousand times is a column nobody can index.

| | On the RUN, once | On the LEG, sampled |
|---|---|---|
| operating system and its build (`Windows 11 26200`, `WSL2 Ubuntu-26.04 / 6.14.0-…-WSL2`) | ✓ | |
| GPU model, **driver version**, **health code**, total VRAM | ✓ | |
| CPU model, physical cores, power plan / governor | ✓ | |
| total RAM | ✓ | |
| disk of the checkout / corpus: device, filesystem, **cluster size**, free space at start | ✓ | |
| machine identity — hostname + stable machine id | ✓ | |
| VRAM used — min · max · mean · **sample count** | | ✓ |
| RAM used — same four | | ✓ |
| GPU utilisation, CPU utilisation, disk throughput | | ✓ |
| **what else held the card** at leg start (resident models, other sidecars) | | ✓ |
| **throttling** — did the GPU hit a power or thermal limit during this leg | | ✓ |
| **concurrency witness** — was another bench run or index pass live | | ✓ |

### 3.2 Four numbers, never one: min · max · mean · COUNT

`HardwareSample` today carries a value. A summary needs the spread and the number of readings behind it, for
exactly the reason `MetricByDimension.Legs` exists: a maximum over two samples and a maximum over two
thousand are different claims, and a report that cannot tell them apart will rank on the first.

### 3.3 Not sampled is not zero — the rule the existing type breaks

`HardwareSample`'s `double GpuUtilisationPercent` cannot say *nobody read this*. On a machine with no
`nvidia-smi` and a non-Windows host that is the normal case, and rendering it as `0 %` is the defect
`Captured` was introduced to prevent, in the one place it was not applied. Every dynamic field becomes a
`CapturedCount` or its floating-point sibling.

### 3.4 Attribution is a decision, not a subtraction

The sidecar already learned this and wrote it down: *"the obvious answer — sample before and after, publish
the delta — rests on nothing"*, so it publishes a VRAM figure only when the build was alone. The benchmark
inherits the rule. A leg's VRAM reading is **attributed** when this process was the only claimant of the
accelerator for the whole leg — which is knowable, because the accelerator lease
([PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.4b) is what serialises them — and **observed only**
otherwise. Two states, stored separately, never averaged together.

### 3.5 The static facts are a FINGERPRINT, and a report says when it differs

Hash the run's static block into a `machine fingerprint`. Two runs with different fingerprints are not
refused — hardware changes, and refusing would make a benchmark unable to span a driver update — but a
report that puts them side by side **says so**, the same three-state honesty the index commit already uses:
same machine · different machine · not recorded.

This closes a gap nothing currently covers: no result row records which machine produced it, so two
machines' rows merge silently today.

### 3.6 The sampler must not become the thing it measures

WMI is a process launch; `nvidia-smi` is another. A per-leg sample at a naive cadence would add seconds to
every leg and change the number it is sampling. So: one static probe per RUN, a background sampler at a
configured cadence (default 2 s) that drains into the leg it overlapped, and a hard rule that a sampler
failure never fails a leg — it makes the reading *not captured*, which is a state the shapes above can hold.

### 3.7 What this plan deliberately does not do

- **No accelerator lease.** It is [PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.4b's, and §3.4 above
  depends on it rather than duplicating it.
- **No per-leg power draw in watts.** Available from both vendors, and no question here needs it yet.
- **No throttling remedy.** Recording that a leg throttled is the deliverable; deciding what to do about it
  is a measurement somebody has to design.

## 4. Build order

1. **The shapes** — `MachineFacts` (static, hashed), `SampleSummary` (min/max/mean/count, captured-aware),
   `Attribution` on the VRAM reading. Domain only, no IO, fully testable.
2. **The static probe** — one read per run, per platform. Windows: WMI + registry + `powercfg`; Linux/WSL:
   `/proc/cpuinfo`, `/proc/meminfo`, `/proc/sys/kernel/osrelease`, `findmnt`, the governor. Reuses
   `GpuProbe`'s script rather than writing a second one — that decision is §5's open question.
3. **The migration** — `run_machine` (one row per run) and the leg summary columns.
4. **The background sampler** — cadence, drain-into-leg, failure-is-not-captured.
5. **The report** — the fingerprint comparison of §3.5, and the dynamic summaries beside the metric.

Steps 1–3 are useful alone: a run that records its machine and samples nothing is already better than today.

## 5. Test plan

| What | How |
|---|---|
| Not sampled is not zero | `A_machine_nobody_could_read_reports_UNKNOWN_rather_than_a_zero_percent_GPU` |
| A card off the bus is loud | `A_run_whose_adapter_reports_error_31_records_it_and_the_report_says_so` — the CPU-numbers-wearing-a-GPU-label case |
| Four numbers, not one | `A_maximum_over_two_samples_and_over_two_thousand_are_different_claims` |
| Attribution is not subtraction | `A_leg_that_shared_the_card_records_its_VRAM_as_observed_rather_than_attributed` |
| The fingerprint is compared, not enforced | `Two_runs_on_different_drivers_are_reported_side_by_side_with_the_difference_named` |
| A sampler failure is not a leg failure | `A_probe_that_throws_leaves_the_leg_scored_and_the_reading_uncaptured` |
| The cadence is a budget | `A_sampler_that_cannot_keep_its_cadence_says_so_rather_than_slowing_the_leg` |

## 6. Definition of Done

- [ ] Build 0 warnings; the suite green; every row of §5 has a named test.
- [ ] A run records its machine once, and a leg records what the card looked like while it ran.
- [ ] No dynamic field can express "unknown" as a zero.
- [ ] A VRAM figure taken while something else held the card is stored as *observed*, never as *attributed*.
- [ ] `bench report` names a fingerprint difference between the runs it compares.
- [ ] Nothing here can fail a leg.

## 7. Open questions

1. **Where does the static probe live — here or in qln?** `GpuProbe` already reads the adapter, its true
   VRAM and its health code, and a second implementation in this repository is exactly the duplicate
   `reuse-first` refuses. But this benchmark measures engines it did not write, and depending on qln for its
   own machine facts would make it unable to measure anything else. **Proposal: the SHAPES and the rules live
   here; the Windows/Linux readers are ported — mechanism, not repository — with their provenance named in
   the comment, the way `PLAN_scoremeter_port.md` ports its cleaned-LOC family.**
2. **Driver version on Linux/WSL.** Windows gives it from the same WMI class `GpuProbe` already queries. Under
   WSL the GPU is passthrough and the driver is the WINDOWS one — `nvidia-smi` inside the distro reports the
   host driver, but ROCm's story on this card is unverified. Needs one probe before the field is promised.
3. **Cadence default.** 2 s is a guess. It should be measured against its own cost on this machine before it
   becomes a number in a config file nobody revisits.
