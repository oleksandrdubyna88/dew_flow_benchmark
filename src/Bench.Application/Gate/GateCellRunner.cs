using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Application.Gate;

/// <summary>How the driver runs, for every cell of a campaign.</summary>
/// <param name="CellTimeout">One ABSOLUTE budget for the whole cell — handshake, every call, the resolves; at it the
/// session is killed and the attempt is settled failed, cause <see cref="FailureKind.Interrupted"/>.</param>
/// <param name="Tap">Whether an <c>api</c> reviewer's calls go through the recording tap (on by default).</param>
/// <param name="TapWait">How long a finished session waits for calls still in flight before the tap closes and marks them.</param>
public sealed record GateDriverSettings(
    string ProductExecutable,
    string ArtifactRoot,
    bool Tap,
    TimeSpan CellTimeout,
    TimeSpan HandshakeTimeout,
    TimeSpan TapWait,
    IReadOnlyDictionary<string, string> ParentEnvironment,
    PrivateNames PrivateNames);

/// <summary>One claimed cell and everything it is run with.</summary>
public sealed record GateCellWork(GateRun Run, GateCell Cell, GateReviewer Reviewer, GateTask Task, GateRunSettings RunSettings, FileHashKey Key, WorkerIdentity Owner);

/// <summary>Runs ONE claimed cell attempt end to end and settles it — a port of the calibration harness's <c>one_run</c>.
/// <para>
/// Begin a fresh attempt directory (earlier ones are marked interrupted, never continued) → the run's own ref in the
/// gate's clone → the reviewer's references and the vault key → the tap for an <c>api</c> row → the environment (the
/// secret last) → ONE product process in the lane's slot → the gate's protocol under the cell's absolute deadline → the
/// process gone → the tap closed with every call answered or marked → the ledger, stderr, session config and tap read
/// back → facts, findings (hashes to the database, text to the artefact root), the settings check → every artefact
/// committed with the run record LAST → the settle. A session that broke is a FAILED cell with its cause; a session
/// that ended with a verdict nobody may build on is a completed, INVALID run.
/// </para></summary>
public sealed class GateCellRunner(
    IGateStore store,
    IGateArtifactStore artifacts,
    IMcpSessionFactory sessions,
    IRecordingTapFactory taps,
    IGateCheckouts checkouts,
    IGateSecrets secrets,
    IGateAttemptFiles files,
    TimeProvider clock,
    GateDriverSettings settings)
{
    public async Task<Outcome<GateCell>> RunAsync(GateCellWork work, LaneSlot slot, CancellationToken cancellationToken)
    {
        var scope = ArtifactScope.Of(work.Run, work.Cell);

        if (scope is Outcome<ArtifactScope>.Fail refused)
        {
            return Outcome<GateCell>.Failure(refused.Reason);
        }

        var attempt = ((Outcome<ArtifactScope>.Ok)scope).Value;
        var deadline = clock.GetUtcNow() + settings.CellTimeout;

        // Everything that can refuse BEFORE launch is decided before the attempt directory exists: a cell that could not
        // be prepared — a reference or the key unset, the checkout, the plan, the product binary not there — was never
        // measured. It is handed back AT ONCE, its attempt given back with it (never a step toward Abandoned), the cause
        // recorded on the cell, and the leg refused so the drain's breaker still ends a dead environment.
        var ready = await ReadyAsync(work, attempt, cancellationToken);
        if (ready is Outcome<Ready>.Fail notReady)
        {
            return await HandBackAsync(work, notReady.Reason, cancellationToken);
        }

        var begun = await artifacts.BeginAttemptAsync(attempt, cancellationToken);
        if (begun is Outcome<ArtifactPath>.Fail notBegun)
        {
            return Outcome<GateCell>.Failure($"the attempt directory could not be begun — {FailureRedaction.Redact(notBegun.Reason, settings.PrivateNames)}");
        }

        var prepared = await EnvironmentAsync(work, attempt, ((Outcome<Ready>.Ok)ready).Value, cancellationToken);

        return prepared switch
        {
            Outcome<Prepared>.Ok ok => await DriveAsync(work, attempt, ok.Value, slot, deadline, cancellationToken),
            Outcome<Prepared>.Fail fail => await CompleteAsync(work, attempt, Evidence.Empty, Failed(FailureKind.Unexplained, fail.Reason, work), cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private async Task<Outcome<GateCell>> HandBackAsync(GateCellWork work, string reason, CancellationToken cancellationToken)
    {
        var cause = FailureRedaction.Redact($"refused before launch: {reason}", settings.PrivateNames);
        await store.HandBackUnmeasuredAsync(work.Cell.Id, work.Owner, work.Cell.Attempts, cause, cancellationToken);

        return Outcome<GateCell>.Failure($"the cell was not measured and was handed back — {cause}");
    }

    /// <summary>What was resolved before the attempt began.</summary>
    private sealed record Ready(string Checkout, string Branch, string PlanText, ResolvedReferences References, SecretValue Key);

    /// <summary>Everything decided before the product starts.</summary>
    private sealed record Prepared(string Checkout, string Branch, string PlanText, CoaiEnvironment Environment, ChildEnvironment Child, IRecordingTap Tap, string ReferencesHash);

    private async Task<Outcome<Ready>> ReadyAsync(GateCellWork work, ArtifactScope scope, CancellationToken cancellationToken)
    {
        if (!files.Exists(settings.ProductExecutable))
        {
            return Outcome<Ready>.Failure($"the product binary {Path.GetFileName(settings.ProductExecutable)} is not there — it could not be started");
        }

        var clone = await checkouts.EnsureAsync(work.Run.Id, work.Task, cancellationToken);
        if (clone is Outcome<string>.Fail noClone)
        {
            return Outcome<Ready>.Failure(noClone.Reason);
        }

        var path = ((Outcome<string>.Ok)clone).Value;
        var branch = RunRef(work, scope.Attempt);
        var plan = files.ReadPlan(path, work.Task.Case.PlanPath);
        var references = secrets.References(work.Reviewer);
        var key = NeedsVault(work.Reviewer) ? secrets.CredsKey(work.Reviewer) : Outcome<SecretValue>.Success(SecretValue.None);

        var refusal = (plan, references, key) switch
        {
            (Outcome<string>.Fail f, _, _) => f.Reason,
            (_, Outcome<ResolvedReferences>.Fail f, _) => f.Reason,
            (_, _, Outcome<SecretValue>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<Ready>.Failure(refusal);
        }

        if (work.Run.Gate != GateKind.Feature && await checkouts.CreateRefAsync(path, branch, work.Task, cancellationToken) is Outcome<string>.Fail noRef)
        {
            return Outcome<Ready>.Failure(noRef.Reason);
        }

        return Outcome<Ready>.Success(new Ready(
            path, branch, ((Outcome<string>.Ok)plan).Value, ((Outcome<ResolvedReferences>.Ok)references).Value, ((Outcome<SecretValue>.Ok)key).Value));
    }

    private async Task<Outcome<Prepared>> EnvironmentAsync(GateCellWork work, ArtifactScope scope, Ready ready, CancellationToken cancellationToken)
    {
        var (clone, branch, planText, resolved, key) = (ready.Checkout, ready.Branch, ready.PlanText, ready.References, ready.Key);
        var tap = await TapAsync(work, scope, resolved, cancellationToken);
        if (tap is Outcome<IRecordingTap>.Fail noTap)
        {
            return Outcome<Prepared>.Failure(noTap.Reason);
        }

        var recorder = ((Outcome<IRecordingTap>.Ok)tap).Value;
        var routes = recorder.Endpoint.Length == 0 ? EndpointRoutes.None : EndpointRoutes.Through(work.Reviewer.Id, recorder.Endpoint);
        var vendors = CoaiVendorsSetting.From([work.Reviewer], work.Run.Gate, resolved, routes);

        if (vendors is Outcome<CoaiVendorsSetting>.Fail noVendors)
        {
            await recorder.DisposeAsync();
            return Outcome<Prepared>.Failure(noVendors.Reason);
        }

        var environment = CoaiEnvironment.For(
            new CoaiEnvironmentInputs(scope, work.Reviewer, work.Task.Id, work.Cell.Repeat, work.RunSettings, settings.ParentEnvironment, settings.ArtifactRoot),
            ((Outcome<CoaiVendorsSetting>.Ok)vendors).Value);

        return Outcome<Prepared>.Success(new Prepared(clone, branch, planText, environment, environment.WithSecret(key), recorder, resolved.Hash));
    }

    /// <summary>The tap sits in front of an <c>api</c> reviewer whose endpoint is known; a CLI reviewer has no HTTP.</summary>
    private async Task<Outcome<IRecordingTap>> TapAsync(GateCellWork work, ArtifactScope scope, ResolvedReferences resolved, CancellationToken cancellationToken)
    {
        var d = work.Reviewer.Definition;
        var upstream = d.Endpoint switch
        {
            ReviewerEndpoint.Value v => v.Url,
            ReviewerEndpoint.Reference r when resolved.TryGet(r.Name, out var url) => url,
            _ => string.Empty,
        };

        if (!settings.Tap || d.Runtime != ReviewerRuntime.Api || upstream.Length == 0)
        {
            return Outcome<IRecordingTap>.Success(NoTap.Instance);
        }

        var directory = Absolute(GateArtifactPaths.Under(CellPaths.AttemptRoot(scope), CellPaths.TapFolder));
        var started = await taps.StartAsync(new TapLaunch(upstream, directory, TimeSpan.FromMinutes(d.Transport.ReviewMinutesCap) + TimeSpan.FromMinutes(5)), cancellationToken);

        return started;
    }

    private async Task<Outcome<GateCell>> DriveAsync(
        GateCellWork work, ArtifactScope scope, Prepared prepared, LaneSlot slot, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var ledgerPath = Path.Combine(prepared.Environment.DataDir, "usage.jsonl");
        var ledgerOffset = files.SizeOf(ledgerPath);
        var stderrPath = Absolute(GateArtifactPaths.Under(CellPaths.AttemptRoot(scope), StderrFile));
        var launch = new McpLaunch(settings.ProductExecutable, [], prepared.Checkout, prepared.Child.Variables, stderrPath, settings.HandshakeTimeout)
        {
            Scrub = prepared.Child.Scrub,
        };

        var tapDirectory = Absolute(GateArtifactPaths.Under(CellPaths.AttemptRoot(scope), CellPaths.TapFolder));
        var mark = new MeasuredMark(() => files.SizeOf(stderrPath), () => prepared.Tap.Endpoint.Length == 0 ? 0 : files.TapCalls(tapDirectory).Count);
        (ProtocolRun Run, string ServerVersion, bool Started) session;
        int marked;

        try
        {
            session = await SessionAsync(work, prepared, slot, launch, deadline, mark, cancellationToken);
            marked = await prepared.Tap.CloseAsync(settings.TapWait, CancellationToken.None);
        }
        finally
        {
            await prepared.Tap.DisposeAsync(); // a tap is never left listening, whatever happened to the session
        }

        var run = Scrubbed(session.Run, prepared.Child.Scrub);

        var evidence = Read(work, scope, prepared, run, ledgerPath, ledgerOffset, stderrPath, session.ServerVersion, marked, mark);

        return await CompleteAsync(work, scope, evidence, Settlement(work, run, evidence), cancellationToken);
    }

    private async Task<(ProtocolRun Run, string ServerVersion, bool Started)> SessionAsync(
        GateCellWork work, Prepared prepared, LaneSlot slot, McpLaunch launch, DateTimeOffset deadline, MeasuredMark mark, CancellationToken cancellationToken)
    {
        var opened = await slot.OpenAsync(work.Cell.Id, sessions, launch, cancellationToken);

        if (opened is Outcome<IMcpSession>.Fail fail)
        {
            return (GateProtocolSteps.Broken([], fail.Reason), string.Empty, false);
        }

        var session = ((Outcome<IMcpSession>.Ok)opened).Value;

        try
        {
            var inputs = new ProtocolInputs(prepared.Checkout, prepared.Branch, work.Task, prepared.PlanText, deadline, clock) { MarkMeasured = mark.Set };
            var run = work.Run.Gate switch
            {
                GateKind.Plan => await PlanGateProtocol.RunAsync(session, inputs, cancellationToken),
                GateKind.Code => await CodeGateProtocol.RunAsync(session, inputs, cancellationToken),
                _ => await FeatureGateProtocol.RunAsync(session, inputs, cancellationToken),
            };

            return (run, session.ServerVersion, true);
        }
        finally
        {
            await slot.ReleaseAsync(); // the process is gone before anything is read back
        }
    }

    private Evidence Read(
        GateCellWork work, ArtifactScope scope, Prepared prepared, ProtocolRun run, string ledgerPath, long ledgerOffset, string stderrPath, string serverVersion, int marked,
        MeasuredMark mark)
    {
        var parsed = GateReplyParser.Parse(run.Measured.Reply);
        var ledgerText = prepared.Child.Scrub(files.ReadFrom(ledgerPath, ledgerOffset));

        // Served/refused, the turn-1 prompt and the tap's calls are the MEASURED stage's: a code cell's plan loop ran in
        // the same session, and its stderr lines and HTTP calls are not the code reviewer's work.
        var stderr = files.ReadFrom(stderrPath, mark.StderrOffset);
        var calls = prepared.Tap.Endpoint.Length == 0 ? [] : files.TapCalls(Absolute(GateArtifactPaths.Under(CellPaths.AttemptRoot(scope), CellPaths.TapFolder)));
        var measuredCalls = calls.Skip(mark.Calls).Select(c => c.Facts).ToList();
        var stderrFacts = StderrFacts.Of(stderr);
        var shim = files.ShimFiles(stderrFacts.PromptFiles);
        var config = files.SessionConfig(prepared.Environment.DataDir, prepared.Checkout, work.Run.Gate == GateKind.Feature ? string.Empty : prepared.Branch);
        var check = SettingsCheck.Compare(prepared.Environment.Snapshot, config);

        return new Evidence(
            parsed, run, ledgerText, LedgerRows.Parse(ledgerText, work.Run.Gate), stderrFacts, calls, measuredCalls, shim, check,
            prepared.Environment.SnapshotJson, prepared.Environment.SettingsHash,
            new GateSessionNotes(serverVersion, prepared.ReferencesHash, check.Checked.Count, check.Mismatches.Count), marked);
    }

    private GateSettlement Settlement(GateCellWork work, ProtocolRun run, Evidence evidence)
    {
        if (!run.HasMeasurement)
        {
            var kind = run.Broken.Contains("did not answer within", StringComparison.Ordinal) || run.Broken.Contains("deadline", StringComparison.Ordinal)
                ? FailureKind.Interrupted
                : FailureKind.ProcessDied;
            return Failed(kind, run.Broken, work) with { Notes = evidence.Notes };
        }

        var facts = GateRunFacts.From(
            evidence.Reply.Reply, evidence.Ledger.Turns, evidence.MeasuredCalls, run.Measured.Seconds,
            evidence.Stderr.Served, evidence.Stderr.Refused);

        if (work.Run.Gate == GateKind.Code && !run.PlanLoopPassed)
        {
            facts = facts with
            {
                Valid = false,
                Failure = new FailureCause(FailureKind.VerdictNotPassing,
                    $"the plan loop never passed in {run.Stages.Count(s => s.Tool == "review_plan")} round(s) (last verdict {evidence.Reply.Verdict}) — review_code was never called"),
            };
        }

        var findings = evidence.Reply.Findings
            .Select(f => GateFinding.Of(f.Ordinal, f.Severity, f.Category, f.IsGating, f.Line, f.Text, f.File, work.Key))
            .OfType<Outcome<GateFinding>.Ok>()
            .Select(o => o.Value)
            .ToList();

        return new GateSettlement.Completed(
            facts with { Failure = FailureRedaction.Redact(facts.Failure, settings.PrivateNames) },
            findings,
            evidence.SettingsHash,
            PromptHash(evidence))
        {
            Notes = evidence.Notes,
        };
    }

    private GateSettlement.Failed Failed(FailureKind kind, string reason, GateCellWork work) =>
        new(FailureRedaction.Redact(new FailureCause(kind, reason), settings.PrivateNames));

    private static string PromptHash(Evidence evidence) =>
        evidence.Shim.FirstOrDefault(f => f.Name.Contains("prompt", StringComparison.OrdinalIgnoreCase)) is { Bytes: { Length: > 0 } bytes }
            ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))
            : string.Empty;

    private async Task<Outcome<GateCell>> CompleteAsync(GateCellWork work, ArtifactScope scope, Evidence evidence, GateSettlement settlement, CancellationToken cancellationToken)
    {
        var root = CellPaths.AttemptRoot(scope);
        var adopted = Adopted(scope, evidence);
        var written = GateAttemptRecord.Files(work, root, evidence, settlement);
        var record = GateAttemptRecord.RunRecord(work, root, evidence, settlement);

        return await new GateCellCompletion(artifacts, store).CompleteAsync(scope, work.Owner, adopted, written, record, settlement, CancellationToken.None);
    }

    private IReadOnlyList<(ArtifactClass, ArtifactPath)> Adopted(ArtifactScope scope, Evidence evidence)
    {
        var root = CellPaths.AttemptRoot(scope);
        var list = new List<(ArtifactClass, ArtifactPath)>();

        if (files.Exists(Absolute(GateArtifactPaths.Under(root, StderrFile))))
        {
            list.Add((ArtifactClass.Stderr, GateArtifactPaths.Under(root, StderrFile)));
        }

        foreach (var name in evidence.Calls.SelectMany(c => c.Files))
        {
            list.Add((TapClass(name), GateArtifactPaths.Under(root, $"{CellPaths.TapFolder}/{name}")));
        }

        return list;
    }

    private static ArtifactClass TapClass(string name) =>
        name.EndsWith(".request.json", StringComparison.Ordinal) ? ArtifactClass.TapRequest
        : name.EndsWith(".response.json", StringComparison.Ordinal) ? ArtifactClass.TapResponse
        : ArtifactClass.TapFacts;

    public const string StderrFile = "stderr.txt";

    /// <summary>Every text the product sent back, with the launch's secrets removed BEFORE anything reads or writes it —
    /// a reply, a resolve, an RPC error the session broke on. The product can echo its environment anywhere.</summary>
    private static ProtocolRun Scrubbed(ProtocolRun run, Func<string, string> scrub)
    {
        ProtocolStage Clean(ProtocolStage stage) => stage with { Reply = scrub(stage.Reply), Resolve = scrub(stage.Resolve), ResolveRefusal = scrub(stage.ResolveRefusal) };

        return run with { Stages = [.. run.Stages.Select(Clean)], Measured = Clean(run.Measured), Broken = scrub(run.Broken) };
    }

    /// <summary>The run's own ref: the run, the reviewer, the task, the repeat AND the attempt — with a shared data
    /// directory a retried attempt would otherwise find the dead attempt's session under the same repo and branch.</summary>
    public static string RunRef(GateCellWork work, int attempt) =>
        string.Create(CultureInfo.InvariantCulture, $"bench/gate/{work.Run.Id.ToString("N")[..8]}/{work.Reviewer.Id.Value}/{work.Task.Id.Value}-r{work.Cell.Repeat}-a{attempt}");

    private static bool NeedsVault(GateReviewer reviewer) => reviewer.Definition.Runtime == ReviewerRuntime.Api;

    private string Absolute(ArtifactPath path) => Path.Combine([settings.ArtifactRoot, .. path.Segments]);
}

/// <summary>What an attempt's session left, read back after the process was gone.</summary>
public sealed record Evidence(
    ParsedReply Reply,
    ProtocolRun Run,
    string LedgerText,
    LedgerRows Ledger,
    StderrFacts.Facts Stderr,
    IReadOnlyList<(HttpCallFacts Facts, IReadOnlyList<string> Files)> Calls,
    IReadOnlyList<HttpCallFacts> MeasuredCalls,
    IReadOnlyList<(string Name, byte[] Bytes)> Shim,
    SettingsApplied Settings,
    string SnapshotJson,
    string SettingsHash,
    GateSessionNotes Notes,
    int TapMarkedAtClose)
{
    public static Evidence Empty { get; } = new(
        GateReplyParser.Parse(string.Empty), GateProtocolSteps.Broken([], string.Empty), string.Empty, new LedgerRows([], 0),
        new StderrFacts.Facts(0, 0, []), [], [], [], new SettingsApplied([], [], []), string.Empty, string.Empty, GateSessionNotes.None, 0);
}

/// <summary>Where the stderr and the tap stood when the MEASURED stage began — set by the protocol, read after the session.</summary>
public sealed class MeasuredMark(Func<long> stderrSize, Func<int> tapCalls)
{
    public long StderrOffset { get; private set; }

    public int Calls { get; private set; }

    public void Set()
    {
        StderrOffset = stderrSize();
        Calls = tapCalls();
    }
}

/// <summary>No recorder — a CLI reviewer, a referenced endpoint nothing resolved, or <c>--no-tap</c>. A state, not a null.</summary>
public sealed class NoTap : IRecordingTap
{
    public static NoTap Instance { get; } = new();

    public string Endpoint => string.Empty;

    public Task<int> CloseAsync(TimeSpan wait, CancellationToken cancellationToken) => Task.FromResult(0);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
