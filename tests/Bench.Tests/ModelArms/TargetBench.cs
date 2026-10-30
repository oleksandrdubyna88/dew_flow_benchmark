using System.Text.Json;
using Bench.Cli;
using Bench.Domain;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using ModelArmsApp = ModelArms;
using PrepBaseSeams = ModelArms.Verbs.PrepBaseSeams;
using PrepBaseVerb = ModelArms.Verbs.PrepBaseVerb;

namespace Bench.Tests.ModelArms;

/// <summary>One call a shim or the wrapper recorded: which tool, the <c>$0</c> it was started as (the executable's path as launched), its arguments, the folder it started in and the NAMES (never the values) of its environment.</summary>
internal sealed record RecordedCall(string Tool, string Executable, IReadOnlyList<string> Arguments, string Cwd, IReadOnlyList<string> EnvironmentNames);

/// <summary>The scratch target of the E3 verbs (E3 §3): a rules repository holding <c>tools/low-priority.mjs</c> — a node script that records its own argv, folder and environment names and then starts its argument — a target repository whose <c>.agents/conventions</c> is that repository as a
/// submodule, with the plan and an <c>src_vs_code</c> folder at <c>base</c>, and three shims in the experiment's only PATH entry: <c>dotnet</c> and <c>npm</c> (shell scripts that record <c>$0</c>, argv, cwd and the environment's names, then print the canned MTP or node-runner lines and exit with the canned code — the suite shim writes its
/// CTRF where <c>--results-directory</c> says), and <c>git</c> (records, then runs the host's real git); <c>node</c> is a link to the host's. The recording goes to ONE file, <see cref="Calls"/>. Everything is under the experiment root's own <see cref="TempFolder"/> and removed with it.
/// <para>
/// What this does NOT prove: that a real <c>dotnet</c> or <c>npm</c> behaves as the shims do (their words are canned; the live check is the attended pilot, M2), or anything on Windows (POSIX shims).
/// </para></summary>
internal sealed class TargetBench : IDisposable
{
    private const string ProbeBuilt = "Build succeeded.";

    /// <summary>The MTP lines a failing main suite prints: one <c>failed</c> record, the indented summary of twelve tests.</summary>
    public const string SuiteOutput = "failed Tgt.Tests.ATests.Fails (3ms)\nTest run summary: Failed! - Tgt.Tests.dll (net10.0|x64)\n  total: 12\n  failed: 1\n  succeeded: 10\n  skipped: 1\n  duration: 3s 120ms\n";

    /// <summary>The node runner's lines for two tests, one failing.</summary>
    public const string ExtensionOutput = "✔ a (1ms)\n✖ b (2ms)\nℹ tests 2\nℹ pass 1\nℹ fail 1\n";

    private readonly string _canned;

    private TargetBench(ExperimentRoot root)
    {
        Root = root;
        _canned = root.Scratch("canned");
        Calls = Path.Combine(root.Temp, "calls.log");
    }

    public ExperimentRoot Root { get; }

    /// <summary>The operator's checkout of the target (the experiment's <c>target.source</c>).</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>The rules repository, and the submodule the target holds it as.</summary>
    public string Rules { get; private set; } = string.Empty;

    /// <summary><c>target.base</c>: the commit that holds the plan, the submodule and <c>src_vs_code</c>.</summary>
    public string BaseSha { get; private set; } = string.Empty;

    /// <summary>The plan branch's head: <see cref="BaseSha"/>, or one commit ahead of it.</summary>
    public string HeadSha { get; private set; } = string.Empty;

    /// <summary>Where the shims and the wrapper append what they were asked.</summary>
    public string Calls { get; }

    public string BaseFolder => Path.Combine(Root.Root, "base");

    public string BaselineFile => Path.Combine(Root.Root, "baseline.json");

    public Experiment Experiment => Root.ExperimentOf();

    /// <summary>The target with its rules submodule at <c>base</c>; with <paramref name="planBranchAhead"/> its plan branch holds one more commit than <c>base</c>; without <paramref name="extensionFolder"/> the target has no <c>src_vs_code</c> (the extension's tools have nowhere to start).</summary>
    public static async Task<TargetBench> CreateAsync(bool planBranchAhead = false, bool extensionFolder = true)
    {
        var root = await new ExperimentRoot().WithTargetAsync();
        var bench = new TargetBench(root);
        try
        {
            await bench.MakeRepositoriesAsync(planBranchAhead, extensionFolder);
            bench.InstallShims();
            bench.Say("build.out", ProbeBuilt + "\n");
            bench.Say("suite.out", SuiteOutput);
            bench.Say("suite.rc", "2\n");
            bench.Say("ext.out", ExtensionOutput);
            bench.Say("ext.rc", "1\n");
            return bench;
        }
        catch
        {
            bench.Dispose();
            throw;
        }
    }

    /// <summary>Sets what a shim prints or exits with: <c>build.out</c>, <c>build.rc</c>, <c>suite.out</c>, <c>suite.rc</c>, <c>suite.sleep</c> (seconds, after the output), <c>npm.out</c>, <c>npm.rc</c>, <c>ext.out</c>, <c>ext.rc</c>.</summary>
    public void Say(string canned, string text) => File.WriteAllText(Path.Combine(_canned, canned), text);

    /// <summary>Every call recorded so far, in order.</summary>
    public IReadOnlyList<RecordedCall> Recorded()
    {
        if (!File.Exists(Calls))
        {
            return [];
        }

        var calls = new List<RecordedCall>();
        List<string>? block = null;
        foreach (var line in File.ReadAllLines(Calls))
        {
            if (line.StartsWith("CALL ", StringComparison.Ordinal))
            {
                block = [line];
            }
            else if (line == "END" && block is not null)
            {
                calls.Add(Parsed(block));
                block = null;
            }
            else
            {
                block?.Add(line);
            }
        }

        return calls;
    }

    /// <summary>The calls the HARNESS's own git made: every one is started <c>-c core.fsmonitor=false</c> (validate's questions are not).</summary>
    public IReadOnlyList<RecordedCall> HarnessGit() =>
        [.. Recorded().Where(call => call.Tool == "git" && call.Arguments.Take(2).SequenceEqual(["-c", "core.fsmonitor=false"]))];

    /// <summary>The tool paths <c>validate</c>'s checks printed, <c>name → real path</c>.</summary>
    public static IReadOnlyDictionary<string, string> Printed(string output) =>
        output.Split('\n').Select(line => line.Split(" → ")).Where(parts => parts.Length == 2 && parts[0].StartsWith("  ", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);

    /// <summary><c>prep-base</c> through the console's entry point, with the production clock and walls.</summary>
    public async Task<(int Code, string Output, string Error)> PrepBaseAsync()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["prep-base", "--experiment", Root.Root], output, error, Root.Environment, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary><c>prep-base</c> through the gate with the clock and wall handed in.</summary>
    public async Task<(int Code, string Output, string Error)> PrepBaseAsync(PrepBaseSeams seams)
    {
        var line = ModelArmsCommandLine.Split(["prep-base", "--experiment", Root.Root]).Ok();
        var output = new StringWriter();
        var error = new StringWriter();
        var input = new ModelArmsApp.VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, seen => PrepBaseVerb.RunAsync(seen, ModelArmsApp.GateSeams.Real, seams), ModelArmsApp.GateSeams.Real);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>The host's git in <paramref name="folder"/>, by its real path; the answer is stdout.</summary>
    public static async Task<string> GitAsync(string folder, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, folder, TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject;
        done.Result.ExitCode.Should().Be(0, $"git {arguments[0]} in {folder}: {done.Result.Output}");
        return done.Result.StandardOutput;
    }

    public void Dispose() => Root.Dispose();

    private static RecordedCall Parsed(List<string> block)
    {
        string Value(string key) => block.FirstOrDefault(line => line.StartsWith(key + " ", StringComparison.Ordinal))?[(key.Length + 1)..] ?? string.Empty;
        IEnumerable<string> Values(string key) => block.Where(line => line.StartsWith(key + " ", StringComparison.Ordinal)).Select(line => line[(key.Length + 1)..]);

        return new RecordedCall(Value("CALL"), Value("ARG0"), [.. Values("ARG")], Value("CWD"), [.. Values("ENV")]);
    }

    private async Task MakeRepositoriesAsync(bool planBranchAhead, bool extensionFolder)
    {
        var experiment = Root.ExperimentOf().Target;
        var parent = Root.Scratch("tb");
        Rules = Path.Combine(parent, "rules");
        Source = Path.Combine(parent, "target");
        Directory.CreateDirectory(Path.Combine(Rules, "tools"));
        await File.WriteAllTextAsync(Path.Combine(Rules, "tools", "low-priority.mjs"), Wrapper(), TestContext.Current.CancellationToken);
        await InitAsync(Rules, "main");
        await CommitAsync(Rules, "the rules");

        Directory.CreateDirectory(Path.Combine(Source, "todo"));
        await File.WriteAllTextAsync(Path.Combine(Source, experiment.Plan), "a plan\n", TestContext.Current.CancellationToken);
        if (extensionFolder)
        {
            Directory.CreateDirectory(Path.Combine(Source, experiment.Extension.Directory));
            await File.WriteAllTextAsync(Path.Combine(Source, experiment.Extension.Directory, "package.json"), "{}\n", TestContext.Current.CancellationToken);
        }

        await InitAsync(Source, experiment.PlanBranch);
        await GitAsync(Source, "-c", "protocol.file.allow=always", "submodule", "add", "--quiet", Rules, experiment.RulesSubmodule);
        await CommitAsync(Source, "the plan");
        BaseSha = (await GitAsync(Source, "rev-parse", "HEAD")).Trim();
        if (planBranchAhead)
        {
            await File.WriteAllTextAsync(Path.Combine(Source, "ahead.txt"), "after the base\n", TestContext.Current.CancellationToken);
            await CommitAsync(Source, "one commit after the base");
        }

        HeadSha = (await GitAsync(Source, "rev-parse", "HEAD")).Trim();
        Root.Edit(top =>
        {
            top["target"]!["source"] = Source;
            top["target"]!["base"] = BaseSha;
            top["target"]!["suite"]!["namePrefix"] = "Tgt.Tests.";
        });
    }

    private static async Task InitAsync(string folder, string branch)
    {
        Directory.CreateDirectory(folder);
        await GitAsync(folder, "init", "--quiet", "--initial-branch", branch);
    }

    private static async Task CommitAsync(string folder, string message)
    {
        await GitAsync(folder, "add", "-A");
        await GitAsync(folder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
    }

    /// <summary>The wrapper the target's own <c>lowPriority</c> names: it records its argv, folder and environment names to the calls file, then starts its first argument with the rest and exits with its code.</summary>
    private string Wrapper() =>
        $$"""
        import { spawnSync } from 'node:child_process';
        import { appendFileSync } from 'node:fs';
        const [, script, tool, ...rest] = process.argv;
        const lines = ['CALL wrapper', 'ARG0 ' + script, 'ARG ' + tool, ...rest.map((a) => 'ARG ' + a), 'CWD ' + process.cwd(), ...Object.keys(process.env).map((k) => 'ENV ' + k), 'END', ''];
        appendFileSync({{JsonSerializer.Serialize(Calls)}}, lines.join('\n'));
        const run = spawnSync(tool, rest, { stdio: 'inherit' });
        process.exit(run.status ?? 1);
        """;

    private void InstallShims()
    {
        var bin = Path.Combine(Root.Temp, "bin");
        Directory.CreateDirectory(Path.GetDirectoryName(Calls)!);
        File.Delete(Path.Combine(bin, "git"));
        File.Delete(Path.Combine(bin, "node"));
        File.CreateSymbolicLink(Path.Combine(bin, "node"), PinnedConsumer.NodePath);
        Root.UsePath(string.Join(Path.PathSeparator, bin, "/usr/bin", "/bin"));
        Root.ShimScript(Path.Combine(bin, "git"), Prelude("git") + $"exec '{ExperimentRoot.RealGit}' \"$@\"\n");
        Root.ShimScript(
            Path.Combine(bin, "dotnet"),
            Prelude("dotnet") + """
            case "$1" in
              build) show build.out; code build.rc; exit "$c" ;;
              format) exit 0 ;;
              *)
                res=''; name=''; prev=''
                for a in "$@"; do
                  case "$prev" in --results-directory) res="$a";; --report-xunit-ctrf-filename) name="$a";; esac
                  prev="$a"
                done
                if [ -n "$res" ] && [ -n "$name" ]; then printf '%s' '{"results":{"tests":[]}}' > "$res/$name"; fi
                show suite.out
                if [ -f "$CANNED/suite.sleep" ]; then read -r s < "$CANNED/suite.sleep"; /bin/sleep "$s"; fi
                code suite.rc; exit "$c" ;;
            esac

            """);
        Root.ShimScript(
            Path.Combine(bin, "npm"),
            Prelude("npm") + """
            case "$1" in
              ci) show npm.out; code npm.rc; exit "$c" ;;
              test) show ext.out; code ext.rc; exit "$c" ;;
              *) exit 0 ;;
            esac

            """);
    }

    /// <summary>The first lines of every shim: where the calls go, where the canned words are, and the three helpers. Only shell built-ins and absolute paths: the shims do not depend on what the PATH holds after their own folder (which is the system's, so git's own scripts find their coreutils).</summary>
    private string Prelude(string tool) =>
        $$"""
        CALLS='{{Calls}}'
        CANNED='{{_canned}}'
        show() { if [ -f "$CANNED/$1" ]; then while IFS= read -r l || [ -n "$l" ]; do printf '%s\n' "$l"; done < "$CANNED/$1"; fi; }
        code() { c=0; if [ -f "$CANNED/$1" ]; then read -r c < "$CANNED/$1"; fi; }
        {
          printf 'CALL {{tool}}\n'
          printf 'ARG0 %s\n' "$0"
          for a in "$@"; do printf 'ARG %s\n' "$a"; done
          printf 'CWD %s\n' "$(pwd)"
          /usr/bin/env | while IFS= read -r l; do case "$l" in [A-Za-z_]*=*) printf 'ENV %s\n' "${l%%=*}";; esac; done
          printf 'END\n'
        } >> "$CALLS"

        """;
}
