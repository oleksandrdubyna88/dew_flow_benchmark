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

/// <summary>The checked-in <c>research/local-model-coding/harness/metrics.mjs</c> run over files this build wrote (E2 §8 items 55; E2.S8 extends the helper for the four readers). The script binds its root and its arm list in
/// the file — line 11 is <c>const E = '/home/jinx/git/coai-exp'</c> and the arms are a fixed list filtered by the existence of <c>&lt;E&gt;/results/&lt;arm&gt;/timing.jsonl</c> — so the helper copies it to a scratch folder with exactly that
/// one line rewritten (a reshaped script fails the helper by name instead of being read silently wrong), links the arm's <c>timing.jsonl</c> where the script looks for it, and runs <c>node</c> by its resolved path
/// (D17 d) under <c>HOME = &lt;scratch&gt;</c>, so an attempt with no result finds no session transcript and reads as <c>complete: false</c>. The pinned file is never edited; the pin's hash is checked.
/// <para>
/// What this does NOT prove: that a line this build writes is read by any reader other than the one the helper runs — the four readers are E6's — or that the script's arithmetic is right (it is read, not judged).
/// </para></summary>
internal static class PinnedConsumer
{
    private const string Sha256 = "63d668b0399dbbef22da7b9ba2105a144118715ec2abea8793f4067786146add";
    private const string RootLine = "const E = ";

    private static readonly Lazy<string> Node = new(() => ExecutableResolution
        .Resolve("node", (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator), [], RootFacts.Host, OperatingSystem.IsWindows())
        is ExecutableAnswer.Found found ? found.Real : string.Empty);

    private static string Pinned => Path.Combine(Cli.Repository.Root, "research", "local-model-coding", "harness", "metrics.mjs");

    /// <summary>The script over a made arm of <paramref name="root"/> (E2.S8; E6 reuses it for every scheme): the arm's <c>out/timing.jsonl</c>, under the name the script lists the scheme by, in a scratch folder of the root's own.</summary>
    public static Task<MetricsRun> MetricsAsync(ExperimentRoot root, string arm, CancellationToken cancellationToken) =>
        MetricsAsync(root.Scratch("metrics-" + arm), root.PathsOf(arm).Timing, Listed(arm), cancellationToken);

    /// <summary>The name the pinned script lists an arm by: the script's fixed list holds <c>C</c>, <c>CR1</c> and <c>CH1</c> — not <c>C1</c> or <c>CHH1</c>. An arm it does not list is a test error.</summary>
    public static string Listed(string arm) =>
        arm switch
        {
            "C1" => "C",
            "CR1" => "CR1",
            "CHH1" => "CH1",
            _ => throw new ArgumentException($"the pinned metrics.mjs does not list an arm named {arm}", nameof(arm)),
        };

    /// <summary>Runs the pinned script over <paramref name="timingFile"/>, which the script finds as the timing of the arm it lists as <paramref name="listed"/> (<c>C</c> for the C scheme).</summary>
    public static async Task<MetricsRun> MetricsAsync(string scratch, string timingFile, string listed, CancellationToken cancellationToken)
    {
        Assert.False(Node.Value.Length == 0, "a node must resolve on the test process's PATH to run the pinned consumer");
        var text = File.ReadAllText(Pinned);
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Pinned))).Should().Be(Sha256, "the consumer is pinned: a different metrics.mjs is a different test");
        var lines = text.Split('\n');
        lines.Count(line => line.StartsWith(RootLine, StringComparison.Ordinal)).Should().Be(1, "the helper rewrites exactly one root line of the pinned metrics.mjs — a reshaped script must be seen, not read silently wrong");

        var results = Directory.CreateDirectory(Path.Combine(scratch, "results", listed)).FullName;
        File.CreateSymbolicLink(Path.Combine(results, "timing.jsonl"), timingFile);
        var copy = Path.Combine(scratch, "metrics.mjs");
        File.WriteAllText(copy, string.Join('\n', lines.Select(line => line.StartsWith(RootLine, StringComparison.Ordinal) ? $"{RootLine}'{scratch}';" : line)));

        var environment = new Dictionary<string, string> { ["HOME"] = scratch, ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty };
        var attempt = await ProcessRunner.RunAsync(Node.Value, [copy, "--json"], scratch, TimeSpan.FromSeconds(60), string.Empty, environment, cancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return new MetricsRun(done.ExitCode, done.StandardError, done.ExitCode == 0 ? JsonNode.Parse(done.StandardOutput)!["report"]![listed] : null);
    }
}
