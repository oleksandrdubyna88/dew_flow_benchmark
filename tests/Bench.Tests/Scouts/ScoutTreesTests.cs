using Bench.Application.Scouts;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.2's history-free trees, built for real from a local repository's bare mirror: ONE commit and no other history, a hash that
/// is stable across builds and machines (the commit too — identity and date fixed by the environment), the PR's plan removed and the removal
/// recorded, a changed tree refused as a STOP, a submodule archived at its pinned sha — and the guards: the trees root (or the mirror root)
/// overlapping a directory the Claude user settings grant is refused naming both paths; a tree's own settings granting outside it are refused;
/// the trees root must be git-ignored inside a checkout, and nothing under it may be tracked (§3.13's hygiene).</summary>
public sealed class ScoutTreesTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    [Fact]
    public async Task A_tree_is_one_commit_with_no_other_history_and_its_hash_and_commit_are_the_same_on_every_build()
    {
        var parent = await HistoryAsync();
        var spec = new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), ["todo/PLAN_change.md"]);

        var first = (await Builder("a").BuildAsync(spec, Ct)).Ok();
        var second = (await Builder("b").BuildAsync(spec, Ct)).Ok();

        (await Git(first.Root, "rev-list", "--all", "--count")).Trim().Should().Be("1", "there is no later history to read");
        (await Git(first.Root, "log", "--format=%an %ad", "--date=iso")).Trim().Should().Be("bench scout tree 2000-01-01 00:00:00 +0000");
        first.TreeHash.Should().MatchRegex("^[0-9a-f]{64}$").And.Be(second.TreeHash, "the hash is over paths and bytes, nothing machine-local");
        (await Git(first.Root, "rev-parse", "HEAD")).Should().Be(await Git(second.Root, "rev-parse", "HEAD"), "the commit's identity and date are fixed");
        first.Files.Should().Equal("README.md", "src/Alpha.cs", "src/Beta.cs");
        File.ReadAllText(Path.Combine(first.Root, "src", "Alpha.cs")).Should().Be("class Alpha {}\n", "P's bytes, whatever this machine's core.autocrlf says");
        first.Removed.Should().Equal("todo/PLAN_change.md");
        File.Exists(Path.Combine(first.Root, "todo", "PLAN_change.md")).Should().BeFalse("the PR's own plan existed at P and is removed");
        Directory.Exists(Path.Combine(_root.Sibling("checkouts-a"), "worktrees")).Should().BeFalse("only the bare mirror is used — no worktree whose .git points at the future");
    }

    [Fact]
    public async Task A_tree_found_again_answers_the_same_hash_and_its_recorded_removals_and_a_changed_tree_stops_the_cell()
    {
        var parent = await HistoryAsync();
        var builder = Builder("a");
        var built = (await builder.BuildAsync(new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), ["todo/PLAN_change.md"]), Ct)).Ok();
        var task = ScoutTask.Frozen(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), CommitSha.Parse(parent).Ok(), ScoutQuestionKind.WhereDecided,
            "Where is it decided?", ["src/Alpha.cs", "src/Beta.cs", "README.md"], [], built.Removed, built.TreeHash).Ok();

        (await builder.PrepareAsync(task, Ct)).Ok().Should().Match<ScoutTree>(t => t.TreeHash == built.TreeHash && t.Removed.SequenceEqual(built.Removed));

        File.AppendAllText(Path.Combine(built.Root, "src", "Alpha.cs"), "// edited\n");
        (await builder.PrepareAsync(task, Ct)).Reason().Should().Contain(ScoutTreeRefusal.HashChanged);
    }

    [Fact]
    public async Task A_submodule_is_archived_into_its_path_at_the_sha_P_pins()
    {
        using var module = new DatedGitRepo(Ct);
        var pinned = await module.InitAsync(("lib.txt", "pinned content\n"), ("m1", "2026-05-01T10:00:00+00:00"));
        await module.CommitAsync(("lib.txt", "a LATER content P never saw\n"), ("m2", "2026-05-02T10:00:00+00:00"));
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await _repo.GitAsync("-c", "protocol.file.allow=always", "submodule", "add", "-q", module.Url, "vendor/lib");
        await _repo.GitAsync("-C", "vendor/lib", "checkout", "-q", pinned);
        await _repo.GitAsync("add", "-A");
        await _repo.GitAsync("commit", "-q", "-m", "pin the submodule");
        var parent = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        var tree = (await Builder("a").BuildAsync(new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), []), Ct)).Ok();

        File.ReadAllText(Path.Combine(tree.Root, "vendor", "lib", "lib.txt")).Should().Be("pinned content\n");
        tree.Files.Should().Contain("vendor/lib/lib.txt");
        Directory.Exists(Path.Combine(tree.Root, "vendor", "lib", ".git")).Should().BeFalse("the submodule's content, not its repository");
    }

    /// <summary>Found live 2026-10-14 (D10): a private repository read from the operator's local clone pins a submodule whose
    /// <c>.gitmodules</c> url is a private GitHub repository this machine cannot reach ("Repository not found"), which stopped the
    /// selection. The local clone already holds the submodule's objects under <c>.git/modules/&lt;path&gt;</c> — so for a LOCAL source the
    /// submodule is archived from there, and the url is only the fallback.</summary>
    [Fact]
    public async Task A_local_sources_submodule_is_archived_from_its_own_git_modules_when_the_url_cannot_be_reached()
    {
        using var module = new DatedGitRepo(Ct);
        var pinned = await module.InitAsync(("lib.txt", "pinned content\n"), ("m1", "2026-05-01T10:00:00+00:00"));
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await _repo.GitAsync("-c", "protocol.file.allow=always", "submodule", "add", "-q", module.Url, "vendor/lib");
        await _repo.GitAsync("config", "-f", ".gitmodules", "submodule.vendor/lib.url", "https://example.invalid/private/lib.git");
        await _repo.GitAsync("add", "-A");
        await _repo.GitAsync("commit", "-q", "-m", "pin the submodule behind an unreachable url");
        var parent = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        var tree = (await Builder("a").BuildAsync(new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), _repo.Url, CommitSha.Parse(parent).Ok(), []), Ct)).Ok();

        File.ReadAllText(Path.Combine(tree.Root, "vendor", "lib", "lib.txt")).Should().Be("pinned content\n");
        pinned.Should().NotBeEmpty();
    }

    [Fact]
    public void A_trees_root_inside_a_directory_the_user_settings_grant_is_refused_naming_both_paths()
    {
        var granted = _root.Sibling("granted");
        var settings = Settings($$"""{ "permissions": { "additionalDirectories": [ {{System.Text.Json.JsonSerializer.Serialize(granted)}} ] } }""");

        var refusal = ScoutGrantGuard.Refusal(Path.Combine(granted, "scout-trees"), _root.Sibling("mirrors"), [settings]);

        refusal.Should().Contain(Path.Combine(granted, "scout-trees")).And.Contain(granted).And.Contain("additionalDirectories");
        ScoutGrantGuard.Refusal(_root.Sibling("trees"), _root.Sibling("mirrors"), [settings]).Should().BeEmpty("a root outside every grant is fine");
        ScoutGrantGuard.Refusal(_root.Sibling("trees"), Path.Combine(granted, "mirrors"), [settings]).Should().Contain("mirror root", "the mirrors hold every task's future");
    }

    [Fact]
    public void A_relative_grant_is_resolved_from_a_tree_so_dot_dot_reaches_the_trees_root_and_is_refused()
    {
        var settings = Settings("""{ "permissions": { "additionalDirectories": [ ".." ] } }""");

        ScoutGrantGuard.Refusal(_root.Sibling("trees"), _root.Sibling("mirrors"), [settings]).Should().Contain(_root.Sibling("trees"));
    }

    /// <summary>§3.2 as amended 2026-10-13: a tree DROPS the project's own Claude settings (<c>.claude/settings.json</c> and a committed
    /// <c>settings.local.json</c>) and records the removal, instead of refusing the task. Measured on the D10 pool: refusing excluded 431
    /// candidates and three whole repositories — every one that commits its Claude settings with a grant outside it — a selection bias against
    /// exactly the repositories that use the tool together. The settings configure the scouting tool, not the code under investigation; the
    /// rest of <c>.claude/</c> and <c>CLAUDE.md</c> stay, as P has them.</summary>
    [Fact]
    public async Task A_tree_drops_the_projects_own_Claude_settings_records_the_removal_and_keeps_the_rest_of_dot_claude()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        Directory.CreateDirectory(Path.Combine(_repo.Root, ".claude"));
        await File.WriteAllTextAsync(Path.Combine(_repo.Root, ".claude", "settings.local.json"), """{ "permissions": { "additionalDirectories": [ "/elsewhere" ] } }""", Ct);
        await _repo.GitAsync("add", "-f", ".claude/settings.local.json");
        var parent = await _repo.CommitManyAsync(
            [(".claude/settings.json", """{ "permissions": { "additionalDirectories": [ "../.." ] } }"""), (".claude/agents/reader.md", "# reader\n"), ("CLAUDE.md", "# rules\n")],
            ("grant", "2026-05-02T10:00:00+00:00"));

        var built = (await Builder("a").BuildAsync(new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), []), Ct)).Ok();

        built.Removed.Should().Equal(".claude/settings.json", ".claude/settings.local.json");
        built.Files.Should().Equal(".claude/agents/reader.md", "CLAUDE.md", "README.md");
        ScoutGrantGuard.ProjectRefusal(built.Root).Should().BeEmpty("the tree grants nothing once its settings are gone");
    }

    /// <summary>The backstop stays: a folder whose own settings grant a directory outside it is still refused by name.</summary>
    [Fact]
    public void A_folder_whose_own_settings_grant_outside_it_is_still_refused_by_the_guard()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root.Path, "granting", ".claude")).Parent!.FullName;
        File.WriteAllText(Path.Combine(folder, ".claude", "settings.json"), """{ "permissions": { "additionalDirectories": [ "../.." ] } }""");

        ScoutGrantGuard.ProjectRefusal(folder).Should().Contain("own .claude settings grant").And.Contain("excluded");
    }

    /// <summary>Found live 2026-10-13: a private repository holds a file whose path, under the trees root, is longer than Windows' 260
    /// characters — <c>git add</c> in the new tree failed ("Filename too long") and stopped the selection. The tree's git runs with
    /// <c>core.longpaths</c> per invocation (on Linux it changes nothing).</summary>
    [Fact]
    public async Task A_tree_holds_a_path_longer_than_the_Windows_limit()
    {
        var deep = string.Join('/', Enumerable.Range(0, 6).Select(i => $"segment{i}-{new string('d', 30)}")) + "/" + new string('f', 40) + ".js";
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await _repo.GitAsync("config", "core.longpaths", "true");
        var parent = await _repo.CommitAsync((deep, "export {};\n"), ("deep", "2026-05-02T10:00:00+00:00"));

        var built = (await Builder("a").BuildAsync(new ScoutTreeSpec(ScoutTaskId.Parse("t01").Ok(), "org/repo", CommitSha.Parse(parent).Ok(), []), Ct)).Ok();

        Path.Combine(built.Root, deep).Length.Should().BeGreaterThan(260, "the path must exceed the limit for the test to mean anything");
        built.Files.Should().Contain(deep);
        (await Git(built.Root, "ls-files")).Should().Contain(deep, "the one commit holds it");
    }

    [Fact]
    public async Task The_trees_root_inside_a_checkout_must_be_ignored_there_and_nothing_under_it_tracked()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var trees = Path.Combine(_repo.Root, "scout-trees");

        (await ScoutRoots.IgnoredAsync(trees, Ct)).Should().Contain("NOT git-ignored");

        await _repo.CommitAsync(("scout-trees/leaked.txt", "a tree file somebody committed\n"), ("leak", "2026-05-02T10:00:00+00:00"));
        await _repo.CommitAsync((".gitignore", "scout-trees/\n"), ("ignore", "2026-05-03T10:00:00+00:00"));

        (await ScoutRoots.TrackedAsync(_repo.Root, trees, Ct)).Should().Equal("scout-trees/leaked.txt");
        (await ScoutRoots.IgnoredAsync(trees, Ct)).Should().Contain("TRACKED", "ignored is not enough when a file under it is already tracked — the planted negative");
    }

    /// <summary>§3.13's hygiene, on THIS repository: the conventional in-repo trees folder is git-ignored and nothing under it is tracked.</summary>
    [Fact]
    public async Task No_file_under_this_repositorys_scout_trees_folder_is_tracked_and_the_folder_is_ignored()
    {
        var repository = RepositoryRoot();
        var trees = Path.Combine(repository, "scout-trees");

        (await ScoutRoots.TrackedAsync(repository, trees, Ct)).Should().BeEmpty();
        (await ScoutRoots.IgnoredAsync(trees, Ct)).Should().BeEmpty("scout-trees/ is in this repository's .gitignore");
    }

    private async Task<string> HistoryAsync()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        return await _repo.CommitManyAsync([("src/Alpha.cs", "class Alpha {}\n"), ("src/Beta.cs", "class Beta {}\n"), ("todo/PLAN_change.md", "# plan\n")], ("p", "2026-06-01T10:00:00+00:00"));
    }

    private ScoutTrees Builder(string name) =>
        new(new ScoutTreeSettings(_root.Sibling($"trees-{name}"), _repo.Url, TimeSpan.FromMinutes(2)),
            new GitCheckoutProvider(CheckoutCacheOptions.Under(_root.Sibling($"checkouts-{name}")), NullLogger<GitCheckoutProvider>.Instance), NullLogger<ScoutTrees>.Instance);

    private string Settings(string json)
    {
        var file = Path.Combine(_root.Path, $"settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, json);
        return file;
    }

    private static async Task<string> Git(string directory, params string[] args) =>
        (await GitCommand.ReadAsync(directory, TimeSpan.FromSeconds(30), Ct, args)).Ok();

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Path.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("the test binary does not sit inside a git checkout");
    }

    public void Dispose()
    {
        _repo.Dispose();
        _root.Dispose();

        foreach (var sibling in new[] { "trees-a", "trees-b", "checkouts-a", "checkouts-b", "granted", "trees", "mirrors" })
        {
            try
            {
                TempGitRepo.DeleteTree(_root.Sibling(sibling));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temp folder is the operating system's to clean.
            }
        }
    }
}
