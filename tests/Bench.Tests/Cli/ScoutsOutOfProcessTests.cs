using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>§5 S2's ONE out-of-process test: a real <c>bench scouts run</c> PROCESS, started by the test and killed by its pid mid-cell — the fake
/// CLI holds after the whole stream (its <c>result</c> included) has reached the artefact root, writes a marker, and the test kills the bench's
/// process tree the moment it sees it. Then a second real process runs <c>bench scouts resume</c>: the entry step settles the killed attempt
/// from its stream, the cell is measured again, and the ledger identity holds — Σ settled <c>total_cost_usd</c> + Σ unknown charges = spent.
/// <para>
/// The bench is launched with the production services (no test variable reaches the CLI's environment); the fake finds its home under the
/// <c>TEMP</c> the test gives the bench process, which the probes' minimal child environment passes through.
/// </para></summary>
[Collection("postgres")]
public sealed class ScoutsOutOfProcessTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_bench_process_killed_mid_cell_is_resumed_by_another_and_the_ledger_identity_holds()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        var temp = setup.Root.Sibling("process-temp");
        using var fake = new ScoutFake(Path.Combine(temp, "bench-fake-cli", ScoutLaunchModel));
        var stream = setup.StreamOf(setup.Tasks[0], ScoutArmId.A1);
        fake.Answer(ScoutPrompts.ForCell(setup.Tasks[0], ScoutArms.Of(ScoutArmId.A1)).Driver, stream);
        fake.Script(new JsonObject { ["holdAfterLine"] = stream.TrimEnd('\n').Split('\n').Length });

        using var run = Start(setup, temp, setup.RunArgs([ScoutArmId.A1]));
        var held = await HeldAsync(fake, run.Process);
        run.Process.Kill(entireProcessTree: true);
        await run.Process.WaitForExitAsync(Ct);

        var runId = (await setup.NewStore().CellsAsync(await RunOfAsync(setup), Ct)).Single().RunId;
        var killed = (await setup.NewStore().AttemptsAsync(runId, Ct)).Single();
        killed.State.Should().Be(ScoutAttemptState.Launching, $"the bench died mid-cell (the fake {held} held after the result line) — {run.Output}");
        File.ReadAllText(new ScoutArtifacts(setup.ArtifactRoot).PathOf(killed, ScoutArtifactKind.Stream)).Should().Contain("\"type\":\"result\"", "the paid output reached the disk as it arrived");

        fake.Script(new JsonObject());
        using var resume = Start(setup, temp, ["resume", "--run", runId.ToString()]);
        var (code, output) = await FinishedAsync(resume);

        code.Should().Be(ExitCodes.Pass, output);
        output.Should().Contain("1 settled from their stream");
        fake.Calls().Should().NotBeEmpty().And.OnlyContain(c => c.SubagentModelGuard == "off",
            "the scout launch measures a model: the real launch path hands the child SUBAGENT_MODEL_GUARD=off, which the allow-list would not inherit");
        var attempts = (await setup.NewStore().AttemptsAsync(runId, Ct)).OrderBy(a => a.Number).ToList();
        attempts.Should().HaveCount(2).And.OnlyContain(a => a.State == ScoutAttemptState.Settled);
        attempts[0].SettledUsd.Should().Be(ScoutCellStreams.CostOf(stream), "the killed attempt settled once, from its own stream");
        await ScoutsCampaignTests.AssertLedgerIdentityAsync(setup, runId, expectedSettled: 2, expectedUnknown: 0);
        (await setup.NewStore().CellsAsync(runId, Ct)).Single().Should().Match<ScoutCell>(c => c.State == CellState.Settled && c.Outcome == ScoutCellOutcome.Scored);
    }

    /// <summary>The model every scout cell pins — the fake's home is keyed by it when no test variable reaches the child.</summary>
    private const string ScoutLaunchModel = global::Bench.Application.Scouts.ScoutLaunch.MainModel;

    /// <summary>A bench process the test started, and everything it printed.</summary>
    private sealed class BenchProcess(Process process) : IDisposable
    {
        private readonly System.Text.StringBuilder _output = new();

        public Process Process { get; } = process;

        public string Output
        {
            get
            {
                lock (_output)
                {
                    return _output.ToString();
                }
            }
        }

        public void Said(string? line)
        {
            lock (_output)
            {
                _output.AppendLine(line);
            }
        }

        public void Dispose() => Process.Dispose();
    }

    private static BenchProcess Start(ScoutsCliSetup setup, string temp, string[] args)
    {
        Directory.CreateDirectory(temp);
        var start = new ProcessStartInfo(Bench) { WorkingDirectory = temp, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };

        string[] all = ["scouts", .. setup.WithDefaults(args)];

        foreach (var argument in all)
        {
            start.ArgumentList.Add(argument);
        }

        // All three: Windows reads TEMP/TMP, while .NET on Linux and macOS reads TMPDIR — and the fake CLI finds its home under
        // Path.GetTempPath(). With TEMP alone the Linux fake looked under /tmp, found no script, answered at once, and the bench
        // finished before anything could be killed (seen in CI on 2026-10-13).
        start.Environment["TEMP"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["TMPDIR"] = temp;
        start.Environment.Remove("BENCH_DB");

        var bench = new BenchProcess(new Process { StartInfo = start });
        bench.Process.OutputDataReceived += (_, e) => bench.Said(e.Data);
        bench.Process.ErrorDataReceived += (_, e) => bench.Said(e.Data);
        bench.Process.Start();
        bench.Process.BeginOutputReadLine();
        bench.Process.BeginErrorReadLine();
        return bench;
    }

    private static async Task<int> HeldAsync(ScoutFake fake, Process run)
    {
        for (var i = 0; i < 1200 && fake.Held().Count == 0 && !run.HasExited; i++)
        {
            await Task.Delay(500, Ct);
        }

        run.HasExited.Should().BeFalse("the bench must still be running when the fake holds — otherwise nothing was killed mid-cell");
        return fake.Held().Should().ContainSingle().Subject;
    }

    private static async Task<(int Code, string Output)> FinishedAsync(BenchProcess bench)
    {
        using var wall = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        wall.CancelAfter(TimeSpan.FromMinutes(15));
        await bench.Process.WaitForExitAsync(wall.Token);
        bench.Process.WaitForExit();

        return (bench.Process.ExitCode, bench.Output);
    }

    private static async Task<Guid> RunOfAsync(ScoutsCliSetup setup)
    {
        await using var db = PostgresFixture.Context(setup.Connection);
        return db.ScoutRuns.Select(r => r.Id).Single();
    }

    /// <summary>The CLI's own apphost, built beside this test project by the solution build.</summary>
    private static string Bench
    {
        get
        {
            var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var root = testBin.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
            return Path.Combine(root, "hosts", "Cli", "bin", testBin.Parent.Name, testBin.Name, OperatingSystem.IsWindows() ? "bench.exe" : "bench");
        }
    }
}
