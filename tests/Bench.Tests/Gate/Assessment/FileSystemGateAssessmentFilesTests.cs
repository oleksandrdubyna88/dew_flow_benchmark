using System.Text;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Assessment;

/// <summary>The assessment's private files: the verdict log survives a killed append and two assessors side by side; what
/// the assessor is handed lives OUTSIDE the artefact root.</summary>
public sealed class FileSystemGateAssessmentFilesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly GateReviewerId Codex = GateReviewerId.Parse("codex-astra").Ok();

    /// <summary>An append killed mid-line leaves a fragment with no newline; the next append must not glue its first line
    /// onto it, or that line — a verdict the database committed — can never be joined to its text again.</summary>
    [Fact]
    public async Task A_line_appended_after_a_torn_one_still_reads_back()
    {
        using var root = GateStoreFixtures.NewRoot();
        using var files = new FileSystemGateAssessmentFiles(root.Path);
        await files.AppendVerdictLinesAsync(Codex, [Line("0000000a")], Ct);
        var log = Path.Combine(root.Path, FileSystemGateAssessmentFiles.Folder, "verdicts", "codex-astra.jsonl");
        await File.AppendAllTextAsync(log, "{\"id\":\"0000000b\",\"task\":\"cs", Ct);

        await files.AppendVerdictLinesAsync(Codex, [Line("0000000c"), Line("0000000d")], Ct);

        (await files.ReadVerdictLinesAsync(Ct)).Select(l => l.Id).Should().Equal(["0000000a", "0000000c", "0000000d"],
            "only the torn fragment is lost; the lines after it are whole");
    }

    /// <summary>Two assessors run side by side, and each pass reads every log at its start: a log held open for writing by
    /// one must still be readable by the other.</summary>
    [Fact]
    public async Task A_log_being_written_by_one_pass_can_be_read_by_another()
    {
        using var root = GateStoreFixtures.NewRoot();
        using var files = new FileSystemGateAssessmentFiles(root.Path);
        await files.AppendVerdictLinesAsync(Codex, [Line("0000000a")], Ct);
        var log = Path.Combine(root.Path, FileSystemGateAssessmentFiles.Folder, "verdicts", "codex-astra.jsonl");

        await using var writer = new FileStream(log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

        (await files.ReadVerdictLinesAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task What_the_assessor_is_handed_lies_outside_the_artefact_root_and_is_archived_into_it()
    {
        using var root = GateStoreFixtures.NewRoot();
        string batch;
        using (var files = new FileSystemGateAssessmentFiles(root.Path))
        {
            batch = await files.BeginBatchAsync("cs2-0000000a-a1", Ct);
            await files.WriteBatchFileAsync(batch, FindingAssessor.PromptFile, "the prompt", Ct);
            await files.ArchiveBatchAsync(batch, "cs2-0000000a-a1", Ct);

            Path.GetFullPath(batch).Should().NotStartWith(Path.GetFullPath(root.Path), "the key and every run's findings would be one directory away");
        }

        File.ReadAllText(Path.Combine(root.Path, FileSystemGateAssessmentFiles.Folder, "batches", "cs2-0000000a-a1", FindingAssessor.PromptFile), Encoding.UTF8)
            .Should().Be("the prompt", "the prompt as sent is kept with the verdicts it produced");
        Directory.Exists(batch).Should().BeFalse("the workspace goes when the pass does");
    }

    private static VerdictLine Line(string id) =>
        new(id, "cs2", "codex-astra", "b1", new string('7', 64), "supported", "high", "yes", "yes", "cs2:k", "none", "n", string.Empty, DateTimeOffset.UnixEpoch);
}
