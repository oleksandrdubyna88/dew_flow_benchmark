using Bench.Domain;

namespace Bench.Infrastructure.Models;

/// <summary>A CLI's bare word (<c>codex</c>, <c>claude</c>) resolved the way a shell resolves it — BEFORE anything is sent to it
/// (D5 of the fidelity plan). On Windows the npm install of a CLI is a <c>.cmd</c>/<c>.ps1</c> shim beside a native
/// <c>.exe</c> deep in <c>node_modules</c>; the process launcher does not run the bare word, and an assessor that "is not
/// installed" was recorded finding by finding as a failed assessment while <c>codex --version</c> answered in the same shell.
/// <para>
/// The first <c>PATH</c> directory holding a match decides, as the shell's order does. A native executable is returned by
/// its full path; a shim is refused naming it — its arguments would pass through <c>cmd.exe</c>, a quoting hazard for a
/// prompt — and so is a word on no directory. Pure over its inputs: the caller hands in <c>PATH</c>, <c>PATHEXT</c> and how
/// to ask whether a file exists, so every rule is testable on any system.
/// </para></summary>
public static class CliExecutable
{
    private static readonly string[] Native = [".exe", ".com"];

    public static Outcome<string> Resolve(string word, string path, string pathExt, bool windows, Func<string, bool> exists)
    {
        var separator = windows ? ';' : ':';
        var candidates = windows ? Extensions(pathExt).Select(e => word + e).ToArray() : [word];
        var hit = path.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(dir => candidates.Select(name => Join(dir, name, windows)).FirstOrDefault(exists))
            .FirstOrDefault(found => found is not null) ?? string.Empty;

        return (hit.Length, windows && !Native.Any(e => hit.EndsWith(e, StringComparison.OrdinalIgnoreCase))) switch
        {
            (0, _) => Outcome<string>.Failure(
                $"'{word}' is not on PATH — add a reviewer row naming the CLI's native executable with --executable-ref"),
            (_, true) => Outcome<string>.Failure(
                $"'{word}' resolves to {hit}, a shim, not a native executable — it is never launched (its arguments would pass through "
                + "cmd.exe); add a reviewer row naming the native executable with --executable-ref"),
            _ => Outcome<string>.Success(hit),
        };
    }

    /// <summary>The same resolution over this process's own environment and file system.</summary>
    public static Outcome<string> OnThisMachine(string word) =>
        Resolve(
            word, Environment.GetEnvironmentVariable("PATH") ?? string.Empty, Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD",
            OperatingSystem.IsWindows(), File.Exists);

    /// <summary>PATHEXT's own order, then the native extensions it left out — an empty or partial PATHEXT must not hide a
    /// real <c>.exe</c> (code round, 2026-09-29).</summary>
    private static IEnumerable<string> Extensions(string pathExt) =>
        pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(e => e.ToLowerInvariant()).Concat(Native).Distinct(StringComparer.Ordinal);

    private static string Join(string dir, string name, bool windows) =>
        windows ? dir.TrimEnd('\\', '/') + "\\" + name : dir.TrimEnd('/') + "/" + name;
}
