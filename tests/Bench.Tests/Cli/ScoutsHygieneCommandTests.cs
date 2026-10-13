using System.Text;
using Bench.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench scouts hygiene</c> (E2 S4, §3.13) against a real repository: a staged blob carrying a deny-list entry, a home path or an
/// e-mail is refused by rule (exit 1) and never by value; a staged file whose NAME carries an entry is refused with the name redacted; a clean
/// index passes; a commit range is scanned at its end; and the verb refuses rather than scans less — no deny-list named (4), a named one that
/// does not load (4), a directory that is not a repository (3).</summary>
public sealed class ScoutsHygieneCommandTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private string DenyList => Path.Combine(_root.Path, "deny.txt");

    public void Dispose()
    {
        _repo.Dispose();
        _root.Dispose();
    }

    [Fact]
    public async Task A_staged_blob_carrying_a_deny_list_entry_a_home_path_or_an_email_is_refused_by_rule_never_by_value()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        File.WriteAllText(DenyList, "contoso-internal\nFabrikam\n");
        await StageAsync(("src/clean.cs", "class Clean {}\n"), ("src/leaky.cs", "// mirrored from contoso-internal by ops@contoso.example at C:\\Users\\someone\\x\n"));

        var (code, output, error) = await RunAsync("--staged");

        code.Should().Be(ExitCodes.Regression, output + error);
        output.Should().Contain("leftover       src/leaky.cs: carries a home path").And.Contain("leftover       src/leaky.cs: carries an e-mail address").And.Contain("leftover       src/leaky.cs: carries deny-list entry #1");
        output.Should().Contain("2 file(s) scanned whole from git, 3 leftover(s)");
        output.Should().Contain("scanning       [1/2] src/clean.cs").And.Contain("scanning       [2/2] src/leaky.cs", "each blob is announced with its place as it is read off the one batch");
        (output + error).Should().NotContain("contoso").And.NotContain("someone").And.NotContain("@");
        error.Should().Contain("3 leftover(s)");
    }

    /// <summary>Review finding (security, E2 code round): a BINARY blob — a NUL in its first 8 KB — was scanned by name only, so a deny-list entry
    /// inside a .pdb, a zip, a compiled resource or a UTF-16 text file passed. Its bytes are scanned for every entry as UTF-8 and as UTF-16LE,
    /// ASCII letters case-insensitively; the hit is named by index and encoding, never by value.</summary>
    [Fact]
    public async Task A_staged_binary_blob_carrying_a_deny_list_entry_in_its_bytes_is_refused_as_utf8_or_utf16_whatever_the_case()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        File.WriteAllText(DenyList, "contoso-internal\nFabrikam\n");
        await StageBytesAsync(
            ("assets/narrow.bin", [0x00, 0x01, 0xFF, .. Encoding.UTF8.GetBytes("--CONTOSO-Internal--"), 0x00]),
            ("assets/wide.bin", [0x4D, 0x5A, 0x00, 0x00, .. Encoding.Unicode.GetBytes("x fabrikam y"), 0x00]),
            ("assets/clean.bin", [0x00, 0x7F, 0x80, 0xFE, 0x41, 0x00]));

        var (code, output, error) = await RunAsync("--staged");

        code.Should().Be(ExitCodes.Regression, output + error);
        output.Should().Contain("leftover       assets/narrow.bin: carries deny-list entry #1 in its bytes (UTF-8)")
            .And.Contain("leftover       assets/wide.bin: carries deny-list entry #2 in its bytes (UTF-16LE)")
            .And.Contain("3 file(s) scanned whole from git, 2 leftover(s)");
        (output + error).Should().NotContainEquivalentOf("contoso").And.NotContainEquivalentOf("fabrikam");
    }

    /// <summary>The batch read lists the index as <c>diff --raw</c>, where a rename carries two paths: the blob is read under — and reported
    /// under — its NEW name, as the name-only listing did before.</summary>
    [Fact]
    public async Task A_staged_rename_is_scanned_under_its_new_name()
    {
        await _repo.InitAsync(("docs/notes.md", "# notes about contoso-internal\n"), ("init", "2026-05-01T10:00:00+00:00"));
        File.WriteAllText(DenyList, "contoso-internal\n");
        await _repo.GitAsync("mv", "docs/notes.md", "docs/moved.md");

        var (code, output, error) = await RunAsync("--staged");

        code.Should().Be(ExitCodes.Regression, output + error);
        output.Should().Contain("leftover       docs/moved.md: carries deny-list entry #1").And.Contain("1 file(s) scanned whole from git, 1 leftover(s)");
    }

    [Fact]
    public async Task A_staged_file_whose_name_carries_a_deny_list_entry_is_refused_with_the_name_redacted_and_a_clean_index_passes()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        File.WriteAllText(DenyList, "contoso-internal\nFabrikam\n");
        await StageAsync(("docs/fabrikam-notes.md", "# notes\n"));

        var named = await RunAsync("--staged");

        named.Code.Should().Be(ExitCodes.Regression, named.Output + named.Error);
        named.Output.Should().Contain("leftover       docs/<private>-notes.md: the file name carries deny-list entry #2").And.NotContainEquivalentOf("fabrikam");

        await _repo.GitAsync("reset", "-q");
        File.Delete(Path.Combine(_repo.Root, "docs", "fabrikam-notes.md"));
        await StageAsync(("src/clean.cs", "class Clean {}\n"));
        var clean = await RunAsync("--staged");

        clean.Code.Should().Be(ExitCodes.Pass, clean.Output + clean.Error);
        clean.Output.Should().Contain("1 file(s) scanned whole from git, 0 leftover(s)");
    }

    [Fact]
    public async Task A_commit_range_is_scanned_at_its_end_and_the_working_tree_is_never_read()
    {
        var first = await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var second = await _repo.CommitAsync(("src/leaky.cs", "// contoso-internal\n"), ("leak", "2026-05-02T10:00:00+00:00"));
        File.WriteAllText(DenyList, "contoso-internal\n");
        File.WriteAllText(Path.Combine(_repo.Root, "src", "leaky.cs"), "// cleaned in the working tree only\n");

        var (code, output, error) = await RunAsync("--range", $"{first}..{second}");

        code.Should().Be(ExitCodes.Regression, output + error);
        output.Should().Contain("leftover       src/leaky.cs: carries deny-list entry #1", "the blob at the range's end is what a push carries, not the working tree");
    }

    [Fact]
    public async Task The_scan_refuses_rather_than_scans_less()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await StageAsync(("src/clean.cs", "class Clean {}\n"));

        Assert.SkipWhen(Environment.GetEnvironmentVariable(ScoutsInputs.DenyListVariable) is { Length: > 0 }, "this machine names a deny-list in the environment");
        var unnamed = await Run(["scouts", "hygiene", "--staged", "--repo", _repo.Root]);
        unnamed.Code.Should().Be(ExitCodes.Configuration, unnamed.Error);
        unnamed.Error.Should().Contain("needs the deny-list");

        var missing = await Run(["scouts", "hygiene", "--staged", "--repo", _repo.Root, "--deny-list", Path.Combine(_root.Path, "no-such.txt")]);
        missing.Code.Should().Be(ExitCodes.Configuration, missing.Error);
        missing.Error.Should().Contain("does not load");

        File.WriteAllText(DenyList, "contoso-internal\n");
        var notARepo = await Run(["scouts", "hygiene", "--staged", "--repo", _root.Path, "--deny-list", DenyList]);
        notARepo.Code.Should().Be(ExitCodes.Environment, notARepo.Error);
        notARepo.Error.Should().Contain("cannot scan");

        var twoModes = await Run(["scouts", "hygiene", "--staged", "--tree", "HEAD", "--repo", _repo.Root, "--deny-list", DenyList]);
        twoModes.Code.Should().Be(ExitCodes.Configuration, twoModes.Error);
    }

    private async Task StageAsync(params (string Path, string Content)[] files)
    {
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(_repo.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content, Ct);
        }

        await _repo.GitAsync("add", "-A");
    }

    private async Task StageBytesAsync(params (string Path, byte[] Content)[] files)
    {
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(_repo.Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllBytesAsync(full, content, Ct);
        }

        await _repo.GitAsync("add", "-A");
    }

    private Task<(int Code, string Output, string Error)> RunAsync(params string[] mode) =>
        Run(["scouts", "hygiene", .. mode, "--repo", _repo.Root, "--deny-list", DenyList]);

    private static async Task<(int Code, string Output, string Error)> Run(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ScoutsCommand.RunAsync(CommandLine.Parse(args), ScoutVerbServices.Machine, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }
}
