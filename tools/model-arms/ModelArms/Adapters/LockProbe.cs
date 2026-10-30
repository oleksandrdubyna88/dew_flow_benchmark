using System.Security.Cryptography;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>The gate lock's conflict code, MEASURED (E3.S0): what the host's own runtime throws when a second open of a file collides with a holder's <see cref="FileShare.None"/>. Linux's code was read from the runtime's source and observed (<c>EWOULDBLOCK</c>, 11); Windows' two
/// are the Win32 documentation's and stay a table in <see cref="FileGateLock"/>. Every other POSIX host (macOS and FreeBSD answer <c>EAGAIN</c> 35 by their headers, which this does not need to know) is observed here, at construction, instead of refused as unmeasured: a file
/// <c>.model-arms-lock-probe-&lt;8 random hex bytes&gt;</c> is opened <c>CreateNew | ReadWrite | FileShare.None</c> and held, a SECOND open of the same path <c>Open | ReadWrite | FileShare.None</c> must throw an <see cref="IOException"/> of exactly that type (its <c>HResult</c> is the code), the
/// holder is released, and a THIRD open must succeed — the control: an <c>EIO</c> or a descriptor limit on the second open would otherwise be taken for the lock's conflict and later accepted as "honoured". The probe file is then deleted.
/// <para>
/// A second open that SUCCEEDS means file locking is inert in that folder (the runtime ignores a refused <c>flock</c> silently) and no gate lock is attempted. Growth: one 0-byte file per construction, deleted at the end; a crash leaves at most one per start in the temp folder,
/// named for what it is. <see cref="Decide"/> is the pure half over what the two opens threw; <see cref="Measure"/> is the live one. <c>DeleteOnClose</c> is NOT used: the file would be gone before the third open.
/// </para></summary>
internal static class LockProbe
{
    /// <summary>The conflict code this host's runtime reports, or why it cannot be measured: the folder is not writable, locking is inert there, or the second open failed for another reason.</summary>
    public static Outcome<int> Measure(string folder)
    {
        var path = Path.Combine(folder, $".model-arms-lock-probe-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}");
        try
        {
            Exception? second;
            using (new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                second = SecondOpen(path);
            }

            return Decide(second, second is null ? null : SecondOpen(path));
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

    /// <summary>What the two opens decided, pure: <paramref name="secondOpen"/> is what the second open threw (<c>null</c> = it succeeded), <paramref name="released"/> what the third open — after the holder let go — threw (<c>null</c> = it succeeded, as it must).</summary>
    public static Outcome<int> Decide(Exception? secondOpen, Exception? released) =>
        secondOpen switch
        {
            null => Outcome<int>.Failure("file locking is inert in this folder: a second open of one file succeeded — no gate lock is attempted here"),
            IOException conflict when conflict.GetType() == typeof(IOException) => released is null
                ? Outcome<int>.Success(conflict.HResult)
                : Outcome<int>.Failure($"the probe's second open failed for a reason other than the lock: {released.Message}"),
            _ => Outcome<int>.Failure($"the probe's second open failed in a way that is not a lock conflict: {secondOpen.Message}"),
        };

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
