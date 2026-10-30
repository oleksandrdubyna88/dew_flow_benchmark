using System.Text;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The two files of D11's snapshot, read BY NAME through the bounded road (C16) and handed to <see cref="LockSnapshots"/>: <c>.locked-modes</c> and <c>.locked-modes.applied</c> of the root. The root's own files take a small limit, because under the lock an arm can still create a file by name in the root. Read-only —
/// nothing here restores a mode; only <c>lock</c> and <c>unlock</c> do (D11). <c>status</c> prints the state, and so do <c>make</c> and <c>checks</c> (E3.S3 and E3.S5) through the same two members.</summary>
internal static class IsolationFiles
{
    /// <summary>The most either file may hold: a snapshot is a few hundred bytes per folder.</summary>
    public const int LimitBytes = 1024 * 1024;

    /// <summary>The state on this host's bounded road.</summary>
    public static LockState State(RootPaths root) => State(root, PlatformFactsReader.CurrentRoad);

    public static LockState State(RootPaths root, PlatformRoad road) => State(root, (path, limit) => BoundedFile.Read(path, limit, road));

    /// <summary>The same with the read handed in (<c>StatusReads.Read</c>, so the verb's one seam sees these two opens too).</summary>
    public static LockState State(RootPaths root, Func<string, int, BoundedRead> read) =>
        LockSnapshots.State(Text(root.LockedModes, read), Text(root.LockedModesApplied, read));

    /// <summary>The bytes as UTF-8 text; a file that is not text (a NUL byte, a UTF-16 mark) is a refusal, never garbage read as JSON.</summary>
    internal static Read<string> Text(string path, Func<string, int, BoundedRead> read) =>
        read(path, LimitBytes) switch
        {
            BoundedRead.Bytes bytes => Decoded(path, bytes.Value),
            BoundedRead.Refused refused => new Read<string>.Refused(refused.Reason),
            _ => new Read<string>.Absent(),
        };

    private static Read<string> Decoded(string path, byte[] bytes)
    {
        try
        {
            return new Read<string>.Present(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return new Read<string>.Refused($"{path} is not UTF-8 text");
        }
    }
}
