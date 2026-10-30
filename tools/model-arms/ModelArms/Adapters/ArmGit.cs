using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Infrastructure.Process;

namespace ModelArms.Adapters;

/// <summary>The harness's own git for one arm (plan D17 d, E2 §8 item 25): launched by the REAL path <c>run-arm</c> resolved, with the working directory the arm's clone and an environment with every <c>GIT_*</c> of the
/// parent taken out, through the shared launcher's replacing overload and a ceiling of 60 s. The arm's claude keeps its own environment (D19): two processes, two environments, on purpose. Nothing here names the module's
/// bare-name git helper — the architecture scan is red the moment it does.
/// <para>
/// A question git cannot answer is a <see cref="Outcome{T}"/> failure carrying git's first stderr line, or how the attempt ended (<c>exit 128</c>, <c>timed out</c>, <c>not found</c>).
/// </para></summary>
public sealed partial class ArmGit(string git, string workingDirectory, IReadOnlyDictionary<string, string> environment)
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    public async Task<Outcome<string>> HeadAsync(CancellationToken ct) => Trimmed(await AskAsync(["rev-parse", "HEAD"], ct));

    public async Task<Outcome<string>> HeadShortAsync(CancellationToken ct) => Trimmed(await AskAsync(["rev-parse", "--short", "HEAD"], ct));

    /// <summary><c>diff &lt;from&gt; HEAD</c>, stdout verbatim. <paramref name="from"/> must be a commit id in hex: it came from a file, and a file's text is never handed to git as an option.</summary>
    public async Task<Outcome<string>> DiffAsync(string from, CancellationToken ct) =>
        CommitId().IsMatch(from) ? await AskAsync(["diff", from, "HEAD"], ct) : Outcome<string>.Failure($"'{from}' is not a commit id — the snapshot file does not hold one");

    private static Outcome<string> Trimmed(Outcome<string> answer) => answer.Match(text => Outcome<string>.Success(text.Trim()), Outcome<string>.Failure);

    private async Task<Outcome<string>> AskAsync(string[] arguments, CancellationToken ct)
    {
        var attempt = await ProcessRunner.RunAsync(git, arguments, workingDirectory, Budget, string.Empty, environment, ct);
        return attempt is ProcessAttempt.Completed { Result.ExitCode: 0 } done
            ? Outcome<string>.Success(done.Result.StandardOutput)
            : Outcome<string>.Failure(Said(attempt));
    }

    private static string Said(ProcessAttempt attempt) =>
        attempt is ProcessAttempt.Completed done && FirstLine(done.Result.StandardError) is { Length: > 0 } line ? line : attempt.Describe;

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{4,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommitId();
}
