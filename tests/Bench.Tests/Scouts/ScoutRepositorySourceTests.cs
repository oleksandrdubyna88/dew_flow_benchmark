using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>Plan D10 and §3.1 as amended: where a task's repository is read from, and what G may hold.
/// <list type="bullet">
/// <item>A repository value that already IS a repository url (https://, ssh://, git@, file://) is used as it is — a private repository is
/// mirrored read-only from its local clone (<c>file:////wsl.localhost/Ubuntu/home/&lt;user&gt;/git/&lt;repo&gt;</c>, verified 2026-10-12); a bare
/// <c>owner/name</c> slug still goes through the template; anything else is refused naming the value and the legal forms.</item>
/// <item>A submodule's GITLINK (mode 160000 at P) leaves G: it is not a tree file a scout can name.</item>
/// </list></summary>
public sealed class ScoutRepositorySourceTests : IDisposable
{
    private const string WslClone = "file:////wsl.localhost/Ubuntu/home/someone/git/private-repo";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private static ScoutTreeSettings GitHub => new("C:/trees", ScoutTreeSettings.GitHub, TimeSpan.FromMinutes(1));

    [Fact]
    public void A_slug_goes_through_the_template()
    {
        GitHub.UrlOf("dew-flow/dew_flow_benchmark").Ok().Value.Should().Be("https://github.com/dew-flow/dew_flow_benchmark.git");
    }

    [Theory]
    [InlineData(WslClone)]
    [InlineData("https://gitlab.example.org/team/project.git")]
    [InlineData("ssh://git@example.org/team/project.git")]
    [InlineData("git@example.org:team/project.git")]
    public void A_value_that_is_already_a_repository_url_is_used_as_it_is(string repository)
    {
        GitHub.UrlOf(repository).Ok().Value.Should().Be(repository);
    }

    [Theory]
    [InlineData("not a repository")]
    [InlineData("http://example.org/team/project.git")]
    [InlineData("team/project/extra")]
    [InlineData("")]
    public void A_value_that_is_neither_a_slug_nor_a_url_is_refused_by_name(string repository)
    {
        GitHub.UrlOf(repository).Reason().Should().Contain($"'{repository}'").And.Contain("owner/name").And.Contain("file://");
    }

    [Fact]
    public void A_task_on_a_private_repository_freezes_with_its_local_url_and_a_malformed_one_is_refused()
    {
        ScoutTask.Frozen(ScoutTaskId.Parse("p01").Ok(), WslClone, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Where is it decided?", ["src/A.cs", "src/B.cs", "src/C.cs"], [], [], new string('c', 64)).Ok()
            .Repository.Should().Be(WslClone);
        ScoutTask.Frozen(ScoutTaskId.Parse("p01").Ok(), "not a repository", CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Where is it decided?", ["src/A.cs"], [], [], new string('c', 64)).Reason().Should().Contain("not a repository");
    }

    [Fact]
    public void A_gitlink_in_Ps_tree_never_enters_G()
    {
        var tree = ScoutGroundTruth.TreeEntries(
            "100644 blob 1111111111111111111111111111111111111111\tsrc/A.cs\n"
            + "160000 commit 2222222222222222222222222222222222222222\tvendor/lib\n"
            + "100644 blob 3333333333333333333333333333333333333333\tsrc/B.cs\n");

        tree.Should().ContainSingle(e => e.IsGitlink).Which.Path.Should().Be("vendor/lib");
        ScoutGroundTruth.Of(["src/A.cs", "vendor/lib", "src/B.cs"], tree, string.Empty).Should().Equal("src/A.cs", "src/B.cs");
    }

    [Fact]
    public async Task A_submodule_bump_in_a_real_change_leaves_G_and_the_repository_is_read_from_its_file_url_as_it_is()
    {
        using var module = new DatedGitRepo(Ct);
        using var repo = new DatedGitRepo(Ct);
        var first = await module.InitAsync(("lib.txt", "one\n"), ("m1", "2026-05-01T10:00:00+00:00"));
        var second = await module.CommitAsync(("lib.txt", "two\n"), ("m2", "2026-05-02T10:00:00+00:00"));
        await repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await repo.GitAsync("-c", "protocol.file.allow=always", "submodule", "add", "-q", module.Url, "vendor/lib");
        await repo.GitAsync("-C", "vendor/lib", "checkout", "-q", first);
        var parent = await repo.CommitManyAsync([("src/A.cs", "class A {}\n"), ("src/B.cs", "class B {}\n"), ("src/C.cs", "class C {}\n")], ("p", "2026-06-01T10:00:00+00:00"));
        await repo.GitAsync("-C", "vendor/lib", "checkout", "-q", second);
        var fix = await repo.CommitManyAsync([("src/A.cs", "class A { int x; }\n"), ("src/B.cs", "class B { int x; }\n"), ("src/C.cs", "class C { int x; }\n")], ("s", "2026-06-02T10:00:00+00:00"));
        (await repo.GitAsync("diff", "--name-only", parent, fix)).Should().Contain("vendor/lib", "the change bumps the submodule — the fixture is the case it claims to be");

        var history = new ScoutHistory(
            new GitCheckoutProvider(CheckoutCacheOptions.Under(_root.Sibling("mirrors")), NullLogger<GitCheckoutProvider>.Instance),
            new ScoutTreeSettings(_root.Sibling("trees"), ScoutTreeSettings.GitHub, TimeSpan.FromMinutes(2)));
        var candidate = new ScoutCandidate(ScoutTaskId.Parse("p01").Ok(), repo.Url, CommitSha.Parse(parent).Ok(), CommitSha.Parse(fix).Ok(),
            ScoutQuestionKind.HowEndToEnd, "How does it work end to end?", string.Empty);

        var facts = (await history.FactsAsync(candidate, Ct)).Ok();

        facts.GroundTruth.Should().Equal("src/A.cs", "src/B.cs", "src/C.cs");
    }

    public void Dispose() => _root.Dispose();
}
