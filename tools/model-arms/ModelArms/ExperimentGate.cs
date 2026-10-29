using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms;

/// <summary>The one road in for every verb that reads an experiment (D4 read once, D17 a, D18). A verb is started only after the root is named, has
/// the files its stage needs, belongs to the operator and is closed to group and others, and its <c>experiment.json</c> is valid; each refusal names
/// what it refused and ends the walk, so a later check never runs on what an earlier one refused. Exit 4 is a wrong configuration, exit 3 is a host
/// that could not say (a <c>stat</c> or <c>id</c> that failed, a file that could not be read).
/// <para>
/// The root is normalised once (<c>GetFullPath</c>, the trailing separator trimmed) and that text is what the trust check stats; the facts it returns
/// carry the normalised path whose owner and mode were read, and <c>experiment.json</c> is read from that path — never from the operator's spelling again.
/// </para></summary>
public static class ExperimentGate
{
    private const string RootVariable = "MODEL_ARMS_EXPERIMENT";
    private const int Environment = 3;
    private const int Configuration = 4;

    /// <summary>What step 3 leaves for step 4: the root, and the path of <c>experiment.json</c> exactly as the trust check read it.</summary>
    private sealed record Judged(string Root, string ExperimentFile);

    public static async Task<int> RunAsync(VerbInput input, Func<ExperimentInput, Task<int>> verb)
    {
        var admitted = await Admit(input);
        return await admitted.Match(verb, stop => Report(input, stop));
    }

    private static Task<Passage<ExperimentInput>> Admit(VerbInput input) =>
        Task.FromResult(NamedRoot(input))
            .Then(root => Present(input, root))
            .ThenAsync(root => TrustedRoot(input, root))
            .ThenAsync(judged => ReadExperiment(input, judged))
            .Then(admitted => Needed(admitted))
            .ThenAsync(admitted => TrustedScenarios(admitted));

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
            "" => Passage<string>.Stop(Configuration, $"model-arms {input.Verb}: no experiment root — pass --experiment <root> or set {RootVariable}"),
            _ when ExecutableRule.HoldsNul(named) => Passage<string>.Stop(Configuration, $"model-arms {input.Verb}: the experiment root '{ExecutableRule.Printable(named)}' holds a NUL character"),
            _ => new Passage<string>.Through(Path.TrimEndingDirectorySeparator(Path.GetFullPath(named))),
        };
    }

    /// <summary>Step 2: the root and its <c>experiment.json</c> exist.</summary>
    private static Passage<string> Present(VerbInput input, string root) =>
        Unmet(input, RootRequirements.ToRead, root, root);

    /// <summary>Step 3: the root and <c>experiment.json</c> are the operator's own and closed to group and others.</summary>
    private static async Task<Passage<Judged>> TrustedRoot(VerbInput input, string root)
    {
        var experiment = new RootPaths(root).ExperimentFile;
        var judged = await Judge(input, root, [root, experiment]);
        return judged.Map(paths => new Judged(root, paths[1]));
    }

    /// <summary>Step 4: <c>experiment.json</c>, read from the path the trust check judged, through every refusal of D4.</summary>
    private static async Task<Passage<ExperimentInput>> ReadExperiment(VerbInput input, Judged judged)
    {
        var text = await TextOf(judged.ExperimentFile);
        return text.Match(
            json => Parsed(input, judged.Root, json),
            reason => Passage<ExperimentInput>.Stop(Environment, $"model-arms {input.Verb}: {reason}"));
    }

    /// <summary>Step 5: the files the verb's stage needs, scenarios and baseline by the names the experiment configured.</summary>
    private static Passage<ExperimentInput> Needed(ExperimentInput admitted)
    {
        var experiment = admitted.Experiment;
        var requirements = RootRequirements.For(admitted.Verb.Verb, ArmsOf(admitted.Verb), experiment.ScenariosFile, experiment.BaselineFile);
        return Unmet(admitted.Verb, requirements, admitted.Root.Root, admitted);
    }

    /// <summary>Step 6: <c>scenarios.json</c>, when the root has one, is trusted as the root is. Verbs that do not need it do not require it.</summary>
    private static async Task<Passage<ExperimentInput>> TrustedScenarios(ExperimentInput admitted)
    {
        var scenarios = admitted.Root.Of(admitted.Experiment.ScenariosFile);
        return await scenarios.Match(
            path => PathExists(path) ? TrustedFile(admitted, path) : Task.FromResult<Passage<ExperimentInput>>(new Passage<ExperimentInput>.Through(admitted)),
            reason => Task.FromResult(Passage<ExperimentInput>.Stop(Configuration, $"model-arms {admitted.Verb.Verb}: {reason}")));
    }

    private static async Task<Passage<ExperimentInput>> TrustedFile(ExperimentInput admitted, string path) =>
        (await Judge(admitted.Verb, admitted.Root.Root, [path])).Map(_ => admitted);

    private static Passage<T> Unmet<T>(VerbInput input, IReadOnlyList<RootRequirement> requirements, string root, T carried)
    {
        var sentence = RootRequirements.FirstUnmet(requirements, root, PathExists);
        return sentence.Length == 0
            ? new Passage<T>.Through(carried)
            : Passage<T>.Stop(Configuration, $"model-arms {input.Verb}: {sentence}");
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
                Configuration,
                [$"model-arms {input.Verb}: experiment.json is refused:", .. reason.Split('\n').Select(problem => $"  {problem}")]));
    }

    private static string Home(IReadOnlyDictionary<string, string> environment) =>
        environment.GetValueOrDefault("HOME", string.Empty) is { Length: > 0 } home ? home : environment.GetValueOrDefault("USERPROFILE", string.Empty);

    /// <summary>The file's text, or why the host could not give it.</summary>
    private static async Task<Outcome<string>> TextOf(string path)
    {
        try
        {
            return Outcome<string>.Success(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"{path} cannot be read: {ex.Message}");
        }
    }

    /// <summary>D17 a over <paramref name="files"/>: off POSIX the printed line says the check was not performed and the walk goes on; on POSIX the
    /// facts are read (a <c>stat</c> or <c>id</c> that fails stops with exit 3), a link is refused, and the owner and mode are judged (exit 4, every
    /// problem printed). The answer is the normalised paths whose facts were read, in the order given.</summary>
    private static async Task<Passage<IReadOnlyList<string>>> Judge(VerbInput input, string root, IReadOnlyList<string> files)
    {
        if (OperatingSystem.IsWindows())
        {
            input.Error.WriteLine(((TrustAnswer.NotPerformed)RootTrust.Decide(false, 0, [], root)).Line);
            return new Passage<IReadOnlyList<string>>.Through(files);
        }

        var uid = await RootFacts.CurrentUidAsync(input.Stopping);
        return await uid.Match(
            id => JudgeFacts(input, root, files, id),
            reason => Task.FromResult(Passage<IReadOnlyList<string>>.Stop(Environment, $"model-arms {input.Verb}: {reason}")));
    }

    private static async Task<Passage<IReadOnlyList<string>>> JudgeFacts(VerbInput input, string root, IReadOnlyList<string> files, int uid)
    {
        var reads = new List<FactsRead>();
        foreach (var file in files)
        {
            reads.Add(await RootFacts.ReadFactsAsync(file, input.Stopping));
        }

        var unread = reads.OfType<FactsRead.Unread>().Select(read => read.Reason).ToList();
        var facts = reads.OfType<FactsRead.Read>().Select(read => read.Facts).ToList();
        IReadOnlyList<string> refusals = [.. reads.OfType<FactsRead.Refused>().Select(read => read.Reason), .. ProblemsOf(RootTrust.Decide(true, uid, facts, root))];
        if (unread.Count > 0)
        {
            return Passage<IReadOnlyList<string>>.Stop(Environment, [.. unread.Select(reason => $"model-arms {input.Verb}: {reason}")]);
        }

        return refusals.Count > 0
            ? Passage<IReadOnlyList<string>>.Stop(Configuration, [.. refusals.Select(reason => $"model-arms {input.Verb}: {reason}")])
            : new Passage<IReadOnlyList<string>>.Through([.. facts.Select(fact => fact.Path)]);
    }

    private static IReadOnlyList<string> ProblemsOf(TrustAnswer answer) =>
        answer is TrustAnswer.Refused refused ? refused.Problems : [];
}
