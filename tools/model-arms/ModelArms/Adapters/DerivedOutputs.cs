using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The ONE writer of a step's derived outputs (plan D21, E2 §8 item 52), used by the first run and by every re-entry, and the reader of the facts <see cref="StepOutputs.Reconcile"/> decides over. A verdict file is written from the
/// LAST result of the step's final stream and from nothing else; a diff from the <c>.before</c> of the step <c>diffFrom</c> names to HEAD, through the step's <see cref="ArmGit"/>. An output whose input is missing is a FAILURE with the step's id and the
/// reason — never a silent skip and never a file: the runner turns it into the resumable stop (exit 5), and the step has no <c>.done</c>.
/// <para>
/// The task coder's head and diff are the task pipeline's (E4.S3): asked for here they are refused.
/// </para></summary>
public static class DerivedOutputs
{
    private const string Pipeline = "task outputs are written by the task pipeline (E4.S3)";

    /// <summary>What is on disk, as <see cref="StepOutputs.Reconcile"/> wants it.</summary>
    public static OutputFacts FactsOf(StepContext context, ArmFiles files) =>
        new(
            PresentOutputs(context, files),
            files.Exists(BeforeOf(context)),
            context.Task.Id.Length > 0 && files.Exists(context.Paths.TaskBefore(context.Task.Id)),
            ClaudeResultEnvelope.LastOf(FinalStream(context, files)));

    /// <summary>Writes <paramref name="outputs"/> in order; the first that cannot be written ends it with a failure.</summary>
    public static async Task<Outcome<IReadOnlyList<DerivedOutput>>> WriteAsync(StepContext context, IReadOnlyList<DerivedOutput> outputs, ArmFiles files, ArmGit git, CancellationToken ct)
    {
        var written = new List<DerivedOutput>();
        foreach (var output in outputs)
        {
            if (await WriteOneAsync(context, output, files, git, ct) is Outcome<string>.Fail failed)
            {
                return Outcome<IReadOnlyList<DerivedOutput>>.Failure(failed.Reason);
            }

            written.Add(output);
        }

        return Outcome<IReadOnlyList<DerivedOutput>>.Success(written);
    }

    private static Task<Outcome<string>> WriteOneAsync(StepContext context, DerivedOutput output, ArmFiles files, ArmGit git, CancellationToken ct) =>
        output switch
        {
            DerivedOutput.Verdict => Task.FromResult(WriteVerdict(context, files)),
            DerivedOutput.StepDiff => WriteDiffAsync(context, files, git, ct),
            _ => Task.FromResult(Outcome<string>.Failure(Pipeline)),
        };

    private static Outcome<string> WriteVerdict(StepContext context, ArmFiles files) =>
        ReviewAnswer.Extract(ClaudeResultEnvelope.LastOf(FinalStream(context, files))).Match(
            text => Put(files, VerdictFile(context), text),
            reason => Outcome<string>.Failure($"{context.Step.Name}: {reason}"));

    private static async Task<Outcome<string>> WriteDiffAsync(StepContext context, ArmFiles files, ArmGit git, CancellationToken ct)
    {
        var before = files.ReadText(BeforeOf(context));
        if (before is Outcome<string>.Fail unread)
        {
            return Outcome<string>.Failure($"{context.Step.Name}: {unread.Reason}");
        }

        return (await git.DiffAsync(((Outcome<string>.Ok)before).Value.Trim(), ct)).Match(
            diff => Put(files, context.Paths.Diff(context.Step.Name), diff),
            reason => Outcome<string>.Failure($"{context.Step.Name}: git could not make the diff from {context.Spec.DiffFrom}'s snapshot ({reason})"));
    }

    private static Outcome<string> Put(ArmFiles files, string path, string text)
    {
        files.WriteOnce(path, text);
        return Outcome<string>.Success(path);
    }

    private static HashSet<DerivedOutput> PresentOutputs(StepContext context, ArmFiles files) =>
        [.. StepOutputs.Of(context.Spec).Where(output => IsPresent(context, output, files))];

    private static bool IsPresent(StepContext context, DerivedOutput output, ArmFiles files) =>
        output switch
        {
            DerivedOutput.Verdict => files.Exists(VerdictFile(context)),
            DerivedOutput.StepDiff => files.Exists(context.Paths.Diff(context.Step.Name)),
            _ => false,
        };

    private static string VerdictFile(StepContext context) =>
        context.Paths.NotesFile(context.Spec.Outputs.First(binding => binding.Field == context.Spec.Verdict));

    /// <summary>The <c>.before</c> of the step <c>diffFrom</c> names: the id's entry in the scheme gives the file name. An id the scheme does not place has no such file.</summary>
    private static string BeforeOf(StepContext context) =>
        context.Scheme.Entries.OfType<SchemeEntry.Step>().FirstOrDefault(entry => entry.Id.Value == context.Spec.DiffFrom) is { } source
            ? context.Paths.Before(source.Name)
            : context.Paths.Before(context.Spec.DiffFrom.Length > 0 ? context.Spec.DiffFrom : context.Step.Name);

    private static string FinalStream(StepContext context, ArmFiles files) =>
        files.ReadText(context.Paths.Stream(context.Step.Name, files.LastAttempt(context.Step.Name))).Match(text => text, _ => string.Empty);
}
