using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Targets;

namespace Bench.Infrastructure.Git;

/// <summary>A short sha resolved to its commit in one repository — <c>git rev-parse --verify &lt;sha&gt;^{commit}</c>, read-only.</summary>
public sealed class GitCommitResolver(string repository) : ICommitResolver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<Outcome<CommitSha>> ResolveAsync(string shortSha, CancellationToken cancellationToken) =>
        shortSha.Trim().Length == 0
            ? Outcome<CommitSha>.Failure("the record names no sha — '^{commit}' alone is the checkout's HEAD, which is not a commit the record named")
            : (await GitCommand.ReadAsync(repository, Timeout, cancellationToken, "rev-parse", "--verify", "--quiet", $"{shortSha.Trim()}^{{commit}}"))
            .Match(sha => CommitSha.Parse(sha.Trim()), reason => Outcome<CommitSha>.Failure($"'{shortSha}' is not a commit of the product's repository — {reason}"));
}
