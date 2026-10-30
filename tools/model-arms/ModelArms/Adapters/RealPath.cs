using System.Runtime.InteropServices;
using Bench.Domain;
using Microsoft.Win32.SafeHandles;

namespace ModelArms.Adapters;

/// <summary>The real path of a file, asked of the kernel: the path it would open, every symbolic link and every <c>..</c> applied in the kernel's own
/// order. Never a managed re-implementation: .NET's own link resolution joins a link's text with its folder and collapses <c>..</c> as text, so
/// <c>home/c/sub/../bin/claude</c> with <c>sub</c> linked to <c>&lt;target&gt;/x</c> answers <c>home/c/bin/claude</c> where the kernel runs
/// <c>&lt;target&gt;/bin/claude</c> (measured on Linux, 2026-10-29). Linux, macOS and FreeBSD ask libc <c>realpath</c>; Windows opens a handle on the
/// path and asks <c>GetFinalPathNameByHandleW</c>, which also resolves <c>\\?\</c>, 8.3 names and junctions. Any other platform is refused: a platform
/// nobody probed does not inherit another's answer (common.platform-limits). The only decision taken on an answer is ok or failed; the failure
/// carries the operating system's own words.</summary>
internal static class RealPath
{
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const int FirstBufferLength = 1024;

    public static Outcome<string> Of(string path)
    {
        if (path.Contains('\0', StringComparison.Ordinal))
        {
            return Outcome<string>.Failure($"'{path}' holds a NUL character — refused (D17)");
        }

        try
        {
            return Ask(path);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Outcome<string>.Failure($"the real-path call of this platform is not available ({ex.Message}) — refused (D17)");
        }
    }

    private static Outcome<string> Ask(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return Windows(path);
        }

        return IsUnixLike()
            ? Unix(path)
            : Outcome<string>.Failure("no real-path resolver is probed for this platform — refused (D17)");
    }

    /// <summary>The platforms libc <c>realpath</c> was probed on; a platform not named here is refused, not given another's answer.</summary>
    private static bool IsUnixLike() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD();

    private static Outcome<string> Unix(string path)
    {
        var answer = Realpath(path, IntPtr.Zero);
        if (answer == IntPtr.Zero)
        {
            return Outcome<string>.Failure($"'{path}': {Marshal.GetLastPInvokeErrorMessage()}");
        }

        try
        {
            return Marshal.PtrToStringUTF8(answer) is { Length: > 0 } real
                ? Outcome<string>.Success(real)
                : Outcome<string>.Failure($"'{path}': the kernel answered an empty path");
        }
        finally
        {
            Free(answer);
        }
    }

    private static Outcome<string> Windows(string path)
    {
        using var handle = CreateFile(path, 0, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        return handle.IsInvalid
            ? Outcome<string>.Failure($"'{path}': {Marshal.GetLastPInvokeErrorMessage()}")
            : FinalPath(handle, path);
    }

    private static Outcome<string> FinalPath(SafeFileHandle handle, string path)
    {
        var buffer = new char[FirstBufferLength];
        var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0);
        if (length > buffer.Length)
        {
            buffer = new char[length];
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0);
        }

        return length == 0 || length > buffer.Length
            ? Outcome<string>.Failure($"'{path}': {Marshal.GetLastPInvokeErrorMessage()}")
            : Outcome<string>.Success(WithoutVerbatimPrefix(new string(buffer, 0, (int)length)));
    }

    /// <summary>The final path comes back verbatim (<c>\\?\C:\x</c>, <c>\\?\UNC\server\share</c>); the plain spelling is what the rest of the module compares.</summary>
    private static string WithoutVerbatimPrefix(string final) =>
        final switch
        {
            _ when final.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) => @"\\" + final[@"\\?\UNC\".Length..],
            _ when final.StartsWith(@"\\?\", StringComparison.Ordinal) => final[@"\\?\".Length..],
            _ => final,
        };

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr Realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, [Out] char[] buffer, uint length, uint flags);
}
