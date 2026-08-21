namespace Bench.Tests.Cli;

/// <summary>
/// One accepted reading question, as the bank-import file a run can freeze a selection from.
///
/// <para>Shared because a SECOND caller appeared — the lane-planning tests and the resume tests both need
/// the smallest bank a run can be built on, and a copy is where the two would drift on the one thing that
/// matters here: the group key and question id the assertions look for.</para>
/// </summary>
internal sealed class TempBank : IDisposable
{
    public TempBank(string suffix, string repoUrl, string commit)
    {
        Group = $"reading-{suffix}";
        QuestionId = $"readq-{suffix}";
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-lanebank-{suffix}.json");
        File.WriteAllText(Path, $$"""
        {
          "targetRepo": "{{repoUrl.Replace("\\", "\\\\")}}",
          "authoredAtCommit": "{{commit}}",
          "groups": [ { "key": "{{Group}}", "title": "Reading tasks", "ordinal": 1 } ],
          "reviewers": [],
          "questions": [
            {
              "group": "{{Group}}", "ordinal": 1, "kind": "Reading", "state": "Accepted",
              "source": "BugsAndTests", "authorModel": "harvest",
              "seed": { "kind": "commit", "reference": "abc", "at": "2026-08-11T00:00:00Z" },
              "id": "{{QuestionId}}",
              "prompt": "What does this repository contain?",
              "expectations": [ { "kind": "File", "file": "one.txt" } ]
            }
          ]
        }
        """);
    }

    public string Path { get; }

    public string Group { get; }

    public string QuestionId { get; }

    public void Dispose() => File.Delete(Path);
}
