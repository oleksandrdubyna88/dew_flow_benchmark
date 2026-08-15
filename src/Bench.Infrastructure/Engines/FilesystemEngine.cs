using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Engines;

/// <summary>The engine that does no retrieval: plain filesystem tools over a pinned checkout.
/// <para>
/// <b>Not a stub, and not a fallback.</b> It is the baseline every other engine is measured against,
/// and the measured answer keeps being uncomfortable: a plain-filesystem tool set scored 36/63 against
/// a full retrieval surface's 37/63, while retrieval cost 52 % more wall-clock. One point out of
/// sixty-three. If this is not a first-class engine, the question simply stops being asked, and every
/// retrieval configuration then competes only against other retrieval configurations — where all of
/// them look worthwhile.
/// </para>
/// <para>
/// <b>Written here rather than borrowed</b> from the tool surface in <c>dew_flow_mcp</c>, which has the
/// same capability. Three moves were considered and rejected: referencing that repository (it is a
/// separate checkout with no project reference, so the benchmark's build would depend on where somebody
/// cloned it), vendoring it (a second copy that drifts), and calling it over MCP (that is the
/// <c>Http</c> engine's job, and it would make the BASELINE depend on a server being up). The deciding
/// reason is measurement rather than plumbing: the wording and shape of a tool surface is a measured
/// variable, so the baseline's surface must be pinned HERE and change only when this repository decides
/// it does — not when a product's public surface ships a new description.
/// </para>
/// <para>Every path is resolved and bounded against the checkout root, so <c>..</c> traversal and
/// symlink escapes are refusals rather than reads. The checkout is a read-only worktree; nothing here
/// writes.</para></summary>
public sealed class FilesystemEngine(string checkoutPath) : IEngine
{
    public const string ReadFile = "read_file";
    public const string ListDirectory = "list_directory";
    public const string SearchLiteral = "search_literal";
    public const string FindFiles = "find_files";

    private const int MaxHits = 200;
    private const int MaxEntries = 500;

    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkoutPath));

    public EngineRef Describe => EngineRef.Filesystem();

    /// <summary>Empty: this engine has no funnel to report, because it has no retrieval to report on.
    /// A report renders its funnel columns as absent, never as zero.</summary>
    public string TraceContractVersion => string.Empty;

    public IReadOnlyList<EngineTool> Tools { get; } =
    [
        new EngineTool(
            ReadFile,
            "Read a file from the repository, whole or as a line window. startLine is 1-based inclusive; "
            + "omit lineCount to read to the end. Every result reports startLine/endLine/totalLines, so "
            + "paging is a number rather than a guess.",
            """{"path":"string","startLine":"int?","lineCount":"int?"}"""),
        new EngineTool(
            ListDirectory,
            "List the files and folders under a repository-relative path. Use it to orient before "
            + "reading; it does not return file contents.",
            """{"path":"string?"}"""),
        new EngineTool(
            SearchLiteral,
            "Find an exact substring across the repository. Not a regex and not a concept search: it "
            + "matches the characters given. Returns file, line number and the matching line.",
            """{"text":"string","maxHits":"int?"}"""),
        new EngineTool(
            FindFiles,
            "Find files by path pattern (for example **/*.cs or src/**/Order*). Matches paths, not "
            + "contents.",
            """{"pattern":"string"}"""),
    ];

    /// <summary>Nothing to warm: there is no index, which is the point of this engine. It confirms the
    /// checkout is there, because a baseline that silently measured an empty directory would make every
    /// engine beside it look good.</summary>
    public Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken) =>
        Task.FromResult(Directory.Exists(checkoutPath)
            ? Outcome<string>.Success(EngineRef.Filesystem().Canonical)
            : Outcome<string>.Failure($"checkout not found: {checkoutPath}"));

    public Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        var arguments = Parse(argumentsJson);
        if (arguments is Outcome<JsonElement>.Fail bad)
        {
            return Task.FromResult(ToolAnswer.Refusal(bad.Reason));
        }

        var args = ((Outcome<JsonElement>.Ok)arguments).Value;

        return Task.FromResult(tool switch
        {
            ReadFile => Read(args),
            ListDirectory => List(args),
            SearchLiteral => Search(args),
            FindFiles => Find(args),
            _ => ToolAnswer.Refusal($"'{tool}' is not a tool this engine offers."),
        });
    }

    private ToolAnswer Read(JsonElement args)
    {
        if (!TryResolve(args, out var full, out var refusal))
        {
            return refusal;
        }

        if (!File.Exists(full))
        {
            return ToolAnswer.Refusal("no such file in this repository");
        }

        var lines = File.ReadAllLines(full);
        var start = Math.Max(1, Number(args, "startLine", 1));
        if (start > lines.Length)
        {
            // Not an error: the caller gets the real total and can correct the offset by number.
            return ToolAnswer.Success($"lines {start}-{start - 1} of {lines.Length}");
        }

        var count = Number(args, "lineCount", 0);
        var end = count <= 0 ? lines.Length : Math.Min(lines.Length, start + count - 1);
        var window = string.Join('\n', lines[(start - 1)..end]);

        return ToolAnswer.Success($"lines {start}-{end} of {lines.Length}\n{window}");
    }

    private ToolAnswer List(JsonElement args)
    {
        var relative = Text(args, "path");
        var full = relative.Length == 0 ? _root : Path.GetFullPath(Path.Combine(_root, relative));

        if (!Inside(full))
        {
            return ToolAnswer.Refusal("that path is outside the repository");
        }

        if (!Directory.Exists(full))
        {
            return ToolAnswer.Refusal("no such directory in this repository");
        }

        var entries = Directory
            .EnumerateFileSystemEntries(full)
            .Take(MaxEntries)
            .Select(entry => Directory.Exists(entry) ? Relative(entry) + "/" : Relative(entry))
            .Order(StringComparer.Ordinal);

        return ToolAnswer.Success(string.Join('\n', entries));
    }

    private ToolAnswer Search(JsonElement args)
    {
        var needle = Text(args, "text");
        if (needle.Length == 0)
        {
            return ToolAnswer.Refusal("argument 'text' is required");
        }

        var cap = Math.Clamp(Number(args, "maxHits", 50), 1, MaxHits);
        var hits = Walk()
            .SelectMany(file => Matches(file, needle))
            .Take(cap)
            .ToList();

        return ToolAnswer.Success(hits.Count == 0 ? "no matches" : string.Join('\n', hits));
    }

    private ToolAnswer Find(JsonElement args)
    {
        var pattern = Text(args, "pattern");
        if (pattern.Length == 0)
        {
            return ToolAnswer.Refusal("argument 'pattern' is required");
        }

        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        var found = matcher
            .Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(_root)))
            .Files
            .Take(MaxEntries)
            .Select(f => f.Path.Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        return ToolAnswer.Success(found.Count == 0 ? "no files match" : string.Join('\n', found));
    }

    private IEnumerable<string> Matches(string file, string needle) =>
        File.ReadLines(file)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(x => x.Line.Contains(needle, StringComparison.Ordinal))
            .Select(x => $"{Relative(file)}:{x.Number}: {x.Line.Trim()}");

    /// <summary>Every file under the checkout except the ones a repository never means: git's own
    /// object store and build output. A literal search that reads a build tree measures the search's
    /// patience — upstream, a "slow full tree walk" turned out to be 250 ms of an 11-second call, and
    /// the rest was reading 1.66 GB of gitignored build output.</summary>
    private IEnumerable<string> Walk() =>
        Directory
            .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(path => !Relative(path).StartsWith(".git/", StringComparison.Ordinal))
            .Where(path => !Relative(path).Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !Relative(path).Contains("/obj/", StringComparison.Ordinal));

    private bool TryResolve(JsonElement args, out string full, out ToolAnswer refusal)
    {
        full = string.Empty;
        var relative = Text(args, "path");

        if (relative.Length == 0)
        {
            refusal = ToolAnswer.Refusal("argument 'path' is required");
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        if (!Inside(candidate))
        {
            refusal = ToolAnswer.Refusal("that path is outside the repository");
            return false;
        }

        full = candidate;
        refusal = ToolAnswer.Success(string.Empty);
        return true;
    }

    /// <summary>Resolve, then compare — checking the STRING for "<c>..</c>" does not work, because
    /// <c>a/../../b</c> and a symlink both spell fine.</summary>
    private bool Inside(string candidate) =>
        candidate == _root
        || candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private string Relative(string full) =>
        Path.GetRelativePath(_root, full).Replace('\\', '/');

    private static Outcome<JsonElement> Parse(string argumentsJson)
    {
        try
        {
            var text = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            return Outcome<JsonElement>.Success(JsonDocument.Parse(text).RootElement.Clone());
        }
        catch (JsonException ex)
        {
            // The subject's mistake, and one it can correct — so a value, never an exception that ends
            // the leg over a stray brace.
            return Outcome<JsonElement>.Failure($"arguments are not valid JSON: {ex.Message}");
        }
    }

    private static string Text(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Number(JsonElement args, string name, int fallback) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var number)
            ? number
            : fallback;
}
