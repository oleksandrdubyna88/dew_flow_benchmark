using System.Text.Json.Nodes;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Steps;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one run of <c>run-arm</c> left on the two writers.</summary>
internal sealed record ArmRun(int Code, string Output, string Error);

/// <summary>A writer that stops the run the moment a line it waits for is written: the test's way to be told that the gate is waiting for its lock, and then to be the operator who stops it.</summary>
internal sealed class StopOnLine(string needle, CancellationTokenSource stopping) : StringWriter
{
    public override void Write(string? value)
    {
        base.Write(value);
        if (value is not null && value.Contains(needle, StringComparison.Ordinal))
        {
            stopping.Cancel();
        }
    }

    public override void WriteLine(string? value)
    {
        base.WriteLine(value);
        if (value is not null && value.Contains(needle, StringComparison.Ordinal))
        {
            stopping.Cancel();
        }
    }
}

/// <summary>What the <c>run-arm</c> scenarios read and write, said once: the steps of each scheme by their file names, the shipped brief a prompt belongs to, an argv group, the arm's files and the timing.</summary>
internal static class RunArmKit
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The scheme's steps as the example names them, in order.</summary>
    public static IReadOnlyList<string> StepsOf(string scheme) =>
        scheme switch
        {
            "C" => ["1-code", "2-review", "3-fix", "4-gate"],
            "CR" => ["1-code", "2-hreview", "3-review", "4-fix", "5-gate"],
            "CHH" => ["1-code", "2-hA", "3-hB", "4-hfix", "5-review", "6-fix", "7-gate"],
            _ => throw new ArgumentException($"no scheme {scheme}", nameof(scheme)),
        };

    /// <summary><c>run-arm &lt;arm&gt; --experiment &lt;root&gt;</c> through the console's own entry point, both writers kept.</summary>
    public static async Task<ArmRun> RunAsync(ExperimentRoot root, string arm, CancellationToken? token = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["run-arm", arm, "--experiment", root.Root], output, error, root.Environment, token ?? Ct);
        return new ArmRun(code, output.ToString(), error.ToString());
    }

    /// <summary><c>run-arm</c> through the gate with the verb's seams handed in (a fixed clock, a recording wait, a busy lock) and the writer and stop token of the test's choosing.</summary>
    public static async Task<int> RunWithSeamsAsync(ExperimentRoot root, string arm, ModelArmsApp.Verbs.RunArmSeams seams, TextWriter output, CancellationToken stopping)
    {
        var input = new ModelArmsApp.VerbInput("run-arm", [arm], Bench.Cli.CommandLine.Parse(["--experiment", root.Root]), output, new StringWriter(), root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), stopping);
        return await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => ModelArmsApp.Verbs.RunArmVerb.RunAsync(admitted, seams), ModelArmsApp.GateSeams.Real);
    }

    /// <summary>The fake replays <paramref name="fixture"/> on every call, except the calls <paramref name="byCall"/> names; the key that makes it leave a transcript stays.</summary>
    public static void Replay(FakeCli fake, string fixture, params (int Call, string File)[] byCall)
    {
        var script = new JsonObject { ["stdoutFile"] = ModelArmsFixtures.Path(fixture) };
        if (byCall.Length > 0)
        {
            script["stdoutByCall"] = new JsonObject(byCall.Select(each => KeyValuePair.Create<string, JsonNode?>(each.Call.ToString(System.Globalization.CultureInfo.InvariantCulture), each.File)));
        }

        fake.Rewrite(ExperimentRoot.Script(script));
    }

    /// <summary>A copy of the recorded success whose last result text ends with <paramref name="verdictJson"/>.</summary>
    public static string SuccessEndingIn(ExperimentRoot root, string name, string verdictJson)
    {
        var escaped = verdictJson.Replace("\"", "\\\"", StringComparison.Ordinal);
        var text = ModelArmsFixtures.Read("result-success.jsonl").Replace("{\\\"verdict\\\":\\\"ready\\\",\\\"findings\\\":[]}", escaped, StringComparison.Ordinal);
        text.Should().Contain(escaped, "the copy must carry the verdict the test is about — a recorded stream that changed shape would pass this test for the wrong reason");
        var path = Path.Combine(root.Scratch("streams"), name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The first line of <paramref name="template"/> up to its first placeholder: what every prompt filled from it starts with.</summary>
    internal static string Opening(string template)
    {
        var first = File.ReadAllText(template).Split('\n')[0];
        var at = first.IndexOf("{{", StringComparison.Ordinal);
        return at < 0 ? first : first[..at];
    }

    /// <summary>The shipped brief a launch was filled from (its stem), or <c>resume</c> for a launch whose prompt is no brief — a resume notice.</summary>
    public static string BriefOf(FakeCliCall call)
    {
        var shipped = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs");
        var stems = Directory.EnumerateFiles(shipped, "*.template.md")
            .Select(file => (Stem: Path.GetFileName(file)[..^".template.md".Length], Opening: Opening(file)))
            .Where(each => call.Prompt.StartsWith(each.Opening, StringComparison.Ordinal))
            .Select(each => each.Stem)
            .ToList();
        return stems.Count == 1 ? stems[0] : stems.Count == 0 ? "resume" : throw new InvalidOperationException($"call {call.Call}'s prompt opens like {string.Join(" and ", stems)}");
    }

    /// <summary>How a call was started: <c>fresh</c> (<c>--session-id</c>) or <c>resumed</c> (<c>--resume</c>).</summary>
    public static string HowOf(FakeCliCall call) => call.Argv.Contains("--resume") ? "resumed" : "fresh";

    /// <summary>The call as a sentence a failure message can start with: <c>2-hreview resumed</c>. A resume's prompt is the resume notice, so its step is told by the session it continues.</summary>
    public static string Named(FakeCliCall call, IReadOnlyList<FakeCliCall> all)
    {
        var brief = BriefOf(call);
        if (brief != "resume")
        {
            return $"{brief} {HowOf(call)}";
        }

        var session = call.After("--resume");
        var first = all.First(other => other.Argv.Contains("--session-id") && other.After("--session-id") == session);
        return $"{BriefOf(first)} resumed";
    }

    /// <summary>The values after <paramref name="flag"/> up to the next flag.</summary>
    public static IReadOnlyList<string> Group(this FakeCliCall call, string flag)
    {
        var at = call.Argv.ToList().IndexOf(flag);
        at.Should().BeGreaterThanOrEqualTo(0, $"call {call.Call}'s argv should carry {flag}: {string.Join(' ', call.Argv)}");
        return [.. call.Argv.Skip(at + 1).TakeWhile(token => !token.StartsWith('-'))];
    }

    public static T As<T>(this TimingLine line) where T : TimingLine => line.Should().BeOfType<T>().Subject;

    public static string NotesText(this ExperimentRoot root, string arm, string relative) =>
        File.ReadAllText(Path.Combine(root.PathsOf(arm).Folder, relative));

    public static IReadOnlyList<TimingLine> TimingOf(this ExperimentRoot root, string arm)
    {
        var timing = root.PathsOf(arm).Timing;
        return File.Exists(timing) ? TimingLines.Read(File.ReadAllText(timing)) : [];
    }

    public static IReadOnlyList<TimingLine.ArmDone> ArmDones(this ExperimentRoot root, string arm) => [.. root.TimingOf(arm).OfType<TimingLine.ArmDone>()];
}
