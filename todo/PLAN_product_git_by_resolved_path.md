# PLAN — The product launches git by its resolved path, under an environment it built, from a directory it chose, with the repository's own configuration unable to name a program

> Status: **plan only, not approved, waits for the owner — nothing implemented, 2026-10-30.** Scope: every launch of
> `git` (and of any other tool by a bare name) under `src/` and `hosts/` — `GitCommand` and its 32 call sites, the two
> direct launches in `ScoutTrees` and `GitBlobBatch`, and the hook's own `Process.Start("git")` — plus the move of the
> model-arms module's generic pieces (executable resolution, kernel real path, the `GIT_*` strip) into the product so the
> module and the product share one implementation. Not a release, not a measurement. The owner said: *create the plan,
> that's all; I'll deal with it later.* The coai plan round (`mcp__coai__review_plan`) has NOT been run on this
> document; it is the first step of the build order (§7), not of the writing.

## 1. Goal, before any solution

The product's own code starts `git` by its bare name, with the environment it inherited, from whatever directory the
caller handed over. Three measured facts, none of them from this repository's product code but all of them about the
same runtime and the same git, say what that costs:

1. **A bare name handed to .NET's `Process.Start` is looked up in the CURRENT DIRECTORY before `PATH` on Linux.** The
   model-arms module measured it on 2026-10-29 with marker-writing shims: a `./stat` and `./id` in the target ran, and with
   a clean `PATH` and the working directory at the target root `./git` ran four times
   (`research/module_model_arms.md:503-505`; `tools/model-arms/ModelArms/Adapters/KernelFacts.cs:12-13`). On Windows,
   `CreateProcess` with no application name documents the same search — the application's directory, then the current
   directory, then the system directories, then `PATH` — unless `NoDefaultCurrentDirectoryInExePath` is set. The product
   has not measured the Windows half; this plan's tests do (T2).
2. **A repository's own `.git/config` can name programs git runs on the caller's behalf**, and so can a global config in a
   `HOME` or `XDG_CONFIG_HOME` the repository's writer chose: `core.fsmonitor` (consulted by `status`), `diff.external` and
   `diff.<driver>.textconv` (`diff`, `log -p`, `show`), a promisor remote's `uploadpack` run by a lazy fetch of a missing blob,
   trace2 targets appended to a file the target chose — each planted and observed by the module
   (`research/module_model_arms.md:585-592`, `:1163`; `tools/model-arms/ModelArms/Adapters/ArmGit.cs:11-13`).
3. **The inherited environment redirects the question.** A parent's `GIT_DIR`, `GIT_WORK_TREE`, `GIT_CEILING_DIRECTORIES`,
   `GIT_CONFIG_GLOBAL`, `GIT_EXEC_PATH`, `GIT_SSH*`, `GIT_EXTERNAL_DIFF` or `GIT_PAGER` makes git answer about another
   repository or run another program (`tools/model-arms/ModelArms.Core/Experiment/HostChecks.cs:68-70`;
   `tests/Bench.Tests/ModelArms/StepGitTests.cs:19`, red before the fix).

The module closed this class for itself under D17 (d) and D26 (b, c) of [PLAN_model_arms.md](PLAN_model_arms.md): every
child by the REAL path it resolved, a replaced environment, a chosen cwd, the hardening flags where the clone is the
model's, and a structural scan that goes red on the next bare name. The research record names the product's own bare
launches and leaves them: *"the benchmark's flows over the operator's clones, named and left; the structural test covers
`tools/model-arms` only"* (`research/module_model_arms.md:615-616`). This plan is the "left" half.

**The goal, stated without a solution:** no process the product starts is looked up by a name; no git the product
starts reads a variable the product did not decide; every git the product starts runs from a directory the caller named;
and a repository that an untrusted party has written — a model, a scout session, a benchmark target's own build and
tests, an agent whose working tree the hook reads — cannot make the product's git run a program of that party's
choosing, for every key this plan's tests plant. What cannot be closed by a launch is said in §6, not hidden.

**What makes this more than hygiene here** (`.agents/PROJECT.md` §11): the product clones and checks out arbitrary
repositories at operator-supplied urls, runs their builds and tests, lets models write into scratch trees, and reads
the hook's `git status` from whatever tree an agent is working in. Every one of those is a directory somebody other
than the operator has written, and today the product's git is started from inside it by a bare name.

## 2. What exists — verified at `5a8b4ce`, 2026-10-30

### 2.1 Every launch by a bare name under `src/` and `hosts/`

Enumerated mechanically, not from memory (common.security § *enumerate before believing*): the model-arms scan's shape
(`GitCommand\.|ProcessRunner\.\w+\(\s*"[A-Za-z]|Process\.Start\(|new\s+ProcessStartInfo\b|ProcessStartInfo\s*\{`,
`tests/Bench.Tests/ModelArms/ModelArmsArchitectureTests.cs:178-181`) run over whole files of `src/` and `hosts/`, then a
second grep for every launcher call (`ProcessRunner.(Run|Stream)Async(`, `ProcessSession.Start(`) and a third for
tool-name literals (`"git"`, `"cmd.exe"`, `"powershell.exe"`, `"dotnet"`, `"node"`, …). The third found one site the
first two shapes miss (`ScoutAccessPlanting`, the literal is a fallback behind `??`), which is why the structural test of
§5 carries both patterns.

| # | Site | What is launched | Environment | cwd |
|---|---|---|---|---|
| L1 | `src/Bench.Infrastructure/Git/GitHistory.cs:17`, `:38`, `:53` — `GitCommand.RunAsync` / `ReadAsync` / `AskAsync` | `"git"`, bare | inherited (the launcher's `environment: null`, `ProcessRunner.cs:120`) | the caller's `workingDirectory` |
| L2 | `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:285` — `InitOneCommitAsync` | `"git"`, bare | the parent's whole environment with six `GIT_AUTHOR_*`/`GIT_COMMITTER_*` names overwritten (`ScoutTrees.cs:341-353`) — every OTHER `GIT_*` of the parent passes | the staging folder |
| L3 | `src/Bench.Infrastructure/Git/GitBlobBatch.cs:35` — `cat-file --batch` streamed | `"git"`, bare | inherited | the repository |
| L4 | `hosts/Hook/Worktree.cs:135` — `Process.Start(new ProcessStartInfo("git"))` | `"git"`, bare | inherited from Claude Code's hook launch | **the agent's cwd** (`HookClient.cs:57`) |
| L5 | `src/Bench.Infrastructure/Git/ScratchWorktree.cs:74` — `GateProcess.RunAsync` | `command.Executable`: the first word of the operator's `--build`/`--test` gate command (`dotnet`, …; `HarvestGates.cs:10-23`) | inherited | **the scratch worktree the target's code runs in** |
| L6 | `src/Bench.Infrastructure/Machine/MachineProbe.cs:155`, `:169` | `"powershell.exe"`, bare | inherited | `Environment.CurrentDirectory` |
| L7 | `src/Bench.Infrastructure/Machine/HardwareSampler.cs:158` | `"powershell.exe"`, bare | inherited | `Environment.CurrentDirectory` |
| L8 | `src/Bench.Infrastructure/Scouts/ScoutAccessPlanting.cs:98-99` | `%ComSpec%` else `"cmd.exe"` (Windows junction) | inherited | the work folder (the product's own) |

Already by a path, listed so the scan's allow-list is explicit: `ProductPinReader.cs:88` (`Path.GetFullPath` of
`--coai-exe`), `CliSubjectRuntime.cs:125` and `CliAgentRuntime.cs:335` (an executable the host resolved through
`CliExecutable.OnThisMachine` — `hosts/Cli/ScoutsInputs.cs:247-258` — or spelled as a path), `McpStdioClient.cs:50` →
`ProcessSession.cs:87-89` (the product's full path, `hosts/Cli/GateCliInputs.cs:213`), `CoaiApiProbeRunner.cs:58`.

### 2.2 The 32 call sites of `GitCommand`, by the repository they run over

| File | Lines | Commands | Repository kind (§3) |
|---|---|---|---|
| `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs` | `:299` (helper; `:216`, `:204`, `:238`, `:251`, `:263`) | `clone --bare` (cwd = cache root), `fetch`, `cat-file -e`, `worktree add --detach --force` | K1 — and K1-network |
| `src/Bench.Infrastructure/Git/ScratchWorktree.cs` | `:26`, `:43` | `worktree add --detach --force`, `worktree remove --force` — **in the bare / repository dir, AFTER untrusted code ran in the tree** | K1 after K2 |
| `src/Bench.Infrastructure/Git/HarvestGates.cs` | `:97`, `:114` | `checkout <fix> -- <tests>`, `checkout --force --detach <fix>` — between and after the target's own build and tests | K2 |
| `src/Bench.Infrastructure/Git/SolutionSignals.cs` | `:117`, `:189` | `checkout <fix> -- <tests>`, `apply` of a model-written diff | K2 |
| `src/Bench.Infrastructure/Git/WorktreeAudit.cs` | `:21` | `status --porcelain` — after a CLI agent ran in the tree | K2 |
| `src/Bench.Infrastructure/Git/FixHarvest.cs` | `:66`, `:74`, `:121` | `rev-parse`, `show -s`, `diff` | K1 (bare or worktree) |
| `src/Bench.Infrastructure/Git/GitHistory.cs` | `:108` (via L1) | `log --first-parent --name-only` | K1 (the provider's read-only worktree; `hosts/Cli/QuestionsCommand.cs:220`) |
| `src/Bench.Infrastructure/Git/GitCommitResolver.cs` | `:15` | `rev-parse --verify` | K5 |
| `src/Bench.Infrastructure/Gate/ProductPinReader.cs` | `:103`, `:111`, `:113` | `rev-parse --show-toplevel`, `rev-parse --short HEAD`, `status --porcelain --untracked-files=no` | K5 |
| `src/Bench.Infrastructure/Gate/GateDriverAdapters.cs` | `:176`, `:204`, `:211`, `:232`, `:234`, `:273`, `:278`, `:292` | `branch -f`, `rev-parse HEAD`, `checkout --detach`, `clone --shared --no-checkout` (cwd = checkout root), `submodule update --init --recursive` with `protocol.file.allow=always` (urls from the TARGET's `.gitmodules`), `config --file .gitmodules`, `cat-file -e` | K1-gate — and K1-network |
| `src/Bench.Infrastructure/Scouts/ScoutTrees.cs` | `:124`, `:184`, `:206`, `:219` | `rev-list --all --count` **in a tree a scout session ran in**; `archive --format=zip`, `ls-tree`, `show :.gitmodules` on the bare mirror | K3 (`:124`); K1 (the rest) |
| `src/Bench.Infrastructure/Scouts/ScoutHistory.cs` | `:46`, `:81` (helper; `:24-27`, `:44`, `:70`) | `show -s`, `diff --name-only`, `ls-tree`, `rev-list --parents`, `merge-base --is-ancestor`, `diff --unified=0` on the bare mirror | K1 |
| `src/Bench.Infrastructure/Scouts/ScoutRoots.cs` | `:62`, `:79` | `check-ignore`, `ls-files` on the checkout above the trees root | K5 |
| `src/Bench.Infrastructure/Scouts/ScoutAccessPlanting.cs` | `:48` (via L2) | `init`, `add -A`, `commit` in the access check's scratch tree, before the session | K3-before |
| `hosts/Cli/ScoutsHygieneCommand.cs` | `:131` (+ L3) | `diff --cached --raw`, `ls-tree -r`, `cat-file --batch` on `--repo` or the cwd — called from `.githooks/pre-commit` | K5 |

### 2.3 What the module already has, and the rule about where it may be used

| Piece | Where | What it does |
|---|---|---|
| `ExecutableRule` (pure) | `tools/model-arms/ModelArms.Core/Experiment/ExecutableRule.cs:7-82` | the textual half: a bare name is fine; a relative path, a device prefix, a NUL, a path under a forbidden root is refused with a sentence |
| `ExecutableResolution` (pure over `HostPaths`) | `ModelArms.Core/Experiment/ExecutableResolution.cs:20-204` | searches `PATH` (`.exe` on Windows), asks the kernel for the real path, judges written and real against written and real roots; `PathEntryRefusal` (empty, relative, under a root); `ConfigFolderRefusal` (`HOME`, `XDG_CONFIG_HOME`); `Climb` over absent components |
| `ExecutableAnswer` / `HostPaths` | `ModelArms.Core/Experiment/ExecutableAnswer.cs:7-27` | Found (the real path) / Absent / Refused; the host's three functions |
| `PathScope` (pure) | `ModelArms.Core/Experiment/PathScope.cs` | "is equal or under", used by the two above and by `ExperimentFile` |
| `GitEnvironment.Of` (pure) | `ModelArms.Core/Experiment/HostChecks.cs:68-76` | the map minus every `GIT_*` name |
| `RealPath.Of` (P/Invoke) | `tools/model-arms/ModelArms/Adapters/RealPath.cs:14-116` | libc `realpath(3)`; Windows `GetFinalPathNameByHandleW`; other platforms refused |
| `ConfigFolders` | `ModelArms/Adapters/ConfigFolders.cs:8-17` | `HOME` and `XDG_CONFIG_HOME` judged once through `RootFacts.Host` |
| `ArmGit` | `ModelArms/Adapters/ArmGit.cs:18-54` | the launch: the real path, `GitEnvironment.Of` + `GIT_NO_LAZY_FETCH=1`, cwd the clone, `-c core.fsmonitor=false` on every question, `-c diff.external= --no-ext-diff --no-textconv` on the diff, a 60 s ceiling, through the shared launcher's replacing overload |
| the scan and its companions | `tests/Bench.Tests/ModelArms/ModelArmsArchitectureTests.cs:161-213` | S22: `tools/model-arms` holds no launch by a literal; a theory per shape; the fixture's bare git is still found |
| the observations | `tests/Bench.Tests/ModelArms/StepGitTests.cs:19`, `:41`, `:118`, `:143`; `ValidateVerbTests.cs:349`, `:481` | an ambient `GIT_DIR` does not change the head read; the launch goes by the real path and no other; `diff.external` / `textconv` / promisor shims stay unrun; `HOME` under the target is refused and git is not asked |

**The rule that decides where these may live:** `Nothing_under_src_or_hosts_references_the_model_arms_module`
(`ModelArmsArchitectureTests.cs:96-118`) — no `*.csproj` under `src/` or `hosts/` names the module and no product
assembly references `ModelArms.Core`; the module references `Bench.Domain`, `Bench.Application`, `Bench.Infrastructure`
and `Bench.Hosting` (`research/module_model_arms.md:33-36`). `ModelArms.Core` references only the Domain and the
Application layer (`Core_never_references_infrastructure_or_entity_framework`). `Bench.Domain` depends on nothing
(`.agents/PROJECT.md` § Project Structure). The hook references `Bench.Contracts` and `Bench.Domain` only
(`hosts/Hook/Hook.csproj:22-27`), by a decision about start-up cost that this plan keeps.

**The product's own resolver:** `CliExecutable.Resolve` (`src/Bench.Infrastructure/Models/CliExecutable.cs:19-36`) —
the first `PATH` directory holding a match, `PATHEXT` order on Windows, a `.cmd`/`.ps1` shim refused by name. Textual:
`File.Exists` only (no execute bit, no real path), relative and empty entries are not judged (`RemoveEmptyEntries` drops
the empty one silently), no forbidden roots. Used for `claude` and `codex` (`hosts/Cli/ProbesInputs.cs:29`,
`ScoutsInputs.cs:28`, `GateAssessCommand.cs:229`).

**The shared launcher** (`src/Bench.Infrastructure/Process/ProcessRunner.cs:62-130`): exe + argv, a timeout that kills
the tree, an optional REPLACED environment (`:529-541`) — the mechanism this plan needs already exists; what is missing
is the policy that builds the map.

### 2.4 A defect in the hook found while reading it (verified, not planned around)

`hosts/Hook/Worktree.cs:100-113`: `git.StandardOutput.ReadToEnd()` runs BEFORE `git.WaitForExit(BudgetMs)`, so a git
that does not end — a `core.fsmonitor` daemon waiting, a hook blocking, a lock held by the agent's own git — blocks the
read forever and the 1.5 s budget is never consulted; and `RedirectStandardError = true` with nothing ever reading
stderr means a git that writes more than a pipe buffer of warnings wedges on its own stderr. The hook's whole contract
is to never be the reason a tool call fails (`hosts/Hook/Program.cs:3-8`). S4 fixes the order with a red test first (T6).

## 3. The trust model, per repository the product's git runs over

The question is not "who wrote this repository" alone but "which configuration will git read, and who could have written
it". Git reads, in order: the system config, the global config (`HOME/.gitconfig`, `XDG_CONFIG_HOME/git/config`), the
repository's **common** config, the worktree config, then `-c`. A linked worktree (`git worktree add`) shares its common
`config`, `hooks/`, `objects/` and `worktrees/` with the repository it was added from: its `.git` is a file naming
`<common>/worktrees/<id>`, and `git config` run inside it writes `<common>/config`.

| Kind | Where | Who writes the TREE | Who can write the CONFIG git reads | Git runs there |
|---|---|---|---|---|
| **K1** the operator's cache | `<cache>/bare/<key>.git`, `<cache>/worktrees/<key>/<sha>` (`GitCheckoutProvider.cs:54-56`); the gate clones under `<checkoutRoot>/gate/<run>/<task>` (`GateDriverAdapters.cs:157`) | the product only: `clone --bare`, `fetch`, `worktree add`; the tree is a commit of a repository at an operator-supplied url — its CONTENT (`.gitattributes`, `.gitmodules`) is the target's | the operator — **until the first K2 tree is added**: every scratch worktree is a linked worktree of the K1 bare (`ScratchWorktree.cs:26-28`), so anything that runs inside K2 can write the bare's `config` and `hooks/` with the operator's rights | mirror, history, archive, the gate's clone and submodule update, `worktree add` / `remove` |
| **K2** scratch worktrees | `<temp>/bench-scratch/<guid>` or under the checkout root (`ScratchWorktree.cs:13-24`) | the target's own build and tests (`HarvestGates`, attended), a model-written diff plus the target's build and tests (`SolutionSignals.cs:40-44`: *arbitrary code execution by design*), a CLI agent with write tools denied — advisory (`WorktreeAudit.cs:6-10`) | **everyone who ran in it**: `.git/config` is the K1 common config; `hooks/` is K1's; `.gitattributes` is in the tree; a `./git` in the tree answers a bare launch from that cwd | `checkout`, `apply`, `status --porcelain`, then `worktree remove` in K1 |
| **K3** scout trees | `<trees root>/<task>` (`ScoutTrees.cs:118`), the access check's `<work>/tree` | `git archive` of an arbitrary public repository's commit, extracted; then `init` / `add -A` / `commit` with a fixed identity; then a **scout session** (claude, the operator's rights) runs IN the tree (`CliScoutRunner.cs:143`) | the archive's `.gitattributes` (applied by `add`), the operator's global config (`init.templateDir`, `filter.lfs.*`), and after the session everything in `.git/` — a local `.git`, not shared with K1 | `init`/`add`/`commit` before the session; `rev-list --all --count` after it (`ScoutTrees.cs:124`) |
| **K4** the hook's cwd | whatever the agent's tool call named (`HookClient.cs:57`) | an AI agent, with Bash, in the operator's own working tree | the agent: `.git/config`, `.git/hooks/`, `core.hooksPath`, `.gitattributes`, a `./git` or `./git.exe` in the tree — **the most exposed repository the product touches, on every tool call** | `status --porcelain` with a 1.5 s budget |
| **K5** the operator's own checkouts | the product's checkout (`ProductPinReader`), the benchmark's own repository (`GitCommitResolver`, `ScoutsHygieneCommand`, `ScoutRoots`) | the operator — and the agents that work in those trees under Claude Code | the operator; an agent in the tree can write `.git/config` as in K4 | `rev-parse`, `status`, `ls-files`, `check-ignore`, `cat-file --batch` |
| **network** | K1's `clone --bare` / `fetch` to an operator url; the gate's `submodule update` to urls the TARGET's `.gitmodules` names, file protocol allowed (`GateDriverAdapters.cs:273`) | — | the operator's global config supplies `credential.helper`, `core.sshCommand`, `url.insteadOf` on purpose; the REPOSITORY config of K1 can supply the same keys after a K2 run | `clone`, `fetch`, `submodule update` |

**What follows, and what the plan decides (D4, D6):** a per-call-site "trusted / untrusted" switch is the wrong shape.
K1 is trusted only until the first scratch worktree; K5 is the operator's but an agent writes there; K4 is hostile by
definition; and the operator's own clone gains nothing from `core.fsmonitor` or an external diff that a benchmark would
miss. So the baseline hardening applies to **every** launch, and what varies per call site is the OPERATION's
capability — may it materialise files, may it reach the network — not the trust. The one place where a launch flag
cannot close the hole (filters and transport keys in a common config an untrusted party wrote) is closed by not sharing
the config in the first place (D6), and what remains open after that is written in §6.

**Threat agent per kind, stated so the reviewer can disagree with it:** K2 and K3 are a party with the operator's
rights, running code the operator did not read — a build script, a model's patch, a scout's tool calls. K4 is an AI
agent in the operator's tree, with Bash. K1 and K5 inherit K2's and K4's writes. This plan's boundary is the
**configuration git reads by its own rules and the name a launch is resolved by** — not the file system: a party with the
operator's rights can overwrite `/usr/bin/git` or `~/.gitconfig`, and nothing a launch does prevents that. That boundary
is [PLAN_code_lane.md](PLAN_code_lane.md) §4.2's (isolation) and the minigit sandbox's
([PLAN_minigit_stack_bench_E2.md](PLAN_minigit_stack_bench_E2.md)), not this plan's.

**The consultation (common.subagent-models § the pipeline, step 2).** Before the epic split, `mcp__coai__ask_consultants`
was asked (2026-10-30, consult `d4d53764…`, 2 of 3 rows answered — `gpt-6-astra` over this disk, `gemini-4-argon-low`
without file access; the Vibe row refused its grant). Both answered the same on the shape: **hardening as the default
for every launch, exceptions by operation capability, never a per-site trust Boolean; the cache is not trusted after a
linked worktree exposed its common config.** Taken, verified against the code (§3's table is the verification: the
`.git` file of a scratch worktree points into the bare). Both listed the execution paths beyond the module's four — hooks
and `core.hooksPath`, `filter.<name>.clean/smudge/process`, `submodule.<name>.update=!cmd`, `log.showSignature` +
`gpg.program`, `core.alternateRefsCommand`, `tar.<format>.command`, `core.sshCommand` / `credential.helper` /
`remote.<n>.uploadpack` — taken as T5's plant list; each is a claim about git's behaviour that T5 OBSERVES, not a fact this
plan asserts (common.security § *a measure you have not observed working is a comment*). One row found the hook defect
of §2.4 — verified by reading `Worktree.cs:107-109`, taken. One row proposed `GIT_CONFIG_GLOBAL=/dev/null` for local
plumbing — **declined**: the operator's global config is theirs (`safe.directory`, identity, `init.templateDir`), the
module's own decision (`research/module_model_arms.md:588-592`), and a judged `HOME` closes the case the row worried about.
One row proposed `safe.directory=*` be considered — both rows then agreed it must NOT be added; D10 records that.

## 4. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | **Git is resolved ONCE per process, before its first launch, to the REAL path the kernel would open; every launch names that path.** The resolution is a lazy, memoised `Outcome<string>` in `Bench.Infrastructure` (`GitExecutable.Resolved`), logged once at Information as `git → <real path>`; a refusal is the launch's `Outcome` failure carrying the sentence (the D17 sentences, reused verbatim). The hook resolves in-process, per invocation (its process IS one invocation), with no child process (T6 measures the cost). | A bare name is looked up in the cwd before `PATH` (§1.1); a path judged and then launched by its written spelling leaves the link for the writer to re-point (`research/module_model_arms.md:483-485`). Once per process: re-resolving per launch adds syscalls and closes no race — whoever can replace `/usr/bin/git` has the operator's rights already (§3's boundary). |
| D2 | **The resolver is ONE implementation, moved, not copied.** The pure deciders — `ExecutableRule`, `ExecutableResolution`, `ExecutableAnswer` + `HostPaths`, `PathScope`, `GitEnvironment` — move from `ModelArms.Core/Experiment/` to **`src/Bench.Domain/Process/`** (namespace `Bench.Domain.Process`); the P/Invoke `RealPath` moves from `ModelArms/Adapters/` to **`src/Bench.Infrastructure/Process/RealPath.cs`**; the module references them from there (it already references both projects); `ConfigFolders` and `RootFacts` stay in the module (they are its `HostPaths` wiring). `CliExecutable` is **retired into the moved resolver**: `ExecutableResolution` gains the one rule it lacked — a hit that is a `.cmd`/`.bat`/`.ps1` shim is `Refused` naming it, `PATHEXT` order kept — and its three callers call `GitExecutable`'s sibling `ToolExecutable.Resolve(word)`; `CliExecutableTests` move with the rule. | Reuse-first § 2: a second copy of a security primitive is the defect from the moment it compiles — the family measured a launcher nearly getting a third copy. The architecture rule allows exactly this direction (the module may see the product; the product never sees the module), and `ModelArms.Core` may reference `Bench.Domain`. The Domain holds pure decisions with `Outcome` already (`ProbeChildEnvironment`, `CoaiEnvironment` are environment policies there), so a pure path decider is not foreign to it. Weighed and rejected: (a) re-implement in `src/` — two copies; (b) reference the module from `src/` — forbidden by `Nothing_under_src_or_hosts_references_the_model_arms_module`, and rightly; (c) move only `RealPath` and leave the deciders in Core — the product would then need its own `ExecutableResolution`, which is (a). |
| D3 | **The hook shares the same two files without a new project reference**: `Hook.csproj` links `src/Bench.Infrastructure/Process/RealPath.cs` by `<Compile Include>` (one file, P/Invoke only, no dependency), and uses `Bench.Domain.Process` through the reference it already has. Its `HostPaths` is three lambdas over `File`/`Directory`/`FileSystemInfo`. | The hook's start-up budget rules out `Bench.Infrastructure` (`Hook.csproj:3-10`; EF, Serilog and hosting ride in with it). A file link is not an assembly reference, so the hook's "Contracts and Domain only" shape is unchanged; `ArchitectureTests` gains a line that says so (T10). |
| D4 | **Every git launch gets the baseline, and a PROFILE names what the operation may do.** `GitLaunch` (new, `src/Bench.Infrastructure/Git/GitLaunch.cs`) is the one road in; `GitCommand`'s three methods, `GitBlobBatch` and `ScoutTrees.InitOneCommitAsync` go through it; it builds the argv as `[.. Baseline, .. profile.Flags, .. the command]` and the environment as D5. **Baseline, every launch:** `-c core.fsmonitor=false`, `-c core.hooksPath=<an empty directory the product owns>`, `-c log.showSignature=false`, `-c submodule.recurse=false`, `-c core.alternateRefsCommand=`, `--no-pager`. **Profile `Inspect`** (rev-parse, cat-file, ls-tree, ls-files, check-ignore, rev-list, merge-base, log, show, diff, status): adds `--no-optional-locks` and, for log/show/diff, `-c diff.external= --no-ext-diff --no-textconv`. **Profile `Materialise`** (checkout, worktree add/remove, apply, archive, init/add/commit, branch -f, a local `clone --shared --no-checkout`): the baseline, plus `--checkout` on every `submodule update` so a `submodule.<name>.update=!cmd` in a config cannot choose the mode; no transport key is touched — a local clone is a path, and the gate's `protocol.file.allow=always` stays explicit where it is (`GateDriverAdapters.cs:273`). **Profile `Network`** (K1 `clone --bare`, `fetch`, the gate's `submodule update`): the baseline, and the operator's global transport configuration passes untouched — this is the one profile where `credential.helper`, `core.sshCommand` and `url.insteadOf` are wanted. | Both consultants, independently, and §3: trust is not per site; capability is. The baseline costs nothing a benchmark wants (`fsmonitor` and hooks run no part of any measurement here). `--no-optional-locks` keeps a read from contending with the agent's own git on `.git/index.lock` — the hook's case above all. Every flag's effect is a claim about git until T5 observes it on the CI's git; a flag T5 cannot show doing anything is deleted, not kept "just in case" (common.security). |
| D5 | **The environment is BUILT, never inherited:** `GitEnvironment.Of(parent)` (every `GIT_*` removed), then `GIT_NO_LAZY_FETCH=1`, `GIT_TERMINAL_PROMPT=0`, the identity six where the caller sets them (`ScoutTrees`), and **`PATH` rebuilt** — the parent's entries minus the empty, the relative and the ones under a forbidden root (each dropped entry logged once, at Warning, naming it). **`HOME` and `XDG_CONFIG_HOME` are judged** (`ExecutableResolution.ConfigFolderRefusal`) against the forbidden roots and a refusal fails the launch with its sentence. The forbidden roots of the product are: the checkout cache root, the scout trees root, the scratch root(s), the gate checkout root — and for the hook, the cwd it was handed. Handed to the launcher through the replacing overload (`ProcessRunner.cs:117-125`). | §1.3. `PATH` matters even with git by its path: git looks its own helpers (`git-remote-https`, `git-lfs`, a credential helper) up in the child's `PATH`, and an empty or relative entry makes the cwd — K2, K4 — supply them (`ExecutableResolution.cs:46-51`, observed by the module). Dropped rather than refused, unlike the module: a product that refuses to run because the operator's shell has `.` on `PATH` is a product that gets a workaround; dropping it and saying so keeps the launch safe by construction. `HOME` is refused, not dropped: git with no `HOME` behaves differently per platform and the operator's config is wanted. |
| D6 | **A scratch tree is a scratch CLONE, not a linked worktree.** `ScratchWorktree.AddAsync` becomes `clone --shared --no-checkout <provider worktree> <scratch>` + `checkout --detach <sha>` — the exact shape `GateCloneCheckouts.CloneAsync` already uses (`GateDriverAdapters.cs:232-234`) — and `RemoveAsync` becomes a directory delete (the gate's `DeleteTree`, `:185`), so no `worktree remove` runs in K1 after K2's code ran. The K1 bare's `config` and `hooks/` are then never the config of a tree an untrusted party ran in. Objects stay shared through alternates (read); new objects land in the clone. | §3: the common config is the hole no `-c` flag closes — `filter.<name>.*` has no wildcard override, and the network profile must read the repository's transport keys. Both consultants named it; the gate shape proves the cost (a clone of a worktree is seconds, measured by the gate's own campaign). `WorkspaceTrust` paths under the checkout root are unchanged (`CliSubjectRuntime` passes `WorktreeRoot`, `ScratchWorktree.cs:18-24`). **Cut-able by the owner** without breaking S1–S5: the baseline still applies; §6 then carries the residual as open. |
| D7 | **The hook's launch**: `Worktree.Porcelain` resolves `git` in-process (D1, D3), builds the environment (D5, with the cwd as the forbidden root), passes the baseline and `--no-optional-locks`, and reads BOTH pipes asynchronously while waiting `BudgetMs`, killing the tree on the budget. A refusal of the resolution, of `HOME`, of a `PATH`, a git that exits 128 (a config the agent broke), and the budget each answer `WorktreeDto.NotChecked(<why>)` — never a bare-name fallback, never a non-zero exit. | §2.4 and §3 K4. `NotChecked` is a state the schema already carries honestly (`Worktree.cs:26-32`). |
| D8 | **The other bare names go through the same resolver**: `GateCommand.Executable` (L5) is resolved by `ToolExecutable.Resolve` with the scratch tree among the forbidden roots BEFORE the first phase, and the resolved path is what `GateProcess` launches; `"powershell.exe"` (L6, L7) is resolved once (Windows only) and launched from the process's base directory, never `Environment.CurrentDirectory`; `%ComSpec%` (L8) is resolved and refused when under a forbidden root. | A `./dotnet` in a scratch tree where the target's tests just ran is the same measurement as the module's `./git` (§1.1). `Environment.CurrentDirectory` is wherever `bench` was started — under an agent session, inside a target's tree. |
| D9 | **One structural scan for `src/` and `hosts/`, written before the fix, with companions** — T1. Allow-listed: `ProcessRunner.cs`, `ProcessSession.cs` (they build the start info from a path they were handed, and a companion asserts each file launches by a variable), the hook's launcher file (same companion). | common.security § *write the scan BEFORE the fix*: its first red is the list S2–S5 work from, and the only version of §2.1 not assembled by hand. |
| D10 | **`safe.directory` is not added.** A dubious-ownership refusal surfaces as git's own words in the `Outcome`. | It is an ownership exception, not a program guard; `safe.directory=*` would make git read a config written by another uid — the opposite of this plan. The module decided the same (`research/module_model_arms.md:588`). |
| D11 | **Observation over assertion.** Every flag of D4, the `PATH` rebuild of D5 and the hook's budget are proven by a planted program that does NOT run (T5, T2, T6), on the git the CI runs; a break-it per flag (the flag removed, the marker written) proves each plant works. Windows: what cannot be observed there is written as "asked for, not observed" in the module doc, and the Windows rows skip saying why — the module's own practice (`research/module_model_arms.md:520-522`, `:615`). | common.security: a test that asserts a flag is in the argv proves the flag was sent, not what it did. |

## 5. Tests — red first, exact names (the build's ids)

All in `tests/Bench.Tests/`, xUnit v3, run through the test executable (never `dotnet test`). Fixtures reuse
`DatedGitRepo` (`tests/Bench.Tests/Infrastructure/DatedGitRepo.cs`) and the module's shim pattern (a marker-writing
script on `PATH` or in the tree; `StepGitTests.cs:118-170`, `ValidateVerbTests.cs:349`).

| Id | Test (name states the guarantee) | Red today because | Story |
|---|---|---|---|
| T1 | `ArchitectureTests.Nothing_under_src_or_hosts_launches_by_a_bare_name` — the S22 regex plus a second pattern for tool-name literals handed to a launcher (`"git"`, `"cmd.exe"`, `"powershell.exe"`), whole files, across lines, `bin`/`obj` skipped, allow-list D9; companions: `The_product_bare_name_pattern_still_matches_each_launch_shape` (a theory row per shape of §2.1, the `??`-fallback literal included), `The_product_bare_name_scan_still_finds_the_fixtures_bare_git` (`tests/.../ModelArms/ExperimentRoot.cs` or the fixture S1 keeps), `Each_allow_listed_launcher_launches_by_a_path_it_was_handed` | eight files of §2.1 match | S1 |
| T2 | `GitLaunchTests.Git_is_launched_by_the_real_path_the_resolution_found_and_neither_a_git_in_the_working_directory_nor_an_empty_PATH_entry_answers` — a marker-writing `./git` in the repository, a `PATH` of `""` + the real bin; the marker stays empty and the answer is the real git's; the Windows row uses `git.exe` in the cwd | the cwd's `./git` runs (the module's measurement, reproduced on the product's launcher) | S3 |
| T3 | `GitLaunchTests.An_ambient_GIT_DIR_GIT_WORK_TREE_and_GIT_CONFIG_GLOBAL_in_the_parent_do_not_reach_git` (theory: each variable) | the launcher inherits | S3 |
| T4 | `GitLaunchTests.A_HOME_or_XDG_CONFIG_HOME_under_a_forbidden_root_refuses_the_launch_naming_the_variable_and_git_is_not_started` (theory; the trace2-target plant of `ValidateVerbTests.cs:481` reused) | nothing judges them | S3 |
| T5 | `GitLaunchTests.A_repository_whose_config_names_programs_is_read_but_none_of_them_runs` — a theory with one row per (key, command the product uses): `core.fsmonitor` × `status`; `core.hooksPath` + `post-checkout` × `checkout`, `worktree add`; `diff.external`, `diff.<d>.textconv` × `diff`, `log -p`, `show`; `log.showSignature` + `gpg.program` × `log`, `show`; `submodule.<n>.update=!cmd` × `submodule update`; `core.pager` × `log`; `remote.origin.uploadpack` + a missing blob × `cat-file --batch`, `diff`; `core.alternateRefsCommand` × `rev-list`; `filter.<n>.clean`/`smudge` × `status`, `checkout` **(expected to stay RED on a linked worktree whose common config holds the plant — that row documents D6's reason and goes green only with S6)**; a break-it per row: the flag removed, the marker written | the keys run their programs | S3 (S6 for the filter rows) |
| T6 | `HookWorktreeTests.A_git_that_does_not_end_answers_NotChecked_within_its_budget_and_is_killed` (a fake git that sleeps past 1.5 s, both pipes), `A_git_that_fills_stderr_does_not_wedge_the_reading`, `The_hook_launches_git_by_its_resolved_path_with_GIT_variables_removed_and_no_optional_locks_and_a_git_in_the_cwd_does_not_answer`, `A_refused_resolution_or_HOME_answers_NotChecked_with_the_sentence`, `Resolution_in_the_hook_costs_under_one_tool_call_s_budget` (measured, the number recorded in the module doc) | `ReadToEnd` before `WaitForExit`; stderr unread; bare name; inherited env | S4 |
| T7 | `ScratchWorktreeTests.A_config_written_inside_a_scratch_tree_does_not_reach_the_mirror_and_a_hook_planted_there_does_not_run_on_the_next_add` | a linked worktree shares the common config and hooks | S6 |
| T8 | `HarvestGatesTests.The_gate_command_s_word_is_resolved_outside_the_scratch_tree_before_the_first_phase_and_a_dotnet_in_the_tree_does_not_answer`; `MachineProbeTests.PowerShell_is_resolved_once_and_launched_from_the_process_base_directory` | L5 launches the word bare from the tree | S5 |
| T9 | `GitLaunchTests.The_resolved_git_is_logged_once_per_process_as_the_real_path_and_a_dropped_PATH_entry_is_named_once` | — (new behaviour) | S3 |
| T10 | `ArchitectureTests.Bench_Domain_still_references_nothing_after_the_process_deciders_moved`; `ModelArmsArchitectureTests.Every_core_decider_is_named_in_the_pure_list` updated (the moved five removed from `PureDeciders`, `ExperimentFile` still found); `Nothing_under_src_or_hosts_references_the_model_arms_module` unchanged and green; `The_module_launches_nothing_by_a_bare_name` unchanged and green; `The_hook_references_Contracts_and_Domain_only_and_links_one_infrastructure_file` | — (guards the move) | S2 |

**Not a test, a rule for every row:** a row that cannot be observed on the CI's platform SKIPS naming why (the module's
practice), never passes by construction.

## 6. What this plan does NOT cover — said, not hidden

- **The file system.** A party with the operator's rights can replace the git binary, the global config, or any file under
  the cache. This plan's boundary is what git reads by its own rules and the name a launch resolves. Isolation is
  [PLAN_code_lane.md](PLAN_code_lane.md) §4.2 and the minigit sandbox.
- **Filters on a repository whose common config an untrusted party wrote**, where D6 was cut or where the tree is K3 after
  a session: `filter.<name>.*` has no wildcard override, and `status`/`checkout` run it. With D6 built, the only K3 git after a
  session is `rev-list --all --count` (`ScoutTrees.cs:124`), an `Inspect` that runs no filter — T5's `rev-list` row.
- **Keys a later git adds**, and keys on paths the product's commands do not take: T5's list is the git the CI runs
  (named in the module doc at build time), as `ArmGit.cs:12-13` says of its own.
- **`.gitattributes` of an arbitrary archive meeting the operator's OWN global filters** (`filter=lfs` and a global
  `filter.lfs.clean = git-lfs clean`) during `ScoutTrees`'s `add -A`: the operator's program runs on target data. Named;
  not changed — the alternative is `GIT_CONFIG_GLOBAL=/dev/null`, declined in §3.
- **The window between resolution and launch** for the real path (the module's residual too), hard links, bind mounts.
- **The agents' own git.** A scout or a CLI subject runs git itself inside its tree with its own environment; that is
  the subject's measurement, not the product's launch (the module's D19 analogue).
- **The gate's file-protocol allowance and target-named submodule urls** (`GateDriverAdapters.cs:273`): a transport
  decision of the gate's plan; this plan hardens the launch around it and names it in T5's `submodule update` row.
- **Windows observations.** The resolver's Windows half exists (`RealPath.Windows`); the plants run on `ubuntu-latest`.
  A Windows row is written and skips until a Windows host runs the suite.
- **The analysis or the measurement campaigns.** No arm, no cell, no number changes; a flag that changed a measurement
  would be a bug (D4's baseline runs no part of any measurement).

## 7. Build order

0. **The coai plan round** — `mcp__coai__open` (`callerModel` of the session that builds), `mcp__coai__review_plan` with
   this document, `resolve` every finding (reject in round one what is wrong or out of scope), until `proceed`. The
   consultation of §3 is recorded; the cadence rule (`common.coai-consultant` 7) applies if the gate orders one.
1. S1 — the scan (T1), red, its output pasted into the stories document as the list.
2. S2 — the move (D2, D3, T10): no behaviour changes; the module's tests stay green; `dotnet build` 0 warnings.
3. S3 — `GitLaunch` (D1, D4, D5, D9, D10, D11; T2–T5, T9): `GitCommand`, `GitBlobBatch`, `ScoutTrees` through it; T1
   goes from eight files to three (L4–L8).
4. S4 — the hook (D7; T6): T1 to two.
5. S5 — the other bare names (D8; T8): T1 green.
6. S6 — scratch clones (D6; T7, T5's filter rows). Independent of S4–S5; may be cut.
7. S7 — the record: `research/module_scouts.md` and `research/architecture.md` (where `GitCheckoutProvider`,
   `ScratchWorktree` and the hook are described — `architecture.md:56`, `:868`, `:1097`), `research/module_model_arms.md:615-616`
   ("named and left" → named and closed, with the moved files' new homes), this plan promoted with its deviations.

Waves for a later build: S1 → S2 → S3 → (S4 ∥ S5 ∥ S6) → S7.

## 8. Growth — nothing that grows without an owner

| Surface | Projected size | Who retires it | Interrupted |
|---|---|---|---|
| the empty hooks directory (D4) | one directory, 0 bytes, ONE fixed path per user under the temp root (`<temp>/bench/no-hooks`), created idempotently | never grows; nothing to retire | none |
| scratch clones in place of worktrees (D6) | per clone: the worktree's files (as today) + `.git` without objects (alternates) ≈ a few hundred KB more than a linked worktree; count unchanged (one per leg or gate run, removed on every path out as today, `CliSubjectRuntime.cs:104`, `HarvestGates.cs:83`, `SolutionSignals.cs:88`) | the same callers, on the same paths; a crash leaves a folder under the scratch root exactly as today's leak of a worktree does — and no stale `worktrees/<id>` entry in the mirror any more | the caller's existing cleanup; no new in-flight state |
| the logged resolution and dropped-entry lines (D1, D5) | one line each per process | the log's retention (`logging-serilog` § Retention) | none |

No table, no cache, no spawned process that outlives a launch.

## 9. Risks

| Risk | Consequence | Mitigation |
|---|---|---|
| A baseline flag changes a product behaviour that a measurement depends on (e.g. `--no-optional-locks` making `status` skip an index refresh, `submodule.recurse=false` on a `checkout` the gate expected to recurse) | a cell measures a different tree | S3 runs the gate's and the scouts' existing suites unchanged; `GateCloneCheckoutsTests` (`tests/Bench.Tests/Gate/Driver/`) pins the submodule shape; a flag that changes an existing green test is a finding, not a flag |
| A flag the CI's git does not know | exit 129, every launch refused | `-c key=value` is accepted for any key; the two option flags (`--no-optional-locks`, 2.15+; `--no-textconv`, old) are below the family's floor; `GIT_NO_LAZY_FETCH` is the variable, not `--no-lazy-fetch`, for the reason `ArmGit.cs:22-23` gives |
| Dropping a `PATH` entry breaks a developer's git helpers (a relative `PATH` entry that supplied `git-lfs`) | a fetch fails naming nothing obvious | the dropped entry is logged by name with the sentence (D5); the fix is the operator's `PATH` |
| The move (S2) touches two projects' architecture tests and the module's pure list | a red guard mid-task | S2 is its own commit, no behaviour change, verified by the module's whole namespace green before S3 starts |
| The hook's resolution cost on a slow `PATH` (WSL with `/mnt/c/...` entries, DrvFs stats at ~ms each) | the 1.5 s budget eaten before git runs | T6 measures it; the hook resolves `git` without `.exe` on Linux, and D5 drops nothing from the hook's `PATH` except the empty/relative entries (the hook has no forbidden roots but its cwd) |
| Git for Windows: `C:\Program Files\Git\cmd\git.exe` is a launcher that sets up `mingw64\bin`; resolving THROUGH it to `mingw64\bin\git.exe` would launch a git without its helpers' `PATH` | `fetch` cannot find `git-remote-https` | the real path of `cmd\git.exe` IS `cmd\git.exe` (no link); the plan launches the file `PATH` names by its real path, never a file it chooses inside the installation — the risk is named so the Windows row of T2 checks it |
| D6 changes `ScratchWorktree`'s contract (`worktree remove` → delete) while a sibling plan assumes the registry entry | a leaked registry entry nobody prunes, or a prune that deletes a clone | `grep -rn "worktree remove\|worktree prune"` at S6; the only callers are the three of §2.2 |

## 10. Definition of Done

- [ ] T1 green over `src/` and `hosts/`, with its three companions green; the first red run's file list is in the stories document.
- [ ] T2–T5, T9 green on the CI's git; a break-it per T5 row recorded red (the flag removed, the marker written); the filter rows green with S6 or recorded red with D6 cut.
- [ ] T6 green: the hook ends a hanging git within its budget, reads both pipes, launches by the real path under a built environment, and exits 0 on every path.
- [ ] T7 green (or S6 cut, and §6's residual promoted to the plan's deviations).
- [ ] T8 green: no gate word, no `powershell.exe`, no `cmd.exe` by a bare name.
- [ ] T10 green: `Bench.Domain` references nothing; `ModelArms.Core`'s pure list names the deciders that stayed; the module's scan and the "nothing in src references the module" guard untouched and green; the hook's reference shape asserted.
- [ ] `dotnet build dew_flow_benchmark.slnx -c Release` 0 warnings; the whole suite's failures equal the main-branch list of the host (none new).
- [ ] `research/module_scouts.md`, `research/architecture.md`, `research/module_model_arms.md:615-616` updated; the moved files' new homes named in the module doc's table (§2.3's rows re-pointed).
- [ ] [PLAN_model_arms.md](PLAN_model_arms.md) §5's boundary line (added by this plan's commit) still true; this plan promoted with deviations — what T5 could NOT observe, which flag was deleted as inert, whether S6 was built.
- [ ] Both plan-lifecycle checkers clean; the README rows match the folders.

## Boundaries (named on both sides, `common.planning-docs`)

| Item | This plan | [PLAN_model_arms.md](PLAN_model_arms.md) (D17 d, D26 b, c) | Disjoint? |
|---|---|---|---|
| launches under `tools/model-arms` | — | every one, by the real path, built environment, chosen cwd; its own scan | yes |
| launches under `src/` and `hosts/` | every one (§2.1); its own scan (T1) | named and left (`research/module_model_arms.md:615-616`) | yes |
| `ExecutableRule`, `ExecutableResolution`, `ExecutableAnswer`/`HostPaths`, `PathScope`, `GitEnvironment`, `RealPath` | MOVES them to `Bench.Domain.Process` / `Bench.Infrastructure.Process` and owns them after | consumes them from there; `ConfigFolders`, `RootFacts`, `ArmGit`, `KernelFacts` stay the module's | yes |
| the hardening flag set | the product's baseline and profiles (D4), observed by T5 | `ArmGit`'s three questions and their flags, observed by `StepGitTests` — may adopt the product's baseline later, not by this plan | yes |
| OS isolation of untrusted trees | not here (§6) | not there (D17's trust check is about the operator's files) — [PLAN_code_lane.md](PLAN_code_lane.md) §4.2 | yes |

Order: this plan after model-arms E2 (shipped `5a8b4ce`) — it moves files E2 landed; model-arms E3/E4 (`TargetTools`,
`TaskWorktrees`) should be built against the moved deciders if this plan lands first, or re-pointed by S2 if they land
first — either way one implementation. The model-arms plan carries the same table's one-line summary in its §5.

## Epics

Cornerstones: 0 of 1 — none marked yet; the cornerstone AREA is the launch policy (E1.S3, `GitLaunch`: the security
judgement of D4/D5 — its spec is Fable's, its code Sonnet's, its review Opus's, per `common.subagent-models`).

One epic, because the slices depend on one road in; Q = ⌈2·1/5⌉ = 1. The stories document
(`todo/PLAN_product_git_by_resolved_path_E1.md`) is written when the epic starts, by the judge role, from the story map
below and §5's ids; the ledger line above is updated by the commit that marks the cornerstone.

### E1 — The product launches nothing by a bare name (M) — cornerstone area: the launch policy

**Goal.** Every launch under `src/` and `hosts/` goes by a resolved real path, under a built environment, from a chosen
directory; every git launch carries the baseline and its profile; the module and the product share one resolver; the
scan keeps it so.

**Scope.** D1–D11, §5 T1–T10, §7 steps 0–7. **Depends on.** Nothing open; model-arms E2 shipped. **Done when.** §10.

| Story | What | Decisions | Files | Tests | Size |
|---|---|---|---|---|---|
| E1.S1 | the structural scan over `src/` and `hosts/`, two patterns, allow-list, three companions — written first, seen red with the eight files of §2.1 | D9 | `tests/Bench.Tests/ArchitectureTests.cs` | T1 | S |
| E1.S2 | the move: five pure deciders → `src/Bench.Domain/Process/`, `RealPath` → `src/Bench.Infrastructure/Process/`, the module re-pointed, `CliExecutable` retired into `ToolExecutable.Resolve` (shim refusal, `PATHEXT`), the hook's file link, the architecture tests updated | D2, D3 | the moved files; `tools/model-arms/**` usings; `ModelArmsArchitectureTests.PureDeciders`; `hosts/Hook/Hook.csproj`; `CliExecutableTests` → `ToolExecutableTests` | T10 | M |
| **E1.S3** (cornerstone) | `GitLaunch`: `GitExecutable.Resolved` once per process, the forbidden roots, the built environment (`GIT_*` strip, `PATH` rebuilt, `HOME`/`XDG` judged, `GIT_NO_LAZY_FETCH`, `GIT_TERMINAL_PROMPT`), the baseline and the three profiles, every `GitCommand` call site given its profile, `GitBlobBatch` and `ScoutTrees.InitOneCommitAsync` through it, the planted-program theory and its break-its | D1, D4, D5, D10, D11 | `src/Bench.Infrastructure/Git/GitLaunch.cs` (new), `GitHistory.cs`, `GitBlobBatch.cs`, `ScoutTrees.cs`, the 13 files of §2.2 (a profile argument each) | T2–T5, T9 | M |
| E1.S4 | the hook: in-process resolution, built environment, baseline + `--no-optional-locks`, both pipes read under the budget, tree kill, `NotChecked` on every refusal | D7 | `hosts/Hook/Worktree.cs` | T6 | S |
| E1.S5 | the other bare names: the gate command's word resolved before the first phase, `powershell.exe` and `ComSpec` resolved and launched from a chosen directory | D8 | `ScratchWorktree.cs` (`GateProcess`), `HarvestGates.cs`, `SolutionSignals.cs`, `MachineProbe.cs`, `HardwareSampler.cs`, `ScoutAccessPlanting.cs` | T8 | S |
| E1.S6 | scratch clones in place of linked worktrees; the filter rows of T5 green | D6 | `ScratchWorktree.cs`, its three callers | T7, T5 (filter rows) | S–M; cut-able |
| E1.S7 | the record: the three research documents, the module doc's "named and left" closed, this plan promoted with deviations | — | `research/module_scouts.md`, `research/architecture.md`, `research/module_model_arms.md`, this file | the lifecycle checkers | S |

**Every story's review checks D26 (a)–(f) of the model-arms plan by name where it applies** — this plan inherits the
same findings rather than rediscovering them: (b) no bare name, (c) `HOME`/`XDG_CONFIG_HOME` judged, and the kernel's
real path, never a textual join (a).
