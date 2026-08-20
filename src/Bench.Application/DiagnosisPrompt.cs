using Bench.Domain.Retrieval;
using Bench.Domain.Suites;

namespace Bench.Application;

/// <summary>The Investigate phase's prompt: the ordinary leg prompt — statement plus whatever
/// retrieval surfaced — with the diagnosis CONTRACT appended (todo/PLAN_investigate_vs_implement.md
/// §3.2).
/// <para>
/// Part of the harness like <see cref="RagPrompt"/>, not a lane doctrine: the doctrine axis measures
/// wordings against each other, while this text defines what the arm DELIVERS — change it and every
/// earlier investigate leg was measured under a different contract. Whether the contract's wording
/// itself shapes the investigation is the plan's open question 4, and if it does, an A/B of contract
/// wordings becomes a doctrine-style arm rather than an edit here.
/// </para></summary>
public static class DiagnosisPrompt
{
    /// <summary>The contract, verbatim. The shape mirrors what <see cref="DiagnosisJson"/> reads —
    /// one fenced block, extraction never repair — and the last sentence is the symptom-trap's other
    /// half: the scorer punishes naming only where the defect manifests, so the contract says so out
    /// loud rather than grading a rule nobody stated.</summary>
    public const string Contract =
        """
        Investigate the defect described above in the repository at the commit under test. Do not write a patch.
        End your answer with exactly ONE fenced JSON block of this shape:

        ```json
        {
          "anchors": [{ "path": "src/File.cs", "member": "Type.Member", "lines": { "start": 120, "end": 141 } }],
          "mechanism": "why the defect happens - the causal chain, not the symptom",
          "fixIntent": "what should change, and where"
        }
        ```

        "path" and "mechanism" are required; "member" and "lines" may be omitted when unknown.
        Name the place the defect is CAUSED. Naming only where it manifests is a wrong answer.
        """;

    public static string Assemble(Question question, RetrievedContext context, RagPromptLimits limits) =>
        RagPrompt.Assemble(question, context, limits) + "\n\n" + Contract;
}
