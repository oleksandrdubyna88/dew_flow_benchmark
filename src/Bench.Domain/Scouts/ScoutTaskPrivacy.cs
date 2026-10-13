using System.Text.RegularExpressions;

namespace Bench.Domain.Scouts;

/// <summary>Whether a task's repository may be named in public (plan D10, §3.13). A PUBLIC task's committed records carry its repository,
/// P, S, statement, G and removals; a PRIVATE task's carry its label, its language, sizes, metrics, reason codes and the HMAC commitments
/// of its statement and G under the campaign key — never a url, a path, a statement, an identifier, a KEY FILES list or a claim.</summary>
public enum ScoutTaskPrivacy
{
    Public,
    Private,
}

/// <summary>The words of <see cref="ScoutTaskPrivacy"/> on the wire and in the operator files, and the shape rules a private task is held to:
/// its id IS its label (<c>p03</c> — a letter and digits, nothing that could spell a name), so every tree, artefact and log path carries the
/// label only; and it names its language (<c>PHP</c>, <c>C#</c>), which is the one thing about the repository the committed record says.</summary>
public static partial class ScoutTaskPrivacies
{
    public const string PublicWord = "public";

    public const string PrivateWord = "private";

    /// <summary>Up to four digits since E2 S8: the screened private pool holds about 1 080 admissible candidates, and every candidate's id IS
    /// its label.</summary>
    public const string LabelForms = "a private task's id is its label — 'p' and two to four digits (p01 … p9999), never a name";

    public const string LanguageForms = "a language is a word of letters, digits, '#', '+', '.' or '-', at most 32 characters, starting with a letter";

    [GeneratedRegex("^p[0-9]{2,4}$")]
    private static partial Regex Label();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9#+.-]{0,31}$")]
    private static partial Regex Language();

    public static string Word(ScoutTaskPrivacy privacy) => privacy == ScoutTaskPrivacy.Private ? PrivateWord : PublicWord;

    public static Outcome<ScoutTaskPrivacy> Parse(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        PublicWord or "" => Outcome<ScoutTaskPrivacy>.Success(ScoutTaskPrivacy.Public),
        PrivateWord => Outcome<ScoutTaskPrivacy>.Success(ScoutTaskPrivacy.Private),
        var other => Outcome<ScoutTaskPrivacy>.Failure($"'{other}' is not a privacy — {PublicWord} or {PrivateWord}"),
    };

    public static bool IsLabel(string id) => Label().IsMatch(id);

    public static bool IsLanguage(string language) => Language().IsMatch(language.Trim());

    /// <summary>Why <paramref name="id"/> and <paramref name="language"/> are not a legal pair for <paramref name="privacy"/>, or empty: a private task
    /// needs a label for an id and a language; a public task may name a language or not, in the same shape.</summary>
    public static string Refusal(ScoutTaskPrivacy privacy, string id, string language) =>
        (privacy == ScoutTaskPrivacy.Private && !IsLabel(id), language.Trim().Length == 0, IsLanguage(language)) switch
        {
            (true, _, _) => $"task {id}: {LabelForms}",
            (_, true, _) when privacy == ScoutTaskPrivacy.Private => $"task {id}: a private task names its language — {LanguageForms}",
            (_, false, false) => $"task {id}: '{language}' is not a language — {LanguageForms}",
            _ => string.Empty,
        };
}

/// <summary>The HMAC commitments a PRIVATE task carries in public (<see cref="ScoutCommitment"/>): of its statement and of its whole ground
/// truth, each under the campaign key — so the pre-registration stays provable while the plaintext stays local. A public task carries none.</summary>
public sealed record ScoutTaskCommitments(string Statement, string GroundTruth)
{
    public static ScoutTaskCommitments None { get; } = new(string.Empty, string.Empty);

    public bool IsPresent => Statement.Length > 0 && GroundTruth.Length > 0;
}
