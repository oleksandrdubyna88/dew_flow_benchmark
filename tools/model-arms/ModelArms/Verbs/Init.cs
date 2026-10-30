using System.Text;
using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary><c>init &lt;root&gt;</c> (D18): the first verb of a run. It creates ONE new folder — never writes into an existing one — and in it the template
/// <c>experiment.json</c> (the shipped example with its values blanked, <see cref="ExperimentTemplate"/>), an empty <c>scenarios.json</c> and an empty
/// <c>briefs/</c>. It does not go through <see cref="ExperimentGate"/>: there is no experiment yet to read and the root it is asked about does not exist.
/// <para>
/// The root is made where the module would later accept it, by the module's own rules and not a second set: the existence check is
/// <see cref="RootRequirements"/>'s, the placement (outside this repository and every git checkout, asked of where the root REALLY is) is
/// <see cref="ExperimentFile.Placement"/>'s, and every question about a link or a kind of file goes through <see cref="RootFacts"/>. The root is made EXCLUSIVELY
/// (libc's <c>mkdir</c> through <see cref="KernelFacts"/> on every POSIX host: anything already at the name is exit 4 and is left as it was), then its modes are SET — <c>0700</c> for the folders,
/// <c>0600</c> for the files, whatever the umask — so the gate's trust check (the operator's own, closed to group and others) passes on the first <c>validate</c>.
/// </para>
/// <para>
/// Links. A root that already is a symbolic link exists and is refused; the parent folder is refused when it is one; the root is judged again right after it is
/// made; the files are created with <c>CreateNew</c>. What is NOT closed: <c>mkdir</c> follows the links ABOVE the root, so whoever can write an ancestor of the parent can
/// swap the parent for a link between its judgement and the <c>mkdir</c> and redirect where the root is made (the gate's own ancestor residual: it does not judge ancestors
/// either). The second judgement then sees a real folder at the redirected place. Only the operator's own folders should be named as the parent.
/// </para>
/// <para>
/// Exit 0 created; 4 a refusal of the configuration (the root exists, its parent does not, it is inside a checkout, a link is in the way); 3 the host could not do it (a
/// folder it may not write in, a file that cannot be made, the example missing). A stop signal is honoured once, before anything
/// is made: a root half-made by a stop would be refused as existing by the same command run again.
/// </para>
/// <para>
/// What it does NOT close: the root is checked absent and then made — two calls. Off Windows the <c>mkdir</c> is exclusive, so two <c>init</c> processes racing for one name cannot
/// both make it and the loser is exit 4; on Windows the runtime makes the folder and a folder made at the name between the check and the creation is adopted silently (Windows already
/// declares the link and mode checks not performed).
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

    public static Task<int> RunAsync(VerbInput input) => RunAsync(input, root => MakeRootFolder(root, PlatformFactsReader.CurrentRoad));

    /// <summary>The same with the making of the root handed in: the only way a test can put a folder at the name between the existence check and the <c>mkdir</c>.</summary>
    internal static Task<int> RunAsync(VerbInput input, Func<string, FolderMade> makeRoot) => RunAsync(input, makeRoot, PlatformFactsReader.CurrentRoad);

    /// <summary>The same with the road handed in too (E3.S0): the road decides how the folders are judged and says, first of all, what this host does not verify.</summary>
    internal static Task<int> RunAsync(VerbInput input, Func<string, FolderMade> makeRoot, PlatformRoad road)
    {
        input.Stopping.ThrowIfCancellationRequested();
        var made = Decided(input, road).Bind(root => Templated(input, root)).Bind(plan => Made(input, plan, makeRoot, road));
        return Task.FromResult(made.Match(plan => Created(input, plan), stop => Stopped(input, stop)));
    }

    /// <summary>The road's line first — what this host does NOT verify, once, before anything is made and before any refusal it qualifies — then every refusal of the configuration, in the order the spec gives: the root exists, its parent does not, it is placed inside a
    /// checkout, a link is in the way.</summary>
    private static Passage<string> Decided(VerbInput input, PlatformRoad road) =>
        Said(input, road)
            .Bind(_ => NamedRoot(input))
            .Bind(root => Absent(input, root))
            .Bind(root => ParentExists(root))
            .Bind(root => Placed(input, root))
            .Bind(root => Judged(ParentOf(root), road).Map(_ => root));

    private static Passage<string> Said(VerbInput input, PlatformRoad road)
    {
        if (road.Line is { Length: > 0 } line)
        {
            input.Error.WriteLine(line);
        }

        return new Passage<string>.Through(string.Empty);
    }

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

    /// <summary>The parent must be there: a directory entry, a dangling link included — <see cref="Judged"/> then names a link, which "does not exist" would hide.</summary>
    private static Passage<string> ParentExists(string root) =>
        RootFacts.Host.Present(ParentOf(root)).Match(
            present => present ? new Passage<string>.Through(root) : Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: {ParentOf(root)} does not exist — init creates one folder, not a path"),
            reason => Passage<string>.Stop(ExitCodes.Environment, $"{Name} init: {reason}"));

    private static Passage<string> Placed(VerbInput input, string root)
    {
        var location = new ExperimentLocation(root, ExperimentGate.Home(input.Environment), RootFacts.GitTopOf(root), RootFacts.ThisRepository(AppContext.BaseDirectory));
        var problems = ExperimentFile.Placement(location);
        return problems.Count == 0 ? new Passage<string>.Through(root) : Passage<string>.Stop(ExitCodes.Configuration, [.. problems.Select(problem => $"{Name} init: {problem}")]);
    }

    private static string ParentOf(string root) => Path.GetDirectoryName(root) ?? string.Empty;

    /// <summary>The folder is a real directory — not a symbolic link, not another kind of file — judged on the road's facts (the kernel's answer or the runtime's). Where the road performs no check (Windows) nothing is judged: the check is declared not performed.</summary>
    private static Passage<string> Judged(string folder, PlatformRoad road) =>
        road.Trust == TrustRoad.NotPerformed ? new Passage<string>.Through(folder) : JudgedOnTheRoad(folder, road);

    private static Passage<string> JudgedOnTheRoad(string folder, PlatformRoad road) =>
        RootFacts.ReadFacts(folder, FileKind.Directory, road) switch
        {
            FactsRead.Read => new Passage<string>.Through(folder),
            FactsRead.Refused refused => Passage<string>.Stop(ExitCodes.Configuration, $"{Name} init: {refused.Reason}"),
            FactsRead.Unread unread => Passage<string>.Stop(ExitCodes.Environment, $"{Name} init: {unread.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

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

    /// <summary>Makes the root exclusively, judges it again (a link swapped in since is refused before a file goes through it), sets its mode, then fills it. A name that is taken is
    /// exit 4 and nothing is touched; a host that will not do any of it is exit 3, saying whether a part-made root is left to remove.</summary>
    private static Passage<Plan> Made(VerbInput input, Plan plan, Func<string, FolderMade> makeRoot, PlatformRoad road)
    {
        try
        {
            return RootMade(input, plan.Root, makeRoot).Bind(root => Judged(root, road)).Bind(root => OwnerOnly(root, OwnerOnlyFolder)).Bind(_ => Filled(plan));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<Plan>.Stop(ExitCodes.Environment, $"{Name} init: cannot create {plan.Root}: {ex.Message}{LeftBehind(plan.Root)}");
        }
    }

    private static Passage<string> RootMade(VerbInput input, string root, Func<string, FolderMade> makeRoot) =>
        makeRoot(root) switch
        {
            FolderMade.Made => new Passage<string>.Through(root),
            FolderMade.AlreadyThere => Unmet(input, root, present: true),
            FolderMade.Failed failed => Passage<string>.Stop(ExitCodes.Environment, $"{Name} init: cannot create {root}: {failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>libc's exclusive <c>mkdir</c> where the road has it (every POSIX host); on Windows there are no modes and no such call, so the folder is made by the runtime (the existence check has just passed).</summary>
    private static FolderMade MakeRootFolder(string root, PlatformRoad road)
    {
        if (road.LibcMkdir)
        {
            return KernelFacts.MakeFolder(root);
        }

        Directory.CreateDirectory(root);
        return new FolderMade.Made();
    }

    private static Passage<Plan> Filled(Plan plan)
    {
        var paths = new RootPaths(plan.Root);
        MakeSubfolder(paths.Briefs);
        WriteNew(paths.ExperimentFile, plan.Template);
        WriteNew(Path.Combine(plan.Root, ScenariosFile.FileName), EmptyScenarios);
        return new Passage<Plan>.Through(plan);
    }

    private static string LeftBehind(string root) =>
        Directory.Exists(root) ? $" — init stopped part-way: remove {root} and run init again" : string.Empty;

    /// <summary>The mode set explicitly: the mode given at creation is narrowed by the umask, and a umask that took the owner's bits would leave a folder <c>init</c> could not fill.
    /// Off Windows only.</summary>
    private static Passage<string> OwnerOnly(string folder, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(folder, mode);
        }

        return new Passage<string>.Through(folder);
    }

    /// <summary>One new folder below the root, owner-only whatever the umask.</summary>
    private static void MakeSubfolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, OwnerOnlyFolder);
        File.SetUnixFileMode(path, OwnerOnlyFolder);
    }

    /// <summary>One new file, created exclusively: an existing file — or a link at that name — is an error and never written through. Its mode is set on the open handle, before the first byte,
    /// whatever the umask.</summary>
    private static void WriteNew(string path, string text)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        using var stream = new FileStream(path, options);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(stream.SafeFileHandle, OwnerOnlyFile);
        }

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
