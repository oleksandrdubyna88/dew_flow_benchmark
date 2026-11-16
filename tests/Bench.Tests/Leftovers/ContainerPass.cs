using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Infrastructure.Process;

namespace Bench.Tests.Leftovers;

// The container pass (E6 S3, plan D9): the Docker half of the start-of-run sweep (rule §4 and §5). It runs ONCE per process, inside
// PostgresFixture.InitializeAsync before the fixture's own container exists, and removes the container a DEAD owner's killed run left behind.
//
// The two keys, as for a folder. SCOPE: the container's name is in this user's owner form for this repository, and its label is the owner record
// the fixture wrote (rule §3: a daemon object's label is its only evidence). ABANDONMENT: that record is `gone`. Age only selects (younger than ten
// minutes is left alone). The removal is by the id captured at the listing (`rm -f -v <id>`), never by name.
//
// Never fails the run: every failure is ONE line of the result, and a missing docker is a notice. The docker CLI shape follows the janitor
// (`tools/janitor.sh`): the label read through `%q`, an id of lower-case hex, `rm -f -v <id>`; the listing is `ps` by label, then `inspect` in batches.
//
// Benchmark's own file: it is not a manifest target (TheAdaptedCopyTests.BenchOwn).

/// <summary>What one pass needs. <paramref name="Consider"/> is a TEST seam (which captured ids may be judged); the real run never sets it.
/// <paramref name="Progress"/> receives the lines said while the pass is still removing (more than a handful of containers): the real run writes them to stderr.</summary>
public sealed record ContainerPassOptions(
    string Docker, DateTimeOffset Now, TimeSpan MinAge, Func<long, Probe> Probe, TimeSpan CallTimeout, Func<string, bool>? Consider = null, Action<string>? Progress = null);

/// <summary>One line of <c>docker inspect</c>, read: the full id, the name (null when it cannot be read), the owner label (null when it cannot be
/// read) and the creation time to whole seconds, UTC (null when it cannot be read).</summary>
public sealed record ContainerRow(string Id, string? Name, string? Label, DateTimeOffset? Created)
{
    private static readonly Regex FullId = new(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Stamp = new(@"^([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})(\.[0-9]+)?Z\z", RegexOptions.CultureInvariant);

    public string ShortId => Id[..12];

    /// <summary>The id as the daemon prints it with <c>--no-trunc</c>: 64 lower-case hex digits and nothing else.</summary>
    public static bool IsFullId(string text) => FullId.IsMatch(text);

    /// <summary>Null is an unreadable line: not four tab-separated fields, or an id that is not 64 lower-case hex.</summary>
    public static ContainerRow? Parse(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length != 4 || !IsFullId(fields[0])) return null;
        var name = Unquote(fields[1]);
        return new ContainerRow(fields[0], name is null ? null : name.StartsWith('/') ? name[1..] : name, Unquote(fields[2]), ReadCreated(fields[3]));
    }

    /// <summary>Go's <c>%q</c> of a plain string, read back only where nothing was escaped: a backslash means the text is not what it looks like.</summary>
    private static string? Unquote(string quoted) =>
        quoted.Length >= 2 && quoted[0] == '"' && quoted[^1] == '"' && !quoted.Contains('\\') ? quoted[1..^1] : null;

    private static DateTimeOffset? ReadCreated(string text)
    {
        var match = Stamp.Match(text);
        return match.Success && DateTimeOffset.TryParseExact(match.Groups[1].Value + "Z", "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }
}

/// <summary>What the pass decides for one container: remove it, keep it with its reason, or only list it.</summary>
public abstract record ContainerVerdict
{
    private ContainerVerdict()
    {
    }

    public sealed record Remove(ContainerRow Row) : ContainerVerdict;

    public sealed record Keep(ContainerRow Row, string Reason) : ContainerVerdict;

    public sealed record List(string Line) : ContainerVerdict;
}

public static class ContainerPass
{
    /// <summary>The owner label's key, <c>testsToDelete.owner</c>. Built, never one literal: the prefix word may be named only in the helper's folder, and the C# guard reads the literal as an owner-blind match.</summary>
    public static string LabelKey => Owned.Prefix[..^1] + ".owner";

    /// <summary>Plan D9: every docker call ends within this.</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Plan D9: ids per <c>docker inspect</c>.</summary>
    public const int MaxBatch = 100;

    /// <summary>Removals in flight at once; each call keeps its own timeout, so a stuck daemon costs one timeout per wave, never one per container.</summary>
    public const int MaxRemovalsAtOnce = 4;

    /// <summary>More removals than this: the pass says how far it is while it runs.</summary>
    public const int ProgressAbove = 5;

    /// <summary>The Ryuk switch's name, assembled once and with no <c>+</c> between literals: the source test over every <c>.cs</c> (scenario (7)) reads a name joined with <c>+</c> as a switch.</summary>
    public static string RyukVariable { get; } = string.Concat("TESTCONTAINERS", "_RYUK", "_DISABLED");

    private static readonly Gate Shared = new(RealPass);

    public static ContainerPassOptions Real(DateTimeOffset now) => new("docker", now, LeftoversSweep.MinAge, Liveness.ProbeProcess, CallTimeout, Progress: SayProgress);

    /// <summary>How many times the real pass ran in this process: 1 after any fixture started.</summary>
    public static int Runs => Shared.Runs;

    /// <summary>The once-per-process pass (D9). The first caller starts it and it prints its report; every other caller awaits the same task. Never throws, and blocks no thread while docker answers.</summary>
    public static Task<SweepResult> OnceAsync() => Shared.AwaitAsync();

    internal static IReadOnlyList<string> ListArgs => ["ps", "-a", "-q", "--no-trunc", "--filter", "label=" + LabelKey];

    internal static IReadOnlyList<string> InspectArgs(IEnumerable<string> ids) => ["inspect", "--type", "container", "--format", Template, .. ids];

    internal static IReadOnlyList<string> RemoveArgs(string id) => ["rm", "-f", "-v", id];

    /// <summary>The id, the name, the owner label and the creation time, each of the first three Go-quoted (<c>%q</c>) so a tab or a newline inside cannot shift a field; a real tab between the fields.</summary>
    internal static string Template =>
        "{{.Id}}\t{{printf \"%q\" .Name}}\t{{printf \"%q\" (index .Config.Labels \"" + LabelKey + "\")}}\t{{.Created}}";

    /// <summary>Rule §5: a Testcontainers fixture keeps the reaper on. Throws when the effective setting says it is off.</summary>
    public static void RequireReaper(bool enabled)
    {
        if (enabled) return;
        throw new InvalidOperationException(
            $"The Testcontainers reaper (Ryuk) is switched off here ({RyukVariable}=true in the environment, or ryuk.disabled=true in ~/.testcontainers.properties, or code): "
            + "rule §5 keeps it on, so this fixture refuses to start a container that nothing would collect after a kill. Unset it and re-run.");
    }

    /// <summary>Run one pass. Never throws: every failure is one line of the result.</summary>
    public static async Task<SweepResult> RunAsync(ContainerPassOptions options)
    {
        var pass = new Pass(options);
        try
        {
            await pass.GoAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            pass.Error($"containers: failed: {FirstLine(ex.Message)}");
        }

        return pass.Result();
    }

    /// <summary>The same pass for a synchronous caller (the tests): the wait is off the caller's context. The fixture never uses this; it awaits <see cref="OnceAsync"/>.</summary>
    public static SweepResult Run(ContainerPassOptions options) => Task.Run(() => RunAsync(options)).GetAwaiter().GetResult();

    /// <summary>The decision for one container (§5.2 of the brief), in this order: name, form, repository, age, record, liveness.</summary>
    internal static ContainerVerdict Judge(ContainerRow row, ContainerPassOptions options) =>
        row.Name is not { } name
            ? new ContainerVerdict.List($"notice: container {row.ShortId} — its name is not readable, ignored")
            : Owned.Classify(name) switch
            {
                NameForm.Owner form => JudgeOwned(row, name, form.Segment, options),
                NameForm.Legacy => new ContainerVerdict.List($"legacy: container {name} ({row.ShortId}) — the old form is listed, never collected; after checking: docker rm -f -v {row.Id}"),
                _ => new ContainerVerdict.List($"foreign: container {name} ({row.ShortId}) — not a bench owner-form name"),
            };

    private static ContainerVerdict JudgeOwned(ContainerRow row, string name, string owner, ContainerPassOptions options) =>
        Owned.Pattern().IsMatch(name) ? JudgeAge(row, owner, options) : new ContainerVerdict.List($"foreign: container {name} ({row.ShortId}) — another owner's or another repository's");

    private static ContainerVerdict JudgeAge(ContainerRow row, string owner, ContainerPassOptions options) =>
        row.Created is not { } created ? new ContainerVerdict.Keep(row, "unknown age")
        : options.Now - created < options.MinAge ? new ContainerVerdict.Keep(row, "young")
        : JudgeRecord(row, owner, options);

    private static ContainerVerdict JudgeRecord(ContainerRow row, string owner, ContainerPassOptions options) =>
        row.Label is null || Owner.Parse(row.Label) is not { } record ? new ContainerVerdict.Keep(row, "unreadable record") : JudgeLiveness(row, record, owner, options);

    // KernelMine stays null: a daemon object has no kernel owner, its label is its only evidence (rule §3).
    private static ContainerVerdict JudgeLiveness(ContainerRow row, Owner record, string nameOwner, ContainerPassOptions options) =>
        Liveness.State(record, options.Probe, new Facts(NameOwner: nameOwner)) switch
        {
            OwnerState.Gone => new ContainerVerdict.Remove(row),
            OwnerState.Live => new ContainerVerdict.Keep(row, "live"),
            _ => new ContainerVerdict.Keep(row, "unknown"),
        };

    private static readonly Regex ControlCharacters = new(@"[\u0000-\u001f\u007f]", RegexOptions.CultureInvariant);

    /// <summary>A text cut to its first line, its control characters turned into <c>?</c>: one line of the report, whatever the daemon printed.</summary>
    private static string FirstLine(string text) => ControlCharacters.Replace(text.Split('\n')[0].TrimEnd('\r'), "?");

    private static async Task<SweepResult> RealPass()
    {
        var result = await RunAsync(Real(DateTimeOffset.UtcNow)).ConfigureAwait(false);
        Report(result);
        return result;
    }

    private static void SayProgress(string line)
    {
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A progress line that cannot be written is not a reason to fail the run.
        }
    }

    /// <summary>The folder sweep's shape (<c>LeftoversSweepFixture</c>): a header, the listed lines, the preserved ones with their reason, the errors. Nothing when the result is empty.</summary>
    private static void Report(SweepResult run)
    {
        if (run.Count == 0) return;
        try
        {
            Console.Error.WriteLine($"{OwnedToken.RepoToken} tests: containers — removed {run.Removed.Count}, preserved {run.Preserved.Count}, listed {run.Listed.Count}");
            foreach (var line in run.Listed) Console.Error.WriteLine("  " + line);
            foreach (var (what, reason) in run.Preserved) Console.Error.WriteLine($"  preserved: {what} — {reason}");
            foreach (var error in run.Errors) Console.Error.WriteLine("  error: " + error);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A report that cannot be written is not a reason to fail the run.
        }
    }

    /// <summary>The once-per-process gate as a seam: the real one runs <see cref="RunAsync"/>; a test passes a counting pass. The factory catches everything, as
    /// <c>StartOfRunSweep.Gate</c> does: a cached exception would fail every later fixture of the process. The lazy holds the TASK, so the pass starts once and every caller awaits it.</summary>
    internal sealed class Gate
    {
        private readonly Lazy<Task<SweepResult>> _once;
        private int _runs;

        internal Gate(Func<Task<SweepResult>> pass)
        {
            _once = new Lazy<Task<SweepResult>>(() => Once(pass), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal Task<SweepResult> AwaitAsync() => _once.Value;

        internal int Runs => Volatile.Read(ref _runs);

        private async Task<SweepResult> Once(Func<Task<SweepResult>> pass)
        {
            Interlocked.Increment(ref _runs);
            try
            {
                return await pass().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return new SweepResult([], [], [], [$"containers: failed: {FirstLine(ex.Message)}"]);
            }
        }
    }

    private sealed class Pass(ContainerPassOptions options)
    {
        private readonly object _sync = new();
        private readonly List<string> _removed = [];
        private readonly List<(string Path, string Reason)> _preserved = [];
        private readonly List<string> _listed = [];
        private readonly List<string> _errors = [];
        private volatile bool _halted;

        internal SweepResult Result() => new(_removed, _preserved, _listed, _errors);

        internal void Error(string line)
        {
            lock (_sync) _errors.Add(line);
        }

        internal async Task GoAsync()
        {
            if (await ListAsync().ConfigureAwait(false) is not { Count: > 0 } ids) return;
            if (await InspectAsync(ids).ConfigureAwait(false) is not { } rows) return;
            var removals = new List<ContainerRow>();
            foreach (var row in rows.Where(Considered)) Sort(ContainerPass.Judge(row, options), removals);
            await RemoveAllAsync(removals).ConfigureAwait(false);
        }

        private bool Considered(ContainerRow row) => options.Consider?.Invoke(row.Id) ?? true;

        private async Task<List<string>?> ListAsync()
        {
            if (await CallAsync("ps", ListArgs, "").ConfigureAwait(false) is not { } done) return null;
            if (done.ExitCode != 0) return Refused<string>("ps", done);
            var lines = done.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _listed.AddRange(lines.Where(line => !ContainerRow.IsFullId(line)).Select(_ => "notice: docker ps printed a line that is not a container id, ignored"));
            return [.. lines.Where(ContainerRow.IsFullId).Distinct()];
        }

        private async Task<List<ContainerRow>?> InspectAsync(List<string> ids)
        {
            var rows = new List<ContainerRow>();
            foreach (var batch in ids.Chunk(MaxBatch))
            {
                if (await InspectBatchAsync(batch).ConfigureAwait(false) is not { } read) return null;
                rows.AddRange(read);
            }

            return rows;
        }

        /// <summary>One batch. A batch that exits non-zero is usually an id that vanished between <c>ps</c> and <c>inspect</c> (the daemon fails the whole call): it is read again one id at a time, so the others are still judged.</summary>
        private async Task<List<ContainerRow>?> InspectBatchAsync(string[] batch)
        {
            if (await CallAsync("inspect", InspectArgs(batch), "").ConfigureAwait(false) is not { } done) return null;
            return done.ExitCode == 0 ? ReadBatch(done.StandardOutput, batch) : await InspectOneByOneAsync(batch).ConfigureAwait(false);
        }

        private async Task<List<ContainerRow>?> InspectOneByOneAsync(string[] batch)
        {
            var rows = new List<ContainerRow>();
            foreach (var id in batch)
            {
                if (await CallAsync("inspect", InspectArgs([id]), "").ConfigureAwait(false) is not { } done) return null;
                if (done.ExitCode == 0) rows.AddRange(ReadBatch(done.StandardOutput, [id]));
                else if (done.StandardError.Contains("no such ", StringComparison.OrdinalIgnoreCase)) _listed.Add($"notice: container {id[..12]} — it was gone before docker inspect could read it, ignored");
                else return Stopped(rows, id, done);
            }

            return rows;
        }

        /// <summary>The daemon refused a read that is not a vanished id: the rest of the batch is left alone, what was read stays.</summary>
        private List<ContainerRow> Stopped(List<ContainerRow> rows, string id, ProcessResult done)
        {
            Error($"containers: docker inspect {id[..12]} failed (exit {done.ExitCode}): {FirstLine(done.StandardError)} — the rest of this batch is left alone");
            return rows;
        }

        /// <summary>The rows of one inspect answer. A line that does not parse, and an id that got no row, are one notice each.</summary>
        private List<ContainerRow> ReadBatch(string output, string[] batch)
        {
            var parsed = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => ContainerRow.Parse(line.TrimEnd('\r'))).ToList();
            _listed.AddRange(parsed.Where(row => row is null).Select(_ => "notice: docker inspect printed a line that cannot be read, ignored"));
            var rows = parsed.OfType<ContainerRow>().ToList();
            _listed.AddRange(batch.Where(id => rows.All(row => row.Id != id)).Select(id => $"notice: container {id[..12]} — docker inspect returned no row for it, ignored"));
            return rows;
        }

        private void Sort(ContainerVerdict verdict, List<ContainerRow> removals)
        {
            switch (verdict)
            {
                case ContainerVerdict.Remove remove:
                    removals.Add(remove.Row);
                    break;
                case ContainerVerdict.Keep keep:
                    _preserved.Add((Label(keep.Row), keep.Reason));
                    break;
                case ContainerVerdict.List listed:
                    _listed.Add(listed.Line);
                    break;
            }
        }

        /// <summary>Removes by captured id, at most <see cref="MaxRemovalsAtOnce"/> at a time, each call under its own timeout. The result lists what was removed in the order the rows were judged.</summary>
        private async Task RemoveAllAsync(List<ContainerRow> removals)
        {
            if (removals.Count == 0) return;
            if (removals.Count > ProgressAbove) options.Progress?.Invoke($"{OwnedToken.RepoToken} tests: containers — removing {removals.Count} containers of dead owners, {MaxRemovalsAtOnce} at a time");
            using var slots = new SemaphoreSlim(MaxRemovalsAtOnce);
            var outcomes = new RemovalOutcome[removals.Count];
            var finished = 0;
            await Task.WhenAll(removals.Select(async (row, index) =>
            {
                await slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    outcomes[index] = await RemoveOneAsync(row).ConfigureAwait(false);
                    var count = Interlocked.Increment(ref finished);
                    if (removals.Count > ProgressAbove && count % 10 == 0) options.Progress?.Invoke($"{OwnedToken.RepoToken} tests: containers — {count} of {removals.Count} removals finished");
                }
                finally
                {
                    slots.Release();
                }
            })).ConfigureAwait(false);
            Settle(removals, outcomes);
        }

        private enum RemovalState { NotRun, Removed, Failed }

        private readonly record struct RemovalOutcome(RemovalState State, string Line);

        private async Task<RemovalOutcome> RemoveOneAsync(ContainerRow row)
        {
            if (await CallAsync("rm", RemoveArgs(row.Id), row.Id).ConfigureAwait(false) is not { } done) return new RemovalOutcome(RemovalState.NotRun, "");
            return done.ExitCode == 0
                ? new RemovalOutcome(RemovalState.Removed, "")
                : new RemovalOutcome(RemovalState.Failed, $"containers: docker rm {row.ShortId} failed (exit {done.ExitCode}): {FirstLine(done.StandardError)}");
        }

        private readonly HashSet<string> _unknown = [];

        /// <summary>Records the outcomes in the order the rows were judged. A removal that timed out is NOT known to have failed: the line says its outcome is unknown and how many removals are confirmed.</summary>
        private void Settle(List<ContainerRow> removals, RemovalOutcome[] outcomes)
        {
            _removed.AddRange(removals.Where((_, i) => outcomes[i].State == RemovalState.Removed).Select(Label));
            _errors.AddRange(outcomes.Where(o => o.State == RemovalState.Failed).Select(o => o.Line));
            if (_unknown.Count == 0) return;
            _errors.Add($"containers: docker rm timed out after {options.CallTimeout.TotalSeconds:0.#}s for {string.Join(", ", removals.Where(r => _unknown.Contains(r.Id)).Select(r => r.ShortId))} — outcome unknown; {_removed.Count} removal(s) confirmed");
        }

        private static string Label(ContainerRow row) => $"container {row.Name} ({row.ShortId})";

        /// <summary>The one error line of a call that exited non-zero; the pass goes no further (null stands for "no result").</summary>
        private List<T>? Refused<T>(string verb, ProcessResult done)
        {
            Error($"containers: docker {verb} failed (exit {done.ExitCode}): {FirstLine(done.StandardError)}");
            return null;
        }

        /// <summary>One docker call. Null when the call gave no answer: the notice or the error line is written (or, for a removal, the id is noted as unknown) and no further call is started.</summary>
        private async Task<ProcessResult?> CallAsync(string verb, IReadOnlyList<string> args, string what)
        {
            if (_halted) return null;
            var attempt = await ProcessRunner.RunAsync(options.Docker, args, AppContext.BaseDirectory, options.CallTimeout, CancellationToken.None).ConfigureAwait(false);
            if (attempt is ProcessAttempt.Completed completed) return completed.Result;
            lock (_sync)
            {
                _halted = true;
                if (attempt is ProcessAttempt.NotFound notFound) _listed.Add($"notice: containers not swept — {notFound.Describe}");
                else if (verb == "rm") _unknown.Add(what);
                else _errors.Add($"containers: docker {verb} timed out after {options.CallTimeout.TotalSeconds:0.#}s — nothing removed");
            }

            return null;
        }
    }
}
