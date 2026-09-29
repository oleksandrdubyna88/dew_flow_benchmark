using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Tests.Gate.Driver;

/// <summary>The seeded task and the gate's clones WITHOUT a database — what the clone and <c>suite verify</c> tests need.
/// <see cref="GateDriverRig"/> creates a Postgres database per test, and CI's container refused new clients ("53300: too
/// many clients") once enough tests that never touch one had each made one.</summary>
internal sealed class GateCloneRig : IAsyncDisposable
{
    private GateCloneRig(DatedGitRepo repo, TempRoot root, GateTask task)
    {
        Repo = repo;
        Root = root;
        Task = task;
        Checkouts = new GateCloneCheckouts(
            new GitCheckoutProvider(CheckoutCacheOptions.Under(root.Sibling("checkouts")), NullLogger<GitCheckoutProvider>.Instance),
            root.Sibling("checkouts"));
    }

    public DatedGitRepo Repo { get; }

    public TempRoot Root { get; }

    public GateTask Task { get; }

    public GateCloneCheckouts Checkouts { get; }

    public static async Task<GateCloneRig> StartAsync()
    {
        var repo = new DatedGitRepo(Xunit.TestContext.Current.CancellationToken);
        var task = await GateDriverRig.SeedTaskAsync(repo);

        return new GateCloneRig(repo, GateStoreFixtures.NewRoot(), task);
    }

    public ValueTask DisposeAsync()
    {
        Repo.Dispose();
        Root.Dispose();
        GateDriverRig.DeleteSiblings(Root);

        return ValueTask.CompletedTask;
    }
}
