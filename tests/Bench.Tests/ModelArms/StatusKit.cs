using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Trace;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one run of <c>status</c> left on the two writers.</summary>
internal sealed record StatusRun(int Code, string Output, string Error)
{
    public IReadOnlyList<string> Lines => Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>A real experiment root (the worked example, so its schemes and runs are the real ones) holding the arms a test names, by hand: the map, each arm's folder, and whatever a test <see cref="Put"/>s — never a made arm's clone, because <c>status</c> reads files and
/// launches nothing. A token is a counter's byte, so two arms never share one and a test can name it.</summary>
internal sealed class StatusKit : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);

    private byte _token = 0x10;

    public StatusKit(params string[] arms)
    {
        var map = ArmMap.Extend(ArmMap.Empty, arms, () => [0, 0, 0, 0, 0, 0, 0, ++_token]);
        Root.Write("arms.map.json", map.ToJson());
        foreach (var arm in arms)
        {
            Directory.CreateDirectory(Paths(arm).Folder);
        }
    }

    public ExperimentRoot Root { get; } = new();

    public ArmPaths Paths(string arm) => Root.PathsOf(arm);

    public RootPaths Layout => new(Root.Root);

    /// <summary>Every token of the map: what no output line may hold.</summary>
    public IReadOnlyList<string> Tokens => [.. ArmMap.Read(File.ReadAllText(Layout.ArmMapFile)).Ok().Entries.Select(entry => entry.Value)];

    /// <summary>A file with the given text at the full path, its folders made.</summary>
    public string Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public string Started(string arm, string step, int attempt, string deadline = "23:12 UTC (00:12 +01:00)") =>
        TimingLines.Write(new TimingLine.Start(arm, step, attempt, "fresh", "s", "m", At, 600, deadline, "abc1234", Paths(arm).Stream(step, attempt)));

    public string Ended(string arm, string step, int attempt, string state, bool limited = false) =>
        TimingLines.Write(new TimingLine.End(arm, step, attempt, At, CapturedCount.Number(60), CapturedCount.Number(0), !limited, limited, "abc1234", state, false));

    public string Stopped(string arm, string step, int exit, string why) => TimingLines.Write(new TimingLine.Stopped(arm, step, exit, why, At));

    public void Timing(string arm, params string[] lines) => Put(Paths(arm).Timing, string.Join('\n', lines) + "\n");

    /// <summary>A <c>.done</c> marker the module's own writer spells.</summary>
    public void Done(string arm, string step, string state) =>
        Put(Paths(arm).Done(step), DoneMarkers.Write(new DoneMarker(1, CapturedCount.Number(0), Paths(arm).Stream(step, 1), state, false)));

    public void Stream(string arm, string step, int attempt) => Put(Paths(arm).Stream(step, attempt), "{}\n");

    public Task<StatusRun> RunAsync(StatusReads? reads = null, CancellationToken? stopping = null) => RunAsync(Root, reads, stopping);

    public static async Task<StatusRun> RunAsync(ExperimentRoot root, StatusReads? reads = null, CancellationToken? stopping = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var input = new ModelArmsApp.VerbInput("status", [], CommandLine.Parse(["--experiment", root.Root]), output, error, root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), stopping ?? TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => StatusVerb.RunAsync(admitted, reads ?? StatusReads.Real), ModelArmsApp.GateSeams.Real);
        return new StatusRun(code, output.ToString(), error.ToString());
    }

    /// <summary>The root's whole tree as text — each entry's relative path, kind, mode and content — to compare before and after a verb that must change nothing.</summary>
    public string Tree()
    {
        var entries = Directory.EnumerateFileSystemEntries(Root.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal);
        return string.Join('\n', entries.Select(entry => Describe(entry)));
    }

    private string Describe(string entry)
    {
        var info = new FileInfo(entry);
        var relative = Path.GetRelativePath(Root.Root, entry);
        return Directory.Exists(entry) ? $"{relative}/ {info.UnixFileMode}" : $"{relative} {info.UnixFileMode} {File.ReadAllText(entry)}";
    }

    public void Dispose() => Root.Dispose();

    /// <summary>The names every file of the ROOT'S OWN would be asked by, derived from the spelling members: what a verb that opens by name may ask, for the arms and runs the experiment has and up to <paramref name="attempts"/> streams per step.</summary>
    public IReadOnlySet<string> NamesAskedFor(IReadOnlyList<string> madeArms, int attempts)
    {
        var experiment = Root.ExperimentOf();
        var layout = Layout;
        var names = new HashSet<string>(StringComparer.Ordinal) { layout.ArmMapFile, layout.LockedModes, layout.LockedModesApplied };
        foreach (var run in experiment.Runs)
        {
            names.UnionWith(Enum.GetValues<RunStage>().Select(stage => layout.RunMarker(run.Name, stage)));
        }

        var allArms = madeArms.Concat(experiment.Runs.SelectMany(run => run.Rounds.SelectMany(round => round))).Distinct();
        foreach (var arm in allArms)
        {
            names.UnionWith([layout.Made(arm), layout.ObjectiveMarker(arm), layout.PidFile(arm)]);
        }

        foreach (var arm in madeArms)
        {
            var paths = Paths(arm);
            var scheme = experiment.Schemes.Single(each => each.Name == ArmNames.SchemeOf(arm).Ok());
            names.Add(paths.Timing);
            foreach (var step in scheme.Entries.OfType<SchemeEntry.Step>())
            {
                names.UnionWith([paths.Done(step.Name), paths.Continue(step.Name), .. Enumerable.Range(1, attempts).Select(n => paths.Stream(step.Name, n))]);
            }
        }

        return names;
    }

    /// <summary>A live process the test owns, held by the fake CLI after its first stdout line (its pid is in <see cref="Pid"/>), killed on dispose.</summary>
    public sealed class HeldProcess : IDisposable
    {
        private readonly Process _process;
        private readonly FakeCli _fake;

        private HeldProcess(Process process, FakeCli fake)
        {
            _process = process;
            _fake = fake;
        }

        public int Pid => _process.Id;

        /// <summary>Starts the fake and waits for it to say it is holding (the <c>held-&lt;pid&gt;</c> file it writes into its home), so the pid is a process that is alive and stays so.</summary>
        public static async Task<HeldProcess> StartAsync()
        {
            var fake = new FakeCli();
            var stream = Path.Combine(fake.Home, "stream.ndjson");
            File.WriteAllText(stream, "{\"n\":1}\n{\"n\":2}\n{\"n\":3}\n");
            fake.Rewrite(new JsonObject { ["stdoutFile"] = stream, ["holdAfterLine"] = 1, ["lineDelayMs"] = 10 });
            var process = Start(fake);
            var held = new HeldProcess(process, fake);
            try
            {
                var give = Stopwatch.StartNew();
                while (!Directory.EnumerateFiles(fake.Home, "held-*").Any())
                {
                    give.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the fake says it is holding within moments");
                    await Task.Delay(50, TestContext.Current.CancellationToken);
                }

                return held;
            }
            catch
            {
                held.Dispose();
                throw;
            }
        }

        /// <summary>A process that has started, run and EXITED: its pid is one nothing holds.</summary>
        public static async Task<int> ExitedPidAsync()
        {
            using var fake = new FakeCli();
            var stream = Path.Combine(fake.Home, "stream.ndjson");
            File.WriteAllText(stream, "{\"n\":1}\n");
            fake.Rewrite(new JsonObject { ["stdoutFile"] = stream });
            using var process = Start(fake);
            var pid = process.Id;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return pid;
        }

        private static Process Start(FakeCli fake)
        {
            var start = new ProcessStartInfo(FakeCli.Executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-p", "--model", fake.Model })
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start)!;
            process.StandardInput.Close();
            process.OutputDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            return process;
        }

        public void Dispose()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
            finally
            {
                _process.Dispose();
                _fake.Dispose();
            }
        }
    }
}
