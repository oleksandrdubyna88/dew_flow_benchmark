using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>A rubric the catalog holds, with the text it hashes to — what the assessor is sent.</summary>
public sealed record LoadedRubric(Rubric Rubric, string Text);

/// <summary>The gate benchmark's two rubrics, read from <c>prompts/gate-assess/</c> through <see cref="PromptCatalog"/>
/// and hashed there: <c>strict-v1</c> (<c>strict.md</c>, the calibration's instructions verbatim — the only one this
/// benchmark ASKS) and <c>lenient-worth-v1</c> (the coai-bench judge's question, a label for imported verdicts). The
/// hash is of the file, so an edited file is a new rubric and never a silent re-reading of old verdicts.</summary>
public static class GateRubrics
{
    public const string StrictId = "strict-v1";
    public const string LenientId = "lenient-worth-v1";

    private static readonly (string Id, RubricKind Kind, string File)[] Known =
    [
        (StrictId, RubricKind.Strict, "strict"),
        (LenientId, RubricKind.LenientWorth, "lenient-worth-v1"),
    ];

    public static Outcome<IReadOnlyList<LoadedRubric>> Load(string promptsRoot)
    {
        var loaded = new List<LoadedRubric>();

        foreach (var (id, kind, file) in Known)
        {
            var read = PromptCatalog.GateRubric(promptsRoot, file)
                .Match(prompt => Rubric.Of(id, kind, prompt.Hash).Match(r => Outcome<LoadedRubric>.Success(new LoadedRubric(r, prompt.Text)), Outcome<LoadedRubric>.Failure),
                    Outcome<LoadedRubric>.Failure);

            if (read is Outcome<LoadedRubric>.Fail fail)
            {
                return Outcome<IReadOnlyList<LoadedRubric>>.Failure(fail.Reason);
            }

            loaded.Add(((Outcome<LoadedRubric>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<LoadedRubric>>.Success(loaded);
    }

    public static RubricCatalog Catalog(IReadOnlyList<LoadedRubric> rubrics) => new([.. rubrics.Select(r => r.Rubric)]);

    /// <summary>The rubric an IMPORT labels verdicts with — any rubric this build holds, the lenient one included: an import
    /// asks nobody, it records what another harness's judge already said (E5).</summary>
    public static Outcome<LoadedRubric> Labelling(IReadOnlyList<LoadedRubric> rubrics, string id) =>
        rubrics.FirstOrDefault(r => string.Equals(r.Rubric.Id.Value, id, StringComparison.Ordinal)) is { } held
            ? Outcome<LoadedRubric>.Success(held)
            : Outcome<LoadedRubric>.Failure($"no rubric '{id}' — this build holds {string.Join(", ", rubrics.Select(r => r.Rubric.Id.Value))}");

    /// <summary>The rubric an assessment ASKS under. Only a strict rubric is asked: the lenient one names verdicts another
    /// harness produced, and sending its question here would be a third rubric wearing its name.</summary>
    public static Outcome<LoadedRubric> Asked(IReadOnlyList<LoadedRubric> rubrics, string id)
    {
        var held = rubrics.FirstOrDefault(r => string.Equals(r.Rubric.Id.Value, id, StringComparison.Ordinal));

        return (held, held?.Rubric.Kind) switch
        {
            (null, _) => Outcome<LoadedRubric>.Failure($"no rubric '{id}' — this build holds {string.Join(", ", rubrics.Select(r => r.Rubric.Id.Value))}"),
            (_, not RubricKind.Strict) => Outcome<LoadedRubric>.Failure(
                $"rubric '{id}' labels imported verdicts and is never asked — the assessment asks {StrictId}"),
            _ => Outcome<LoadedRubric>.Success(held),
        };
    }
}
