using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>Which bytes of the product a run measured.
/// <para>
/// SHA-256 of the binary, the text of <c>--version</c>, and — when the binary sits under a git checkout — the
/// short sha and how many tracked files of the product's own source tree were dirty. Stored on every cell at
/// claim time, never read once per run: a claimed cell finishes under the pin it started with, and a campaign
/// that finds a different binary is REFUSED naming both. The calibration this benchmark replaces crossed that
/// line once (the branch was rebased mid-measurement; phase-1 and phase-2 runs record different shas), which
/// is precisely the case a report must be able to see.
/// </para>
/// <para>
/// Reading the pin off a binary is an adapter's job (<c>ProductPin.Read</c>, E3); this type holds the value
/// and the one decision — may this binary continue that campaign.
/// </para></summary>
public sealed partial record ProductPin
{
    private ProductPin(string binarySha256, string versionText, string gitSha, CapturedCount dirtyFiles, string checkedTree)
    {
        BinarySha256 = binarySha256;
        VersionText = versionText;
        GitSha = gitSha;
        DirtyFiles = dirtyFiles;
        CheckedTree = checkedTree;
    }

    /// <summary>Lower-case hex of the binary's bytes. Empty when nothing was hashed — an imported run names a
    /// sha the other harness recorded and has no binary to hash — and <see cref="BinaryHashed"/> says so.</summary>
    public string BinarySha256 { get; }

    public string VersionText { get; }

    /// <summary>Empty when the binary sits under no checkout. Not a failure: a release build has no tree.</summary>
    public string GitSha { get; }

    /// <summary>Tracked files modified under <see cref="CheckedTree"/>. <i>Not captured</i> when there is no
    /// checkout to ask — never a zero, because a zero reads as "clean".</summary>
    public CapturedCount DirtyFiles { get; }

    /// <summary>The tree the dirty check covered, relative to the checkout — the product's own project directory,
    /// or the whole checkout when no project was found above the binary. Empty when there was no checkout.
    /// Carried because "dirty" without "where" cannot be re-checked.</summary>
    public string CheckedTree { get; }

    /// <summary>The value a cell carries before it is claimed. Not a null: "not yet pinned" is a state.</summary>
    public static ProductPin None { get; } = new(
        string.Empty, string.Empty, string.Empty, CapturedCount.Unavailable("not yet pinned"), string.Empty);

    public bool BinaryHashed => BinarySha256.Length > 0;

    public bool UnderCheckout => GitSha.Length > 0;

    /// <summary>A pin carried over from another harness: no binary was hashed, and the version text names the population
    /// that harness declared (<see cref="Imported"/>).</summary>
    public bool IsImported => !BinaryHashed && VersionText.StartsWith(ImportedPrefix, StringComparison.Ordinal);

    public bool IsPinned => BinaryHashed || UnderCheckout || IsImported;

    /// <summary>How every imported pin's version text starts — the one spelling <see cref="IsImported"/> reads back.</summary>
    public const string ImportedPrefix = "imported from ";

    private const string ImportedSuffix = " — binary not hashed";

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex { get; }

    [GeneratedRegex("^[0-9a-f]{7,40}$")]
    private static partial Regex GitShaHex { get; }

    /// <summary>A pin read off a real binary. The git fields are empty for a binary outside any checkout, and
    /// the dirty count is then <i>not captured</i> rather than zero.</summary>
    public static Outcome<ProductPin> Hashed(
        string? binarySha256, string? versionText, string? gitSha, CapturedCount dirtyFiles, string? checkedTree)
    {
        var sha = (binarySha256 ?? string.Empty).Trim().ToLowerInvariant();
        var version = (versionText ?? string.Empty).Trim();
        var git = (gitSha ?? string.Empty).Trim().ToLowerInvariant();

        var refusal = Refuse(sha, version, git);

        return refusal.Length > 0
            ? Outcome<ProductPin>.Failure(refusal)
            : Outcome<ProductPin>.Success(new ProductPin(sha, version, git, dirtyFiles, (checkedTree ?? string.Empty).Trim()));
    }

    /// <summary>A pin carried over from another harness's record: the sha it wrote down (when it wrote one), no binary to
    /// hash, and the POPULATION that harness compared its runs within — <c>calib-py phase 2</c>. The population, not the
    /// sha, is the version text, because the version text is what a <see cref="GateScope"/> compares: the other harness
    /// folded the product commits it moved across into one comparison, and a scope per commit would split its table into
    /// pieces nobody can hold against it. The sha stays on <see cref="GitSha"/>, per cell. Such a pin never
    /// <see cref="Matches"/> anything — nothing proves the bytes were the same.</summary>
    public static Outcome<ProductPin> Imported(string? gitSha, CapturedCount dirtyFiles, string? population)
    {
        var git = (gitSha ?? string.Empty).Trim().ToLowerInvariant();
        var label = (population ?? string.Empty).Trim();

        var refusal = (label.Length, git.Length == 0 || GitShaHex.IsMatch(git)) switch
        {
            (0, _) => "an imported pin names the population the other harness compared its runs within — got none",
            (_, false) => $"'{git}' is not a git sha — an imported pin carries the sha the other harness recorded, 7 to 40 hex characters, or none",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ProductPin>.Failure(refusal)
            : Outcome<ProductPin>.Success(new ProductPin(string.Empty, ImportedPrefix + label + ImportedSuffix, git, dirtyFiles, string.Empty));
    }

    /// <summary>An imported pin read back from its stored columns: the population is taken out of the stored version
    /// text, so a row reads back as exactly the pin that was written.</summary>
    public static Outcome<ProductPin> ImportedStored(string? versionText, string? gitSha, CapturedCount dirtyFiles)
    {
        var text = (versionText ?? string.Empty).Trim();

        return text.StartsWith(ImportedPrefix, StringComparison.Ordinal) && text.EndsWith(ImportedSuffix, StringComparison.Ordinal)
            ? Imported(gitSha, dirtyFiles, text[ImportedPrefix.Length..^ImportedSuffix.Length])
            : Outcome<ProductPin>.Failure($"'{text}' is not an imported pin's version text");
    }

    /// <summary>Whether two pins name the same bytes. Only a hashed binary can match: two pins that were
    /// never hashed are not evidence of anything, however alike their version texts.</summary>
    public bool Matches(ProductPin other) =>
        BinaryHashed && other.BinaryHashed && string.Equals(BinarySha256, other.BinarySha256, StringComparison.Ordinal);

    /// <summary>May <paramref name="current"/> continue a campaign pinned to <paramref name="campaign"/>? Refused
    /// naming both when the product moved, so the operator can see what changed rather than that something did.
    /// <c>--allow-product-change</c> is answered by the caller starting a NEW scope, never by relaxing this.</summary>
    public static Outcome<ProductPin> Continue(ProductPin campaign, ProductPin current) =>
        campaign.Matches(current)
            ? Outcome<ProductPin>.Success(current)
            : Outcome<ProductPin>.Failure(
                $"the product moved: the campaign is pinned to {campaign.Short} ({campaign.VersionText}) and this binary is "
                + $"{current.Short} ({current.VersionText}) — pass --allow-product-change to measure it as a new scope; "
                + "a changed product never extends the old one");

    public string Short => BinaryHashed ? HashText.Short(BinarySha256) : "unhashed";

    public string Describe =>
        $"{Short} · {VersionText}"
        + (UnderCheckout ? $" · git {GitSha}, {DirtyText} in {CheckedTreeOrWhole}" : " · no checkout above the binary");

    /// <summary>The dirty count in words — "1 dirty file(s)", or why it was not captured.</summary>
    public string DirtyText =>
        DirtyFiles.WasCaptured ? $"{DirtyFiles.Value} dirty file(s)" : $"dirty count not captured ({DirtyFiles.Reason})";

    private string CheckedTreeOrWhole => CheckedTree.Length > 0 ? CheckedTree : "the whole checkout";

    private static string Refuse(string sha, string version, string git) =>
        (Sha256Hex.IsMatch(sha), version.Length > 0, git.Length == 0 || GitShaHex.IsMatch(git)) switch
        {
            (false, _, _) => $"'{sha}' is not a SHA-256 — a product pin hashes the binary's bytes, 64 hex characters",
            (_, false, _) => "a product pin needs the text of --version — a binary that answered nothing is not pinned",
            (_, _, false) => $"'{git}' is not a git sha — 7 to 40 hex characters, or empty for a binary outside any checkout",
            _ => string.Empty,
        };
}
