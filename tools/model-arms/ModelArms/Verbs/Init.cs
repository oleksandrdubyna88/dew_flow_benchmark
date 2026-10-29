using System.Text;
using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary><c>init &lt;root&gt;</c> (D18): the first verb of a run. It creates ONE new folder — never writes into an existing one — and in it the template
/// <c>experiment.json</c> (the shipped example with its values blanked, <see cref="ExperimentTemplate"/>), an empty <c>scenarios.json</c> and an empty
/// <c>briefs/</c>. It does not go through <see cref="ExperimentGate"/>: there is no experiment yet to read, and the root it is asked about does not exist.
/// <para>
/// The root is made where the module would later accept it, by the module's own rules and not a second set: the existence check is
/// <see cref="RootRequirements"/>'s, the placement (outside this repository and every git checkout) is <see cref="ExperimentFile.Placement"/>'s, and every
/// question about a link or a kind of file goes through <see cref="RootFacts"/>. The folders are made <c>0700</c> and the files <c>0600</c> off Windows, so the
/// gate's trust check (the operator's own, closed to group and others) passes on the first <c>validate</c>. A symbolic link is never followed: a root that
/// already is one exists and is refused, a parent folder that is one is refused, and the root is judged again right after it is made.
/// </para>
/// <para>
/// Exit 0 created; 4 a refusal of the configuration (the root exists, its parent does not, it is inside a checkout, a link is in the way); 3 the host could
/// not do it (a folder it may not write in, a file that cannot be made, the example missing). A stop signal is honoured once, before anything is made: a root
/// half-made by a stop would be refused as existing by the same command run again.
/// </para>
/// <para>
/// What it does NOT close: the root is checked absent and then made — two calls, so two <c>init</c> processes racing for one name can both pass the check; the files
/// are created with <c>CreateNew</c>, so neither overwrites the other. An ANCESTOR above the parent folder may be a link (the gate allows one too: a WSL home usually is).
/// </para></summary>
public static class InitVerb
{
    private const string Name = "model-arms";
    private const UnixFileMode OwnerOnlyFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const string EmptyScenarios = "[]\n";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The root and the template text, once every refusal has been passed.</summary>
    private sealed record Plan(string Root, string Template);

    public static Task<int> RunAsync(VerbInput input)
    {
        input.Stopping.ThrowIfCancellationRequested();
        var made = Decided(input).Bind(root => Templated(input, root)).Bind(plan => Made(input, plan));
        return Task.FromResult(made.Match(plan => Created(input, plan), stop => Stopped(input, stop)));
    }

    /// <summary>Every refusal of the configuration, in the order the spec gives: the root exists, its parent does not, it is placed inside a checkout, a link is in the way.</summary>
    private static Passage<string> Decided(VerbInput input) =>
        NamedRoot(input)
            .Bind(root => Absent(input, root))
            .Bind(root => ParentExists(root))
            .Bind(root => Placed(input, root))
            .Bind(root => Judged(input, ParentOf(root)).Map(_ => root));

    private static Passage<string> NamedRoot(VerbInput input)
    {
        var named = input.Positionals[0];
        return named switch
        {
            "" => Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: the root is an empty name — pass the folder to create"),
            _ when ExecutableRule.HoldsNul(named) => Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: the root '{ExecutableRule.Printable(named)}' holds a NUL character"),
            _ => new Passage<string>.Through(Path.TrimEndingDirectorySeparator(Path.GetFullPath(named))),
        };
    }

    /// <summary>The root must not be there. "There" is a directory entry by that name — a dangling link counts, which <c>File.Exists</c> and <c>Directory.Exists</c> would not say.</summary>
    private static Passage<string> Absent(VerbInput input, string root) =>
        RootFacts.Host.Present(root).Match(
            present => Unmet(input, root, present),
            reason => Passage<string>.Stop(ExitCodes.Environment, $"{Name} init: {reason}"));

    private static Passage<string> Unmet(VerbInput input, string root, bool present)
    {
        var sentence = RootRequirements.FirstUnmet(RootRequirements.For(input.Verb, [], string.Empty, string.Empty), root, _ => present);
        return sentence.Length == 0 ? new Passage<string>.Through(root) : Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: {sentence}");
    }

    private static Passage<string> ParentExists(string root) =>
        Directory.Exists(ParentOf(root))
            ? new Passage<string>.Through(root)
            : Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: {ParentOf(root)} does not exist — init creates one folder, not a path");

    private static Passage<string> Placed(VerbInput input, string root)
    {
        var location = new ExperimentLocation(root, ExperimentGate.Home(input.Environment), RootFacts.GitTopOf(root), RootFacts.ThisRepository(AppContext.BaseDirectory));
        var problems = ExperimentFile.Placement(location);
        return problems.Count == 0 ? new Passage<string>.Through(root) : Passage<string>.Stop(ExitCodes.Configuration, [.. problems.Select(problem => $"{Name} init: {problem}")]);
    }

    private static string ParentOf(string root) => Path.GetDirectoryName(root) ?? string.Empty;

    /// <summary>The folder is a real directory — not a symbolic link, not another kind of file — judged by the kernel's own answer. Off Windows only: there the check is declared not performed.</summary>
    private static Passage<string> Judged(VerbInput input, string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            return new Passage<string>.Through(folder);
        }

        return RootFacts.ReadFacts(folder, FileKind.Directory) switch
        {
            FactsRead.Read => new Passage<string>.Through(folder),
            FactsRead.Refused refused => Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: {refused.Reason}"),
            FactsRead.Unread unread => Passage<string>.Stop(ExitCodes.Environment, $"{Name} init: {unread.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The template, read before anything is made, so a missing example leaves nothing behind.</summary>
    private static Passage<Plan> Templated(VerbInput input, string root)
    {
        try
        {
            return new Passage<Plan>.Through(new Plan(root, ExperimentTemplate.From(File.ReadAllText(input.Module.ExampleFile))));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<Plan>.Stop(ExitCodes.Environment, $"{Name} init: the shipped example {input.Module.ExampleFile} cannot be read: {ex.Message}");
        }
    }

    /// <summary>Makes the root, judges it again (a link swapped in since the existence check is refused before a file is written through it), then fills it. A host that
    /// will not do any of it is exit 3, and says whether a part-made root is left to remove.</summary>
    private static Passage<Plan> Made(VerbInput input, Plan plan)
    {
        try
        {
            MakeFolder(plan.Root);
            return Judged(input, plan.Root).Bind(_ => Filled(plan));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<Plan>.Stop(ExitCodes.Environment, $"{Name} init: cannot create {plan.Root}: {ex.Message}{LeftBehind(plan.Root)}");
        }
    }

    private static Passage<Plan> Filled(Plan plan)
    {
        var paths = new RootPaths(plan.Root);
        MakeFolder(paths.Briefs);
        WriteNew(paths.ExperimentFile, plan.Template);
        WriteNew(Path.Combine(plan.Root, ScenariosFile.FileName), EmptyScenarios);
        return new Passage<Plan>.Through(plan);
    }

    private static string LeftBehind(string root) =>
        Directory.Exists(root) ? $" — init stopped part-way: remove {root} and run init again" : string.Empty;

    /// <summary>One new folder, owner-only off Windows. The mode is asked for at creation, so there is no moment the folder is open to others.</summary>
    private static void MakeFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, OwnerOnlyFolder);
    }

    /// <summary>One new file, owner-only off Windows, created exclusively: an existing file — or a link at that name — is an error and never written through.</summary>
    private static void WriteNew(string path, string text)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnlyFile;
        }

        using var stream = new FileStream(path, options);
        stream.Write(Utf8WithoutBom.GetBytes(text));
    }

    private static int Created(VerbInput input, Plan plan)
    {
        if (OperatingSystem.IsWindows())
        {
            input.Error.WriteLine($"{Name} init: the symbolic-link and owner-only checks were not performed — POSIX only (D17)");
        }

        input.Output.WriteLine(
            $"created {plan.Root}: experiment.json (a template — replace every {ExperimentTemplate.Blank}), scenarios.json (empty), briefs/ (template overrides by name). "
            + $"Next: {Name} validate --experiment {plan.Root}");
        return ExitCodes.Pass;
    }

    private static int Stopped(VerbInput input, Passage<Plan>.Stopped stop)
    {
        foreach (var line in stop.Lines)
        {
            input.Error.WriteLine(line);
        }

        return stop.ExitCode;
    }
}
