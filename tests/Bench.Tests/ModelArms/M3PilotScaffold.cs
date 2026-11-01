using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scaffold of M3, the attended pilot of the whole module (plan <c>todo/PLAN_model_arms_E6.md</c> §5 E6.S3): everything the hand-run pilot needs, written by this test so the pilot's inputs are under git and not in a shell history. <b>Explicit: the suite never runs it</b> (<c>Explicit = true</c>), because what it makes is meant to outlive the test — the pilot's last step removes it.
/// <para>
/// <b>Names and the manifest.</b> Every path is <c>/tmp/testsToDelete-m3-&lt;what&gt;-&lt;id&gt;</c>, M2's form — the LEGACY form of <c>common.test-leftovers</c> §1 (no owner segment: this repository has not reached the producer stage of that rollout), so no sweep collects it by age and the pilot's own teardown is the only thing that removes it. The scaffold draws an <c>&lt;id&gt;</c>, creates FIRST and exclusively (<see cref="FileMode.CreateNew"/>; one already there draws a new id) the manifest <c>testsToDelete-m3-manifest-&lt;id&gt;.json</c> — the id, the caller's uid and the EXACT path of everything it creates or
/// names, each written before it exists — and only then creates anything. The teardown removes exactly the manifest's paths, one by one, never a glob.
/// </para>
/// <para>
/// <b>What it makes.</b> <c>target</c> (M2's tiny .NET solution and <c>src_vs_code</c>, the rules repository as the submodule, with the files to copy into the root in <c>.git/m3-pilot/</c>); <c>root</c> (a NAME only: <c>init</c> creates it); <c>home</c> (a scratch HOME holding <c>.claude/projects</c>, <c>file-history</c>, <c>session-env</c>, <c>shell-snapshots</c>, so the isolation's <c>~/.claude/*</c> are the pilot's and the operator's own <c>~/.claude</c> is never touched); <c>old</c> (a folder the isolation closes); <c>bin</c> (<c>claude</c> as a WRAPPER over the fake, links to the host's <c>git</c>, <c>node</c>, <c>npm</c>, <c>dotnet</c>); <c>fake</c> (<c>FAKE_CLI_HOME</c> and its script); <c>d28</c>, <c>deleg</c>, <c>home14</c> and <c>keep</c>
/// (working folders of the later steps). The experiment is the worked example with this target's paths and ONE run <c>pilot</c> whose one round is the four schemes; both files are read through the module's own readers and <c>validate</c> runs over a throwaway root (<see cref="M2PilotScaffold.ValidatesOnAProbeRootAsync"/>) before anything is printed.
/// </para>
/// <para>
/// <b>What the wrapper plays</b> — the model's file writes, which the fake cannot do: the split's fresh call writes <c>work/tasks.json</c> (five tasks, <c>t5</c> owning the plan) and the briefs; a task coder's fresh call writes and commits <c>&lt;id&gt;.txt</c>; the FIRST gate call of the pilot writes <c>notes/gate.json</c> blocked (a marker folder in <c>bin</c>, made by <c>mkdir</c>, says which arm it blocked) and every later gate call writes <c>proceed</c>. <b>What the fake replays</b> is chosen by prompt text, never by call number: the opening of <c>hr-review-fix</c> gets the 2026-10-28 429 line, the opening of <c>hA-trace</c> a stream with no result, anything else (and every resume) the recorded success; the gate model's script has <c>delayMs</c> 10000.
/// </para>
/// <para>
/// What this does NOT prove: that the wrapper's shell text works under the module's launch environment — that is the pilot's own first observation.
/// </para></summary>
public sealed class M3PilotScaffold
{
    private const string Fake = "m3-fake";
    private const string FakeGate = "m3-fake-gate";

    [Fact(Explicit = true)]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    public async Task Writes_the_scratch_target_root_home_and_claude_for_the_pilot()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "the pilot's scratch is made with POSIX paths, a real git, a shell wrapper and /proc/self/status");
        var temp = Path.GetTempPath().TrimEnd('/');
        var (id, manifest) = ClaimManifest(temp);
        var names = new[] { "target", "root", "home", "old", "bin", "fake", "d28", "deleg", "home14", "keep" };
        var paths = names.ToDictionary(name => name, name => Path.Combine(temp, $"testsToDelete-m3-{name}-{id}"), StringComparer.Ordinal);
        var files = Path.Combine(paths["target"], ".git", "m3-pilot");
        var uid = Uid();

        // Every path is in the manifest BEFORE it exists (the manifest itself is already there, claimed exclusively).
        var entries = new JsonArray([.. paths.Select(each => (JsonNode?)new JsonObject { ["name"] = each.Key, ["path"] = each.Value }), (JsonNode?)new JsonObject { ["name"] = "manifest", ["path"] = manifest }]);
        try
        {
            await File.WriteAllTextAsync(manifest, new JsonObject { ["id"] = id, ["uid"] = uid, ["paths"] = entries }.ToJsonString(M2PilotScaffold.Pretty) + "\n", TestContext.Current.CancellationToken);
            var baseSha = await M2PilotScaffold.MakeTargetAsync(paths["target"]);
            Directory.CreateDirectory(files);
            var settings = await M2PilotScaffold.CoaiSettingsAsync(files);
            var experiment = Experiment(paths, baseSha, M2PilotScaffold.CoaiBinary(), settings);
            var scenarios = M2PilotScaffold.Scenarios();
            await File.WriteAllTextAsync(Path.Combine(files, "experiment.json"), experiment, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(files, "scenarios.json"), scenarios, TestContext.Current.CancellationToken);

            await M2PilotScaffold.ValidatesOnAProbeRootAsync(ForTheProbe(experiment), scenarios, id, "m3");
            Directory.Exists(paths["root"]).Should().BeFalse("the root is a name only: init creates it, exclusively");

            foreach (var name in new[] { "home", "old", "bin", "fake", "d28", "deleg", "home14", "keep" })
            {
                Directory.CreateDirectory(paths[name]);
            }

            foreach (var folder in new[] { "projects", "file-history", "session-env", "shell-snapshots" })
            {
                Directory.CreateDirectory(Path.Combine(paths["home"], ".claude", folder));
            }

            await File.WriteAllTextAsync(Path.Combine(paths["old"], "left-over.txt"), "an earlier experiment's file, closed by the isolation\n", TestContext.Current.CancellationToken);
            WriteFakeScript(paths["fake"]);
            WriteBin(paths["bin"], paths["fake"]);

            Say($"M3 scaffold — manifest: {manifest} (uid {uid}, id {id})");
            foreach (var name in names)
            {
                Say($"M3 scaffold — {name}{(name == "root" ? " (NOT created; init makes it)" : string.Empty)}: {paths[name]}");
            }

            Say($"M3 scaffold — target.base: {baseSha}");
            Say($"M3 scaffold — coai.binary: {M2PilotScaffold.CoaiBinary()}; coai.settings: {settings} (no key in either file)");
            Say($"M3 scaffold — copy into the root after init: {Path.Combine(files, "experiment.json")} and {Path.Combine(files, "scenarios.json")}");
            Say("M3 scaffold — the environment of every verb: env -i PATH=" + PathOf(paths["bin"]) + $" HOME={paths["home"]} LANG=C.UTF-8 FAKE_CLI_HOME={paths["fake"]}" + (OperatingSystem.IsLinux() && System.Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is { Length: > 0 } ? " WSL_DISTRO_NAME=… WSL_INTEROP=…" : string.Empty));
            Say("M3 scaffold — experiment.json:\n" + experiment);
        }
        catch (Exception failure)
        {
            // Nothing is half-made and left: what this run created is removed by the manifest's own names, best effort per path, the manifest last.
            // The ORIGINAL exception is rethrown as it was; what the cleanup could not remove is said on the output and carried in its Data.
            var left = new List<string>();
            foreach (var name in names)
            {
                Remove(paths[name], left);
            }

            Remove(manifest, left);
            if (left.Count > 0)
            {
                var text = "cleanup left (remove by hand): " + string.Join(", ", left);
                failure.Data["cleanup"] = text;
                Say("M3 scaffold — " + text);
            }

            throw;
        }
    }

    /// <summary>Removes one path the scaffold named: a folder with owner access restored first, each deletion's failure caught and recorded in <paramref name="left"/>.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void Remove(string path, List<string> left)
    {
        try
        {
            if (Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is null)
            {
                Open(path, left);
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path) || new FileInfo(path).LinkTarget is not null)
            {
                File.Delete(path);
            }
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            left.Add(path);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void Open(string folder, List<string> left)
    {
        const UnixFileMode Owner = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        try
        {
            File.SetUnixFileMode(folder, File.GetUnixFileMode(folder) | Owner);
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                var info = new FileInfo(entry);
                if (info.LinkTarget is not null)
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    Open(entry, left);
                }
                else
                {
                    File.SetUnixFileMode(entry, File.GetUnixFileMode(entry) | UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            left.Add(folder);
        }
    }

    /// <summary>The manifest, claimed exclusively: one already there draws a new id.</summary>
    private static (string Id, string Path) ClaimManifest(string temp)
    {
        while (true)
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            var path = Path.Combine(temp, $"testsToDelete-m3-manifest-{id}.json");
            try
            {
                using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return (id, path);
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static string Uid() =>
        File.ReadLines("/proc/self/status").First(line => line.StartsWith("Uid:", StringComparison.Ordinal)).Split('\t', StringSplitOptions.RemoveEmptyEntries)[1];

    private static void Say(string text) => M2PilotScaffold.Say(text);

    /// <summary>The PATH of the pilot: the wrapper's folder, node's, dotnet's, <c>/usr/bin</c>, <c>/bin</c>, and on WSL the folder of <c>pwsh.exe</c> (the keep-awake needs it).</summary>
    private static string PathOf(string bin)
    {
        var folders = new List<string> { bin, Path.GetDirectoryName(M2PilotScaffold.HostTool("node"))!, Path.GetDirectoryName(M2PilotScaffold.HostTool("dotnet"))!, "/usr/bin", "/bin" };
        if (System.Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is { Length: > 0 })
        {
            var pwsh = (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(folder => File.Exists(Path.Combine(folder, "pwsh.exe")));
            if (pwsh is not null)
            {
                folders.Add(pwsh);
            }
        }

        return string.Join(':', folders.Distinct(StringComparer.Ordinal));
    }

    /// <summary>The worked example with this pilot's values, read back through <see cref="ExperimentFile.Read"/> so a file the module would refuse is never printed. ONE run <c>pilot</c>, one round of the four schemes; the isolation closes <c>old</c> and the pilot's own <c>~/.claude</c> folders, spelled absolutely so a wrong HOME can never aim them at the operator's; every step's model the fake's, the gate's another so its script can carry a delay.</summary>
    private static string Experiment(IReadOnlyDictionary<string, string> paths, string baseSha, string coaiBinary, string coaiSettings)
    {
        var example = File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));
        var tree = JsonNode.Parse(example, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
        var section = tree["target"]!;
        section["source"] = paths["target"];
        section["planBranch"] = M2PilotScaffold.PlanBranch;
        section["base"] = baseSha;
        section["plan"] = M2PilotScaffold.PlanFile;
        section["branch"] = "feat/m3-scratch";
        section["build"] = new JsonArray("dotnet", "build", M2PilotScaffold.Solution, "-c", "Debug", "-m:4", "--disable-build-servers");
        section["suite"]!["command"] = new JsonArray("dotnet", M2PilotScaffold.SuiteDll, "--report-xunit-ctrf");
        section["suite"]!["namePrefix"] = "ScratchTests.";
        section["format"]!["solution"] = M2PilotScaffold.Solution;
        tree["known"] = "Nothing is known to fail: at the base commit the scratch suite and the extension's tests pass.";
        foreach (var step in tree["steps"]!.AsObject())
        {
            step.Value!["model"] = step.Value["kind"]!.GetValue<string>() == "gate" ? FakeGate : Fake;
            step.Value["minutes"] = 2;
        }

        tree["runs"] = new JsonObject { ["pilot"] = new JsonObject { ["rounds"] = new JsonArray(new JsonArray("C1", "CR1", "SRS1", "CHH1")) } };
        tree["limits"] = JsonNode.Parse("""{"waitSeconds":30,"tries":3,"gateWaitSeconds":30,"gateTries":2,"minimumRemainingSeconds":30,"killGraceSeconds":60}""");
        tree["awake"] = System.Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is { Length: > 0 } ? "auto" : "none";
        var home = paths["home"];
        var deny = tree["tools"]!["deny"]!.AsArray();
        foreach (var entry in deny.Where(entry => entry!.GetValue<string>().Contains("//example/old-experiment", StringComparison.Ordinal) || entry.GetValue<string>().Contains("//example/vault", StringComparison.Ordinal)).ToList())
        {
            deny.Remove(entry);
        }

        tree["coai"] = new JsonObject { ["binary"] = coaiBinary, ["settings"] = coaiSettings, ["side"] = "wsl-ubuntu" };
        tree["isolation"] = new JsonObject
        {
            ["closed"] = new JsonArray(paths["old"]),
            ["unlisted"] = new JsonArray([.. new[] { "<root>", "<root>/r", "<root>/pids" }.Concat(new[] { "projects", "file-history", "session-env", "shell-snapshots" }.Select(folder => Path.Combine(home, ".claude", folder))).Select(each => (JsonNode?)JsonValue.Create(each))]),
        };
        var text = tree.ToJsonString(M2PilotScaffold.Pretty).Replace("//example/home", "/" + home, StringComparison.Ordinal).Replace("//example/root/arms.map.json", "/" + paths["root"] + "/arms.map.json", StringComparison.Ordinal);
        text.Should().NotContain("/example/", "every path of the example is replaced or removed");
        var read = ExperimentFile.Read(text, new ExperimentLocation(paths["root"], home, string.Empty, Cli.Repository.Root)).Ok();
        read.Coai.Configured.Should().BeTrue("every kept scheme has a gate step, and a gate step needs the coai server's MCP config — only the KEY is the owner's");
        return text;
    }

    /// <summary>The probe root's <c>validate</c> runs with a PATH of its own that holds no <c>pwsh.exe</c>: the keep-awake is judged by the pilot's own step 4, so the probe sees the same file with <c>awake: none</c>.</summary>
    private static string ForTheProbe(string experiment)
    {
        var tree = JsonNode.Parse(experiment)!;
        tree["awake"] = "none";
        return tree.ToJsonString(M2PilotScaffold.Pretty);
    }

    /// <summary>The fake's script: success for every call and resume; the 429 line for a fresh <c>hr-review-fix</c> prompt; a stream with no result for a fresh <c>hA-trace</c> prompt; the gate model's <c>delayMs</c> 10000 (read per model, slept before every gate call — the window in which the gate lock reads held).</summary>
    private static void WriteFakeScript(string fakeHome)
    {
        var briefs = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs");
        var limited = RunArmKit.Opening(Path.Combine(briefs, "hr-review-fix.template.md"));
        var interrupted = RunArmKit.Opening(Path.Combine(briefs, "hA-trace.template.md"));
        limited.Should().NotBe(interrupted, "two briefs that open alike cannot be told apart by their prompt");
        var script = new JsonObject
        {
            ["transcripts"] = true,
            ["stdoutFile"] = ModelArmsFixtures.Path("result-success.jsonl"),
            ["stdoutByPromptContaining"] = new JsonObject
            {
                [limited] = ModelArmsFixtures.Path("result-429-usage-limit-reached.jsonl"),
                [interrupted] = ModelArmsFixtures.Path("stream-no-result.jsonl"),
            },
            ["subjects"] = new JsonObject { [FakeGate] = new JsonObject { ["delayMs"] = 10000 } },
        };
        File.WriteAllText(Path.Combine(fakeHome, "script.json"), script.ToJsonString(M2PilotScaffold.Pretty) + "\n");
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void WriteBin(string bin, string fakeHome)
    {
        foreach (var tool in new[] { "git", "node", "npm", "dotnet" })
        {
            File.CreateSymbolicLink(Path.Combine(bin, tool), M2PilotScaffold.HostTool(tool));
        }

        var briefs = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs");
        var split = Quote(RunArmKit.Opening(Path.Combine(briefs, "x-split.template.md")));
        var git = Quote(M2PilotScaffold.HostTool("git"));
        var fakeHomeQ = Quote(fakeHome);
        var gateFirst = Quote(Path.Combine(bin, "gate-first"));
        var gateFirstArm = Quote(Path.Combine(bin, "gate-first", "arm"));
        var wrapperLog = Quote(Path.Combine(fakeHome, "wrapper.log"));
        var fakeCli = Quote(Bench.Tests.Probes.Driver.FakeCli.Executable);
        var tasks = Tasks();
        var text = $$"""
            #!/bin/sh
            # The pilot's claude: the model's file writes the fake cannot do, then the fake (M3PilotScaffold).
            IN={{fakeHomeQ}}/wrapper-in-$$
            cat > "$IN"
            case " $* " in *" --session-id "*) how=fresh;; *) how=resume;; esac
            here="$(pwd -P)"
            case "$here" in */wt/*) arm="${here%/wt/*}";; *) arm="${here%/*}";; esac
            token="${arm##*/}"
            kind=other
            case " $* " in *" --model {{FakeGate}} "*) kind=gate;; esac
            if head -n 1 "$IN" | grep -qF -- {{split}}; then kind=split; fi
            id=-
            case "$here" in */wt/*) id="${here##*/}";; esac
            if [ "$id" = - ]; then id="$(sed -n 's/.*ONE task, \*\*\(t[0-9]*\)\*\*.*/\1/p' "$IN" | head -n 1)"; [ -n "$id" ] || id=-; fi
            if [ "$kind" = gate ]; then
              if mkdir {{gateFirst}} 2>/dev/null; then
                echo "$token" > {{gateFirstArm}}
                printf '%s\n' '{"status":"blocked","why":"pilot"}' > "$arm/notes/gate.json"
                what=blocked
              else
                printf '%s\n' '{"status":"proceed"}' > "$arm/notes/gate.json"
                what=proceed
              fi
            elif [ "$kind" = split ] && [ "$how" = fresh ]; then
              mkdir -p "$arm/work"
              cat > "$arm/work/tasks.json" <<'TASKS'
            {{tasks}}
            TASKS
              for n in 1 2 3 4 5; do echo "# t$n" > "$arm/work/t$n.md"; done
              what=split
            elif [ "$id" != - ] && [ "$how" = fresh ]; then
              echo "the coder of $id wrote this line" > "$id.txt"
              {{git}} add -- "$id.txt"
              {{git}} -c user.name=m3 -c user.email=m3@example.invalid -c commit.gpgsign=false commit -q -m "task $id"
              what="coder $id"
            else
              what="$kind"
            fi
            echo "$token $how $what" >> {{wrapperLog}}
            exec {{fakeCli}} "$@" < "$IN"

            """;
        var path = Path.Combine(bin, "claude");
        File.WriteAllText(path, text);
        File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("755", 8));
    }

    /// <summary>The split's list: t1–t4 independent (t1–t3 deliver the three scenarios), t5 owns the plan and depends on every other — two batches.</summary>
    private static string Tasks() =>
        new JsonObject
        {
            ["tasks"] = new JsonArray(
                new TaskSpec("t1", ["src/A.cs", "src/ATests.cs"], [], ["S1"]).Json(),
                new TaskSpec("t2", ["src/B.cs"], [], ["S2"]).Json(),
                new TaskSpec("t3", ["src/G.cs"], [], ["S3"]).Json(),
                new TaskSpec("t4", ["src/D.cs"], [], []).Json(),
                new TaskSpec("t5", ["src/E.cs", M2PilotScaffold.PlanFile], ["t1", "t2", "t3", "t4"], []).Json()),
        }.ToJsonString();

    /// <summary>The text as one single-quoted shell word.</summary>
    private static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
