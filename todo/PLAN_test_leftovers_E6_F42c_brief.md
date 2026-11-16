# E6 F42-c brief — the adapted C# copy re-synced to mcp's F42 commit (a prerequisite PR of E6.S3)

> Status: **plan only, nothing implemented yet, 2026-11-16; the coordinator's decisions folded in the same day (§ 12).** The brief of the prerequisite PR F42-c of `todo/PLAN_test_leftovers_E6.md` (§ 6 *Before S3*; § 5 row "— (F42-c)"; D2). Its scope is widened by the parent's entry 9.1 (§ 0.1).
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-16. Read-only inputs, all verified for this brief: benchmark
> worktree `/home/jinx/git/wt/bench-f42c`, branch `feat/e6-copy-f42` from `origin/main` `83f166d6` (#216: the rules pin at
> `b1746499`, F41's release; CI green, run `43932109912`), mount `.agents/conventions` at `b1746499`; template
> `dew_flow_mcp` at `53f651309a271e7388c703e1ec73799ff6f1c76e` (F42, mcp #81) and the commit S2 copied,
> `21553ce5c5729e57557504c4a59e0311e69bc489`; mcp's plan `todo/PLAN_F42_template_container_scoped.md` at `53f65130`; the
> parent `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` § *Epoch 9* (rows F42-a, F42-b, F42-c; items 2 and
> 5; the progress record 9.1 on branch `docs/rollout-epoch-9-progress`, `d87eeb3`). "Stories" is
> `todo/PLAN_test_leftovers_E6.md`. "T:" is the template at `53f65130`; "T0:" the template at `21553ce`. "B:" is benchmark at
> `83f166d6`. Every **[M]** below was measured for this brief, read-only (the commands are in § 13).

## 0. What you build, in one paragraph

You bring benchmark's adapted copy of mcp's C# leftovers helper (`tests/Bench.Tests/Leftovers/`, copied by E6.S2 at
`21553ce`) to mcp's F42 commit `53f65130`. Between the two commits mcp changed nine of the copied files and added two
(`LeftoversGuardScenarios.cs`, `LeftoversGuardDifferentialTests.cs`): the C# guard now reads the `container-scoped` marker
case for case with the Node guard of F41, a live differential test holds the two guards to one scenario table, and the
notes N1, N4 and N5 of S2's brief are fixed (N2 did not reproduce; N3 is bounded; both only in files benchmark does not
copy). You rewrite the eleven files from the template under the SAME manifest operations (one count changes), add the two
new files as manifest entries, re-pin the manifest to `53f65130`, and move the one line of `TheAdaptedCopyTests` that pins
the manifest's commit. The differential test runs the MOUNTED Node guard, which reads workflows through the mount's `yaml`
package, so `build-test` gets the step mcp's CI got: `npm ci --ignore-scripts` in `.agents/conventions`. You rewrite case
15's Windows skip reason, update the docs, and promote this brief and the S2 brief. ONE pull request, in this repository
only. No copied file is edited by hand; no ownership, liveness or deletion code is written here.

## 0.1 Scope — exactly five touches beyond the copy, by the parent's entry 9.1

The stories' § 6 *Before S3* says "No benchmark-own file changes" and "Files. `tests/Bench.Tests/Leftovers/**` (manifest
targets and `template-manifest.json` only)" (`todo/PLAN_test_leftovers_E6.md:315-319`). That is an explicit scope line,
and F42-c cannot keep it. The question consultants (consult `57ac1bd6`, both answers) judged the widening a plan change;
it is recorded as entry **9.1** of the parent's ledger (`dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`,
`- 9.1 · 2026-11-16 · agent · scope, approach — F42-c … cannot keep E6's § 6 boundary …`, conventions PR #172). It is the
first change of epoch 9: recorded, not a stop.

Beyond the manifest targets and `template-manifest.json`, this PR touches exactly these five, and nothing else:

| # | Touch | Where | Why it cannot be avoided |
|---|---|---|---|
| T1 | the commit pin | `tests/Bench.Tests/Leftovers/TheAdaptedCopyTests.cs:79` (one line) | it pins the manifest's `commit`; D2 moves that commit to F42's (§ 4) |
| T2 | the CI step `npm ci --ignore-scripts` in `.agents/conventions` | `.github/workflows/ci.yml`, job `build-test` (§ 6) | the copied differential test runs the mounted Node guard, which needs the mount's `yaml` |
| T3 | two `-text` lines | `.gitattributes` (§ 4.1 step 3) | S2's standing decision: one line per copied file |
| T4 | case 15's Windows skip reason (the skip stays) | `tests/Bench.Tests/Leftovers/LeftoversSweepCase15Tests.cs:14-15`, `:19-21` (§ 7) | the copied N1 fix makes today's reason untrue |
| T5 | the docs | `research/module_tests.md`; the stories (their § 6 sentence amended to name T1–T5 and cite entry 9.1, and the progress of § 14); `todo/README.md`; `research/README.md`; this brief and the S2 brief (status and promotion, § 14) | they must say what exists |

**Anything beyond T1–T5 needs a new decision first, and the next accepted change to the parent is the SECOND of epoch 9:
the ninth full stop** (`common/plan-change-control.md`). If the build, the format check, an analyzer, a test or a reviewer
asks for any other file (a `.csproj`, an `.editorconfig` section, another own file, another workflow line), STOP and
report; do not add it.

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; warnings are errors).
- `.agents/conventions/common/test-leftovers.md`, its detail (§1–§4, §6 (b), §7) and the companion
  `.agents/conventions/common/test-leftovers-exemptions.md` (the `container-scoped` kind, F41; in the mount since `b1746499`).
- The stories: § 1 rows E6-e (`:51`) and E6-f1 (`:52`); § 4 D2 (`:169-182`); § 5 (`:252`, `:263-269`); § 6 *Before S3*
  (`:310-320`); § 9 (`:456`, `:458`); § 11 (`:475`); § 12 item 5 (`:498-500`).
- The S2 brief, `todo/PLAN_test_leftovers_E6_S2_brief.md`: § 4.1 (the operations), § 5 (the manifest and the check),
  § 14 (commands), § 17 (N1–N5), § 19 item 1 (`.gitattributes`).
- `common/planning-docs.md` (the docs and the promotion), `common/plan-change-control.md` (§ 0.1 and § 12 Q1: entry 9.1 and the ninth full stop).
- No Python anywhere (scripts in Node or bash). No `git stash`. Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions

1. **F42 is merged in mcp** — met: `53f65130` is mcp #81's merge commit (`git -C /home/jinx/git/dew_flow_mcp log --oneline
   21553ce..53f65130` → `53f6513`, `7898a93`, `07a0f5b` **[M]**).
2. **F41-b is merged here** — met: B:`83f166d6` is #216, the mount at `b1746499`; its module exports `DECLARATION_KINDS`
   (B:`.agents/conventions/tools/leftovers-check.mjs:139`), which is what the differential test reads to decide whether
   the marker rows run (T:`LeftoversGuardDifferentialTests.cs:24`, `:65-69`).
3. **The open-PR cap** (`common/pull-requests.md` rule 7, at most three): settled. Five Dependabot PRs are open (#210–#214)
   **[M]**, and the owner allowed every E6 PR over the cap (2026-11-16; § 12 Q4).
4. **Entry 9.1 is recorded in the parent** (conventions PR #172) before this PR merges: the PR body cites it (§ 0.1).
5. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`); the coai code gate and the Opus
   review are done. F42-c merges BEFORE E6.S3's code (stories § 5).

## 3. What changed in the template — the file set (point 1)

`git diff --name-status 21553ce 53f65130` in mcp **[M]**, restricted to what benchmark copies (the 51 files of
`tests/Mcp.Tests/Leftovers/` at `21553ce`, plus `tests/Mcp.Tests/DirectoryLinks.cs` and `RepositoryRoot.cs`):

| Template file | Change | Blob `21553ce` → `53f65130` | Lines | What it carries |
|---|---|---|---|---|
| `LeftoversAudit.cs` | M | `f1a93747` → `3f32fc52d9c0109d6600ed7a7a3fed553091b238` | 105 → 198 | N5: `RootExists` (T:`:63`) reads the root with the native `lstat`; an unreachable root is a `LeftoversAuditException`; the listing is lazy; `DiscoverKeeping` (T:`:169`) keeps the registry's groups |
| `LeftoversAuditException.cs` | M | `6d703ecf` → `dd5860bb1e5eadc3eb0bf3fc64bdfe8bd9620fc1` | 36 → 63 | N5: the unreachable-root constructors and `UnreadableRoot`; the message starts with `OwnedToken.RepoToken` |
| `LeftoversAuditTests.cs` | M | `fcc8484b` → `2e258e84b57fabfe2c0b8d78db859f115cba0ad1` | 313 → 544 | N5's tests (`chmod 000`, skipped on Windows and when privileged); one more `mcp tests:` assertion (T:`:249`) |
| `LeftoversGuard.cs` | M | `8a92a79b` → `529646e32d820b8f576338c023de66a3f407e3cb` | 296 → 321 | F42-a: `SharedStoreHits(line, containerScoped)`, `ContainerTeardown`, `IsContainerTeardown` |
| `LeftoversGuardExemptions.cs` | M | `156fe549` → `a240a05af9cd39c4a0311d7d0d55f72d226f47a6` | 63 → 103 | F42-a: the kind `ContainerScoped` (T:`:24`), not in `Exemptible` (T:`:20`); the owner rule; the corrected doc comment |
| `LeftoversGuardTests.cs` | M | `f6a8e035` → `951b43af34c791a87162c71e350858321d1b0808` | 698 → 875 | the rows split into groups; the marker rows read from `GuardScenarios`; N4: `RunNode` (T:`:682`) ends and reaps the child tree |
| `Native.cs` | M | `dd6c8680` → `f9a277f6c16727b10651221ee7f37e88685a0b91` | 511 → 547 | N1: `LinkCountOf` (T:`:66`, Linux and Windows) |
| `OwnedTests.cs` | M | `20a0a213` → `b97e3eff1241a3ac84450dd21f62b1698d7ef4f9` | 472 → 634 | N1's tests: a git repository with a commit removed with its claim (runs on Linux too), the read-only clearing, the hard-link cases (Windows-only ones skip elsewhere) |
| `Removal.cs` | M | `05e58fb5` → `d6ec896f57179e85cff457c2ae3f53fa27fd16d3` | 125 → 159 | N1: `ClearReadOnlyBelow` (T:`:64`), called on Windows only (T:`:54`) |
| `LeftoversGuardScenarios.cs` | **A** | — → `8da3abb6c114b96ae997cbfb4665010d7f8800f8` | 139 | the ONE scenario table (F41's P1–P7 and N1–N19 in C#, parity rows, unmarked shared-store rows); detector words split by `§` |
| `LeftoversGuardDifferentialTests.cs` | **A** | — → `2d308cd35b31b8973243e428341d3410fe61bf68` | 107 | the live check: the table through `LeftoversGuard.Scan` and through the MOUNTED Node guard's `scanText`; line and class always, text for `shared-store` and `exemption` |

**Unchanged, so their entries stay as they are** (blob equal at both commits **[M]**, the § 13 script): the other 42
folder files, `DirectoryLinks.cs` (`5512b98e`) and `RepositoryRoot.cs` (`2884381a`) — among them `LeftoversSweep.cs`
(`64f97f5f`, the three F29 ops), `LeftoversSweepFixture.cs` (`797d7457`), `OwnedToken.cs` (`578314ee`),
`LeftoversSelfTest.cs` (`b1ebd559`), `LeftoversGuardTokens.cs` (`ce039ff1`) and `LeftoversRepositoryGuardTests.cs`
(`956f18ff`, the `Explicit` op). `git ls-tree -r --name-only 53f65130 tests/Mcp.Tests/Leftovers | wc -l` → 53 **[M]**.

**Changed in mcp, not copied here, and why nothing follows:** `tests/Mcp.Tests/DirectoryLinksTests.cs` (N2's pin, which did
not reproduce, and N3's space case; S2 brief B2 left the file out, coai did the same; `DirectoryLinks.cs` itself did not
change); `.github/workflows/ci.yml` (its `npm ci` step, § 6 below carries it); `research/module_tests.md`, the plan and
`todo/README.md` (mcp's own docs); `.agents/conventions` and `.claude/hooks/lib/agent-models-decide.mjs` (mcp's pin bump
#82, which benchmark made as #216).

## 4. The manifest: operations, counts, entries (point 1)

**Every existing operation still applies to the new bytes; NO operation's old text disappears.** Measured by applying the
manifest's operations forward to each template blob at `53f65130` and counting the old text before each step (the § 13
script; at `21553ce` the same script reports every count as listed, the control) **[M]**:

| Entry | Operation | Count at `21553ce` | Count at `53f65130` | Where at T |
|---|---|---|---|---|
| `LeftoversAuditTests.cs` | `mcp tests:` → `bench tests:` | 6 | **7** | `:249` (new: `An_audit_that_cannot_read_the_root_still_reports_what_the_registry_holds`), `:451`, `:460`, `:461`, `:478`, `:499`, `:504` |
| `LeftoversAuditTests.cs` | `--token mcp --dry-run` → `--token bench --dry-run` | 1 | 1 | `:454` |
| `LeftoversGuard.cs` | `tests/Mcp.Tests/` → `tests/Bench.Tests/` | 5 | 5 | `:47`, `:48`, `:63`, `:64`, `:65` |
| `LeftoversGuardTests.cs` | `tests/Mcp.Tests/` → `tests/Bench.Tests/` | 11 | 11 | `:151`, `:155`, `:197-200`, `:214`, `:393`, `:799`, `:827`, `:837` |
| `LeftoversGuardTests.cs` | `RepositoryIdentity.Row` → `OwnedToken.RepoRow` | 1 | 1 | `:488` |
| `OwnedTests.cs` | `-mcp-pg-` → `-bench-pg-` | 1 | 1 | `:186` |
| every changed file | its namespace line | 1 | 1 | — |

**The one count to correct:** `LeftoversAuditTests.cs` op 2, `"count":6` → `"count":7` (B:`template-manifest.json:35`).
The seventh occurrence is an assertion on the audit's message (`error.Message.Should().StartWith("mcp tests: this run left
1 folder(s) it claimed — 1 never disposed")`), and that message is built from `OwnedToken.RepoToken`
(T:`LeftoversAuditException.cs`), so in the copy it MUST read `bench tests:`; left at 6, the copy either keeps `mcp tests:`
and that test is red, or replaces all seven and `TheAdaptedCopyTests` is red. Keep the op's `reason`.

**The two new entries** (each with the namespace op only, count 1; no other mcp-specific text in them needs an op — see
below):
```json
{ "upstream": "tests/Mcp.Tests/Leftovers/LeftoversGuardDifferentialTests.cs", "target": "tests/Bench.Tests/Leftovers/LeftoversGuardDifferentialTests.cs", "blob": "2d308cd35b31b8973243e428341d3410fe61bf68", "replace": [
    {"old":"namespace Mcp.Tests.Leftovers;","new":"namespace Bench.Tests.Leftovers;","count":1}
] },
{ "upstream": "tests/Mcp.Tests/Leftovers/LeftoversGuardScenarios.cs", "target": "tests/Bench.Tests/Leftovers/LeftoversGuardScenarios.cs", "blob": "8da3abb6c114b96ae997cbfb4665010d7f8800f8", "replace": [
    {"old":"namespace Mcp.Tests.Leftovers;","new":"namespace Bench.Tests.Leftovers;","count":1}
] },
```
Place them in the ordinal order the folder entries already follow: the differential test after `LeftoversGuard.cs`
(B:`template-manifest.json:38-41`), the scenarios after `LeftoversGuardLiterals.cs` (`:51-53`). The manifest then has
**55 entries** (53 folder files + `DirectoryLinks.cs` + `RepositoryRoot.cs`).

**mcp-specific text in the new and changed files, read and left alone on purpose** (the same policy as S2 brief § 4.1 *Not
changed*): the `{Lower}_u1000_mcp_x_abc123` and `{P}-u1000-mcp-t-abc123` names in `LeftoversGuardScenarios.cs` (`:120-124`,
`:133` of the copy) are owner-form fixture names; the C# guard's owner-form test does not read the token
(T:`LeftoversGuard.cs:111`, `OwnerForm` = prefix + owner segment only, and `:277`), so they mean the same in benchmark.
The differential test's skip `Reason` (T:`:16`) says "mcp's rules pin"; it is shown only while the mount predates F41,
and benchmark's mount does not (§ 2 item 2): no op (decided, § 12 Q3).

**The manifest's other fields:** `commit` → `"53f651309a271e7388c703e1ec73799ff6f1c76e"` (B:`:4`); the nine changed
entries' `blob` → the full SHAs of § 3; `about` (B:`:2`) rewritten to name the commit, keeping every word
`TheManifest_IsWellFormed` checks (`F29`, `F31`, `BUILTIN\Administrators`, `Git Bash`; B:`TheAdaptedCopyTests.cs:81`):

> E6 S2 copied, F42-c re-synced: the adapted copy of dew_flow_mcp tests/Mcp.Tests/Leftovers at 53f65130 (F42, mcp #81;
> plan D2; S2 copied it at 21553ce), plus DirectoryLinks.cs and RepositoryRoot.cs. Per file: upstream path, target path,
> upstream git blob SHA, ordered exact-replace operations with their match counts and, beyond the namespace line, a
> reason. TheAdaptedCopyTests undoes them in reverse on the raw bytes, compares the git blob SHA, and applies them forward
> again. Limits (plan D3): F29 is closed by copy - …(the rest of today's text, word for word, from "on Windows the sweep
> holds" to "the limit does not apply there.")

**One benchmark-own line moves with the manifest:** `TheAdaptedCopyTests.cs:79` pins the commit
(`manifest.Commit.Should().Be("21553ce5c5729e57557504c4a59e0311e69bc489", "the copy is of this one template commit (plan
D2)")`). Change the SHA to `53f651309a271e7388c703e1ec73799ff6f1c76e` and the because-text to "the copy is of this one
template commit (plan D2; re-synced by F42-c)". Nothing else in that file changes: `BenchOwn` (`:32-46`) stays the twelve
files, because the two new files are manifest entries, never own files (`:130`). This is touch T1 of § 0.1 (entry 9.1).

### 4.1 How to build the copy (the S2 procedure, § 5.1 there)

1. A throw-away Node script in YOUR scratchpad (never committed). For each manifest entry `E` (all 55): read the raw
   bytes with `git -C /home/jinx/git/dew_flow_mcp cat-file blob 53f65130:<E.upstream>` (a `Buffer`, no line-end change);
   check `git -C /home/jinx/git/dew_flow_mcp rev-parse 53f65130:<E.upstream>` equals `E.blob`; apply `E.replace` in order,
   counting each old text first and stopping with an error when the count is not the listed one; write the bytes to
   `E.target`. Writing all 55 is deliberate: the 44 unchanged ones must come out byte-identical (`git status` shows only
   the eleven), which is a second check of the script.
2. Never let an editor or formatter touch a copied file afterwards. `dotnet format --verify-no-changes` must pass on the
   new bytes; if it wants to change a COPIED file, stop and report (the answer is project-level, never an edit).
3. `.gitattributes` (touch T3): add one `-text` line for each new copied file, in the block of S2 (B:`.gitattributes:7-62`, its
   comment at `:7-8`; S2 brief § 19 item 1: one line per copied file, never a glob), in ordinal order:
   `tests/Bench.Tests/Leftovers/LeftoversGuardDifferentialTests.cs -text` after `:20`, and
   `tests/Bench.Tests/Leftovers/LeftoversGuardScenarios.cs -text` after `:24`.

## 5. The copied tests that arrive, and what they need here

| Class (copied) | What it adds | Needs here |
|---|---|---|
| `LeftoversGuardTests` | the marker rows (from `GuardScenarios`), five refusal facts, the teardown-beside-a-reset facts, N4's `A_node_that_does_not_answer_in_time_…` | `node` on `PATH` (already true: the scripts rows ran in S2) |
| `LeftoversGuardDifferentialTests` | three facts: the table's shape; the rows that do not depend on the marker, through the mounted Node guard; the marker rows, skipped only when the mounted module lacks `DECLARATION_KINDS` | `node` 20+ AND the mount's `yaml` package: the table holds workflow rows (`.github/workflows/w.yml`, T:`LeftoversGuardScenarios.cs:78`, `:131`) in BOTH halves, and the Node guard reads a workflow only through `yaml@2` (B:`.agents/conventions/tools/leftovers-check.mjs:58-59`, `loadYaml` `:250-262`); § 6 |
| `LeftoversAuditTests` | N5: unreachable roots red, link roots judged by the final target, the run record checked first | POSIX and NOT root (`Environment.IsPrivilegedProcess` skips): CI's runner user and the WSL user are not root |
| `OwnedTests` | N1: a git repository with a commit removed with its claim (runs on Linux: `git` with `-c user.name=t -c user.email=…`, T:`:27`); `ClearReadOnlyBelow` pinned on every OS by a seam; a real second hard link counted on Linux | `git` on `PATH`; the Windows-only tests skip |

## 6. CI: the mount's node dependencies in `build-test` (point 2)

**Why.** `LeftoversGuardDifferentialTests` starts `node --input-type=module -e <program> -- <module URL>` through
`LeftoversGuardTests.RunNode` (T:`LeftoversGuardTests.cs:682`), which asserts exit code 0. Without the mount's
`node_modules`, the guard's `scanText` of a workflow row throws `YamlMissingError` **[M]**: in a copy of the mount's
`tools/` with no `node_modules`, `scanText('.github/workflows/w.yml', '      - run: psql -c "TRUNCATE TABLE users"')` →
a `YamlMissingError` ("… package (version 2) is needed to read workflows and cannot be found …"); with
`npm ci --ignore-scripts --prefix .agents/conventions` done, the same call → `["shared-store"]` and `DECLARATION_KINDS` is
exported (`true`). The guard is not run as a CLI by that call: its `invokedDirectly` check (B:`leftovers-check.mjs:746-755`)
compares `process.argv[1]` as a path, and the test passes a `file://` URL **[M]** (the exact program, run from the worktree
root with one workflow row on stdin → exit 0, `{"f41":true,"results":[[{"line":1,"cls":"shared-store",…}]]}`). S2's brief
said `build-test` needed no `npm ci` (S2 brief § 11.2 item 1); that held for the 21553ce copy and stops holding with this one.
mcp's CI added the same step for the same reason in F42 (`git -C /home/jinx/git/dew_flow_mcp diff 21553ce 53f65130 --
.github/workflows/ci.yml`, three jobs).

**Where.** B:`.github/workflows/ci.yml`, job `build-test` (`:13-72`): after the submodule line (`:23`) and before
`actions/setup-dotnet` (`:25`). Only `build-test` runs `Bench.Tests`; `contract` (`:78`) and `plans` (`:140`) do not, and
get nothing. `node` is the runner's own (the `contract` job already relies on it, `:128-134`); the mount has a
`package-lock.json`.
```yaml
      # The leftovers tests run the mounted Node guard through node (LeftoversGuardDifferentialTests feeds it workflow rows),
      # and its workflow reader imports the mount's `yaml` package: an uninstalled mount makes it throw YamlMissingError.
      # node itself is the runner's (the contract job relies on it too); --ignore-scripts because the mount's install needs none.
      - name: Install the mounted conventions' node dependencies
        run: npm ci --ignore-scripts
        working-directory: .agents/conventions
```
The Node guard reads workflows: the step must add no finding (§ 9, the after-count). This is touch T2 of § 0.1.

**Deterministic, and it dirties nothing** (the consultant's caution; check K2 in § 10). `npm ci` installs exactly the
mount's `package-lock.json` and never writes it; `--ignore-scripts` runs no install script. Measured in this worktree
after `npm ci --ignore-scripts --prefix .agents/conventions` **[M]**: the mount's `.gitignore:2` ignores `node_modules`
(`git -C .agents/conventions status --porcelain --ignored` → only `!! node_modules/`), its `package-lock.json` is
unchanged (`git diff --quiet` → 0), `git status --porcelain` of benchmark is empty, and `git submodule status` shows the
pin `b1746499` with no `+` or dirty mark. `TheAdaptedCopyTests` reads only `tests/Bench.Tests/Leftovers/` and the
manifest (B:`TheAdaptedCopyTests.cs:25`, `:56`, `:121-124`), so nothing under `.agents/conventions` can turn it red.
Do not use `npm install`, `npm update` or a version range anywhere: they may write the lockfile.

**It also serves S7.** S7 adds the Node guard as a CLI step to `build-test` (stories `:407-408`); that guard reads
benchmark's workflows and needs the same `yaml`, so S7 keeps this step and adds none.

## 7. Benchmark's own files against F42 (point 3)

Benchmark-own files are the twelve of `BenchOwn` (B:`TheAdaptedCopyTests.cs:32-46`) plus `template-manifest.json`. Each
interaction, read (not run):

| Own file | What in F42 touches it | Effect | Action |
|---|---|---|---|
| `SweepLock.cs` | `Native.cs` grew `LinkCountOf` and `StatxNlink` only; `FinalPathOfFollowing` (used at `SweepLock.cs:75`) is unchanged | none | none |
| `StartOfRunSweep.cs`, `StartOfRunOrderAttribute.cs`, `StartOfRunSweepTests.cs` | `LeftoversSweep.cs` and `LeftoversSweepFixture.cs` are unchanged (blobs equal); their ops (F29 F1–F3; `typeof`; `StartOfRunSweep.Run()`) apply as they are | none; G3 (`Runs == 1`) holds | none |
| the end-of-run audit (the copied fixture's `DisposeAsync` → `LeftoversAudit.Run`) | `Discover` now checks the run record BEFORE touching the root, reads the root with `RootExists`, lists lazily, and an unreachable root throws `LeftoversAuditException` that keeps the registry's groups (`DiscoverKeeping`, T:`LeftoversAudit.cs:169`) | a run whose temp root cannot be reached is now RED with "bench tests: the end-of-run audit could not read the temp root …" instead of passing as clean. The CI audit step (B:`ci.yml:55-72`) greps the normal first line, which is unchanged | none; it is the fix N5 asked for |
| `TempDir.cs`, `TempDirTests.cs` | `OwnedDir.Dispose` → `Removal.Quietly` → `RemoveFolder` now calls `ClearReadOnlyBelow` on Windows only (T:`Removal.cs:54`); Linux unchanged | none on Linux; on Windows a folder of read-only files is now removed | none |
| `LeftoversSweepCase15Tests.cs` | the sweep removes a quarantined folder through `Removal.Quietly` (B:`LeftoversSweep.cs:331`), which now clears read-only files on Windows (N1, observed RED then GREEN on mcp's `windows-latest`, run `43928795620`) | Linux: unchanged, case 15 still runs. Windows: the skip REASON (`:14-15`, `:19-21`: "the copied Removal … does not clear that flag on Windows") becomes untrue | touch T4 (decided, § 12 Q2): rewrite the reason, keep the skip; the text is in § 7.1 |
| `WindowsNewFileOwnerLimitTests.cs` | reads the owner SID directly; `Scope.cs` and `OwnedDir.cs` are unchanged | none | none |
| `TwoSweepsAtOnceTests.cs`, `SweepPlants.cs`, `SuiteChild.cs` | none of the changed files; the child's audit returns early on a foreign run record exactly as before (the order of the two tests changed, both give `[]`) | none | none |
| `TheAdaptedCopyTests.cs` | the manifest's commit | `:79` pins it | changed (§ 4) |
| `template-manifest.json` | the re-pin | — | changed (§ 4) |

**Names.** No type or member added by F42 collides with anything in `tests/`, `src/` or `hosts/`
(`git grep -w -E 'GuardScenario|GuardScenarios|LinkCountOf|ClearReadOnlyBelow|RootExists|UnreadableRoot|NodeAnswer|NodeFinding'`
→ nothing outside the copy **[M]**; `LiveProcess` is a private class inside the copied `LeftoversAuditTests.cs`). No code
outside `tests/Bench.Tests/Leftovers/` calls `LeftoversAudit`, `Removal` or `Native` **[M]**.

### 7.1 Case 15's Windows reason (touch T4)

Keep all three `Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsReason)` lines; change only the text. Benchmark has no
Windows leg, so removing the skip would claim a Windows result nobody here runs. The constant (`:19-21`) becomes:

> not run on Windows here: benchmark has no Windows leg. Since the re-sync to mcp 53f65130 the copied Removal clears
> read-only files below a folder before the delete on Windows (N1, observed by mcp on windows-latest, run 43928795620),
> so read-only git objects no longer stop the removal; not observed in this repository

and the doc comment's *Windows* paragraph (`:14-15`) says the same in one sentence. Nothing else in the file changes.

## 8. The repository guard test stays `Explicit` (point 4)

`LeftoversRepositoryGuardTests.cs` is unchanged between the two commits (blob `956f18ff` at both **[M]**), so its op
(`[Fact]` → `[Fact(Explicit = true)] // E6 S7 switches this on …`, B:`template-manifest.json:77`) applies with count 1 and the
test stays off until S7 (stories D10). Do not touch that op.

## 9. The Node guard over benchmark (point 4)

- **Before, measured read-only on B:`83f166d6` with the mount at `b1746499`** **[M]**: `--profile node --token bench
  --identity "the measuring harness"` → **209** findings; without the profile → **3**. (The same 209 and 3 the stories
  measured at `46e27108` with the mount at `4c9a684`.) None is in `tests/Bench.Tests/Leftovers/`.
- **After, measured in a throw-away copy** (`git archive` of `83f166d6`, the mount copied in and excluded from the copy's
  git, `git init`, all 55 files written by the § 4.1 procedure, the two new ones with the namespace op) **[M]**: **209**
  and **3**: the re-synced copy adds no finding. File by file, `scanText` of each of the eleven changed or new copied files
  → `[]` **[M]**: the scenario table spells every detector word with a `§` inside it and builds the prefix from
  `LeftoversGuard.CamelWord` (T:`LeftoversGuardScenarios.cs:16-21`, `:29`), and the differential test's program holds no
  detector word.
- **The PR must show 209 and 3 before and after** on its head, the CI step of § 6 included (it was not in the throw-away
  copy). If a count moves, the cause is a NEW benchmark line (the CI step, a doc), never a reason to edit a copied file.
- The copied C# guard (Explicit) by hand, before and after, as information for S3–S7 (`--explicit only`, § 13); record both
  counts on the PR.

## 10. Test scenarios (point 5)

Every RED is watched for the right reason and quoted on the PR (command and first line). Builds and runs follow § 13.

| # | Scenario | Expected | RED |
|---|---|---|---|
| C1 | `TheAdaptedCopyTests` at commit 1 (§ 11): the manifest re-pinned and the `:79` pin moved, the copied files still `21553ce`'s | **12 red of 58**: the 11 theory rows of the changed and new files (nine "the git blob is …; the manifest says …", `LeftoversAuditTests.cs` failing at its op 2, "expected 7 occurrence(s) of the new text, found 6"; two new files not found) and `EveryLeftoversFile_IsInTheManifestOrIsBenchsOwn` (two targets missing); `TheManifest_IsWellFormed` and the count fact green | this IS the red; record the count |
| C2 | the same at commit 2 (the eleven files written) | **58 of 58 green** (55 rows + 3 facts) | — |
| C3 | one copied byte changed (a character in a comment of the NEW `LeftoversGuardScenarios.cs`) | its row red, naming the file and both SHAs; with `BENCH_TEMPLATE_REPO=/home/jinx/git/dew_flow_mcp` the first differing line is named | by hand, restored (the stories' and the parent row F42-c's counterexample) |
| C4 | the `count` of `mcp tests:` set back to 6 | red at op 2 ("expected 6 … found 7") | by hand, restored |
| C5 | a stray `tests/Bench.Tests/Leftovers/Stray.cs` | `EveryLeftoversFile_…` red naming it | by hand, removed |
| D1 | `--filter-class "*LeftoversGuardDifferentialTests"` with the mount's `node_modules` ABSENT (a fresh `git submodule update --init`, before any `npm ci`) | the two node facts red: "node said: … YamlMissingError …" | this is the RED of § 6's CI step; record it, then `npm ci --ignore-scripts --prefix .agents/conventions` and re-run: 3 of 3 green |
| D2 | `--filter-method "*Marker_lines_read_the_same_in_the_mounted_node_guard_once_it_knows_the_marker"` | **succeeded, not skipped** (the mount at `b1746499` exports `DECLARATION_KINDS`) | — |
| D3 | CI `build-test` on the PR head | green; the mount at `b1746499` there too, so D2's fact runs, not skips. Quote the run; if the runner's log does not name skipped tests, compare its skipped count with `main`'s run `43932109912` plus the new Windows-only skips, and say so | — |
| L1 | `--filter-namespace "Bench.Tests.Leftovers"` | 0 failed; record total, passed, skipped against S2's 503 / 479 / 24 (#207); every new skip is Windows-only, macOS-only or the two self-tests and the Explicit guard, with its reason | — |
| L2 | `--filter-class "*LeftoversSweepCase15Tests"` | 3 of 3 green on Linux (case 15 unchanged on Linux: `ClearReadOnlyBelow` runs on Windows only) | — |
| L3 | `--filter-class "*StartOfRunSweepTests"` | G1–G4 green; `Runs == 1` | — |
| L4 | the audit's teeth, by hand (`BENCH_LEFTOVERS_SELFTEST=1`, then `=failed-dispose`) | non-zero exit and the audit's first line, as on `main` | — |
| W1 | the whole suite, once on B:`83f166d6` (built from the base) and once on the head | the two FAILED lists are identical: no new failure. Known on this machine: every class in the `"postgres"` collection (`PostgresFixture`, Testcontainers) fails, no Docker here; #207 recorded **572** such failures on its base and head (its PR body). Re-measure the base, do not assume 572 | — |
| W2 | `dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore`; `git diff --check`; the build | clean; 0 errors, and no warning that `main` does not have (#207 recorded one, `ASPIRE010` from AppHost, the same on `main`) | — |
| G1 | the Node guard, § 9 | 209 and 3 before and after | — |
| P1 | `node .agents/conventions/tools/plan-lifecycle.mjs` | clean | — |
| K1 | **Before the PR is opened: re-read S3–S7 for any other file-scope boundary the re-sync crosses** (the consultant's caution) | none crossed beyond T1–T5; the PR body lists what was read. At this brief: see the list below | a boundary found → STOP, a new decision (§ 0.1) |
| K2 | **The CI `npm ci` is deterministic and dirties nothing** (the consultant's caution) | after the step, locally and in a CI step's log if the coordinator wants it: `git status --porcelain` empty, `git submodule status` without `+`, `git -C .agents/conventions diff --quiet package-lock.json` → 0; `TheAdaptedCopyTests` green with `node_modules` present AND absent (run C2 both ways) | `npm install` in place of `npm ci` is never tried on the branch |

**K1, as read for this brief** (the stories at B:`83f166d6`; re-read on the PR's base, it may have moved):

- **S3** (`:322-355`): Files `PostgresFixture.cs`, a benchmark-own `ContainerPass.cs` "on the copy check's own list"
  (`:352-353`), its tests, `research/module_tests.md`. Crossing: none. S3 will edit `BenchOwn` in `TheAdaptedCopyTests.cs`
  (`:32-46`), F42-c only `:79`: the same file, other lines; F42-c merges first (§ 5). S3's marker (E6-f1) is accepted by
  the copied C# guard only after F42-c: that is F42-c's purpose, not a crossing.
- **S4** (`:357-370`): `tests/Bench.Tests/ModelArms/**`. Disjoint.
- **S5** (`:372-388`): `tests/Bench.Tests/**` outside `ModelArms/` and `Leftovers/`, the fakes, three `src/` files,
  `research/module_tests.md`. Disjoint (F42-c is inside `Leftovers/`); `module_tests.md` is shared docs, other sections.
- **S6** (`:390-399`): `spikes/`. Disjoint.
- **S7** (`:401-418`): deletes the `Explicit` op on `LeftoversRepositoryGuardTests.cs` and adds one op on
  `LeftoversGuardFiles.cs` (`:403-406`); both files are UNCHANGED between `21553ce` and `53f65130` (blobs `956f18ff`,
  `952cf1d6` **[M]**), so S7's ops still apply after the re-pin. S7's CI step in `build-test` sits beside T2 and relies on
  it (§ 6). Crossing: none.
- **D2** (`:169-182`, the permitted operations): the count 6 → 7 is a correction the stories' § 6 already allows ("any
  operation whose count changed corrected", `:315`); the two new entries carry the namespace op only. **D5** (`:200-203`):
  every new copied test makes its folders inside an `OwnedScope`/`OwnedDir` (the template's practice). **§ 9** (`:456`,
  `:458`): "never edits the copied files by hand" holds.

## 11. The commit series

One PR. Each commit builds; commit 1's RED is intended.

1. **RED** — `template-manifest.json` (commit, about, nine blobs, the count 7, the two entries); `TheAdaptedCopyTests.cs:79`;
   the two `.gitattributes` lines. Run C1.
2. **GREEN** — the eleven copied files, written by the § 4.1 script (and the 44 unchanged ones byte-identical). Run C2,
   then C3–C5 by hand, L1–L3, D2 (with the mount's dependencies installed).
3. **CI** (T2) — the step of § 6. Run D1 (the RED without the dependencies) and record it; K2.
4. **Case 15 and docs** (T4, T5) — § 7.1; `research/module_tests.md`; the stories (the § 6 amendment citing entry 9.1, and
   the progress); `todo/README.md` (§ 14). K1 before the PR is opened.
5. **Promotion, last** (T5) — this brief and the S2 brief to `research/` (§ 14), `git mv` last, after CI is green on
   commit 4's head.

Size: about 1,100 copied lines changed (script-made) and about 60 hand-written lines (the manifest's new entries and
about, one test line, CI, case 15's reason, docs). One Sonnet coder; no split.

## 12. The coordinator's decisions (2026-11-16)

The brief's first version asked five questions; the coordinator answered each. The questions are kept in one line each,
with the decision.

1. **Q1 — Is widening F42-c's Files list a plan change? — YES.** The brief had recommended "not a change". The question
   consultants (consult `57ac1bd6`) both said it is: § 6's "No benchmark-own file changes"
   (`todo/PLAN_test_leftovers_E6.md:315`) is an explicit scope line. Recorded as entry **9.1** of the parent's ledger
   (conventions PR #172; kinds `scope, approach`). Consequences, all in this brief: § 0.1 names the five touches T1–T5 and
   nothing else; F42-c's PR amends the stories' § 6 sentence to name T1–T5 and cite entry 9.1 (§ 14); any further file needs
   a new decision, and the next accepted change is the ninth full stop.
2. **Q2 — Case 15's Windows reason. — Rewrite the reason, keep the skip** (benchmark has no Windows leg). Touch T4; the
   text is § 7.1.
3. **Q3 — An op for the differential test's skip text ("mcp's rules pin", T:`:16`)? — No op.** The text shows only while
   the mount predates F41, and benchmark's does not.
4. **Q4 — The open-PR cap (five Dependabot PRs open). — Settled:** the owner allowed every E6 PR over the cap (2026-11-16).
5. **Q5 — Promotion. — Promote this brief to `research/` in the PR's last commit; and in the same PR repair the S2 brief's
   stale status line** (it still says "plan only, nothing implemented yet"; S2 merged as #207 `84d4953e` on 2026-11-12) **and
   promote it the same way.** Why the S2 brief moves too: no brief of this repository has been promoted yet (`ls research |
   grep -i brief` → nothing; no rename into `research/` in `git log`) **[M]**, so there is no brief-specific convention, and
   `common/planning-docs.md` decides: a plan whose status is `IMPLEMENTED` is never left in `todo/` (the lifecycle check is
   red on it), so a repaired status line means a move. Both are T5; how, in § 14.

## 13. Commands (from the worktree root; WSL, no Docker, no `.exe`)

```bash
# the mount's locked dependencies (the resolver needs them; so do D2 and the after-count)
npm ci --ignore-scripts --prefix .agents/conventions
# build (this machine: lowered, -m:4; one build at a time)
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64
dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"
nice -n 19 $T --filter-class "*LeftoversGuardDifferentialTests"
nice -n 19 $T --filter-method "*Marker_lines_read_the_same_in_the_mounted_node_guard_once_it_knows_the_marker"
nice -n 19 $T --filter-class "*LeftoversGuardTests"
nice -n 19 $T --filter-class "*LeftoversAuditTests"
nice -n 19 $T --filter-class "*OwnedTests"
nice -n 19 $T --filter-class "*LeftoversSweepCase15Tests"
nice -n 19 $T --filter-class "*StartOfRunSweepTests"
nice -n 19 $T --filter-namespace "Bench.Tests.Leftovers"
BENCH_LEFTOVERS_SELFTEST=1 nice -n 19 $T --filter-class "*LeftoversSelfTest"; echo "exit $?"
BENCH_LEFTOVERS_SELFTEST=failed-dispose nice -n 19 $T --filter-class "*LeftoversSelfTest"; echo "exit $?"
nice -n 19 $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"     # information only (S7 turns it on)
nice -n 19 $T                                                                       # the whole suite, W1
# the Node guard, before and after: 209 and 3
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1
node .agents/conventions/tools/plan-lifecycle.mjs
```
`node .agents/conventions/tools/low-priority.mjs <command>` (PROJECT.md) is the same lowering; either is fine on Linux.
Binaries must be the ones just built. The commands this brief's **[M]** rest on, all read-only:
```bash
git -C /home/jinx/git/dew_flow_mcp diff --name-status 21553ce 53f65130
git -C /home/jinx/git/dew_flow_mcp diff 21553ce 53f65130 -- tests/Mcp.Tests/Leftovers .github/workflows/ci.yml
git -C /home/jinx/git/dew_flow_mcp ls-tree -r --name-only 53f65130 tests/Mcp.Tests/Leftovers | wc -l      # 53
# a scratchpad Node script: per manifest entry, rev-parse <commit>:<upstream>, cat-file the blob, apply the ops
# forward counting each old text; at 21553ce every count is as listed, at 53f65130 only op 2 of LeftoversAuditTests.cs
# differs (6 → 7) and nine blobs change
git grep -n -w -E 'GuardScenario|GuardScenarios|LinkCountOf|ClearReadOnlyBelow|RootExists|UnreadableRoot|NodeAnswer|NodeFinding' -- tests src hosts
gh run list --repo oleksandrdubyna88/dew_flow_benchmark --branch main --limit 1     # 83f166d6 ci success 43932109912
```

## 14. The docs to update (point 6)

- **`research/module_tests.md` § *Test leftovers*** (`:632-661`):
  - `:636` *The helper*: "at `21553ce`" → "at `53f65130` (F42, mcp #81; S2 copied `21553ce`, F42-c re-synced it)"; add that
    the copied C# guard reads the `container-scoped` marker as the Node guard does (`common/test-leftovers-exemptions.md`),
    and that `LeftoversGuardDifferentialTests` holds the two guards to one scenario table by running the MOUNTED Node
    guard, which is why `build-test` installs the mount's node dependencies.
  - The audit paragraph (`:640`): an audit that cannot reach the temp root fails the run and says so (N5).
  - The limits (`:646`): the read-only git objects line is no longer true. Replace it with: on Windows the copied removal
    clears read-only files below a folder before the delete, never entering a link, and keeps a file with a second hard
    link (N1, observed by mcp on `windows-latest`, run `43928795620`; not observed here, no Windows leg); case 15 still
    skips on Windows, with the reason of § 7.1 (T4).
  - Commands (`:651-659`): add `--filter-class "*LeftoversGuardDifferentialTests"` and the line that it needs
    `npm ci --ignore-scripts --prefix .agents/conventions` first.
  - Pitfall (S2 brief § 12): no table row whose first cell starts with a backticked `bench ` (`ModuleTestsCatalogTests`).
- **The stories** (`todo/PLAN_test_leftovers_E6.md`) — first the amendment that entry 9.1 makes, then the progress:
  - **§ 6 *Before S3*, the amended scope (entry 9.1).** Replace "No benchmark-own file changes, and no copied file is
    edited by hand." (`:315-316`) with: "No copied file is edited by hand. Beyond the manifest targets and
    `template-manifest.json`, five touches and no others (the parent's entry 9.1, 2026-11-16): the commit pin in
    `TheAdaptedCopyTests.cs:79`; the `build-test` step `npm ci --ignore-scripts` in `.agents/conventions`; two
    `.gitattributes` `-text` lines; case 15's Windows skip reason; the docs." and widen the **Files.** line (`:319`) to
    the same five. Cite entry 9.1 by its ledger line. The stories keep no ledger of their own
    (`common/plan-change-control.md`), so nothing else records it here;
  - one line under § 6 *Before S3* naming this brief (by its `research/` path after the promotion);
  - the status line (`:3-11`): "F42-c merged (#…): the copy re-synced to mcp `53f65130`; its scope widened by the parent's
    entry 9.1";
  - row E6-e's state (`:51`): "re-run by F42-c (#…): 12 red at commit 1, green at `53f65130`, one copied byte → red";
  - row E6-f1's state (`:52`): F41-b (#216) and F42-c (#…) are met, so it is **proposed** (S3 writes and runs it), no
    longer blocked;
  - § 5 (`:252`) and § 12 item 5 (`:498-500`): F42-c merged;
  - § 11 (`:475`): "(`21553ce` until F42-c)" → "(`53f65130` since F42-c, #…)"; the box stays open (it holds F29 and F31 too).
  - The status line also stops naming the S2 brief by its `todo/` file name once that brief moves (`:6`, "brief
    `PLAN_test_leftovers_E6_S2_brief.md`" → its `research/` path).
- **`todo/README.md`**: the E6 row (`:99`) gets the same progress words; the two brief rows (`:100`, `:101`) are removed by
  the promotion.
- **The promotion of both briefs (Q5), the PR's last commit**, in the order of `common/planning-docs.md` § *The git mv
  goes LAST*: every edit while the files are still in `todo/`, then `git mv`, `git add` of the destinations, and ONE
  commit.
  - This brief: status `> Status: **IMPLEMENTED, <merge date>.**` with its deviations (what shipped differently from this
    brief: the RED counts actually seen, anything K1 found, any analyzer or format finding and how it was settled).
  - The S2 brief: status `> Status: **IMPLEMENTED, 2026-11-12** (merged as #207 \`84d4953e\`).` — a status repair, not a
    plan change — with its deviations read from #207's body and its § 19 (the coordinator's decisions: `.gitattributes`
    lines, the F31 pin in S2, case 15's RED by mutation, no start-of-run switch); "checked: #207's PR body and the brief's
    § 19" names the edges of that search.
  - `research/README.md`: one row each, in the shape of the other design records there (`:39-45`: "Design record,
    IMPLEMENTED <date> — …"), naming the stories they serve.
  - Links: neither brief holds a markdown link (`grep -c '](' ` → 0 for both **[M]**); the inbound references are the
    stories' status line (`:6`, a backticked name, fixed above) and this brief's own backticked paths to the S2 brief
    (§ 1, the S2 brief bullet), rewritten to `research/` before the move. Run `plan-lifecycle.mjs` after the commit.
- **Not in this repository**: the parent's row F42-c (`dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` § *Epoch
  9* item 3) becomes **observed** with this PR's runs (C1–C3), written by the coordinator in conventions. Entry 9.1 is
  already recorded there (conventions PR #172); this PR cites it and adds nothing to the parent.

## 15. Done when, and what the PR body quotes

- `TheAdaptedCopyTests` green with 55 entries at `53f65130`: C1's 12 reds (commit 1), C2's 58 greens, C3–C5's reds by hand.
- `LeftoversGuardDifferentialTests` 3 of 3 green locally with the marker fact run, not skipped (D2); D1's RED without the
  mount's dependencies; D3: `build-test` green on the PR head, with its run quoted.
- The Leftovers namespace 0 failed, its counts against S2's (L1); case 15 and the gate tests green (L2, L3); the audit's
  teeth as on `main` (L4).
- The whole suite: no new failure against the base, both FAILED lists diffed (W1); format clean, no new warning (W2).
- The Node guard 209 and 3 before and after (G1); the C# guard's Explicit count before and after, as information.
- `LeftoversRepositoryGuardTests` still `Explicit`; no copied file edited by hand; the PR's diff outside the manifest
  targets and `template-manifest.json` is T1–T5 of § 0.1 and nothing else (`git diff --name-only <base>..HEAD` quoted).
- K1 (S3–S7 re-read on the PR's base, what was read listed) and K2 (the `npm ci` leaves the trees clean) recorded.
- The docs of § 14 written, the stories' § 6 amended with entry 9.1 cited; both briefs promoted; `plan-lifecycle.mjs`
  clean; the PR body cites entry 9.1 (conventions PR #172).
