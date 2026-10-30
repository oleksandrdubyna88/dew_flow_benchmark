using Bench.Domain;
using Bench.Infrastructure.Scouts;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The arm's files, read and written (plan D5, D11). STUB.</summary>
public sealed class ArmFiles(ArmPaths paths)
{
    private readonly ArmPaths _paths = paths;

    public void Prepare()
    {
    }

    public bool Exists(string path) => false;

    public int NextAttempt(string step) => 1;

    public int LastAttempt(string step) => 0;

    public Outcome<Guid> ReadSession(string step) => Outcome<Guid>.Failure("stub");

    public void ReplaceSession(string step, Guid session)
    {
    }

    public ScoutStreamWriter Open(string path) => new(new FileStream("/dev/null", FileMode.Open, FileAccess.Write));

    public void WriteOnce(string path, string text)
    {
    }

    public void Replace(string path, string text)
    {
    }

    public bool WritePrompt(string step, int n, string text) => false;

    public void Delete(string path)
    {
    }

    public void AppendTiming(TimingLine line)
    {
    }

    public IReadOnlyList<TimingLine> ReadTiming() => [];

    public Outcome<string> ReadText(string path) => Outcome<string>.Failure("stub");

    public string Tail(string path, int characters) => string.Empty;

    public DateTimeOffset ModifiedUtc(string path) => DateTimeOffset.MinValue;
}
