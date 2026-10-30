using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>The harness's own git for one arm (plan D17 d, E2 §8 item 25). STUB.</summary>
public sealed class ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment)
{
    private readonly object[] _held = [git, workingDirectory, environment];

    public Task<Outcome<string>> HeadAsync(CancellationToken ct) => Task.FromResult(Outcome<string>.Failure("stub"));

    public Task<Outcome<string>> HeadShortAsync(CancellationToken ct) => Task.FromResult(Outcome<string>.Failure("stub"));

    public Task<Outcome<string>> DiffAsync(string from, CancellationToken ct) => Task.FromResult(Outcome<string>.Failure("stub"));
}
