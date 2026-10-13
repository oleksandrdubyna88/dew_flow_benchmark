using System.Globalization;

namespace Bench.Cli;

/// <summary>Where an unhandled exception of a <c>bench scouts</c> verb goes (S4, §3.13): its whole text — message, inner exceptions, stack
/// — into one file per fault under a LOCAL folder, never into the console or the log, because the message of an I/O failure quotes the path
/// it failed on and a private task's tree is made of private paths. The console and the log get the exception's TYPE and the file's NAME;
/// the stack trace carries method names and line numbers, never values, and stays in the file beside the message.</summary>
public static class ScoutsFault
{
    public const string Folder = "scouts-faults";

    /// <summary><c>%LOCALAPPDATA%/bench/scouts-faults</c> — beside the checkout cache, outside every repository.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bench", Folder);

    /// <summary>Writes the fault and answers the file's name; empty when even that failed (the console then says so).</summary>
    public static string Withhold(Exception ex, string directory)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), ex.ToString() + "\n");
            return name;
        }
        catch (Exception io) when (io is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>How the console names the fault file: by folder and name, the default folder spelled by its variable rather than expanded
    /// — an expanded one is a home path.</summary>
    public static string Describe(string directory, string name) =>
        name.Length == 0
            ? "no fault file — the folder could not be written; re-run with the log level raised"
            : $"{(directory == DefaultDirectory ? $"%LOCALAPPDATA%/bench/{Folder}" : directory)}/{name}";
}
