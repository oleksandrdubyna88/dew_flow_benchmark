namespace Bench.Tests.ModelArms;

/// <summary>The checked-in readers of the harness (D28, E6.S1): the seven that read an arm's files and are run, and every other script of the two folders, each by name with the reason it is not. The sha256 of each is the file's own at the commit E6.S1 was built on; the seven shell scripts the layout scan reads
/// are pinned by the parent's §2 and checked in <see cref="ShellLayout"/>. A reader's <see cref="PinnedReader.Timeout"/> is ten times its wall measured on the host that built this test (module doc, <i>The consumer check (E6.S1)</i>), never under 30 s.</summary>
internal static class PinnedReaders
{
    public const string Harness = "research/local-model-coding/harness/";

    public const string Part6 = "research/local-model-coding/harness/part6/";

    private static readonly TimeSpan Floor = TimeSpan.FromSeconds(30);

    public static PinnedReader Metrics { get; } = new("metrics.mjs", Harness + "metrics.mjs", "63d668b0399dbbef22da7b9ba2105a144118715ec2abea8793f4067786146add", [Rewrite.RootLine], Floor);

    /// <summary>The seven, in the order the story's table lists them.</summary>
    public static IReadOnlyList<PinnedReader> Run { get; } =
    [
        Metrics,
        new("metrics6.mjs", Part6 + "metrics6.mjs", "4b64f3114f5703699475b338c01b3a23b176c625609bd56c684ed05fbc9ebc50", [], Floor),
        new("t-survival.mjs", Harness + "t-survival.mjs", "5968556b310e848e59513229664416a53a6908eca05a86fd75d7638eeb5189ac", [Rewrite.BaseLiteral], Floor),
        new("compare.mjs", Harness + "compare.mjs", "274cde8951c60d97ed5b010c59784797ac5105cbf7dae04ff13d196121f314be", [Rewrite.RootLine], Floor),
        new("make-tables.mjs", Harness + "make-tables.mjs", "d08aed2e940b8c5efa4bb86e53335906d6a8da99da9fea6a1ea1d50eba0a59db", [], Floor),
        new("coai-findings.mjs", Part6 + "coai-findings.mjs", "6aa6528389eedb6431901dd30a5247929fcc01b57aaf719e2d621626b5f84cf2", [Rewrite.RootLine], Floor),
        new("coai-cost.mjs", Part6 + "coai-cost.mjs", "f7ff61f2b69d15350beb8f9c99229bc3ce225db415663077c72b8e705d22c794", [Rewrite.RootLine], Floor),
    ];

    /// <summary>Every other <c>*.mjs</c> and <c>*.js</c> of the two folders: repository path → why it is not run (§8 item 6). The judges and the mutation checks are shell scripts or analysis; <c>ObjectiveReportTests</c>' round trip stands for the judges (D28).</summary>
    public static IReadOnlyDictionary<string, string> Excluded { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Part6 + "make-tables6.mjs"] = "reads data/part6/, the write-up's assembled folder, whose judge files no run writes; make-tables.mjs imports it only when that folder exists, and this test never creates it",
        [Part6 + "make-tables7.mjs"] = "reads data/part7/, the write-up's assembled folder, whose judge files no run writes; imported by make-tables.mjs only when that folder exists",
        [Harness + "survival.mjs"] = "reads the Q and H arms' fill files, which this module never writes",
        [Harness + "mutate.mjs"] = "mutates the final code of an arm: analysis (the analysis plan), not a reader of the record",
        [Part6 + "mutate6.mjs"] = "mutates the final code of an arm: analysis (the analysis plan), not a reader of the record",
        [Harness + "tasks-plan.mjs"] = "a checker of a task list, not a reader of the record; E4.S1's oracle for TasksPlan",
        [Part6 + "tasks-plan.mjs"] = "a checker of a task list, not a reader of the record; E4.S1's oracle for TasksPlan",
        [Harness + "ask.mjs"] = "parts 1-5's Q-arm runner: it asks a model, it reads no arm",
        [Harness + "ask-haiku.mjs"] = "parts 1-5's H-arm runner: it asks a model, it reads no arm",
        [Harness + "fill.mjs"] = "parts 1-5's filler for the Q and H arms: it writes, it reads no arm of this module",
        [Harness + "gen-q-briefs.js"] = "parts 1-5's brief generator for the Q arms: it writes briefs",
    };
}
