using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Scouts;

/// <summary>The files one attempt leaves under the artefact root, <c>scouts/&lt;run&gt;/&lt;cell&gt;/a&lt;n&gt;/</c>.</summary>
public enum ScoutArtifactKind
{
    /// <summary><c>stream.ndjson</c> — the CLI's stdout, written LINE BY LINE as it arrives (§3.10), so a crash leaves what was paid for.</summary>
    Stream,

    /// <summary><c>stream.sha256</c> — the stream's seal, committed once the process has ended: its SHA-256 and length.</summary>
    StreamSeal,

    Stderr,

    /// <summary><c>argv.json</c> — the exact argv and the NAMES of the child's environment.</summary>
    Argv,

    /// <summary><c>prompt.txt</c> — the driver the main session read on stdin.</summary>
    Prompt,
}

/// <summary>The scouts' artefacts under the artefact root — the gate's root, checked outside every git checkout by
/// <see cref="FileSystemGateArtifactStore.Open"/>; the scouts add a <c>scouts/</c> tree beside the gate's <c>runs/</c> and the probes'
/// <c>probes/</c>. Every path is contained (<see cref="ArtifactContainment"/>: resolved on the real filesystem, links included).
/// <list type="bullet">
/// <item><b>The stream</b> is opened before the launch (<c>CreateNew</c> — an attempt's stream is never written twice) and every line is
/// appended and flushed the moment it is read. It is the only artefact not written through <see cref="ArtifactCommit"/>: it has to exist
/// WHILE the process runs.</item>
/// <item><b>The rest</b> — stderr, the launch record, the prompt and the stream's seal — go through <see cref="ArtifactCommit"/> once the
/// process has ended: stage → flush → hash → rename, committed once, never overwritten.</item>
/// <item><b>Recovery reads the stream</b> (<see cref="IScoutStreams"/>): the <c>total_cost_usd</c> of a <c>result</c> envelope that reached
/// the disk, or nothing — a stream with no result line is a launch whose cost is unknown.</item>
/// </list></summary>
public sealed class ScoutArtifacts(string artifactRoot) : IScoutStreams, IScoutAssessmentFiles
{
    public const string Folder = "scouts";

    private readonly string _root = Path.GetFullPath(artifactRoot);

    public string Root => _root;

    public static string FileName(ScoutArtifactKind kind) => kind switch
    {
        ScoutArtifactKind.Stream => "stream.ndjson",
        ScoutArtifactKind.StreamSeal => "stream.sha256",
        ScoutArtifactKind.Stderr => "stderr.txt",
        ScoutArtifactKind.Argv => "argv.json",
        _ => "prompt.txt",
    };

    /// <summary>The attempt's folder, relative to the artefact root, <c>/</c>-separated.</summary>
    public static string AttemptFolder(Guid runId, Guid cellId, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{Folder}/{runId:D}/{cellId:D}/a{number}");

    public string PathOf(ScoutAttempt attempt, ScoutArtifactKind kind) =>
        Path.Combine([_root, .. AttemptFolder(attempt.RunId, attempt.CellId, attempt.Number).Split('/'), FileName(kind)]);

    /// <summary>The stream file, created — refused when it exists already (an attempt is launched once) or resolves outside the root.</summary>
    public Outcome<ScoutStreamWriter> OpenStream(ScoutAttempt attempt)
    {
        var path = PathOf(attempt, ScoutArtifactKind.Stream);

        if (Contained(path) is Outcome<string>.Fail outside)
        {
            return Outcome<ScoutStreamWriter>.Failure(outside.Reason);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            return Outcome<ScoutStreamWriter>.Success(new ScoutStreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)));
        }
        catch (IOException ex)
        {
            return Outcome<ScoutStreamWriter>.Failure($"the stream of {attempt.Label} could not be created — {ex.Message}");
        }
    }

    public async Task<Outcome<CommittedBytes>> CommitAsync(ScoutAttempt attempt, ScoutArtifactKind kind, string text, CancellationToken cancellationToken)
    {
        var path = PathOf(attempt, kind);

        return Contained(path) is Outcome<string>.Ok
            ? (await ArtifactCommit.CommitAsync(path, Encoding.UTF8.GetBytes(text), ArtifactProbe.None, cancellationToken)).Match(
                Outcome<CommittedBytes>.Success,
                reason => Outcome<CommittedBytes>.Failure($"{FileName(kind)} of {attempt.Label} {reason}"))
            : Outcome<CommittedBytes>.Failure($"{FileName(kind)} of {attempt.Label} resolves outside the artefact root — a write through it is refused");
    }

    /// <summary>The stream's seal: its SHA-256 and length as they stand once the process has ended.</summary>
    public string Seal(ScoutAttempt attempt)
    {
        var path = PathOf(attempt, ScoutArtifactKind.Stream);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return string.Create(CultureInfo.InvariantCulture, $"{Convert.ToHexStringLower(SHA256.HashData(stream))}  {new FileInfo(path).Length}\n");
    }

    /// <summary>The stream as it stands on disk, or empty when none was ever written.</summary>
    public string StreamText(ScoutAttempt attempt)
    {
        var path = PathOf(attempt, ScoutArtifactKind.Stream);

        try
        {
            return File.Exists(path) ? ReadShared(path) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    public CapturedUsd SurvivingCost(ScoutAttempt attempt) =>
        ClaudeSubagents.Read(StreamText(attempt)).Envelope.TotalCostUsd;

    /// <summary>The empty MCP config the scout launch points <c>--mcp-config</c> at — written once under <c>scouts/</c>, read-only after.</summary>
    public Outcome<string> EmptyMcpConfig()
    {
        var path = Path.Combine(_root, Folder, "empty-mcp.json");

        if (File.Exists(path))
        {
            return string.Equals(File.ReadAllText(path), ScoutLaunch.EmptyMcpConfig, StringComparison.Ordinal)
                ? Outcome<string>.Success(path)
                : Outcome<string>.Failure($"{path} is not the empty MCP config any more — a scout would load whatever it now names; delete it and run again");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ArtifactCommit.Stage(path, Encoding.UTF8.GetBytes(ScoutLaunch.EmptyMcpConfig));
        return Outcome<string>.Success(path);
    }

    /// <summary>Where the pinned settings file lives — <c>scouts/pinned-settings.json</c> under the artefact root (S5).</summary>
    public string PinnedSettingsFile => Path.Combine(_root, Folder, ScoutPinnedSettings.FileName);

    /// <summary>The pinned settings file the scout launch points <c>--settings</c> at (S5): written ONCE from the operator's user settings —
    /// <c>effortLevel</c> and <c>modelSettings</c> only — and never rewritten. Found again, it must still hold only those two keys and still say
    /// what the user settings say now; otherwise the verb refuses and names what to do, because a run that silently started under another
    /// effort than the operator's would measure a condition nobody chose. (A run in progress is held to the file's HASH — the settings pin —
    /// by the pin reader, not to the user settings.)</summary>
    public Outcome<string> PinnedSettings(IReadOnlyList<string> userSettingsFiles)
    {
        var path = PinnedSettingsFile;
        var user = userSettingsFiles.Where(File.Exists).Select(File.ReadAllText).FirstOrDefault() ?? "{}";
        var projection = ScoutPinnedSettings.Project(user);

        if (projection is Outcome<string>.Fail unreadable)
        {
            return Outcome<string>.Failure($"the user settings could not be projected into the pinned settings file — {unreadable.Reason}");
        }

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ArtifactCommit.Stage(path, Encoding.UTF8.GetBytes(((Outcome<string>.Ok)projection).Value));
            return Outcome<string>.Success(path);
        }

        return Found(path, File.ReadAllText(path), user);
    }

    private static Outcome<string> Found(string path, string pinned, string user) =>
        ScoutPinnedSettings.Verify(pinned) switch
        {
            Outcome<string>.Fail fail => Outcome<string>.Failure($"{path}: {fail.Reason} — delete it and run again to pin the user settings afresh"),
            _ => ScoutPinnedSettings.Drift(pinned, user) is { Length: > 0 } drift
                ? Outcome<string>.Failure($"{path} no longer matches the user settings it was pinned from — {drift}; delete it to pin again, or restore the settings (a run in progress keeps its pin: resume it before re-pinning)")
                : Outcome<string>.Success(path),
        };

    /// <summary>The assessor's batch folder (S3): <c>scouts/&lt;run&gt;/assess/&lt;batch&gt;/</c> — fresh, contained, beside the attempts and never
    /// inside a task's tree, so the assessor reading the tree never meets a prompt, a schema or a blinding key.</summary>
    public Task<string> BeginBatchAsync(Guid runId, string batchId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_root, Folder, runId.ToString("D"), "assess", batchId);

        if (Contained(path) is Outcome<string>.Fail outside)
        {
            throw new IOException(outside.Reason);
        }

        Directory.CreateDirectory(path);
        return Task.FromResult(path);
    }

    public async Task WriteBatchFileAsync(string batchDirectory, string name, string text, CancellationToken cancellationToken)
    {
        var path = Path.Combine(batchDirectory, name);

        if (Contained(path) is Outcome<string>.Fail outside)
        {
            throw new IOException(outside.Reason);
        }

        await File.WriteAllTextAsync(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    private Outcome<string> Contained(string path) =>
        ArtifactContainment.Resolve(path) is Outcome<string>.Ok { Value: var full } && ArtifactContainment.IsWithin(full, _root)
            ? Outcome<string>.Success(full)
            : Outcome<string>.Failure($"{Path.GetRelativePath(_root, path)} resolves outside the artefact root — a link on the way leads elsewhere");

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>An attempt's stream file, open for the launch: each line appended and FLUSHED as it is written — a crash of the harness then
/// loses at most the line in flight. Disposed when the process has ended.</summary>
public sealed class ScoutStreamWriter(FileStream file) : IDisposable
{
    private readonly StreamWriter _writer = new(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    public void Line(string line)
    {
        _writer.Write(line);
        _writer.Write('\n');
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}
