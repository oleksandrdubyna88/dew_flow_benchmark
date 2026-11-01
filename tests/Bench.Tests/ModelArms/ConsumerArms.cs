using System.Text.Json.Nodes;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;
using static Bench.Tests.ModelArms.RunArmKit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The fake arms of E6.S1 (C27): ONE experiment root holding an arm of each scheme — C1, CR1, CHH1 and SRS1 — each MADE by the module's own <c>make</c> over a scratch <see cref="TargetBench"/> target, RUN by its own <c>run-arm</c> against the fake CLI (one arm at a time, in the order of
/// <see cref="Arms"/>) and CHECKED by its own <c>checks</c> through the gate with the bench's recording shims. Built once per test class; the tests read it and never write it.
/// <para>
/// What each arm was made to leave, so a reader has something to report: C1's blocked gate (a resumed session, costs 1.25 then a cumulative 2.00, one coai round with one finding accepted) and its one review verdict that is not <c>ready</c>; CR1's limited <c>2-hreview</c> resumed; CHH1's interrupted <c>2-hA</c> (exit 5, then
/// <c>run-arm</c> again); SRS1's five tasks in two batches, each task's file one meaningful line. Every arm's FIRST step runs on a second model id with a pause of 1.5 s in the fake, so its <c>wall_s</c> is not 0 and a wall assertion cannot pass on zeros. Every arm ends with <c>arm-done</c>; an arm that does not fails the
/// construction naming it.
/// </para>
/// <para>
/// The class is public only because xUnit builds a public test class from public fixtures; every member is internal.
/// </para>
/// What this does NOT prove: a real claude's streams (the fake replays recorded shapes), a real multi-hour task's diff, anything off Linux.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ConsumerArms : IAsyncLifetime
{
    /// <summary>The line every SRS task's file holds, shell text with <c>$id</c>: one meaningful line (<c>t-survival.mjs:16</c> counts a trimmed line longer than 3 that is not punctuation alone).</summary>
    internal const string TaskLine = "the coder of $id wrote this line";

    /// <summary>The pause of the slow step's fake, per call.</summary>
    internal const int SlowMilliseconds = 1500;

    internal static readonly IReadOnlyList<(string Arm, string Scheme)> Arms = [("C1", "C"), ("CR1", "CR"), ("CHH1", "CHH"), ("SRS1", "SRS")];

    private const string ReviewVerdict = """{"verdict":"changes-requested","findings":[{"severity":"major","title":"consumers"}]}""";

    private SrsKit? _kit;

    internal ExperimentRoot Root => _kit?.Root ?? throw new InvalidOperationException("the fake arms were not built (a platform that skips them)");

    internal ArmPaths PathsOf(string arm) => Root.PathsOf(arm);

    /// <summary><c>target.base</c>: the commit <c>t-survival.mjs</c>'s literal is rewritten to.</summary>
    internal string Base => Root.ExperimentOf().Target.Base.Value;

    /// <summary>Every call the fake logged, the slow step's included.</summary>
    internal IReadOnlyList<FakeCliCall> Calls => _kit!.Fake.Calls();

    /// <summary>The fake's one model id: every step but the slow one runs on it.</summary>
    internal string MainModel => _kit!.Fake.Model;

    /// <summary>The second model id the slow step of each arm runs on. It does not START with the main id: the fake numbers a model's calls by the files whose name starts <c>call-&lt;model&gt;-</c>, and an id that did would be counted in the main model's numbering.</summary>
    internal string SlowModel => "slow-" + MainModel;

    /// <summary>The step of <paramref name="arm"/> that runs on the slow model: its first.</summary>
    internal static string SlowStepOf(string scheme) => scheme == "SRS" ? "1-split" : "1-code";

    public async ValueTask InitializeAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var made = await MakeKit.CreateAsync([.. Arms.Select(each => each.Arm)]);
        try
        {
            made.Root.Edit(top => top["runs"] = JsonNode.Parse("""{"consumers":{"rounds":[["C1","CR1","CHH1","SRS1"]]}}"""));
            var code = await made.MakeAsync();
            code.Code.Should().Be(0, "the four arms are made by make itself: " + code.Everything);
            _kit = SrsKit.Over(made, TaskLine);
        }
        catch
        {
            made.Dispose();
            throw;
        }

        await LimitsAreTheSmallestAdmittedAsync();
        foreach (var (arm, scheme) in Arms)
        {
            await BuildAsync(arm, scheme);
        }
    }

    /// <summary>The waits are the smallest <see cref="ExperimentFile"/> admits (read from its refusal of a zero: a wait must be positive), so the arms spend seconds and not minutes - and <c>validate</c> accepts the experiment so limited.</summary>
    private async Task LimitsAreTheSmallestAdmittedAsync()
    {
        foreach (var member in new[] { "waitSeconds", "gateWaitSeconds" })
        {
            SetLimit(member, 0);
            var refusal = Record.Exception(() => Root.ExperimentOf());
            refusal.Should().NotBeNull($"limits.{member} of 0 must be refused, or 1 is not the smallest admitted");
            refusal!.Message.Should().Contain(member);
            SetLimit(member, 1);
        }

        var validated = new StringWriter();
        var error = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["validate", "--experiment", Root.Root], validated, error, Root.Environment, Ct);
        code.Should().Be(0, "validate accepts the experiment with its smallest waits: " + error + validated);
    }

    private void SetLimit(string member, int seconds) => Root.Edit(top => top["limits"]![member] = seconds);

    public ValueTask DisposeAsync()
    {
        _kit?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task BuildAsync(string arm, string scheme)
    {
        UseModels(scheme);
        var run = await RunSchemeAsync(arm, scheme);
        run.Code.Should().Be(0, $"{arm} must finish for the readers to have a whole arm: " + run.Error + run.Output);
        File.Delete(Path.Combine(Root.Root, "pids", $"{arm}.pid")); // the in-process runner wrote its own pid, which is this test host: checks reads a live pid as a live runner
        var checks = await ChecksKit.RunAsync(Root, arm);
        checks.Code.Should().Be(0, $"checks {arm} writes the objective.json the readers read: " + string.Join('\n', checks.Lines));
        Root.ArmDones(arm).Should().ContainSingle($"{arm} ends with arm-done");
        File.Exists(PathsOf(arm).Objective).Should().BeTrue($"{arm} has its objective.json");
    }

    /// <summary>Every step runs on the main model but the arm's first, which runs on the slow one (steps are keyed by kind, shared by the arms, so this is set before each arm).</summary>
    private void UseModels(string scheme)
    {
        var first = scheme == "SRS" ? "split" : "code";
        Root.Edit(top =>
        {
            foreach (var step in top["steps"]!.AsObject())
            {
                step.Value!["model"] = step.Key == first ? SlowModel : MainModel;
            }
        });
    }

    private async Task<ArmRun> RunSchemeAsync(string arm, string scheme)
    {
        var paths = PathsOf(arm);
        var next = MainCalls() + 1;
        switch (scheme)
        {
            case "C":
                Directory.CreateDirectory(paths.Notes);
                File.WriteAllText(Path.Combine(paths.Notes, "gate.json"), """{"status":"blocked","why":"consumers"}""");
                await CommitAsync(paths, "src_mcp/tests/ATests.cs", "class ATests { void A_scenario() { } }\n");
                var review = SuccessEndingIn(Root, "review-changes.jsonl", ReviewVerdict);
                Script((next, review), (next + 2, Fixture("stream-gate-coai-round.jsonl")), (next + 3, Fixture("result-success-cost-2.jsonl")));
                return await RunAsync(Root, arm);
            case "CR":
                Script((next, ModelArmsFixtures.Path("result-429-usage-limit-reached.jsonl")));
                return await RunAsync(Root, arm);
            case "CHH":
                Script((next, ModelArmsFixtures.Path("stream-no-result.jsonl")));
                var first = await RunAsync(Root, arm);
                first.Code.Should().Be(5, "the interrupted 2-hA is exit 5, never repeated by run-arm itself: " + first.Error + first.Output);
                return await RunAsync(Root, arm);
            default:
                _kit!.Plan(_kit.Five());
                Script();
                return await RunAsync(Root, arm);
        }
    }

    private static string Fixture(string name) => ModelArmsFixtures.Path(System.IO.Path.Combine("consumers", name));

    /// <summary>The number of calls the fake has logged for the MAIN model: the next call of it is this plus one. Counted as the fake counts, by the files named for the model.</summary>
    private int MainCalls() =>
        Directory.Exists(_kit!.Fake.EventsDir) ? Directory.EnumerateFiles(_kit.Fake.EventsDir, $"call-{MainModel}-*.json").Count() : 0;

    /// <summary>The fake replays a success on every call but the numbered ones; the slow model has its pause and none of the numbered replays (its own numbering starts at 1 too).</summary>
    private void Script(params (int Call, string File)[] byCall)
    {
        var script = new JsonObject { ["stdoutFile"] = ModelArmsFixtures.Path("result-success.jsonl") };
        if (byCall.Length > 0)
        {
            script["stdoutByCall"] = new JsonObject(byCall.Select(each => KeyValuePair.Create<string, JsonNode?>(each.Call.ToString(System.Globalization.CultureInfo.InvariantCulture), each.File)));
        }

        script["subjects"] = new JsonObject { [SlowModel] = new JsonObject { ["delayMs"] = SlowMilliseconds, ["stdoutByCall"] = new JsonObject() } };
        _kit!.Fake.Rewrite(ExperimentRoot.Script(script));
    }

    private static async Task CommitAsync(ArmPaths paths, string relative, string text)
    {
        var file = System.IO.Path.Combine(paths.CloneFolder, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, text, Ct);
        await TargetBench.GitAsync(paths.CloneFolder, "add", "--", relative);
        await TargetBench.GitAsync(paths.CloneFolder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "the arm's work");
    }
}
