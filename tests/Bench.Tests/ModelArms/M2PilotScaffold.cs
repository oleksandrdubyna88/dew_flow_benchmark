using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scaffold of M2, the attended pilot (plan <c>todo/PLAN_model_arms_E3.md</c> §5 E3.S5, M2): the scratch target and the filled experiment the OWNER runs the verbs over by hand, written by this test so the pilot's inputs are under git and not in a shell history. <b>Explicit: the suite never runs it</b> (<c>Explicit = true</c>), because what
/// it makes is meant to outlive the test — the owner removes it after the pilot (<c>rm -rf</c> of the two printed <c>testsToDelete-m2-…</c> folders).
/// <para>
/// It writes, under the temp folder and outside every checkout: <c>testsToDelete-m2-target-&lt;id&gt;</c> — a git repository on a plan branch holding a tiny .NET solution with an xunit v3 test project (a real <c>dotnet build</c> and a real CTRF), an <c>src_vs_code</c> folder whose <c>npm test</c> is <c>node --test</c>, the target's own lowering wrapper <c>tools/low-priority.mjs</c> inside the rules
/// repository it holds as the submodule <c>.agents/conventions</c>, all committed (the plan branch's head is <c>target.base</c>); and, beside the target's <c>.git</c>, the files to copy into the root once <c>init</c> has made it: <c>experiment.json</c> (the worked example with this target's paths, one run holding <c>C1</c>, no coai, no isolation, <c>awake: none</c>) and
/// <c>scenarios.json</c> (two <c>cs</c> scenarios the scratch suite names and one <c>node</c>). <c>testsToDelete-m2-root-&lt;id&gt;</c> is a NAME only: the root is NOT created — <c>init</c> creates it exclusively. Both files are read through the module's own readers before they are printed, so what the owner copies is known to parse.
/// </para>
/// <para>
/// What this does NOT prove: that the scratch solution builds on the owner's host or that the suite and the node runner print what the readers expect — that is the pilot's own first observation (E3 §5 M2); nor does it run any verb.
/// </para></summary>
public sealed class M2PilotScaffold
{
    private const string PlanBranch = "docs/plan-m2-scratch";
    private const string PlanFile = "todo/PLAN_m2_scratch.md";
    private const string Solution = "scratch.slnx";
    private const string SuiteDll = "src_mcp/tests/bin/Debug/net10.0/ScratchTests.dll";

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly string[] CsScenarios = ["A_scratch_scenario_passes", "Another_scratch_scenario_passes"];
    private const string NodeScenario = "a scratch node scenario passes";

    [Fact(Explicit = true)]
    public async Task Writes_the_scratch_target_and_root_for_the_pilot()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the pilot's scratch target is made with POSIX paths and a real git");
        var id = Guid.NewGuid().ToString("N")[..8];
        var target = Path.Combine(Path.GetTempPath(), $"testsToDelete-m2-target-{id}");
        var root = Path.Combine(Path.GetTempPath(), $"testsToDelete-m2-root-{id}");
        try
        {
            var baseSha = await MakeTargetAsync(target);
            var experiment = Experiment(target, root, baseSha);
            var scenarios = Scenarios();
            var files = Path.Combine(target, ".git", "m2-pilot");
            Directory.CreateDirectory(files);
            await File.WriteAllTextAsync(Path.Combine(files, "experiment.json"), experiment, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(files, "scenarios.json"), scenarios, TestContext.Current.CancellationToken);

            Directory.Exists(root).Should().BeFalse("the root is a name only: init creates it, exclusively");
            Say($"M2 scaffold — the target: {target}");
            Say($"M2 scaffold — the root (NOT created; init makes it): {root}");
            Say($"M2 scaffold — target.base: {baseSha}");
            Say($"M2 scaffold — copy into the root after init: {Path.Combine(files, "experiment.json")} and {Path.Combine(files, "scenarios.json")}");
            Say("M2 scaffold — experiment.json:\n" + experiment);
            Say("M2 scaffold — scenarios.json:\n" + scenarios);
        }
        catch
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            throw;
        }
    }

    private static void Say(string text)
    {
        Console.Out.WriteLine(text);
        TestContext.Current.TestOutputHelper?.WriteLine(text);
    }

    /// <summary>The target repository: the rules repository inside it first (its own repository, committed), then the target's files, the rules added as the submodule at the path it already is at, one commit on the plan branch. Returns that commit — <c>target.base</c>.</summary>
    private static async Task<string> MakeTargetAsync(string target)
    {
        var rules = Path.Combine(target, ".agents", "conventions");
        Directory.CreateDirectory(Path.Combine(rules, "tools"));
        await File.WriteAllTextAsync(Path.Combine(rules, "tools", "low-priority.mjs"), Wrapper, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(rules, "package.json"), PackageJson("scratch-rules"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(rules, "package-lock.json"), PackageLock("scratch-rules"), TestContext.Current.CancellationToken);
        await TargetBench.GitAsync(rules, "init", "--quiet", "--initial-branch", "main");
        await CommitAsync(rules, "the rules");

        await Put(target, ".gitignore", "bin/\nobj/\nnode_modules/\n");
        await Put(target, PlanFile, "a scratch plan for the pilot\n");
        await Put(target, Solution, "<Solution>\n  <Project Path=\"src_mcp/tests/ScratchTests.csproj\" />\n</Solution>\n");
        await Put(target, "src_mcp/tests/ScratchTests.csproj", Project);
        await Put(target, "src_mcp/tests/ATests.cs", Tests);
        await Put(target, "src_vs_code/package.json", PackageJson("scratch-extension", test: "node --test --test-reporter=spec src/test/"));
        await Put(target, "src_vs_code/package-lock.json", PackageLock("scratch-extension"));
        await Put(target, "src_vs_code/src/test/scratch.test.js", NodeTests);
        await TargetBench.GitAsync(target, "init", "--quiet", "--initial-branch", PlanBranch);
        await TargetBench.GitAsync(target, "-c", "protocol.file.allow=always", "submodule", "add", "--quiet", rules, ".agents/conventions");
        await CommitAsync(target, "the scratch target");
        return (await TargetBench.GitAsync(target, "rev-parse", "HEAD")).Trim();
    }

    private static async Task Put(string folder, string relative, string text)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
    }

    private static async Task CommitAsync(string folder, string message)
    {
        await TargetBench.GitAsync(folder, "add", "-A");
        await TargetBench.GitAsync(folder, "-c", "user.name=m2", "-c", "user.email=m2@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
    }

    /// <summary>The worked example with this target's values: the example is read, its comments dropped, and the members the pilot changes written; the result is read back through <see cref="ExperimentFile.Read"/> so a file the module would refuse is never printed.</summary>
    private static string Experiment(string target, string root, string baseSha)
    {
        var example = File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));
        var tree = JsonNode.Parse(example, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
        var section = tree["target"]!;
        section["source"] = target;
        section["planBranch"] = PlanBranch;
        section["base"] = baseSha;
        section["plan"] = PlanFile;
        section["branch"] = "feat/m2-scratch";
        section["build"] = new JsonArray("dotnet", "build", Solution, "-c", "Debug", "-m:4", "--disable-build-servers");
        section["suite"]!["command"] = new JsonArray("dotnet", SuiteDll, "--report-xunit-ctrf");
        section["suite"]!["namePrefix"] = "ScratchTests.";
        section["format"]!["solution"] = Solution;
        tree["known"] = "Nothing is known to fail: at the base commit the scratch suite and the extension's tests pass.";
        tree["runs"] = new JsonObject { ["pilot"] = new JsonObject { ["rounds"] = new JsonArray(new JsonArray("C1")) } };
        tree["awake"] = "none";
        var deny = tree["tools"]!["deny"]!.AsArray();
        foreach (var entry in deny.Where(entry => entry!.GetValue<string>().Contains("//example/old-experiment", StringComparison.Ordinal) || entry.GetValue<string>().Contains("//example/vault", StringComparison.Ordinal)).ToList())
        {
            deny.Remove(entry);
        }

        tree.AsObject().Remove("coai");
        tree.AsObject().Remove("isolation");
        var home = System.Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        var text = tree.ToJsonString(Pretty).Replace("//example/home", "/" + home, StringComparison.Ordinal);
        text.Should().NotContain("/example/", "every path of the example is replaced or removed (the deny list's //example/home is the operator's own home)");
        ExperimentFile.Read(text, new ExperimentLocation(root, home, string.Empty, Cli.Repository.Root)).Ok();
        return text;
    }

    private static string Scenarios()
    {
        var text = new JsonArray(
            Scenario("S1", "cs", CsScenarios[0]),
            Scenario("S2", "cs", CsScenarios[1]),
            Scenario("S3", "node", NodeScenario)).ToJsonString(Pretty);
        ScenariosFile.Read(text).Ok();
        return text;
    }

    private static JsonObject Scenario(string id, string kind, string name) => new() { ["id"] = id, ["kind"] = kind, ["name"] = name };

    private static string PackageJson(string name, string test = "node --test") =>
        $$"""{ "name": "{{name}}", "version": "1.0.0", "private": true, "scripts": { "test": "{{test}}" } }""" + "\n";

    private static string PackageLock(string name) =>
        $$"""{ "name": "{{name}}", "version": "1.0.0", "lockfileVersion": 3, "requires": true, "packages": { "": { "name": "{{name}}", "version": "1.0.0" } } }""" + "\n";

    /// <summary>The target's own lowering wrapper in miniature (the real one lowers the process's priority): it starts its argument with the rest and exits with its code.</summary>
    private const string Wrapper = """
        import { spawnSync } from 'node:child_process';
        const [, , tool, ...rest] = process.argv;
        const run = spawnSync(tool, rest, { stdio: 'inherit' });
        process.exit(run.status ?? 1);

        """;

    private const string Project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
            <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="xunit.v3" Version="4.0.2" />
          </ItemGroup>
        </Project>

        """;

    private const string Tests = """
        using Xunit;

        namespace ScratchTests;

        public class ATests
        {
            [Fact]
            public void A_scratch_scenario_passes() => Assert.True(true);

            [Fact]
            public void Another_scratch_scenario_passes() => Assert.Equal(4, 2 + 2);

            [Fact]
            public void A_third_scratch_test_passes() => Assert.NotNull("a");
        }

        """;

    private const string NodeTests = """
        const test = require('node:test');
        const assert = require('node:assert');

        test('a scratch node scenario passes', () => assert.strictEqual(1 + 1, 2));
        test('another scratch node test passes', () => assert.ok(true));

        """;
}
