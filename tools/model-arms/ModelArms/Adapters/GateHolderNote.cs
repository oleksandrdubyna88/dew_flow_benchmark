using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The holder's note, <c>.gate.lock.holder</c> (beside <c>.gate.lock</c>, never IN it): who took the lock and when, so <c>gate-lock status</c> can say <c>held by pid N since &lt;utc&gt;</c>. <b>The kernel's lock is the truth and
/// this file is a hint.</b> It is written by the holder AFTER it has the lock, beside and renamed over (a reader never sees half a note), and removed BEFORE the lock is released (or a next holder's note could be deleted by the last one's
/// release); a killed holder leaves a stale note, which the next holder overwrites and which <c>status</c> never believes unless it names a LIVE process that STARTED when the note says (a reused pid does not match). The
/// note is informational, so a write that fails (a full disk, a permission) does not fail the lock; a note that is not a regular file, not JSON or not a live holder reads as <c>holder unknown</c>.
/// <para>
/// The temporary file has one fixed name, <c>.gate.lock.holder.tmp</c>: only the holder of the lock writes it, so it cannot collide, and a crash between the write and the rename leaves exactly one such file, overwritten by the next
/// holder (the growth bound: two small files per root).
/// </para></summary>
internal static class GateHolderNote
{
    private const string Suffix = ".holder";

    private const string Utc = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string PathFor(string lockPath) => lockPath + Suffix;

    /// <summary>Writes the note of THIS process, taken at <paramref name="since"/>. Best effort: an IO fault is the note's, never the lock's.</summary>
    public static void Write(string lockPath, DateTimeOffset since)
    {
        var note = PathFor(lockPath);
        var temporary = note + ".tmp";
        try
        {
            File.Delete(temporary);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new { pid = Environment.ProcessId, started_utc = Format(StartOfThisProcess()), since_utc = Format(since) });
            }

            File.Move(temporary, note, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the note is informational: a lock that cannot say who holds it is still a lock
        }
    }

    /// <summary>Removes the note; a missing one is not a fault.</summary>
    public static void Remove(string lockPath)
    {
        try
        {
            File.Delete(PathFor(lockPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // a stale note is read as one by status; nothing to do
        }
    }

    /// <summary>The words <c>status</c> adds after <c>held</c> for a lock somebody has: <c>by pid N since &lt;utc&gt;</c> when the note is a regular file naming a live process that started at the time it says, else <c>(holder unknown)</c>.</summary>
    public static string Describe(string lockPath) =>
        Believed(PathFor(lockPath)) is { } note ? $"by pid {note.Pid} since {note.Since}" : "(holder unknown)";

    private sealed record Note(int Pid, string Since);

    private static Note? Believed(string note)
    {
        if (GateLockPath.Of(note) is not (GateLockPath.Regular or GateLockPath.Unjudged))
        {
            return null;
        }

        return Read(note) is { } read && IsLiveAndStartedThen(read.Pid, read.Started) ? new Note(read.Pid, read.Since) : null;
    }

    private sealed record Raw(int Pid, string Started, string Since);

    private static Raw? Read(string note)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(note));
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var number)
                ? new Raw(number, Text(root, "started_utc"), Text(root, "since_utc"))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool IsLiveAndStartedThen(int pid, string started) =>
        WorkerLiveness.ProcessIsAlive(pid) && string.Equals(StartOf(pid), started, StringComparison.Ordinal);

    /// <summary>The start time of a process as the note spells it, or <c>""</c> when it cannot be read (a process that ended, or one we may not look at).</summary>
    private static string StartOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return Format(process.StartTime);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    private static DateTimeOffset StartOfThisProcess()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime;
    }

    private static string Format(DateTimeOffset instant) => instant.ToUniversalTime().ToString(Utc, CultureInfo.InvariantCulture);
}
