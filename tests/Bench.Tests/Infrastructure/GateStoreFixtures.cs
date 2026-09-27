using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Gate;

namespace Bench.Tests.Infrastructure;

/// <summary>The shapes the gate store tests build again and again — written once, because a second copy of a
/// "valid settled session" is a second thing to keep valid.</summary>
internal static class GateStoreFixtures
{
    public static readonly DateTimeOffset Noon = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    public static readonly FileHashKey Key = FileHashKey.Of(Enumerable.Range(0, 32).Select(i => (byte)(i * 11 + 5)).ToArray()).Ok();

    public static ProductPin Pin(char letter = 'a') =>
        ProductPin.Hashed(new string(letter, 64), "0.0.0+abc1234", "abc1234", CapturedCount.Number(0), "src_mcp").Ok();

    public static GateRun Run(DataDirMode mode = DataDirMode.Isolated) =>
        GateRun.Planned(Guid.CreateVersion7(), GateKind.Feature, "sample#0123456789ab", mode, Noon);

    /// <summary>A planned run of <paramref name="count"/> cells over one task and one reviewer.</summary>
    public static (GateRun Run, IReadOnlyList<GateCell> Cells) Planned(int count = 1, DataDirMode mode = DataDirMode.Isolated)
    {
        var run = Run(mode);
        var cells = Enumerable.Range(0, count)
            .Select(i => GateCell.Pending(Guid.CreateVersion7(), run.Id, new GateMatrixCell(
                GateTaskId.Parse("cs2").Ok(), GateReviewerId.Parse("grok-medium").Ok(), Repeat: i + 1, Slot: 0, Position: i)))
            .ToList();

        return (run, cells);
    }

    /// <summary>A valid, completed session: two turns, one finding list of two, a metered cost.</summary>
    public static GateSettlement.Completed Completed(int findings = 2)
    {
        var turn = new LedgerTurn(12.34, CapturedCount.Number(1000), CapturedCount.Number(200), CapturedCount.Number(64),
            CapturedCount.Number(512), CapturedUsd.Amount(0.0123m), "ok");
        var call = new HttpCallFacts(200, "stop", CapturedCount.Number(64), CapturedCount.Number(512), 12.0, 900);
        var facts = GateRunFacts.From(
            new GateReply.Answered(GateVerdictWord.Revise, CapturedCount.Number(findings)), [turn, turn], [call, call], 30.5, served: 1, refused: 0);

        return new GateSettlement.Completed(
            facts,
            [.. Enumerable.Range(0, findings).Select(i => GateFinding.Of(i, FindingSeverity.Major, FindingCategory.Architecture, i == 0, 10 + i,
                $"finding {i} text that quotes private code", $"src/File{i}.cs", Key).Ok())],
            SettingsHash: new string('5', 64),
            PromptHash: new string('6', 64));
    }

    /// <summary>A directory under the system temp folder — outside any git checkout — removed afterwards.</summary>
    public static TempRoot NewRoot() => new();

    public static FileSystemGateArtifactStore Store(TempRoot root, ArtifactProbe? probe = null, TimeProvider? clock = null) =>
        FileSystemGateArtifactStore.Open(root.Path, clock ?? TimeProvider.System, probe ?? ArtifactProbe.None).Ok();

    public static WorkerIdentity Here(string label = "lane") => WorkerIdentity.Here(label);
}

internal sealed class TempRoot : IDisposable
{
    public TempRoot()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bench-gate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Sibling(string name) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, $"{System.IO.Path.GetFileName(Path)}-{name}");

    public void Dispose()
    {
        foreach (var directory in new[] { Path, Sibling("outside") }.Where(Directory.Exists))
        {
            try
            {
                RemoveLinks(directory);
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A test that failed half-way can leave a link Windows will not delete in one pass; the temp folder
                // is the operating system's to clean, and failing a green test over it would hide the real result.
            }
        }
    }

    /// <summary>Links first, each removed as ITSELF — a recursive delete must never walk through one into its target.</summary>
    private static void RemoveLinks(string directory)
    {
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (new DirectoryInfo(child).LinkTarget is not null)
            {
                Directory.Delete(child);
                continue;
            }

            RemoveLinks(child);
        }
    }
}

/// <summary>A probe that dies at one step — the process-crash stand-in the protocol tests use.</summary>
internal sealed class CrashAt(ArtifactStep step, int after = 1) : ArtifactProbe
{
    private int _seen;

    public override void Reached(ArtifactStep reached)
    {
        if (reached == step && ++_seen == after)
        {
            throw new SimulatedCrash(step);
        }
    }
}

internal sealed class SimulatedCrash(ArtifactStep step) : Exception($"simulated crash at {step}");
