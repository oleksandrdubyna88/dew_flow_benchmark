using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The holder's note, <c>.gate.lock.holder</c> (beside <c>.gate.lock</c>, never IN it): who took the lock and when, so <c>gate-lock status</c> can say <c>held by pid N since &lt;utc&gt;</c>. <b>The kernel's lock is the truth and
/// this file is a hint.</b> It is written by the holder AFTER it has the lock, beside and renamed over (a reader never sees half a note), and removed BEFORE the lock is released (or a next holder's note could be deleted by the last one's
/// release); a killed holder leaves a stale note, which the next holder overwrites and which <c>status</c> never believes unless it names a LIVE process whose kernel start time (field 22 of <c>/proc/&lt;pid&gt;/stat</c>, an integer in clock ticks) is the one the note recorded (a reused pid does not match). The
/// note is informational, so a write that fails (a full disk, a permission) does not fail the lock; a note that is not a regular file of its own, bigger than 4 KiB, not JSON or not a live holder reads as <c>holder unknown</c> — it is opened without blocking and read through the descriptor (<see cref="KernelFacts.ReadSmall"/>), so a FIFO, a link or a huge file costs no wait and no memory. Off Linux nothing can be verified, so no holder is ever named.
/// <para>
/// The temporary file has one fixed name, <c>.gate.lock.holder.tmp</c>: only the holder of the lock writes it, so it cannot collide, and a crash between the write and the rename leaves exactly one such file, overwritten by the next
/// holder (the growth bound: two small files per root).
/// </para></summary>
internal static class GateHolderNote
{
    private const string Suffix = ".holder";

    private const string Utc = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string PathFor(string lockPath) => lockPath + Suffix;

    /// <summary>The note may be at most this big; a holder's note is under 200 bytes.</summary>
    private const int NoteLimit = 4096;

    /// <summary>The 1-based field of <c>/proc/&lt;pid&gt;/stat</c> that holds <c>starttime</c>, in clock ticks since boot (<c>proc_pid_stat(5)</c>); counted from the state, which is field 3, so its token index after the command name is 19.</summary>
    private const int StartTimeToken = 22 - 3;

    /// <summary>The kernel's start time of <paramref name="pid"/> — an INTEGER every reader sees the same, unlike <see cref="Process.StartTime"/>, which two readings of one pid can differ on by a millisecond or two (a live holder that started near a
    /// second boundary then read as unknown about once in thirty reads). <c>null</c> when it cannot be read: off Linux, a process that ended, one we may not look at. A holder is never believed without it.</summary>
    internal static long? StartTicksOf(int pid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            return StartTicksFrom(File.ReadAllText($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Field 22 of a <c>/proc/&lt;pid&gt;/stat</c> text. The command name (field 2) is in parentheses and can hold spaces and <c>)</c>, so the fields are counted after the LAST <c>)</c>; a line too short or a field that is not a whole number is <c>null</c>.</summary>
    internal static long? StartTicksFrom(string stat)
    {
        var close = stat.LastIndexOf(')');
        var fields = close < 0 ? [] : stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > StartTimeToken && long.TryParse(fields[StartTimeToken], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ? ticks : null;
    }

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
                JsonSerializer.Serialize(stream, new { pid = Environment.ProcessId, start_ticks = StartTicksOf(Environment.ProcessId), since_utc = Format(since) });
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

    private static Note? Believed(string note) =>
        Read(note) is { } read && IsLiveAndStartedThen(read.Pid, read.StartTicks) ? new Note(read.Pid, read.Since) : null;

    private sealed record Raw(int Pid, long? StartTicks, string Since);

    /// <summary>The note, read through <see cref="KernelFacts.ReadSmall"/>: opened without blocking, judged by the descriptor (a regular file, the same inode the path names, no link), never read past 4 KiB. Anything else, or text that is not
    /// a note, is <c>null</c> — never an exception and never a wait.</summary>
    private static Raw? Read(string note)
    {
        var bytes = KernelFacts.ReadSmall(note, NoteLimit);
        return bytes.Match(Parsed, _ => null);
    }

    private static Raw? Parsed(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var number)
                ? new Raw(number, Ticks(root), Text(root, "since_utc"))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? Ticks(JsonElement root) =>
        root.TryGetProperty("start_ticks", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var ticks) ? ticks : null;

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    /// <summary>The pid runs AND the kernel's start time of that pid is the one the note recorded (a reused pid has another). A note with no start time, or a platform that cannot read one, is never believed.</summary>
    private static bool IsLiveAndStartedThen(int pid, long? recorded) =>
        recorded is not null && WorkerLiveness.ProcessIsAlive(pid) && StartTicksOf(pid) == recorded;

    private static string Format(DateTimeOffset instant) => instant.ToUniversalTime().ToString(Utc, CultureInfo.InvariantCulture);
}
