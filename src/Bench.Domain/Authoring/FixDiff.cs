using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Domain.Authoring;

/// <summary>One file a reference fix touched, in the BASE tree's terms.</summary>
/// <param name="OldPath">The a-side path — the name the file has at <c>baseCommit</c>, which is the
/// tree the solver investigates. A rename anchors under its old name because only that name exists
/// there.</param>
/// <param name="OldSpans">The changed regions in OLD-side line numbers, one span per contiguous run of
/// change. Insertions anchor to the neighbouring old line — a diagnosis pointing at either side of the
/// seam has found the place.</param>
public sealed record FixDiffFile(
    string OldPath,
    bool IsNew,
    bool IsDeleted,
    bool IsTest,
    IReadOnlyList<LineSpan> OldSpans);

/// <summary>A reference fix's unified diff, read for what the harvest needs
/// (todo/PLAN_investigate_vs_implement.md §3.3, §3.5): where the defect was CAUSED, and which files are
/// the fix's own tests.
/// <para>
/// The ground truth for diagnosis scoring is DERIVED from this diff rather than authored — the same
/// principle as seed dates: the author names the change and the repository locates it, so there is no
/// second copy of the diff to drift. Spans are OLD-side coordinates on purpose: the solver reads the
/// tree at <c>baseCommit</c>, and the a-side numbering is that tree's.
/// </para></summary>
public sealed record FixDiff(IReadOnlyList<FixDiffFile> Files)
{
    /// <summary>The places a correct diagnosis points at: every changed span of every non-test file the
    /// base tree HAS. A file the fix created is no anchor at all — a diagnosis cannot point at a file
    /// that does not exist in the tree it investigated — and a file the fix deleted is a whole-file
    /// claim, since every line of it was the change.</summary>
    public IReadOnlyList<SourceAnchor> CausalAnchors(CommitSha baseCommit) =>
        [.. Files.Where(f => !f.IsTest && !f.IsNew).SelectMany(f => f.IsDeleted
            ? [SourceAnchor.File(f.OldPath, baseCommit)]
            : f.OldSpans.Select(span => SourceAnchor.Member(f.OldPath, string.Empty, span, baseCommit)))];

    /// <summary>The fix's own test files — its verification, not its cause, and the harvest's
    /// hidden-test candidates. A test file the fix CREATED counts: a regression test is usually new.</summary>
    public IReadOnlyList<string> TestFiles => [.. Files.Where(f => f.IsTest).Select(f => f.OldPath)];

    public static Outcome<FixDiff> Parse(string unifiedDiff)
    {
        var reader = new Reader();

        foreach (var line in (unifiedDiff ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            reader.Take(line);
        }

        var files = reader.Finish();

        return files.Count == 0
            ? Outcome<FixDiff>.Failure(
                "the text carries no unified diff — no file header ('diff --git' / '--- a/…') was found")
            : Outcome<FixDiff>.Success(new FixDiff(files));
    }

    /// <summary>The line-by-line walk, kept as a small mutable reader so <see cref="Parse"/> stays a
    /// statement of the format rather than a page of counters.</summary>
    private sealed class Reader
    {
        private readonly List<FixDiffFile> _files = [];
        private readonly List<int> _changed = [];
        private string _oldPath = string.Empty;
        private string _newPath = string.Empty;
        private bool _isNew;
        private bool _isDeleted;
        private bool _inFile;
        private bool _inHunk;
        private int _old;

        public void Take(string line)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                Close();
                _inFile = true;
                return;
            }

            // A 100%-similarity rename has NO ---/+++ headers — only these lines name the file, and
            // without them a renamed hidden test vanishes from the harvest entirely. Outside hunks
            // only: a changed body line spelling 'rename from' starts with '+'/'-'/' ', never bare.
            if (_inFile && !_inHunk && line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                _oldPath = RenamePath(line["rename from ".Length..]);
                return;
            }

            if (_inFile && !_inHunk && line.StartsWith("rename to ", StringComparison.Ordinal))
            {
                _newPath = RenamePath(line["rename to ".Length..]);
                return;
            }

            // Header order matters: '--- a/…' and '+++ b/…' must be read before the hunk branches
            // below would misread them as a deletion and an insertion.
            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                var path = HeaderPath(line[4..]);
                _isNew = path.Length == 0;
                _oldPath = path;
                _inHunk = false;
                return;
            }

            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                var path = HeaderPath(line[4..]);
                _isDeleted = path.Length == 0;
                _newPath = path;
                return;
            }

            if (line.StartsWith("@@ -", StringComparison.Ordinal))
            {
                _inHunk = int.TryParse(OldStart(line), out _old);
                return;
            }

            if (!_inHunk || line.Length == 0)
            {
                return;
            }

            switch (line[0])
            {
                case ' ':
                    _old++;
                    break;
                case '-':
                    _changed.Add(_old);
                    _old++;
                    break;
                case '+':
                    // An insertion sits between two old lines; anchoring it to the earlier neighbour
                    // keeps the span inside the file even when the insertion is at the very end.
                    _changed.Add(Math.Max(1, _old - 1));
                    break;
                default:
                    _inHunk = false; // '\ No newline at end of file', or the next file's metadata
                    break;
            }
        }

        public IReadOnlyList<FixDiffFile> Finish()
        {
            Close();
            return _files;
        }

        private void Close()
        {
            // A new file has no a-side name, so it is carried under its b-side one — CausalAnchors
            // excludes it either way, but the test classification needs the real name.
            var path = _oldPath.Length > 0 ? _oldPath : _newPath;

            if (_inFile && path.Length > 0)
            {
                _files.Add(new FixDiffFile(path, _isNew, _isDeleted, IsTestPath(path), Spans(_changed)));
            }

            _changed.Clear();
            _oldPath = string.Empty;
            _newPath = string.Empty;
            _isNew = false;
            _isDeleted = false;
            _inFile = false;
            _inHunk = false;
        }

        /// <summary>An a/b header's path: unquoted if git C-quoted it, prefix stripped, timestamp
        /// dropped, slashes normalised — empty for <c>/dev/null</c>, which is how "no such side" is
        /// spelt.</summary>
        private static string HeaderPath(string raw)
        {
            var token = raw.StartsWith('"') ? Unquoted(raw) : raw.Split('\t')[0].Trim();

            if (token == "/dev/null")
            {
                return string.Empty;
            }

            var stripped = token.StartsWith("a/", StringComparison.Ordinal)
                        || token.StartsWith("b/", StringComparison.Ordinal)
                ? token[2..]
                : token;

            return stripped.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>A rename header's path — no a/ b/ prefix to strip (git writes these bare, and a
        /// real directory named 'a' must survive), but the same quoting and normalisation.</summary>
        private static string RenamePath(string raw)
        {
            var token = raw.StartsWith('"') ? Unquoted(raw) : raw.Trim();

            return token.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>git C-quotes any path with a space, quote or non-ASCII byte; taken verbatim, the
        /// quotes and escapes become part of the path and no anchor ever matches it. Byte-wise, because
        /// the octal escapes are UTF-8 BYTES ('é' is \303\251), not characters.</summary>
        private static string Unquoted(string raw)
        {
            List<byte> bytes = [];
            var i = 1;

            while (i < raw.Length && raw[i] != '"')
            {
                i = raw[i] == '\\' ? Escaped(raw, i, bytes) : Plain(raw, i, bytes);
            }

            return System.Text.Encoding.UTF8.GetString([.. bytes]);
        }

        private static int Plain(string raw, int index, List<byte> bytes)
        {
            bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(raw[index..(index + 1)]));
            return index + 1;
        }

        private static int Escaped(string raw, int index, List<byte> bytes)
        {
            var next = index + 1 < raw.Length ? raw[index + 1] : '"';

            if (next is >= '0' and <= '7')
            {
                var digits = new string([.. raw[(index + 1)..].TakeWhile(c => c is >= '0' and <= '7').Take(3)]);
                bytes.Add((byte)Convert.ToInt32(digits, 8));
                return index + 1 + digits.Length;
            }

            bytes.Add(next switch { 't' => (byte)'\t', 'n' => (byte)'\n', 'r' => (byte)'\r', _ => (byte)next });
            return index + 2;
        }

        private static string OldStart(string hunkHeader)
        {
            var afterMinus = hunkHeader[4..];
            var end = afterMinus.IndexOfAny([',', ' ']);

            return end < 0 ? afterMinus : afterMinus[..end];
        }

        /// <summary>Contiguous changed old lines fold into one span; a gap becomes a new one. Two far
        /// hunks as one stretched claim would match a diagnosis pointing anywhere between them.</summary>
        private static IReadOnlyList<LineSpan> Spans(List<int> changed)
        {
            if (changed.Count == 0)
            {
                return [];
            }

            var ordered = changed.Distinct().OrderBy(l => l).ToList();
            List<LineSpan> spans = [];
            var start = ordered[0];
            var end = ordered[0];

            foreach (var line in ordered.Skip(1))
            {
                if (line <= end + 1)
                {
                    end = line;
                    continue;
                }

                spans.Add(new LineSpan(start, end));
                start = line;
                end = line;
            }

            spans.Add(new LineSpan(start, end));
            return spans;
        }

        /// <summary>A pragmatic classification, stated rather than hidden: a path segment named
        /// test/tests, or a file name ending Test(s).cs. What it misclassifies, a reviewer's mark can
        /// still correct — the harvest's candidates are vetted like everything else.</summary>
        private static bool IsTestPath(string path)
        {
            var segments = path.Split('/');
            var file = segments[^1];

            return segments.Any(s =>
                    s.Equals("test", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("tests", StringComparison.OrdinalIgnoreCase))
                || file.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase);
        }
    }
}
