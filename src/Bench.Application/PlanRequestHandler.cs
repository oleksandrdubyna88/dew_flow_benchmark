using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>Turns a plan request into a plan. The ONE place that does it: the CLI and the API both
/// arrive here, so the two surfaces cannot drift into different behaviours sharing a name.</summary>
public static class PlanRequestHandler
{
    public static Outcome<RunPlanDto> Handle(PlanRequestDto request) =>
        ReadTarget(request).Match(
            target => ReadSuite(request, target).Match(
                suite => ReadSubjects(request).Match(
                    subjects => PlanRun.Plan(target, suite, ReadEngine(request), request.Repeats, subjects, ReadLanes(request)),
                    Outcome<RunPlanDto>.Failure),
                Outcome<RunPlanDto>.Failure),
            Outcome<RunPlanDto>.Failure);

    private static Outcome<MeasurementTarget> ReadTarget(PlanRequestDto request) =>
        RepoUrl.Parse(request.Repo).Match(
            repo => CommitSha.Parse(request.Commit).Match(
                commit => Outcome<MeasurementTarget>.Success(
                    MeasurementTarget.At(repo, commit).Excluding([.. request.Exclusions])),
                Outcome<MeasurementTarget>.Failure),
            Outcome<MeasurementTarget>.Failure);

    private static Outcome<Suite> ReadSuite(PlanRequestDto request, MeasurementTarget target) =>
        SuiteJsonLoader.Load(request.SuiteJson, target.Commit);

    /// <summary>Entries of the form <c>id@local</c> / <c>id@cloud</c>. An entry with no id is refused
    /// rather than defaulted — an unset model must never resolve to whatever the installation happens
    /// to call its default, which is how a run labelled "local, $0" once came one invocation away from
    /// billing a cloud API.</summary>
    private static Outcome<IReadOnlyList<Subject>> ReadSubjects(PlanRequestDto request)
    {
        if (request.Subjects.Count == 0)
        {
            return Outcome<IReadOnlyList<Subject>>.Failure(
                "at least one subject is required, e.g. qwen@local or opus@cloud");
        }

        List<Subject> subjects = [];

        foreach (var entry in request.Subjects)
        {
            var parsed = ParseSubject(entry, request.Seed);

            if (parsed is Outcome<Subject>.Fail fail)
            {
                return Outcome<IReadOnlyList<Subject>>.Failure(fail.Reason);
            }

            subjects.Add(((Outcome<Subject>.Ok)parsed).Value);
        }

        return Outcome<IReadOnlyList<Subject>>.Success(subjects);
    }

    private static Outcome<Subject> ParseSubject(string entry, int seed)
    {
        var at = entry.LastIndexOf('@');
        var id = at < 0 ? entry : entry[..at];
        var hosting = at >= 0 && entry[(at + 1)..].Equals("cloud", StringComparison.OrdinalIgnoreCase)
            ? ModelHosting.Cloud
            : ModelHosting.Local;

        return ModelRef.Parse(id, hosting).Match(
            model => Outcome<Subject>.Success(new Subject(model, Sampling.Deterministic(seed))),
            Outcome<Subject>.Failure);
    }

    private static IReadOnlyList<Lane> ReadLanes(PlanRequestDto request) =>
        request.Lanes.Count == 0 ? [Lane.Named("default")] : [.. request.Lanes.Select(Lane.Named)];

    private static EngineRef ReadEngine(PlanRequestDto request)
    {
        var kind = Enum.TryParse<EngineKind>(request.Engine, ignoreCase: true, out var parsed)
            ? parsed
            : EngineKind.NoRetrieval;

        return new EngineRef(kind, request.EngineEndpoint, request.EngineVersion, request.IndexFingerprint);
    }
}
