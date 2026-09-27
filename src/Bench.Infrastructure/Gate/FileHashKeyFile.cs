using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Infrastructure.Gate;

/// <summary>The file-hash key on disk: <c>&lt;artifact-root&gt;/file-hash.key</c>, 32 random bytes, readable by
/// this user only.
/// <para>
/// Created with <see cref="FileMode.CreateNew"/>, so two workers racing on a fresh root cannot both write one, and
/// with its permissions set AT creation rather than after it, so there is no instant in which the key exists and
/// is readable by anyone else: mode <c>0600</c> on POSIX (<see cref="UnixFileMode"/> in the create options), and on
/// Windows a PROTECTED access list — inheritance cut — naming the current user alone. The bytes come from the
/// operating system's generator; nothing about them is derived from a path, a time or a machine. They are never
/// printed, never logged and never written anywhere but this file.
/// </para></summary>
public static class FileHashKeyFile
{
    public const string FileName = "file-hash.key";

    public const int KeyBytes = 32;

    public static FileHashKeyRead Read(string root)
    {
        var path = Path.Combine(root, FileName);

        if (!File.Exists(path))
        {
            return new FileHashKeyRead.Missing();
        }

        try
        {
            return FileHashKey.Of(File.ReadAllBytes(path)).Match<FileHashKeyRead>(
                key => new FileHashKeyRead.Present(key),
                reason => new FileHashKeyRead.Unusable($"{FileName}: {reason}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileHashKeyRead.Unusable($"{FileName} could not be read — {ex.GetType().Name}");
        }
    }

    public static Outcome<FileHashKey> Create(string root)
    {
        var path = Path.Combine(root, FileName);
        var bytes = RandomNumberGenerator.GetBytes(KeyBytes);

        try
        {
            using (var stream = OpenNew(path))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            return FileHashKey.Of(bytes);
        }
        catch (IOException) when (File.Exists(path))
        {
            return Outcome<FileHashKey>.Failure($"{FileName} already exists — a key is created once and never replaced");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static FileStream OpenNew(string path) =>
        OperatingSystem.IsWindows()
            ? OpenNewOnWindows(path)
            : new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });

    [SupportedOSPlatform("windows")]
    private static FileStream OpenNewOnWindows(string path) =>
        new FileInfo(path).Create(
            FileMode.CreateNew,
            FileSystemRights.Read | FileSystemRights.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough,
            OwnerOnly());

    /// <summary>A protected list — nothing inherited from the folder — with one rule: this user, full control.</summary>
    [SupportedOSPlatform("windows")]
    public static FileSecurity OwnerOnly()
    {
        var security = new FileSecurity();
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("the current Windows identity has no SID to grant the key file to");

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));

        return security;
    }
}
