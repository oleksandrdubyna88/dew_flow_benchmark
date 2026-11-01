using System.Text;
using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Adapters;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;

namespace ModelArms.Verbs;

/// <summary>What git said of the arm's clone: its short head, the commits and the diff since <c>base</c>, the number of uncommitted files, and which scenario each test glob names.</summary>
internal sealed record GitFacts(string HeadShort, long Commits, int Uncommitted, string Numstat, string Shortstat, IReadOnlyDictionary<string, bool> Present);

/// <summary>The reading half of <c>checks</c> (plan §4.9, E3.S5 steps 6–9): the git facts through <see cref="ArmGit"/> (which builds its own reduced environment and keeps hooks and filter drivers from running), the CTRF and the done markers read through <see cref="BoundedFile"/>, the facts assembled for <see cref="ObjectiveReport"/>, and
/// the report published — <c>out/objective.json</c>, then the summary on stdout and in <c>out/objective.out</c>, then the marker LAST (E3 §8 item 6), so a marker always means a report.
/// <para>
/// What this does not do: judge whether the numbers are true. The CTRF is the arm's suite's own output; a total is the FIRST <c>total:</c> anywhere in the log (E3 §8 item 27 e); a submodule's uncommitted edit is not counted (item 40).
/// </para></summary>
internal static class ChecksReport
{
    private const string Name = "model-arms checks";
    private const int BatchesLimit = 1024 * 1024;
    private const int CtrfLimit = 64 * 1024 * 1024;
    private const int MarkerLimit = 4 * 1024;

    /// <summary>Step 6: the questions of the report, all asked (the scenarios' questions stop at the first git cannot answer), then the FIRST failure in the order head, uncommitted, commits, numstat, shortstat, scenarios is the sentence — the verb's exit 3.</summary>
    public static async Task<Outcome<GitFacts>> GitAsync(ChecksPlan plan, ArmGit git, CancellationToken ct)
    {
        var baseSha = plan.Input.Experiment.Target.Base.Value;
        var head = await git.HeadShortAsync(ct);
        var uncommitted = await git.UncommittedCountAsync(ct);
        var commits = await git.CountSinceAsync(baseSha, ct);
        var numstat = await git.NumstatAsync(baseSha, ct);
        var shortstat = await git.ShortstatAsync(baseSha, ct);
        var present = await PresentAsync(plan, git, ct);
        var unanswered = new[] { head.Reason(), uncommitted.Reason(), commits.Reason(), numstat.Reason(), shortstat.Reason(), present.Reason() }.FirstOrDefault(reason => reason.Length > 0);
        return unanswered is not null
            ? Outcome<GitFacts>.Failure($"git cannot answer: {unanswered}")
            : Outcome<GitFacts>.Success(new GitFacts(Value(head), Value(commits), Value(uncommitted), Value(numstat), Value(shortstat), Value(present)));
    }

    /// <summary>Whether the target's tests name each scenario (<c>git grep -l -F -e &lt;name&gt; -- &lt;testGlobs&gt;</c>), keyed by id; the first question git cannot answer ends the asking.</summary>
    private static async Task<Outcome<IReadOnlyDictionary<string, bool>>> PresentAsync(ChecksPlan plan, ArmGit git, CancellationToken ct)
    {
        var present = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var scenario in plan.Scenarios)
        {
            var mentions = await git.MentionsAsync(scenario.Name, plan.Input.Experiment.Target.TestGlobs, ct);
            if (mentions is not Outcome<bool>.Ok { Value: var named })
            {
                return Outcome<IReadOnlyDictionary<string, bool>>.Failure($"whether the tests name scenario {scenario.Id}: {mentions.Reason()}");
            }

            present[scenario.Id] = named;
        }

        return Outcome<IReadOnlyDictionary<string, bool>>.Success(present);
    }

    /// <summary>Step 7's CTRF: <c>out/objective/&lt;ctrfFile&gt;</c> through the bounded road (64 MiB — the real one is 11.7 MB), as text; why it cannot be read otherwise — absent, a link, a FIFO, past the limit, not a CTRF object. (The name is a plain file name: the experiment reader refuses a separator or a climbing segment in <c>suite.ctrfFile</c>.) The caller reads a failure as no tests and says why.</summary>
    public static Outcome<string> Ctrf(ChecksPlan plan)
    {
        var path = Path.Combine(plan.Paths.ObjectiveFolder, plan.Input.Experiment.Target.Suite.CtrfFile);
        return BoundedFile.Read(path, CtrfLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Bytes bytes => Parsed(Encoding.UTF8.GetString(bytes.Value)),
            BoundedRead.Refused refused => Outcome<string>.Failure(refused.Reason),
            _ => Outcome<string>.Failure($"{path} was not written"),
        };
    }

    /// <summary>The text of one of the four logs by its path through the bounded road — <c>--parse-only</c> only; a full run reads the logs back from the handles it made them with. Absent, refused or past the limit is the empty text and a line saying why.</summary>
    public static string LogByPath(ChecksPlan plan, string path)
    {
        var read = BoundedFile.Read(path, CtrfLimit, PlatformFactsReader.CurrentRoad);
        if (read is BoundedRead.Bytes bytes)
        {
            return Encoding.UTF8.GetString(bytes.Value);
        }

        plan.Warn($"{Path.GetFileName(path)} is not read — {(read is BoundedRead.Refused refused ? refused.Reason : $"{path} is absent")} — read as empty");
        return string.Empty;
    }

    /// <summary>A log read back from a held handle: its text, or the empty text and a line saying why (past 64 MiB).</summary>
    public static string LogText(ChecksPlan plan, string name, Outcome<string> text)
    {
        if (text is Outcome<string>.Fail failed)
        {
            plan.Warn($"{name} is not read — {failed.Reason} — read as empty");
        }

        return text is Outcome<string>.Ok ok ? ok.Value : string.Empty;
    }

    /// <summary>The steps whose <c>.done</c> says <c>hard-deadline</c> (step 8): each marker by name through the bounded road; one that is absent or refused is not on its deadline. A scheme with a <c>tasks</c> entry also asks for each task coder of <c>work/batches.json</c> (E4.S3): a file that is absent or does not read names no coder, and one line says so.</summary>
    public static IReadOnlyList<string> StepsOnDeadline(ChecksPlan plan) =>
        DeadlineSteps.Of(plan.Scheme, TaskIdsOf(plan), stem => Done(plan.Paths.Done(stem)));

    private static IReadOnlyList<string> TaskIdsOf(ChecksPlan plan)
    {
        if (!plan.Scheme.Entries.Any(entry => entry is SchemeEntry.Tasks))
        {
            return [];
        }

        var read = BoundedFile.Read(plan.Paths.BatchesJson, BatchesLimit, PlatformFactsReader.CurrentRoad);
        var batches = read is BoundedRead.Bytes bytes ? BatchesFile.Read(Encoding.UTF8.GetString(bytes.Value), plan.Paths.Work) : Outcome<TaskBatches>.Failure(read is BoundedRead.Refused refused ? refused.Reason : "work/batches.json is absent");
        return batches.Match(
            found => found.TaskIds,
            reason =>
            {
                plan.Warn($"the task coders are not checked for a deadline — {ExecutableRule.Printable(reason)}");
                return [];
            });
    }

    /// <summary>Step 9: the report, the summary, the marker. A failed write is the verb's exit 3 — nothing after it is written, so a marker never stands for a report that is not there.</summary>
    public static Passage<int> Publish(ChecksPlan plan, ObjectiveFacts facts, ChecksSeams seams)
    {
        try
        {
            var files = new ArmFiles(plan.Paths);
            var summary = plan.Map.Scrub(ObjectiveReport.Summary(facts));
            files.Replace(plan.Paths.Objective, ObjectiveReport.Write(facts));
            files.Replace(plan.Paths.ObjectiveLog, summary + "\n");
            plan.Input.Verb.Output.WriteLine(summary);
            seams.AfterReport?.Invoke();
            files.Replace(plan.Input.Root.ObjectiveMarker(plan.Arm), string.Empty);
            return new Passage<int>.Through(0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<int>.Stop(3, $"{Name} {plan.Arm}: the report cannot be written: {ex.Message}");
        }
    }

    /// <summary>The facts <see cref="ObjectiveReport"/> writes from — what the stages said, what the logs and the CTRF hold, what git said, the scenarios and the baseline.</summary>
    public static ObjectiveFacts Facts(ChecksPlan plan, StageRuns stages, GitFacts git, string suiteLog, string extensionLog) =>
        new(
            plan.Arm,
            git.HeadShort,
            CapturedCount.Number(git.Commits),
            git.Uncommitted,
            StageRecord.Exits(stages),
            suiteLog,
            CtrfSaid(plan),
            extensionLog,
            plan.Scenarios,
            git.Present,
            plan.Baseline,
            plan.Input.Experiment.Target.Suite.NamePrefix,
            git.Numstat,
            git.Shortstat,
            StepsOnDeadline(plan));

    private static Outcome<string> CtrfSaid(ChecksPlan plan)
    {
        var ctrf = Ctrf(plan);
        if (ctrf is Outcome<string>.Fail failed)
        {
            plan.Warn($"the CTRF {plan.Input.Experiment.Target.Suite.CtrfFile} is not read — {failed.Reason} — read as no tests");
        }

        return ctrf;
    }

    private static Outcome<string> Parsed(string text) =>
        CtrfScenarios.Tests(text).Match(_ => Outcome<string>.Success(text), reason => Outcome<string>.Failure(reason));

    private static Read<DoneMarker> Done(string path) =>
        BoundedFile.Read(path, MarkerLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Bytes bytes => DoneMarkers.Read(Encoding.UTF8.GetString(bytes.Value)).Match<Read<DoneMarker>>(marker => new Read<DoneMarker>.Present(marker), reason => new Read<DoneMarker>.Refused($"{path}: {reason}")),
            BoundedRead.Refused refused => new Read<DoneMarker>.Refused(refused.Reason),
            _ => new Read<DoneMarker>.Absent(),
        };

    private static T Value<T>(Outcome<T> outcome) => ((Outcome<T>.Ok)outcome).Value;
}
