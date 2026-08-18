using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>Pre-trusting a checkout before an agent is launched in it.
/// <para>
/// Against a TEMPORARY config file, never <c>~/.claude.json</c>: these tests write, and the real file holds the
/// operator's whole CLI state.
/// </para>
/// <para>
/// It exists because of a measured half hour. Two of four authoring groups on 2026-08-18 produced nothing and
/// burned their full 900-second wall, because the CLI would not act in an untrusted workspace and waited for a
/// dialog no headless run can answer.
/// </para></summary>
public sealed class WorkspaceTrustTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"bench-trust-{Guid.NewGuid():N}");

    private string Config => Path.Combine(_home, ".claude.json");

    private string Root => Path.Combine(_home, "checkouts");

    public WorkspaceTrustTests() => Directory.CreateDirectory(Root);

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public void A_worktree_needs_BOTH_its_own_path_and_the_bare_repository_it_points_at()
    {
        var worktree = Tree("worktrees/abc", "gitdir: " + Path.Combine(Root, "bare", "x.git", "worktrees", "abc"));

        var keys = WorkspaceTrust.KeysFor(worktree);

        // The CLI's own message names the BARE path while the agent is launched in the worktree, so trusting one
        // and not the other is a coin flip on which spelling it looks up.
        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.EndsWith("worktrees/abc", StringComparison.OrdinalIgnoreCase));
        keys.Should().Contain(k => k.EndsWith("bare/x.git", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Keys_are_written_with_forward_slashes_because_that_is_what_the_CLI_writes()
    {
        WorkspaceTrust.KeysFor(Tree("plain", string.Empty)).Should().OnlyContain(k => !k.Contains('\\'),
            "the lookup is by string, so a differently-spelled key is a new entry nothing reads");
    }

    [Theory]
    [InlineData("gitdir: C:/repos/bare/x.git/worktrees/abc", "C:/repos/bare/x.git")]
    [InlineData("gitdir: C:\\repos\\bare\\x.git\\worktrees\\abc", "C:/repos/bare/x.git")]
    [InlineData("gitdir: C:/repos/plain/.git", "C:/repos/plain/.git")]
    [InlineData("this is not a git pointer", "")]
    [InlineData("", "")]
    public void The_bare_repository_is_read_out_of_the_pointer_file(string text, string expected) =>
        WorkspaceTrust.Bare(text).Should().Be(expected);

    [Fact]
    public void A_fresh_config_gains_the_trust_flag_for_every_key()
    {
        var worktree = Tree("worktrees/one", "gitdir: " + Path.Combine(Root, "bare", "one.git", "worktrees", "one"));
        Write(new JsonObject { ["numStartups"] = 2 });

        var result = Trust().Ensure(worktree).Ok();

        result.Outcome.Should().Be(TrustOutcome.Granted);
        Projects().Should().HaveCount(2);
        Projects().Should().OnlyContain(entry => entry.Value!["hasTrustDialogAccepted"]!.GetValue<bool>());
    }

    [Fact]
    public void An_existing_entry_keeps_every_OTHER_field_it_had()
    {
        var worktree = Tree("plain", string.Empty);
        var key = worktree.Replace('\\', '/');
        Write(new JsonObject
        {
            ["projects"] = new JsonObject
            {
                [key] = new JsonObject { ["allowedTools"] = new JsonArray("Read"), ["hasTrustDialogAccepted"] = false },
            },
        });

        Trust().Ensure(worktree).Ok().Outcome.Should().Be(TrustOutcome.Granted);

        // The one boolean, and nothing else. This file is the operator's CLI state — allowed tools, MCP server
        // choices, onboarding flags — and a rewritten entry would silently drop whichever of them we forgot.
        var entry = Projects()[key]!;
        entry["hasTrustDialogAccepted"]!.GetValue<bool>().Should().BeTrue();
        entry["allowedTools"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    public void A_config_that_ALREADY_trusts_the_keys_is_left_alone()
    {
        var worktree = Tree("plain", string.Empty);
        Write(new JsonObject
        {
            ["projects"] = new JsonObject
            {
                [worktree.Replace('\\', '/')] = new JsonObject { ["hasTrustDialogAccepted"] = true },
            },
        });
        var before = File.ReadAllText(Config);

        Trust().Ensure(worktree).Ok().Outcome.Should().Be(TrustOutcome.AlreadyTrusted);

        File.ReadAllText(Config).Should().Be(before, "an idempotent step must not rewrite a live config every run");
    }

    [Fact]
    public void A_directory_OUTSIDE_the_checkout_root_is_refused_by_name()
    {
        var elsewhere = Path.Combine(_home, "somebody-elses-repo");
        Directory.CreateDirectory(elsewhere);
        Write(new JsonObject());

        var result = Trust().Ensure(elsewhere).Ok();

        // The scope IS the guard. Trusting an arbitrary path on the operator's behalf would hand any repository
        // this benchmark is pointed at the permissions of a trusted workspace.
        result.Outcome.Should().Be(TrustOutcome.Refused);
        result.Reason.Should().Contain("not under this benchmark's checkout root");
        Projects().Should().BeEmpty();
    }

    [Fact]
    public void A_missing_config_is_a_refusal_rather_than_a_config_this_run_invents()
    {
        var result = Trust().Ensure(Tree("plain", string.Empty)).Ok();

        result.Outcome.Should().Be(TrustOutcome.Refused);
        result.Reason.Should().Contain("run the agent's CLI once");
        File.Exists(Config).Should().BeFalse();
    }

    [Fact]
    public void A_projects_node_that_is_not_an_object_REFUSES_instead_of_replacing_it()
    {
        var worktree = Tree("plain", string.Empty);
        Write(new JsonObject { ["projects"] = "surprise" });

        var refused = Trust().Ensure(worktree);

        refused.Reason().Should().Contain("refusing").And.Contain("the operator's");
        JsonNode.Parse(File.ReadAllText(Config))!["projects"]!.GetValue<string>().Should().Be("surprise");
    }

    [Fact]
    public void A_granted_write_leaves_a_BACKUP_of_what_was_there()
    {
        var worktree = Tree("plain", string.Empty);
        Write(new JsonObject { ["numStartups"] = 7 });

        Trust().Ensure(worktree);

        // The one operation here that touches a file nobody asked us to own. A backup is cheap and the failure
        // it guards — a half-written CLI config — is not.
        File.Exists(Config + ".bench-backup").Should().BeTrue();
        JsonNode.Parse(File.ReadAllText(Config + ".bench-backup"))!["numStartups"]!.GetValue<int>().Should().Be(7);
    }

    private WorkspaceTrust Trust() => new(Config, Root);

    private JsonObject Projects() =>
        JsonNode.Parse(File.ReadAllText(Config))!["projects"] as JsonObject ?? [];

    private void Write(JsonObject config) =>
        File.WriteAllText(Config, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>A directory under the checkout root, with a <c>.git</c> pointer file when one is given.</summary>
    private string Tree(string relative, string pointer)
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);

        if (pointer.Length > 0)
        {
            File.WriteAllText(Path.Combine(path, ".git"), pointer);
        }

        return path;
    }
}
