using Bench.Application;
using Bench.Cli;
using Bench.Domain.Telemetry;
using Bench.Tests.Telemetry;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The ingest verb's contract, over a store the test owns — the exit codes and the file
/// handling, without a database. What the database guarantees is asserted separately, against a real
/// one.</summary>
public sealed class TelemetryCommandTests
{
    [Fact]
    public async Task An_empty_spool_is_a_pass_that_says_it_ingested_nothing()
    {
        using var spool = new TempSpool();

        var (code, output, _) = await Run(new FakeStore(), "telemetry", "ingest", "--spool", spool.Path);

        // Nothing to do is not a failure and is not a regression — it is the normal state of a spool
        // that has already been drained.
        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("nothing to ingest");
    }

    [Fact]
    public async Task A_missing_spool_directory_is_an_environment_problem_not_a_regression()
    {
        var (code, _, error) = await Run(new FakeStore(), "telemetry", "ingest", "--spool", "does-not-exist");

        code.Should().Be(ExitCodes.Environment);
        code.Should().NotBe(ExitCodes.Regression, "a harness that cannot read its input has measured nothing");
        error.Should().Contain("spool directory not found");
    }

    [Fact]
    public async Task A_spool_file_is_ingested_and_then_marked_so_it_is_not_read_twice()
    {
        using var spool = new TempSpool(Fixture.Text);
        var store = new FakeStore();

        var (code, output, _) = await Run(store, "telemetry", "ingest", "--spool", spool.Path);

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("ingested 3");
        store.Received.Should().HaveCount(3);

        // Renamed rather than deleted: until somebody says otherwise this file is the only copy of
        // that data, and a benchmark is not the component that should decide to destroy it.
        Directory.GetFiles(spool.Path, "*.jsonl", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(spool.Path, "*" + TelemetryCommand.IngestedSuffix, SearchOption.AllDirectories)
            .Should().ContainSingle();
    }

    [Fact]
    public async Task A_line_this_build_cannot_read_is_a_regression_rather_than_a_silent_pass()
    {
        using var spool = new TempSpool(Fixture.Lines[0] + "\n" + """{"schema":"telemetry/v9"}""");

        var (code, output, _) = await Run(new FakeStore(), "telemetry", "ingest", "--spool", spool.Path);

        // The readable line still lands — a refusal is per line. But the command must not exit 0: an
        // emitter writing a version this build cannot read is exactly the news worth acting on, and a
        // green exit is how it would go unnoticed for a month.
        code.Should().Be(ExitCodes.Regression);
        output.Should().Contain("ingested 1").And.Contain("refused 1").And.Contain("telemetry/v9");
    }

    [Fact]
    public async Task A_report_over_an_empty_store_is_no_report_rather_than_a_pass()
    {
        var (code, output, _) = await Run(new FakeStore(), "telemetry", "report");

        // Distinct from pass on purpose: nothing was measured, and reading that as success is how an
        // empty database becomes "no problems found".
        code.Should().Be(ExitCodes.NoReport);
        output.Should().Contain("no telemetry stored");
    }

    [Fact]
    public async Task A_report_names_the_caller_key_and_marks_what_was_not_captured()
    {
        var store = new FakeStore
        {
            Totals = [new ToolTelemetryTotals("k", "rt_read_local_file", "claude-code/?/stdio", 3, 1, 1, 1, 13.4, 20.0)],
        };

        var (code, output, _) = await Run(store, "telemetry", "report");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("rt_read_local_file").And.Contain("claude-code/?/stdio");
        output.Should().Contain("'?' = not captured", "an unknown must be legible as an unknown, not read as a name");
        output.Should().Contain(ToolTelemetry.ContractVersion);
    }

    [Fact]
    public async Task An_action_that_is_not_a_verb_here_is_a_configuration_problem()
    {
        var (code, _, error) = await Run(new FakeStore(), "telemetry", "inject", "--spool", ".");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("unknown telemetry action 'inject'");
    }

    [Fact]
    public async Task Telemetry_with_no_action_says_which_actions_exist()
    {
        var (code, _, error) = await Run(new FakeStore(), "telemetry");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("ingest").And.Contain("report");
    }

    [Fact]
    public void The_verb_without_a_database_refuses_rather_than_guessing_at_localhost()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = Program.Run(["telemetry", "report", "--connection", ""], output, error);

        // A default connection string would silently write a benchmark's data into whatever database
        // happened to be listening on this machine — of which there are several.
        code.Should().Be(ExitCodes.Environment);
        error.ToString().Should().Contain("no database");
    }

    private static async Task<(int Code, string Output, string Error)> Run(
        ITelemetryStore store, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await TelemetryCommand.RunAsync(CommandLine.Parse(args), store, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempSpool : IDisposable
    {
        public TempSpool(string? contents = null)
        {
            Path = Directory.CreateTempSubdirectory("bench-spool").FullName;
            if (contents is not null)
            {
                var day = Directory.CreateDirectory(System.IO.Path.Combine(Path, "2026-08-15"));
                File.WriteAllText(System.IO.Path.Combine(day.FullName, "mcp-09-30-00-1234.jsonl"), contents);
            }
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class FakeStore : ITelemetryStore
    {
        public List<ToolTelemetry> Received { get; } = [];

        public IReadOnlyList<ToolTelemetryTotals> Totals { get; init; } = [];

        public Task<IngestReport> AppendAsync(
            IReadOnlyList<ToolTelemetry> records, CancellationToken cancellationToken)
        {
            Received.AddRange(records);
            return Task.FromResult(new IngestReport(records.Count, 0, 0, []));
        }

        public Task<IReadOnlyList<ToolTelemetryTotals>> TotalsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Totals);
    }
}
