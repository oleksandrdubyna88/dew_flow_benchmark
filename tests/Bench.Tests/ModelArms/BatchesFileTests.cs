using System.Text.Json.Nodes;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Tasks;
using Xunit;
using static Bench.Tests.ModelArms.TasksPlanKit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S1: <c>work/batches.json</c> — written in the shell's bytes (<c>tasks-plan.mjs:50</c>; the oracle test of <see cref="TasksPlanTests"/> compares it with the shell's own file) and read back by the pipeline's re-entry, which refuses what the writer cannot produce — and the
/// <c>{files}</c> field of a task's brief.
/// <para>
/// What this does NOT prove: that a re-entry's text comparison (E4.S3) sees a hand-edited file; the reader is strict about the shape, not about the tasks' meaning (the checker decided that when the file was written).
/// </para></summary>
public sealed class BatchesFileTests
{
    private const string Work = "/x/arm/work";

    private static TaskBatches Seven() => Check(Valid(7)).Batches;

    private static JsonObject Edited(Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(BatchesFile.Write(Seven(), Work))!.AsObject();
        edit(node);
        return node;
    }

    private static string Refused(Action<JsonObject> edit) => BatchesFile.Read(Edited(edit).ToJsonString(), Work).Reason();

    [Fact]
    public void Batches_json_reads_back_what_it_writes_and_refuses_anything_else()
    {
        var batches = Seven();
        var text = BatchesFile.Write(batches, Work);

        text.Should().StartWith("{\n  \"batches\": [\n    [\n      \"t1\",\n      \"t2\",", "two-space indent, \\n line ends, the batches first");
        text.Should().Contain("\"t1\": {\n      \"files\": [\n        \"src/F1.cs\"\n      ],\n      \"brief\": \"/x/arm/work/t1.md\",\n      \"depends\": [],\n      \"scenarios\": [\n        \"S1\"\n      ]\n    },");
        text.Should().EndWith("\n    }\n  }\n}");
        text.Should().NotEndWith("\n", "JSON.stringify writes no trailing newline");
        BatchesFile.Read(text, Work).Ok().Should().BeEquivalentTo(batches, options => options.WithStrictOrdering(), "the unedited text is read back whole");

        Refused(node => node["batches"]!.AsArray()[0]!.AsArray().Add("t9")).Should().Contain("t9");
        Refused(node => node["batches"]!.AsArray().RemoveAt(2)).Should().Contain("t7").And.Contain("no batch");
        Refused(node => node["tasks"]!["t1"]!["brief"] = "/elsewhere/t1.md").Should().Contain("t1").And.Contain("/x/arm/work");
        Refused(node => node["tasks"]!["t2"]!["brief"] = "/x/arm/work/sub/t2.md").Should().Contain("t2").And.Contain("one file name");
        Refused(node => node.Remove("tasks")).Should().Contain("tasks");
        Refused(node => node.Remove("batches")).Should().Contain("batches");
        Refused(node => node["batches"]!.AsArray()[1]!.AsArray().Add("t1")).Should().Contain("t1").And.Contain("twice");
        Refused(node => node["batches"]!.AsArray().Add(new JsonArray())).Should().Contain("empty batch");
        Refused(node => node["tasks"]!["t3"]!.AsObject().Remove("files")).Should().Contain("t3").And.Contain("files");
        Refused(node => node["tasks"]!["t3"]!["scenarios"] = "S3").Should().Contain("t3").And.Contain("scenarios");
        Refused(node => ((JsonArray)node["tasks"]!["t3"]!["depends"]!).Add("t9")).Should().Contain("t3").And.Contain("t9");
        Refused(node => node["tasks"]!["t3"]!["extra"] = 1).Should().Contain("extra");
        Refused(node => node["extra"] = 1).Should().Contain("extra");
        BatchesFile.Read("{\"batches\": [", Work).Reason().Should().Contain("batches.json is not JSON");
        BatchesFile.Read("[]", Work).Reason().Should().Contain("not an object");
    }

    [Fact]
    public void The_files_field_is_each_file_in_backquotes_joined_by_a_comma()
    {
        var task = new PlannedTask("t1", ["src/A.cs", "src/B.cs"], "t1.md", [], []);

        TaskBriefFields.Files(task).Should().Be("`src/A.cs`, `src/B.cs`");
        TaskBriefFields.Files(task with { Files = ["only.cs"] }).Should().Be("`only.cs`");
    }
}
