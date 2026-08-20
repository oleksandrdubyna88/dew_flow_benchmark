using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Models;

/// <summary>An arbiter that is a model — asked once, over stored evidence, with the narrowest question
/// that can still be useful.
/// <para>
/// <b>Not "score this answer".</b> A judge asked for a number invents a scale and then drifts along it
/// between runs, which is the failure mode that makes judged benchmarks unreproducible. This one is asked
/// a single yes/no — <i>does the answer state the same thing as the reference</i> — because a binary is
/// the only judgement that means the same thing in March and in August.
/// </para>
/// <para>
/// It is also asked at temperature 0 with a fixed seed, and what actually went out is recorded on the
/// answer: a runtime that substitutes its own sampling over the request is a real, measured behaviour
/// here, and an arbiter whose sampling drifted silently would re-score an old run differently for no
/// visible reason.
/// </para></summary>
/// <summary>What a verdict is ABOUT — the judge's construction detail, never the port's: `IJudge` still
/// asks one binary over (question, answer, reference), and the framing decides what those three ARE.
/// Chosen from the run's stored <see cref="TaskKind"/>, so re-judging an old run months later frames it
/// the way its legs were asked.</summary>
public enum JudgeFraming
{
    /// <summary>A reading leg: does the candidate answer state what the reference answer states.</summary>
    Answer,

    /// <summary>A fix leg (todo/PLAN_investigate_vs_implement.md step 7): the reference is the
    /// reference FIX's unified diff — ground truth that can be run, unlike any prose — and the question
    /// is whether the candidate's diagnosis identifies the cause that fix addresses, or patched a
    /// symptom. The part no mechanical signal covers, exactly as PLAN_code_lane.md §5.4 scoped it.</summary>
    Diagnosis,
}

public sealed class ModelJudge(
    IModelRuntime runtime, ModelEndpoint endpoint, int seed, JudgeFraming framing = JudgeFraming.Answer) : IJudge
{
    /// <summary>Deliberately not a rubric. Every extra instruction is an extra thing that can change
    /// meaning when the arbiter is swapped, and the port exists precisely so it can be swapped.</summary>
    private const string AnswerSystem = """
        You judge whether a candidate answer states the same thing as a reference answer about a
        specific code repository. You are not grading style, completeness, or wording.

        Answer with exactly one word on the first line: YES or NO.
        YES  — the candidate states the same mechanism or fact as the reference.
        NO   — it contradicts the reference, is about something else, or is too vague to tell.
        Then one short line saying why.
        """;

    private const string DiagnosisSystem = """
        You judge whether a candidate DIAGNOSIS of a software defect identifies the cause that the
        reference fix addresses. The reference is the actual fix, as a unified diff. You are not
        grading style, completeness, or whether the candidate proposed the same code.

        Answer with exactly one word on the first line: YES or NO.
        YES  — the diagnosis names the mechanism the fix corrects, in the place the fix corrects it.
        NO   — it names a different cause, only describes the symptom, or is too vague to tell.
        Then one short line saying why.
        """;

    public ModelRef Model => endpoint.Model;

    public async Task<Outcome<JudgeVerdict>> JudgeAsync(
        string question, string answer, string reference, CancellationToken cancellationToken)
    {
        var system = framing == JudgeFraming.Diagnosis ? DiagnosisSystem : AnswerSystem;
        var request = new ModelRequest(
            endpoint, Sampling.Deterministic(seed), system, Prompt(question, answer, reference), []);
        var asked = await runtime.AskAsync(request, cancellationToken);

        if (asked is Outcome<ModelAnswer>.Fail unreachable)
        {
            return Outcome<JudgeVerdict>.Failure($"the arbiter could not be reached: {unreachable.Reason}");
        }

        var verdict = ((Outcome<ModelAnswer>.Ok)asked).Value;

        if (!verdict.Text.WasCaptured)
        {
            return Outcome<JudgeVerdict>.Failure($"the arbiter returned no text: {verdict.Text.Reason}");
        }

        return Read(verdict.Text.Value);
    }

    /// <summary>Parsing the verdict — and refusing rather than guessing.
    /// <para>
    /// An unreadable verdict is a REFUSAL, never a NO. Defaulting to NO would make an arbiter that has
    /// stopped answering look exactly like a subject that has stopped being right, and every leg it
    /// touched would silently join the failures.
    /// </para></summary>
    private static Outcome<JudgeVerdict> Read(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = lines.Length > 0 ? lines[0].TrimEnd('.', ':', ',').Trim() : string.Empty;
        var why = lines.Length > 1 ? lines[1] : text.Trim();

        return first.ToUpperInvariant() switch
        {
            "YES" => Outcome<JudgeVerdict>.Success(new JudgeVerdict(true, why)),
            "NO" => Outcome<JudgeVerdict>.Success(new JudgeVerdict(false, why)),
            _ => Outcome<JudgeVerdict>.Failure(
                $"the arbiter's verdict did not begin with YES or NO — it began '{Head(first)}'"),
        };
    }

    private static string Head(string line) => line.Length <= 40 ? line : line[..40] + "…";

    private string Prompt(string question, string answer, string reference) =>
        framing == JudgeFraming.Diagnosis
            ? $"""
              THE DEFECT, AS THE SOLVER WAS TOLD IT
              {question}

              THE REFERENCE FIX (unified diff)
              {reference}

              THE CANDIDATE'S DIAGNOSIS
              {answer}
              """
            : $"""
              QUESTION
              {question}

              REFERENCE ANSWER
              {reference}

              CANDIDATE ANSWER
              {answer}
              """;
}
