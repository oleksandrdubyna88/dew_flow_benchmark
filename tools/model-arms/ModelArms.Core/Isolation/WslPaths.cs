using Bench.Domain;

namespace ModelArms.Core.Isolation;

/// <summary>A Linux path in the spelling Windows' <c>pwsh.exe</c> reads it by, from inside WSL (E5.S2): a drive mounted under <c>/mnt/&lt;letter&gt;</c> is that drive, anything else is the distribution's file system reached through <c>\\wsl.localhost\&lt;distro&gt;\</c> (the form measured to run, M-9).
/// <para>
/// Pure text: nothing is opened. What this does NOT prove is that the path exists or that Windows can reach it.
/// </para></summary>
public static class WslPaths
{
    public const string DistroSentence = "WSL_DISTRO_NAME is not set — the keep-awake script's Windows path cannot be named";

    public static Outcome<string> Windows(string linuxPath, string distro) =>
        Refusal(linuxPath, distro) is { Length: > 0 } refusal
            ? Outcome<string>.Failure(refusal)
            : Outcome<string>.Success(Mounted(linuxPath) ?? Unc(linuxPath, distro));

    private static string Refusal(string linuxPath, string distro) =>
        linuxPath switch
        {
            _ when !linuxPath.StartsWith('/') => $"'{linuxPath}' is not an absolute Linux path",
            _ when linuxPath.Contains('\\') || linuxPath.Contains('\0') => $"'{linuxPath.Replace("\0", "\\0", StringComparison.Ordinal)}' holds a backslash or a NUL — it cannot be written as a Windows path",
            _ when linuxPath.Split('/').Contains("..") => $"'{linuxPath}' holds a '..' segment — it is not a path to hand to Windows",
            _ when distro.Length == 0 => DistroSentence,
            _ when distro.Contains('\\') || distro.Contains('/') => $"WSL_DISTRO_NAME '{distro}' holds a slash or a backslash",
            _ => string.Empty,
        };

    /// <summary><c>/mnt/c</c> or <c>/mnt/c/rest</c> (one letter) as <c>C:\</c> + rest; else null.</summary>
    private static string? Mounted(string linuxPath)
    {
        var parts = linuxPath.Split('/');
        return parts is ["", "mnt", { Length: 1 } letter, ..] && char.IsAsciiLetter(letter[0])
            ? char.ToUpperInvariant(letter[0]) + @":\" + string.Join('\\', parts.Skip(3))
            : null;
    }

    private static string Unc(string linuxPath, string distro) =>
        $@"\\wsl.localhost\{distro}\" + linuxPath[1..].Replace('/', '\\');
}
