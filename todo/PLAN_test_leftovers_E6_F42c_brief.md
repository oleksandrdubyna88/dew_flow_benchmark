# E6 F42-c brief — the adapted C# copy re-synced to mcp's F42 commit (a prerequisite PR of E6.S3)

> Status: **plan only, nothing implemented yet, 2026-11-16; revised the same day to the parent's epoch 10 (§ 0.2), with
> every claim this PR relies on observed in a scratch before its plan gate (§ 10.1); the coordinator's decisions folded
> in (§ 12).** The brief of the prerequisite PR F42-c of `todo/PLAN_test_leftovers_E6.md` (§ 6 *Before S3*, restated by
> the parent's row F42-c and item 4.1; § 5 row "— (F42-c)"; D2). Its scope is item 4.1's named paths (§ 0.1); the
> parent's entry 9.1 found the need, its § *Epoch 10* settled the bound.
>
> *Was (until the epoch-10 revision):* "plan only, nothing implemented yet, 2026-11-16; the coordinator's decisions folded
> in the same day (§ 12). … Its scope is widened by the parent's entry 9.1 (§ 0.1)."
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
>
> **Epoch-10 revision (2026-11-16), new commits on the pushed branch, never a rewrite.** The branch merged benchmark's
> `main` `191ef77f` (#217, E6's stories amended to the parent's epoch 10; docs only: `git diff --stat 83f166d6 191ef77f --
> tests .github .gitattributes .agents src hosts` is empty **[M]**, so every B: reference to code still holds). "Stories"
> line numbers below are re-read at `191ef77f`. The parent is read at conventions `main` `af607536`
> (`todo/PLAN_test_leftovers_rollout.md` § *Epoch 10*: row F42-c, items 4.1, 8.1, 8.3, and the re-review record's cut row
> for this brief). **[O]** marks a claim OBSERVED for this revision in a scratch clone (§ 10.1 names the six things of
> each); **[R]** a statement read from code and not run, as the parent marks it.

## 0. What you build, in one paragraph

You bring benchmark's adapted copy of mcp's C# leftovers helper (`tests/Bench.Tests/Leftovers/`, copied by E6.S2 at
`21553ce`) to mcp's F42 commit `53f65130`. Between the two commits mcp changed nine of the copied files and added two
(`LeftoversGuardScenarios.cs`, `LeftoversGuardDifferentialTests.cs`): the C# guard now reads the `container-scoped` marker
case for case with the Node guard of F41, a live differential test holds the two guards to one scenario table, and the
notes N1, N4 and N5 of S2's brief are fixed (N2 did not reproduce; N3 is bounded; both only in files benchmark does not
copy). You rewrite the eleven files from the template under the SAME manifest operations (one count changes), add the two
new files as manifest entries, re-pin the manifest to `53f65130`, and move the one line of `TheAdaptedCopyTests` that pins
the manifest's commit. The differential test runs the MOUNTED Node guard, which needs Node 20 or later and reads workflows
through the mount's `yaml` package, so `build-test` gets ONE step before the *Test* step: Node's version printed and
checked, then `npm ci --ignore-scripts` in `.agents/conventions` (mcp's CI got the `npm ci` for the same reason). You
rewrite case 15's Windows skip reason, update the docs (progress only), and promote this brief and the S2 brief. ONE pull
request, in this repository only. No copied file is edited by hand; no ownership, liveness or deletion code is written
here.

## 0.1 Scope — the named paths of the parent's item 4.1, each with the dependency that needs it

The stories' § 6 *Before S3* used to say "No benchmark-own file changes" (claim C1 there); the parent's entry 9.1 found
that F42-c cannot keep it, and its § *Epoch 10* item 4.1 replaced the boundary by a list of NAMED PATHS, each with the
dependency that needs it, proved complete by a run from a clean checkout (row F42-c's checks (1)–(4b)); never a count
and never a bare "no other file". The stories carry the same list since #217 (`todo/PLAN_test_leftovers_E6.md:371-402`,
at `191ef77f`). This PR touches these paths, each within its bound, and no other:

| # | Path | Bound (row F42-c (4b)) | The dependency that needs it |
|---|---|---|---|
| — | `tests/Bench.Tests/Leftovers/template-manifest.json` and its targets | the manifest changes only its `commit`, its `about` (as § 4 writes it), the nine blob SHAs, the `mcp tests:` count 6 → 7 and the two new entries; the targets are the template's bytes under the listed operations; no new kind of operation | D2: the copy is of one template commit, now F42's (§ 3, § 4) |
| T1 | `tests/Bench.Tests/Leftovers/TheAdaptedCopyTests.cs` | `:79` only | it pins the manifest's `commit` (§ 4) |
| T2 | `.github/workflows/ci.yml` | job `build-test` only, ONE step, before the *Test* step | the copied differential test runs the mounted Node guard: Node 20 or later, and the mount's `yaml` (§ 6) |
| T3 | `.gitattributes` | one `-text` line per new copied file (two) | S2's standing decision: one line per copied file (§ 4.1 step 3) |
| T4 | `tests/Bench.Tests/Leftovers/LeftoversSweepCase15Tests.cs` | the Windows reason's text only: `:19-21` and the doc comment `:14-15`; the skip stays | the copied N1 fix makes today's reason untrue (§ 7.1) |
| T5 | the docs: `research/module_tests.md`, `research/README.md`, `todo/README.md`, the stories (progress lines only: their § 6 boundary is already amended by #217), this brief and the S2 brief (their promotion to `research/`) | § 14 | they must say what exists |

T1–T5 stay as names for the touches; they are no longer a count. **A file the clean-checkout run (§ 10, CC1) or the build
needs outside these paths is found BEFORE the PR, by that run; adding it is then a change of the parent's epoch 10 and
counts there** (the parent's item 4.1; epoch 10 is open with 0 changes, so it would be entry 10.1). If the build, the
format check, an analyzer, a test or a reviewer asks for any other file (a `.csproj`, an `.editorconfig` section, another
own file, another workflow line, `.agents/PROJECT.md`), STOP and report; do not add it. The ones found so far are
decided in § 16 (none enters this PR).

*Was (epoch 9, cut by the parent's re-review record, this brief's cut row; fixed forward here):* "Scope — exactly five
touches beyond the copy, by the parent's entry 9.1 … this PR touches exactly these five, and nothing else", with T5
including "the stories (their § 6 sentence amended to name T1–T5 and cite entry 9.1 …)", and "Anything beyond T1–T5
needs a new decision first, and the next accepted change to the parent is the SECOND of epoch 9: the ninth full stop".
The bound by count became item 4.1's paths; the stories' § 6 was amended by #217, not by this PR; the ninth stop came
from entry 9.2 (E7), and the next counted change is now 10.1.

## 0.2 What the epoch-10 revision changed in this brief

| What | Why (source) | Where |
|---|---|---|
| § 0.1's bound by count → item 4.1's named paths | the parent's item 4.1 and item 8.3; the re-review record's cut row for this brief | § 0.1 |
| § 14 no longer amends the stories' § 6 | #217 amended it (the parent's item 5) | § 14 |
| the clean-checkout run (row F42-c (1)) and Node's printed version (row F42-c (3)) join the checks | row F42-c; item 8.3 | § 6, § 10 (CC1, D3) |
| D1's RED runs in a fresh isolated checkout, or with the mount's `node_modules` moved aside and `test ! -e .agents/conventions/node_modules/yaml` asserted; then installed and green | coai plan gate `0cb22033`, finding (a), accepted | § 10 D1, § 13 |
| merge facts (dates, "merged", the merge SHA) are written after the merge, never in a pre-merge commit | coai plan gate `0cb22033`, finding (b), accepted | § 11, § 14 |
| C1's count, spelled out: 8 blob messages + `LeftoversAuditTests.cs` failing at op 2 + 2 missing targets = 11 theory rows, + `EveryLeftoversFile_…` = 12 red of 58 | the Opus review | § 10 C1 |
| the CI step (T2) folds into the copy commit, so no pushed commit between them is red for want of it | the Opus review | § 11 |
| the build runs right after the copy commit, before anything else; a new warning stops the work (xunit.v3 4.0.3 here vs mcp's 4.0.0, analyzers, `TreatWarningsAsErrors`) | the Opus review | § 11, § 10 W2 |
| `.agents/PROJECT.md` *Commands* lacks the mount's `npm ci` line the leftovers tests need; row E6-c's N1 sentence becomes untrue once N1's fix is copied | the Opus review | § 16 (the first is outside item 4.1's paths: a decision), § 14 (the second, as progress) |
| every claim this PR relies on observed in a scratch, with the six things | the parent's item 8.1 | § 10.1 |
| CC1 and R4 run on the FINAL head (after the promotion commit), their results on the PR, never committed; no pre-merge progress text claims checks (1)–(4b) ran | coai document gate (interim re-review, `proceed`), finding G1, accepted | § 10, § 11, § 14, § 15 |
| the stories' `research/` path rewrites and both index updates (`todo/README.md` rows removed, `research/README.md` rows added) move into the promotion commit with the two `git mv`s | the same gate, finding G2, accepted | § 11, § 14 |
| § 13's fresh-checkout commands show the build and D1's RED run BEFORE `npm ci`, then the install and the green rerun | the same gate, finding G3, accepted | § 13 |
| § 16 decided: PROJECT.md out (`module_tests.md` carries the line), E6-c in its *State* cell only, no 10.1 | the coordinator, 2026-11-16 | § 14, § 16 |

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; warnings are errors).
- `.agents/conventions/common/test-leftovers.md`, its detail (§1–§4, §6 (b), §7) and the companion
  `.agents/conventions/common/test-leftovers-exemptions.md` (the `container-scoped` kind, F41; in the mount since `b1746499`).
- The stories, at `191ef77f`: § 1 rows E6-c (`:67`), E6-e (`:69`) and E6-f1 (`:70`); § 1 *The claims*, C1–C10
  (`:115-139`); § 4 D2 (`:218-231`); § 5 (`:301`, `:309-323`); § 6 *Before S3* (`:364-423`: the named paths, the
  permitted transformations, the `about` permission, checks (1)–(4b)); § 9 (`:585`, `:587`); § 11 (`:604-606`); § 12
  item 5 (`:629-631`). (At `83f166d6` these were `:51`, `:52`, `:169-182`, `:252`, `:263-269`, `:310-320`, `:456`,
  `:458`, `:475`, `:498-500`.)
- The parent, `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` § *Epoch 10*: row F42-c (item 3), items 4.1,
  8.1 and 8.3, and the re-review record's two rows for this brief.
- The S2 brief, `todo/PLAN_test_leftovers_E6_S2_brief.md`: § 4.1 (the operations), § 5 (the manifest and the check),
  § 14 (commands), § 17 (N1–N5), § 19 item 1 (`.gitattributes`).
- `common/planning-docs.md` (the docs and the promotion), `common/plan-change-control.md` (§ 0.1 and § 16: a file outside
  item 4.1's paths is a change of epoch 10, entry 10.1).
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
   Met: #172 merged as `745d7204` **[M]** (`gh pr view 172`).
5. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows · actionlint`); the coai
   code gate and the Opus review are done. F42-c merges BEFORE E6.S3's code (stories § 5).
6. **Epoch 10, added by the revision.** E6's stories amendment merged before this brief's plan gate (the parent's item
   5): met, #217 `191ef77f` **[M]** (`gh pr view 217`), merged into this branch. Every claim of this brief that the PR
   relies on is observed before the plan gate (the parent's item 8.1): § 10.1. The parent's epoch 10 is open with 0
   changes (conventions `af607536`, status line).
7. **`main`'s own CI at the base, read for this revision.** Run `43957154320` on `191ef77f` (#217, docs only): `build-test`
   **failure**, one test, `Bench.Tests.ModelArms.StepRunnerTests.An_operator_stop_during_the_steps_own_git_call_leaves_a_stopped_line_not_an_exception`
   ("Expected type to be … StepOutcome+Stopped … but found … StepOutcome+Done"); `contract`, `plans` and `workflows`
   success **[M]** (`gh run view 43957154320`). `83f166d6`'s run `43932109912` was green with the same code (§ 0, the empty
   code diff), so it reads as a timing-dependent test outside `Leftovers/`, NOT diagnosed here and not this PR's to fix.
   If it fails again on the PR head, the coordinator decides (re-run, or a finding to its owner); the W1 diff of FAILED
   lists (§ 10) shows whether it is new. *Decided 2026-11-16:* the coordinator re-ran the failed job; not F42-c's scope.

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

The `about` may change only as this brief writes it (stories `:386-387`, the parent's row F42-c (4b): "its `about` as the
brief writes it", "and in no other way"): the text above, nothing else. Observed with exactly this text: `TheManifest_IsWellFormed`
green (§ 10.1, O1, O2).

**One benchmark-own line moves with the manifest:** `TheAdaptedCopyTests.cs:79` pins the commit
(`manifest.Commit.Should().Be("21553ce5c5729e57557504c4a59e0311e69bc489", "the copy is of this one template commit (plan
D2)")`). Change the SHA to `53f651309a271e7388c703e1ec73799ff6f1c76e` and the because-text to "the copy is of this one
template commit (plan D2; re-synced by F42-c)". Nothing else in that file changes: `BenchOwn` (`:32-46`) stays the twelve
files, because the two new files are manifest entries, never own files (`:130`). This is touch T1 of § 0.1 (item 4.1's
path: `:79` and nothing else of that file; S3 edits its `BenchOwn`, claim C2).

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
`actions/setup-dotnet` (`:25`). Only `build-test` runs `Bench.Tests`; `contract` (`:78`), `plans` (`:140`) and
`workflows` (`:208`, actionlint) do not, and get nothing. `node` is the runner's own (the `contract` job already relies
on it, `:128-134`); the mount has a `package-lock.json`.

**Epoch 10: ONE step, Node's version printed and checked, then the install** (the parent's row F42-c (3) and item 4.1:
"Node 20 or later on `PATH` with its version printed, then `npm ci --ignore-scripts` in `.agents/conventions`, both
before the *Test* step"; (4b): "`ci.yml` only `build-test`, one step"). The runner's Node version is in no log read so
far (the parent's item 8.2), so the step fails loudly below 20 rather than letting the differential test fail on syntax:
```yaml
      # The leftovers tests run the mounted Node guard (LeftoversGuardDifferentialTests feeds it workflow rows), which needs
      # Node 20 or later and reads a workflow only through the mount's `yaml` package: without it, YamlMissingError.
      # `npm ci` installs the mount's lockfile exactly and never writes it; --ignore-scripts, because the install needs none.
      - name: Node 20 or later, and the mounted conventions' node dependencies
        working-directory: .agents/conventions
        run: |
          node --version
          node -e "if (Number(process.versions.node.split('.')[0]) < 20) { console.error('node ' + process.version + ' is below 20'); process.exit(1); }"
          npm ci --ignore-scripts
```
Observed in the scratch clone (§ 10.1, O9): the step inserted after `:23` parses (the mount's `yaml`: `build-test`'s
steps are checkout, submodule, this step, setup-dotnet, Formatted, Build, Test, the audit step); its two node lines exit 0
on Node `v26.12.1`, and the same check with the threshold raised to 99 exits 1 with "node v26.12.1 is below 99" (the
check can fail); the Node guard's counts with the step in place stay 209 and 3 (§ 9). Not observed: actionlint and
shellcheck over the new `run:` block (neither is installed here); the PR's `workflows · actionlint` job is that check,
red or not. The Node guard reads workflows: the step must add no finding (§ 9, the after-count). This is touch T2 of
§ 0.1.

*Was (epoch 9):* the step was `name: Install the mounted conventions' node dependencies`, `run: npm ci --ignore-scripts`,
with no printed version.

**Deterministic, and it dirties nothing** (the consultant's caution; check K2 in § 10). `npm ci` installs exactly the
mount's `package-lock.json` and never writes it; `--ignore-scripts` runs no install script. Measured in this worktree
after `npm ci --ignore-scripts --prefix .agents/conventions` **[M]**: the mount's `.gitignore:2` ignores `node_modules`
(`git -C .agents/conventions status --porcelain --ignored` → only `!! node_modules/`), its `package-lock.json` is
unchanged (`git diff --quiet` → 0), `git status --porcelain` of benchmark is empty, and `git submodule status` shows the
pin `b1746499` with no `+` or dirty mark. `TheAdaptedCopyTests` reads only `tests/Bench.Tests/Leftovers/` and the
manifest (B:`TheAdaptedCopyTests.cs:25`, `:56`, `:121-124`), so nothing under `.agents/conventions` can turn it red.
Do not use `npm install`, `npm update` or a version range anywhere: they may write the lockfile.

Observed again for this revision in the fresh scratch clone (§ 10.1, O7): the same four facts after `npm ci
--ignore-scripts --prefix .agents/conventions` (`yaml` 2.9.0 installed).

**It also serves S7.** S7 adds the Node guard as a CLI step to `build-test` (stories `:524-525` at `191ef77f`); that guard reads
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
- **Observed again for the epoch-10 revision** (§ 10.1, O8): before, on this branch at `1abde6e5` (= `191ef77f` plus this
  brief; code equal to `83f166d6`) → **209** and **3**; after, in the fresh scratch clone with the eleven files written,
  the two `.gitattributes` lines, the manifest and `:79`, AND the § 6 step in `ci.yml` → **209** and **3**. The CI step
  adds no finding.
- The copied C# guard (Explicit) by hand, before and after, as information for S3–S7 (`--explicit only`, § 13); record both
  counts on the PR. Observed in the scratch (§ 10.1, O10): **173** findings before (the copy as S2 copied it) and **173**
  after (the copy at `53f65130`), the same 173 lines (`diff` of the two lists empty): 115 `raw-temp`, 22 `raw-worktree`,
  18 `legacy-name`, 18 `owner-blind`. The re-sync changes nothing the C# guard reports over benchmark.

## 10. Test scenarios (point 5)

Every RED is watched for the right reason and quoted on the PR (command and first line). Builds and runs follow § 13.

| # | Scenario | Expected | RED |
|---|---|---|---|
| C1 | `TheAdaptedCopyTests` at commit 1 (§ 11): the manifest re-pinned and the `:79` pin moved, the copied files still `21553ce`'s | **12 red of 58**, counted: **8** theory rows with the blob message ("with the listed replaces undone, the git blob is <old>; the manifest says <new>": `LeftoversAudit.cs`, `LeftoversAuditException.cs`, `LeftoversGuard.cs`, `LeftoversGuardExemptions.cs`, `LeftoversGuardTests.cs`, `Native.cs`, `OwnedTests.cs`, `Removal.cs`) + **1** row failing at an operation's count (`LeftoversAuditTests.cs`: "operation 2 of 3 ('mcp tests:' -> 'bench tests:') expected 7 occurrence(s) of the new text, found 6 at line(s) 220, 229, 230, 247, 268, 273") + **2** rows of the missing targets (`LeftoversGuardDifferentialTests.cs`, `LeftoversGuardScenarios.cs`: `FileNotFoundException`) = **11** theory rows, + `EveryLeftoversFile_IsInTheManifestOrIsBenchsOwn` (two targets missing) = **12**; 46 green, among them `TheManifest_IsWellFormed` and the count fact. **Observed** (§ 10.1, O1) | this IS the red; record the count |
| C2 | the same at commit 2 (the eleven files written, and the CI step, § 11) | **58 of 58 green** (55 rows + 3 facts). **Observed** (O2), with the mount's `node_modules` absent and again present (K2) | — |
| CC1 | **Row F42-c (1), from a CLEAN checkout of the FINAL PR head** (after the promotion commit, § 11 commit 4; coai document gate, finding G1): a fresh clone; `git submodule update --init` (the mount only, `.gitmodules`); `npm ci --ignore-scripts` in `.agents/conventions`; build; then `TheAdaptedCopyTests`, C3 and C4 there | 58 of 58 green with 55 entries; one copied byte → that row red; the count back to 6 → red at op 2. A file this run needs outside § 0.1's paths stops the PR (§ 0.1, § 16). **Its results go in the PR body or a PR comment, never in a committed file** (no commit comes after the head it ran on) | the reds are C3 and C4, run in that clone. The same sequence ran on a scratch stand-in of the head (O1–O5): no file outside the paths was needed for the build or these tests |
| C3 | one copied byte changed (a character in a comment of the NEW `LeftoversGuardScenarios.cs`) | its row red, naming the file and both SHAs; with `BENCH_TEMPLATE_REPO=/home/jinx/git/dew_flow_mcp` the first differing line is named. **Observed** (O3): 1 red of 58, "the git blob is bad862a2…; the manifest says 8da3abb6…", "First differing line 3" | by hand, restored (the stories' and the parent row F42-c's counterexample) |
| C4 | the `count` of `mcp tests:` set back to 6 | red at op 2 ("expected 6 … found 7"). **Observed** (O4): 1 red of 58, "operation 2 of 3 … expected 6 occurrence(s) of the new text, found 7 at line(s) 249, 451, 460, 461, 478, 499, 504" | by hand, restored |
| C5 | a stray `tests/Bench.Tests/Leftovers/Stray.cs` | `EveryLeftoversFile_…` red naming it. **Observed** (O5): 1 red of 58, naming `Stray.cs` | by hand, removed |
| D1 | `--filter-class "*LeftoversGuardDifferentialTests"` with the mount's `node_modules` ABSENT, in a FRESH ISOLATED checkout (a new clone, `git submodule update --init`, before any `npm ci`), asserting `test ! -e .agents/conventions/node_modules/yaml` first. In an existing worktree instead: move `.agents/conventions/node_modules` aside (never delete it, never `npm uninstall`), assert the same `test ! -e`, run, move it back. (coai plan gate `0cb22033`, finding (a)) | the two node facts red: "node said: … YamlMissingError …". **Observed** (O6): exit 2, 2 red of 3 (both node facts), "YamlMissingError: leftovers-check: the `yaml` package (version 2) is needed to read workflows and cannot be found"; the table-shape fact green | this is the RED of § 6's CI step; record it, then `npm ci --ignore-scripts --prefix .agents/conventions` and re-run: 3 of 3 green (**observed**, O7) |
| D2 | `--filter-method "*Marker_lines_read_the_same_in_the_mounted_node_guard_once_it_knows_the_marker"` | **succeeded, not skipped** (the mount at `b1746499` exports `DECLARATION_KINDS`). **Observed** (O7): 1 succeeded, 0 skipped | — |
| D3 | CI `build-test` on the PR head (row F42-c (3)) | green; the step's log prints Node's version (20 or later), and its `npm ci` runs before the *Test* step; the mount at `b1746499` there too, so D2's fact runs, not skips. Quote the run and the printed version; if the runner's log does not name skipped tests, compare its skipped count with `83f166d6`'s run `43932109912` plus the two new Windows-only skips (L1), and say so. `main`'s run on `191ef77f` failed one ModelArms test (§ 2 item 7): a failure there again is compared, not assumed | — |
| L1 | `--filter-namespace "Bench.Tests.Leftovers"` | 0 failed; record total, passed, skipped against S2's 503 / 479 / 24 (#207); every new skip is Windows-only, macOS-only or the two self-tests and the Explicit guard, with its reason. **Observed** (O11): **600 / 574 / 26**, 0 failed; the two new skips are `OwnedTests.A_folder_holding_a_read_only_file_with_another_hard_link_is_kept_and_the_file_is_named` ("a read-only file stops a delete on Windows only; the hosted Windows leg runs this") and `OwnedTests.A_folder_holding_read_only_files_at_two_depths_is_removed_with_its_claim` ("a read-only attribute blocks a delete on Windows only; elsewhere the folder's mode decides"); N5's `LeftoversAuditTests` ran (27 passed, not root) | — |
| L2 | `--filter-class "*LeftoversSweepCase15Tests"` | 3 of 3 green on Linux (case 15 unchanged on Linux: `ClearReadOnlyBelow` runs on Windows only). **Observed** in L1's run (O11): the three case-15 facts passed | — |
| L3 | `--filter-class "*StartOfRunSweepTests"` | G1–G4 green; `Runs == 1`. **Observed** in L1's run (O11): the four passed | — |
| L4 | the audit's teeth, by hand (`BENCH_LEFTOVERS_SELFTEST=1`, then `=failed-dispose`) | non-zero exit and the audit's first line, as on `main`. **Observed** (O12): exit 2, "bench tests: this run left 1 folder(s) it claimed — 1 never disposed"; exit 2, "… — 1 disposed but not removed"; no selftest folder left in `/tmp` | — |
| W1 | the whole suite, once on the base (built from it) and once on the head | the two FAILED lists are identical: no new failure. Known on this machine: every class in the `"postgres"` collection (`PostgresFixture`, Testcontainers) fails, no Docker here; #207 recorded **572** such failures on its base and head (its PR body). Re-measure the base, do not assume 572. Not run for this revision (the machine is shared; the PR runs it) | — |
| W2 | **Right after the copy commit, before anything else:** the build (`nice -n 19`, `-m:4`, `--disable-build-servers`, `-p:AppHostRuntimeIdentifier=linux-x64`); then `dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore`; `git diff --check` | 0 errors, and no warning that `main` does not have (#207 recorded one, `ASPIRE010` from AppHost, the same on `main`). The risk named by the Opus review: the copied files were written against mcp's `xunit.v3` 4.0.0 and its analyzers, benchmark pins 4.0.3 with `TreatWarningsAsErrors`. **A new warning or error STOPS the work and is reported**: the answer is project-level (a `.csproj`, an `.editorconfig` section), which is a file outside § 0.1's paths, so a counted change (§ 16), never an edit of a copied file. **Observed** for the test project (O2, O13): `tests/Bench.Tests/Bench.Tests.csproj` built 0 warnings, 0 errors at commit 1's state and at commit 2's; `dotnet format tests/Bench.Tests/Bench.Tests.csproj --verify-no-changes --no-restore` exit 0; `git diff --check` clean. Not observed: the whole `.slnx` (the PR's build) | — |
| G1 | the Node guard, § 9 | 209 and 3 before and after. **Observed** (O8), the CI step included | — |
| P1 | `node .agents/conventions/tools/plan-lifecycle.mjs` | clean | — |
| K1 | **Before the PR is opened: re-read S3–S7 for any other file-scope boundary the re-sync crosses** (the consultant's caution) | none crossed beyond § 0.1's paths; the PR body lists what was read. At this brief: see the list below | a boundary found → STOP, a decision (§ 0.1, § 16) |
| K2 | **The CI `npm ci` is deterministic and dirties nothing** (the consultant's caution) | after the step, locally and in a CI step's log if the coordinator wants it: `git status --porcelain` empty, `git submodule status` without `+`, `git -C .agents/conventions diff --quiet package-lock.json` → 0; `TheAdaptedCopyTests` green with `node_modules` present AND absent (run C2 both ways). **Observed** (O7): all four, and C2 58 of 58 both ways | `npm install` in place of `npm ci` is never tried on the branch |
| R4 | **Row F42-c (4) and (4b)**, on the FINAL PR head (after the promotion commit; finding G1): `git diff --name-only <base>..HEAD` and `git diff -U0 <base>..HEAD` per path | every path in § 0.1's table (the two briefs at their `research/` paths and `research/README.md` included); each path within its bound (the manifest: `commit`, `about`, nine blobs, the count, two entries; `TheAdaptedCopyTests.cs` only `:79`; case 15 only the reason's text; `ci.yml` only `build-test`, one step; `.gitattributes` two lines; the stories only progress lines); quoted in the PR body or a PR comment, never in a committed file. The base is the PR's merge base with `main` (the brief's own commits are in the diff: this brief and `todo/README.md`, both § 0.1 paths) | a path outside → STOP (§ 16) |

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

**K1, re-read at `191ef77f` for the epoch-10 revision** (the amendment #217 moved the lines; what is new is marked):

- **S3** (`:425-468`): Files (`:463-466`) now name `TheAdaptedCopyTests.cs` for its `BenchOwn` (`:32-46`, claim C2) and
  say "F42-c touches only `:79` of the same file and merges first". Crossing: none, as before.
- **S4** (`:470-483`), **S5** (`:485-500`), **S6** (`:503-512`): as before; disjoint.
- **S7** (`:514-542`): the `Explicit` op and the one op on `LeftoversGuardFiles.cs` (`:516-518`, claim C10); both files
  still unchanged between `21553ce` and `53f65130`. Its CI step (`:524-525`) sits beside T2. Crossing: none.
- **§ 6 *Before S3*** (`:364-423`), new: it IS this PR's bound (§ 0.1): the named paths, the permitted transformations
  (D2's operations unchanged; one count, 6 → 7; the namespace op only on the two new entries; no new kind), the `about`
  permission (`:386-387`), checks (1)–(4b) (`:407-420`). This brief follows it, path for path.
- **§ 1 *The claims*** (`:115-139`), new: C1 is this PR's (replaced by the named paths: **proposed**, before F42-c's plan
  gate; completeness by row F42-c's checks). C2, C7 and C9 are observed through F42-c's copy before S3's plan gate, C3–C6
  and C10 before S7's, C8 before S4's: none of them before THIS gate, and this PR relies on none of them. The copy at
  `53f65130` built in the scratch of § 10.1 is the copy they will run through, so the S3 brief may reuse that method.

## 10.1 Observed before this brief's plan gate (the parent's item 8.1)

Every claim this brief makes about the copied tool and its checks that this PR relies on was run on 2026-11-16 in a
scratch, before the gate; each row names the six things. **Common to all rows:** consumer **benchmark `1abde6e5`** (this
branch after merging `main` `191ef77f`; its code equals `83f166d6`, § 0), cloned fresh (`git clone --no-hardlinks`) into
a scratchpad folder outside every worktree, the mount initialised from the local conventions repository at the pin
(`git submodule update --init` → `b1746499`, no `node_modules`); **upstream pin mcp `53f65130`** (the copy) and the
mount **`b1746499`**; Node `v26.12.1`, .NET SDK `10.0.114`, WSL, not root. Two scratch commits stood in for the PR's:
**`d68f18e5`** (commit 1's state: the manifest re-pinned by a scratchpad Node script, exactly as § 4 writes it, and `:79`)
and **`38445ae4`** (commit 2's state: the § 4.1 script wrote all 55 targets, `git status` showed only the eleven, so the
44 unchanged came out byte-identical; the two `.gitattributes` lines). Builds: `nice -n 19 dotnet build
tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers`, one
at a time (53 s and 55 s). `T` = `./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests`, run as `nice -n 19 $T …`. The
scratch is not kept (a reboot clears it); the numbers are here.

| # | Claim (where) | State, invocation | Fixture | Expected | Observed |
|---|---|---|---|---|---|
| O1 | C1's RED count (§ 10 C1, § 11 commit 1) | `d68f18e5`, built (0 warnings, 0 errors); `$T --filter-class "*TheAdaptedCopyTests"` | the manifest with 55 entries at `53f65130`; the copied files as S2 copied them | 12 red of 58 | exit 2; total 58, failed **12**, succeeded 46: 8 blob messages (the eight files of § 10 C1), `LeftoversAuditTests.cs` "operation 2 of 3 … expected 7 occurrence(s) of the new text, found 6 at line(s) 220, 229, 230, 247, 268, 273", 2 × `FileNotFoundException` (`LeftoversGuardDifferentialTests.cs`, `LeftoversGuardScenarios.cs`), `EveryLeftoversFile_IsInTheManifestOrIsBenchsOwn`; `TheManifest_IsWellFormed` green with § 4's `about` |
| O2 | C2, and W2's build after the copy (§ 10 C2, W2) | `38445ae4`, built: **0 warnings, 0 errors**; the same filter, the mount's `node_modules` absent | the eleven files written, 55 entries | 58 of 58 | exit 0; 58 succeeded |
| O3 | C3, the copy check has teeth (row F42-c (1)) | `BENCH_TEMPLATE_REPO=/home/jinx/git/dew_flow_mcp $T --filter-class "*TheAdaptedCopyTests"` | line 3 of `LeftoversGuardScenarios.cs`, `///` → `//x/`; restored after | that row red, both SHAs, the first differing line | exit 2; 1 failed of 58: "the git blob is bad862a2ca8d…; the manifest says 8da3abb6c114…", "First differing line 3: template '/// <summary>…' vs copy … '//x/ <summary>…'" |
| O4 | C4, a count set back is refused (row F42-c (1)) | the same filter | `template-manifest.json`, `mcp tests:` count 7 → 6; restored after | red at op 2 | exit 2; 1 failed of 58: "operation 2 of 3 ('mcp tests:' -> 'bench tests:') expected 6 occurrence(s) of the new text, found 7 at line(s) 249, 451, 460, 461, 478, 499, 504" |
| O5 | C5, a stray file is refused | the same filter | `tests/Bench.Tests/Leftovers/Stray.cs` (one namespace line); removed after | `EveryLeftoversFile_…` red naming it | exit 2; 1 failed of 58, naming `Stray.cs` |
| O6 | D1's RED: the differential test needs the mount's `yaml` (§ 6 *Why*, row F42-c (2)) | `test ! -e .agents/conventions/node_modules/yaml` → true (printed); `$T --filter-class "*LeftoversGuardDifferentialTests"` | the fresh clone, no `npm ci` | the two node facts red with `YamlMissingError` | exit 2; total 3, failed 2 (`Marker_lines_read_the_same_…`, `Lines_that_do_not_depend_on_the_marker_…`), succeeded 1 (the table's shape): "node said: …/leftovers-check.mjs:262 throw new YamlMissingError … the `yaml` package (version 2) is needed to read workflows and cannot be found" |
| O7 | D1 green, D2 runs, K2 dirties nothing (§ 6, row F42-c (2)) | `nice -n 19 npm ci --ignore-scripts --prefix .agents/conventions` (`yaml` 2.9.0); then D1's filter; `$T --filter-method "*Marker_lines_read_the_same_in_the_mounted_node_guard_once_it_knows_the_marker"`; C2's filter again | the same clone | 3 of 3; the marker fact succeeded, not skipped; the trees clean; C2 still 58 | D1 exit 0, 3 of 3; D2 exit 0, succeeded 1, skipped 0; `git status --porcelain` empty; `git submodule status` ` b1746499…` (no `+`); `git -C .agents/conventions diff --quiet package-lock.json` → 0; `status --porcelain --ignored` → `!! node_modules/` only; C2 58 of 58 |
| O8 | G1: the Node guard's counts unchanged (§ 9) | `node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" \| head -1`, and without `--profile node` | before: this worktree at `1abde6e5`; after: the clone at `38445ae4` plus the § 6 step in `ci.yml` | 209 and 3, both | before **209** and **3**; after **209** and **3** |
| O9 | the § 6 step: one step, Node printed and checked, before *Test* (row F42-c (3)) | the step inserted after `ci.yml:23` in the clone; read back with the mount's `yaml` (`jobs['build-test'].steps`); its two node lines run in `.agents/conventions` | `.github/workflows/ci.yml` | parses; order checkout, submodule, the step, setup-dotnet, …, Test; the check passes on 20+ and can fail | parses, that order; `node --version` → `v26.12.1`, the check exit 0; the same check with 99 for 20 → exit 1, "node v26.12.1 is below 99". NOT observed: actionlint/shellcheck (not installed), the runner's Node (the PR's D3) |
| O10 | the C# guard over benchmark, information for S3–S7 (§ 9) | `$T --explicit only --filter-class "*LeftoversRepositoryGuardTests"` at `d68f18e5`'s build and at `38445ae4`'s | the repository tree | a count each, as information | before **173**, after **173**, the same lines (`diff` empty): 115 `raw-temp`, 22 `raw-worktree`, 18 `legacy-name`, 18 `owner-blind` |
| O11 | L1–L3: the copied tests pass here, the new skips are Windows-only | `$T --filter-namespace "Bench.Tests.Leftovers" --output Detailed --no-ansi` | `38445ae4` with `node_modules` | 0 failed; new skips Windows-only; case 15 3 of 3; the gate tests green | exit 0; **600 / 574 / 26** (S2: 503 / 479 / 24); the 2 new skips are the two `OwnedTests` read-only cases (reasons in § 10 L1); the 3 case-15 facts and the 4 `StartOfRunSweepTests` passed; `LeftoversAuditTests` 27 passed, among them `An_audit_that_cannot_read_the_root_still_reports_what_the_registry_holds` |
| O12 | L4: the audit's teeth unchanged | `BENCH_LEFTOVERS_SELFTEST=1`, then `=failed-dispose`, `$T --filter-class "*LeftoversSelfTest"` | the self-test's own folder | non-zero, the audit's first line | exit 2, "bench tests: this run left 1 folder(s) it claimed — 1 never disposed"; exit 2, "… — 1 disposed but not removed"; nothing left in `/tmp` |
| O13 | W2: format and whitespace on the new bytes | `nice -n 19 dotnet format tests/Bench.Tests/Bench.Tests.csproj --verify-no-changes --no-restore`; `git diff --check d68f18e5~1 38445ae4` | the copy at `53f65130` | clean | format exit 0; `diff --check` exit 0 |

**Written open-ended, not observed here:** the whole `.slnx` build and `dotnet format` over the `.slnx` (the PR's W2; the
test project above holds every changed file); the whole suite (W1); CI on the head (D3: the runner's Node version, the
skip count, actionlint over the new step); the clean checkout of the REAL head (CC1; the scratch above is its stand-in,
red or not, the PR's run says which); Windows (no leg here).

## 11. The commit series

One PR. Each commit builds. Commit 1 is RED by design (C1, the copy check's RED, watched before the copy exists); it is
the only red commit, and CI runs on the pushed head, never on commit 1 alone. The CI step goes in WITH the copy (the Opus
review), so no commit holds the differential test without the step that makes it pass.

1. **RED** — `template-manifest.json` (commit, about, nine blobs, the count 7, the two entries); `TheAdaptedCopyTests.cs:79`;
   the two `.gitattributes` lines. Build, then run C1.
2. **GREEN, and the CI step** (T2 folded in) — the eleven copied files, written by the § 4.1 script (and the 44 unchanged
   ones byte-identical), AND the § 6 step in `ci.yml`. **Build right after this commit, before anything else (W2): a new
   warning or error stops the work and is reported (§ 16), never fixed by editing a copied file or a project file.** Then
   C2, D1 (the RED without the mount's dependencies, as § 10 D1 says: a fresh isolated checkout, or `node_modules` moved
   aside with `test ! -e .agents/conventions/node_modules/yaml` asserted) → `npm ci` → D1 green and D2; C3–C5 by hand;
   L1–L4; K2; G1.
3. **Case 15 and docs** (T4, T5) — § 7.1; `research/module_tests.md` (its commands carry the mount's `npm ci` line, § 14);
   the stories' progress lines that cite no `research/` brief path (§ 14); the E6 row of `todo/README.md`. No merge fact
   and no claim that checks (1)–(4b) ran (§ 14). K1 before the PR is opened.
4. **Promotion, last** (T5), after CI is green on commit 3's head — ONE commit holding everything that names a brief at
   its `research/` path (coai document gate, finding G2), so no earlier commit references a file that does not exist yet:
   both briefs' status and deviations; the stories' path rewrites (the status line's S2 brief name, the § 6 *State*
   line's citation of this brief); `todo/README.md`'s two brief rows removed; `research/README.md`'s two rows added; this
   brief's own path to the S2 brief. Inside the commit the order of `common/planning-docs.md` § *The git mv goes LAST*:
   every edit while the briefs are still in `todo/`, then the two `git mv`s, `git add` of both destinations, one commit.
   The status says `IMPLEMENTED, <the date of this commit>` and "built on `feat/e6-copy-f42`"; never "merged", a merge
   date or a merge SHA (coai plan gate `0cb22033`, finding (b)).
   **Then, on this final head:** CI green (D3), CC1 and R4, their results in the PR body or a PR comment, never committed
   (finding G1).
5. **After the merge** (not a commit of this PR): the merge facts (merged, the merge SHA, the date) are written where
   § 14 says, in a later commit on `main` through its own PR, never into a commit of this PR before the merge.

*Was (epoch 9):* five commits: 1 RED, 2 GREEN (the copy), 3 CI (T2, with D1), 4 case 15 and docs (with the stories' § 6
amendment), 5 promotion; the status written as `IMPLEMENTED, <merge date>` in commit 5.

Size: about 1,100 copied lines changed (script-made) and about 60 hand-written lines (the manifest's new entries and
about, one test line, CI, case 15's reason, docs). One Sonnet coder; no split.

## 12. The coordinator's decisions (2026-11-16)

The brief's first version asked five questions; the coordinator answered each. The questions are kept in one line each,
with the decision.

1. **Q1 — Is widening F42-c's Files list a plan change? — YES.** The brief had recommended "not a change". The question
   consultants (consult `57ac1bd6`) both said it is: § 6's "No benchmark-own file changes"
   (`todo/PLAN_test_leftovers_E6.md:315`) is an explicit scope line. Recorded as entry **9.1** of the parent's ledger
   (conventions PR #172; kinds `scope, approach`). Consequences, as revised for epoch 10: § 0.1 names item 4.1's paths,
   each with its dependency and its bound (T1–T5 are their names, not a count); the stories' § 6 was amended by #217, not
   by F42-c's PR; a file outside the paths is a change of epoch 10 (entry 10.1), found before the PR (§ 16).
   *Was (epoch 9, cut by the parent's re-review record):* "§ 0.1 names the five touches T1–T5 and nothing else; F42-c's PR
   amends the stories' § 6 sentence to name T1–T5 and cite entry 9.1 (§ 14); any further file needs a new decision, and
   the next accepted change is the ninth full stop."
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
# D1's RED and CC1, in a FRESH ISOLATED checkout (before any npm ci there); <scratch> is your scratchpad
git clone -q --no-hardlinks <this worktree> <scratch>/clone && cd <scratch>/clone
git config submodule..claude/rules/shared.url /home/jinx/git/dew_flow_conventions   # or leave the GitHub url
git -c protocol.file.allow=always submodule update --init .agents/conventions
test ! -e .agents/conventions/node_modules/yaml && echo "yaml absent"              # MUST print before D1 runs
# (in an existing worktree instead: mv .agents/conventions/node_modules <scratch>/nm-aside; the same test; run D1;
#  mv it back — never rm, never npm uninstall)
# build (this machine: lowered, -m:4, no build server; one build at a time) — right after the copy commit (W2);
# the build needs no node_modules (O1, O2 built without them)
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 --disable-build-servers -p:AppHostRuntimeIdentifier=linux-x64
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
# D1, the RED, BEFORE npm ci: expected exit non-zero, 2 of 3 red, "node said: … YamlMissingError …" (O6)
nice -n 19 $T --filter-class "*LeftoversGuardDifferentialTests"; echo "exit $?"
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"          # K2's "absent" half: 58 of 58
# the install: the mount's locked dependencies (the resolver needs them; so do D2 and the after-count)
npm ci --ignore-scripts --prefix .agents/conventions
test -e .agents/conventions/node_modules/yaml && echo "yaml present"
# D1 green again after the install: expected 3 of 3 (O7)
nice -n 19 $T --filter-class "*LeftoversGuardDifferentialTests"; echo "exit $?"
dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"
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
  - Commands (`:651-659`): add `--filter-class "*LeftoversGuardDifferentialTests"` and, above the test lines, the mount's
    install, `npm ci --ignore-scripts --prefix .agents/conventions`, with a comment that the leftovers tests
    (`LeftoversGuardDifferentialTests`) need the mount's `yaml`. This is THE place that carries the line:
    `.agents/PROJECT.md` stays out of this PR (§ 16 item 1, decided).
  - Pitfall (S2 brief § 12): no table row whose first cell starts with a backticked `bench ` (`ModuleTestsCatalogTests`).
- **The stories** (`todo/PLAN_test_leftovers_E6.md`, line numbers at `191ef77f`) — **progress lines only** (row F42-c
  (4b)); their § 6 *Before S3* is already amended by #217, so this PR does NOT amend it. **No merge fact before the
  merge** (coai plan gate `0cb22033`, finding (b)): no "merged", no merge SHA, no merge date and no PR number in any commit
  of this PR; they are written after the merge (the last bullet). **No claim that checks (1)–(4b) ran** (coai document
  gate, finding G1): they run on the final head, after the last commit, and their results live on the PR; the stories
  get them after the merge. Before the merge, in commit 3 (no `research/` path, finding G2):
  - the status line (`:3-15`): "F42-c built on `feat/e6-copy-f42` (the copy re-synced to mcp `53f65130`)";
  - the *State* line of § 6 *Before S3* (`:423`): "built on `feat/e6-copy-f42`; checks (1)–(4b) run on its final head,
    results on its PR";
  - row E6-e's state (`:69`): "run again by F42-c (`feat/e6-copy-f42`), on its final head; results on its PR";
  - row E6-c's state (`:67`, the Opus review): its *Limits* cell says "Windows: N1 (read-only git objects)", and its
    *State* cell "N1 is F42's, for the template". Once N1's fix is copied the first is untrue. The state cell (a progress
    line) gets: "Since F42-c, the copied `Removal` clears read-only files below a folder before the delete on Windows
    (N1, fixed in mcp F42 and observed there on `windows-latest`, run `43928795620`; not observed here, no Windows leg);
    the *Limits* cell's N1 sentence describes the copy before F42-c." The *Limits* cell itself is NOT edited: it is not a
    progress line, and rewriting it is outside row F42-c (4b)'s bound for this document (§ 16 item 2, decided: the
    *State* cell only);
  - claim C1's state (`:130`): unchanged before the merge (it says "proposed, before F42-c's plan gate; completeness
    proved by row F42-c's checks (1)–(4b)"; that the checks ran is a post-merge line).
  - **In the promotion commit (commit 4), with the two `git mv`s** (finding G2): the status line stops naming the S2
    brief by its `todo/` file name (`:9`, "brief `PLAN_test_leftovers_E6_S2_brief.md`" →
    `research/PLAN_test_leftovers_E6_S2_brief.md`); the § 6 *State* line (`:423`) gets "(brief
    `research/PLAN_test_leftovers_E6_F42c_brief.md`)".
  - **Not before the merge:** row E6-f1 (`:70`, it already reads "no longer blocked once F42-c merges"), § 5 (`:301`),
    § 12 item 5 (`:629-631`): their "F42-c merged" is a merge fact. § 11 (`:604`) already names `53f65130`; its box
    stays open (it holds F29 and F31 too).
  - **After the merge** (in a later commit on `main`, through its own PR; E6.S3's first commit is one such place, the
    coordinator's choice): "F42-c merged (#<n> `<sha>`, <date>)" in the status line, E6-f1, § 5 and § 12 item 5, and the PR
    number where the lines above say `feat/e6-copy-f42`; the results of checks (1)–(4b) as the PR quotes them (C1's
    state, the § 6 *State* line, row E6-e: C1 12 red of 58 at commit 1, 58 green at `53f65130`, one copied byte → red,
    the count back to 6 → red, D1 red without the mount's `yaml` and 3 of 3 with it, CC1 and R4 on the final head); and
    this brief's promoted status gets "merged as #<n> `<sha>`".
- **`todo/README.md`**: the E6 row (`:99`) gets the same progress words in commit 3 (no merge fact, no checks claim);
  the two brief rows (`:100`, `:101`) are removed in the promotion commit (finding G2).
- **The promotion of both briefs (Q5), the PR's last commit**, in the order of `common/planning-docs.md` § *The git mv
  goes LAST*: every edit while the files are still in `todo/` (the two statuses, the stories' two path rewrites above,
  `todo/README.md`'s two rows removed, `research/README.md`'s two rows added, this brief's path to the S2 brief), then
  the two `git mv`s, `git add` of the destinations, and ONE commit (finding G2: no commit before it names a
  `research/` brief path, no commit after it exists in the PR).
  - This brief: status `> Status: **IMPLEMENTED, <the date of the promotion commit>** (built on `feat/e6-copy-f42`).`
    with its deviations (what shipped differently from this brief: the RED counts actually seen, anything K1 or CC1 found,
    any analyzer or format finding and how it was settled). No "merged", merge SHA or merge date: those come after the
    merge (above). *Was (epoch 9):* `IMPLEMENTED, <merge date>`.
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
  10* item 3; it was § *Epoch 9* item 3) becomes **observed** with this PR's runs (checks (1)–(4b): C1–C4, CC1, D1–D3,
  R4), written by the coordinator in conventions. Entry 9.1 is already recorded there (conventions PR #172); this PR
  cites it and adds nothing to the parent.
- **Not in this PR (decided, § 16 item 1):** `.agents/PROJECT.md` § *Commands*; `research/module_tests.md` carries the
  mount's `npm ci` line instead.

## 15. Done when, and what the PR body quotes

- `TheAdaptedCopyTests` green with 55 entries at `53f65130`: C1's 12 reds (commit 1), C2's 58 greens, C3–C5's reds by hand.
- `LeftoversGuardDifferentialTests` 3 of 3 green locally with the marker fact run, not skipped (D2); D1's RED without the
  mount's dependencies; D3: `build-test` green on the PR head, with its run quoted.
- The Leftovers namespace 0 failed, its counts against S2's (L1); case 15 and the gate tests green (L2, L3); the audit's
  teeth as on `main` (L4).
- The whole suite: no new failure against the base, both FAILED lists diffed (W1); format clean, no new warning (W2).
- The Node guard 209 and 3 before and after (G1); the C# guard's Explicit count before and after, as information.
- `LeftoversRepositoryGuardTests` still `Explicit`; no copied file edited by hand; the PR's diff lies within § 0.1's
  named paths, each path within its bound (R4: `git diff --name-only <base>..HEAD` and `git diff -U0` per path quoted).
- CC1: row F42-c (1) from a clean checkout of the FINAL head (after the promotion commit), with what it needed (nothing
  outside § 0.1's paths, or a stop); CC1 and R4 quoted in the PR body or a PR comment, not committed (finding G1).
- D3: the CI log's printed Node version (20 or later) quoted with the run.
- K1 (S3–S7 re-read on the PR's base, what was read listed) and K2 (the `npm ci` leaves the trees clean) recorded.
- The docs of § 14 written (progress only, no merge fact before the merge); both briefs promoted; `plan-lifecycle.mjs`
  clean; the PR body cites entry 9.1 (conventions PR #172) and the parent's § *Epoch 10* item 4.1.
- § 16's items as decided (PROJECT.md left out; E6-c in its *State* cell only), said so on the PR.

*Was (epoch 9):* "the PR's diff outside the manifest targets and `template-manifest.json` is T1–T5 of § 0.1 and nothing
else"; "the stories' § 6 amended with entry 9.1 cited".

## 16. Outside item 4.1's paths — found, and decided by the coordinator (2026-11-16): no change 10.1

Found by the epoch-10 revision. Adding any of them to this PR would have been a change of the parent's epoch 10 (entry
10.1), per item 4.1. **The coordinator's decisions (2026-11-16):** (1) `.agents/PROJECT.md` is left OUT;
`research/module_tests.md`, inside the named docs, carries the mount's `npm ci` line (§ 14); no 10.1. (2) Row E6-c is
corrected in its *State* cell only, as § 14 writes it; no 10.1. (3) The stop-on-new-warning rule stands. (4) `main`'s
failed ModelArms test (§ 2 item 7): the coordinator re-ran the failed job; not F42-c's scope. The PR says so. The
findings below are kept as written before the decisions.

1. **`.agents/PROJECT.md` § *Commands*** (the Opus review). The project's command list (`.agents/PROJECT.md:24-88` at
   `191ef77f` **[M]**, read; the build `:30-31`, the test runner `:33-39`) has the build and the test runner but no line that installs the mount's node dependencies,
   and after this PR the leftovers tests need them: `LeftoversGuardDifferentialTests` is red without the mount's `yaml`
   (O6). The shared entry names the step for the resolver (`.agents/conventions/ENTRY.md` step 3), but not for the tests.
   What would go in: one line, `npm ci --ignore-scripts --prefix .agents/conventions   # the mounted conventions'
   locked dependencies: the resolver and the leftovers tests (LeftoversGuardDifferentialTests) need them`, above the
   test-runner lines. `research/module_tests.md` gets the same line within the paths (§ 14); the question is only
   PROJECT.md. Not needed by the build or by the clean-checkout run (O1–O13 ran without it): a documentation need, not a
   dependency of the PR's checks. Options: (a) add it here as entry 10.1; (b) leave it to S7, which closes E6's docs, as
   that story's own scope (S7's Files do not name PROJECT.md today, so that is a stories matter for S7's brief); (c)
   leave it out, `module_tests.md` carrying the line.
2. **Row E6-c's *Limits* cell in the stories** (`todo/PLAN_test_leftovers_E6.md:67`, the Opus review): "Windows: N1
   (read-only git objects)" becomes untrue once N1's fix is copied. Row F42-c (4b) bounds this PR's edit of the stories
   to progress lines, so § 14 corrects it in the row's *State* cell (progress) and leaves the *Limits* cell. Rewriting
   the *Limits* cell itself is outside that bound: the coordinator decides whether it waits for a later stories
   revision or is a change of epoch 10.
3. **A build or analyzer finding on the new bytes** (W2). None was seen for `Bench.Tests.csproj` (O2: 0 warnings, 0
   errors with `xunit.v3` 4.0.3 and `TreatWarningsAsErrors`); the whole `.slnx` is the PR's build. If one appears, its
   answer is a project file, outside the paths: stop and report (§ 0.1), never edit a copied file.
