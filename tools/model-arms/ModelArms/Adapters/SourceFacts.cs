using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What git says about the target's source, asked of the git <c>Resolve</c> FOUND — launched by its real path, under a replaced environment
/// (<see cref="GitEnvironment"/>), from the source as its working directory. Nothing is looked up by name: a bare <c>git</c> is resolved by the runtime beside the
/// binary, in the current directory and then in PATH, so a <c>git</c> an arm placed there would answer the question that exists to keep arms' files from running.
/// <para>
/// <b>What <c>rev-parse</c> runs.</b> It is a builtin that reads the config and the object database and runs no configured program: no hook (hooks run on commit,
/// push, checkout), no <c>core.fsmonitor</c> (consulted when the index is refreshed: <c>status</c>, <c>add</c>), no <c>core.sshCommand</c> or credential helper
/// (transport), no diff or filter driver, no pager (the output is a pipe), no editor. That is observed, not reasoned: <c>ValidateVerbTests</c> puts a marker shim under
/// every key git 2.53 runs a program from in the target's <c>.git/config</c> and sees none run. No flag is passed to switch any of it off — a flag nobody observed
/// working is a comment. The git that answers is the host's, trusted as the system is; the target's config is READ.
/// </para>
/// <para>
/// <b>The questions.</b> Is the source the top of a checkout (<c>--show-toplevel</c> equal, by real path, to the source: a plain folder INSIDE a checkout answers
/// <c>--git-dir</c> with success), does it hold the plan branch, the base commit, and the plan at that commit (<c>rev-parse --verify --quiet</c>: exit 1 is the "no").
/// Any other exit — 128 "dubious ownership", a timeout, a start that failed — is <see cref="SourceAnswer.Unanswered"/> with git's first line, never a silent "no".
/// A source with no <c>.git</c> in it or any ancestor is not a repository and git is not asked (exit 128 would be the same for it and for dubious ownership).
/// </para></summary>
internal static class SourceFacts
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    public static async Task<SourceAnswer> ReadAsync(
        TargetFacts target, string git, IReadOnlyDictionary<string, string> environment, Func<string, Outcome<string>> realPath, CancellationToken ct)
    {
        if (!Directory.Exists(target.Source))
        {
            return new SourceAnswer.Missing();
        }

        if (RootFacts.GitTopOf(target.Source).Length == 0)
        {
            return NotARepository;
        }

        var run = new GitRun(git, target.Source, environment, ct);
        var isGit = await run.IsTopOfCheckoutAsync(realPath);
        return await isGit.Match(
            top => top ? PlanAsync(run, target) : Task.FromResult<SourceAnswer>(NotARepository),
            reason => Task.FromResult<SourceAnswer>(new SourceAnswer.Unanswered(reason)));
    }

    private static SourceAnswer NotARepository { get; } = new SourceAnswer.Asked(false, false, false, false);

    private static async Task<SourceAnswer> PlanAsync(GitRun run, TargetFacts target)
    {
        var branch = await run.YesNoAsync("rev-parse", "--verify", "--quiet", "refs/heads/" + target.PlanBranch);
        var commit = await run.YesNoAsync("rev-parse", "--verify", "--quiet", target.Base.Value + "^{commit}");
        var plan = commit is Outcome<bool>.Ok { Value: true }
            ? await run.YesNoAsync("rev-parse", "--verify", "--quiet", target.Base.Value + ":" + target.Plan)
            : Outcome<bool>.Success(false);
        return new[] { branch, commit, plan }.OfType<Outcome<bool>.Fail>().FirstOrDefault() is { } failed
            ? new SourceAnswer.Unanswered(failed.Reason)
            : new SourceAnswer.Asked(true, Yes(branch), Yes(commit), Yes(plan));
    }

    private static bool Yes(Outcome<bool> answer) => answer is Outcome<bool>.Ok { Value: true };

    /// <summary>One git, launched by the path it was found at.</summary>
    private sealed class GitRun(string git, string source, IReadOnlyDictionary<string, string> environment, CancellationToken ct)
    {
        public async Task<Outcome<bool>> IsTopOfCheckoutAsync(Func<string, Outcome<string>> realPath) =>
            (await RunAsync("rev-parse", "--show-toplevel")).Match(
                done => done.ExitCode == 0 ? Outcome<bool>.Success(Same(done.StandardOutput.Trim(), realPath)) : Outcome<bool>.Failure(Said(done)),
                Outcome<bool>.Failure);

        /// <summary>Exit 0 is yes, 1 is no; anything else is git failing to answer.</summary>
        public async Task<Outcome<bool>> YesNoAsync(params string[] arguments) =>
            (await RunAsync(arguments)).Match(
                done => done.ExitCode switch
                {
                    0 => Outcome<bool>.Success(true),
                    1 => Outcome<bool>.Success(false),
                    _ => Outcome<bool>.Failure(Said(done)),
                },
                Outcome<bool>.Failure);

        private bool Same(string toplevel, Func<string, Outcome<string>> realPath) =>
            realPath(toplevel) is Outcome<string>.Ok top && realPath(source) is Outcome<string>.Ok here && top.Value == here.Value;

        private async Task<Outcome<ProcessResult>> RunAsync(params string[] arguments)
        {
            var attempt = await ProcessRunner.RunAsync(git, arguments, source, Budget, string.Empty, environment, ct);
            return attempt is ProcessAttempt.Completed completed
                ? Outcome<ProcessResult>.Success(completed.Result)
                : Outcome<ProcessResult>.Failure($"git could not answer about target.source: {attempt.Describe}");
        }

        private static string Said(ProcessResult done)
        {
            var line = FirstLine(done.StandardError);
            return $"git could not answer about target.source: {(line.Length > 0 ? line : $"exit {done.ExitCode}")}";
        }

        private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
    }
}
