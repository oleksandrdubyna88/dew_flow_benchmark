using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What one staged run of a reader gave: the run, and the text it produced - its stdout when it prints its result, else the file it wrote (<see cref="OutputFile"/>).</summary>
internal sealed record StagedRun(PinnedReader Reader, ReaderRun Run, string OutputFile, string Output)
{
    public JsonNode Json => JsonNode.Parse(Output)!;
}

/// <summary>How each of the seven pinned readers is given the arms of <see cref="ConsumerArms"/> (E6.S1): in a scratch folder of the root's own, the reader's copy with its rewrites, a link for everything it looks for under the name it looks for it, and a check - BEFORE the reader runs - that every link has a target
/// (a link to an absent file is the reader's <c>existsSync</c> answering <c>false</c>, which reads as "nothing to report"). The environment is the test process's PATH and a HOME of the scratch; only <c>t-survival.mjs</c>, which runs git, gets a minimal <c>.gitconfig</c> and the author and committer variables.
/// <para>
/// What this does NOT give a reader: the Q and H arms' files, the judges' scores, <c>vllm/requests-&lt;arm&gt;.jsonl</c> (the module runs no local model), a per-task review diff (no scheme has one) and a <c>data/part6</c> or <c>data/part7</c> folder (E6.S1's inputs table).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class ConsumerReaders
{
    private static readonly IReadOnlyDictionary<string, Func<ConsumerArms, string, Task<StagedRun>>> Stages = new Dictionary<string, Func<ConsumerArms, string, Task<StagedRun>>>(StringComparer.Ordinal)
    {
        ["metrics.mjs"] = (arms, scratch) => MetricsAsync(arms, scratch),
        ["metrics6.mjs"] = (arms, scratch) => Metrics6Async(arms, scratch),
        ["t-survival.mjs"] = (arms, scratch) => TSurvivalAsync(arms, scratch, mapped: true),
        ["compare.mjs"] = CompareAsync,
        ["make-tables.mjs"] = MakeTablesAsync,
        ["coai-findings.mjs"] = (arms, scratch) => CoaiAsync(arms, scratch, PinnedReaders.Run.Single(reader => reader.Name == "coai-findings.mjs"), "coai-findings.json", []),
        ["coai-cost.mjs"] = (arms, scratch) => CoaiAsync(arms, scratch, PinnedReaders.Run.Single(reader => reader.Name == "coai-cost.mjs"), "coai-cost.json", [Path.Combine(scratch, "coai-cost.json")]),
    };

    /// <summary>The reader over the arms, in a scratch folder of its own.</summary>
    public static Task<StagedRun> RunAsync(ConsumerArms arms, PinnedReader reader) => Stages[reader.Name](arms, Scratch(arms, reader.Name));

    /// <summary><c>t-survival.mjs SR1</c> with the coder diffs linked under <c>task-&lt;id&gt;-haiku.diff</c> (<paramref name="mapped"/>, THE map of the rename) or under the module's own name <c>task-&lt;id&gt;-coder.diff</c> (no map).</summary>
    public static Task<StagedRun> TSurvivalAsync(ConsumerArms arms, bool mapped) => TSurvivalAsync(arms, Scratch(arms, "t-survival.mjs"), mapped);

    /// <summary>The ids of SRS1's tasks, as the module's own reader reads <c>work/batches.json</c>.</summary>
    public static IReadOnlyList<string> TaskIds(ConsumerArms arms)
    {
        File.Exists(arms.PathsOf("SRS1").BatchesJson).Should().BeTrue("SRS1 left its work/batches.json");
        return [.. BatchesFile.Read(File.ReadAllText(arms.PathsOf("SRS1").BatchesJson), arms.PathsOf("SRS1").Work).Ok().Tasks.Select(task => task.Id)];
    }

    private static string Scratch(ConsumerArms arms, string reader) => arms.Root.Scratch("readers", reader + "-" + Guid.NewGuid().ToString("N")[..6]);

    private static IReadOnlyDictionary<string, string> Environment(string home) =>
        new Dictionary<string, string> { ["HOME"] = home, ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty };

    /// <summary>A link <paramref name="link"/> to <paramref name="target"/>, which must exist: made only to something that is there, so the reader's answer "nothing" is never a missing target's.</summary>
    private static void Link(string link, string target)
    {
        (File.Exists(target) || Directory.Exists(target)).Should().BeTrue($"the link {link} must have a target to be read through: {target} is not there");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.CreateSymbolicLink(link, target);
    }

    private static PinnedReader Reader(string name) => PinnedReaders.Run.Single(reader => reader.Name == name);

    private static async Task<StagedRun> MetricsAsync(ConsumerArms arms, string scratch)
    {
        foreach (var (arm, _) in ConsumerArms.Arms)
        {
            Link(Path.Combine(scratch, "results", PinnedConsumer.Listed(arm), "timing.jsonl"), arms.PathsOf(arm).Timing);
        }

        if (arms.Root.TimingOf("CHH1").OfType<TimingLine.Start>().FirstOrDefault(start => start.Step == "2-hA" && start.Attempt == 1) is { } interrupted)
        {
            var transcript = Path.Combine(scratch, ".claude", "projects", "consumers", interrupted.Session + ".jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
            File.WriteAllText(transcript, ModelArmsFixtures.Read("consumers/transcript-interrupted.jsonl").Replace("__TIMESTAMP__", interrupted.StartUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        var reader = PinnedReaders.Metrics;
        var copy = await PinnedConsumer.CopyAsync(reader, scratch, new Dictionary<Rewrite, string> { [Rewrite.RootLine] = scratch });
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, ["--json"], Environment(scratch), scratch, TestContext.Current.CancellationToken);
        return new StagedRun(reader, run, string.Empty, run.Output);
    }

    private static async Task<StagedRun> Metrics6Async(ConsumerArms arms, string scratch)
    {
        Link(Path.Combine(scratch, "r"), Path.Combine(arms.Root.Root, "r"));
        var reader = Reader("metrics6.mjs");
        var copy = await PinnedConsumer.CopyAsync(reader, scratch, new Dictionary<Rewrite, string>());
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, [Path.Combine(arms.Root.Root, "arms.map.json")], Environment(scratch), scratch, TestContext.Current.CancellationToken);
        return Written(reader, run, Path.Combine(scratch, "metrics6.json"));
    }

    private static async Task<StagedRun> TSurvivalAsync(ConsumerArms arms, string scratch, bool mapped)
    {
        const string arm = "SRS1";
        var listed = PinnedConsumer.Listed(arm);
        var paths = arms.PathsOf(arm);
        Link(Path.Combine(scratch, "arm-" + listed), paths.CloneFolder);
        foreach (var id in TaskIds(arms))
        {
            Link(Path.Combine(scratch, "results", listed, mapped ? $"task-{id}-haiku.diff" : $"task-{id}-coder.diff"), paths.CoderDiff(id));
        }

        Link(Path.Combine(scratch, "vllm", "work", listed, "batches.json"), paths.BatchesJson);
        File.WriteAllText(Path.Combine(scratch, ".gitconfig"), "[user]\n\tname = test\n\temail = test@example.invalid\n");
        var environment = new Dictionary<string, string>(Environment(scratch))
        {
            ["COAI_EXP_ROOT"] = scratch,
            ["GIT_AUTHOR_NAME"] = "test",
            ["GIT_AUTHOR_EMAIL"] = "test@example.invalid",
            ["GIT_COMMITTER_NAME"] = "test",
            ["GIT_COMMITTER_EMAIL"] = "test@example.invalid",
        };
        var reader = Reader("t-survival.mjs");
        var copy = await PinnedConsumer.CopyAsync(reader, scratch, new Dictionary<Rewrite, string> { [Rewrite.BaseLiteral] = arms.Base });
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, [listed], environment, scratch, TestContext.Current.CancellationToken);
        return Written(reader, run, Path.Combine(scratch, "results", listed, "t-survival.json"));
    }

    private static async Task<StagedRun> CompareAsync(ConsumerArms arms, string scratch)
    {
        var metrics = await MetricsAsync(arms, Scratch(arms, "metrics.mjs-for-compare"));
        File.WriteAllText(Path.Combine(scratch, "metrics.json"), metrics.Output);
        foreach (var (arm, _) in ConsumerArms.Arms)
        {
            Link(Path.Combine(scratch, "results", PinnedConsumer.Listed(arm), "objective.json"), arms.PathsOf(arm).Objective);
        }

        var reader = Reader("compare.mjs");
        var copy = await PinnedConsumer.CopyAsync(reader, scratch, new Dictionary<Rewrite, string> { [Rewrite.RootLine] = scratch });
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, ["--json"], Environment(scratch), scratch, TestContext.Current.CancellationToken);
        return new StagedRun(reader, run, string.Empty, run.Output);
    }

    private static async Task<StagedRun> MakeTablesAsync(ConsumerArms arms, string scratch)
    {
        var metrics = await MetricsAsync(arms, Scratch(arms, "metrics.mjs-for-make-tables"));
        var survival = await TSurvivalAsync(arms, mapped: true);
        var data = Path.Combine(scratch, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "metrics.json"), metrics.Output);
        foreach (var (arm, _) in ConsumerArms.Arms)
        {
            var listed = PinnedConsumer.Listed(arm);
            var paths = arms.PathsOf(arm);
            Link(Path.Combine(data, "arms", listed, "objective.json"), paths.Objective);
            Link(Path.Combine(data, "arms", listed, listed + "-review.json"), Path.Combine(paths.Notes, "review.json"));
            Link(Path.Combine(data, "arms", listed, "timing.jsonl"), paths.Timing);
        }

        Link(Path.Combine(data, "arms", "SR1", "tasks", "batches.json"), arms.PathsOf("SRS1").BatchesJson);
        Link(Path.Combine(data, "arms", "SR1", "t-survival.json"), survival.OutputFile);
        var reader = Reader("make-tables.mjs");
        var copy = await PinnedConsumer.CopyAsync(reader, Path.Combine(scratch, "harness"), new Dictionary<Rewrite, string>());
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, [], Environment(scratch), scratch, TestContext.Current.CancellationToken);
        return Written(reader, run, Path.Combine(scratch, "tables.md"));
    }

    private static async Task<StagedRun> CoaiAsync(ConsumerArms arms, string scratch, PinnedReader reader, string written, IReadOnlyList<string> arguments)
    {
        File.WriteAllText(Path.Combine(scratch, "exp.env"), $"MAP={Path.Combine(arms.Root.Root, "arms.map.json")}\n");
        Link(Path.Combine(scratch, "r"), Path.Combine(arms.Root.Root, "r"));
        var copy = await PinnedConsumer.CopyAsync(reader, scratch, new Dictionary<Rewrite, string> { [Rewrite.RootLine] = scratch });
        var run = await PinnedConsumer.RunAsync(reader, scratch, copy, arguments, Environment(scratch), scratch, TestContext.Current.CancellationToken);
        return Written(reader, run, Path.Combine(scratch, written));
    }

    /// <summary>The run, with the text of the file the reader was to write ("" when it wrote none: the assertion on the output then names the file).</summary>
    private static StagedRun Written(PinnedReader reader, ReaderRun run, string file) =>
        new(reader, run, file, File.Exists(file) ? File.ReadAllText(file) : string.Empty);
}
