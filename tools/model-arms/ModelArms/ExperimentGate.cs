using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms;

/// <summary>The one road in for every verb that reads an experiment (D4 read once, D17 a, D18). A verb is started only after the root is named, has
/// the files its stage needs, belongs to the operator and is closed to group and others, and its <c>experiment.json</c> is valid; each refusal names
/// what it refused and ends the walk, so a later check never runs on what an earlier one refused. Exit 4 is a wrong configuration, exit 3 is a host
/// that could not say (a kernel read that failed, a file that could not be read).
/// <para>
/// The root is normalised once (<c>GetFullPath</c>, the trailing separator trimmed) and that text is what the trust check stats; the facts it returns
/// carry the normalised path whose owner, mode and inode were read, and <c>experiment.json</c> is read from that path — never from the operator's
/// spelling again. The root must be a folder and the two files regular files: a FIFO would block the read and a folder would fail it.
/// </para>
/// <para>
/// <b>The residual race.</b> Reading a file by path cannot be tied to the <c>stat</c> before it, so the file is stat-ed AGAIN after the read and
/// refused (exit 4) when its device, inode, type, owner, mode or change time (ctime, with nanoseconds, read by the same <c>statx</c> call) are not what
/// was judged. That closes a swap in the window between the first <c>stat</c> and the read — measured before this check: 18 of 1500 passes read a
/// link's target swapped in with <c>mv -T</c> — and, through the ctime, a swap and a swap back (<c>ln -f</c>, <c>mv -T</c> twice) that leaves the same
/// inode under the name: 101 of 1500 passed with both stats equal before the ctime. It does not close everything: inode numbers can be reused on some
/// file systems, and a ctime narrows that but does not close it; a swap AFTER the second <c>stat</c> is not seen; and nothing above the root is
/// judged, so whoever can write the root (only the operator, by D17) or ANY ANCESTOR of it can still swap the root or an ancestor and change what is
/// read. The gate reads <c>experiment.json</c> only; <c>scenarios.json</c> is judged here (its facts travel on as <see cref="ExperimentInput.JudgedScenarios"/>) and read by the verb, which must
/// look at it again as this does — <c>validate</c> does, through <see cref="StillTheSame"/>.
/// </para></summary>
public static class ExperimentGate
{
    private const string RootVariable = "MODEL_ARMS_EXPERIMENT";

    /// <summary>A path and the kind of file it must be.</summary>
    private sealed record Wanted(string Path, FileKind Kind);

    /// <summary>What step 3 leaves for step 4: the root, and <c>experiment.json</c> exactly as the trust check read it (its path and its facts).</summary>
    private sealed record Judged(string Root, FileFacts Experiment);

    public static Task<int> RunAsync(VerbInput input, Func<ExperimentInput, Task<int>> verb) => RunAsync(input, verb, GateSeams.Real);

    /// <summary>The same with the host's reads handed in. Internal: a caller that could hand in its own reads could skip the trust check, so only the
    /// overload above (the real reads) and the test assembly reach it.</summary>
    internal static async Task<int> RunAsync(VerbInput input, Func<ExperimentInput, Task<int>> verb, GateSeams seams)
    {
        var admitted = await Admit(input, seams);
        return await admitted.Match(verb, stop => Report(input, stop));
    }

    private static Task<Passage<ExperimentInput>> Admit(VerbInput input, GateSeams seams) =>
        Task.FromResult(NamedRoot(input))
            .Then(root => Present(input, root))
            .Then(root => TrustedRoot(input, root, seams))
            .ThenAsync(judged => ReadExperiment(input, judged, seams))
            .Then(admitted => Needed(admitted))
            .Then(admitted => TrustedScenarios(admitted, seams));

    private static Task<int> Report(VerbInput input, Passage<ExperimentInput>.Stopped stop)
    {
        foreach (var line in stop.Lines)
        {
            input.Error.WriteLine(line);
        }

        return Task.FromResult(stop.ExitCode);
    }

    /// <summary>Step 1: the root from <c>--experiment</c>, else from <c>MODEL_ARMS_EXPERIMENT</c>, made absolute and normalised.</summary>
    private static Passage<string> NamedRoot(VerbInput input)
    {
        var flag = input.Flags.Value("experiment");
        var named = flag.Length > 0 ? flag : input.Environment.GetValueOrDefault(RootVariable, string.Empty);
        return named switch
        {
            "" => Passage<string>.Stop(ExitCodes.Configuration, $"model-arms {input.Verb}: no experiment root — pass --experiment <root> or set {RootVariable}"),
            _ when ExecutableRule.HoldsNul(named) => Passage<string>.Stop(ExitCodes.Configuration, $"model-arms {input.Verb}: the experiment root '{ExecutableRule.Printable(named)}' holds a NUL character"),
            _ => new Passage<string>.Through(Path.TrimEndingDirectorySeparator(Path.GetFullPath(named))),
        };
    }

    /// <summary>Step 2: the root and its <c>experiment.json</c> exist.</summary>
    private static Passage<string> Present(VerbInput input, string root) =>
        Unmet(input, RootRequirements.ToRead, root, root);

    /// <summary>Step 3: the root and <c>experiment.json</c> are the operator's own and closed to group and others.</summary>
    private static Passage<Judged> TrustedRoot(VerbInput input, string root, GateSeams seams)
    {
        var experiment = new RootPaths(root).ExperimentFile;
        var judged = Judge(input, root, [new(root, FileKind.Directory), new(experiment, FileKind.Regular)], seams);
        return judged.Map(facts => new Judged(root, facts[1]));
    }

    /// <summary>Step 4: <c>experiment.json</c>, read from the path the trust check judged, looked at again after the read, then through every refusal of D4.</summary>
    private static async Task<Passage<ExperimentInput>> ReadExperiment(VerbInput input, Judged judged, GateSeams seams)
    {
        var text = await TextOf(judged.Experiment.Path, input.Stopping);
        await seams.AfterRead(judged.Experiment.Path);
        var same = StillTheSame(input, judged.Experiment, seams);
        return text.Match(
            json => same.Bind(_ => Parsed(input, judged.Root, json)),
            reason => Passage<ExperimentInput>.Stop(ExitCodes.Environment, $"model-arms {input.Verb}: {reason}"));
    }

    /// <summary>Step 5: the files the verb's stage needs, scenarios and baseline by the names the experiment configured.</summary>
    private static Passage<ExperimentInput> Needed(ExperimentInput admitted)
    {
        var experiment = admitted.Experiment;
        var requirements = RootRequirements.For(admitted.Verb.Verb, ArmsOf(admitted.Verb), experiment.ScenariosFile, experiment.BaselineFile);
        return Unmet(admitted.Verb, requirements, admitted.Root.Root, admitted);
    }

    /// <summary>Step 6: <c>scenarios.json</c>, when the root has one, is trusted as the root is. Verbs that do not need it do not require it.</summary>
    private static Passage<ExperimentInput> TrustedScenarios(ExperimentInput admitted, GateSeams seams)
    {
        var scenarios = admitted.Root.Of(admitted.Experiment.ScenariosFile);
        return scenarios.Match(
            path => PathExists(path) ? TrustedFile(admitted, path, seams) : new Passage<ExperimentInput>.Through(admitted),
            reason => Passage<ExperimentInput>.Stop(ExitCodes.Configuration, $"model-arms {admitted.Verb.Verb}: {reason}"));
    }

    private static Passage<ExperimentInput> TrustedFile(ExperimentInput admitted, string path, GateSeams seams) =>
        Judge(admitted.Verb, admitted.Root.Root, [new(path, FileKind.Regular)], seams).Map(facts => admitted with { JudgedScenarios = [facts[0]] });

    private static Passage<T> Unmet<T>(VerbInput input, IReadOnlyList<RootRequirement> requirements, string root, T carried)
    {
        var sentence = RootRequirements.FirstUnmet(requirements, root, PathExists);
        return sentence.Length == 0
            ? new Passage<T>.Through(carried)
            : Passage<T>.Stop(ExitCodes.Configuration, $"model-arms {input.Verb}: {sentence}");
    }

    private static IReadOnlyList<string> ArmsOf(VerbInput input) =>
        input.Verb is "run-arm" or "run-all" or "checks" ? input.Positionals : [];

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    private static Passage<ExperimentInput> Parsed(VerbInput input, string root, string json)
    {
        var location = new ExperimentLocation(root, Home(input.Environment), RootFacts.GitTopOf(root), RootFacts.ThisRepository(AppContext.BaseDirectory));
        return ExperimentFile.Read(json, location).Match(
            experiment => new Passage<ExperimentInput>.Through(new ExperimentInput(input, new RootPaths(root), experiment)),
            reason => Passage<ExperimentInput>.Stop(
                ExitCodes.Configuration,
                [$"model-arms {input.Verb}: experiment.json is refused:", .. reason.Split('\n').Select(problem => $"  {problem}")]));
    }

    private static string Home(IReadOnlyDictionary<string, string> environment) =>
        environment.GetValueOrDefault("HOME", string.Empty) is { Length: > 0 } home ? home : environment.GetValueOrDefault("USERPROFILE", string.Empty);

    /// <summary>The file's text, or why the host could not give it. A stop the operator asked for cancels the read.</summary>
    private static async Task<Outcome<string>> TextOf(string path, CancellationToken ct)
    {
        try
        {
            return Outcome<string>.Success(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"{path} cannot be read: {ex.Message}");
        }
    }

    /// <summary>The second <c>stat</c>: the file just read is the file that was judged. Shared with the verbs that read a file the gate only judged. Off POSIX nothing was judged, so nothing is compared.</summary>
    internal static Passage<FileFacts> StillTheSame(VerbInput input, FileFacts judged, GateSeams seams)
    {
        if (OperatingSystem.IsWindows())
        {
            return new Passage<FileFacts>.Through(judged);
        }

        var again = seams.ReadFacts(judged.Path, FileKind.Regular);
        return again switch
        {
            FactsRead.Read read => Compared(input, judged, read.Facts),
            FactsRead.Refused refused => Passage<FileFacts>.Stop(ExitCodes.Configuration, $"model-arms {input.Verb}: {refused.Reason}"),
            FactsRead.Unread unread => Passage<FileFacts>.Stop(ExitCodes.Environment, $"model-arms {input.Verb}: {unread.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static Passage<FileFacts> Compared(VerbInput input, FileFacts judged, FileFacts now)
    {
        var changes = RootTrust.Changes(judged, now);
        return changes.Count == 0
            ? new Passage<FileFacts>.Through(now)
            : Passage<FileFacts>.Stop(
                ExitCodes.Configuration,
                $"model-arms {input.Verb}: {System.IO.Path.GetFileName(judged.Path)} changed between its trust check and its read ({string.Join(", ", changes)}) — refused (D17)");
    }

    /// <summary>D17 a over <paramref name="files"/>: off POSIX the printed line says the check was not performed and the walk goes on; on POSIX the
    /// facts are read (a kernel read that fails stops with exit 3), a link or a file of the wrong kind is refused, and the owner and mode
    /// are judged (exit 4, every problem printed). The answer is the facts that were read, in the order given.</summary>
    private static Passage<IReadOnlyList<FileFacts>> Judge(VerbInput input, string root, IReadOnlyList<Wanted> files, GateSeams seams)
    {
        if (OperatingSystem.IsWindows())
        {
            input.Error.WriteLine(((TrustAnswer.NotPerformed)RootTrust.Decide(false, 0, [], root)).Line);
            return new Passage<IReadOnlyList<FileFacts>>.Through([.. files.Select(file => new FileFacts(file.Path, -1, UnixFileMode.None))]);
        }

        return JudgeFacts(input, root, files, RootFacts.CurrentUid(), seams);
    }

    private static Passage<IReadOnlyList<FileFacts>> JudgeFacts(VerbInput input, string root, IReadOnlyList<Wanted> files, int uid, GateSeams seams)
    {
        var reads = files.Select(file => seams.ReadFacts(file.Path, file.Kind)).ToList();

        var unread = reads.OfType<FactsRead.Unread>().Select(read => read.Reason).ToList();
        var facts = reads.OfType<FactsRead.Read>().Select(read => read.Facts).ToList();
        IReadOnlyList<string> refusals = [.. reads.OfType<FactsRead.Refused>().Select(read => read.Reason), .. ProblemsOf(RootTrust.Decide(true, uid, facts, root))];
        if (unread.Count > 0)
        {
            return Passage<IReadOnlyList<FileFacts>>.Stop(ExitCodes.Environment, [.. unread.Concat(refusals).Select(reason => $"model-arms {input.Verb}: {reason}")]);
        }

        return refusals.Count > 0
            ? Passage<IReadOnlyList<FileFacts>>.Stop(ExitCodes.Configuration, [.. refusals.Select(reason => $"model-arms {input.Verb}: {reason}")])
            : new Passage<IReadOnlyList<FileFacts>>.Through(facts);
    }

    private static IReadOnlyList<string> ProblemsOf(TrustAnswer answer) =>
        answer is TrustAnswer.Refused refused ? refused.Problems : [];
}
