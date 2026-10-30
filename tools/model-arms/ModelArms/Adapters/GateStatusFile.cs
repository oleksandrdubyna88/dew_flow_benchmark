namespace ModelArms.Adapters;

/// <summary>STUB (red first): the status file read the old way.</summary>
internal static class GateStatusFile
{
    public const int LimitBytes = 64 * 1024;

    public abstract record Answer
    {
        private Answer()
        {
        }

        public sealed record Absent : Answer;

        public sealed record Text(string Value) : Answer;

        public sealed record Refused(string Reason) : Answer;
    }

    public static Answer Read(string path) => File.Exists(path) ? new Answer.Text(File.ReadAllText(path)) : new Answer.Absent();

    internal static Answer ReadFallback(string path, int limit) => File.Exists(path) ? new Answer.Text(File.ReadAllText(path)) : new Answer.Text(string.Empty);
}
