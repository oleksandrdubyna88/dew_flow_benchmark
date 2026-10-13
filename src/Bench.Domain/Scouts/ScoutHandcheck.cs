using System.Globalization;
using System.Text;

namespace Bench.Domain.Scouts;

/// <summary>One (output, G file) pair for the matcher's hand-check (§3.7): a scored cell's team output — its KEY FILES exactly as the scouts
/// wrote them — one file of G, and what the matcher said: found or not.</summary>
public sealed record HandcheckPair(Guid CellId, string Task, string GroundTruthFile, IReadOnlyList<string> KeyFilesAsWritten, bool Found)
{
    public string DrawKey => $"pair|{CellId:D}|{GroundTruthFile}";
}

/// <summary>One assessed claim for the assessor's hand-check: the claim as the scout wrote it and the verdict the assessor gave.</summary>
public sealed record HandcheckClaim(ScoutClaimKey Key, string Task, ScoutClaim Claim, ScoutClaimVerdict Verdict)
{
    public string DrawKey => $"claim|{Key.CellId:D}|{Key.Ordinal}|{Key.Index}";
}

/// <summary>What one draw took: N found pairs, N not-found pairs and N claim verdicts, and the seed that took them.</summary>
public sealed record HandcheckDraw(int Seed, int PerKind, IReadOnlyList<HandcheckPair> Found, IReadOnlyList<HandcheckPair> NotFound, IReadOnlyList<HandcheckClaim> Claims);

/// <summary>Which tasks of a draw are PRIVATE, and the committer their items are shown through (D10, E2 S4): a private item names its task's
/// label and the commitment of its G file or its claim, never the path or the claim's words — those go to the local companion file.</summary>
/// <param name="Committer">Required only when <paramref name="PrivateTasks"/> is not empty; <see cref="AllPublic"/> carries none.</param>
public sealed record ScoutHandcheckPrivacy(IReadOnlySet<string> PrivateTasks, ScoutCommitter? Committer)
{
    public static ScoutHandcheckPrivacy AllPublic { get; } = new(new HashSet<string>(StringComparer.Ordinal), null);

    public bool IsPrivate(string task) => PrivateTasks.Contains(task);

    /// <summary>The committer a private item is shown through; a draw that holds a private task and no committer is a configuration fault
    /// the verb refuses before it draws, so reaching here without one is a bug, not a condition.</summary>
    public ScoutCommitter CommitterFor(string task) =>
        Committer ?? throw new InvalidOperationException($"task {task} is private and the hand-check was drawn without a campaign key");
}

/// <summary>The hand-check of §3.7 as a pure draw and a Markdown file a person fills in (D9: the session reads every item, opening every file,
/// and records its reading item by item so the operator can audit it).
/// <list type="bullet">
/// <item><b>The draw</b> is ordered by <see cref="StableHash"/> of the seed and each item's identity — never by <see cref="Random"/> — so the
/// same seed over the same run draws the same items on any machine; a pool smaller than N gives what it has.</item>
/// <item><b>The file</b> names the arm of nothing: a reader judging whether a file was found is not told which team found it.</item>
/// <item><b>A private task's item</b> (<see cref="ScoutHandcheckPrivacy"/>) shows its label and a commitment; the plaintext the reader needs is
/// in the <see cref="PrivateCompanion"/>, a local-only file keyed by the same item numbers.</item>
/// </list></summary>
public static class ScoutHandcheck
{
    /// <summary>§3.7: at least 19 of 20 matcher items must agree with the hand reading, 18 of 20 assessor verdicts.</summary>
    public const string Thresholds = "the matcher passes at ≥ 19 of 20 agreeing pairs, the assessor at ≥ 18 of 20 agreeing verdicts (§3.7)";

    /// <summary>Every (output, G file) pair of one scored cell: the team's KEY FILES as written, each file of G, and the matcher's reading of
    /// the pair over the tree's file list — the reading the stored recall was computed with.</summary>
    public static IReadOnlyList<HandcheckPair> PairsOf(
        Guid cellId, string task, IReadOnlyList<string> groundTruth, IReadOnlyList<string> keyFilesAsWritten, IReadOnlyList<string> treeFiles, string treeRoot)
    {
        var found = RecallMatcher.Match(keyFilesAsWritten, groundTruth, treeFiles, treeRoot).Found.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. groundTruth.Select(g => new HandcheckPair(cellId, task, g, keyFilesAsWritten, found.Contains(RecallMatcher.Normalise(g))))];
    }

    public static HandcheckDraw Draw(IReadOnlyList<HandcheckPair> pairs, IReadOnlyList<HandcheckClaim> claims, int perKind, int seed) =>
        new(
            seed,
            perKind,
            Take(pairs.Where(p => p.Found), p => p.DrawKey, perKind, seed),
            Take(pairs.Where(p => !p.Found), p => p.DrawKey, perKind, seed),
            Take(claims, c => c.DrawKey, perKind, seed));

    private static IReadOnlyList<T> Take<T>(IEnumerable<T> pool, Func<T, string> key, int count, int seed) =>
        [.. pool.OrderBy(item => StableHash.Of(string.Create(CultureInfo.InvariantCulture, $"{seed}|{key(item)}")), StringComparer.Ordinal).Take(count)];

    /// <summary>The file of a draw whose tasks are all public.</summary>
    public static string Markdown(Guid runId, HandcheckDraw draw) => Markdown(runId, draw, ScoutHandcheckPrivacy.AllPublic);

    /// <summary>The file: a header with the rule and the thresholds, then every item with a checkbox pair and a note line — the reader ticks
    /// exactly one of <c>agree</c> / <c>disagree</c> per item. A private task's item is shown by label and commitment.</summary>
    public static string Markdown(Guid runId, HandcheckDraw draw, ScoutHandcheckPrivacy privacy)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"# Scout hand-check — run {runId:D}\n\n")
            .Append(CultureInfo.InvariantCulture, $"> Drawn by `bench scouts score --handcheck {draw.PerKind} --seed {draw.Seed}`. For every item, open the file(s) it names, ")
            .Append("then tick EXACTLY ONE of `agree` / `disagree` and say why on the note line. ")
            .Append(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(Thresholds[0])}{Thresholds[1..]}.\n")
            .Append(CultureInfo.InvariantCulture, $"> Drawn: {draw.Found.Count} found pair(s), {draw.NotFound.Count} not-found pair(s), {draw.Claims.Count} claim verdict(s).\n");

        if (privacy.PrivateTasks.Count > 0)
        {
            text.Append("> A PRIVATE task's item names its label and an HMAC commitment; the path or the claim's words are in the local companion file, under the same item number.\n");
        }

        Section(text, "Recall matcher — the matcher said FOUND", "F", draw.Found, (t, p) => Pair(t, p, privacy));
        Section(text, "Recall matcher — the matcher said NOT FOUND", "N", draw.NotFound, (t, p) => Pair(t, p, privacy));
        Section(text, "Assessor verdicts", "C", draw.Claims, (t, c) => Claim(t, c, privacy));

        return text.ToString();
    }

    /// <summary>The LOCAL companion of a draw with private tasks: the plaintext of every private item, under the public file's item numbers
    /// — refused inside git by the verb that writes it. Empty of items when no task is private.</summary>
    public static string PrivateCompanion(Guid runId, HandcheckDraw draw, IReadOnlySet<string> privateTasks)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"# Scout hand-check — run {runId:D} — PRIVATE companion\n\n")
            .Append("> NEVER commit this file. Each item below is the plaintext of a private item in the public hand-check file, under the same number.\n");

        Section(text, "Recall matcher — the matcher said FOUND", "F", draw.Found, (t, p) => Plain(t, p, privateTasks));
        Section(text, "Recall matcher — the matcher said NOT FOUND", "N", draw.NotFound, (t, p) => Plain(t, p, privateTasks));
        Section(text, "Assessor verdicts", "C", draw.Claims, (t, c) => Plain(t, c, privateTasks));

        return text.ToString();
    }

    private static void Section<T>(StringBuilder text, string title, string prefix, IReadOnlyList<T> items, Action<StringBuilder, T> body)
    {
        text.Append(CultureInfo.InvariantCulture, $"\n## {title}\n");

        foreach (var (item, i) in items.Select((item, i) => (item, i + 1)))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n### {prefix}{i:00}\n\n");
            body(text, item);
            text.Append("\n- [ ] agree\n- [ ] disagree\n- note: \n");
        }
    }

    private static void Pair(StringBuilder text, HandcheckPair pair, ScoutHandcheckPrivacy privacy)
    {
        if (privacy.IsPrivate(pair.Task))
        {
            text.Append(CultureInfo.InvariantCulture, $"Task `{pair.Task}` {ScoutPublication.PrivateMark} · cell `{pair.CellId:D}` · G file commitment `{privacy.CommitterFor(pair.Task).GroundTruthFile(pair.Task, pair.GroundTruthFile)}` · matcher: **{(pair.Found ? "found" : "not found")}**\n\n")
                .Append("The team's KEY FILES: in the companion file.\n");
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"Task `{pair.Task}` · cell `{pair.CellId:D}` · G file `{pair.GroundTruthFile}` · matcher: **{(pair.Found ? "found" : "not found")}**\n\n")
            .Append("The team's KEY FILES, as written:\n\n");
        KeyFiles(text, pair);
    }

    private static void KeyFiles(StringBuilder text, HandcheckPair pair)
    {
        foreach (var entry in pair.KeyFilesAsWritten.DefaultIfEmpty("(none)"))
        {
            text.Append(CultureInfo.InvariantCulture, $"- `{entry}`\n");
        }
    }

    private static void Claim(StringBuilder text, HandcheckClaim claim, ScoutHandcheckPrivacy privacy)
    {
        if (privacy.IsPrivate(claim.Task))
        {
            text.Append(CultureInfo.InvariantCulture, $"Task `{claim.Task}` {ScoutPublication.PrivateMark} · cell `{claim.Key.CellId:D}` · scout {claim.Key.Ordinal} · claim {claim.Key.Index} · assessor: **{ScoutClaimVerdicts.Word(claim.Verdict)}**\n\n")
                .Append(CultureInfo.InvariantCulture, $"> claim commitment `{privacy.CommitterFor(claim.Task).Claim(claim.Task, claim.Claim.Statement)}` — the claim is in the companion file\n");
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"Task `{claim.Task}` · cell `{claim.Key.CellId:D}` · scout {claim.Key.Ordinal} · claim {claim.Key.Index} · assessor: **{ScoutClaimVerdicts.Word(claim.Verdict)}**\n\n")
            .Append(CultureInfo.InvariantCulture, $"> `{claim.Claim.Path}{Line(claim)}` — {claim.Claim.Statement}\n");
    }

    private static void Plain(StringBuilder text, HandcheckPair pair, IReadOnlySet<string> privateTasks)
    {
        if (!privateTasks.Contains(pair.Task))
        {
            text.Append(CultureInfo.InvariantCulture, $"Task `{pair.Task}` — public; see the public file.\n");
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"Task `{pair.Task}` · cell `{pair.CellId:D}` · G file `{pair.GroundTruthFile}` · matcher: **{(pair.Found ? "found" : "not found")}**\n\n")
            .Append("The team's KEY FILES, as written:\n\n");
        KeyFiles(text, pair);
    }

    private static void Plain(StringBuilder text, HandcheckClaim claim, IReadOnlySet<string> privateTasks)
    {
        if (!privateTasks.Contains(claim.Task))
        {
            text.Append(CultureInfo.InvariantCulture, $"Task `{claim.Task}` — public; see the public file.\n");
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"Task `{claim.Task}` · cell `{claim.Key.CellId:D}` · scout {claim.Key.Ordinal} · claim {claim.Key.Index} · assessor: **{ScoutClaimVerdicts.Word(claim.Verdict)}**\n\n")
            .Append(CultureInfo.InvariantCulture, $"> `{claim.Claim.Path}{Line(claim)}` — {claim.Claim.Statement}\n");
    }

    private static string Line(HandcheckClaim claim) =>
        claim.Claim.Line.WasCaptured ? string.Create(CultureInfo.InvariantCulture, $":{claim.Claim.Line.Value}") : string.Empty;
}
