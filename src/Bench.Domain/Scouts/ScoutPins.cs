namespace Bench.Domain.Scouts;

/// <summary>What a scout run is pinned to (§3.4): the CLI version, the plugin cache hash (which pins the agent type), the
/// resolved model ids behind <c>opus</c> and <c>sonnet</c>, and the operator's settings hash — effort levels included, because
/// they are the real condition the rule will run under. Frozen on the run; <b>a change of any pin mid-run stops the run</b>.
/// The per-cell CLI binary is a <see cref="Gate.ProductPin"/> on the cell, as for the probes; the tree hash is the task's.</summary>
public sealed record ScoutPins
{
    private ScoutPins(string cliVersion, string pluginCacheHash, string resolvedOpus, string resolvedSonnet, string settingsHash)
    {
        CliVersion = cliVersion;
        PluginCacheHash = pluginCacheHash;
        ResolvedOpus = resolvedOpus;
        ResolvedSonnet = resolvedSonnet;
        SettingsHash = settingsHash;
    }

    public string CliVersion { get; }

    public string PluginCacheHash { get; }

    public string ResolvedOpus { get; }

    public string ResolvedSonnet { get; }

    public string SettingsHash { get; }

    /// <summary>Every pin is a single token with no whitespace — a version, a hash, a model id. An empty pin is not a pin.</summary>
    public static Outcome<ScoutPins> Of(string? cliVersion, string? pluginCacheHash, string? resolvedOpus, string? resolvedSonnet, string? settingsHash)
    {
        string[] values = [.. new[] { cliVersion, pluginCacheHash, resolvedOpus, resolvedSonnet, settingsHash }.Select(v => (v ?? string.Empty).Trim())];
        string[] names = ["the CLI version", "the plugin cache hash", "the resolved opus id", "the resolved sonnet id", "the settings hash"];
        var bad = Array.FindIndex(values, v => v.Length == 0 || v.Any(char.IsWhiteSpace));

        return bad >= 0
            ? Outcome<ScoutPins>.Failure($"{names[bad]} is not a pin — one token, never empty")
            : Outcome<ScoutPins>.Success(new ScoutPins(values[0], values[1], values[2], values[3], values[4]));
    }

    /// <summary>The resolved id the read-back holds every child of an arm on this model to.</summary>
    public string Resolved(ScoutModel model) => model == ScoutModel.Opus ? ResolvedOpus : ResolvedSonnet;

    /// <summary>May the run go on under <paramref name="current"/>? Refused naming every pin that moved, both values — the run stops,
    /// and a moved alias is a new run, never an extension of this one.</summary>
    public static Outcome<ScoutPins> Continue(ScoutPins frozen, ScoutPins current)
    {
        var moved = new (string Name, string Was, string Now)[]
        {
            ("CLI version", frozen.CliVersion, current.CliVersion),
            ("plugin cache hash", frozen.PluginCacheHash, current.PluginCacheHash),
            ("resolved opus id", frozen.ResolvedOpus, current.ResolvedOpus),
            ("resolved sonnet id", frozen.ResolvedSonnet, current.ResolvedSonnet),
            ("settings hash", frozen.SettingsHash, current.SettingsHash),
        }.Where(p => !string.Equals(p.Was, p.Now, StringComparison.Ordinal)).ToList();

        return moved.Count == 0
            ? Outcome<ScoutPins>.Success(current)
            : Outcome<ScoutPins>.Failure("a pin moved mid-run, so the run stops — " + string.Join("; ", moved.Select(p => $"{p.Name} {p.Was} → {p.Now}")));
    }
}
