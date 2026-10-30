using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>ArmFiles.ReplacePrivate</c> (E3.S3, C15, E2 §8 item 14): a file that carries a secret — <c>mcp-coai.json</c> with the vault key, the copied settings, the arm map — is created at mode <c>0600</c> FROM its creation, never widened and narrowed after. A create-then-chmod leaves a
/// window in which the file is readable by other users with the key already in it; the seam between the staged write and the rename is where the test looks.
/// <para>
/// What this does NOT prove: a umask that widens (it cannot — a requested mode is only ever narrowed by it), a file system without modes (FAT, some mounts: the mode asked for is then not what the file has, and nothing here can see that), or Windows (POSIX modes; skipped with its reason).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ArmFilesTests
{
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [Fact]
    public void A_private_file_is_0600_from_its_creation_not_after_it()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "modes are POSIX");
        using var temp = new TempFolder();
        var path = temp.Combine("keys", "mcp-coai.json");
        var seen = new List<(UnixFileMode Mode, string Text)>();

        ArmFiles.ReplacePrivate(path, "{\"key\":\"s3cret\"}\n", staged => seen.Add((File.GetUnixFileMode(staged), File.ReadAllText(staged))));

        seen.Should().ContainSingle("the seam runs once, after the staging file is written and before the rename");
        seen[0].Text.Should().Contain("s3cret", "the staging file already holds the secret at that moment — so its mode is what matters");
        seen[0].Mode.Should().Be(Private, "a create-then-chmod would leave the default umask's 0644 here, with the key already written");
        File.GetUnixFileMode(path).Should().Be(Private);
        File.ReadAllText(path).Should().Be("{\"key\":\"s3cret\"}\n");

        File.WriteAllText(path, "old");
        File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("644", 8));
        ArmFiles.ReplacePrivate(path, "new", null);
        File.GetUnixFileMode(path).Should().Be(Private, "a file that existed wider is replaced by a private one — never widened, never kept");
        File.ReadAllText(path).Should().Be("new");
        Directory.EnumerateFileSystemEntries(temp.Combine("keys")).Should().ContainSingle("no staging file is left beside it");

        var blocked = temp.Folder("keys", "blocked");
        temp.File("x", "keys", "blocked", "inside");
        var failing = () => ArmFiles.ReplacePrivate(blocked, "the-secret-bytes", null);
        var thrown = failing.Should().Throw<Exception>("a rename onto a folder fails").Which;
        thrown.Message.Should().NotContain("the-secret-bytes", "a failure never carries the bytes it was writing");
        Directory.EnumerateFileSystemEntries(temp.Combine("keys")).Select(Path.GetFileName).Should().OnlyContain(name => !name!.Contains(".staging-", StringComparison.Ordinal), "a failed rename does not strand a private file holding the secret");
    }
}
