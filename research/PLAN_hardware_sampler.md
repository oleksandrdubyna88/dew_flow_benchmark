# PLAN — the machine a number came off, recorded so the number stays readable

> Status: **IMPLEMENTED, 2026-08-23.** All five steps shipped (1–3 on 2026-08-19/20, 4–5 with them, the
> fingerprint comparison on 2026-08-23): a run records the machine it measured on, each leg records what that
> machine was doing while it ran, the report carries both, and a comparison that folds runs from more than one
> machine now says so. Verified end to end against the real database on 2026-08-20 and re-verified by suite on
> 2026-08-23.
>
> **The fingerprint comparison — the item this plan called blocked, and was not.** The status line above used
> to read *"needs cross-run comparison, which `bench report` deliberately does not do"*. That was true when it
> was written and false within hours: `ArmComparison` — cross-run by construction — shipped the same day
> ([PLAN_bench_console.md](PLAN_bench_console.md) step 6), and nobody re-read this plan against it. The
> comparison folded runs into one average per arm while reading no machine at all, which is exactly the silent
> merge §3.5 exists to prevent, one level up from where it was being guarded. The test double left behind said
> so in as many words — *"a comparison does not read machines yet — see the plan's open tail"*. Closed
> 2026-08-23: `MachineAgreement` (`src/Bench.Domain/Trace/MachineAgreement.cs`) folds a population of
> `MachineFacts` into four states, `IRunStore.MachinesAsync` reads them for a whole comparison in one query,
> and both the scope and each individual arm carry their own reading into the contract and onto the console.
>
> **Four states rather than §3.5's three, and the fourth is the one a real database produces.** *Same machine ·
> different machine · not recorded* leaves nowhere to put a comparison where SOME runs were probed and some
> predate the probe — the ordinary state here. Folding it into *same machine* would let one probed run vouch
> for a population nobody read, so `PartlyRecorded` is its own state. `SeveralMachines` is reported and never
> refused, exactly as §3.5 required: a benchmark unable to span a hardware change would be useless.
>
> Two things remain, and neither is a gap in this plan's own steps — both are owned elsewhere and named here
> so they are not re-derived:
>
> - **VRAM is unreachable on Linux/WSL** (PDH is Windows-only and the ROCm path is unverified), and on
>   Windows a read costs about a second, so a leg shorter than the slow cadence catches none. Per-leg VRAM is
>   therefore meaningful only for legs measured in MINUTES — index passes, code-lane tasks — and no tuning
>   fixes that. A cheaper source is DXGI through P/Invoke, or asking the engine, which answers a different
>   question.
> - **`VramAttribution.Attributed` is unreachable** until the accelerator lease exists
>   ([PLAN_variant_matrix.md](../todo/PLAN_variant_matrix.md) §3.4b). Every figure is `Observed`, which is
>   correct rather than pending: nothing can currently prove a leg held the card alone.
>
> Originally: Scope: `Bench.Domain/Trace` (the sample shapes and
> the machine fingerprint), `Bench.Application` (the sampler port, already declared, and a run-start probe),
> `Bench.Infrastructure` (the adapters — WMI, `nvidia-smi`/`rocm-smi`, `/proc`, the qln runtime read),
> `hosts/Cli`, one migration. Founding-plan step 7 (`PLAN_rag_bench_repo.md` §5.1), raised by the operator
> 2026-08-19.
>
> Related: [PLAN_compute_backend_axis.md](../todo/PLAN_compute_backend_axis.md) (the arm this describes the inside
> of), [architecture.md](architecture.md) (*Guards that shape the API* — the
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
  during an unrelated run ([PLAN_corpus_litter.md](../todo/PLAN_corpus_litter.md)).

## 2. What already exists, verified — most of the reading is written

| Fact | Where | Note |
|---|---|---|
| Adapter name, true VRAM size, **health code** | `dew_flow_rag_qln · src/Rag.Infrastructure/Gpu/GpuProbe.cs` | WMI `Win32_VideoController` plus the display-class registry key, because WMI's `AdapterRAM` is a uint32 and saturates at 4 GiB — a 32 GB card and a 4 GB integrated one both report "4 GB". Linux asks `nvidia-smi`. **No driver version yet.** |
| Resident models and their VRAM, per sidecar and per Ollama | `RuntimeInspector` → `RuntimeStatusVm` | This is the "who else is holding the card" read, already built |
| The arm — route/provider/device | `ComputeArm`, `/index-state` | [PLAN_compute_backend_axis.md](../todo/PLAN_compute_backend_axis.md); this plan describes what is INSIDE that arm |
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
| the operating system stack — **five independent versions under WSL**, see §3.1a | ✓ | |
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

### 3.1a "The OS version" is five versions, and two of the obvious reads are wrong

Probed on this machine 2026-08-19, so the shape below is what is actually available rather than what a field
list would guess. Under WSL the layers version **independently of each other**: Windows patches on its own
cadence, the WSL runtime updates from the Store, the distro is upgraded by the operator, and the kernel moves
with the WSL package. Recording one of them and calling it "the OS" would attribute a regression to whichever
layer happened to be named.

| Axis | Where it is read | Value here |
|---|---|---|
| Windows edition + release | `HKLM\…\CurrentVersion` → `DisplayVersion`, `EditionID` | `25H2`, `Professional` |
| Windows build + **patch** | `CurrentBuild` + **`UBR`** | `26200` + **`8653`** → `10.0.26200.8653` |
| WSL runtime, WSLg, MSRDC, **Direct3D**, **DXCore** | `wsl.exe --version`, one call | `2.7.10.0` · `1.0.73.2` · `1.2.6676` · **`1.611.1-81528511`** · **`10.0.26100.1`** |
| distro | `/etc/os-release` → `VERSION_ID`, `VERSION_CODENAME` | `26.04`, `resolute` |
| kernel | `/proc/sys/kernel/osrelease` | `6.18.33.2-microsoft-standard-WSL2` |

**Two traps, both live on this machine:**

- **`ProductName` lies.** The registry says `Windows 10 Pro` on a Windows 11 machine — Microsoft never
  updated that value — so a run labelled from it would name the wrong operating system in every row. The
  BUILD is the truth; `26200` is Windows 11. Read `DisplayVersion` and the build, never the product name.
- **`Win32_OperatingSystem.Version` has no patch.** It stops at `10.0.26200`. The UBR — the number that moves
  on Patch Tuesday, and therefore the only one that can answer *"we updated and it got slower"* — is
  registry-only. A version without it cannot distinguish two runs a month apart.

**Direct3D and DXCore are not decoration.** They are the GPU passthrough shims, delivered by the Windows
driver package into `/usr/lib/wsl/lib`, and they are the layer the 155 s boundary finding lives in. A WSL arm
whose D3D shim changed is not the same arm, and nothing else in this table would show it.

**The driver is per adapter, and under WSL it is the WINDOWS one.** Read from `Win32_VideoController`:
`DriverVersion`, `DriverDate` and `ConfigManagerErrorCode` together. Here both cards report
`32.0.31035.1003`, dated 2026-07-24, code `0` — one driver serving the discrete R9700 and the integrated
890M, which is why the field belongs to the adapter rather than to the machine even when the values agree.

**ROCm's version is NOT at the conventional path.** `/opt/rocm/.info/version` does not exist on this
install, so the ROCm version needs its own probe before it is promised — §7's second open question, now
half-answered: the Windows driver version is available and the ROCm one is not, at least not there.

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
([PLAN_variant_matrix.md](../todo/PLAN_variant_matrix.md) §3.4b) is what serialises them — and **observed only**
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

- **No accelerator lease.** It is [PLAN_variant_matrix.md](../todo/PLAN_variant_matrix.md) §3.4b's, and §3.4 above
  depends on it rather than duplicating it.
- **No per-leg power draw in watts.** Available from both vendors, and no question here needs it yet.
- **No throttling remedy.** Recording that a leg throttled is the deliverable; deciding what to do about it
  is a measurement somebody has to design.

## 4. Build order

1. ~~**The shapes.**~~ **IMPLEMENTED 2026-08-19.** `MachineFacts` with its fingerprint, `SampleSummary`,
   `VramReading` + `VramAttribution`. Domain only. The fingerprint excludes free space — the trap it was
   most likely to fall into, since a fingerprint that moved by the minute would give every run its own
   machine and destroy the comparison it exists to enable.
2. ~~**The static probe.**~~ **IMPLEMENTED 2026-08-19.** `IMachineProbe` + `MachineProbe`, with the pure
   parsers in `MachineText` tested against output captured from this machine. Two traps the real output
   taught it, both now tested: `wsl.exe --version` emits UTF-16LE, and `/proc/meminfo` under WSL reports the
   VM's allocation rather than the machine's. The Windows GPU read is ported from `GpuProbe`
   (§7 question 1, answered in favour of porting) and demonstrated live: the registry returns
   34 208 743 424 bytes for the R9700 where WMI's `AdapterRAM` would say 4 GiB for it and the integrated
   890M alike. Left unread rather than guessed: the physical core count and cluster size on Linux.
3. ~~**The migration.**~~ **IMPLEMENTED 2026-08-20.** `run_machines`, keyed by the run, facts as JSON with
   the fingerprint lifted out and indexed, written ONCE — a second read cannot re-label a run in flight.
   Wired into `bench run` in the same slice, because a table nothing writes is the "built and never called"
   pattern this repository has met three times. **Deviation:** the leg summary columns are NOT here; they
   have no writer until step 4, and shipping columns nothing fills is that same defect.
4. ~~**The background sampler**~~ **IMPLEMENTED 2026-08-20.** `HardwareSampler` — two clocks in one loop,
   drain-into-leg by window, and every tick wrapped so a reader that throws stops contributing rather than
   failing the leg. **Deviation:** the cadence SPLIT rather than being one number, because §7.3's measurement
   refuted the plan's own 2-second default — see that question.
5. ~~**The report**~~ **IMPLEMENTED 2026-08-20 (dynamic summaries) and 2026-08-23 (the fingerprint
   comparison).** The machine and the load reach `RunReportDto`; the comparison of §3.5 landed on the ARM
   comparison rather than on `bench report`, which is the deviation the status line explains — a single-run
   report has only one machine to name, so a *difference* between machines can only exist where runs are
   folded, and that use case did not exist when this step was written.

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

- [x] Build 0 warnings; the suite green; every row of §5 has a named test.
- [x] A run records its machine once, and a leg records what the card looked like while it ran.
- [x] No dynamic field can express "unknown" as a zero.
- [x] A VRAM figure taken while something else held the card is stored as *observed*, never as *attributed*.
- [x] A fingerprint difference between the runs a comparison folds is NAMED — on the arm comparison rather
      than on `bench report`, per the deviation in the status line, and per arm as well as per scope.
- [x] Nothing here can fail a leg.

## 7. Open questions

1. **Where does the static probe live — here or in qln?** `GpuProbe` already reads the adapter, its true
   VRAM and its health code, and a second implementation in this repository is exactly the duplicate
   `reuse-first` refuses. But this benchmark measures engines it did not write, and depending on qln for its
   own machine facts would make it unable to measure anything else. **Proposal: the SHAPES and the rules live
   here; the Windows/Linux readers are ported — mechanism, not repository — with their provenance named in
   the comment, the way `PLAN_scoremeter_port.md` ports its cleaned-LOC family.**
2. ~~**Driver version on Linux/WSL.**~~ **Half-answered by the probe of §3.1a, 2026-08-19.** The Windows
   driver reads cleanly from `Win32_VideoController` — `32.0.31035.1003`, dated 2026-07-24, one driver for
   both adapters — and under WSL that IS the driver, materialised as the Direct3D and DXCore shims
   `wsl.exe --version` reports. What is still unknown is the **ROCm** version: `/opt/rocm/.info/version` does
   not exist on this install, so either another path carries it or it needs `rocminfo`, which is a process
   launch on the WSL side of the boundary. Do not promise the field until one probe settles it.
3. ~~**Cadence default.**~~ **MEASURED 2026-08-20, and the guess was refuted.** A vendor-neutral machine-wide
   VRAM read on Windows — `\GPU Adapter Memory(*)\Dedicated Usage` through PDH, the only path that covers
   AMD, NVIDIA and Intel alike — costs **1 639 ms cold and 1 004 ms warm** on this machine. At the 2-second
   cadence this plan proposed, the sampler would spend half the wall clock sampling: §3.6's "must not become
   the thing it measures", failed by its own default.

   So the readings SPLIT by cost rather than sharing one cadence:

   | stream | source | cost | cadence |
   |---|---|---|---|
   | CPU + RAM | `Process` times and `GC.GetGCMemoryInfo` — no IO, no process launch | microseconds | the base tick |
   | VRAM | PDH on Windows | **~1 s** | its own, far slower, and its `Count` says how thin it was |

   That is why `SampleSummary` carries a count: a ten-second leg honestly reporting **one** VRAM sample is
   legible, while the same leg reporting a smooth min/max would be fiction. The cheap alternative — asking
   the engine's own `/health` — is milliseconds but answers a DIFFERENT question: what our processes hold,
   not what the card holds, and the two differ by exactly the amount somebody else is using.
