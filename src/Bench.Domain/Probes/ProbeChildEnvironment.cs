using Bench.Domain.Gate;

namespace Bench.Domain.Probes;

/// <summary>The environment a probe launches a CLI under (S2c, review finding 3). Before S2c the CLIs inherited the bench's WHOLE
/// environment — <c>BENCH_DB</c> with its password, every <c>*_KEY</c> and <c>*_TOKEN</c> in the operator's shell — and a shell-capable
/// model running <c>Get-ChildItem Env:</c> would have written them into the artefacts and into the vendor's transcript. Now the child
/// gets EXACTLY the variables a CLI needs to run and to find its own login, by name, and nothing else:
/// <list type="bullet">
/// <item>the process basics — <c>PATH</c>, <c>PATHEXT</c>, <c>ComSpec</c>, <c>SystemRoot</c>, <c>windir</c>, <c>OS</c>, <c>NUMBER_OF_PROCESSORS</c>,
/// <c>PROCESSOR_*</c>, <c>ProgramFiles*</c>, <c>ProgramData</c>;</item>
/// <item>where its login and settings live — <c>USERPROFILE</c>, <c>HOME</c>, <c>HOMEDRIVE</c>, <c>HOMEPATH</c>, <c>APPDATA</c>, <c>LOCALAPPDATA</c>,
/// <c>USERNAME</c> (claude keeps its OAuth under <c>~/.claude</c>, codex under <c>~/.codex</c>, agy under <c>%LOCALAPPDATA%</c>);</item>
/// <item>a scratch place — <c>TEMP</c>, <c>TMP</c>, and <c>TMPDIR</c>, which .NET and most CLIs read on Linux and macOS instead (added
/// 2026-10-13: with TEMP alone a Linux child wrote its scratch under /tmp, outside the place the launcher chose).</item>
/// </list>
/// Measured live on 2026-10-01 with exactly this set: claude 2.1.258 (<c>sonnet</c>), codex-cli 0.156.1 (<c>gpt-5.6-terra</c>) and agy
/// 1.2.14 (<c>gemini-3.1-pro-high</c>) each started, found their login and answered. Never a <c>BENCH_*</c>, never a <c>COAI_*</c>, never a
/// secret-named variable, whatever this list says (<see cref="CoaiEnvironment.Minimal"/> refuses them structurally); and every
/// secret-named VALUE of the bench's own environment is scrubbed from the CLI's stdout, stderr and answer before they are written.
/// The names that passed — never the values — are recorded in each attempt's <c>argv.json</c>.</summary>
public static class ProbeChildEnvironment
{
    /// <summary>Exact names, compared case-insensitively — Windows spells <c>Path</c>, <c>ProgramData</c>; Linux spells <c>PATH</c>, <c>HOME</c>.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "PATH", "PATHEXT", "SystemRoot", "windir", "ComSpec", "OS", "NUMBER_OF_PROCESSORS", "ProgramData",
        "USERPROFILE", "HOME", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA", "USERNAME",
        "TEMP", "TMP", "TMPDIR",
    ];

    /// <summary>Name prefixes, compared case-insensitively: <c>ProgramFiles</c>, <c>ProgramFiles(x86)</c>, <c>ProgramW6432</c>; <c>PROCESSOR_ARCHITECTURE</c>, …</summary>
    public static IReadOnlyList<string> Prefixes { get; } = ["ProgramFiles", "ProgramW6432", "PROCESSOR_"];

    /// <summary>Whether a variable NAME is one a CLI child may inherit. Harness-owned and secret-named variables never pass, whatever
    /// they are called (<see cref="CoaiEnvironment.IsHarnessVariable"/>, <see cref="CoaiEnvironment.IsSecretName"/>).</summary>
    public static bool Passes(string name) =>
        !CoaiEnvironment.IsHarnessVariable(name) && !CoaiEnvironment.IsSecretName(name)
        && (Names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) || Prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The model-guard opt-out every probe and scout child carries (2026-10-13): these launches MEASURE a model, so the sub-agent
    /// model-guard hook installed in the user's <c>~/.claude</c> must neither refuse nor hint during one. A FIXED policy value, never
    /// inherited — the allow-list copies only named parent variables, so without this the child would not see it at all.</summary>
    public const string ModelGuardVariable = "SUBAGENT_MODEL_GUARD";

    public const string ModelGuardOff = "off";

    /// <summary>The child environment for a CLI launch over <paramref name="parent"/> (the bench's own): the pass-through variables and
    /// no secret — <see cref="ChildEnvironment.Scrub"/> still knows every secret-named value of the parent — plus
    /// <see cref="ModelGuardVariable"/>=<see cref="ModelGuardOff"/>, always, whatever the parent holds (any parent spelling of it is replaced).</summary>
    public static ChildEnvironment Of(IReadOnlyDictionary<string, string> parent) =>
        CoaiEnvironment.Minimal(WithGuardOff(parent), static name => Passes(name) || name == ModelGuardVariable).WithSecret(SecretValue.None);

    private static Dictionary<string, string> WithGuardOff(IReadOnlyDictionary<string, string> parent)
    {
        var variables = parent
            .Where(v => !string.Equals(v.Key, ModelGuardVariable, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        variables[ModelGuardVariable] = ModelGuardOff;

        return variables;
    }
}
