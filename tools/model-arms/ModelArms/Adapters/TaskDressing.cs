using System.Globalization;
using System.Text;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;

namespace ModelArms.Adapters;

/// <summary>A worktree's dressing (E4.S2): the rules submodule from the arm's OWN checkout, the <c>node_modules</c> links, the warm build — in that order (the links' parents are the submodule's folders). Every git is <see cref="ArmGit"/>'s; the build is the target's own wrapper through <see cref="TargetTools"/>.</summary>
internal sealed class TaskDressing(TaskContext c)
{
    private const long BuildLogCap = 1_048_576;

    public async Task<Halt?> DressAsync(string id, bool build, CancellationToken ct)
    {
        if (RefusedEntry(id) is { } refused)
        {
            return refused;
        }

        if (await SubmoduleAsync(id, ct) is { } submoduleHalt)
        {
            return submoduleHalt;
        }

        c.Probe.Reached(TaskPoint.SubmoduleUpdated, id);
        if (LinkAll(id) is { } linkHalt)
        {
            return linkHalt;
        }

        c.Probe.Reached(TaskPoint.Linked, id);
        if (build && await BuildAsync(id, ct) is { } buildHalt)
        {
            return buildHalt;
        }

        c.Probe.Reached(TaskPoint.Built, id);
        return null;
    }

    private Halt? RefusedEntry(string id) =>
        c.Target.NodeModules.FirstOrDefault(entry => Path.IsPathRooted(entry) || entry.Replace('\\', '/').Split('/').Contains("..")) is { } entry
            ? new Halt(ExitCodes.Configuration, $"{id}: target.nodeModules entry '{entry}' is rooted or climbs out of the worktree — refused", id)
            : null;

    private async Task<Halt?> SubmoduleAsync(string id, CancellationToken ct)
    {
        var listed = await c.Clone.SubmodulePathsAsync(ct);
        if (listed is Outcome<string>.Fail listFailed)
        {
            return TaskContext.Failed(id, listFailed.Reason);
        }

        var name = GitModules.NameOf(((Outcome<string>.Ok)listed).Value, c.Target.RulesSubmodule);
        if (name.Length == 0)
        {
            return new Halt(ExitCodes.Configuration, $"{id}: target.rulesSubmodule {c.Target.RulesSubmodule} is not a submodule of the arm's clone", id);
        }

        if (await c.Clone.SetSubmoduleUrlAsync(name, Path.Combine(c.Paths.CloneFolder, c.Target.RulesSubmodule), ct) is Outcome<string>.Fail urlFailed)
        {
            return TaskContext.Failed(id, urlFailed.Reason);
        }

        return await c.In(c.Paths.Worktree(id)).ArmSubmoduleUpdateAsync(c.Target.RulesSubmodule, ct) is Outcome<string>.Fail updateFailed ? TaskContext.Failed(id, updateFailed.Reason) : null;
    }

    private Halt? LinkAll(string id)
    {
        foreach (var entry in c.Target.NodeModules)
        {
            if (Link(id, entry) is { } halt)
            {
                return halt;
            }
        }

        return null;
    }

    /// <summary>One link <c>&lt;wt&gt;/&lt;entry&gt;</c> → <c>&lt;clone&gt;/&lt;entry&gt;</c>, only for a folder the clone has, made exclusively, and only through real folders of the worktree: <c>symlink(2)</c> resolves the parent path THROUGH links (M-w) and the worktree's tree is the arm branch's — the model's — so EVERY existing component of the
    /// link's parent, from the worktree down, is read with lstat semantics and a link among them stops the arm (D26 a, §8 item 24). The clone's side is the model's own and gets no check.</summary>
    private Halt? Link(string id, string entry)
    {
        var source = Path.Combine(c.Paths.CloneFolder, entry);
        if (!Directory.Exists(source))
        {
            c.Log.Line($"no {entry} in the clone — not linked");
            return null;
        }

        var wt = c.Paths.Worktree(id);
        var link = Path.Combine(wt, entry);
        var parent = Path.GetDirectoryName(link)!;
        if (LinkedComponent(id, wt, parent) is { } linked)
        {
            return linked;
        }

        if (!Directory.Exists(parent))
        {
            c.Log.Line($"{id}: {Path.GetRelativePath(wt, parent)} is not in the worktree — {entry} not linked");
            return null;
        }

        return Create(id, entry, link, source);
    }

    /// <summary>The first existing component of <paramref name="parent"/>, from the worktree down, that is a link.</summary>
    private static Halt? LinkedComponent(string id, string wt, string parent)
    {
        var relative = Path.GetRelativePath(wt, parent);
        var current = wt;
        foreach (var component in new[] { string.Empty }.Concat(relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar)))
        {
            current = component.Length == 0 ? wt : Path.Combine(current, component);
            if (!Path.Exists(current))
            {
                return null;
            }

            if (RootFacts.LinkTargetOf(current) is { } target)
            {
                var label = component.Length == 0 ? $"wt/{id}" : Path.GetRelativePath(wt, current);
                return new Halt(ExitCodes.Configuration, $"{id}: {label} in wt/{id} is a symbolic link (→ {target}) — a node_modules link is made only through real folders of the worktree", id);
            }
        }

        return null;
    }

    private static Halt? Create(string id, string entry, string link, string source)
    {
        try
        {
            File.CreateSymbolicLink(link, source);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Halt(ExitCodes.Environment, $"{id}: cannot link {entry}: {ex.Message}", id);
        }
    }

    /// <summary>The warm build (<c>t-lib.sh:19</c>) through the target's own wrapper and tools, under the target's environment, into <c>out/wt-build-&lt;id&gt;.log</c>: the old log deleted by name, the new one created exclusively before the child starts and capped where it is written (§8 item 19). A failure, a kill at the wall or a start
    /// failure stops the arm with exit 3 and leaves the worktree (D16 — the shell only warned).</summary>
    private async Task<Halt?> BuildAsync(string id, CancellationToken ct)
    {
        var path = c.Paths.WorktreeBuildLog(id);
        Directory.CreateDirectory(c.Paths.Out);
        File.Delete(path);
        var created = ToolLog.Create(path);
        if (created is Outcome<ToolLog>.Fail failed)
        {
            return new Halt(ExitCodes.Environment, $"{id}: {failed.Reason}", id);
        }

        using var log = ((Outcome<ToolLog>.Ok)created).Value;
        var capped = new CappedStream(log.Stream, BuildLogCap);
        var run = await new TargetTools(TargetEnvironment.Of(c.Tools.Environment), c.Clock).RunAsync(TargetArgv.Build(c.Target, c.Tools.Build, c.Paths.Worktree(id)), capped, ct);
        capped.Finish();
        return run is { Started: true, KilledAtWall: false, Exit: { WasCaptured: true, Value: 0 } }
            ? null
            : new Halt(ExitCodes.Environment, $"the warm build of {id}'s worktree failed (see out/wt-build-{id}.log)", id);
    }

    /// <summary>The first <c>cap</c> bytes of what is written, then the number dropped: the warm build's log (§8 item 19). The build is not stopped by the cap; its wall stops it.</summary>
    private sealed class CappedStream(Stream inner, long cap) : Stream
    {
        private long _kept;
        private long _dropped;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var room = (int)Math.Min(buffer.Length, Math.Max(0, cap - _kept));
            inner.Write(buffer[..room]);
            _kept += room;
            _dropped += buffer.Length - room;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Flush() => inner.Flush();

        /// <summary>The one line that says how much was not kept.</summary>
        public void Finish()
        {
            if (_dropped > 0)
            {
                inner.Write(new UTF8Encoding(false).GetBytes($"[model-arms: the build's output past 1 MiB was not kept — {_dropped.ToString(CultureInfo.InvariantCulture)} bytes dropped]\n"));
                inner.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
