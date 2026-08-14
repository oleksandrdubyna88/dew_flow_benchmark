using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>What a run WOULD execute, without executing it. The first use case, and the one that makes
/// the measurement contract visible: a plan names its target, its frozen suite, its engine, every cell
/// in execution order, and which half of the suite each question belongs to.
/// <para>
/// It also produces warnings rather than silently proceeding. A single repeat cannot support a ranking
/// — the measured repeat spread upstream reached the same magnitude as the effects being chased — so a
/// plan with <c>repeats = 1</c> is legal, useful for a smoke run, and says so out loud.
/// </para></summary>
public static class PlanRun
{
    public static Outcome<RunPlanDto> Plan(
        MeasurementTarget target,
        Suite suite,
        EngineRef engine,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes)
    {
        if (!suite.IsFrozen)
        {
            return Outcome<RunPlanDto>.Failure(
                $"suite {suite.Id} is a draft — freeze it first, or its results name a version that can still change underneath them");
        }

        var planned = Matrix.Plan(suite.Questions, repeats, subjects, lanes);

        return planned.Match(
            cells => Assemble(target, suite, engine, repeats, subjects, lanes, cells),
            Outcome<RunPlanDto>.Failure);
    }

    private static Outcome<RunPlanDto> Assemble(
        MeasurementTarget target,
        Suite suite,
        EngineRef engine,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes,
        IReadOnlyList<MatrixCell> cells) =>
        SeedSplit.Assign(suite).Match(
            halves => Outcome<RunPlanDto>.Success(new RunPlanDto(
                target.Canonical,
                suite.Stamp,
                engine.Canonical,
                suite.Questions.Count,
                repeats,
                subjects.Count * lanes.Count,
                [.. cells.Select(c => new PlannedCellDto(c.QuestionId, c.Repeat, c.Leg.Canonical, c.Position))],
                Matrix.FirstPositionCounts(cells),
                halves.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal),
                Warnings(target, engine, repeats, subjects))),
            Outcome<RunPlanDto>.Failure);

    private static IReadOnlyList<string> Warnings(
        MeasurementTarget target, EngineRef engine, int repeats, IReadOnlyList<Subject> subjects)
    {
        List<string> warnings = [];

        if (repeats < 2)
        {
            warnings.Add("repeats = 1: this plan can produce numbers but cannot support a ranking — repeat spread is routinely the size of the effect");
        }

        if (target.Exclusions.Count == 0)
        {
            warnings.Add("no exclusions: if the target repository contains published results of its own, the engine can read its own answers");
        }

        if (engine.IndexFingerprint.Length == 0)
        {
            warnings.Add("the engine reports no index fingerprint — results cannot later be attributed to the index that served them");
        }

        warnings.AddRange(subjects
            .Where(s => s.Model.IsBillable)
            .Select(s => $"{s.Model.Id} is a billable cloud model — set per-phase and per-question cost ceilings before running"));

        return warnings;
    }
}
