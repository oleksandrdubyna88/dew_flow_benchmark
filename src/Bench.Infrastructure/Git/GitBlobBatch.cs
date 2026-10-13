using System.Globalization;
using System.Text;
using Bench.Domain;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Git;

/// <summary>One blob git holds: the path it sits at and its object id.</summary>
public sealed record GitBlobRef(string Path, string ObjectId);

/// <summary>Several blobs read from ONE <c>git cat-file --batch</c> (E2 code round): one <c>git show</c> per staged file was a process launch
/// each — a push of a few hundred files took minutes — and the batch answers them in order on one pipe. Each object's bytes are handed to the
/// caller with its place in the list AS THEY ARRIVE, never the whole batch in memory; an object git reports <c>missing</c>, a stream that ends
/// early, or an id that is not one refuses the whole read. Stdout is read as bytes (<see cref="ProcessRunner.StreamAsync"/>): a blob is a
/// payload a text decode would change.</summary>
public static class GitBlobBatch
{
    private const int BufferBytes = 64 * 1024;

    public static async Task<Outcome<IReadOnlyList<T>>> ReadAsync<T>(
        string repository, IReadOnlyList<string> objectIds, TimeSpan timeout, Func<int, ReadOnlyMemory<byte>, T> each, CancellationToken cancellationToken)
    {
        if (objectIds.FirstOrDefault(id => !IsObjectId(id)) is { } odd)
        {
            return Outcome<IReadOnlyList<T>>.Failure($"'{odd}' is not an object id — 40 or 64 hex characters");
        }

        if (objectIds.Count == 0)
        {
            return Outcome<IReadOnlyList<T>>.Success([]);
        }

        var reader = new BatchReader<T>(objectIds, each);
        var attempt = await ProcessRunner.StreamAsync(
            "git", ["cat-file", "--batch"], repository, timeout, string.Concat(objectIds.Select(id => id + "\n")), (stdout, _) => reader.ReadAsync(stdout), cancellationToken);

        return (attempt, reader.Refusal) switch
        {
            (_, { Length: > 0 } refused) => Outcome<IReadOnlyList<T>>.Failure(refused),
            (ProcessAttempt.Completed { Result.Ok: true }, _) => Outcome<IReadOnlyList<T>>.Success(reader.Results),
            (ProcessAttempt.Completed c, _) => Outcome<IReadOnlyList<T>>.Failure($"git cat-file exited {c.Result.ExitCode}: {c.Result.Output}"),
            _ => Outcome<IReadOnlyList<T>>.Failure($"git cat-file {attempt.Describe}"),
        };
    }

    private static bool IsObjectId(string id) => id.Length is 40 or 64 && id.All(char.IsAsciiHexDigit);

    /// <summary>The batch's stdout, object by object: a header line (<c>&lt;id&gt; &lt;type&gt; &lt;size&gt;</c>, or <c>&lt;id&gt; missing</c>), the
    /// bytes, one LF. Stops at the first refusal and drains the rest so the child can end on its own.</summary>
    private sealed class BatchReader<T>(IReadOnlyList<string> ids, Func<int, ReadOnlyMemory<byte>, T> each)
    {
        private const string Missing = "missing";

        private readonly List<T> _results = new(ids.Count);
        private readonly byte[] _one = new byte[1];

        public IReadOnlyList<T> Results => _results;

        public string Refusal { get; private set; } = string.Empty;

        public async Task ReadAsync(Stream stdout)
        {
            await using var buffered = new BufferedStream(stdout, BufferBytes);

            for (var at = 0; at < ids.Count && Refusal.Length == 0; at++)
            {
                Refusal = await ObjectAsync(buffered, at);
            }

            await buffered.CopyToAsync(Stream.Null);
        }

        private async Task<string> ObjectAsync(Stream stdout, int at)
        {
            try
            {
                var header = Header(await LineAsync(stdout));
                return await header.Match(size => BodyAsync(stdout, at, size), reason => Task.FromResult(Refused(at, reason)));
            }
            catch (EndOfStreamException)
            {
                return Refused(at, "git cat-file ended before the object was whole");
            }
        }

        private async Task<string> BodyAsync(Stream stdout, int at, int size)
        {
            var bytes = new byte[size];
            await stdout.ReadExactlyAsync(bytes);
            await stdout.ReadExactlyAsync(_one);
            _results.Add(each(at, bytes));
            return string.Empty;
        }

        private string Refused(int at, string reason) => string.Create(CultureInfo.InvariantCulture, $"object {at + 1} of {ids.Count} ({ids[at][..12]}): {reason}");

        private async Task<string> LineAsync(Stream stdout)
        {
            var line = new List<byte>(80);

            while (await stdout.ReadAsync(_one) == 1 && _one[0] != (byte)'\n')
            {
                line.Add(_one[0]);
            }

            return Encoding.ASCII.GetString([.. line]);
        }

        private static Outcome<int> Header(string line) =>
            line.Split(' ') switch
            {
                [""] => Outcome<int>.Failure("git cat-file ended before answering"),
                [_, Missing] => Outcome<int>.Failure("git has no such object (missing)"),
                [_, _, var size] => Size(size, line),
                _ => Outcome<int>.Failure($"git cat-file answered '{line}' where '<id> <type> <size>' was expected"),
            };

        private static Outcome<int> Size(string text, string line) =>
            long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size <= int.MaxValue
                ? Outcome<int>.Success((int)size)
                : Outcome<int>.Failure($"git cat-file answered '{line}' — not a size this scan reads whole");
    }
}
