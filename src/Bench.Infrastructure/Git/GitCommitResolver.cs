using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Targets;

namespace Bench.Infrastructure.Git;

/// <summary>A short sha resolved to its commit in one repository — <c>git rev-parse --verify &lt;sha&gt;^{commit}</c>, read-only.</summary>
public sealed class GitCommitResolver(string repository) : ICommitResolver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task<Outcome<CommitSha>> ResolveAsync(string shortSha, CancellationToken cancellationToken) =>
        (await GitCommand.ReadAsync(repository, Timeout, cancellationToken, "rev-parse", "--verify", "--quiet", $"{shortSha.Trim()}^{{commit}}"))
            .Match(sha => CommitSha.Parse(sha.Trim()), reason => Outcome<CommitSha>.Failure($"'{shortSha}' is not a commit of the product's repository — {reason}"));
}
