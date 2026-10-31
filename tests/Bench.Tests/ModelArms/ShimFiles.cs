using System.ComponentModel;
using System.Diagnostics;

namespace Bench.Tests.ModelArms;

/// <summary>The one way this suite writes a shell script it is about to START (a recording shim, a planted hook, a filter): written, made <c>0755</c>, and PROVED startable before the path is handed on.
/// <para>
/// Why: a script that is written and started at once can fail to start with <c>ETXTBSY</c> (<i>Text file busy</i>) while another thread of the same process forks — the fork's child holds a copy of the write descriptor until its own <c>exec</c>, however short, and <c>exec</c> of a file somebody holds open for writing is refused. Measured on this runtime with six threads forking and four writing: 144 of 1200 starts failed with
/// <i>Text file busy</i>. The suite runs its tests in parallel, so a shim started right after it was written sometimes never ran — and the test read a log that was never written (<c>ArmGitWideningTests</c>, 2 of 13 namespace runs). A failure that depends on what ELSE is running is shared state, not a flake.
/// </para>
/// <para>
/// How: the script gets a first line that makes it exit at once when <see cref="ProbeVariable"/> is set, and the writer starts it that way until a start succeeds. A successful start means no process held it for writing at that moment, and none can after: a descriptor that is closed is not inherited by a later fork. The probe runs nothing of the shim's own body.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class ShimFiles
{
    /// <summary>The variable the probe start sets; the guard line the writer adds exits when it is set.</summary>
    public const string ProbeVariable = "MODEL_ARMS_SHIM_PROBE";

    private const int TextFileBusy = 26;
    private const int Tries = 500;
    private static readonly TimeSpan Backoff = TimeSpan.FromMilliseconds(10);

    /// <summary>Writes <paramref name="script"/> (it starts with <c>#!/bin/sh</c> and a newline) at <paramref name="path"/>, its folder made, mode <c>0755</c>, and returns the path once a probe start has succeeded. A start that fails for any reason but <i>Text file busy</i> is thrown; one that stays busy for five seconds is too.</summary>
    public static string Write(string path, string script)
    {
        const string Shebang = "#!/bin/sh\n";
        if (!script.StartsWith(Shebang, StringComparison.Ordinal))
        {
            throw new ArgumentException("a shim starts with #!/bin/sh and a newline", nameof(script));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Shebang + $"[ -n \"${ProbeVariable}\" ] && exit 0\n" + script[Shebang.Length..]);
        File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("755", 8));
        Settle(path);
        return path;
    }

    private static void Settle(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Probe(path);
                return;
            }
            catch (Win32Exception busy) when (busy.NativeErrorCode == TextFileBusy && attempt < Tries)
            {
                Thread.Sleep(Backoff);
            }
        }
    }

    private static void Probe(string path)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false };
        start.Environment[ProbeVariable] = "1";
        using var probe = Process.Start(start)!;
        probe.WaitForExit();
    }
}
