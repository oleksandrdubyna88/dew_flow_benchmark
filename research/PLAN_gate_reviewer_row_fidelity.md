# PLAN — a reviewer row that says what the product will do: thinking in three states, the price tier, the import's label

> Status: **IMPLEMENTED, 2026-09-29.** D1, D2, D3 and D5 in this repository (PR #57), D4 in coai (PR #622, merged). The
> deviations are under *As built* below. D4's bench-side lane stop was T5 of
> [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md): built 2026-09-30 (#59), and checked on coai
> mcp 0.40.4. References re-verified against `main` `3e08dd7`. Scope: `src/Bench.Domain/Gate/ReviewerDefinition.cs`,
> `CoaiVendorsSetting.cs`, `CoaiVendorRow.cs`, `src/Bench.Domain/Gate/Import/CalibRecord.cs`, `src/Bench.Infrastructure/Persistence`
> (the `gate_reviewers.Thinking` column), `hosts/Cli/GateToolsCommand.cs` (`reviewers add`), `hosts/Cli/GateAssessCommand.cs` (the assessor's launch), one migration; one
> cross-repository item in `dew_flow_connect_other_ais` (D4, its own pull request there).
>
> Related docs: [PLAN_coai_gate_model_benchmark.md](PLAN_coai_gate_model_benchmark.md) (E7, where this was found),
> [module_gate.md](module_gate.md), [RESULTS_gate_aa_cs2.md](RESULTS_gate_aa_cs2.md).
> Cross-repository citations are paths: `coai ·` is `dew_flow_connect_other_ais`; `calib ·` is the operator's local
> Python calibration harness (outside every repository), as in the gate plan.

## 1. The symptom, measured

The first A/A campaign of E7 (2026-09-28, run `01a0e98d-eb72-7340-8a30-84f68f245de7`, product `coai@5d73ead7`) sent
grok-4.7 and glm-5.3 — the calibration's own phase-2 rows, `grok-4-7-9ca08acf` and `glm-5-3-106e63ec` — and the product
answered `skipped` in 2–4 s for all four cells, no reviewer asked:

> FeatureReview could not go to grok-4-7-9ca08acf: xai has no thinking switch — the vendor documents no way to turn
> reasoning off for this family. (glm: the same sentence for `glm`.)

The vendor row the C# driver wrote carried `"thinking": false`. The Python harness that measured those same rows never
wrote the field at all (`calib · harness/run.py:96-100`). The product reads the field in THREE states — absent is the
vendor's default, `true` is on, `false` is off (`coai@5d73ead7 · src_mcp/core/Api/ApiRowSettings.cs:4-6, 23, 60`;
`src_mcp/src/Api/AskApiMode.cs:93-94`) — and refuses `off` for a family with no switch
(`src_mcp/core/Api/OpenAiCompatibleVendor.cs:70`). The bench models it in TWO.

E7 went on by adding rows with `--thinking` (`true` is on, and on is what the vendor default is for these families), so
nothing here blocks E7. What remains wrong is below.

## 2. The defects

| # | where | what is wrong | consequence |
|---|---|---|---|
| D1 | `ReviewerDefinition.cs:19, 27, 30, 64` · `CoaiVendorsSetting.cs:122` · `CoaiVendorRow.cs:19, 107` (the panel-row reader: absent read as ON, where the product reads absent as the vendor default) · `GateEntities.cs:302` · `PostgresGateReviewerCatalog.cs:100, 138` | `ReviewerTransport.Thinking` is a `bool`, canonical `thinking-on` / `thinking-off`, and the vendor row always spells it | a row added WITHOUT `--thinking` asks for reasoning OFF — not the product's default — and the product refuses it for xai and glm; a person adding a row gets a refusal they did not ask for |
| D2 | `src/Bench.Domain/Gate/Import/CalibRecord.cs:16` | the calibration import builds every transport with `thinking: false` | the imported phase-2 rows are LABELLED `thinking-off` (it is inside their hash) while the Python runs measured them at the vendor default — the catalog describes 84 runs as something they were not |
| D3 | `hosts/Cli/GateToolsCommand.cs:320-324` | `reviewers add` takes `--price-in/--price-cached/--price-out` only; the long-context tier (`TierFromTokens`, `TierIn/Cached/Out`) that the catalog stores (`PostgresGateReviewerCatalog.cs:140`) and the import reads (`Import/CalibReviewers.cs:49`) cannot be given | a grok row added by hand prices a call above 200 000 tokens at the base rate — cost per run under-counted exactly where it is largest |
| D4 | `coai · src_mcp/runners/Reviewers/ReviewerRuntime.cs:263, 372` (origin/main `3f351c05`) | `WhyItFailed` is implemented for codex only; the Claude CLI puts its reason in the stdout JSON (`is_error`, `result`, `api_error_status`) | measured in the same campaign: Fable 5.1's cells read `exit 1 (the CLI said nothing on stderr)` while the CLI had said *"You've reached your Fable limit"* (HTTP 429) — the reason three lines away, as for codex on 2026-09-14. **Again on 2026-09-29, at a cost:** S7.3's Fable campaigns lost 6 plan cells and 38 code cells (runs `01a0edf2`, `01a0ee1f`) to the same line, while the CLI said *"You've hit your monthly spend limit"* (429). Each doomed cell ran its plan loop to `Unknown`; a campaign that could read the reason could have stopped at the first |
| D5 | `hosts/Cli/GateAssessCommand.cs:217-221` | an assessor row with no executable reference launches the runtime's bare word (`codex`); on Windows the npm install is a `.cmd`/`.ps1` shim beside a native `codex.exe` deep in `node_modules`, and the launcher does not find the bare word | measured 2026-09-28: `bench gate assess --assessor codex-gpt-6-astra` recorded all 16 findings of a campaign as *assessment failed — 'codex' is not installed*, with `codex --version` answering in the same shell; worked around by a row naming the exe through `--executable-ref` |

## 3. Decisions

- **D1 → three states, mirroring the product.** `ThinkingSetting { VendorDefault, On, Off }` on `ReviewerTransport`;
  `reviewers add --thinking on|off` (absent = vendor default). The vendor row spells the field only for `On` / `Off`.
  **Hash stability:** the canonical text keeps `thinking-on` / `thinking-off` for the two states stored today and adds
  `thinking-default` for the new one, so every existing row still hashes to its stored hash and still resolves.
  Storage: `gate_reviewers.Thinking` becomes a nullable `bool` (`null` = vendor default) in one migration; existing
  values keep their meaning.
- **D2 → fix the import, do not rewrite history.** New imports build `VendorDefault`. The rows already imported keep
  their stored definition — changing it changes their hash and their id, and every imported cell names them — and
  `module_gate.md` records, beside the import, that rows imported before this change say `thinking-off` for runs that ran
  at the default. A re-import into the existing database is refused as a changed record (the import's existing rule),
  never silently duplicated; that refusal is tested.
- **D3 → `--price-tier-from --price-tier-in --price-tier-cached --price-tier-out`**, all four or none (refused by name
  otherwise), through the existing `ReviewerPrices.Of` overload the catalog already calls. Given, `--price-tier-from`
  must be at least 1: `Of` reads 0 as "no tier", so a tier asked for at 0 would silently vanish (plan round, finding 3).
  Negative prices are refused by `Of` already.
- **D4 → a coai pull request, built with this plan.** The Claude runtime's failure reader reads the `is_error` JSON on
  stdout (`result`, `api_error_status`), RED first with the measured 429 body. The **bench-side** half (a reviewer
  refused for a spend or usage limit stops the lane instead of settling every remaining cell unmeasured) waits on a
  coai release that carries D4 into a harness's product. It is moved to
  [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md) rather than held here.

- **D5 → resolve the bare word the way a shell does; launch only a native executable; refuse the rest before the
  batch** (plan round, finding 1: one rule, not two). The word is looked up on `PATH`, with `PATHEXT` on Windows:
  - it resolves to a native executable (`.exe`, or an extensionless file elsewhere): that full path is launched;
  - it resolves only to a `.cmd`, `.bat` or `.ps1` shim: refused at the assessor check (3), naming the shim it found
    and saying to pass `--executable-ref` (arguments through `cmd.exe` are a quoting hazard, so the shim is never
    launched);
  - it resolves to nothing: refused (3) the same way.

  A refusal happens BEFORE any finding is sent, never recorded finding by finding as *assessment failed*.

## 4. Build order

1. RED: a vendor-row test — a row added with no thinking flag writes NO `thinking` field; `On` writes `true`, `Off`
   writes `false`; the reader (`CoaiVendorRow.cs:107`, absent = on) round-trips all three.
2. RED: every row stored before the change still hashes to its stored hash (a fixture row per state).
3. The domain type, the builder, the reader, the canonical text.
4. The migration + the entity mapping (`PostgresFixture`: a `null` column reads `VendorDefault`).
5. `reviewers add --thinking on|off` and the tier flags (RED: refusals by name; a tiered row round-trips).
6. The import builds `VendorDefault` (RED: a fresh import's rows carry no `thinking` field in their vendor row; a
   re-import into a database holding the old label is refused, never duplicated).
7. RED: an assessor row whose bare word resolves only to a shim is refused 3 before any batch, naming the shim and
   the flag; one that resolves to a native executable launches that path.
8. coai (D4, its own branch and gate session): RED with the measured 429 stdout body; the reason reaches the round's
   `reviewers` line; coai's own suite; a coai PR.
9. Docs: `module_gate.md` (the entity row, the import's labelled-history sentence, the entry point); this plan promoted.

### As built (2026-09-29), deviations from the text above

- **D1:** the bare `--thinking` keeps meaning ON, the state rows were added with before, next to `on|off`. The panel-row
  reader (`CoaiVendorRow`) now reads an absent field as the vendor's default; it had read absent as ON.
- **D2:** the re-import guard is not "the import's existing rule". That rule compares the source bytes, which are
  unchanged, and reviewers are matched by hash BEFORE any cell is checked. So after the label change a re-import would
  have added new reviewer rows no cell names, and read every cell unchanged. The guard built instead is a preflight
  (`CalibImport.DriftedAsync`, reading every stored cell's reviewer in ONE query through
  `IGateImportStore.StoredReviewersAsync`). A stored cell whose reviewer this import no longer builds, OR whose reviewer
  the catalog cannot resolve, refuses the import before anything is written. The cadence consultation found the second
  case: `ListAsync` drops a row that does not read back, and the guard had failed open.
- **D3:** the bench never prices a call. The tier reaches the product inside the vendor row's `price`, and the product
  prices the call. The DoD item "prices a 250 000-token call at the tier rate" is therefore met as "the tier is stored and
  carried". `--price-tier-from` below 1 is refused.
- **D5:** `CliExecutable.Resolve` is pure over `PATH`, `PATHEXT` and a file-exists function. It joins paths with the
  separator of the system it resolves for, so the Windows cases run on Linux CI. Proved on the machine where it was found:
  the original `codex-gpt-6-astra` row is refused 3, naming the npm `codex.cmd` shim, with nothing sent.

- **Gate:** plan round `proceed` (4 findings: 2 accepted, 2 rejected). A cadence consultation (Astra, closed as solved)
  found the fail-open guard, and the missing freeze of the pre-change hashes: they are frozen from `main`'s own code, for
  on and off. Code round `proceed`, 8 reviewers, 13 findings: 7 accepted and fixed (a tier without its base prices
  refused; native extensions always tried whatever `PATHEXT` says; the preflight in one query; the strict `Down`
  documented), 6 rejected with the code that disproves them.

## 5. Test plan

Pure tests for the three states and the canonical text; `PostgresFixture` for the migration and the stored-hash
stability; the CLI through `Program.Run` for `reviewers add`; the import tests over the redacted calibration fixture.
Whole suite by the executable, never `dotnet test`.

## 6. Definition of Done

- [x] A row added without a thinking flag reaches the product with no `thinking` field (probed against the product: `glm-5-3-default`, coai 0.40.3, vault key found, no refusal; stored as NULL).
- [x] Every row stored before the change resolves, hash unchanged (the frozen hashes, and all 20 rows of the real catalog listed after the migration).
- [x] `reviewers add` can give grok's price tier, and the vendor row carries it to the product, which prices the call (see *As built*).
- [x] New imports carry `VendorDefault`; the old rows' label is documented, not rewritten; a re-import is refused.
- [x] An assessor that cannot be launched is refused before a finding is sent (D5).
- [x] The coai item (D4) is a merged pull request: `dew_flow_connect_other_ais` #622.
- [x] `module_gate.md` updated; this plan promoted with its deviations.
