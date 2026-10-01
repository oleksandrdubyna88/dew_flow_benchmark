using System.Text.RegularExpressions;
using Bench.Domain.Gate;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>One fact about one attempt, in three states. <see cref="NotCaptured"/> is the honest value whenever the
/// evidence is missing — a field the CLI did not print, a reader not yet confirmed on live output, a control that voided
/// the probe — and it is never rendered as <see cref="No"/>: a gap in instrumentation must not become a claim about the CLI.</summary>
public enum ProbeFact
{
    NotCaptured,
    Yes,
    No,
}

/// <summary>How the LAST attempt of a cell ended. <see cref="None"/> is a cell nobody has attempted.</summary>
public enum ProbeAttemptKind
{
    None,

    /// <summary>The CLI (or the product) answered; the facts were read off the answer and the transcript.</summary>
    Answered,

    /// <summary>A usage-error exit — the argv was refused by this build. Every fact is <i>not captured</i>; the exit code is kept.</summary>
    LaunchRefused,

    /// <summary>The wall ended the process. Every fact is <i>not captured</i>.</summary>
    TimedOut,

    /// <summary>The attempt ran and measured nothing the bench may use — an empty account (D8). The cell is handed back
    /// Pending with this kind on it; such an attempt is never SETTLED.</summary>
    Unmeasured,

    /// <summary>The CLI ran and exited non-zero with neither a usage error nor a quota marker — a crash, an API error, a
    /// model refusal. Every fact is <i>not captured</i>; the exit code and the stderr artefact say why (S2).</summary>
    Failed,
}

/// <summary>The one "why" a probe cell row may carry — an allow-listed WORD (D11), never a sentence.</summary>
public enum ProbeReason
{
    None,

    /// <summary>Handed back <see cref="Runs.Claimable.MaxAttempts"/> times; terminal.</summary>
    Abandoned,

    /// <summary>The subject's account was out when the attempt ran; the cell went back Pending.</summary>
    AccountOut,

    LaunchRefused,

    TimedOut,

    /// <summary>The CLI exited 0 and printed nothing.</summary>
    NoAnswer,
}

/// <summary>Which file of an attempt an artefact is.</summary>
public enum ProbeArtifactKind
{
    Answer,
    Stdout,
    Stderr,
}

/// <summary>What the database knows of an attempt's file: its path RELATIVE to the artefact root, the bytes' SHA-256 and
/// their length — never the bytes (D11). <see cref="ArtifactPath"/> is the gate's: relative, <c>/</c>-separated, never climbing.</summary>
public sealed partial record ProbeArtifact
{
    private ProbeArtifact(ProbeArtifactKind kind, ArtifactPath path, string sha256, long length)
    {
        Kind = kind;
        Path = path;
        Sha256 = sha256;
        Length = length;
    }

    public ProbeArtifactKind Kind { get; }

    public ArtifactPath Path { get; }

    public string Sha256 { get; }

    public long Length { get; }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex { get; }

    public static Outcome<ProbeArtifact> Of(ProbeArtifactKind kind, ArtifactPath path, string? sha256, long length)
    {
        var sha = (sha256 ?? string.Empty).Trim().ToLowerInvariant();

        var refusal = (Sha256Hex.IsMatch(sha), length >= 0) switch
        {
            (false, _) => $"'{sha}' is not a SHA-256 — an artefact ref hashes the committed bytes, 64 hex characters",
            (_, false) => $"an artefact's length is never negative — got {length}",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<ProbeArtifact>.Failure(refusal) : Outcome<ProbeArtifact>.Success(new ProbeArtifact(kind, path, sha, length));
    }

    /// <summary>Read back from a row: the path is re-parsed, so a hand-edited <c>..</c> is refused on read.</summary>
    public static Outcome<ProbeArtifact> Stored(string? kind, string? path, string? sha256, long length) =>
        Enum.TryParse<ProbeArtifactKind>(kind, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? ArtifactPath.Parse(path).Match(p => Of(parsed, p, sha256, length), Outcome<ProbeArtifact>.Failure)
            : Outcome<ProbeArtifact>.Failure($"'{kind}' is not an artefact kind");
}

/// <summary>Every fact one attempt produced, each in three states, beside the attempt's kind and exit code. Which facts a
/// probe fills is <see cref="ProbeVerdicts"/>' business; this is the shape the cell stores.</summary>
public sealed record ProbeFacts(
    ProbeAttemptKind Kind,
    CapturedCount ExitCode,
    ProbeFact CanaryRead,
    ProbeFact ReadAttempted,
    ProbeFact AnswerCurrent,
    ProbeFact ToolEvidence,
    ProbeFact Reachable,
    ProbeFact AccountOut)
{
    /// <summary>A cell nobody has attempted.</summary>
    public static ProbeFacts None { get; } = NothingCaptured(ProbeAttemptKind.None, CapturedCount.Unavailable("not attempted"));

    /// <summary>Every fact <i>not captured</i> — a refused launch, a timeout, an unmeasured attempt.</summary>
    public static ProbeFacts NothingCaptured(ProbeAttemptKind kind, CapturedCount exitCode) =>
        new(kind, exitCode, ProbeFact.NotCaptured, ProbeFact.NotCaptured, ProbeFact.NotCaptured, ProbeFact.NotCaptured, ProbeFact.NotCaptured, ProbeFact.NotCaptured);

    /// <summary>An answered attempt, every fact <i>not captured</i> until a reader fills it.</summary>
    public static ProbeFacts Answered(CapturedCount exitCode) => NothingCaptured(ProbeAttemptKind.Answered, exitCode);
}

/// <summary>What one claimed cell settles with: its facts and the artefacts the attempt committed.</summary>
public sealed record ProbeSettlement(ProbeFacts Facts, IReadOnlyList<ProbeArtifact> Artifacts);

/// <summary>The two random tokens of one attempt — the IN token in <c>cwd/inside.txt</c>, the OUT token in
/// <c>outside/canary.txt</c>. Fresh per attempt (D7), so a token that leaks is one no later attempt uses.</summary>
public sealed record ProbeTokens
{
    private ProbeTokens(string inside, string outside)
    {
        Inside = inside;
        Outside = outside;
    }

    public string Inside { get; }

    public string Outside { get; }

    public static Outcome<ProbeTokens> Of(string? inside, string? outside)
    {
        var a = (inside ?? string.Empty).Trim();
        var b = (outside ?? string.Empty).Trim();

        var refusal = (TooShort(a, b), Nested(a, b)) switch
        {
            (true, _) => "a canary token is at least 8 characters — a short token is found by accident",
            (_, true) => "the two canary tokens must not contain each other — an answer quoting one would read as the other",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<ProbeTokens>.Failure(refusal) : Outcome<ProbeTokens>.Success(new ProbeTokens(a, b));
    }

    private static bool TooShort(string a, string b) => a.Length < 8 || b.Length < 8;

    private static bool Nested(string a, string b) => a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
}
