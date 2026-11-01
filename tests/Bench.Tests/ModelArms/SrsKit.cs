using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>One task of the split's list as a test writes it: its files, what it waits for and the scenarios it delivers; its brief is <c>&lt;id&gt;.md</c>.</summary>
internal sealed record TaskSpec(string Id, string[] Files, string[] Depends, string[] Scenarios)
{
    public JsonObject Json() =>
        new()
        {
            ["id"] = Id,
            ["files"] = new JsonArray([.. Files.Select(file => (JsonNode?)JsonValue.Create(file))]),
            ["brief"] = Id + ".md",
            ["depends"] = new JsonArray([.. Depends.Select(each => (JsonNode?)JsonValue.Create(each))]),
            ["scenarios"] = new JsonArray([.. Scenarios.Select(each => (JsonNode?)JsonValue.Create(each))]),
        };
}

/// <summary>A line of <c>wrapper.log</c>: how the claude wrapper was started (<c>fresh</c> or <c>resume</c>), the folder it ran in, that folder's HEAD and whether the task's file was there (<c>-</c> when the call names no task).</summary>
internal sealed record WrapperCall(string How, string Cwd, string Head, string Has);

/// <summary>The arm of the SRS tests (E4.S3): a MADE arm — <see cref="MakeKit"/> over <see cref="TargetBench"/>'s scratch target, <c>prep-base</c> run and <c>make</c> run, so the clone is a real clone with the rules submodule and the target's own wrapper — whose steps all name the fake CLI at one minute and
/// whose <c>claude</c> is a shell WRAPPER around the fake. The wrapper keeps what the fake cannot do: it saves stdin, and on a FRESH call only (its argv carries <c>--session-id</c>, never <c>--resume</c>, D9) it writes <c>&lt;id&gt;.txt</c> and commits it with the host's git by its real path when its folder is under
/// the arm's <c>wt/</c> (the id is the folder's name) or its prompt holds <c>ONE task, **t&lt;n&gt;**</c> (a batch of one, in the clone); a RESUMED call commits nothing — the task's commit is already on its kept <c>tmp/&lt;id&gt;</c> (ledger 3.1), and a second one would fail on an unchanged file or double it. Every call appends a line
/// to <c>wrapper.log</c>; a prompt that holds the split fix's wording while a staged <c>fix.json</c> exists copies it over <c>work/tasks.json</c>; then it execs the fake with the saved stdin. The fake replays per task through a script of its own that the wrapper selects for a fresh call of that task (the pause and the hold are script-wide, so <c>stdoutByPromptContaining</c> cannot carry them); a resume's prompt is the resume
/// notice, so a resume falls through to the script's <c>stdoutFile</c>. Calls are told apart by PROMPT and folder, never by the fake's call number (it can race for two calls at one instant).
/// <para>
/// What this does NOT prove: a real claude splitting a real plan or coding a real task (the fake replays, the wrapper commits one file); a cherry-pick of a real multi-hour task; Windows (shims and wrapper are POSIX).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class SrsKit : IDisposable
{
    public const string Arm = "SRS1";

    private readonly JsonObject _script = [];
    private readonly string _taskLine;

    private SrsKit(MakeKit kit, FakeCli fake, string taskLine)
    {
        Kit = kit;
        Fake = fake;
        _taskLine = taskLine;
        Root.Scratch("staged");
    }

    public MakeKit Kit { get; }

    public FakeCli Fake { get; }

    public ExperimentRoot Root => Kit.Root;

    public ArmPaths Paths => Kit.Paths(Arm);

    public string Clone => Paths.CloneFolder;

    public string WrapperLog => Path.Combine(Root.Temp, "wrapper.log");

    /// <summary>The line every task's file holds when the caller names none: the task's id, which is no line a reader of the code counts (E6.S1 names a longer one).</summary>
    public const string IdLine = "$id";

    /// <param name="taskLine">The one line the wrapper writes into <c>&lt;id&gt;.txt</c> of a FRESH task call, shell text with <c>$id</c> for the task's id; <see cref="IdLine"/> when none is given.</param>
    public static async Task<SrsKit> CreateAsync(string taskLine = IdLine)
    {
        MakeKit.Posix();
        var kit = await MakeKit.CreateAsync(Arm);
        try
        {
            var made = await kit.MakeAsync();
            made.Code.Should().Be(0, "the arm is made by make itself: " + made.Everything);
            return Over(kit, taskLine);
        }
        catch
        {
            kit.Dispose();
            throw;
        }
    }

    /// <summary>The kit over a <see cref="MakeKit"/> that has ALREADY made its arms (E6.S1: one root holding four): the fake, the wrapper as <c>claude</c> and the small limits are installed on it. The kit then owns <paramref name="kit"/> and disposes it.</summary>
    public static SrsKit Over(MakeKit kit, string taskLine = IdLine)
    {
        var built = new SrsKit(kit, new FakeCli(), taskLine);
        built.Install();
        return built;
    }

    /// <summary>The valid list of five: t1–t4 independent (t1 owns two files and delivers S1), t5 owns the plan and depends on every other — batches <c>[[t1 t2 t3 t4], [t5]]</c>.</summary>
    public IReadOnlyList<TaskSpec> Five() =>
    [
        new("t1", ["src/A.cs", "src/ATests.cs"], [], ["S1"]),
        new("t2", ["src/B.cs"], [], []),
        new("t3", ["src/G.cs"], [], []),
        new("t4", ["src/D.cs"], [], []),
        new("t5", ["src/E.cs", Root.ExperimentOf().Target.Plan], ["t1", "t2", "t3", "t4"], []),
    ];

    /// <summary>Writes <c>work/tasks.json</c> and each task's brief <c>work/&lt;id&gt;.md</c> — the fake writes no file, so the split's work is done here before the run.</summary>
    public void Plan(IEnumerable<TaskSpec> tasks)
    {
        var list = tasks.ToList();
        Put(Paths.TasksJson, new JsonObject { ["tasks"] = new JsonArray([.. list.Select(task => (JsonNode?)task.Json())]) }.ToJsonString());
        foreach (var task in list)
        {
            Put(Path.Combine(Paths.Work, task.Id + ".md"), $"# {task.Id}\n");
        }
    }

    /// <summary>The list the split fix will write: staged as <c>fix.json</c>, with its briefs, for the wrapper to copy over <c>work/tasks.json</c> when it sees the fix's prompt.</summary>
    public void Stage(IEnumerable<TaskSpec> tasks)
    {
        var list = tasks.ToList();
        File.WriteAllText(Path.Combine(Root.Temp, "staged", "fix.json"), new JsonObject { ["tasks"] = new JsonArray([.. list.Select(task => (JsonNode?)task.Json())]) }.ToJsonString());
        foreach (var task in list.Where(task => !File.Exists(Path.Combine(Paths.Work, task.Id + ".md"))))
        {
            Put(Path.Combine(Paths.Work, task.Id + ".md"), $"# {task.Id}\n");
        }
    }

    public static string TasksText(IEnumerable<TaskSpec> tasks) => new JsonObject { ["tasks"] = new JsonArray([.. tasks.Select(task => (JsonNode?)task.Json())]) }.ToJsonString();

    /// <summary>Every call replays <paramref name="fixture"/>; a pause per line and a hold may be added.</summary>
    public void Replay(string fixture, int lineDelayMs = 0, int holdAfterLine = 0)
    {
        _script["stdoutFile"] = ModelArmsFixtures.Path(fixture);
        Set("lineDelayMs", lineDelayMs);
        Set("holdAfterLine", holdAfterLine);
        Write();
    }

    /// <summary>The coder of <paramref name="id"/> replays <paramref name="fixture"/>, with its own pause per line and its own hold — a script of its own that the wrapper selects (<c>FAKE_CLI_SCRIPT</c>) for a FRESH call of that task; its RESUME replays <paramref name="resumedFixture"/> when one is given, else the arm's script (the prompt of a resume is the resume notice, nothing to match a replay on).</summary>
    public void ReplayFor(string id, string fixture, int lineDelayMs = 0, int holdAfterLine = 0, string? resumedFixture = null)
    {
        if (resumedFixture is not null)
        {
            File.WriteAllText(Path.Combine(Root.Temp, "staged", $"script-{id}.resume.json"), ExperimentRoot.Script(new JsonObject { ["stdoutFile"] = ModelArmsFixtures.Path(resumedFixture) }).ToJsonString());
        }

        var script = new JsonObject { ["stdoutFile"] = ModelArmsFixtures.Path(fixture) };
        if (lineDelayMs > 0)
        {
            script["lineDelayMs"] = lineDelayMs;
        }

        if (holdAfterLine > 0)
        {
            script["holdAfterLine"] = holdAfterLine;
        }

        File.WriteAllText(Path.Combine(Root.Temp, "staged", $"script-{id}.json"), ExperimentRoot.Script(script).ToJsonString());
    }

    /// <summary>The split fix's FRESH call replays <paramref name="fixture"/> (the wrapper recognises it by its wording): the fix a test kills after it wrote a valid list.</summary>
    public void ReplayFix(string fixture) =>
        File.WriteAllText(Path.Combine(Root.Temp, "staged", "script-fix.json"), ExperimentRoot.Script(new JsonObject { ["stdoutFile"] = ModelArmsFixtures.Path(fixture) }).ToJsonString());

    public void ForgetReplays()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root.Temp, "staged"), "script-*.json"))
        {
            File.Delete(file);
        }
    }

    private void Set(string name, int value)
    {
        if (value > 0)
        {
            _script[name] = value;
        }
        else
        {
            _script.Remove(name);
        }
    }

    public void Write() => Fake.Rewrite(ExperimentRoot.Script(JsonNode.Parse(_script.ToJsonString())!.AsObject()));

    /// <summary><c>run-arm SRS1</c> through the console's own entry point.</summary>
    public Task<ArmRun> RunAsync(CancellationToken? stopping = null) => RunArmKit.RunAsync(Root, Arm, stopping);

    /// <summary>The fake's calls whose folder is under the arm's <c>wt/</c>: the coders of a batch of many.</summary>
    public IReadOnlyList<FakeCliCall> InWorktrees() => [.. Fake.Calls().Where(call => call.Cwd.StartsWith(Paths.Worktrees + Path.DirectorySeparatorChar, StringComparison.Ordinal))];

    /// <summary>The fake's calls whose prompt is the task coder's brief (fresh launches).</summary>
    public IReadOnlyList<FakeCliCall> Coders() => [.. Fake.Calls().Where(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal))];

    public IReadOnlyList<WrapperCall> Wrapper() =>
        File.Exists(WrapperLog)
            ? [.. File.ReadAllLines(WrapperLog).Where(line => line.Length > 0).Select(line => line.Split(' ')).Select(parts => new WrapperCall(parts[0], parts[1], parts[2], parts[3]))]
            : [];

    public Task<string> GitAsync(string folder, params string[] arguments) => TargetBench.GitAsync(folder, arguments);

    /// <summary>The subjects of the commits on the arm's branch past <paramref name="from"/>, oldest first.</summary>
    public async Task<IReadOnlyList<string>> SubjectsAsync(string from) =>
        [.. (await GitAsync(Clone, "log", "--reverse", "--format=%s", $"{from}..HEAD")).Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    public async Task<string> HeadAsync(string folder) => (await GitAsync(folder, "rev-parse", "HEAD")).Trim();

    public async Task<IReadOnlyList<string>> TrackedAsync(string folder, string revision = "HEAD") =>
        [.. (await GitAsync(folder, "ls-tree", "-r", "--name-only", revision)).Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    public async Task<IReadOnlyList<string>> TmpBranchesAsync() =>
        [.. (await GitAsync(Clone, "branch", "--list", "tmp/*", "--format=%(refname:short)")).Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Edits <c>experiment.json</c> (the file the next entry of the verb reads).</summary>
    public void Edit(Action<JsonObject> change) => Root.Edit(top => change(top.AsObject()));

    /// <summary><c>checks SRS1</c> through the gate, as the console runs it.</summary>
    public async Task<ChecksRun> ChecksAsync()
    {
        // The in-process runner wrote its own pid (E5.S1) into pids/<arm>.pid and that pid is this test host, which is alive: `checks` reads a live pid as a live runner. The runner has ended, so its pid file goes (a real runner's pid dies with it).
        File.Delete(Path.Combine(Root.Root, "pids", $"{Arm}.pid"));
        var line = ModelArmsCommandLine.Split(["checks", Arm, "--experiment", Root.Root]).Ok();
        var output = new StringWriter();
        var error = new StringWriter();
        var input = new ModelArmsApp.VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, seen => ChecksVerb.RunAsync(seen, new ChecksSeams(new TickingClock(), null)), ModelArmsApp.GateSeams.Real);
        return new ChecksRun(code, output.ToString(), error.ToString());
    }

    public JsonNode Objective() => JsonNode.Parse(File.ReadAllText(Paths.Objective))!;

    public IReadOnlyList<TimingLine> Timing() => Root.TimingOf(Arm);

    private static void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Points every step at the fake with one minute and small limits, puts the wrapper in the PATH folder in place of nothing, and tells the fake where its script is.</summary>
    private void Install()
    {
        Edit(top =>
        {
            foreach (var step in top["steps"]!.AsObject())
            {
                step.Value!["model"] = Fake.Model;
                step.Value["minutes"] = 1;
            }

            top["limits"] = JsonNode.Parse("""{"waitSeconds":1,"tries":1,"gateWaitSeconds":1,"gateTries":1,"minimumRemainingSeconds":1,"killGraceSeconds":1}""");
        });
        var bin = Path.Combine(Root.Temp, "bin");
        Root.ShimScript(Path.Combine(bin, "claude"), WrapperScript());
        Root.WithVariables(("FAKE_CLI_HOME", Fake.Home));
        Write();
    }

    private string WrapperScript()
    {
        var staged = Path.Combine(Root.Temp, "staged");
        return $$"""
            IN='{{Root.Temp}}'/wrapper-in-$$
            cat > "$IN"
            case " $* " in *" --session-id "*) how=fresh;; *) how=resume;; esac
            here="$(pwd -P)"
            id=-
            case "$here" in '{{Paths.Worktrees}}'/*) id="${here##*/}";; esac
            if [ "$id" = - ]; then id="$(sed -n 's/.*ONE task, \*\*\(t[0-9]*\)\*\*.*/\1/p' "$IN" | head -n 1)"; [ -n "$id" ] || id=-; fi
            has=-
            if [ "$id" != - ]; then
              if [ "$how" = fresh ]; then
                echo "{{_taskLine}}" > "$id.txt"
                '{{ExperimentRoot.RealGit}}' add -- "$id.txt"
                '{{ExperimentRoot.RealGit}}' -c user.name=test -c user.email=test@example.invalid -c commit.gpgsign=false commit -q -m "task $id"
              fi
              if [ -f "$id.txt" ]; then has=yes; else has=no; fi
              if [ "$how" = fresh ] && [ -f '{{staged}}/script-'"$id"'.json' ]; then export FAKE_CLI_SCRIPT='{{staged}}/script-'"$id"'.json'; fi
              if [ "$how" = resume ] && [ -f '{{staged}}/script-'"$id"'.resume.json' ]; then export FAKE_CLI_SCRIPT='{{staged}}/script-'"$id"'.resume.json'; fi
            fi
            echo "$how $here $('{{ExperimentRoot.RealGit}}' rev-parse HEAD) $has" >> '{{WrapperLog}}'
            if grep -q 'was checked by a script and has these problems' "$IN" && [ -f '{{staged}}/fix.json' ]; then cp '{{staged}}/fix.json' '{{Paths.TasksJson}}'; fi
            if grep -q 'was checked by a script and has these problems' "$IN" && [ -f '{{staged}}/script-fix.json' ]; then export FAKE_CLI_SCRIPT='{{staged}}/script-fix.json'; fi
            exec '{{FakeCli.Executable}}' "$@" < "$IN"
            """;
    }

    public void Dispose()
    {
        Fake.Dispose();
        Kit.Dispose();
    }
}
