# PLAN — a reviewer row that says what the product will do: thinking in three states, the price tier, the import's label

> Status: **plan only, 2026-09-28 — nothing implemented yet.** Scope: `src/Bench.Domain/Gate/ReviewerDefinition.cs`,
> `CoaiVendorsSetting.cs`, `CoaiVendorRow.cs`, `Import/CalibRecord.cs`, `src/Bench.Infrastructure/Persistence`
> (the `gate_reviewers.Thinking` column), `hosts/Cli/GateToolsCommand.cs` (`reviewers add`), `hosts/Cli/GateAssessCommand.cs` (the assessor's launch), one migration; one
> cross-repository item in `dew_flow_connect_other_ais` (named, not built here).
>
> Related docs: [PLAN_coai_gate_model_benchmark.md](PLAN_coai_gate_model_benchmark.md) (E7, where this was found),
> [module_gate.md](../research/module_gate.md), [RESULTS_gate_aa_cs2.md](../research/RESULTS_gate_aa_cs2.md).
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
| D1 | `ReviewerDefinition.cs:19, 27, 64` · `CoaiVendorsSetting.cs:122` | `ReviewerTransport.Thinking` is a `bool`, canonical `thinking-on` / `thinking-off`, and the vendor row always spells it | a row added WITHOUT `--thinking` asks for reasoning OFF — not the product's default — and the product refuses it for xai and glm; a person adding a row gets a refusal they did not ask for |
| D2 | `Import/CalibRecord.cs:16` | the calibration import builds every transport with `thinking: false` | the imported phase-2 rows are LABELLED `thinking-off` (it is inside their hash) while the Python runs measured them at the vendor default — the catalog describes 84 runs as something they were not |
| D3 | `hosts/Cli/GateToolsCommand.cs:315-316` | `reviewers add` takes `--price-in/--price-cached/--price-out` only; the long-context tier (`TierFromTokens`, `TierIn/Cached/Out`) that the catalog stores (`PostgresGateReviewerCatalog.cs:140`) and the import reads (`Import/CalibReviewers.cs:49`) cannot be given | a grok row added by hand prices a call above 200 000 tokens at the base rate — cost per run under-counted exactly where it is largest |
| D4 | `coai · src_mcp/runners/Reviewers/ReviewerRuntime.cs:263, 372` (origin/main `3f351c05`) | `WhyItFailed` is implemented for codex only; the Claude CLI puts its reason in the stdout JSON (`is_error`, `result`, `api_error_status`) | measured in the same campaign: Fable 5.1's cells read `exit 1 (the CLI said nothing on stderr)` while the CLI had said *"You've reached your Fable limit"* (HTTP 429) — the reason three lines away, as for codex on 2026-09-14 |
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
  otherwise), through the existing `ReviewerPrices.Of` overload the catalog already calls.
- **D4 → named, not built here.** A coai pull request: `ClaudeRuntime.WhyItFailed` reads the `is_error` JSON on stdout
  (`result`, `api_error_status`) — RED first with the measured 429 body. Owner: the next coai session; this plan links it
  when it exists.

- **D5 → resolve the bare word the way a shell does, or refuse before the batch.** Look the word up on `PATH` with
  `PATHEXT` on Windows; a `.cmd` shim is not launched (arguments through `cmd.exe` are a quoting hazard) — the refusal
  names the shim it found and says to pass `--executable-ref`. Either way it is refused at the assessor check (3), BEFORE
  any finding is sent, never recorded finding by finding as *assessment failed*.

## 4. Build order

1. RED: a vendor-row test — a row added with no thinking flag writes NO `thinking` field; `On` writes `true`, `Off`
   writes `false`; the reader (`CoaiVendorRow.cs:107`, absent = on) round-trips all three.
2. RED: every row stored before the change still hashes to its stored hash (a fixture row per state).
3. The domain type, the builder, the reader, the canonical text.
4. The migration + the entity mapping (`PostgresFixture`: a `null` column reads `VendorDefault`).
5. `reviewers add --thinking on|off` and the tier flags (RED: refusals by name; a tiered row round-trips).
6. The import builds `VendorDefault` (RED: a fresh import's rows carry no `thinking` field in their vendor row; a
   re-import into a database holding the old label is refused, never duplicated).
7. RED: an assessor row whose bare word resolves only to a shim is refused 3 before any batch, naming the flag.
8. Docs: `module_gate.md` (the entity row, the import's labelled-history sentence, the entry point); this plan promoted.

## 5. Test plan

Pure tests for the three states and the canonical text; `PostgresFixture` for the migration and the stored-hash
stability; the CLI through `Program.Run` for `reviewers add`; the import tests over the redacted calibration fixture.
Whole suite by the executable, never `dotnet test`.

## 6. Definition of Done

- [ ] A row added without a thinking flag reaches the product with no `thinking` field (probed against the product).
- [ ] Every row stored before the change resolves, hash unchanged.
- [ ] `reviewers add` can give grok's price tier; a tiered row prices a 250 000-token call at the tier rate.
- [ ] New imports carry `VendorDefault`; the old rows' label is documented, not rewritten; a re-import is refused.
- [ ] An assessor that cannot be launched is refused before a finding is sent (D5).
- [ ] The coai item (D4) is a linked pull request, or its absence is stated here with its owner.
- [ ] `module_gate.md` updated; this plan promoted with its deviations.
