using System.Text.RegularExpressions;

namespace Bench.Tests.ModelArms;

/// <summary>A variable of a pinned shell script that stands for a place under the experiment root, how the script ASSIGNS it (the text <c>VARIABLE=assignment</c>, which the script must hold - in <paramref name="AssignedIn"/> when it only inherits it) and what it stands for relative to the root.</summary>
internal sealed record ShellRoot(string Variable, string Assignment, string Expansion, string AssignedIn = "");

/// <summary>One of the seven part-6 scripts the scan reads, pinned by the sha256 of the parent's §2, the variables it maps and what its <c>$1</c> stands for.</summary>
internal sealed record ShellScript(string Name, string Sha256, string DollarOne, IReadOnlyList<ShellRoot> Roots);

/// <summary>What the module calls a path the shell called otherwise, and the decision that renamed it.</summary>
internal sealed record Rename(string Module, string Decision);

/// <summary>The shell's layout, SCANNED (E6.S1, D5): every path the seven pinned part-6 scripts build from the experiment root's variables (<c>$E</c>, <c>$R</c>, <c>$OUT</c>, <c>$NOTES</c>, <c>$WORK</c>, <c>$O</c>, <c>$M</c>, <c>$S</c> and <c>${O}/../</c>), normalised to the module's placeholders: <c>$name</c> and the script's <c>$1</c> → <c>&lt;step&gt;</c> (or
/// <c>&lt;id&gt;</c>, <c>&lt;arm&gt;</c> as the script uses it), <c>$attempt</c> → <c>&lt;n&gt;</c>, <c>$id</c> → <c>&lt;id&gt;</c>, <c>$i</c> → <c>&lt;i&gt;</c>, <c>$a</c> → <c>&lt;arm&gt;</c>, <c>$(tok "$a")</c> and <c>$TOKEN</c> → <c>&lt;token&gt;</c>, <c>$(date …)</c> → <c>&lt;stamp&gt;</c>, the root of <c>$M</c> (<c>chain</c> in <c>after6.sh:7</c>, <c>chain7</c> in <c>after7.sh:8</c>) and of <c>$S</c>
/// (<c>.locked-modes</c>, <c>lock.sh:8</c>) kept per script. The three tables beside the scan are the whole decision list: a path the shell wrote that the module does not spell is in <see cref="Renamed"/> (with the decision) or <see cref="ShellOnly"/> (with why); a path the module spells that the shell did not write is in <see cref="Additions"/>.
/// <para>
/// What it cannot see: files the MODEL writes by brief (<c>work/tasks.json</c>, <c>work/t&lt;n&gt;.md</c>), paths built inside inline node other than <c>${O}/../objective.json</c> and the literal ones the heredoc of <c>objective-checks.sh</c> holds, a glob (<c>$OUT/$name.a*.jsonl</c> lists, it names no file), and the paths of the target's own clone.
/// </para></summary>
internal static partial class ShellLayout
{
    public const string Part6 = "research/local-model-coding/harness/part6/";
    private const string Experiment = "E=/home/jinx/git/coai-exp2";

    public static IReadOnlyList<ShellScript> Scripts { get; } =
    [
        new("run-arm.sh", "95dead2b92ac5386934bc944511d8ab83f905c13e87ae437ac4544dab1b6bb57", "<step>", [new("E", Experiment[2..], string.Empty), new("R", "$E/r/$TOKEN", "r/<token>"), new("OUT", "$R/out", "r/<token>/out"), new("NOTES", "$R/notes", "r/<token>/notes"), new("WORK", "$R/work", "r/<token>/work")]),
        new("t-lib.sh", "6ee808d13b96cc6c6212acc59c19118fd4041c5484c58175762a442a44b02986", "<id>", [new("E", Experiment[2..], string.Empty, "run-arm.sh"), new("R", "$E/r/$TOKEN", "r/<token>", "run-arm.sh"), new("OUT", "$R/out", "r/<token>/out", "run-arm.sh"), new("NOTES", "$R/notes", "r/<token>/notes", "run-arm.sh"), new("WORK", "$R/work", "r/<token>/work", "run-arm.sh")]),
        new("objective-checks.sh", "7e18ae7d1c2c7fbed676f1b24b135754c0d932ba6e02428280af32552cba92ea", string.Empty, [new("E", Experiment[2..], string.Empty), new("O", "$E/r/$TOKEN/out/objective", "r/<token>/out/objective")]),
        new("after6.sh", "097b5592a1dd5584822d9e3e6e85871640ca363e5bf4818188073f65dc51d15d", "<arm>", [new("E", Experiment[2..], string.Empty), new("M", "$E/chain", "chain"), new("R", "$E/r/$(tok \"$a\")", "r/<token>")]),
        new("after7.sh", "12199719e5e46ccebbdd19f81f413d44bb66547eef536ccaa494df057bbcde4c", "<arm>", [new("E", Experiment[2..], string.Empty), new("M", "$E/chain7", "chain7"), new("R", "$E/r/$(tok \"$a\")", "r/<token>")]),
        new("lock.sh", "531be3e8cee4ed29507ebd6cd46cbe077afd6a10d381a81b266946b9e9012903", string.Empty, [new("E", Experiment[2..], string.Empty), new("S", "$E/.locked-modes", ".locked-modes")]),
        new("run-all.sh", "dacc74d261e2f54f05ddb6c7ab3925d675b76a76dcd644187156e934af18cb85", string.Empty, [new("E", Experiment[2..], string.Empty)]),
    ];

    /// <summary>The paths the shell wrote (or read) that the module spells under ANOTHER name, shell name → the module's name and the decision that renamed it. Compared as the module's spellings are: a concrete name stands for its placeholder (<c>2-hA.before</c> for <c>&lt;step&gt;.before</c>).</summary>
    public static IReadOnlyDictionary<string, Rename> Renamed { get; } = new Dictionary<string, Rename>(StringComparer.Ordinal)
    {
        ["r/<token>/out/task-<id>-haiku.diff"] = new("r/<token>/out/task-<id>-coder.diff", "D5: the coder model is configuration, so no path names a model - THE one rename; the analysis plan carries it"),
        ["r/<token>/out/task-<id>-haiku.head"] = new("r/<token>/out/task-<id>-coder.head", "D5: the same rename, for the snapshot t-survival.mjs does not read"),
        ["chain/locked"] = new("chain/<run>/locked", "D27: the stage markers are per run (part 6's chain/)"),
        ["chain/unlocked"] = new("chain/<run>/unlocked", "D27: the stage markers are per run (part 6's chain/)"),
        ["chain/arms-done"] = new("chain/<run>/arms-done", "D27: the stage markers are per run (part 6's chain/)"),
        ["chain7"] = new("chain", "D27: part 7's chain7/ and part 6's chain/ are one folder, the run a level below it for the three stage markers"),
        ["chain7/locked"] = new("chain/<run>/locked", "D27: the stage markers are per run (part 7's chain7/)"),
        ["chain7/unlocked"] = new("chain/<run>/unlocked", "D27: the stage markers are per run (part 7's chain7/)"),
        ["chain7/arms-done"] = new("chain/<run>/arms-done", "D27: the stage markers are per run (part 7's chain7/)"),
        ["chain7/made-<arm>"] = new("chain/made-<arm>", "D27: the made and objective markers are per arm, in the one chain folder"),
        ["chain7/objective-<arm>"] = new("chain/objective-<arm>", "D27: the made and objective markers are per arm, in the one chain folder"),
        ["exp.env"] = new("experiment.json", "D4: the shell's sourced constants are one JSON file at the root"),
    };

    /// <summary>The paths the shell wrote (or read) that the module does not spell at all, shell name → why. The scripts are replaced by verbs; the rest is the analysis half's, which the module only CLOSES by name (<c>closed</c>) and never writes.</summary>
    public static IReadOnlyDictionary<string, string> ShellOnly { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["run-arm.sh"] = "replaced by the run-arm verb (E2.S8)",
        ["run-all.sh"] = "replaced by the run-all verb (E5.S3)",
        ["lock.sh"] = "replaced by the lock and unlock verbs (D11, E5.S1)",
        ["objective-checks.sh"] = "replaced by the checks verb (E3.S5)",
        ["t-lib.sh"] = "replaced by the task pipeline of run-arm (D14, E4.S3)",
        ["tasks-plan.mjs"] = "replaced by TasksPlan (E4.S1); a checker, not a file of the record",
        ["judge-abs"] = "the analysis half's absolute judge folder (D28, section 5); closed by name in part 7's isolation, never written by this module",
        ["judge-abs.log"] = "the analysis half's judge log; closed by name, never written by this module",
        ["judge-coai"] = "the analysis half's coai judge folder; closed by name, never written by this module",
        ["judge-coai.out"] = "the analysis half's coai judge log; closed by name, never written by this module",
        ["mut6"] = "the analysis half's mutation folder; closed by name, never written by this module",
        ["mut6.log"] = "the analysis half's mutation log; closed by name, never written by this module",
        ["coai-findings.json"] = "written by coai-findings.mjs (a reader this test runs), at the root of the experiment's own folder; closed by name in part 7",
        ["metrics6.json"] = "written by metrics6.mjs (a reader this test runs); closed by name in part 7",
        ["chain.log"] = "the owner's own log of the chain; closed by name in part 7, never written by this module",
        ["PROTOCOL.md"] = "the owner's protocol document (read once, E6 section 8 item 4); closed by name in part 7",
    };

    /// <summary>The paths the module spells that no scanned script builds, module name → the decision or story that added it.</summary>
    public static IReadOnlyDictionary<string, string> Additions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["r/<token>/out/<step>.continue"] = "D7, D10 (ledger 1.1): the gate's marker 'attempt N finished, continue its session'",
        ["chain/<run>/.chain.lock"] = "E5.S4 (item 45): the run's own lock, held by the one chain process",
        [".isolation.lock"] = "D11: the lock over every change of a mode",
        [".locked-modes.applied"] = "D11: the second immutable file of the two-file isolation state",
        [".locked-modes.applied.restored-<stamp>"] = "D11: where unlock moves the applied file",
        ["pids/<arm>.lock"] = "D13: the runner's own lock, the one answer to whether an arm runs",
        ["pids/.run-all.lock"] = "E5.S3: run-all's own lock",
        ["r/<token>/out/pick-<id>.json"] = "D22: the pick record of a task carried back",
        ["chain/executables.txt"] = "D17: the executables the chain resolved",
        ["r/<token>/out/objective.prev-<stamp>"] = "E3.S5: an earlier objective folder, renamed aside",
        ["r/<token>/out/objective/stages.json"] = "E3.S5: how each stage ended, what checks --parse-only reads",
        [".gate.lock.holder"] = "E2.S6: who holds the gate lock, a hint beside the kernel's lock",
        ["keep-awake.<stamp>.<n>.err"] = "E5.S2: the stderr of each launch of the keep-awake child",
        ["arms.map.json"] = "D4: the arm map (the shell's was $MAP of exp.env, a hidden file named by hash)",
        ["prep-base.log"] = "E3.S1: prep-base.sh's log (a script that is not one of the seven scanned)",
        ["prep-base.prev-<stamp>"] = "E3.S1: an earlier attempt's prep-base logs, moved aside",
        ["base-build.log"] = "E3.S1: prep-base's build log",
        ["base-npm-<n>.log"] = "E3.S1: prep-base's npm ci logs",
        ["base-test.log"] = "E3.S1: prep-base's main suite log",
        ["base-ts-test.log"] = "E3.S1: prep-base's extension suite log",
        ["base-results"] = "E3.S1: the folder prep-base's main suite writes its results into",
        ["r/<token>/work/tasks.json"] = "D14: the task list the SPLIT model writes by brief (the shell's model wrote it too; the module spells it to read it)",
        ["r/<token>/work/<id>.md"] = "D14: a task's brief, written by the split model by brief (the shell's t-lib.sh read it through batches.json)",
    };

    /// <summary>Whether the module spells <paramref name="path"/> (a shell name, placeholders and all): some spelling is an instance of it, or it is the spelling's own folder.</summary>
    public static bool IsSpelled(string path) => ModuleSpellings.All.Any(spelling => ModuleSpellings.PatternOf(spelling).IsMatch(Instance(path)));

    /// <summary>Whether the scan of the shell finds <paramref name="spelling"/>: a scanned path is an instance of it, or lives under it (a folder).</summary>
    public static bool IsFound(string spelling) =>
        Paths().Any(path => ModuleSpellings.PatternOf(spelling).IsMatch(Instance(path)) || path.StartsWith(spelling + "/", StringComparison.Ordinal));

    /// <summary>A shell name with its number placeholders as numbers, so the pattern of a spelling can match it.</summary>
    private static string Instance(string path) => path.Replace("<n>", "1", StringComparison.Ordinal).Replace("<i>", "1", StringComparison.Ordinal);

    public static string TextOf(ShellScript script) => File.ReadAllText(Path.Combine(Cli.Repository.Root, Part6 + script.Name));

    /// <summary>Every path the seven scripts build under the root, normalised and sorted.</summary>
    public static IReadOnlyList<string> Paths() => [.. Scripts.SelectMany(Scan).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The paths of ONE script (its own roots and its own <c>$1</c>).</summary>
    public static IEnumerable<string> Scan(ShellScript script)
    {
        var text = Prepared(TextOf(script));
        var names = string.Join('|', script.Roots.Select(root => root.Variable));
        foreach (Match match in new Regex($@"\$(?:\{{(?<braced>{names})\}}|(?<bare>{names})(?![A-Za-z0-9_])){Tail}", RegexOptions.CultureInvariant).Matches(text))
        {
            var variable = match.Groups["braced"].Success ? match.Groups["braced"].Value : match.Groups["bare"].Value;
            if (Normalised(script, script.Roots.Single(root => root.Variable == variable).Expansion, match.Groups["tail"].Value) is { Length: > 0 } path)
            {
                yield return path;
            }
        }
    }

    private const string Tail = @"""?(?<tail>(?:[/.][A-Za-z0-9_./*<>\-]*|(?:\$\{\w+\}|\$\w+)|[A-Za-z0-9_*<>\-])*)";

    /// <summary>The script's text without its comment lines, <c>$(tok "$x")</c> and <c>$TOKEN</c> as <c>&lt;token&gt;</c>, <c>$(date …)</c> as <c>&lt;stamp&gt;</c>.</summary>
    private static string Prepared(string text)
    {
        var lines = text.Split('\n').Where(line => !line.TrimStart().StartsWith('#') || line.StartsWith("#!", StringComparison.Ordinal));
        var joined = string.Join('\n', lines);
        joined = TokenCall().Replace(joined, "<token>");
        joined = DateCall().Replace(joined, "<stamp>");
        return joined.Replace("${TOKEN}", "<token>", StringComparison.Ordinal).Replace("$TOKEN", "<token>", StringComparison.Ordinal);
    }

    private static string Normalised(ShellScript script, string expansion, string tail)
    {
        if (tail.Contains('*', StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var variables = Variable().Replace(tail, each => each.Groups["name"].Value switch
        {
            "name" => "<step>",
            "attempt" => "<n>",
            "id" => "<id>",
            "i" => "<i>",
            "a" => "<arm>",
            "1" => script.DollarOne.Length > 0 ? script.DollarOne : "$1",
            var other => "${" + other + "}",
        });
        if (variables.Contains('$', StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var segments = new List<string>(expansion.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in variables.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(segment);
            }
        }

        var path = string.Join('/', segments);
        return variables.StartsWith('.') && !variables.StartsWith("./", StringComparison.Ordinal) && !variables.StartsWith("../", StringComparison.Ordinal) ? expansion + variables : path;
    }

    [GeneratedRegex(@"\$\(tok ""\$\w+""\)", RegexOptions.CultureInvariant)]
    private static partial Regex TokenCall();

    [GeneratedRegex(@"\$\(date [^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex DateCall();

    [GeneratedRegex(@"\$\{(?<name>\w+)\}|\$(?<name>\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();
}
