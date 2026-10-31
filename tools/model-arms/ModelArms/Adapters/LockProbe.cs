using System.Security.Cryptography;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>The gate lock's conflict code, MEASURED (E3.S0): what the host's own runtime throws when a second open of a file collides with a holder's <see cref="FileShare.None"/>. Linux's code was read from the runtime's source and observed (<c>EWOULDBLOCK</c>, 11); Windows' two
/// are the Win32 documentation's and stay a table in <see cref="FileGateLock"/>. Every other POSIX host (macOS and FreeBSD answer <c>EAGAIN</c> 35 by their headers, which this does not need to know) is observed here, at construction, instead of refused as unmeasured: a file
/// <c>.model-arms-lock-probe-&lt;8 random hex bytes&gt;</c> is opened <c>CreateNew | ReadWrite | FileShare.None</c> and held, a SECOND open of the same path <c>Open | ReadWrite | FileShare.None</c> must throw an <see cref="IOException"/> of exactly that type (its <c>HResult</c> is the code), the
/// holder is released, and a THIRD open must succeed — the control: an <c>EIO</c> on the second open would otherwise be taken for the lock's conflict and later accepted as "honoured". And, while the FIRST handle is still held, a DIFFERENT new file is created and closed (<see cref="IndependentOpen"/>): a descriptor limit (<c>EMFILE</c>) on the second open cannot be told from the conflict by the released control, which frees the one descriptor and so succeeds — a host that cannot open a second file is "descriptors exhausted?", never a measured code. The probe file and the control's file are then deleted.
/// <para>
/// A second open that SUCCEEDS means file locking is inert in that folder (the runtime ignores a refused <c>flock</c> silently) and no gate lock is attempted. Growth: one 0-byte file per construction, deleted at the end; a crash leaves at most one per start in the temp folder,
/// named for what it is. <see cref="Decide"/> is the pure half over what the opens threw; <see cref="Measure"/> is the live one. <c>DeleteOnClose</c> is NOT used: the file would be gone before the third open.
/// </para></summary>
internal static class LockProbe
{
    /// <summary>The conflict code this host's runtime reports, or why it cannot be measured: the folder is not writable, locking is inert there, or the second open failed for another reason.</summary>
    public static Outcome<int> Measure(string folder) => Measure(folder, SecondOpen);

    /// <summary>The same with what a second open does handed in: the only way a test can see a folder where locking is inert, which the real runtime on a healthy file system never shows.</summary>
    internal static Outcome<int> Measure(string folder, Func<string, Exception?> secondOpen) => Measure(folder, secondOpen, IndependentOpen);

    /// <summary>The same with the control's open handed in as well: the only way a test can see a host out of descriptors.</summary>
    internal static Outcome<int> Measure(string folder, Func<string, Exception?> secondOpen, Func<string, Exception?> independentOpen)
    {
        var path = Path.Combine(folder, $".model-arms-lock-probe-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}");
        try
        {
            Exception? second;
            Exception? independent;
            using (new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                second = secondOpen(path);
                independent = independentOpen(folder);
            }

            return Decide(second, second is null ? null : secondOpen(path), independent, folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<int>.Failure($"the gate lock's conflict code could not be measured in {folder}: {ex.Message}");
        }
        finally
        {
            Remove(path);
        }
    }

    /// <summary>What the opens decided, pure: <paramref name="secondOpen"/> is what the second open threw (<c>null</c> = it succeeded), <paramref name="released"/> what the third open — after the holder let go — threw (<c>null</c> = it succeeded, as it must), <paramref name="independent"/> what
    /// the open of a DIFFERENT new file threw while the first handle was still held (<c>null</c> = it succeeded, as it must). The last is judged first: a host with no descriptor left for a second file cannot say anything about the lock, whatever the second open threw (the released control frees the one descriptor, so
    /// it cannot see that).</summary>
    public static Outcome<int> Decide(Exception? secondOpen, Exception? released, Exception? independent, string folder = "the probe folder") =>
        independent is not null
            ? Outcome<int>.Failure($"the probe could not open a second file while holding the first ({independent.Message}) — descriptors exhausted?; no gate lock is attempted")
            : Decide(secondOpen, released, folder);

    private static Outcome<int> Decide(Exception? secondOpen, Exception? released, string folder) =>
        secondOpen switch
        {
            null => Outcome<int>.Failure($"file locking is inert in {folder}: a second open of one file succeeded — no gate lock is attempted here"),
            IOException conflict when conflict.GetType() == typeof(IOException) => released is null
                ? Outcome<int>.Success(conflict.HResult)
                : Outcome<int>.Failure($"the probe's second open failed for a reason other than the lock: {released.Message}"),
            _ => Outcome<int>.Failure($"the probe's second open failed in a way that is not a lock conflict: {secondOpen.Message}"),
        };

    /// <summary>The control that stops a descriptor limit from being read as the lock: while the FIRST handle is still held, a DIFFERENT new file in <paramref name="folder"/> is created and closed; what it threw, or <c>null</c>. It is deleted after, whatever came of it.</summary>
    internal static Exception? IndependentOpen(string folder)
    {
        var control = Path.Combine(folder, $".model-arms-lock-probe-control-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}");
        try
        {
            using var stream = new FileStream(control, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
        finally
        {
            Remove(control);
        }
    }

    /// <summary>One open of the probe file with no sharing, closed at once; what it threw, or <c>null</c>. Whatever it throws is the data: the classification is <see cref="Decide"/>'s.</summary>
    private static Exception? SecondOpen(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the probe file is named for what it is; a leftover is one empty file
        }
    }
}
