using System.Globalization;
using System.Text;
using ModelArms.Core.Isolation;

namespace ModelArms.Adapters;

/// <summary>The one way a log file of the experiment root is appended to (<c>lock.log</c>, <c>keep-awake.log</c>): one line <c>&lt;utc&gt; &lt;text&gt;</c>, opened by NAME with <see cref="FileMode.Append"/>, and only when the name is not a link (a link there: one line on <paramref name="error"/>, nothing appended).
/// A failed append never throws: it is one line on <paramref name="error"/>. <b>The residual:</b> between the link check and the open a link could be planted — only the operator can write the root (D17). Nothing here lists a folder; the root may be at mode 300.</summary>
internal static class RootLogFile
{
    public static void Append(string path, TimeProvider clock, string text, TextWriter error)
    {
        try
        {
            if (BoundedFile.IsLink(path))
            {
                error.WriteLine($"model-arms: {path} is a symbolic link — not appended to (D17)");
                return;
            }

            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes($"{clock.GetUtcNow().ToString(SnapshotFiles.Utc, CultureInfo.InvariantCulture)} {text}\n"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"model-arms: {path} could not be appended to: {ex.Message}");
        }
    }
}
