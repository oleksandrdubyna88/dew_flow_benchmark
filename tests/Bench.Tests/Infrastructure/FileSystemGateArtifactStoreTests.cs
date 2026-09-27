using System.Text;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The artefact root's three guarantees on a real filesystem: CONTAINMENT (a write lands under its own
/// attempt's roots, resolved with links followed, or nowhere), COMMIT (staged, flushed, hashed, renamed — a crash
/// at any step leaves nothing under the real name that is not whole), and FRESH ATTEMPTS (a later attempt gets a
/// new directory; the earlier one is kept and marked).</summary>
public sealed class FileSystemGateArtifactStoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"verdict\":\"revise\"}");

    [Fact]
    public void A_root_inside_a_git_checkout_is_refused()
    {
        using var temp = NewRoot();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));
        var root = Path.Combine(temp.Path, "nested", "artifacts");

        FileSystemGateArtifactStore.Open(root, TimeProvider.System, ArtifactProbe.None).Reason()
            .Should().Contain("inside the git checkout").And.Contain("git add");
        Directory.Exists(root).Should().BeFalse("a refused root is not created");
    }

    [Fact]
    public void A_root_inside_a_worktree_whose_git_is_a_file_is_refused_too()
    {
        using var temp = NewRoot();
        File.WriteAllText(Path.Combine(temp.Path, ".git"), "gitdir: /somewhere/.git/worktrees/x\n");

        FileSystemGateArtifactStore.Open(Path.Combine(temp.Path, "a"), TimeProvider.System, ArtifactProbe.None).Reason()
            .Should().Contain("inside the git checkout");
    }

    [Fact]
    public async Task A_written_artefact_reads_back_hashing_to_what_was_written_and_a_changed_one_is_refused()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        var path = CellPaths.AttemptRoot(scope).Then("reply.json").Ok();

        var written = (await store.WriteAsync(scope, ArtifactClass.Reply, path, Body, Ct)).Ok();

        written.Length.Should().Be(Body.Length);
        written.Sha256.Should().Be(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Body)));
        (await store.ReadAsync(written, Ct)).Ok().ToArray().Should().Equal(Body);

        await File.AppendAllTextAsync(Path.Combine([store.Root, .. path.Segments]), " ", Ct);
        (await store.ReadAsync(written, Ct)).Reason().Should().Contain("no longer matches its ref");
    }

    [Fact]
    public async Task An_artefact_is_committed_once_and_never_overwritten()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        var path = CellPaths.AttemptRoot(scope).Then("reply.json").Ok();
        await store.WriteAsync(scope, ArtifactClass.Reply, path, Body, Ct);

        (await store.WriteAsync(scope, ArtifactClass.Reply, path, Encoding.UTF8.GetBytes("other"), Ct)).Reason()
            .Should().Contain("already exists");
    }

    [Theory]
    [InlineData("runs/../../outside.txt")]
    [InlineData("runs/./x")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Users\\x\\file")]
    [InlineData("C:file")]
    [InlineData("runs\\x")]
    [InlineData("https://host/x")]
    [InlineData("runs//x")]
    [InlineData("..")]
    [InlineData("")]
    public void A_path_that_could_climb_or_is_not_relative_is_refused_before_anything_touches_the_disk(string path)
    {
        ArtifactPath.Parse(path).Reason().Should().Contain("artefact path");
    }

    [Fact]
    public async Task A_write_into_another_cell_or_another_attempt_is_refused()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        var otherCell = CellPaths.CellRoot(scope.Run.Id, Guid.CreateVersion7()).Then("attempt-1/reply.json").Ok();
        var otherAttempt = CellPaths.CellRoot(scope.Run.Id, scope.CellId).Then("attempt-10/reply.json").Ok();

        (await store.WriteAsync(scope, ArtifactClass.Reply, otherCell, Body, Ct)).Reason().Should().Contain("outside cell");
        (await store.WriteAsync(scope, ArtifactClass.Reply, otherAttempt, Body, Ct)).Reason()
            .Should().Contain("outside cell", "attempt-10 is not under attempt-1 — the check is by segment, never by string prefix");
        Directory.Exists(Path.Combine(store.Root, "runs", scope.Run.Id.ToString(), "cells", scope.CellId.ToString(), "attempt-10")).Should().BeFalse();
    }

    [Fact]
    public async Task An_isolated_cell_cannot_reach_the_shared_data_directory()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store, DataDirMode.Isolated);
        var shared = CellPaths.RunRoot(scope.Run.Id).Then($"{CellPaths.SharedDataFolder}/coai.db").Ok();

        (await store.WriteAsync(scope, ArtifactClass.Other, shared, Body, Ct)).Reason().Should().Contain("outside cell");
        File.Exists(Path.Combine([store.Root, .. shared.Segments])).Should().BeFalse("a refused write writes nothing — the refusal comes before the disk");
        (await store.WriteAsync(scope, ArtifactClass.Ledger, CellPaths.DataDirFor(scope).Then("usage.jsonl").Ok(), Body, Ct)).Ok();
    }

    [Fact]
    public async Task A_shared_run_cannot_reach_a_cells_private_data_directory()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store, DataDirMode.Shared);
        var privateData = CellPaths.AttemptRoot(scope).Then($"{CellPaths.DataFolder}/usage.jsonl").Ok();

        (await store.WriteAsync(scope, ArtifactClass.Ledger, privateData, Body, Ct)).Reason().Should().Contain("outside cell");
        File.Exists(Path.Combine([store.Root, .. privateData.Segments])).Should().BeFalse("a refused write writes nothing");
        CellPaths.DataDirFor(scope).Value.Should().EndWith(CellPaths.SharedDataFolder);
        (await store.WriteAsync(scope, ArtifactClass.Ledger, CellPaths.DataDirFor(scope).Then("usage.jsonl").Ok(), Body, Ct)).Ok();
        (await store.WriteAsync(scope, ArtifactClass.Reply, CellPaths.AttemptRoot(scope).Then("reply.json").Ok(), Body, Ct)).Ok();
    }

    [Fact]
    public async Task An_attempt_directory_that_already_exists_is_refused_rather_than_reused()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);

        (await store.BeginAttemptAsync(scope, Ct)).Reason().Should().Contain("already exists").And.Contain("attempt-2");
    }

    [Fact]
    public async Task A_later_attempt_gets_a_new_directory_and_the_interrupted_one_is_kept_and_marked()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var first = await BegunAsync(store);
        var reply = CellPaths.AttemptRoot(first).Then("reply.json").Ok();
        await store.WriteAsync(first, ArtifactClass.Reply, reply, Body, Ct);

        var second = first with { Attempt = 2 };
        (await store.BeginAttemptAsync(second, Ct)).Ok().Value.Should().EndWith("attempt-2");

        var attempts = await store.AttemptsAsync(first.Run.Id, first.CellId, Ct);
        attempts.Select(a => (a.Attempt, a.State)).Should().Equal((1, AttemptState.Interrupted), (2, AttemptState.Open));
        File.Exists(Path.Combine([store.Root, .. reply.Segments])).Should().BeTrue("the interrupted attempt's artefacts are kept — never continued, never deleted");
        CellPaths.DataDirFor(second).Should().NotBe(CellPaths.DataDirFor(first), "a fresh attempt gets a fresh data directory");
    }

    [Theory]
    [InlineData(ArtifactStep.Staged)]
    [InlineData(ArtifactStep.Flushed)]
    [InlineData(ArtifactStep.Hashed)]
    public async Task A_crash_before_the_rename_leaves_nothing_under_the_real_name(ArtifactStep step)
    {
        using var temp = NewRoot();
        var scope = await BegunAsync(Store(temp));
        var crashing = Store(temp, new CrashAt(step));
        var path = CellPaths.AttemptRoot(scope).Then("reply.json").Ok();

        var crash = async () => await crashing.WriteAsync(scope, ArtifactClass.Reply, path, Body, Ct);

        await crash.Should().ThrowAsync<SimulatedCrash>();
        var directory = Path.Combine([crashing.Root, .. CellPaths.AttemptRoot(scope).Segments]);
        File.Exists(Path.Combine(directory, "reply.json")).Should().BeFalse($"a crash at {step} must not leave half an artefact under its real name");
        Directory.EnumerateFiles(directory, "reply.json.staging-*").Should().ContainSingle("the staging name says what it is; nothing reads it as an artefact");
    }

    [Fact]
    public async Task A_crash_after_the_rename_leaves_a_whole_file_and_the_next_attempt_starts_fresh()
    {
        using var temp = NewRoot();
        var scope = await BegunAsync(Store(temp));
        var crashing = Store(temp, new CrashAt(ArtifactStep.Renamed));
        var path = CellPaths.AttemptRoot(scope).Then(CellPaths.RunRecordFile).Ok();

        await ((Func<Task>)(async () => await crashing.WriteAsync(scope, ArtifactClass.RunRecord, path, Body, Ct))).Should().ThrowAsync<SimulatedCrash>();

        var healthy = Store(temp);
        File.ReadAllBytes(Path.Combine([healthy.Root, .. path.Segments])).Should().Equal(Body, "renamed means whole");
        (await healthy.BeginAttemptAsync(scope with { Attempt = 2 }, Ct)).Ok();
        (await healthy.AttemptsAsync(scope.Run.Id, scope.CellId, Ct)).Select(a => a.State)
            .Should().Equal(AttemptState.Interrupted, AttemptState.Open);
    }

    [Fact]
    public async Task A_symbolic_link_or_junction_inside_an_attempt_cannot_carry_a_write_out_of_the_root()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        var outside = temp.Sibling("outside");
        Directory.CreateDirectory(outside);
        var link = Path.Combine([store.Root, .. CellPaths.AttemptRoot(scope).Segments, "tap"]);
        DirectoryLink.CreateOrSkip(link, outside);

        var written = await store.WriteAsync(scope, ArtifactClass.TapRequest, CellPaths.AttemptRoot(scope).Then("tap/call-01.request.json").Ok(), Body, Ct);

        written.Reason().Should().Contain("resolves outside");
        Directory.EnumerateFileSystemEntries(outside).Should().BeEmpty("nothing reached the directory the link points at");
    }

    [Fact]
    public async Task A_link_to_a_sibling_that_merely_shares_the_attempts_prefix_is_outside_it()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        var attempt = Path.Combine([store.Root, .. CellPaths.AttemptRoot(scope).Segments]);
        var sibling = attempt + "-evil";
        Directory.CreateDirectory(sibling);
        DirectoryLink.CreateOrSkip(Path.Combine(attempt, "tap"), sibling);

        (await store.WriteAsync(scope, ArtifactClass.TapRequest, CellPaths.AttemptRoot(scope).Then("tap/call-01.request.json").Ok(), Body, Ct))
            .Reason().Should().Contain("resolves outside", "attempt-1-evil starts with the characters of attempt-1 and is not under it");
        Directory.EnumerateFileSystemEntries(sibling).Should().BeEmpty();
    }

    [Theory]
    [InlineData("root-other", false)]
    [InlineData("root", true)]
    [InlineData("root/x", true)]
    [InlineData("rootx/y", false)]
    public void Containment_is_separator_aware(string candidate, bool within)
    {
        var root = Path.Combine(Path.GetTempPath(), "bench-containment", "root");
        var path = Path.Combine(Path.GetTempPath(), "bench-containment", candidate.Replace('/', Path.DirectorySeparatorChar));

        ArtifactContainment.IsWithin(path, root).Should().Be(within);
    }

    [Fact]
    public async Task A_link_that_swaps_one_cells_folder_for_anothers_is_refused()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var victim = await BegunAsync(store);
        var attacker = victim with { CellId = Guid.CreateVersion7() };
        var attackerCell = Path.Combine([store.Root, .. CellPaths.CellRoot(attacker.Run.Id, attacker.CellId).Segments]);
        var victimCell = Path.Combine([store.Root, .. CellPaths.CellRoot(victim.Run.Id, victim.CellId).Segments]);
        DirectoryLink.CreateOrSkip(attackerCell, victimCell);

        (await store.BeginAttemptAsync(attacker with { Attempt = 2 }, Ct)).Reason().Should().Contain("reached through a link");
        (await store.WriteAsync(attacker, ArtifactClass.Reply, CellPaths.AttemptRoot(attacker).Then("reply.json").Ok(), Body, Ct)).Reason()
            .Should().Contain("resolves outside", "a path that spells cell B and lands in cell A's folder is outside cell B's own root");
        File.Exists(Path.Combine(victimCell, "attempt-1", "reply.json")).Should().BeFalse();
    }

    [Fact]
    public async Task A_footprint_counts_the_bytes_the_files_and_the_tap_share_of_a_run()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = await BegunAsync(store);
        await store.WriteAsync(scope, ArtifactClass.Reply, CellPaths.AttemptRoot(scope).Then("reply.json").Ok(), new byte[100], Ct);
        await store.WriteAsync(scope, ArtifactClass.TapRequest, CellPaths.AttemptRoot(scope).Then("tap/call-01.request.json").Ok(), new byte[400], Ct);

        var footprint = await store.FootprintAsync(scope.Run.Id, Ct);

        footprint.Bytes.Value.Should().Be(500);
        footprint.Files.Value.Should().Be(2);
        footprint.TapBytes.Value.Should().Be(400);
        footprint.Describe.Should().Be("500 B in 2 file(s), 400 B of it tap bodies");
        (await store.RunsAsync(Ct)).Should().Equal(scope.Run.Id);
    }

    [Fact]
    public void A_footprint_that_could_not_be_measured_prints_unknown_never_zero()
    {
        ArtifactFootprint.Unknown("IOException while measuring the run's directory").Describe
            .Should().Be("unknown (IOException while measuring the run's directory)");
    }

    private static async Task<ArtifactScope> BegunAsync(FileSystemGateArtifactStore store, DataDirMode mode = DataDirMode.Isolated)
    {
        var scope = new ArtifactScope(Run(mode), Guid.CreateVersion7(), Attempt: 1);
        (await store.BeginAttemptAsync(scope, Ct)).Ok();
        return scope;
    }
}

/// <summary>Creates a directory link the way the operating system lets this user: a symbolic link where that is
/// permitted, and on Windows without the privilege a JUNCTION (which needs none) through <c>cmd /c mklink /J</c>,
/// launched as exe + argv. Only when neither is possible is the test skipped — the rules repository's precedent
/// for link tests on a machine that forbids them.</summary>
internal static class DirectoryLink
{
    public static void CreateOrSkip(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No symlink privilege — fall through to a junction on Windows.
        }

        Assert.SkipUnless(OperatingSystem.IsWindows() && Junction(link, target), "this OS forbids both symbolic links and junctions to this user");
    }

    private static bool Junction(string link, string target)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", link, target },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        process.WaitForExit(30_000);
        return process.HasExited && process.ExitCode == 0 && Directory.Exists(link);
    }
}
