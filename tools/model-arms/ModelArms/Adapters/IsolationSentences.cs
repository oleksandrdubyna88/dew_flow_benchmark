using System.Runtime.InteropServices;
using ModelArms.Core.Isolation;

namespace ModelArms.Adapters;

/// <summary>The sentences both isolation adapters say, spelled once: what the verification found for one entry, and what a refused mode call came to.</summary>
internal static class IsolationSentences
{
    public static string Listable(string path) => $"{path} is still listable by this user";

    public static string Readable(string path) => $"{path} is still readable by this user";

    public static string Traversable(string path) => $"{path} is still traversable by this user";

    public static string Vanished(string path, string why) => $"{path} vanished during the lock: {why}";

    /// <summary>A refused mode call in the operating system's words.</summary>
    public static string Refused(string path, int errno) => $"{path}: {Marshal.GetPInvokeErrorMessage(errno)}";

    public const int Enoent = 2;

    public const int Eacces = 13;

    public const int Enotdir = 20;

    /// <summary>The saved mode as the bits a <c>chmod</c> takes; a record that came from <see cref="LockSnapshots.Saved"/> always parses.</summary>
    public static int SavedBits(SavedMode saved) => SnapshotFiles.ModeBits(saved.Mode) is Bench.Domain.Outcome<int>.Ok ok ? ok.Value : 0;

    public static string Joined(IEnumerable<string> sentences) => string.Join("; ", sentences);
}
