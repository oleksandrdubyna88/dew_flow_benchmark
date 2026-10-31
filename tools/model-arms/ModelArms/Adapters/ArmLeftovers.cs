using System.Text;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>What an earlier <c>make</c> left of an arm (E3.S3, D26 a, e): the one place the module DELETES an arm's work, so the guard is the whole class. A folder <c>r/&lt;token&gt;</c> without <c>chain/made-&lt;arm&gt;</c> is a crashed make's — or an arm that RAN and lost its marker, which is days of model work. It is removed whole only on POSITIVE evidence that the arm never ran, and
/// the absence of evidence is not that: every doubt is an exit 4 that touches nothing. A half-made arm of this <see cref="ArmMaker"/> always has a real <c>out</c> (made before the copy), so the evidence is read there:
/// <list type="bullet">
/// <item>the folder is not a real folder — a link (never entered) or a file;</item>
/// <item><c>out</c> is there but is not a real folder (a link, live or dangling, a file, anything else): <c>cannot tell whether &lt;arm&gt; ran</c> — the evidence may be behind the link;</item>
/// <item><c>out</c> is absent: <i>never ran</i> ONLY when <c>arm</c>, <c>notes</c>, <c>work</c> and <c>tmp</c> are absent too, checked by name — the crash window between making <c>r/&lt;token&gt;</c> and making <c>out</c>; otherwise <i>cannot tell</i> (somebody removed <c>out</c> from an arm that has work);</item>
/// <item><c>out/timing.jsonl</c> through <see cref="BoundedFile"/> (1 MiB) is refused — a link, a FIFO, a folder, past the limit, not UTF-8 — or holds a line that does not read as a timing line: <c>cannot tell</c>;</item>
/// <item>the timing holds a line that is written only once an attempt ran — <c>start</c>, <c>end</c>, <c>gate-wait</c> or <c>arm-done</c>: <c>&lt;arm&gt; has run</c> — restore its marker or move its folder away; make never remakes an arm that ran.</item>
/// </list>
/// Only an absent timing file, or one whose every line is a <c>stopped</c> line (a runner that stopped at its admission, before any attempt), over a real <c>out</c>, is evidence of a make that never reached a run. The removal is <c>Directory.Delete(recursive)</c>, which unlinks a link inside the tree as a link and never descends through it (observed on this runtime, <c>MakeVerbTests</c> plants outside sentinels);
/// a folder it cannot remove (one a build left at mode 0555) is a failure, exit 3, left as it is.
/// <para>
/// What this does NOT prove: that the folder is the arm's and nobody else's (the token is the arm's name by the map; a folder at that name is taken to be it); a link in a PARENT of <c>r/&lt;token&gt;</c> (the root's own, which the gate judged).
/// </para></summary>
internal static class ArmLeftovers
{
    /// <summary>The most <c>timing.jsonl</c> may hold for this guard: a made arm that never ran has no timing, and a few lines are a few hundred bytes. More is not read — and is not removed.</summary>
    private const int TimingLimit = 1024 * 1024;

    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>What the folder is, as far as a removal may rely on it: <see cref="Gone"/> nothing there, <see cref="NeverRan"/> removable, or a stop that touches nothing.</summary>
    internal abstract record Verdict
    {
        private Verdict() { }

        public sealed record Gone : Verdict;

        public sealed record NeverRan : Verdict;

        public sealed record Stop(int ExitCode, string Why) : Verdict;
    }

    /// <summary>The verdict on <c>r/&lt;token&gt;</c> of <paramref name="arm"/>. Nothing is changed here.</summary>
    public static Verdict Judge(string arm, ArmPaths paths, PlatformRoad road) =>
        KernelFacts.Present(paths.Folder).Match(
            present => present ? Folder(arm, paths, road) : new Verdict.Gone(),
            reason => new Verdict.Stop(3, $"cannot tell whether {paths.Folder} exists: {reason}"));

    private static Verdict Folder(string arm, ArmPaths paths, PlatformRoad road)
    {
        var attributes = new FileInfo(paths.Folder).Attributes;
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return new Verdict.Stop(4, $"{arm}'s folder {paths.Folder} is a link — it is neither removed nor entered; move it away by hand");
        }

        return attributes.HasFlag(FileAttributes.Directory)
            ? Out(arm, paths, road)
            : new Verdict.Stop(4, $"{arm}'s folder {paths.Folder} is not a folder — it is not removed; move it away by hand");
    }

    /// <summary><c>out</c> by name, never followed: a real folder is where the evidence is read; absent is the crash window or somebody's removal; anything else cannot be trusted to say.</summary>
    private static Verdict Out(string arm, ArmPaths paths, PlatformRoad road) =>
        KernelFacts.Present(paths.Out).Match(
            present => present
                ? (IsRealFolder(paths.Out, road) ? Timing(arm, BoundedFile.Read(paths.Timing, TimingLimit, road)) : Cannot(arm, "out is not a real folder — a link, a file or something else"))
                : BeforeOut(arm, paths),
            reason => Cannot(arm, $"out cannot be looked at: {reason}"));

    /// <summary>The kernel's kind where the host has a reader (<c>statx</c>, no follow), the entry's own attributes elsewhere: a folder that is not a link.</summary>
    private static bool IsRealFolder(string path, PlatformRoad road) =>
        road.Trust == TrustRoad.Kernel
            ? KernelFacts.Read(path) is FactsRead.Read { Facts.Identity.Kind: FileKind.Directory }
            : new FileInfo(path).Attributes is var attributes && attributes.HasFlag(FileAttributes.Directory) && !attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>No <c>out</c>: the crash window of a make killed between <c>mkdir r/&lt;token&gt;</c> and <c>mkdir out</c> — nothing else exists yet. Anything that does exist is work whose <c>out</c> is gone, and nothing says it never ran.</summary>
    private static Verdict BeforeOut(string arm, ArmPaths paths)
    {
        foreach (var name in new[] { paths.CloneFolder, paths.Notes, paths.Work, paths.Tmp })
        {
            if (KernelFacts.Present(name) is not Outcome<bool>.Ok { Value: false })
            {
                return Cannot(arm, $"out is missing but {Path.GetFileName(name)} is there");
            }
        }

        return new Verdict.NeverRan();
    }

    private static Verdict Timing(string arm, BoundedRead timing) =>
        timing switch
        {
            BoundedRead.Absent => new Verdict.NeverRan(),
            BoundedRead.Bytes bytes => Lines(arm, bytes.Value),
            BoundedRead.Refused refused => Cannot(arm, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Verdict Lines(string arm, byte[] bytes)
    {
        IReadOnlyList<TimingLine> lines;
        try
        {
            lines = TimingLines.Read(Strict.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return Cannot(arm, "out/timing.jsonl is not UTF-8 text");
        }

        if (lines.Any(line => line is TimingLine.Other))
        {
            return Cannot(arm, "out/timing.jsonl holds a line that does not read as a timing line");
        }

        return lines.All(line => line is TimingLine.Stopped)
            ? new Verdict.NeverRan()
            : new Verdict.Stop(4, $"{arm} has run (its timing has lines) but has no made marker — restore chain/made-{arm} or move its folder away; make never remakes an arm that ran");
    }

    private static Verdict.Stop Cannot(string arm, string reason) => new(4, $"cannot tell whether {arm} ran ({reason}) — move its folder away by hand");

    /// <summary>Removes the folder whole. Only ever asked after <see cref="Judge"/> answered <see cref="Verdict.NeverRan"/>.</summary>
    public static Verdict Remove(ArmPaths paths)
    {
        try
        {
            Directory.Delete(paths.Folder, recursive: true);
            return new Verdict.Gone();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Verdict.Stop(3, $"cannot remove {paths.Folder}: {ex.Message}");
        }
    }
}
