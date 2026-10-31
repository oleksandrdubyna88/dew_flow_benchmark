using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The base clone (<c>prep-base.sh:7-11</c>, plan E3 §8 item 9): the plan branch cloned from the operator's checkout, DETACHED at <c>target.base</c> and asserted to be there, and the rules submodule — the one whose path is <c>target.rulesSubmodule</c> — pointed at the
/// operator's own checkout of it and updated from there. Every question goes through <see cref="ArmGit"/> (the real git the verb resolved, <c>GIT_*</c> taken out, every question <c>-c core.fsmonitor=false</c>); a failure is a stop with its exit code, never a throw. Exit 3 is a host or a repository that could not answer;
/// exit 4 a rules submodule that is not one.</summary>
internal static class BaseCheckout
{
    private const string Name = "model-arms prep-base";

    /// <summary>The clone, made. The answer is the short id of its HEAD, for the stage log.</summary>
    public static async Task<Passage<string>> MakeAsync(TargetFacts target, string root, string baseFolder, string git, IReadOnlyDictionary<string, string> environment, CancellationToken ct)
    {
        var cloned = await new ArmGit(git, root, environment).CloneAsync(target.Source, target.PlanBranch, baseFolder, ct);
        if (cloned is Outcome<string>.Fail clone)
        {
            return Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot clone {target.Source} at {target.PlanBranch} into {baseFolder}: {clone.Reason}");
        }

        var inBase = new ArmGit(git, baseFolder, environment);
        var at = await DetachedAsync(target, inBase, ct);
        return at is Passage<string>.Through ? await SubmoduleAsync(target, inBase, ct) : at;
    }

    private static async Task<Passage<string>> DetachedAsync(TargetFacts target, ArmGit inBase, CancellationToken ct)
    {
        var detached = await inBase.DetachAsync(target.Base.Value, ct);
        if (detached is Outcome<string>.Fail fail)
        {
            return Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot check out {target.Base.Value} in the base clone: {fail.Reason}");
        }

        return (await inBase.HeadAsync(ct)).Match(
            head => string.Equals(head, target.Base.Value, StringComparison.OrdinalIgnoreCase)
                ? new Passage<string>.Through(head)
                : Passage<string>.Stop(ExitCodes.Environment, $"{Name}: the base clone is at {head}, not at target.base {target.Base.Value}"),
            reason => Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot read the base clone's HEAD: {reason}"));
    }

    private static async Task<Passage<string>> SubmoduleAsync(TargetFacts target, ArmGit inBase, CancellationToken ct)
    {
        var paths = await inBase.SubmodulePathsAsync(ct);
        if (paths is Outcome<string>.Fail failed)
        {
            return Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot read .gitmodules of the base clone: {failed.Reason}");
        }

        var name = GitModules.NameOf(((Outcome<string>.Ok)paths).Value, target.RulesSubmodule);
        if (name.Length == 0)
        {
            return Passage<string>.Stop(ExitCodes.Configuration, $"{Name}: target.rulesSubmodule {target.RulesSubmodule} is not a submodule of the target at {target.Base.Value}");
        }

        var url = Path.Combine(target.Source, target.RulesSubmodule);
        var set = await inBase.SetSubmoduleUrlAsync(name, url, ct);
        var updated = set is Outcome<string>.Ok ? await inBase.SubmoduleUpdateAsync(target.RulesSubmodule, ct) : set;
        return updated is Outcome<string>.Fail fail
            ? Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot bring the rules submodule {target.RulesSubmodule} from {url}: {fail.Reason}")
            : (await inBase.HeadShortAsync(ct)).Match(
                head => (Passage<string>)new Passage<string>.Through(head),
                reason => Passage<string>.Stop(ExitCodes.Environment, $"{Name}: cannot read the base clone's HEAD: {reason}"));
    }
}
