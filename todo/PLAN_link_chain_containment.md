# PLAN — a link's target path is resolved root-down in the scout audit and the artifact containment

> Status: **plan only, 2026-11-01 — nothing built.** Raised by the link-chain audit of the `dew_flow_*` family on
> 2026-11-01 (the same defect was fixed in `dew_flow_mcp` #61 and `dew_flow_connect_other_ais` #938). Scope: the two
> places in this repository that resolve a link to decide containment. References verified at `ced49afb`.

## The symptom

A containment check that resolves a path's links must resolve them as the kernel does: one segment at a time, from
the filesystem root, with a link's own target put in front of the rest of the path, so the folders INSIDE a link's
target are resolved too. Two places here do not do that.

### 1. The scout path audit — MEDIUM

`src/Bench.Infrastructure/Scouts/FileSystemLinkTargets.cs:34-40` (`Final`) replaces a link segment by
`ResolveLinkTarget(returnFinalTarget: true).FullName`. That is the final target's SPELLING. When the target path
itself runs through a second link, the second link is never resolved. Before that, `TreePaths.Resolve`
(`src/Bench.Domain/Scouts/TreePaths.cs:17-22`) folds `..` as text, so `l3/../x` with `l3` a link is judged as `x`
beside it, while the OS reads `x` beside `l3`'s target.

What it is used for: `CliScoutRunner` (`src/Bench.Infrastructure/Scouts/CliScoutRunner.cs:192`) passes
`FileSystemLinkTargets.Here` to `PathAudit.Audit` (`src/Bench.Domain/Scouts/PathAudit.cs:54`). After a scout run,
the audit reads the child's transcript. A Read, Glob, Grep or LS target that resolves outside the tree and returned
content makes the verdict STOP the campaign.

Reproduction (measured on Linux, 2026-11-01): in the tree, `alias` → `<tree>/l2/secret.txt` and `l2` → a folder
outside the tree. `Resolve(<tree>/alias)` answers `<tree>/l2/secret.txt` (inside), while a read of `alias` returns
the outside file. **Consequence:** the audit can miss a read outside the tree, so a campaign that should STOP goes
on. The audit reads nothing itself, so this is a missed detection, not a read. Hence MEDIUM.

Not checked: whether the Claude CLI also folds `..` as text before it reads. If it does, the second flaw judges
what the CLI actually reads, and only the first one stays.

### 2. The artifact containment — LOW

`src/Bench.Infrastructure/Gate/ArtifactContainment.cs:48-75` walks root-down and recurses on a link's target, with
a 40-hop budget, and fails closed. It already resolves the folders inside a target. Two gaps stay:

- `ResolveFrom` starts with `Path.GetFullPath(path)` (`:50`), also for a link's text, so a `..` inside a link's
  text is folded as a string before the walk (`a` → `l3/../x` with `l3` a link is judged as `x` beside `a`).
- To check by a test: the hop count is not cumulative. The recursion returns only the path, and the walk carries
  on with `hops + 1` (`:72-73`), not with what the nested walk spent. Sibling links each start from the same
  count. A target that holds several links, each with the same shape, may then cost more than the budget suggests.

What it is used for: probe-tree removal (`src/Bench.Infrastructure/Probes/ProbeTrees.cs:15`), the tap pruner
(`src/Bench.Infrastructure/Gate/TapPruner.cs:102`), the probe and scout artifact writes
(`src/Bench.Infrastructure/Probes/ProbeArtifacts.cs:22`, `src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:241`),
the gate store root (`src/Bench.Infrastructure/Gate/FileSystemGateArtifactStore.cs:42`), the probe roots
(`src/Bench.Infrastructure/Probes/ProbeRoots.cs:38`) and the campaign key
(`src/Bench.Infrastructure/Scouts/ScoutCampaignKey.cs:41`). **Consequence:** a removal or a write can be judged
inside when it lands outside. That needs a crafted link with `..` in its text inside the benchmark's own artifact
root, so LOW.

## What must be true when done

- Both places resolve the way the two fixed resolvers do: root-down, one cumulative budget (40; 32 on macOS, its
  kernel's `MAXSYMLINKS`), the root's own links resolved once and not paid by every request, `..` taken by the disk
  and not folded as text (a `..` after a missing name or after a file is refused), and any failure is a refusal.
- One resolver serves both places, not two copies. `ArtifactContainment` is the nearer one to extend.
- Each gap gets RED → GREEN → RED: the `alias` → `l2/secret.txt` layout for the audit; a link with `..` in its text
  for the containment; a branching link layout for the budget.
- Both resolvers to copy from: `dew_flow_connect_other_ais · src_mcp/runners/Files/RealPath.cs` and
  `dew_flow_mcp · src/Workspace.Infrastructure/SandboxedFileReader.cs` (`RealPathWalk`). Their tests list the
  layouts to cover (chained, relative, loop, budget, deep path, `..` after a missing name and after a file).

## Build order

1. Tell the sessions that work in this repository (benchmark work is shared) before the branch starts.
2. The containment resolver: the tests, then the walk. It is the base of step 3.
3. The scout audit's `LinkTargets` port on top of it. The cross-OS path arithmetic of `TreePaths` (a transcript
   captured on Windows is read on Linux) stays for the lexical part. Only the disk part moves to the resolver.
4. Docs: the module doc of the scouts and the gate, and this plan moved to `research/` with what shipped.

## Not in scope

The audit's lexical rules (`TreePaths`) for a transcript from another OS, and any change to what the audit does
with a finding.
