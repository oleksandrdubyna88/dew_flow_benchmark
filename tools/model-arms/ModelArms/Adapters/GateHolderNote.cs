using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The holder's note, <c>.gate.lock.holder</c> (beside <c>.gate.lock</c>, never IN it): who took the lock and when, so <c>gate-lock status</c> can say <c>held by pid N since &lt;utc&gt;</c>. <b>The kernel's lock is the truth and
/// this file is a hint.</b> It is written by the holder AFTER it has the lock, beside and renamed over (a reader never sees half a note), and removed BEFORE the lock is released (or a next holder's note could be deleted by the last one's
/// release); a killed holder leaves a stale note, which the next holder overwrites and which <c>status</c> never believes unless it names a LIVE process whose start time is the one the note recorded — on Linux the kernel's (field 22 of <c>/proc/&lt;pid&gt;/stat</c>, an integer in clock ticks), elsewhere <c>Process.StartTime</c> in whole seconds (<c>start_unix</c>) within two seconds (a reused pid does not match, unless it started within that tolerance of the old one). The
/// note is informational, so a write that fails (a full disk, a permission) does not fail the lock; a note that is not a regular file of its own, bigger than 4 KiB, not JSON or not a live holder reads as <c>holder unknown</c> — it is read through <see cref="BoundedFile"/> (the kernel's non-blocking open where the road has it, the runtime's under a ceiling elsewhere), so a FIFO, a link or a huge file costs no memory and no unbounded wait. A holder is believed by its road (<see cref="HolderRoad"/>): the kernel's tick on Linux, the process's start second within two seconds on the other POSIX hosts, never on Windows.
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

    /// <summary>The start second of <paramref name="pid"/> as the runtime reports it (<see cref="Process.StartTime"/>, whole UTC seconds since the epoch), on every platform; <c>null</c> when it cannot be read — a process that ended, one we may not look at.</summary>
    internal static long? StartUnixOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeSeconds();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
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
                JsonSerializer.Serialize(stream, new { pid = Environment.ProcessId, start_ticks = StartTicksOf(Environment.ProcessId), start_unix = StartUnixOf(Environment.ProcessId), since_utc = Format(since) });
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
    public static string Describe(string lockPath) => Describe(lockPath, PlatformFactsReader.CurrentRoad);

    /// <summary><see cref="Describe(string)"/> with the road that believes the note handed in.</summary>
    public static string Describe(string lockPath, PlatformRoad road) =>
        Believed(PathFor(lockPath), road) is { } note ? $"by pid {note.Pid} since {note.Since}" : "(holder unknown)";

    private sealed record Note(int Pid, string Since);

    private static Note? Believed(string note, PlatformRoad road) =>
        Read(note, road.Bounded) is { } read && IsLiveAndStartedThen(read, road.Holder) ? new Note(read.Pid, read.Since) : null;

    /// <summary>What a note says, as parsed: the pid, the kernel's start ticks (Linux writers), the start second (every writer) and the acquisition time.</summary>
    internal sealed record Raw(int Pid, long? StartTicks, long? StartUnix, string Since);

    /// <summary>The note, read through <see cref="KernelFacts.ReadSmall"/>: opened without blocking, judged by the descriptor (a regular file, the same inode the path names; a link is refused at the open by O_NOFOLLOW), never read past 4 KiB. Anything else, or text that is not
    /// a note, is <c>null</c> — never an exception and never a wait.</summary>
    private static Raw? Read(string note, BoundedRoad road) =>
        BoundedFile.Read(note, NoteLimit, road) is BoundedRead.Bytes bytes ? Parsed(bytes.Value) : null;

    private static Raw? Parsed(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var number)
                ? new Raw(number, Number(root, "start_ticks"), Number(root, "start_unix"), Text(root, "since_utc"))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var ticks) ? ticks : null;

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool IsLiveAndStartedThen(Raw note, HolderRoad road) =>
        road != HolderRoad.None && WorkerLiveness.ProcessIsAlive(note.Pid) && Believe(note, alive: true, StartTicksOf(note.Pid), StartUnixOf(note.Pid), road);

    /// <summary>Whether the note names the process that is there now, pure over what was read: <see cref="HolderRoad.ProcTicks"/> — the kernel's start tick equals the one recorded; <see cref="HolderRoad.ProcessStartTime"/> — the start second the process reports now is within
    /// <see cref="StartTolerance"/> of the one recorded (two readings of one pid differ by milliseconds, so whole seconds with a tolerance; a reused pid started within the tolerance of the old one is not told apart — the note is a hint, D10); <see cref="HolderRoad.None"/> (Windows) — never.
    /// A note without the fact the road needs is never believed.</summary>
    internal static bool Believe(Raw note, bool alive, long? liveTicks, long? liveStart, HolderRoad road) =>
        alive && road switch
        {
            HolderRoad.ProcTicks => note.StartTicks is { } recorded && liveTicks == recorded,
            HolderRoad.ProcessStartTime => note.StartUnix is { } recorded && liveStart is { } now && Math.Abs(now - recorded) <= StartTolerance,
            _ => false,
        };

    /// <summary>How far the start second a process reports may be from the one its note recorded, in seconds.</summary>
    private const int StartTolerance = 2;

    private static string Format(DateTimeOffset instant) => instant.ToUniversalTime().ToString(Utc, CultureInfo.InvariantCulture);
}
