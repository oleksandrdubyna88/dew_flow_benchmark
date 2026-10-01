using Bench.Domain.Probes;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 acceptance 3 — the fixtures: a fresh directory and fresh tokens per attempt (two attempts of one cell share
/// nothing), deleted afterwards, and the cleanup of finding 3 — <c>DeleteStranded</c> removes a Pending cell's leftover and a
/// dead owner's, keeps a live owner's on this host, and never touches another run's root or anything above the run's.</summary>
public sealed class ProbeFixturesTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private ProbeFixtures Fixtures => new(_root.Path);

    [Fact]
    public void Two_attempts_of_one_cell_share_neither_a_directory_nor_a_token_and_each_has_the_files_its_probe_wants()
    {
        var (run, cell) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var first = Fixtures.Begin(ProbeKind.ReadOutsideBare, new ProbeAttemptScope(run, cell, 1, 1)).Ok();
        var second = Fixtures.Begin(ProbeKind.ReadOutsideBare, new ProbeAttemptScope(run, cell, 1, 2)).Ok();

        first.Root.Should().NotBe(second.Root);
        first.Root.Should().EndWith(Path.Combine("g1", "a1"));
        second.Root.Should().EndWith(Path.Combine("g1", "a2"));
        first.Tokens.Should().NotBe(second.Tokens, "a token that repeats can be remembered (D7)");
        first.Tokens.Inside.Should().StartWith("IN-").And.HaveLength(15);
        first.Tokens.Outside.Should().StartWith("OUT-").And.HaveLength(16);
        File.ReadAllText(first.InsideFile).Trim().Should().Be(first.Tokens.Inside);
        File.ReadAllText(first.CanaryFile).Trim().Should().Be(first.Tokens.Outside);
        first.Cwd.Should().Be(Path.Combine(first.Root, "cwd"));
        first.CanaryFile.Should().Be(Path.Combine(first.Root, "outside", "canary.txt"), "outside the working directory, beside it");
        Fixtures.Begin(ProbeKind.ReadOutsideBare, new ProbeAttemptScope(run, cell, 1, 1)).Reason().Should().Contain("already exists", "a leftover is never continued");

        var web = Fixtures.Begin(ProbeKind.WebSearch, new ProbeAttemptScope(run, Guid.CreateVersion7(), 1, 1)).Ok();
        Directory.EnumerateFileSystemEntries(web.Cwd).Should().BeEmpty("the web search runs in an EMPTY cwd");
        File.Exists(web.CanaryFile).Should().BeFalse("and has no canary anywhere");

        var confined = Fixtures.Begin(ProbeKind.WebConfined, new ProbeAttemptScope(run, Guid.CreateVersion7(), 1, 1)).Ok();
        Directory.EnumerateFileSystemEntries(confined.Cwd).Should().BeEmpty();
        File.Exists(confined.CanaryFile).Should().BeTrue("the confined row asks for the canary outside");
    }

    [Fact]
    public void Delete_removes_the_attempts_root_and_nothing_else_and_tolerates_a_root_already_gone()
    {
        var run = Guid.CreateVersion7();
        var one = Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, Guid.CreateVersion7(), 1, 1)).Ok();
        var two = Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, Guid.CreateVersion7(), 1, 1)).Ok();

        Fixtures.Delete(one);
        Fixtures.Delete(one);

        Directory.Exists(one.Root).Should().BeFalse();
        Directory.Exists(two.Root).Should().BeTrue("another attempt's fixture is not this one's to delete");
    }

    [Fact]
    public void DeleteStranded_removes_every_cell_folder_not_claimed_by_a_live_owner_and_touches_nothing_outside_the_runs_root()
    {
        var (run, otherRun) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var (pending, dead, live) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, pending, 1, 1)).Ok();
        Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, dead, 1, 2)).Ok();
        var kept = Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, live, 1, 1)).Ok();
        var elsewhere = Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(otherRun, Guid.CreateVersion7(), 1, 1)).Ok();
        var runRoot = Path.Combine(_root.Path, "probes", run.ToString("D"));
        var above = Path.Combine(_root.Path, "probes", "notes.txt");
        var stray = Path.Combine(runRoot, "not-a-cell");
        File.WriteAllText(above, "a file above the run's root");
        Directory.CreateDirectory(stray);

        var deleted = Fixtures.DeleteStranded(run, new HashSet<Guid> { live });

        deleted.Should().Be(3, "the Pending cell's leftover, the dead owner's, and a folder that names no cell");
        Directory.Exists(Path.Combine(runRoot, pending.ToString("D"))).Should().BeFalse("a Pending cell's leftover is a kill's — gone (finding 3)");
        Directory.Exists(Path.Combine(runRoot, dead.ToString("D"))).Should().BeFalse();
        Directory.Exists(stray).Should().BeFalse();
        Directory.Exists(kept.Root).Should().BeTrue("a live owner on this host is still measuring it");
        Directory.Exists(elsewhere.Root).Should().BeTrue("another run's root is not this run's");
        File.Exists(above).Should().BeTrue("nothing above the run's root is touched");
        Fixtures.DeleteStranded(Guid.CreateVersion7(), new HashSet<Guid>()).Should().Be(0, "a run with no root deletes nothing");
    }

    [Fact]
    public void DeleteStranded_removes_a_link_as_itself_and_never_follows_it()
    {
        var run = Guid.CreateVersion7();
        Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run, Guid.CreateVersion7(), 1, 1)).Ok();
        var runRoot = Path.Combine(_root.Path, "probes", run.ToString("D"));
        var target = _root.Sibling("outside");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "precious.txt"), "evidence that must survive");
        var link = Path.Combine(runRoot, Guid.CreateVersion7().ToString("D"));
        DirectoryLink.CreateOrSkip(link, target);

        Fixtures.DeleteStranded(run, new HashSet<Guid>()).Should().Be(2);

        Directory.Exists(link).Should().BeFalse("the link itself is a stranded folder");
        File.Exists(Path.Combine(target, "precious.txt")).Should().BeTrue("its target was never entered");
    }

    public void Dispose() => _root.Dispose();
}
