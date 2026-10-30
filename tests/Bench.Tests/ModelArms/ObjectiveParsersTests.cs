using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The readers of the target's logs and of the CTRF report (<c>objective-checks.sh:33-47</c>, C13): pure functions over text, tested on the recorded lines of part 6's arm C1 (<c>Fixtures/model-arms/objective</c>, its
/// token replaced by <c>0123456789ab</c>). Two things in the fixtures are WRITTEN, not recorded, and say so: C1's run had no failing test, so <c>mtp.log</c> carries two <c>failed …</c> records written in the shape of its real
/// <c>skipped</c> records, and <c>node.log</c> carries two <c>✖</c> lines (one printed twice, as the node runner prints a failure inline and again in its summary) and three indented <c>✔</c> lines — C1's log holds no indented one,
/// and the shell's pattern allows them. The summary lines of <c>mtp.log</c> are C1's: <c>failed: 0</c> beside two failed records is the fixture's, and the shell never reads that line.
/// <para>
/// What this does NOT prove: that the shell's patterns match every line another version of the test runners prints (the readers port the shell's patterns, not the runners' grammars); the byte-for-byte agreement over a whole
/// report is <see cref="ObjectiveReportTests"/>'s.
/// </para></summary>
public sealed class ObjectiveParsersTests
{
    private static NodeTestLog NodeLog() => TsTestLog.Read(ModelArmsFixtures.Read("objective/node.log"));

    private static IReadOnlyList<CtrfTest> Ctrf() => CtrfScenarios.Tests(ModelArmsFixtures.Read("objective/ctrf.json")).Ok();

    private static string PassingOf(string name, string kind = "cs", IReadOnlyList<CtrfTest>? tests = null, NodeTestLog? node = null) =>
        CtrfScenarios.Passing(new Scenario("S", kind, name), tests ?? Ctrf(), node ?? NodeLog());

    private static ObjectiveFacts Failing(IReadOnlyList<string> mcpFailed, IReadOnlyList<string> tsFailed, Baseline baseline) =>
        ObjectiveFactsKit.Facts() with
        {
            SuiteLog = string.Join('\n', mcpFailed.Select(name => $"failed {ObjectiveFactsKit.Prefix}{name} (1ms)")),
            ExtensionLog = string.Join('\n', tsFailed.Select(name => $"✖ {name} (1.5ms)")),
            Baseline = baseline,
        };

    [Fact]
    public void The_mtp_summary_reads_total_succeeded_skipped_and_every_failed_name()
    {
        var summary = TestLogSummary.Read(ModelArmsFixtures.Read("objective/mtp.log"), ObjectiveFactsKit.Prefix);

        summary.Total.WasCaptured.Should().BeTrue();
        summary.Total.Value.Should().Be(13894);
        summary.Succeeded.Value.Should().Be(13777);
        summary.Skipped.Value.Should().Be(117);
        summary.Failed.Should().Equal("ScenarioTests.TheLadder_Climbs", "OtherTests.ASlowCase_TimesOut");
    }

    [Fact]
    public void The_node_log_reads_passed_failed_and_the_count()
    {
        var log = NodeLog();

        log.Tests.WasCaptured.Should().BeTrue();
        log.Tests.Value.Should().Be(6540, "the shell takes the FIRST `ℹ tests` line; the second says 4");
        log.Failed.Should().Equal("a failing node scenario", "an old flaky node test");
        log.Passed.Should().Contain("a nested node scenario", "a ✔ line that starts with spaces is a passed test");
        log.Passed.Should().Contain("a deeper nested node scenario").And.Contain("the Consultant tab names a row by its display name — in the strip, the picker and a stranded pick's sentence");
        log.Passed.Should().NotContain("a failing node scenario");
    }

    [Fact]
    public void Ctrf_decides_passed_failed_absent_and_mixed_per_scenario()
    {
        PassingOf("TheSpentVerdict_IsHopeless").Should().Be("passed", "its one test passed");
        PassingOf("TheLadder_Climbs").Should().Be("failed", "its one test failed");
        PassingOf("TheMissingCase_IsNeverRun").Should().Be("absent", "no test of the report names it");
        PassingOf("TheMixedCase_ReadsBoth").Should().Be("passed+failed", "the statuses of its tests, in the order they appear, joined by +");

        PassingOf("a nested node scenario", "node").Should().Be("passed", "a node scenario is read from the node log, never from the CTRF");
        PassingOf("a failing node scenario", "node").Should().Be("failed");
        PassingOf("a node scenario nobody ran", "node").Should().Be("absent");
    }

    [Fact]
    public void New_failures_exclude_the_baseline()
    {
        var baseline = ObjectiveFactsKit.BaselineOf(["B"], ["Y"]);

        var report = ObjectiveFactsKit.Parse(ObjectiveReport.Write(Failing(["A", "B"], ["X", "Y"], baseline)));

        ObjectiveFactsKit.Strings(report, "mcp", "failedNew").Should().Equal("A");
        ObjectiveFactsKit.Strings(report, "mcp", "failedBaseline").Should().Equal("B");
        ObjectiveFactsKit.Strings(report, "ts", "failedNew").Should().Equal("X");
    }

    [Fact]
    public void Changed_lines_split_into_tests_src_and_docs()
    {
        var rows = DiffStats.Rows(
            "10\t2\tsrc_mcp/tests/ATests.cs\n5\t1\tsrc_mcp/src/A.cs\n3\t0\tresearch/x.md\n-\t-\tsrc_vs_code/icon.png\n1\t1\ttools/b.mjs\n");

        var lines = DiffStats.Split(rows);

        lines.Tests.Should().Be(12);
        lines.Src.Should().Be(6);
        lines.Docs.Should().Be(3);
        rows.Select(r => r.File).Should().Equal("src_mcp/tests/ATests.cs", "src_mcp/src/A.cs", "research/x.md", "src_vs_code/icon.png", "tools/b.mjs");
        rows.Single(r => r.File == "src_vs_code/icon.png").Should().Be(new NumstatRow("src_vs_code/icon.png", 0, 0), "a binary row counts no line");
    }

    [Fact]
    public void A_total_the_log_does_not_carry_is_unavailable_never_zero()
    {
        var mtp = TestLogSummary.Read("the run printed no summary\n", ObjectiveFactsKit.Prefix);
        var node = TsTestLog.Read("✔ a test (1ms)\n");

        mtp.Total.WasCaptured.Should().BeFalse();
        mtp.Succeeded.WasCaptured.Should().BeFalse();
        mtp.Skipped.WasCaptured.Should().BeFalse();
        node.Tests.WasCaptured.Should().BeFalse("a node log without a `ℹ tests` line has no count");

        var report = ObjectiveReport.Write(ObjectiveFactsKit.Facts() with { SuiteLog = "the run printed no summary\n", ExtensionLog = "✔ a test (1ms)\n" });
        var parsed = ObjectiveFactsKit.Parse(report);
        ObjectiveFactsKit.Value(parsed, "mcp", "total").Should().Be("null");
        ObjectiveFactsKit.Value(parsed, "mcp", "passed").Should().Be("null");
        ObjectiveFactsKit.Value(parsed, "mcp", "skipped").Should().Be("null");
        ObjectiveFactsKit.Value(parsed, "ts", "total").Should().Be("null");
    }

    [Fact]
    public void A_scenario_name_is_a_whole_word_and_never_a_pattern()
    {
        IReadOnlyList<CtrfTest> tests =
        [
            new("Ns.FooBar", "passed"), new("Ns.Foo(x: 1)", "passed"), new("Ns.AxB", "passed"), new("Ns.Given a name (x)", "passed"), new("Ns.Hit+Run", "failed"),
        ];

        PassingOf("Foo", tests: tests).Should().Be("passed", "`Ns.Foo(x: 1)` holds Foo as a whole word");
        PassingOf("FooBa", tests: tests).Should().Be("absent", "FooBa is not a whole word of FooBar");
        PassingOf("A.B", tests: tests).Should().Be("absent", "the dot is a dot, not any character: AxB is not A.B");
        PassingOf("Given a name", tests: tests).Should().Be("passed", "a space in a name is matched as a space");
        PassingOf("Hit+Run", tests: tests).Should().Be("failed", "a plus in a name is a plus, not a repeat");
    }

    [Fact]
    public void A_ctrf_that_is_not_a_ctrf_object_is_refused_with_its_reason()
    {
        var refused = new[]
        {
            ("[]", "array"),
            ("{}", "results"),
            ("""{"results":{}}""", "tests"),
            ("""{"results":{"tests":[{"name":"A"}]}}""", "status"),
            ("""{"results":{"tests":[{"name":"A","status":"passed"}""", "valid JSON"),
        };

        foreach (var (text, why) in refused)
        {
            var outcome = CtrfScenarios.Tests(text);
            outcome.Should().BeOfType<Outcome<IReadOnlyList<CtrfTest>>.Fail>($"{text} is not a CTRF report");
            outcome.Reason().Should().Contain(why);
        }

        CtrfScenarios.Tests("""{"results":{"tests":[]}}""").Ok().Should().BeEmpty("a report of no tests is a report");

        var report = ObjectiveReport.Write(ObjectiveFactsKit.Facts() with { Ctrf = Outcome<string>.Success("[]"), Scenarios = [new Scenario("S1", "cs", "A")], Present = new Dictionary<string, bool> { ["S1"] = true } });
        ObjectiveFactsKit.Value(ObjectiveFactsKit.Parse(report), "scenarios", "S1", "passing").Should().Be("absent", "a report that is not a CTRF is read as no tests");
    }

    [Fact]
    public void A_line_ends_where_javascript_ends_it()
    {
        const string log = "progress\rfailed A (1ms)\u2028✖ X (1ms)\u2029failed B (2ms)\r\n  ✔ Z (3ms)\r\nℹ tests 7\r";

        TestLogSummary.Read(log, string.Empty).Failed.Should().Equal("A", "B");
        var node = TsTestLog.Read(log);
        node.Failed.Should().Equal("X");
        node.Passed.Should().Contain("Z");
        node.Tests.Value.Should().Be(7);
    }
}
