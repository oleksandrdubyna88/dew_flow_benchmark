# PLAN — the benchmark arrives in the daemon as a PINNED commit, not a sibling path

> Status: **plan only, 2026-08-23.** Scope: four `ProjectReference` lines and one `.gitmodules` entry in
> `dew_flow_rag_qln`; nothing in this repository changes except that its commits must reach its remote.
>
> Extracted from [../research/PLAN_bench_console.md](../research/PLAN_bench_console.md) when that plan was
> promoted: the console shipped and every line of its Definition of Done is met, but §5's *submodule* is the
> one thing that arrived as something else. A mechanical chore left inside a document filed as documentation
> is a chore nobody will find, so it lives here instead.

## 1. The symptom

`dew_flow_rag_qln` builds the benchmark console out of a path that leaves its own repository:

```
hosts/Daemon/Daemon.csproj:46-48   ../../../dew_flow_benchmark/src/Bench.Ui|Bench.Api|Bench.Infrastructure
hosts/Daemon.Client/Daemon.Client.csproj:21   ../../../dew_flow_benchmark/src/Bench.Ui
```

So the daemon builds **only** on a machine where the two repositories happen to sit side by side, in
directories named exactly this, at whatever commit that tree happens to be on. A clone of `dew_flow_rag_qln`
does not build at all, and two machines with both repositories can build two different consoles from the same
qln commit — which is the reproducibility property this whole family exists to defend, broken in the build
system rather than in a measurement.

`BenchModule` (`dew_flow_rag_qln · hosts/Daemon/BenchModule.cs`) says so in its own comment and names the fix.

## 2. Why it was not done, and what changed

The blocker on record was that *"pinning a commit requires the benchmark's commits to exist on its remote,
and pushing them is not this session's to do"*. That is still the only blocker: a submodule records a commit
id, and a commit that lives on one laptop pins nothing.

Two facts make it small now:

- `dew_flow_benchmark` **has** a remote (`origin`, `github.com/oleksandrdubyna88/dew_flow_benchmark`) and
  `main` tracks it. On 2026-08-23 the local branch was level with `origin/main`.
- The precedent is already in the same file. `external/dew_flow_mcp` is a submodule of this exact shape, and
  `Daemon.csproj` references six projects out of it (`:37-43, 49-50`).

## 3. The shape

1. **Push** `dew_flow_benchmark` so the commit to be pinned exists on the remote. This is the whole blocker
   and it is a decision for whoever owns the remote, not a technical obstacle.
2. `git submodule add https://github.com/oleksandrdubyna88/dew_flow_benchmark external/dew_flow_benchmark`
   in `dew_flow_rag_qln`.
3. Repoint the four `ProjectReference` lines at `..\..\external\dew_flow_benchmark\src\…`, matching the
   backslash style the MCP lines beside them already use.
4. Build the daemon and run the qln suite.

**The build props resolve correctly and this is worth checking rather than assuming.** MSBuild walks UP from
a project file and stops at the first `Directory.Build.props`/`Directory.Packages.props` it finds, so a
project under `external/dew_flow_benchmark/src/` finds the benchmark's own — the same resolution the sibling
path already gets, and the reason `Daemon.csproj:44-45` says *"its own build props"*. The MCP submodule proves
the arrangement works in this repository today.

## 4. What it costs, stated because it is a real trade

The console in the daemon **freezes at the pinned commit**. Editing `Bench.Ui` in the sibling checkout stops
showing up in the running daemon until someone bumps the pin — which is exactly what a pin means, and exactly
what makes a clone reproducible. Anyone doing console work will want the pin bumped in the same task; that is
one command and belongs in the change that touched the console.

## 5. Test plan

- The daemon builds with the sibling directory **renamed away**, which is the only real proof the path
  dependency is gone.
- The qln suite passes unchanged — the mount is a reference change, not a behaviour change.
- `git submodule status` reports the benchmark beside `dew_flow_mcp` and `.claude/rules/shared`.

## 6. Definition of Done

- [ ] The pinned commit exists on `origin`.
- [ ] `dew_flow_rag_qln` reaches the benchmark only through `external/dew_flow_benchmark`; no path leaves the
      repository.
- [ ] The daemon builds with the sibling checkout renamed away.
- [ ] The qln suite passes.
- [ ] [../research/PLAN_bench_console.md](../research/PLAN_bench_console.md)'s deviation note records that
      its §5 finally landed, and this plan is promoted.
