using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What the pinned consumer made of one arm: its exit code, its stderr and the <c>report[&lt;listed&gt;]</c> it printed.</summary>
internal sealed record MetricsRun(int ExitCode, string Error, JsonNode? Report);

/// <summary>A rewrite a scratch copy of a pinned reader may carry, each matched EXACTLY ONCE (a reshaped script fails the helper by name): <see cref="RootLine"/> is the one line starting <c>const E = </c>; <see cref="BaseLiteral"/> is <c>t-survival.mjs</c>'s <c>'b4e2ea96'</c>, parts 1-5's base.</summary>
internal enum Rewrite { None, RootLine, BaseLiteral }

/// <summary>One checked-in reader of the harness: where it is, the sha256 it is pinned by, the rewrites its copy carries and how long it may run (ten times its measured wall on this host, never under 30 s - the module doc's readers table).</summary>
internal sealed record PinnedReader(string Name, string RepositoryPath, string Sha256, IReadOnlyList<Rewrite> Rewrites, TimeSpan Timeout);

/// <summary>What one run of a reader left: its exit code, both pipes' text and the scratch folder its outputs were written to.</summary>
internal sealed record ReaderRun(int ExitCode, string Output, string Error, string Scratch);

/// <summary>The checked-in readers of the harness (<c>research/local-model-coding/harness/</c> and <c>part6/</c>) run over files this build wrote (E2 §8 item 55 for <c>metrics.mjs</c>; E6.S1 for the other six, <see cref="PinnedReaders"/>). A script binds its root and its arm list in
/// the file — <c>metrics.mjs:11</c> is <c>const E = '/home/jinx/git/coai-exp'</c> and its arms are a fixed list filtered by the existence of <c>&lt;E&gt;/results/&lt;arm&gt;/timing.jsonl</c> — so the helper copies it to a scratch folder with exactly the rewrites its <see cref="Rewrite"/>s name (each
/// matched once: a reshaped script fails the helper by name instead of being read silently wrong), the caller links what the reader looks for, and <c>node</c> runs by its resolved path (D17 d) under a HOME of the caller's. The pinned file is never edited; its hash is checked before it is copied.
/// <para>
/// What this does NOT prove: that a reader's arithmetic is right (it is run, not judged), that a line a real claude writes is read as the replayed shapes are, or the judges (<c>ObjectiveReportTests</c>' round trip stands for them, D28).
/// </para></summary>
internal static class PinnedConsumer
{
    private static readonly Lazy<string> Node = new(() => ExecutableResolution
        .Resolve("node", (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator), [], RootFacts.Host, OperatingSystem.IsWindows())
        is ExecutableAnswer.Found found ? found.Real : string.Empty);

    /// <summary>The <c>node</c> the test process's PATH resolves to, as its real path (empty when none does): what a test that runs a pinned script launches (D17 d).</summary>
    public static string NodePath => Node.Value;

    /// <summary>The script over a made arm of <paramref name="root"/> (E2.S8; E6 reuses it for every scheme): the arm's <c>out/timing.jsonl</c>, under the name the script lists the scheme by, in a scratch folder of the root's own.</summary>
    public static Task<MetricsRun> MetricsAsync(ExperimentRoot root, string arm, CancellationToken cancellationToken) =>
        MetricsAsync(root.Scratch("metrics-" + arm), root.PathsOf(arm).Timing, Listed(arm), cancellationToken);

    /// <summary>The ONE name map: the name parts 1-5's readers list an arm by (<c>metrics.mjs:12</c>, <c>make-tables.mjs:16</c>, <c>t-survival.mjs</c>'s argv, the keys of <c>metrics.json</c>): <c>C1</c> is <c>C</c>, <c>CHH1</c> is <c>CH1</c>, <c>SRS1</c> is <c>SR1</c> (the Sonnet-tasks, Haiku-codes
    /// shape of parts 1-5) and <c>CR1</c> is itself. An arm NAME they have no spelling for is a test error. Whether one reader KEEPS the row is not this map's answer: <c>compare.mjs:12</c> lists no <c>CR*</c>, <c>CH*</c> or <c>SR*</c> arm and drops them by its own list.</summary>
    public static string Listed(string arm) =>
        arm switch
        {
            "C1" => "C",
            "CR1" => "CR1",
            "CHH1" => "CH1",
            "SRS1" => "SR1",
            _ => throw new ArgumentException($"the pinned readers of parts 1-5 have no name for an arm named {arm}", nameof(arm)),
        };

    /// <summary>Runs <c>metrics.mjs</c> over <paramref name="timingFile"/>, which the script finds as the timing of the arm it lists as <paramref name="listed"/> (<c>C</c> for the C scheme). HOME is the scratch: an interrupted attempt finds no session transcript and reads as <c>complete: false</c>.</summary>
    public static async Task<MetricsRun> MetricsAsync(string scratch, string timingFile, string listed, CancellationToken cancellationToken)
    {
        var results = Directory.CreateDirectory(Path.Combine(scratch, "results", listed)).FullName;
        File.CreateSymbolicLink(Path.Combine(results, "timing.jsonl"), timingFile);
        var copy = await CopyAsync(PinnedReaders.Metrics, scratch, new Dictionary<Rewrite, string> { [Rewrite.RootLine] = scratch });
        var environment = new Dictionary<string, string> { ["HOME"] = scratch, ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty };
        var attempt = await ProcessRunner.RunAsync(Node.Value, [copy, "--json"], scratch, TimeSpan.FromSeconds(60), string.Empty, environment, cancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return new MetricsRun(done.ExitCode, done.StandardError, done.ExitCode == 0 ? JsonNode.Parse(done.StandardOutput)!["report"]![listed] : null);
    }

    /// <summary>The copy of <paramref name="reader"/> in <paramref name="folder"/>, under its own file name: the pinned file's sha256 is checked FIRST, then each rewrite of the reader is applied to the one line or literal it names (zero or two matches fail naming the reader and the rewrite), with
    /// the value <paramref name="replacements"/> gives it. The repository's file is never written.</summary>
    public static Task<string> CopyAsync(PinnedReader reader, string folder, IReadOnlyDictionary<Rewrite, string> replacements)
    {
        Assert.False(Node.Value.Length == 0, "a node must resolve on the test process's PATH to run a pinned reader");
        var source = Path.Combine(Cli.Repository.Root, reader.RepositoryPath);
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source))).Should().Be(reader.Sha256, $"{reader.Name} is pinned: a different {reader.Name} is a different test");
        var text = File.ReadAllText(source);
        foreach (var rewrite in reader.Rewrites)
        {
            text = Rewritten(reader, text, rewrite, replacements.TryGetValue(rewrite, out var value) ? value : throw new ArgumentException($"{reader.Name} carries the rewrite {rewrite} and no value was given for it", nameof(replacements)));
        }

        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, Path.GetFileName(reader.RepositoryPath));
        File.WriteAllText(copy, text);
        return Task.FromResult(copy);
    }

    private static string Rewritten(PinnedReader reader, string text, Rewrite rewrite, string value) =>
        rewrite switch
        {
            Rewrite.RootLine => RootLineRewritten(reader, text, value),
            Rewrite.BaseLiteral => BaseLiteralRewritten(reader, text, value),
            _ => text,
        };

    private static string RootLineRewritten(PinnedReader reader, string text, string root)
    {
        const string prefix = "const E = ";
        var lines = text.Split('\n');
        lines.Count(line => line.StartsWith(prefix, StringComparison.Ordinal)).Should().Be(1, $"the helper rewrites exactly one root line of the pinned {reader.Name} - a reshaped script must be seen, not read silently wrong");
        return string.Join('\n', lines.Select(line => line.StartsWith(prefix, StringComparison.Ordinal) ? $"{prefix}'{root}';" : line));
    }

    private static string BaseLiteralRewritten(PinnedReader reader, string text, string commit)
    {
        const string literal = "'b4e2ea96'";
        (text.Split(literal).Length - 1).Should().Be(1, $"the helper rewrites exactly one base literal of the pinned {reader.Name} - without it git diff fails inside the swallowing catch and every line reads as dropped");
        return text.Replace(literal, $"'{commit}'", StringComparison.Ordinal);
    }

    /// <summary>Runs <paramref name="copy"/>, the copy of a reader that <see cref="CopyAsync"/> made in the <paramref name="scratch"/> folder (or below it), with <c>node</c> by its resolved path under EXACTLY <paramref name="environment"/> (the caller names <c>PATH</c> and <c>HOME</c>) in <paramref name="workingDirectory"/>, within the reader's own <see cref="PinnedReader.Timeout"/>. A reader that does not end in time fails the test
    /// naming the reader, its timeout and the scratch, which is PRESERVED: moved out of the fixture's folder to a sibling <c>testsToDelete-modelarms-reader-&lt;name&gt;-&lt;id&gt;</c> the fixture does not own (a legacy name, removed by hand), its own outputs kept.</summary>
    public static async Task<ReaderRun> RunAsync(PinnedReader reader, string scratch, string copy, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string workingDirectory, CancellationToken cancellationToken)
    {
        var attempt = await ProcessRunner.RunAsync(Node.Value, [copy, .. arguments], workingDirectory, reader.Timeout, string.Empty, environment, cancellationToken);
        switch (attempt)
        {
            case ProcessAttempt.Completed completed:
                return new ReaderRun(completed.Result.ExitCode, completed.Result.StandardOutput, completed.Result.StandardError, scratch);
            case ProcessAttempt.TimedOut timedOut:
                Assert.Fail($"{reader.Name} did not end within its timeout of {reader.Timeout.TotalSeconds:0.###} s ({attempt.Describe}); its scratch is kept at {Preserved(reader, scratch)} (a leftover to remove by hand); stderr so far: {timedOut.StandardError}");
                return new ReaderRun(-1, string.Empty, string.Empty, scratch);
            default:
                Assert.Fail($"{reader.Name} could not be started: {attempt.Describe}");
                return new ReaderRun(-1, string.Empty, string.Empty, scratch);
        }
    }

    private static string Preserved(PinnedReader reader, string scratch)
    {
        var owner = new DirectoryInfo(scratch);
        while (owner.Parent is not null && !owner.Name.StartsWith("testsToDelete-modelarms-", StringComparison.Ordinal))
        {
            owner = owner.Parent;
        }

        var parent = owner.Parent?.FullName ?? Path.GetTempPath();
        var kept = Path.Combine(parent, $"testsToDelete-modelarms-reader-{Path.GetFileNameWithoutExtension(reader.Name)}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.Move(scratch, kept);
        return kept;
    }
}
