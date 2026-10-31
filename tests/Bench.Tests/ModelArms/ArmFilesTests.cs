using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
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
        (UnixFileMode Mode, string Text)? seen = null;

        ArmFiles.ReplacePrivate(path, "{\"key\":\"s3cret\"}\n", staged => seen = (File.GetUnixFileMode(staged), File.ReadAllText(staged)));

        seen.Should().NotBeNull("the seam runs once, after the staging file is written and before the rename");
        seen!.Value.Text.Should().Contain("s3cret", "the staging file already holds the secret at that moment — so its mode is what matters");
        seen.Value.Mode.Should().Be(Private, "a create-then-chmod would leave the default umask's 0644 here, with the key already written");
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

    /// <summary>The tasks of a batch are parallel <c>Task</c>s of one process, each with its own <see cref="ArmFiles"/> (E4.S3, §8 item 6): every timing line is one append, and eight writers' lines must each be whole. The lock around <c>AppendTiming</c> is process-wide, so eight instances share it; what the
    /// runtime does WITHOUT the lock is recorded in the module doc, not assumed.</summary>
    [Fact]
    public async Task Timing_lines_appended_from_many_threads_at_once_are_each_whole()
    {
        using var temp = new TempFolder();
        var paths = new ArmPaths(temp.Path, "00000000000000a1");
        const int Threads = 8;
        const int Lines = 200;
        var files = Enumerable.Range(0, Threads).Select(_ => new ArmFiles(paths)).ToList();
        using var start = new Barrier(Threads);

        await Task.WhenAll(files.Select((file, writer) => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait();
                for (var line = 0; line < Lines; line++)
                {
                    file.AppendTiming(new TimingLine.Stopped("SRS1", $"2-t{writer}-code", 5, new string('x', 200 + line), new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)));

        var text = File.ReadAllText(paths.Timing);
        var written = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        written.Should().HaveCount(Threads * Lines, "every append wrote its whole line and its newline, none was cut or joined to another");
        TimingLines.Read(text).Should().HaveCount(Threads * Lines).And.AllBeOfType<TimingLine.Stopped>("every line parses as the line it was written as");
        written.Select(line => line.Length).Should().OnlyContain(length => length > 200);
        TimingLines.Read(text).OfType<TimingLine.Stopped>().GroupBy(line => line.Step).Select(group => group.Count()).Should().HaveCount(Threads).And.OnlyContain(count => count == Lines, "each writer's lines are all there");
    }
}
