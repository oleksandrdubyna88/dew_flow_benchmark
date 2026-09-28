using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bench.Cli;

/// <summary><c>bench gate aa</c> (E7, S7.2a) — every cell of a finished run against one reference cell's turn-1 prompt, by
/// its normalised shape (<see cref="GateAaCheck"/>). One line per cell; ids, hash prefixes and positions only, every detail
/// through <see cref="FailureRedaction"/> with the suites' private names — so what it prints can be copied into a public
/// record. Exit 0 every compared cell has the reference's shape · 1 a cell differs, is ambiguous, or an API reviewer left
/// no prompt · 3 an artefact or the database cannot be read · 4 asked wrongly · 5 nothing was compared, or the run is not
/// finished.</summary>
public static class GateAaCommand
{
    /// <summary><c>bench gate aa --run &lt;campaign&gt; --against &lt;cell&gt; --suite-file &lt;file&gt;[,&lt;file&gt;] --artifact-root &lt;dir&gt; --db …</c>.</summary>
    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = Flags(command);
        if (flags.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, flags);
        }

        var absent = Absent(command);
        if (absent.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, absent);
        }

        var suites = await SuitesAsync(command.List("suite-file"), cancellationToken);
        var artifacts = FileSystemGateArtifactStore.Open(command.Value("artifact-root"), TimeProvider.System, ArtifactProbe.None);

        return (suites, artifacts) switch
        {
            (Outcome<IReadOnlyList<GateSuite>>.Fail bad, _) => GateRunCommand.Refuse(error, ExitCodes.Configuration, bad.Reason),
            (_, Outcome<FileSystemGateArtifactStore>.Fail bad) => GateRunCommand.Refuse(error, ExitCodes.Configuration, FailureRedaction.Redact(bad.Reason, PrivateNames.None)),
            (Outcome<IReadOnlyList<GateSuite>>.Ok s, Outcome<FileSystemGateArtifactStore>.Ok a) => await CheckAsync(command, s.Value, a.Value, output, error, cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>One refusal at a time, in the order a person fixes them — the id that is wrong named alone.</summary>
    private static string Flags(CommandLine command) =>
        (Guid.TryParse(command.Value("run"), out _), Guid.TryParse(command.Value("against"), out _), command.List("suite-file").Count > 0,
            command.Value("artifact-root").Length > 0 && GateCliInputs.Connection(command).Length > 0) switch
        {
            (false, _, _, _) => "gate aa needs --run <campaign id> — the campaign whose cells are compared",
            (_, false, _, _) => "gate aa needs --against <reference cell id> — the cell whose turn-1 prompt is the reference",
            (_, _, false, _) => "gate aa needs --suite-file <suite.json>[,<suite.json>] — the suites of the run and of the reference",
            (_, _, _, false) => "gate aa needs --artifact-root (the folder the prompts were committed to) and --db (or BENCH_DB)",
            _ => string.Empty,
        };

    /// <summary>A named file or folder that is not there is the machine's (3). The root is checked HERE because opening the
    /// store creates a missing root, and a read-only check must never create what it was pointed at. No path is printed.</summary>
    private static string Absent(CommandLine command) =>
        (command.List("suite-file").Where(f => !File.Exists(f)).DefaultIfEmpty(string.Empty).First(), Directory.Exists(command.Value("artifact-root"))) switch
        {
            ({ Length: > 0 } missing, _) => $"the suite file {Path.GetFileName(missing)} is not there",
            (_, false) => "the artefact root is not there — pass the folder the run committed its prompts to",
            _ => string.Empty,
        };

    private static async Task<Outcome<IReadOnlyList<GateSuite>>> SuitesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var suites = new List<GateSuite>();
        foreach (var file in files)
        {
            var parsed = GateSuiteFile.Parse(await File.ReadAllTextAsync(file, cancellationToken));

            if (parsed is Outcome<GateSuite>.Fail bad)
            {
                return Outcome<IReadOnlyList<GateSuite>>.Failure($"{Path.GetFileName(file)}: {bad.Reason}");
            }

            suites.Add(((Outcome<GateSuite>.Ok)parsed).Value);
        }

        return Outcome<IReadOnlyList<GateSuite>>.Success(suites);
    }

    private static async Task<int> CheckAsync(
        CommandLine command, IReadOnlyList<GateSuite> suites, FileSystemGateArtifactStore artifacts, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            var check = new GateAaCheck(
                new PostgresGateStore(db, TimeProvider.System), artifacts, new PostgresGateReads(db, TimeProvider.System), new PostgresGateReviewerCatalog(db));
            var names = PrivateNames.Of(suites.SelectMany(s => s.PrivateNames));

            return Answer(await check.RunAsync(Guid.Parse(command.Value("run")), Guid.Parse(command.Value("against")), suites, cancellationToken), names, output, error);
        }
        catch (Exception ex) when (GateReportCommand.IsStoreFailure(ex))
        {
            // Redacted too: a driver's message can carry a host, a path or a suite's private name.
            var names = PrivateNames.Of(suites.SelectMany(s => s.PrivateNames));
            return GateRunCommand.Refuse(error, ExitCodes.Environment, FailureRedaction.Redact($"the database is unreachable — {ex.Message.Split('\n')[0]}", names));
        }
    }

    private static int Answer(AaResult result, PrivateNames names, TextWriter output, TextWriter error) => result switch
    {
        AaResult.Refused refused => GateRunCommand.Refuse(error, RefusalCode(refused.Kind), FailureRedaction.Redact(refused.Reason, names)),
        AaResult.Checked checkedRun => Printed(checkedRun, names, output),
        _ => throw new InvalidOperationException("unreachable"),
    };

    private static int RefusalCode(AaRefusalKind kind) => kind switch
    {
        AaRefusalKind.Environment => ExitCodes.Environment,
        AaRefusalKind.Unsettled => ExitCodes.NoReport,
        _ => ExitCodes.Configuration,
    };

    private static int Printed(AaResult.Checked result, PrivateNames names, TextWriter output)
    {
        foreach (var line in result.Lines)
        {
            output.WriteLine(FailureRedaction.Redact(Text(line), names));
        }

        output.WriteLine($"reference shape {result.ReferenceShape[..12]} · compared {result.ComparedCount} of {result.Lines.Count} · {Summary(result.Standing)}");
        return result.Standing switch
        {
            AaStanding.Pass => ExitCodes.Pass,
            AaStanding.Failed => ExitCodes.Regression,
            AaStanding.Unreadable => ExitCodes.Environment,
            _ => ExitCodes.NoReport,
        };
    }

    private static string Text(AaLine line) =>
        $"{line.Cell}  {line.Reviewer.Value,-28} {line.Task.Value,-6} r{line.Repeat}  raw {Hash(line.RawHash)}  shape {Hash(line.ShapeHash)}  "
        + Words(line.Verdict) + (line.Detail.Length > 0 ? $" — {line.Detail}" : string.Empty);

    private static string Hash(string prefix) => prefix.Length > 0 ? prefix : "—".PadRight(12);

    private static string Words(AaVerdict verdict) => verdict switch
    {
        AaVerdict.SameShape => "same shape",
        AaVerdict.Differs => "differs",
        AaVerdict.Ambiguous => "ambiguous fences or history lines",
        AaVerdict.ApiLeftNoPrompt => "API reviewer left no prompt",
        AaVerdict.CliNoPrompt => "CLI reviewer — no prompt by design",
        AaVerdict.SessionFailed => "session failed — not compared",
        AaVerdict.Reference => "the reference — not compared",
        AaVerdict.OtherTask => "other task — not compared",
        AaVerdict.OtherPin => "other product pin — not compared",
        AaVerdict.Unreadable => "unreadable",
        AaVerdict.HashMismatch => "not the file its settlement hashed",
        _ => throw new InvalidOperationException("unreachable"),
    };

    private static string Summary(AaStanding standing) => standing switch
    {
        AaStanding.Pass => "every compared cell has the reference's shape",
        AaStanding.Failed => "a cell differs, is ambiguous, or left no prompt — a harness defect until shown otherwise",
        AaStanding.Unreadable => "an artefact could not be read — nothing about the shapes is concluded",
        _ => "nothing was compared — this is not a pass",
    };
}
