using System.Globalization;
using System.Text.Json;

namespace Bench.Domain.Gate;

/// <summary>A secret held as a value that cannot be printed. <see cref="ToString"/> is redacted, the record's
/// generated rendering is replaced, and the only way to the characters is <see cref="ChildEnvironment"/> — so an
/// <c>Outcome&lt;SecretValue&gt;</c> logged by mistake, or a snapshot serialised whole, says <c>[redacted]</c>.</summary>
public sealed class SecretValue
{
    private SecretValue(string value) => Value = value;

    internal string Value { get; }

    /// <summary>No secret — a reviewer that needs no vault. A state, not a null.</summary>
    public static SecretValue None { get; } = new(string.Empty);

    public bool IsPresent => Value.Length > 0;

    /// <summary>The secret, trimmed. An empty one is refused: an empty key is not "no key", it is a key that fails three
    /// layers later as "unauthorised".</summary>
    public static Outcome<SecretValue> Of(string? value, string name)
    {
        var trimmed = (value ?? string.Empty).Trim();

        return trimmed.Length > 0
            ? Outcome<SecretValue>.Success(new SecretValue(trimmed))
            : Outcome<SecretValue>.Failure($"{name} resolved to nothing — a secret is refused by name when it is unset, never passed on empty");
    }

    public override string ToString() => "[redacted]";
}

/// <summary>What a run pins for every cell, beside what the reviewer row pins for its own: the product knobs the
/// measurement holds still, plus the operator's <c>--set COAI_X=Y</c> extras. Stored with the run so a resume
/// re-applies exactly these.</summary>
public sealed record GateRunSettings
{
    private GateRunSettings(IReadOnlyDictionary<string, string> values) => Values = values;

    /// <summary>Name → value, ordinal, every name a <c>COAI_*</c> variable.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>The knobs every gate cell runs under unless the operator overrides one: min-epics 1 (the product's
    /// default of 3 SKIPS a small seeded plan instead of reviewing it), consult off, the exhausted policy
    /// <c>good_enough</c> (so a code cell's plan loop ends passing rather than asking a person nobody is watching),
    /// the product's own concurrency caps, and Debug logs (the served/refused line and the shim's argv are Debug).</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["COAI_FEATURE_MIN_EPICS"] = "1",
        ["COAI_CONSULT_ENABLED"] = "false",
        ["COAI_ON_EXHAUSTED"] = "good_enough",
        ["COAI_MAX_CONCURRENCY"] = "2",
        ["COAI_MAX_PER_PROVIDER"] = "2",
        ["COAI_ESCALATION_SECONDS"] = "5",
        ["COAI_LOG_LEVEL"] = "Debug",
    };

    /// <summary>The defaults with the operator's extras laid over them. An extra must be a <c>COAI_*</c> name, may not
    /// be one the harness owns per cell (data dir, caller session, vendors, the creds key, the reviewer's transport),
    /// and may not look like a secret — a secret never travels as a run setting.</summary>
    public static Outcome<GateRunSettings> With(IReadOnlyDictionary<string, string> extras)
    {
        foreach (var name in extras.Keys)
        {
            var refusal = ExtraRefusal(name);
            if (refusal.Length > 0)
            {
                return Outcome<GateRunSettings>.Failure(refusal);
            }
        }

        var merged = new Dictionary<string, string>(Defaults, StringComparer.Ordinal);
        foreach (var (name, value) in extras)
        {
            merged[name.ToUpperInvariant()] = value;
        }

        return Outcome<GateRunSettings>.Success(new GateRunSettings(merged));
    }

    public string ToJson() => JsonSerializer.Serialize(Values.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value));

    public static Outcome<GateRunSettings> FromJson(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            return With(values.Where(v => !Defaults.ContainsKey(v.Key) || Defaults[v.Key] != v.Value).ToDictionary(v => v.Key, v => v.Value));
        }
        catch (JsonException ex)
        {
            return Outcome<GateRunSettings>.Failure($"the run's stored settings do not parse — {ex.Message}");
        }
    }

    private static string ExtraRefusal(string name) =>
        (name.StartsWith("COAI_", StringComparison.OrdinalIgnoreCase), CoaiEnvironment.IsHarnessOwned(name), CoaiEnvironment.IsSecretName(name)) switch
        {
            (false, _, _) => $"--set {name}: a run setting is a COAI_* variable — the product reads nothing else",
            (_, true, _) => $"--set {name}: that variable is set per cell by the harness (data dir, caller session, vendors, the reviewer's transport) — a run cannot pin it",
            (_, _, true) => $"--set {name}: a secret never travels as a run setting — it is resolved at launch, by name, and never stored",
            _ => string.Empty,
        };
}

/// <summary>The inputs of one cell attempt's environment.</summary>
/// <param name="Parent">The harness's own environment. Every <c>COAI_*</c> is dropped from it, and so is the variable
/// the reviewer names as holding the creds key — the child gets the key once, under the product's name, and not also
/// under the operator's.</param>
/// <param name="ArtifactRoot">The artefact root's absolute path; the data directory is <see cref="CellPaths.DataDirFor(ArtifactScope)"/>
/// under it.</param>
public sealed record CoaiEnvironmentInputs(
    ArtifactScope Scope,
    GateReviewer Reviewer,
    GateTaskId Task,
    int Repeat,
    GateRunSettings RunSettings,
    IReadOnlyDictionary<string, string> Parent,
    string ArtifactRoot);

/// <summary>One cell attempt's environment for the product, WITHOUT its secret — a port of the calibration harness's
/// <c>child_env</c>.
/// <para>
/// <see cref="Snapshot"/> is every <c>COAI_*</c> the product will be sent, secrets removed by name; it is what an attempt
/// stores (in the artefact root — it names the data directory, a path on this machine). <see cref="SettingsHash"/> is
/// the SCOPE's settings hash, over the snapshot minus what is already another axis (<see cref="AxisVariables"/>): the
/// cell's identity (its data directory and caller session) and the reviewer's own configuration (the vendors string —
/// whose tap port also moves per run — and the transport knobs, all in <c>ReviewerDefinition.Hash</c>). Without that,
/// every cell would be a scope of its own and no two reviewers could share a table.
/// </para>
/// <para>
/// The secret joins only at <see cref="WithSecret"/>, LAST, into a separate <see cref="ChildEnvironment"/> whose rendering
/// is redacted — so nothing that holds this value can print a key, and the hash cannot depend on one.
/// </para></summary>
public sealed record CoaiEnvironment
{
    /// <summary>The name the product reads the vault's access key from.</summary>
    public const string CredsKeyVariable = "COAI_CREDS_KEY";

    public const string DataDirVariable = "COAI_DATA_DIR";

    public const string CallerSessionVariable = "COAI_CALLER_SESSION";

    /// <summary>The reviewer's transport as the product's variables — ONE list, read both to SET them and to leave them out
    /// of the scope's hash, so a transport knob added here can never become a scope difference by being forgotten there.</summary>
    public static IReadOnlyList<(string Name, Func<ReviewerTransport, string> Value)> TransportVariables { get; } =
    [
        ("COAI_LOCAL_MAX_TOKENS", t => Invariant(t.MaxTokens)),
        ("COAI_LOCAL_REASONING_EFFORT", t => t.ReasoningEffort),
        ("COAI_REVIEWER_TIMEOUT_MINUTES", t => Invariant(t.TimeoutMinutes)),
        ("COAI_FEATURE_API_REVIEW_MINUTES", t => Invariant(t.ReviewMinutesCap)),
        ("COAI_FEATURE_SOURCE_FOLLOWUPS", t => Invariant(t.FollowUps)),
    ];

    /// <summary>The variables that are another axis already, each with the reason it is out of the scope's hash. The vendors
    /// variable is out as well — the reviewer's row (and the tap's per-run port), hashed in <c>ReviewerDefinition.Hash</c> —
    /// and is asked through <see cref="CoaiVendorsSetting.IsVariable"/>, because its name is spelled in one file only.</summary>
    public static IReadOnlyDictionary<string, string> AxisVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [DataDirVariable] = "the cell attempt's own directory — the cell is its own axis",
        [CallerSessionVariable] = "the cell attempt's own identity — the cell is its own axis",
        ["COAI_PROVIDERS"] = "the reviewer's id — the reviewer is its own axis",
    }.Concat(TransportVariables.Select(t => KeyValuePair.Create(t.Name, $"the reviewer's transport ({t.Name}) — in ReviewerDefinition.Hash")))
     .ToDictionary(StringComparer.Ordinal);


    private readonly IReadOnlyList<string> _inheritedSecrets;

    private CoaiEnvironment(
        IReadOnlyDictionary<string, string> variables, IReadOnlyDictionary<string, string> snapshot, string dataDir, string callerSession, IReadOnlyList<string> inheritedSecrets)
    {
        Variables = variables;
        Snapshot = snapshot;
        DataDir = dataDir;
        CallerSession = callerSession;
        _inheritedSecrets = inheritedSecrets;
    }

    /// <summary>A value shorter than this is not scrubbed as a secret: replacing a three-letter "key" everywhere would
    /// mangle the record without protecting anything.</summary>
    public const int ShortestScrubbed = 8;

    /// <summary>The whole child environment except the secret: the parent's non-<c>COAI_*</c> variables and every knob.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>Every <c>COAI_*</c> sent, minus secrets by name. Stored per attempt; never in the database.</summary>
    public IReadOnlyDictionary<string, string> Snapshot { get; }

    public string DataDir { get; }

    public string CallerSession { get; }

    /// <summary>The scope's settings hash — the snapshot minus <see cref="AxisVariables"/>, canonical, hashed.</summary>
    public string SettingsHash => StableHash.Of(CanonicalFields.Of(
    [
        "gate-settings",
        .. Snapshot.Where(v => !IsAxis(v.Key)).OrderBy(v => v.Key, StringComparer.Ordinal).SelectMany(v => new[] { v.Key, v.Value }),
    ]));

    public string SnapshotJson => JsonSerializer.Serialize(
        Snapshot.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value),
        new JsonSerializerOptions { WriteIndented = true });

    /// <summary>The caller session id the product keys its per-caller memory by: one per ATTEMPT, so a retried cell is a
    /// new caller rather than a continuation of the one that died.</summary>
    public static string CallerSessionFor(Guid runId, GateReviewerId reviewer, GateTaskId task, int repeat, int attempt) =>
        string.Create(CultureInfo.InvariantCulture, $"bench-gate-{runId.ToString("N")[..8]}-{reviewer.Value}-{task.Value}-r{repeat}-a{attempt}");

    /// <param name="vendors">The one producer's value, passed in rather than held by <paramref name="inputs"/>: nothing but
    /// <see cref="CoaiVendorsSetting.From"/> may hand one back, so no record carries one.</param>
    public static CoaiEnvironment For(CoaiEnvironmentInputs inputs, CoaiVendorsSetting vendors)
    {
        var dataDir = Path.Combine([inputs.ArtifactRoot, .. CellPaths.DataDirFor(inputs.Scope).Segments]);
        var caller = CallerSessionFor(inputs.Scope.Run.Id, inputs.Reviewer.Id, inputs.Task, inputs.Repeat, inputs.Scope.Attempt);
        var credsRef = inputs.Reviewer.Definition.CredsKeyRef;

        var parent = inputs.Parent
            .Where(v => !v.Key.StartsWith("COAI_", StringComparison.OrdinalIgnoreCase))
            .Where(v => credsRef.Length == 0 || !string.Equals(v.Key, credsRef, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        var knobs = new Dictionary<string, string>(inputs.RunSettings.Values, StringComparer.Ordinal);
        foreach (var (name, value) in TransportVariables)
        {
            knobs[name] = value(inputs.Reviewer.Definition.Transport);
        }

        knobs[DataDirVariable] = dataDir;
        knobs[CallerSessionVariable] = caller;
        knobs["COAI_PROVIDERS"] = inputs.Reviewer.Id.Value;

        var variables = vendors.ApplyTo(new Dictionary<string, string>([.. parent, .. knobs], StringComparer.Ordinal));
        var snapshot = variables
            .Where(v => v.Key.StartsWith("COAI_", StringComparison.Ordinal) && !IsSecretName(v.Key))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        // The operator's other secrets (any *_KEY, *_TOKEN, … in the harness's shell) still reach the child — a CLI reviewer
        // may authenticate by one, and the calibration passed the parent through — but their VALUES are scrubbed from
        // everything the harness writes, beside the vault key.
        var inherited = parent.Where(v => IsSecretName(v.Key) && v.Value.Trim().Length >= ShortestScrubbed).Select(v => v.Value.Trim()).Distinct(StringComparer.Ordinal).ToList();

        return new CoaiEnvironment(variables, snapshot, dataDir, caller, inherited);
    }

    /// <summary>The environment of a product launch that carries NO knobs — <c>coai-mcp --probe-api</c> (the probes, S2): the
    /// parent's variables minus every <c>COAI_*</c>, so the product reads nothing behind the harness's back, and the secret
    /// joins LAST through <see cref="WithSecret"/> exactly as for a cell, scrubbed from every text written beside the operator's
    /// inherited secrets. No data directory, no caller session, an empty snapshot — there is no session to key them by.
    /// Since S2c (finding 3) the parent's <c>BENCH_*</c> and secret-named variables are dropped as well: the product authenticates
    /// through the vault by the one key joined last, and the database url it would otherwise inherit carries a password.</summary>
    public static CoaiEnvironment Bare(IReadOnlyDictionary<string, string> parent) => Minimal(parent, static _ => true);

    /// <summary>A MINIMAL child environment (S2c, finding 3): only the parent's variables <paramref name="passes"/> admits — never a
    /// <c>BENCH_*</c>, never a <c>COAI_*</c>, never a secret-named one, whatever the predicate says — while every secret-named value and
    /// every <c>BENCH_*</c> value of the parent is still scrubbed from the texts written (<see cref="ChildEnvironment.Scrub"/>): the child
    /// cannot see them, and a text that quotes one anyway (a log line, an echo) must not carry it to disk. No snapshot, no data
    /// directory, no caller session — the probes' launches have none of those.</summary>
    public static CoaiEnvironment Minimal(IReadOnlyDictionary<string, string> parent, Func<string, bool> passes)
    {
        var variables = parent
            .Where(v => passes(v.Key) && !IsHarnessVariable(v.Key) && !IsSecretName(v.Key))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        var inherited = parent
            .Where(v => IsScrubbedName(v.Key) && v.Value.Trim().Length >= ShortestScrubbed)
            .Select(v => v.Value.Trim()).Distinct(StringComparer.Ordinal).ToList();

        return new CoaiEnvironment(variables, new Dictionary<string, string>(StringComparer.Ordinal), string.Empty, string.Empty, inherited);
    }

    /// <summary>A variable whose VALUE is scrubbed from every text a probe writes: a secret-named one, or a <c>BENCH_*</c> one — the
    /// database url among them, and a url carries its password.</summary>
    private static bool IsScrubbedName(string name) => IsSecretName(name) || name.StartsWith("BENCH_", StringComparison.OrdinalIgnoreCase);

    /// <summary>A variable the HARNESS owns — <c>BENCH_*</c> (the database url with its password, the artefact root, the CLI references)
    /// and <c>COAI_*</c> (the product's knobs) — which no probe child may inherit (S2c).</summary>
    public static bool IsHarnessVariable(string name) =>
        name.StartsWith("BENCH_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("COAI_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The launch environment: every variable, then the secret, LAST — nothing after this step can read or
    /// overwrite it, and nothing before it could see one. A reviewer that needs no vault gets no key.</summary>
    public ChildEnvironment WithSecret(SecretValue credsKey) =>
        new(new Dictionary<string, string>(Variables, StringComparer.Ordinal), credsKey, _inheritedSecrets);

    /// <summary>Whether a variable name is a secret's — removed from every snapshot, by name, whatever its case.</summary>
    public static bool IsSecretName(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper == CredsKeyVariable || upper.EndsWith("_KEY", StringComparison.Ordinal) || upper.EndsWith("_TOKEN", StringComparison.Ordinal)
               || upper.EndsWith("_SECRET", StringComparison.Ordinal) || upper.EndsWith("_PASSWORD", StringComparison.Ordinal);
    }

    /// <summary>A variable the harness sets per cell, which a run setting may not override.</summary>
    public static bool IsHarnessOwned(string name) => IsAxis(name.ToUpperInvariant());

    private static bool IsAxis(string name) => AxisVariables.ContainsKey(name) || CoaiVendorsSetting.IsVariable(name);

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>The environment a product process is launched with — the only type that holds the secret's characters
/// beside <see cref="SecretValue"/>, and it renders as a list of NAMES.</summary>
public sealed class ChildEnvironment
{
    private readonly SecretValue _secret;
    private readonly IReadOnlyList<string> _scrubbed;

    internal ChildEnvironment(Dictionary<string, string> variables, SecretValue secret, IReadOnlyList<string> inheritedSecrets)
    {
        _secret = secret;
        _scrubbed = [.. (secret.IsPresent ? [secret.Value, .. inheritedSecrets] : inheritedSecrets)
            .SelectMany(v => new[] { v, JsonSerializer.Serialize(v)[1..^1] })
            .Where(v => v.Length >= CoaiEnvironment.ShortestScrubbed)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(v => v.Length)];

        if (secret.IsPresent)
        {
            variables[CoaiEnvironment.CredsKeyVariable] = secret.Value;
        }

        Variables = variables;
    }

    /// <summary>For the process launcher only. Never logged, never serialised: log <see cref="Names"/>.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }

    public IReadOnlyList<string> Names => [.. Variables.Keys.Order(StringComparer.Ordinal)];

    public bool CarriesSecret => _secret.IsPresent;

    /// <summary>Removes every secret's characters — the vault key and the operator's inherited secrets, raw and as they
    /// read inside a JSON string — from text the harness is about to write: the product's stderr, a reply, a failure
    /// sentence. A child that echoes its environment leaves <c>[redacted]</c> on disk.</summary>
    public string Scrub(string text) =>
        text.Length == 0 ? text : _scrubbed.Aggregate(text, (current, secret) => current.Replace(secret, "[redacted]", StringComparison.Ordinal));

    public override string ToString() => $"{Variables.Count} variable(s): {string.Join(", ", Names)}";
}
