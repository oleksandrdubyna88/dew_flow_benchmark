using System.Text.Json;
using Bench.Domain;

namespace ModelArms.Core.Isolation;

/// <summary>Which inode an entry was when it was locked: the device (<c>major &lt;&lt; 32 | minor</c>) and the inode number. Named <c>NodeIdentity</c>, not <c>FileIdentity</c>, because <c>ModelArms.Core.Experiment.FileIdentity</c> is the trust check's (kind and change time too — and a <c>chmod</c> changes the change time).
/// It is what tells, at <c>unlock</c>, an entry that was replaced since the lock from the one that was locked; the runtime road cannot read it, so there it is <c>null</c> on both sides.</summary>
public sealed record NodeIdentity(ulong Device, ulong Inode);

/// <summary>One path of the snapshot: what it is, the 12 permission bits it had before the lock as octal text (<c>stat -c %a</c>: <c>755</c>, <c>2755</c>, <c>0</c>) and, on the kernel road, which inode.</summary>
public sealed record SavedMode(string Path, IsolationKind Kind, string Mode, NodeIdentity? Identity);

/// <summary>What <c>.locked-modes</c> holds: the run, the digest of the plan, the moment of the snapshot and the saved modes in lock order.</summary>
public sealed record SavedSnapshot(string Run, string Digest, string Since, IReadOnlyList<SavedMode> Modes);

/// <summary>The bytes of D11's two files, as values: <c>.locked-modes</c> is <c>{"run","digest","since","modes":[{"path","kind","mode"[,"dev","ino"]}]}</c> and <c>.locked-modes.applied</c> is <c>{"run","digest","verifiedAt"}</c>, each followed by <c>\n</c>. The reader of both is <see cref="LockSnapshots"/> — one reader, two writers.</summary>
public static class SnapshotFiles
{
    public const string Utc = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private const int OctalBase = 8;
    private const int MostModeDigits = 4;

    public static string Snapshot(SavedSnapshot saved) =>
        JsonText.Write(
            new JsonWriterOptions { Indented = false },
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("run", saved.Run);
                writer.WriteString("digest", saved.Digest);
                writer.WriteString("since", saved.Since);
                writer.WriteStartArray("modes");
                foreach (var mode in saved.Modes)
                {
                    Write(writer, mode);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }) + "\n";

    public static string Applied(string run, string digest, string verifiedAt) =>
        JsonText.Write(
            new JsonWriterOptions { Indented = false },
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("run", run);
                writer.WriteString("digest", digest);
                writer.WriteString("verifiedAt", verifiedAt);
                writer.WriteEndObject();
            }) + "\n";

    /// <summary><c>0o755</c> as <c>"755"</c>, <c>0</c> as <c>"0"</c>, <c>0o2755</c> as <c>"2755"</c>.</summary>
    public static string ModeText(int permissionBits) => Convert.ToString(permissionBits, OctalBase);

    /// <summary>The inverse: one to four octal digits, else a failure naming the text.</summary>
    public static Outcome<int> ModeBits(string text) =>
        text.Length is >= 1 and <= MostModeDigits && text.All(digit => digit is >= '0' and <= '7')
            ? Outcome<int>.Success(Convert.ToInt32(text, OctalBase))
            : Outcome<int>.Failure($"'{text}' is not a mode: one to four octal digits");

    private static void Write(Utf8JsonWriter writer, SavedMode mode)
    {
        writer.WriteStartObject();
        writer.WriteString("path", mode.Path);
        writer.WriteString("kind", mode.Kind == IsolationKind.Closed ? "closed" : "unlisted");
        writer.WriteString("mode", mode.Mode);
        if (mode.Identity is { } identity)
        {
            writer.WriteNumber("dev", identity.Device);
            writer.WriteNumber("ino", identity.Inode);
        }

        writer.WriteEndObject();
    }
}
