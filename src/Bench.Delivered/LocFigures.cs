namespace Bench.Delivered;

/// <summary>The cleaned-LOC family — the line figures only.
///
/// <para><b>What is deliberately NOT here.</b> The source's headline metric is
/// <c>FileSaturationLayer</c> (Σ per-file cleaned churn^0.75 × a LAYER WEIGHT) and its sibling
/// <c>LayerSpreadLoc</c>. Both were fitted on that stack's directory layout and its ticket pool, and a
/// weight table tuned for a Symfony tree says nothing about this one. Porting them would import a number
/// that looks measured and is not — so the three fixed line figures come across and the fits stay behind.
/// The source's own report says the ordering of its size metrics flipped between pools of 9, 20 and 21
/// tickets, which is the argument against inheriting a fit rather than for it.</para>
/// </summary>
/// <param name="Diff">Raw added + removed over counted AND evidence files — the diff as it arrived.</param>
/// <param name="Cleaned">Cleaned added + removed in the COUNTED files: the part that was priced. Removals
/// count, because the source measured that deletions carry signal an additions-only figure misses.</param>
/// <param name="Excluded">Cleaned churn in the EVIDENCE files — tests, migrations, docs, generated code.
/// Its own figure rather than a footnote: an arm that wrote only tests has a real number here and a zero in
/// <paramref name="Cleaned"/>, and one total would hide exactly that.</param>
/// <param name="Added">Cleaned, joined, wrapped logic lines ADDED in counted files.</param>
/// <param name="Physical">The same pipeline WITHOUT continuation joining. Carried so the join's influence
/// stays measurable rather than assumed — it is the one adaptation in this port that could silently change
/// every number, and a figure that cannot be compared against its unjoined twin cannot show that.</param>
public sealed record LocFigures(
    int Diff,
    int Cleaned,
    int Excluded,
    int Added,
    int Physical,
    int FilesCounted,
    int FilesEvidence,
    int FilesDropped)
{
    public static LocFigures None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>The three named figures, by the key a report column uses. Fixed rather than resolved
    /// through a setting: a column labelled "Cleaned LOC" that a configuration change could re-point at a
    /// different number is a defect nothing would report.</summary>
    public int ValueOf(string figure) => figure switch
    {
        Figures.Diff => Diff,
        Figures.Cleaned => Cleaned,
        Figures.Excluded => Excluded,
        _ => 0,
    };

    public string Describe =>
        $"{Cleaned} cleaned / {Diff} raw · {Excluded} evidence · "
        + $"{FilesCounted} counted, {FilesEvidence} evidence, {FilesDropped} dropped file(s)";
}

/// <summary>The figure names, written once because the store, the report and any sort must read the same
/// key.</summary>
public static class Figures
{
    public const string Diff = "delivered.diff";

    public const string Cleaned = "delivered.cleaned";

    public const string Excluded = "delivered.evidence";
}

/// <summary>Computes the line figures from a diff. No model calls, no repository access — a pure function
/// of the diff text, so it costs nothing and can be recomputed at will. That property is the whole reason
/// the module is a leaf.</summary>
public static class LocCalculator
{
    public static LocFigures Compute(string diff) => Compute([diff]);

    /// <summary>Several diffs, because one task may span repositories.</summary>
    public static LocFigures Compute(IReadOnlyList<string> diffs)
    {
        var files = diffs.SelectMany(DiffParser.Hunks).Select(Measure).ToList();

        var counted = files.Where(f => f.Fate == PathFate.Counted).ToList();
        var evidence = files.Where(f => f.Fate == PathFate.Evidence).ToList();

        return new LocFigures(
            // Dropped files are excluded from the RAW figure too. A vendored tree in the diff is not "the
            // diff as it arrived" for pricing purposes — it is noise that would dwarf every real number.
            Diff: counted.Concat(evidence).Sum(f => f.Raw),
            Cleaned: counted.Sum(f => f.Churn),
            Excluded: evidence.Sum(f => f.Churn),
            Added: counted.Sum(f => f.Added),
            Physical: counted.Sum(f => f.PhysicalAdded),
            FilesCounted: counted.Count,
            FilesEvidence: evidence.Count,
            FilesDropped: files.Count(f => f.Fate == PathFate.Dropped));
    }

    /// <param name="Churn">Added plus removed. Both counted in wrapped physical lines of JOINED logical
    /// lines, so a wrapped statement costs the same as the one-liner it could have been.</param>
    private sealed record FileSize(string Path, int Added, int Churn, int PhysicalAdded, int Raw, PathFate Fate);

    private static FileSize Measure(FileHunks file)
    {
        var added = WrappedCount(file.Added, file.Path);
        var removed = WrappedCount(file.Removed, file.Path);

        return new FileSize(
            file.Path,
            Added: added,
            Churn: added + removed,
            PhysicalAdded: LineNormalizer.Normalize(file.Added, file.Path).Sum(LineNormalizer.WrappedLineCount),
            Raw: file.Added.Count + file.Removed.Count,
            Fate: PathCategoryTable.Fate(file.Path));
    }

    private static int WrappedCount(IReadOnlyList<string> lines, string path) =>
        LineNormalizer.NormalizeAndJoin(lines, path).Sum(LineNormalizer.WrappedLineCount);
}
