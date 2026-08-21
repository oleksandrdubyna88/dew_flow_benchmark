using Bench.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Not trusting a CLI subject with a tree: deny-writes planted before the leg, the audit read
/// after — and the audit must not flag its own hardening, or every leg reads dirty.</summary>
public sealed class WorktreeAuditTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly DatedGitRepo _repo;

    public WorktreeAuditTests() => _repo = new DatedGitRepo(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_clean_tree_audits_clean_and_a_written_one_names_its_files()
    {
        await _repo.InitAsync(("src/F.cs", "class F { }\n"), ("seed", "2026-05-01T12:00:00"));

        (await WorktreeAudit.ChangesAsync(_repo.Root, Timeout, Ct)).Ok().Should().BeEmpty();

        await File.WriteAllTextAsync(Path.Combine(_repo.Root, "src", "Sneaky.cs"), "// written\n", Ct);

        (await WorktreeAudit.ChangesAsync(_repo.Root, Timeout, Ct)).Ok()
            .Should().Contain("Sneaky.cs", "a leg that wrote is marked with what it wrote — evidence, not assumption");
    }

    [Fact]
    public async Task The_planted_hardening_is_not_flagged_as_the_agents_write()
    {
        await _repo.InitAsync(("src/F.cs", "class F { }\n"), ("seed", "2026-05-01T12:00:00"));

        await WorktreeAudit.DenyWritesAsync(_repo.Root, Ct);

        File.ReadAllText(Path.Combine(_repo.Root, ".claude", "settings.local.json"))
            .Should().Contain("\"deny\"").And.Contain("\"Write\"");
        (await WorktreeAudit.ChangesAsync(_repo.Root, Timeout, Ct)).Ok().Should().BeEmpty(
            "flagging our own settings file would make every audited leg read dirty");
    }

    [Fact]
    public async Task A_write_elsewhere_under_dot_claude_IS_flagged()
    {
        // The target repositories COMMIT a .claude tree; a subject editing the rules that govern what a
        // session may do is exactly the write this audit exists to catch, and a whole-folder exclusion
        // was blind to it.
        await _repo.InitAsync((".claude/rules/shared/testing.md", "# rule\n"), ("seed", "2026-05-01T12:00:00"));
        await WorktreeAudit.DenyWritesAsync(_repo.Root, Ct);

        await File.WriteAllTextAsync(
            Path.Combine(_repo.Root, ".claude", "rules", "shared", "testing.md"), "# weakened\n", Ct);

        (await WorktreeAudit.ChangesAsync(_repo.Root, Timeout, Ct)).Ok()
            .Should().Contain("testing.md");
    }

    public void Dispose() => _repo.Dispose();
}
