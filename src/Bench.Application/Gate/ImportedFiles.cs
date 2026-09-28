using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>A source run directory, read for copying: every file whose name an artefact path can carry, under
/// <c>source/</c>, classed by what it is; the rest counted, never silently dropped.</summary>
public sealed record CopiedFiles(IReadOnlyList<ImportFile> Files, int Skipped);

/// <summary>How an imported run directory lands in the artefact root.</summary>
public static class ImportedFiles
{
    public const string SourceFolder = "source";

    public static async Task<CopiedFiles> CopyAsync(IImportSource source, string directory, CancellationToken cancellationToken)
    {
        var files = new List<ImportFile>();
        var skipped = 0;

        foreach (var relative in await source.FilesUnderAsync(directory, cancellationToken))
        {
            var bytes = ArtifactPath.Parse($"{SourceFolder}/{relative}") is Outcome<ArtifactPath>.Ok
                ? await source.ReadBytesAsync($"{directory}/{relative}", cancellationToken)
                : Outcome<byte[]>.Failure("a name outside the artefact path alphabet");

            if (bytes is Outcome<byte[]>.Ok ok)
            {
                files.Add(new ImportFile($"{SourceFolder}/{relative}", ClassOf(relative), ok.Value));
                continue;
            }

            skipped++;
        }

        return new CopiedFiles(files, skipped);
    }

    /// <summary>The class of a copied file, by the other harness's own layout — its reply, its request, the server's stderr,
    /// the ledger, the shim's prompt and answer files, the tap's bodies and facts; anything else is <c>Other</c>.</summary>
    public static ArtifactClass ClassOf(string relative) => relative switch
    {
        "reply.json" => ArtifactClass.Reply,
        "request.json" => ArtifactClass.Request,
        "mcp-stderr.log" => ArtifactClass.Stderr,
        "usage.jsonl" => ArtifactClass.Ledger,
        _ when relative.StartsWith("answers/", StringComparison.Ordinal) => relative.EndsWith(".prompt", StringComparison.Ordinal) ? ArtifactClass.Prompt : ArtifactClass.Answer,
        _ when relative.StartsWith("tap/", StringComparison.Ordinal) => TapClass(relative),
        _ => ArtifactClass.Other,
    };

    /// <summary>The SHA-256 of the product's turn-1 prompt — the first prompt file the shim wrote, in the name order the
    /// other harness numbered them — the same fact a native cell's prompt hash is; empty when the run left none.</summary>
    public static string TurnOnePromptHash(IReadOnlyList<ImportFile> files) =>
        files.Where(f => f.Class == ArtifactClass.Prompt).OrderBy(f => f.RelativePath, StringComparer.Ordinal).FirstOrDefault() is { } first
            ? GateImportWriter.Sha(first.Bytes)
            : string.Empty;

    private static ArtifactClass TapClass(string relative) => relative switch
    {
        _ when relative.EndsWith(".request.json", StringComparison.Ordinal) => ArtifactClass.TapRequest,
        _ when relative.EndsWith(".response.json", StringComparison.Ordinal) => ArtifactClass.TapResponse,
        _ => ArtifactClass.TapFacts,
    };
}
