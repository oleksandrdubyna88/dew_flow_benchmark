using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Persistence;

/// <summary>Domain ↔ row for the probe tables. A row is a fact about the past, so reading one back re-parses what it holds —
/// the subject ids, the oracle, the artefact paths — rather than trusting them: a hand-edited row is refused by name.</summary>
internal static class ProbeRowMapping
{
    public static ProbeRunRow ToRow(ProbeRun run) => new()
    {
        Id = run.Id,
        CreatedAt = run.CreatedAt,
        OracleVersion = run.Oracle.Version,
        OracleSource = run.Oracle.Source,
        Repeats = run.Repeats,
        ArtifactsPruned = run.ArtifactsPruned,
        SubjectIds = [.. run.Subjects.Select(s => s.Id.Value)],
        SubjectRuntimes = [.. run.Subjects.Select(s => s.Runtime.ToString())],
        SubjectModels = [.. run.Subjects.Select(s => s.ModelId)],
        SubjectExecutableRefs = [.. run.Subjects.Select(s => s.ExecutableRef)],
        SubjectVendors = [.. run.Subjects.Select(s => s.Vendor)],
        SubjectEndpoints = [.. run.Subjects.Select(s => s.Endpoint)],
        SubjectDialects = [.. run.Subjects.Select(s => s.Dialect)],
        SubjectConfinements = [.. run.Subjects.Select(s => s.Confinement.ToString())],
        Probes = [.. run.Probes.Select(p => p.ToString())],
    };

    public static Outcome<ProbeRun> ToDomain(ProbeRunRow row)
    {
        if (!SameLength(
                row.SubjectIds.Count, row.SubjectRuntimes.Count, row.SubjectModels.Count, row.SubjectExecutableRefs.Count,
                row.SubjectVendors.Count, row.SubjectEndpoints.Count, row.SubjectDialects.Count, row.SubjectConfinements.Count))
        {
            return Outcome<ProbeRun>.Failure($"probe run {row.Id}: its eight subject columns disagree in length — the row was edited");
        }

        return ProbeOracle.Parse(row.OracleVersion, row.OracleSource).Match(
            oracle => Subjects(row).Match(
                subjects => Probes(row).Match(
                    probes => Outcome<ProbeRun>.Success(new ProbeRun(row.Id, oracle, subjects, row.Repeats, row.CreatedAt) { ArtifactsPruned = row.ArtifactsPruned, Probes = probes }),
                    Outcome<ProbeRun>.Failure),
                Outcome<ProbeRun>.Failure),
            Outcome<ProbeRun>.Failure);
    }

    /// <summary>The asked probes back from their enum NAMES; a name that is no probe was written by hand, and is refused by name.</summary>
    private static Outcome<IReadOnlyList<ProbeKind>> Probes(ProbeRunRow row)
    {
        var unknown = row.Probes.FirstOrDefault(name => !Enum.TryParse<ProbeKind>(name, ignoreCase: false, out var kind) || !Enum.IsDefined(kind));

        return unknown is null
            ? Outcome<IReadOnlyList<ProbeKind>>.Success([.. row.Probes.Select(Enum.Parse<ProbeKind>)])
            : Outcome<IReadOnlyList<ProbeKind>>.Failure($"probe run {row.Id}: '{unknown}' is not a probe — the row was edited");
    }

    /// <summary>Parallel list columns are read only when every list has the same length — a row where they do not was edited.</summary>
    private static bool SameLength(params int[] counts) => counts.Distinct().Count() == 1;

    private static Outcome<IReadOnlyList<ProbeSubject>> Subjects(ProbeRunRow row)
    {
        var subjects = new List<ProbeSubject>();

        foreach (var i in Enumerable.Range(0, row.SubjectIds.Count))
        {
            var subject = ProbeSubject.Parse(
                row.SubjectIds[i], RuntimeWord(row.SubjectRuntimes[i]), row.SubjectModels[i], row.SubjectExecutableRefs[i], ConfinementWord(row.SubjectConfinements[i]),
                row.SubjectVendors[i], row.SubjectEndpoints[i], row.SubjectDialects[i]);

            if (subject is Outcome<ProbeSubject>.Fail fail)
            {
                return Outcome<IReadOnlyList<ProbeSubject>>.Failure($"probe run {row.Id}: {fail.Reason}");
            }

            subjects.Add(((Outcome<ProbeSubject>.Ok)subject).Value);
        }

        return Outcome<IReadOnlyList<ProbeSubject>>.Success(subjects);
    }

    /// <summary>A stored enum NAME back to its word; anything else is handed to the parser as it is, to be refused by name.</summary>
    private static string RuntimeWord(string stored) =>
        Enum.TryParse<ProbeRuntime>(stored, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? ProbeRuntimeWord.Of(parsed) : stored;

    private static string ConfinementWord(string stored) =>
        Enum.TryParse<ProbeConfinement>(stored, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? ProbeConfinementWord.Of(parsed) : stored;

    public static ProbeCellRow ToRow(ProbeCell cell)
    {
        var row = new ProbeCellRow
        {
            Id = cell.Id,
            RunId = cell.RunId,
            Probe = cell.Probe,
            SubjectId = cell.Subject.Value,
            Repeat = cell.Repeat,
            Generation = cell.Generation,
            Slot = cell.Slot,
            Position = cell.Position,
            State = cell.State,
            Attempts = cell.Attempts,
            Owner = cell.Owner.Label,
            OwnerHost = cell.Owner.Host,
            OwnerPid = cell.Owner.Pid,
            ClaimedAt = cell.ClaimedAt,
            PinBinarySha256 = cell.Pin.BinarySha256,
            PinVersionText = cell.Pin.VersionText,
            PinGitSha = cell.Pin.GitSha,
            PinDirtyCaptured = cell.Pin.DirtyFiles.WasCaptured,
            PinDirtyFiles = cell.Pin.DirtyFiles.Value,
            PinCheckedTree = cell.Pin.CheckedTree,
            Reason = cell.Reason,
        };

        Apply(row, cell.Facts, cell.Artifacts);
        return row;
    }

    /// <summary>Writes facts and artefacts onto a row — the half of a settle that is not the guarded state change, and the
    /// same code the planner's row goes through, so one column list exists.</summary>
    public static void Apply(ProbeCellRow row, ProbeFacts facts, IReadOnlyList<ProbeArtifact> artifacts)
    {
        row.AttemptKind = facts.Kind;
        row.ExitCodeCaptured = facts.ExitCode.WasCaptured;
        row.ExitCode = facts.ExitCode.Value;
        row.CanaryRead = facts.CanaryRead;
        row.ReadAttempted = facts.ReadAttempted;
        row.AnswerCurrent = facts.AnswerCurrent;
        row.ToolEvidence = facts.ToolEvidence;
        row.ShellUsed = facts.ShellUsed;
        row.ReaderOffered = facts.ReaderOffered;
        row.Reachable = facts.Reachable;
        row.AccountOut = facts.AccountOut;
        row.ArtifactKinds = [.. artifacts.Select(a => a.Kind.ToString())];
        row.ArtifactPaths = [.. artifacts.Select(a => a.Path.Value)];
        row.ArtifactSha256s = [.. artifacts.Select(a => a.Sha256)];
        row.ArtifactLengths = [.. artifacts.Select(a => a.Length)];
    }

    public static Outcome<ProbeCell> ToDomain(ProbeCellRow row) =>
        ProbeSubjectId.Parse(row.SubjectId).Match(
            subject => Artifacts(row).Match(
                artifacts => Outcome<ProbeCell>.Success(new ProbeCell(
                    row.Id,
                    row.RunId,
                    row.Probe,
                    subject,
                    row.Repeat,
                    row.Generation,
                    row.Slot,
                    row.Position,
                    Claimable.Stored(row.State, row.Attempts, WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid), row.ClaimedAt),
                    GateRowMapping.Pin(row.PinBinarySha256, row.PinVersionText, row.PinGitSha, row.PinDirtyCaptured, row.PinDirtyFiles, row.PinCheckedTree),
                    Facts(row),
                    artifacts,
                    row.Reason)),
                Outcome<ProbeCell>.Failure),
            Outcome<ProbeCell>.Failure);

    /// <summary>A cell nobody attempted reads back as exactly <see cref="ProbeFacts.None"/>; any other row's uncaptured exit code
    /// reads back with the stored reason, as every not-captured count does.</summary>
    private static ProbeFacts Facts(ProbeCellRow row) =>
        row.AttemptKind == ProbeAttemptKind.None
            ? ProbeFacts.None
            : new ProbeFacts(
                row.AttemptKind,
                row.ExitCodeCaptured ? CapturedCount.Number(row.ExitCode) : CapturedCount.Unavailable(GateRowMapping.StoredNotCaptured),
                row.CanaryRead,
                row.ReadAttempted,
                row.AnswerCurrent,
                row.ToolEvidence,
                row.ShellUsed,
                row.ReaderOffered,
                row.Reachable,
                row.AccountOut);

    private static Outcome<IReadOnlyList<ProbeArtifact>> Artifacts(ProbeCellRow row)
    {
        if (!SameLength(row.ArtifactKinds.Count, row.ArtifactPaths.Count, row.ArtifactSha256s.Count, row.ArtifactLengths.Count))
        {
            return Outcome<IReadOnlyList<ProbeArtifact>>.Failure($"probe cell {row.Id}: its four artefact columns disagree in length — the row was edited");
        }

        var artifacts = new List<ProbeArtifact>();

        foreach (var i in Enumerable.Range(0, row.ArtifactKinds.Count))
        {
            var artifact = ProbeArtifact.Stored(row.ArtifactKinds[i], row.ArtifactPaths[i], row.ArtifactSha256s[i], row.ArtifactLengths[i]);

            if (artifact is Outcome<ProbeArtifact>.Fail fail)
            {
                return Outcome<IReadOnlyList<ProbeArtifact>>.Failure($"probe cell {row.Id}: {fail.Reason}");
            }

            artifacts.Add(((Outcome<ProbeArtifact>.Ok)artifact).Value);
        }

        return Outcome<IReadOnlyList<ProbeArtifact>>.Success(artifacts);
    }
}
