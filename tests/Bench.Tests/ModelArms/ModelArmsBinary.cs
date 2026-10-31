using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The console's own binary, started as a process from a test: the one place the two-process tests (the gate lock, E2.S6) get a child that is not a double. It is the apphost the scenario tests already run
/// (<see cref="ExperimentRoot.ConsoleBinary"/>, copied beside the test assembly by the project reference) — an absolute path, started through the shared launcher, so no test launches <c>dotnet</c> by name — in the fixture's
/// environment plus the runtime's location variables, which an apphost needs and a replaced environment would hide.</summary>
internal static class ModelArmsBinary
{
    public static readonly TimeSpan Short = TimeSpan.FromSeconds(60);

    private static IReadOnlyDictionary<string, string> EnvironmentOf(ExperimentRoot root)
    {
        var environment = root.Environment.ToDictionary(variable => variable.Key, variable => variable.Value);
        foreach (var name in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" })
        {
            if (System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        return environment;
    }

    /// <summary>The binary run to its end with <paramref name="arguments"/> and <c>--experiment &lt;root&gt;</c>; the wall is a minute, past which the whole tree is killed and the test fails.</summary>
    public static async Task<(int Code, string Output, string Error)> RunAsync(ExperimentRoot root, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync(
            ExperimentRoot.ConsoleBinary, [.. arguments, "--experiment", root.Root], root.Temp, Short, string.Empty, EnvironmentOf(root), TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return (done.ExitCode, done.StandardOutput, done.StandardError);
    }

    /// <summary>The same with <paramref name="input"/> on the child's stdin (so the child's descriptor 0 is a PIPE, not the test host's own — what a start that inherited stdin would show a detached runner).</summary>
    public static async Task<(int Code, string Output, string Error)> RunWithInputAsync(ExperimentRoot root, string input, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync(
            ExperimentRoot.ConsoleBinary, [.. arguments, "--experiment", root.Root], root.Temp, Short, input, EnvironmentOf(root), TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return (done.ExitCode, done.StandardOutput, done.StandardError);
    }

    /// <summary>The binary started as a task, its stdout lines kept as they arrive with the instant each came. <see cref="IAsyncDisposable.DisposeAsync"/> kills the child's tree if it still lives and waits for it, so no test
    /// leaves a process behind.</summary>
    public static Child Start(ExperimentRoot root, params string[] arguments) =>
        new(ExperimentRoot.ConsoleBinary, [.. arguments, "--experiment", root.Root], root.Temp, EnvironmentOf(root));

    /// <summary>The test-only gate holder (<c>tests/GateHolder</c>), found in its own output folder as <c>FakeCli.Executable</c> finds the fake: the configuration and framework are read off the test assembly's folder.</summary>
    public static string HolderExecutable
    {
        get
        {
            var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var tests = testBin.Parent!.Parent!.Parent!.Parent!.FullName;
            return Path.Combine(tests, "GateHolder", "bin", testBin.Parent.Name, testBin.Name, OperatingSystem.IsWindows() ? "gate-holder.exe" : "gate-holder");
        }
    }

    /// <summary>The test-only killed lock (<c>tests/LockCrasher</c>), found in its own output folder as the holder is.</summary>
    public static string CrasherExecutable
    {
        get
        {
            var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var tests = testBin.Parent!.Parent!.Parent!.Parent!.FullName;
            return Path.Combine(tests, "LockCrasher", "bin", testBin.Parent.Name, testBin.Name, OperatingSystem.IsWindows() ? "lock-crasher.exe" : "lock-crasher");
        }
    }

    /// <summary>The killed lock run to its end (it ends by its own SIGKILL): <c>lock-crasher &lt;root&gt; &lt;run&gt;</c>. The exit code is the signal's (137 on Linux) or 9 when nothing was proved.</summary>
    public static async Task<(int Code, string Output, string Error)> RunCrasherAsync(ExperimentRoot root, string run)
    {
        var attempt = await ProcessRunner.RunAsync(CrasherExecutable, [root.Root, run], root.Temp, Short, string.Empty, EnvironmentOf(root), TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return (done.ExitCode, done.StandardOutput, done.StandardError);
    }

    /// <summary>The gate holder started as a task: it takes <paramref name="lockPath"/>, starts <paramref name="childExecutable"/> with <paramref name="childArguments"/> and prints <c>held &lt;pid&gt;</c> and <c>child &lt;pid&gt;</c>.</summary>
    public static Child StartHolder(ExperimentRoot root, string lockPath, string childExecutable, params string[] childArguments) =>
        new(HolderExecutable, [lockPath, childExecutable, .. childArguments], root.Temp, EnvironmentOf(root));

    internal sealed class Child : IAsyncDisposable
    {
        private readonly object _gate = new();
        private readonly List<(DateTimeOffset At, string Line)> _lines = [];
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        public Child(string executable, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment)
        {
            Attempt = ProcessRunner.RunAsync(executable, arguments, workingDirectory, Short, string.Empty, environment, Record, stderrLine: null, _stop.Token);
        }

        public Task<ProcessAttempt> Attempt { get; }

        /// <summary>Waits for a stdout line containing <paramref name="text"/> and returns the instant it arrived; the wait ends when the child does, or after <paramref name="within"/>.</summary>
        public async Task<DateTimeOffset> LineAsync(string text, TimeSpan within)
        {
            var limit = TimeProvider.System.GetUtcNow() + within;
            while (TimeProvider.System.GetUtcNow() < limit)
            {
                lock (_gate)
                {
                    if (_lines.FirstOrDefault(line => line.Line.Contains(text, StringComparison.Ordinal)) is { Line: not null } found)
                    {
                        return found.At;
                    }
                }

                if (Attempt.IsCompleted)
                {
                    break;
                }

                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            throw new Xunit.Sdk.XunitException($"the child printed no line with '{text}' (it printed: {Printed()}; {(Attempt.IsCompleted ? "it has ended" : "it was still running")})");
        }

        public string Printed()
        {
            lock (_gate)
            {
                return string.Join(" | ", _lines.Select(line => line.Line));
            }
        }

        /// <summary>The child's end, waited for at most <paramref name="within"/>: its exit code.</summary>
        public async Task<int> ExitAsync(TimeSpan within)
        {
            var finished = await Task.WhenAny(Attempt, Task.Delay(within, TestContext.Current.CancellationToken));
            finished.Should().BeSameAs(Attempt, $"the child ends within {within.TotalSeconds:0} s (it printed: {Printed()})");
            return (await Attempt).Should().BeOfType<ProcessAttempt.Completed>().Subject.Result.ExitCode;
        }

        /// <summary>Ends the child's whole tree (the shared launcher kills it on cancellation) and waits for it; a child that already ended is left alone.</summary>
        public async Task KillAsync()
        {
            await _stop.CancelAsync();
            try
            {
                await Attempt;
            }
            catch (OperationCanceledException)
            {
                // the kill was asked for: the launcher reports the caller's cancellation, and the tree is gone when it does
            }
        }

        public async ValueTask DisposeAsync()
        {
            await KillAsync();
            _stop.Dispose();
        }

        private void Record(string line)
        {
            lock (_gate)
            {
                _lines.Add((TimeProvider.System.GetUtcNow(), line));
            }
        }
    }
}
