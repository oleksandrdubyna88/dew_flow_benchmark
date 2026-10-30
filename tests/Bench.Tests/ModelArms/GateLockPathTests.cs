using System.Runtime.InteropServices;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What the gate lock will open (D17): <c>.gate.lock</c> must be a regular file of the root's own, or absent. The lock opened its path with <c>OpenOrCreate</c>, which follows a link to any kind of file, so a link planted
/// in the root made <c>status</c> create a file OUTSIDE the root, made <c>hold</c> lock one that is not the root's, and a FIFO or <c>/dev/null</c> was accepted as the lock; a folder ended as an unexpected fault. The check sits on the
/// one road in — <see cref="FileGateLock"/> itself — so no caller of the port can skip it: the path is read WITHOUT following a link (the module's <c>statx</c> reader), a link or a file of another kind is refused with a sentence naming
/// the path, what it is and D17, a missing file is made exclusively (<c>O_CREAT | O_EXCL</c>), and after the open the file the handle holds is compared with the file the path names (device and inode).
/// <para>
/// What this does NOT prove: a swap AFTER the second comparison (the header of the lock says so), a link in a PARENT folder of the root (D17 judges the root), or Windows (the POSIX facts do not exist there; the gate declares the
/// check not performed). The tests plant their files with the OS's own calls: links, <c>mkfifo</c>.
/// </para></summary>
public sealed class GateLockPathTests
{
    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static string Outside(ExperimentRoot root, string name) => Path.Combine(Directory.CreateDirectory(Path.Combine(root.Temp, "outside")).FullName, name);

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "links, FIFOs and statx are POSIX");

    /// <summary>Plants what the shape names at the lock's path; returns what the refusal must call it and the outside file the shape could reach (or empty).</summary>
    private static (string Kind, string Reached) Plant(ExperimentRoot root, string shape)
    {
        var path = GateLockTestKit.LockOf(root);
        switch (shape)
        {
            case "a link to a file outside":
                var real = Outside(root, "real.txt");
                File.WriteAllText(real, "keep");
                File.CreateSymbolicLink(path, real);
                return ("a symbolic link", real);
            case "a dangling link":
                var never = Outside(root, "never-made");
                File.CreateSymbolicLink(path, never);
                return ("a symbolic link", never);
            case "a link to a folder":
                File.CreateSymbolicLink(path, Directory.CreateDirectory(Outside(root, "folder")).FullName);
                return ("a symbolic link", string.Empty);
            case "a folder":
                Directory.CreateDirectory(path);
                return ("a directory", string.Empty);
            case "a FIFO":
                mkfifo(path, Convert.ToUInt32("600", 8)).Should().Be(0, "the fixture makes a FIFO");
                return ("a FIFO", string.Empty);
            default:
                File.CreateSymbolicLink(path, "/dev/null");
                return ("a symbolic link", "/dev/null");
        }
    }

    [Theory]
    [InlineData("a link to a file outside")]
    [InlineData("a dangling link")]
    [InlineData("a link to a folder")]
    [InlineData("a folder")]
    [InlineData("a FIFO")]
    [InlineData("a link to /dev/null")]
    public async Task A_lock_path_that_is_not_a_regular_file_is_refused_by_status_and_by_hold_and_nothing_outside_is_touched(string shape)
    {
        Posix();
        using var root = new ExperimentRoot();
        var (kind, reached) = Plant(root, shape);
        var path = GateLockTestKit.LockOf(root);
        var before = reached.Length > 0 && File.Exists(reached) ? File.ReadAllText(reached) : null;

        foreach (var line in new[] { new[] { "gate-lock", "status" }, ["gate-lock", "hold", "--seconds", "1"] })
        {
            var (code, output, error) = await GateLockTestKit.RunAsync(root, line);

            code.Should().Be(4, $"{string.Join(' ', line)} refuses {shape}");
            error.Should().Contain(path).And.Contain(kind).And.Contain("D17", "the sentence names the path, what it is and the decision");
            output.Should().BeEmpty("nothing was taken");
        }

        if (reached is { Length: > 0 } && reached != "/dev/null")
        {
            (File.Exists(reached) ? File.ReadAllText(reached) : null).Should().Be(before, "a file the link points at is neither created nor changed");
        }

        GateLockTestKit.RootFiles(root).Should().NotContain(".gate.lock.holder", "no holder was recorded for a lock that was never taken");
    }

    [Fact]
    public async Task Status_on_a_root_with_no_lock_file_says_free_and_creates_nothing_while_hold_makes_one_regular_file()
    {
        Posix();
        using var root = new ExperimentRoot();
        var path = GateLockTestKit.LockOf(root);

        var (code, output, _) = await GateLockTestKit.RunAsync(root, "gate-lock", "status");

        (code, output).Should().Be((0, "model-arms gate-lock: free"));
        File.Exists(path).Should().BeFalse("status looks and makes nothing");

        var held = GateLockTestKit.Lock().TryAcquire(path);
        held.Should().NotBeNull();
        held!.Dispose();
        new FileInfo(path).LinkTarget.Should().BeNull("the created file is the file itself");
        new FileInfo(path).Length.Should().Be(0, "an empty file, one per root");
        File.GetAttributes(path).Should().NotHaveFlag(FileAttributes.Directory);
    }

    [Fact]
    public void A_link_planted_between_the_check_and_the_open_is_caught_by_the_comparison_after_the_open()
    {
        Posix();
        using var root = new ExperimentRoot();
        var path = GateLockTestKit.LockOf(root);
        File.WriteAllText(path, string.Empty);
        var outside = Outside(root, "other.txt");
        File.WriteAllText(outside, "not the root's");
        var gate = new FileGateLock(TimeProvider.System, GateLockTestKit.Poll)
        {
            AfterJudged = judged =>
            {
                File.Delete(judged);
                File.CreateSymbolicLink(judged, outside);
            },
        };

        var answer = gate.Take(path, create: true, note: false);

        answer.Should().BeOfType<GateLockAnswer.Refused>("the path was a regular file when it was checked and a link when it was opened")
            .Which.Reason.Should().Contain(path).And.Contain("D17");
        File.ReadAllText(outside).Should().Be("not the root's");
    }

    [Fact]
    public void A_different_file_swapped_in_after_the_open_is_caught_by_device_and_inode()
    {
        Posix();
        using var root = new ExperimentRoot();
        var path = GateLockTestKit.LockOf(root);
        File.WriteAllText(path, string.Empty);
        var gate = new FileGateLock(TimeProvider.System, GateLockTestKit.Poll)
        {
            AfterOpened = opened =>
            {
                var other = opened + ".other";
                File.WriteAllText(other, string.Empty);
                File.Move(other, opened, overwrite: true);
            },
        };

        var answer = gate.Take(path, create: true, note: false);

        answer.Should().BeOfType<GateLockAnswer.Refused>("the handle holds one regular file and the path now names another: they are not the same inode")
            .Which.Reason.Should().Contain(path).And.Contain("D17");
    }

    [Fact]
    public void A_link_planted_where_the_file_was_absent_is_not_followed_by_the_exclusive_create()
    {
        Posix();
        using var root = new ExperimentRoot();
        var path = GateLockTestKit.LockOf(root);
        var outside = Outside(root, "other.txt");
        var gate = new FileGateLock(TimeProvider.System, GateLockTestKit.Poll)
        {
            AfterJudged = judged => File.CreateSymbolicLink(judged, outside),
        };

        var answer = gate.Take(path, create: true, note: false);

        answer.Should().NotBeOfType<GateLockAnswer.Held>("the name was free when checked and a LINK when the exclusive create ran: that create must not follow it");
        File.Exists(outside).Should().BeFalse("nothing was created through the link");
    }
}
