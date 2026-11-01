using System.Globalization;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;
using static Bench.Tests.ModelArms.ConsumerFacts;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E6.S1 (D28, D5's compatibility) - every checked-in reader of the harness run over a fake arm of each scheme (C, CR, CHH, SRS) that the module itself wrote with its own <c>make</c>, <c>run-arm</c> and <c>checks</c>: seven readers (parts 1-5's <c>metrics.mjs</c>, <c>t-survival.mjs</c>, <c>compare.mjs</c>, <c>make-tables.mjs</c> and part 6's
/// <c>metrics6.mjs</c>, <c>coai-findings.mjs</c>, <c>coai-cost.mjs</c>), each from a scratch COPY of its pinned source with only the rewrites <see cref="Rewrite"/> names, each given what it looks for and its result asserted - a number or a name the arm produced, read back from the module's files. The one rename (<c>task-&lt;id&gt;-haiku.*</c> → <c>task-&lt;id&gt;-coder.*</c>) is proved to be the whole
/// difference; the shell's layout (the seven pinned scripts, <see cref="ShellLayout"/>) is compared with the module's (<see cref="ModuleSpellings"/>) in both directions; and the double count of a resumed step's cost (E2 §8 item 62) is measured in the readers, not fixed. Linux only (links, node, the POSIX shims).
/// <para>
/// What this does NOT prove: a real claude's streams (the fake replays recorded shapes), a real multi-hour task's diff, the judges (<c>ObjectiveReportTests</c>' round trip stands for them), the parts 1-5 Q/H readers' inputs (the module writes no fill files), <c>make-tables.mjs</c>'s part-6 and part-7 sections (it imports them only when <c>data/part6</c> or <c>data/part7</c> exists, and the test creates neither), the fitted
/// unit prices of <c>metrics.mjs</c> (three priced attempts per model are needed), or <c>compare.mjs</c> over a CR, CHH or SRS arm (its list holds none).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ConsumerCompatibilityTests(ConsumerArms arms) : IClassFixture<ConsumerArms>
{
    private const string Gate = "4-gate";

    /// <summary>The readers' names, for the two theories.</summary>
    public static TheoryData<string> ReaderNames => [.. PinnedReaders.Run.Select(reader => reader.Name)];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PinnedReader ReaderNamed(string name) => PinnedReaders.Run.Single(reader => reader.Name == name);

    private async Task<StagedRun> StagedAsync(PinnedReader reader)
    {
        MakeKit.Posix();
        return await ConsumerReaders.RunAsync(arms, reader);
    }

    [Theory]
    [MemberData(nameof(ReaderNames))]
    public async Task Every_checked_in_reader_reads_a_fake_arm_of_each_scheme_without_an_error(string name)
    {
        var reader = ReaderNamed(name);
        reader.Timeout.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30), "a reader's timeout is ten times its measured wall and never under 30 s");

        var staged = await StagedAsync(reader);

        staged.Run.ExitCode.Should().Be(0, $"{name} must read the four fake arms: {staged.Run.Error}{staged.Run.Output}");
        staged.Run.Error.Should().BeEmpty($"{name} says nothing on stderr when it reads what the module wrote");
        staged.Output.Should().NotBeNullOrWhiteSpace($"{name}'s result ({(staged.OutputFile.Length > 0 ? staged.OutputFile : "its stdout")}) must exist");
        if (name == "make-tables.mjs")
        {
            staged.Output.Should().StartWith("# Tables", "make-tables.mjs writes a markdown document");
        }
        else
        {
            staged.Json.Should().NotBeNull($"{name}'s result parses as JSON");
        }
    }

    [Theory]
    [MemberData(nameof(ReaderNames))]
    public async Task The_readers_report_what_the_fake_arm_did(string name)
    {
        var staged = await StagedAsync(ReaderNamed(name));

        staged.Run.ExitCode.Should().Be(0, $"{name} must read the arms before it can report on them: {staged.Run.Error}");
        switch (name)
        {
            case "metrics.mjs":
                MetricsReportsTheArms(staged);
                break;
            case "metrics6.mjs":
                Metrics6ReportsTheArms(staged);
                break;
            case "t-survival.mjs":
                TSurvivalReportsTheTasks(staged);
                break;
            case "compare.mjs":
                await CompareReportsTheClassicArmAsync(staged);
                break;
            case "make-tables.mjs":
                MakeTablesReportsTheArms(staged);
                break;
            case "coai-findings.mjs":
                CoaiFindingsReportsTheGate(staged);
                break;
            default:
                CoaiCostReportsTheGate(staged);
                break;
        }
    }

    private void MetricsReportsTheArms(StagedRun staged)
    {
        var report = staged.Json["report"]!;
        report.AsObject().Select(each => each.Key).Should().BeEquivalentTo(ConsumerArms.Arms.Select(each => PinnedConsumer.Listed(each.Arm)), "the reader lists exactly the arms it was given a timing for");
        foreach (var (arm, scheme) in ConsumerArms.Arms)
        {
            ArmRanItsScheme(arm, scheme);
            var listed = report[PinnedConsumer.Listed(arm)]!;
            var steps = listed["steps"]!.AsObject();
            steps.Select(each => each.Key).Should().Equal(StepsOf(arms, arm), $"{arm}: the distinct steps of its start lines, in start order");
            foreach (var step in StepsOf(arms, arm))
            {
                var attempts = steps[step]!.AsArray();
                attempts.Should().HaveCount(Starts(arms, arm, step).Count, $"{arm} {step}: one attempt per start line");
                foreach (var start in Starts(arms, arm, step))
                {
                    var wall = attempts[start.Attempt - 1]!["wallSeconds"];
                    wall.Should().NotBeNull($"{arm} {step} a{start.Attempt}: the reader found the attempt's wall in its end line");
                    wall!.GetValue<long>().Should().Be(EndOf(arms, arm, step, start.Attempt).WallSeconds.Value, $"{arm} {step} a{start.Attempt}: the runner's own end line");
                }
            }

            var slow = steps[ConsumerArms.SlowStepOf(scheme)]![0]!["wallSeconds"]!.GetValue<long>();
            slow.Should().BeGreaterThan(0, $"{arm}: the slow step's wall is not a zero that every wall would pass on");
            listed["wallSeconds"]!.GetValue<long>().Should().Be(WallOf(arms, arm), $"{arm}: the sum of its attempts' walls");
            listed["vllm"].Should().BeNull($"{arm}: the module runs no local model and writes no vllm/requests file");
        }

        var limited = report[PinnedConsumer.Listed("CR1")]!["steps"]!["2-hreview"]!.AsArray();
        limited.Should().HaveCount(2, "CR1's limited review was resumed");
        limited[0]!["terminal"]!.GetValue<string>().Should().Be(TerminalOf(arms, "CR1", "2-hreview", 1), "the limited attempt's terminal reason is the 429 stream's own");
        var interrupted = report[PinnedConsumer.Listed("CHH1")]!["steps"]!["2-hA"]!.AsArray();
        interrupted[0]!["complete"]!.GetValue<bool>().Should().BeFalse("CHH1's 2-hA a1 had no result");
        interrupted[0]!["cost"].Should().BeNull("an attempt without a result has no host cost - never guessed");
        var (model, usage) = TranscriptFixture();
        var counted = interrupted[0]!["byModel"]![model]!;
        counted["input"]!.GetValue<long>().Should().Be(usage["input_tokens"]!.GetValue<long>(), "the interrupted attempt's tokens are the TRANSCRIPT's (the stream's message carries no usage)");
        counted["output"]!.GetValue<long>().Should().Be(usage["output_tokens"]!.GetValue<long>());
        counted["cacheRead"]!.GetValue<long>().Should().Be(usage["cache_read_input_tokens"]!.GetValue<long>());
        counted["cacheWrite"]!.GetValue<long>().Should().Be(usage["cache_creation_input_tokens"]!.GetValue<long>());
        var gate = report[PinnedConsumer.Listed("C1")]!["steps"]![Gate]!.AsArray();
        gate[0]!["cost"]!.GetValue<double>().Should().Be(CostOf(arms, "C1", Gate, 1));
        gate[1]!["cost"]!.GetValue<double>().Should().Be(CostOf(arms, "C1", Gate, 2));
    }

    private void Metrics6ReportsTheArms(StagedRun staged)
    {
        var report = staged.Json.AsObject();
        report.Select(each => each.Key).Should().Equal(ConsumerArms.Arms.Select(each => each.Arm), "metrics6.mjs keys its report by the map's arms");
        foreach (var (arm, scheme) in ConsumerArms.Arms)
        {
            ArmRanItsScheme(arm, scheme);
            var steps = report[arm]!["steps"]!.AsObject();
            report[arm]!["attempts"]!.GetValue<int>().Should().Be(Directory.EnumerateFiles(arms.PathsOf(arm).Out, "*.jsonl").Count(file => file.Contains(".a", StringComparison.Ordinal)), $"{arm}: one attempt per .a<N>.jsonl of its out folder");
            foreach (var step in steps)
            {
                step.Value!["wall_s"]!.GetValue<long>().Should().Be(arms.Root.TimingOf(arm).OfType<TimingLine.End>().Where(end => end.Step == step.Key).Sum(end => end.WallSeconds.Value), $"{arm} {step.Key}: the sum of its end lines' wall_s");
                step.Value["model"]!.GetValue<string>().Should().Be(Starts(arms, arm, step.Key).Last().Model, $"{arm} {step.Key}: its start line's model");
            }

            steps[ConsumerArms.SlowStepOf(scheme)]!["wall_s"]!.GetValue<long>().Should().BeGreaterThan(0);
            steps[ConsumerArms.SlowStepOf(scheme)]!["model"]!.GetValue<string>().Should().Be(arms.SlowModel);
            report[arm]!["span_min"].Should().NotBeNull($"{arm}: read from its arm-done line (minute-rounded, so only its presence is asserted)");
        }

        var coders = report["SRS1"]!["steps"]!.AsObject().Where(step => step.Key.StartsWith("2-t", StringComparison.Ordinal)).ToList();
        coders.Should().HaveCount(TaskCount(), "one coder step per task");
        coders.Should().OnlyContain(step => step.Value!["kind"]!.GetValue<string>() == "t-code", "SRS1's task coders are of kind t-code");
        report["C1"]!["steps"]![Gate]!["cost"]!.GetValue<double>().Should().Be(CostOf(arms, "C1", Gate, 2), "the last result of the gate's one session");
    }

    /// <summary>The arm's own timing names every step of its scheme: what the readers' reports are compared with is not an empty file's nothing.</summary>
    private void ArmRanItsScheme(string arm, string scheme)
    {
        var steps = StepsOf(arms, arm);
        if (scheme != "SRS")
        {
            steps.Should().Equal(RunArmKit.StepsOf(scheme), $"{arm} ran every step of the {scheme} scheme");
            return;
        }

        steps.Should().StartWith(new[] { "1-split" }).And.EndWith(new[] { "3-hreview", "4-review", "5-fix", "6-gate" });
        steps.Where(step => step.EndsWith("-code", StringComparison.Ordinal)).Should().HaveCount(TaskCount(), "SRS1 ran one coder step per task");
    }

    private int TaskCount() => ConsumerReaders.TaskIds(arms).Count;

    private void TSurvivalReportsTheTasks(StagedRun staged)
    {
        var ids = ConsumerReaders.TaskIds(arms);
        staged.Json["tasks"]!.AsArray().Select(task => task!["id"]!.GetValue<string>()).Should().Equal(ids, "the tasks of work/batches.json");
        staged.Json["haikuLines"]!.GetValue<int>().Should().Be(ids.Count, "one meaningful line per task, each in the diff the coder left");
        staged.Json["keptFinal"]!.GetValue<double>().Should().Be(1, "every task's line is in the arm's final code: the pick-backs kept them");
        staged.Json["reviewChangedLines"].Should().BeNull("no scheme reviews per task");
        staged.Run.Output.Should().Contain("no task review ran");
        foreach (var id in ids)
        {
            File.ReadAllText(arms.PathsOf("SRS1").CoderDiff(id)).Should().Contain(ConsumerArms.TaskLine.Replace("$id", id, StringComparison.Ordinal), $"{id}'s coder diff holds the line the reader counts");
        }
    }

    private async Task CompareReportsTheClassicArmAsync(StagedRun staged)
    {
        var rows = staged.Json["rows"]!.AsArray();
        rows.Should().ContainSingle("compare.mjs lists C, C2.. and not a CR, CH or SR arm (:12)");
        var row = rows[0]!;
        row["arm"]!.GetValue<string>().Should().Be(PinnedConsumer.Listed("C1"));
        row["steps"]![Gate].Should().NotBeNull($"compare lists C's {Gate}");
        row["steps"]![Gate]!["cost"]!.GetValue<double>().Should().Be(CostOf(arms, "C1", Gate, 1) + CostOf(arms, "C1", Gate, 2), "compare sums every attempt of the gate step (:34-35)");
        row["cost"].Should().BeNull("the arm's other attempts carry no cost, so metrics.mjs's total is NaN, written as null");
        row["objective"]!.GetValue<string>().Should().NotBe("n/a", "objective.json was linked and read");
        row["scenarios"]!.GetValue<int>().Should().Be(ObjectiveOf(arms, "C1")["scenariosPresentCount"]!.GetValue<int>());
        row["judge"].Should().BeNull("no judge scores were given");
        row["survival"].Should().BeNull("survival.json is the Q arms'");

        var metrics = await ConsumerReaders.RunAsync(arms, PinnedReaders.Metrics);
        var listed = metrics.Json["report"]!.AsObject().Select(each => each.Key).ToList();
        foreach (var arm in new[] { "CR1", "CHH1", "SRS1" })
        {
            listed.Should().Contain(PinnedConsumer.Listed(arm), $"metrics.json, compare's input, holds {arm}");
            rows.Select(each => each!["arm"]!.GetValue<string>()).Should().NotContain(PinnedConsumer.Listed(arm), $"compare.mjs:12 drops {arm} by its own list - and the test never relabels an arm to keep it");
        }
    }

    private void MakeTablesReportsTheArms(StagedRun staged)
    {
        staged.Run.Output.Should().Contain($"tables.md written: {ConsumerArms.Arms.Count} arms");
        var tables = staged.Output;
        var objective = Section(tables, "## 3. Objective gate");
        var review = Section(tables, "## 3b.");
        foreach (var (arm, _) in ConsumerArms.Arms)
        {
            var listed = PinnedConsumer.Listed(arm);
            var shortstat = ObjectiveOf(arms, arm)["diff"]!["shortstat"]!.GetValue<string>();
            RowOf(objective, listed).Should().EndWith($"| {shortstat} |", $"{listed}: §3's last cell is objective.json's diff.shortstat");
            var verdict = ReviewOf(arms, arm);
            var findings = verdict["findings"]!.AsArray();
            var counts = new[] { "blocking", "major", "minor" }.Select(severity => findings.Count(finding => finding!["severity"]!.GetValue<string>() == severity));
            RowOf(review, listed).Should().Be($"| {listed} | {findings.Count} | {string.Join(" / ", counts)} | {verdict["verdict"]!.GetValue<string>()} |", $"{listed}: §3b's row is notes/review.json");
        }

        ObjectiveOf(arms, "SRS1")["diff"]!["shortstat"]!.GetValue<string>().Should().NotBeEmpty("SRS1's tasks committed files");
        var tasks = Section(tables, "## 4b.");
        var sr = RowOf(tasks, "SR1");
        var batches = BatchesFile.Read(File.ReadAllText(arms.PathsOf("SRS1").BatchesJson), arms.PathsOf("SRS1").Work).Ok();
        sr.Should().Contain($"| {batches.Tasks.Count} / {batches.Batches.Count} (", "§4b: tasks / batches from batches.json").And.Contain($"| {batches.Tasks.Count} → 100 % |", "§4b: Haiku lines → kept, from t-survival.json");
        sr.Split('|')[2].Trim().Should().NotBe("n/a", "§4b's elapsed is read from timing.jsonl (minute-rounded, so only its presence is asserted)");
        RowOf(Section(tables, "## 8."), "C").Should().Contain("not run", "no mutation check ran for C");
        RowOf(Section(tables, "## 6."), "C").Should().Contain("not judged yet", "no judge scored C");
    }

    private void CoaiFindingsReportsTheGate(StagedRun staged)
    {
        var round = GateRoundOf(arms);
        var rounds = staged.Json["C1"]!.AsArray();
        rounds.Should().ContainSingle("C1's gate stream holds one coai code round");
        rounds[0]!["stage"]!.GetValue<string>().Should().Be("code");
        rounds[0]!["verdict"]!.GetValue<string>().Should().Be(round.Verdict);
        var findings = rounds[0]!["findings"]!.AsArray();
        findings.Should().HaveCount(round.Findings);
        findings[0]!["decision"]!.GetValue<string>().Should().Be(round.Action, "the resolve's decisions (a JSON string) joined the finding");
        findings[0]!["reason"]!.GetValue<string>().Should().Be(round.Reason);
        foreach (var arm in new[] { "CR1", "CHH1", "SRS1" })
        {
            staged.Json[arm]!.AsArray().Should().BeEmpty($"{arm}'s gate made no coai call");
        }
    }

    private void CoaiCostReportsTheGate(StagedRun staged)
    {
        var round = GateRoundOf(arms);
        var rounds = staged.Json["C1"]!.AsArray();
        rounds.Should().ContainSingle();
        rounds[0]!["tokensIn"]!.GetValue<long>().Should().Be(round.TokensIn);
        rounds[0]!["tokensOut"]!.GetValue<long>().Should().Be(round.TokensOut);
        foreach (var arm in new[] { "CR1", "CHH1", "SRS1" })
        {
            staged.Json[arm]!.AsArray().Should().BeEmpty($"{arm}'s gate made no coai call");
        }
    }

    [Fact]
    public async Task The_renamed_coder_files_reach_t_survival_through_the_named_map()
    {
        MakeKit.Posix();
        var ids = ConsumerReaders.TaskIds(arms);

        var mapped = await ConsumerReaders.TSurvivalAsync(arms, mapped: true);
        var unmapped = await ConsumerReaders.TSurvivalAsync(arms, mapped: false);

        mapped.Run.ExitCode.Should().Be(0, mapped.Run.Error);
        mapped.Json["haikuLines"]!.GetValue<int>().Should().Be(ids.Count, "with the one-line map, task-<id>-haiku.diff -> task-<id>-coder.diff, the reader finds every task's lines");
        unmapped.Run.ExitCode.Should().Be(0, "the reader does not fail on a name it does not know: it reads nothing");
        ids.Should().OnlyContain(id => new FileInfo(arms.PathsOf("SRS1").CoderDiff(id)).Length > 0, "the coder diffs the unmapped run links are not empty: the zero below is the NAME's");
        unmapped.Json["haikuLines"]!.GetValue<int>().Should().Be(0, "under the module's own name task-<id>-coder.diff the reader sees no diff at all");
        unmapped.Json["keptFinal"].Should().BeNull();
    }

    [Fact]
    public void A_file_the_shell_wrote_that_the_module_does_not_is_listed_by_name_or_the_test_fails()
    {
        MakeKit.Posix();
        var unaccounted = ShellLayout.Paths().Where(path => !ShellLayout.IsSpelled(path) && !ShellLayout.Renamed.ContainsKey(path) && !ShellLayout.ShellOnly.ContainsKey(path)).ToList();

        unaccounted.Should().BeEmpty("every path the shell built is spelled by the module, renamed with its decision, or listed as the shell's alone with its reason - add a row to ShellLayout.Renamed or ShellLayout.ShellOnly for: " + string.Join(", ", unaccounted));
        ShellLayout.Renamed.Keys.Concat(ShellLayout.ShellOnly.Keys).Should().OnlyContain(path => ShellLayout.Paths().Contains(path), "a row for a path the scan no longer finds is a stale decision");
        ShellLayout.Renamed.Keys.Concat(ShellLayout.ShellOnly.Keys).Should().OnlyContain(path => !ShellLayout.IsSpelled(path), "a row for a path the module spells under that very name says nothing");
        ShellLayout.Renamed.Values.Should().OnlyContain(rename => ModuleSpellings.All.Contains(rename.Module), "a rename names a path the module really spells");
        ShellLayout.Renamed.Values.Should().OnlyContain(rename => rename.Decision.Length > 0);
        ShellLayout.ShellOnly.Values.Should().OnlyContain(why => why.Length > 0);
    }

    [Fact]
    public void A_file_the_module_writes_that_the_shell_did_not_is_listed_by_name_with_its_decision()
    {
        MakeKit.Posix();
        var unaccounted = ModuleSpellings.All.Where(spelling => !ShellLayout.IsFound(spelling) && !ShellLayout.Additions.ContainsKey(spelling) && !ShellLayout.Renamed.Values.Any(rename => rename.Module == spelling)).ToList();

        unaccounted.Should().BeEmpty("every path the module spells is one the shell built, a rename of one, or an addition with its decision - add a row to ShellLayout.Additions for: " + string.Join(", ", unaccounted));
        ShellLayout.Additions.Keys.Should().OnlyContain(spelling => ModuleSpellings.All.Contains(spelling), "a row for a path the module no longer spells is a stale decision");
        ShellLayout.Additions.Keys.Should().OnlyContain(spelling => !ShellLayout.IsFound(spelling), "a path the shell builds is no addition");
        ShellLayout.Additions.Values.Should().OnlyContain(decision => decision.Length > 0);
    }

    [Fact]
    public void The_shell_scan_still_finds_known_paths_in_each_pinned_script()
    {
        var known = new Dictionary<string, string[]>
        {
            ["run-arm.sh"] = ["r/<token>/out/timing.jsonl", "r/<token>/notes/hA.json", ".gate.lock", "r/<token>/out/<step>.a<n>.jsonl"],
            ["t-lib.sh"] = ["r/<token>/out/task-<id>-haiku.diff", "r/<token>/wt/<id>"],
            ["objective-checks.sh"] = ["r/<token>/out/objective.json", "r/<token>/out/objective/build.log"],
            ["after6.sh"] = ["chain/arms-done", "r/<token>/out/objective.out", "r/<token>/out/prep-build.log"],
            ["after7.sh"] = ["chain7/locked", "chain7/arms-done"],
            ["lock.sh"] = [".locked-modes.restored-<stamp>", ".locked-modes"],
            ["run-all.sh"] = ["pids/<arm>.pid", "pids/<arm>.log"],
        };

        foreach (var script in ShellLayout.Scripts)
        {
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Cli.Repository.Root, ShellLayout.Part6 + script.Name)))).Should().Be(script.Sha256, $"{script.Name} is pinned by the parent's §2");
            ShellLayout.Scan(script).Should().Contain(known[script.Name], $"the scan of {script.Name} must still find these");
            foreach (var root in script.Roots)
            {
                ShellLayout.TextOf(root.AssignedIn.Length > 0 ? ShellLayout.Scripts.Single(other => other.Name == root.AssignedIn) : script).Should().Contain($"{root.Variable}={root.Assignment}", $"{script.Name}: ${root.Variable} stands for {(root.Expansion.Length > 0 ? root.Expansion : "the root")} as it is assigned there");
            }
        }

        ShellLayout.Paths().Should().Contain(["r/<token>/out/objective.out", "r/<token>/out/prep-build.log", ".locked-modes.restored-<stamp>"], "the three shell-written files the parent's table of additions missed (E6 §8 item 11)");
    }

    [Fact]
    public void Every_file_a_fake_arm_holds_is_one_the_module_spells()
    {
        MakeKit.Posix();
        var root = arms.Root.Root;
        var held = new List<string>();
        held.AddRange(Directory.EnumerateFileSystemEntries(root).Select(each => Path.GetRelativePath(root, each)));
        foreach (var folder in new[] { "pids", "chain" })
        {
            held.AddRange(Directory.EnumerateFileSystemEntries(Path.Combine(root, folder), "*", SearchOption.AllDirectories).Select(each => Path.GetRelativePath(root, each)));
        }

        foreach (var (arm, _) in ConsumerArms.Arms)
        {
            var paths = arms.PathsOf(arm);
            held.AddRange(Directory.EnumerateFileSystemEntries(paths.Folder).Select(each => Path.GetRelativePath(root, each)));
            foreach (var folder in new[] { paths.Out, paths.Notes, paths.Work })
            {
                held.AddRange(Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories).Select(each => Path.GetRelativePath(root, each)));
            }
        }

        held.Select(path => path.Replace('\\', '/')).Should().Contain(["experiment.json", $"chain/made-C1", "pids/C1.lock", $"r/{arms.PathsOf("C1").Token}/out/timing.jsonl"], "the walk finds the files it is about");
        var unspelled = held.Select(path => System.Text.RegularExpressions.Regex.Replace(path.Replace('\\', '/'), "^r/[0-9a-f]{16}/", "r/<token>/", System.Text.RegularExpressions.RegexOptions.CultureInvariant)).Where(path => !ModuleSpellings.All.Any(spelling => ModuleSpellings.PatternOf(spelling).IsMatch(path))).Distinct().Order(StringComparer.Ordinal).ToList();

        unspelled.Should().BeEmpty("the module wrote these files and spells none of them in RootPaths, ArmPaths or the example's bindings: " + string.Join(", ", unspelled));
    }

    [Fact]
    public async Task A_resumed_steps_cost_is_its_sessions_last_result_in_metrics6_and_every_attempt_in_metrics_mjs()
    {
        MakeKit.Posix();
        var starts = Starts(arms, "C1", Gate);
        starts.Select(start => start.Session).Distinct().Should().ContainSingle("the blocked gate was CONTINUED: both attempts are one session");
        var (first, second) = (CostOf(arms, "C1", Gate, 1), CostOf(arms, "C1", Gate, 2));
        first.Should().BeGreaterThan(0, "the gate's first attempt ended in a result with a cost");
        second.Should().BeGreaterThan(first, "a resume's result carries the session's cumulative cost");

        var metrics6 = await StagedAsync(ReaderNamed("metrics6.mjs"));
        var metrics = await StagedAsync(PinnedReaders.Metrics);
        var compare = await StagedAsync(ReaderNamed("compare.mjs"));

        metrics6.Json["C1"]!["steps"]![Gate]!["cost"]!.GetValue<double>().Should().Be(second, "metrics6.mjs takes the last result per session");
        var gateNode = metrics.Json["report"]![PinnedConsumer.Listed("C1")]!["steps"]![Gate];
        gateNode.Should().NotBeNull($"metrics.mjs lists C's {Gate}");
        var gate = gateNode!.AsArray();
        gate[0]!["cost"]!.GetValue<double>().Should().Be(first);
        gate[1]!["cost"]!.GetValue<double>().Should().Be(second);
        var counted = metrics.Json["report"]![PinnedConsumer.Listed("C1")]!["models"]!["claude-sonnet-5-5"]!["cost"]!.GetValue<double>();
        counted.Should().Be(first + second, "THE DEFECT, measured: metrics.mjs:140 sums every attempt's costUSD, so a resumed step is counted once per attempt (E2 §8 item 62)");
        counted.Should().NotBe(second, "the session really cost its last result");
        compare.Json["rows"]![0]!["steps"]![Gate]!["cost"]!.GetValue<double>().Should().Be(first + second, "compare.mjs:34-35 sums the attempts too");
        metrics.Json["report"]![PinnedConsumer.Listed("C1")]!["cost"].Should().BeNull("the arm's other attempts have no cost, so :144 sums NaN, which JSON writes as null");
    }

    [Fact]
    public void Every_checked_in_script_of_the_harness_is_run_or_excluded_by_name_with_its_reason()
    {
        var checkedIn = new[] { PinnedReaders.Harness, PinnedReaders.Part6 }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Cli.Repository.Root, folder)).Where(file => file.EndsWith(".mjs", StringComparison.Ordinal) || file.EndsWith(".js", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(Cli.Repository.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        checkedIn.Should().Contain([PinnedReaders.Metrics.RepositoryPath, PinnedReaders.Part6 + "metrics6.mjs"], "the scan finds the readers it is about");
        var run = PinnedReaders.Run.Select(reader => reader.RepositoryPath).ToList();
        run.Intersect(PinnedReaders.Excluded.Keys).Should().BeEmpty("a script is run or excluded, never both");
        var unlisted = checkedIn.Except(run).Except(PinnedReaders.Excluded.Keys).ToList();
        unlisted.Should().BeEmpty("every checked-in script is run or excluded by name with its reason - add it to PinnedReaders.Run or PinnedReaders.Excluded: " + string.Join(", ", unlisted));
        run.Concat(PinnedReaders.Excluded.Keys).Except(checkedIn).Should().BeEmpty("a script that is no longer checked in is a stale row");
        PinnedReaders.Excluded.Values.Should().OnlyContain(reason => reason.Length > 0);
    }
}
