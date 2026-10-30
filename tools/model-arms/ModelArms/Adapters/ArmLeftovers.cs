using System.Text;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>What an earlier <c>make</c> left of an arm (E3.S3, D26 a, e): the one place the module DELETES an arm's work, so the guard is the whole class. A folder <c>r/&lt;token&gt;</c> without <c>chain/made-&lt;arm&gt;</c> is a crashed make's — and is removed whole only on POSITIVE evidence that the arm never ran. A guard that took
/// "I cannot tell" for "no" would delete a finished arm, which is days of model work; so every doubt is an exit 4 that touches nothing:
/// <list type="bullet">
/// <item>the folder is not a real folder — a link (whatever it names is never entered: it is an operator's mistake or a planted one) or a file;</item>
/// <item><c>out/timing.jsonl</c> through <see cref="BoundedFile"/> (1 MiB) is <i>refused</i> — a link, a FIFO, a folder, past the limit, not UTF-8 — or holds a line that does not read as a timing line: <c>cannot tell whether &lt;arm&gt; ran</c>;</item>
/// <item>the timing holds a <c>start</c> line: <c>&lt;arm&gt; has run</c> — restore its marker or move its folder away; make never remakes an arm that ran.</item>
/// </list>
/// Only an <i>absent</i> timing file, or one whose every line reads and none is a <c>start</c> (a runner that stopped at its admission), is evidence of a make that never reached a run. The removal is <c>Directory.Delete(recursive)</c>, which unlinks a link inside the tree as a link and never descends through it (observed on this runtime, <c>MakeVerbTests</c> plants outside sentinels);
/// a folder it cannot remove (one a build left at mode 0555) is a failure, exit 3, left as it is.
/// <para>
/// What this does NOT prove: that the folder is the arm's and nobody else's (the token is the arm's name by the map; a folder at that name is taken to be it); a link in a PARENT of <c>timing.jsonl</c> (<c>out</c> itself a link — the bounded road's stated residual: the delete does not follow it, and a <c>start</c> line a link names still stops the removal).
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
            ? Timing(arm, BoundedFile.Read(paths.Timing, TimingLimit, road))
            : new Verdict.Stop(4, $"{arm}'s folder {paths.Folder} is not a folder — it is not removed; move it away by hand");
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

        return lines.Any(line => line is TimingLine.Start)
            ? new Verdict.Stop(4, $"{arm} has run (its timing has lines) but has no made marker — restore chain/made-{arm} or move its folder away; make never remakes an arm that ran")
            : new Verdict.NeverRan();
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
