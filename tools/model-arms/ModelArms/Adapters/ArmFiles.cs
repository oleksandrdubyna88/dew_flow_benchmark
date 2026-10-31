using System.Globalization;
using System.Text;
using Bench.Domain;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Scouts;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The arm's files, read and written (plan D5, D11) — the one place the step runner touches the disk. Every write goes beside its target and is moved over it: <see cref="WriteOnce"/> refuses an existing file (the
/// runtime's refusal of <c>File.Move(…, overwrite: false)</c>, through <see cref="ArtifactCommit.Stage"/>), <see cref="Replace"/> swaps. A folder is created the first time something is written under it. Attempts are counted by
/// NAME — the stream of attempt n is probed, no folder is enumerated (D11) — and the number of an attempt is reserved by its STREAM, never by its prompt (E2 §8 item 45).
/// <para>
/// Residual: a crash between the staged write and the rename leaves a file named <c>….staging-&lt;guid&gt;</c> that nothing reads (the private twin's is created <c>0600</c> too); a file system without an atomic rename is not supported.
/// </para></summary>
public sealed class ArmFiles(ArmPaths paths)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private const UnixFileMode PrivateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Creates the folders the arm writes under: <c>out</c>, <c>notes</c>, <c>work</c> and the CLI's <c>tmp</c>.</summary>
    public void Prepare()
    {
        foreach (var folder in new[] { paths.Out, paths.Notes, paths.Work, paths.Tmp })
        {
            Directory.CreateDirectory(folder);
        }
    }

    public bool Exists(string path) => File.Exists(path);

    /// <summary>The smallest n ≥ 1 whose stream does not exist.</summary>
    public int NextAttempt(string step)
    {
        var n = 1;
        while (File.Exists(paths.Stream(step, n)))
        {
            n++;
        }

        return n;
    }

    public int LastAttempt(string step) => NextAttempt(step) - 1;

    /// <summary>The session id <c>.session</c> holds; a failure when there is none or it is not a guid.</summary>
    public Outcome<Guid> ReadSession(string step) =>
        ReadText(paths.Session(step)).Match(
            text => Guid.TryParse(text.Trim(), CultureInfo.InvariantCulture, out var id) ? Outcome<Guid>.Success(id) : Outcome<Guid>.Failure($"{Path.GetFileName(paths.Session(step))} does not hold a session id"),
            Outcome<Guid>.Failure);

    /// <summary>The id and a newline, as the shell's <c>echo "$sid"</c> wrote it — swapped in whole, the one file rewritten when a fresh id replaces a lost transcript's.</summary>
    public void ReplaceSession(string step, Guid session) => Replace(paths.Session(step), session.ToString("D", CultureInfo.InvariantCulture) + "\n");

    /// <summary>The stream (or stderr) file of an attempt, created NEW and shared for reading: each line is flushed as it is written.</summary>
    public ScoutStreamWriter Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new ScoutStreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
    }

    /// <summary>A file that is written once: <c>.before</c>, <c>.done</c>, the verdict file, <c>.diff</c> — never the prompt. An existing file is an <see cref="IOException"/>.</summary>
    public void WriteOnce(string path, string text) => WriteFileOnce(path, text);

    /// <summary><see cref="WriteOnce"/> for a file that is not under an arm — the root's own <c>baseline.json</c>: staged beside the name and renamed, an existing file (or link) at the name refused with an <see cref="IOException"/>.</summary>
    public static void WriteFileOnce(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            ArtifactCommit.Stage(path, Utf8.GetBytes(text));
        }
        catch (IOException)
        {
            RemoveStaging(path);
            throw;
        }
    }

    /// <summary>A refused write leaves the file it staged beside the target; it is removed before the refusal travels on.</summary>
    private static void RemoveStaging(string path)
    {
        foreach (var staged in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ArtifactCommit.StagingMarker + "*"))
        {
            File.Delete(staged);
        }
    }

    /// <summary>Stages beside the target, flushes to disk and renames over it.</summary>
    public void Replace(string path, string text) => ReplaceFile(path, text);

    /// <summary><see cref="Replace"/> without an instance (E5.S1): the pid file of a runner, written BY NAME — staged beside, flushed, renamed over — and never through <see cref="WriteFileOnce"/>, whose failure path lists the folder (<c>pids/</c> is at mode 300 while the isolation stands).</summary>
    public static void ReplaceFile(string path, string text) => Commit(path, Utf8.GetBytes(text), createMode: null, betweenStagingAndRename: null);

    /// <summary><see cref="Replace"/> for a file that carries a secret (<c>mcp-coai.json</c> with the vault key, the copied coai settings, the arm map — C15, E2 §8 item 14): the staged file is CREATED at mode <c>0600</c> (<see cref="FileStreamOptions.UnixCreateMode"/>), so no moment exists in which it is readable by other users with the secret already in it — a
    /// create-then-chmod has that moment, and the test looks at the seam between the staged write and the rename. The rename swaps a wider file that was there for this private one: never widened, never kept. A failed write or rename removes the staged file (it holds the secret) and its exception carries the runtime's words and the paths, never the bytes. The mode is a REQUEST to the kernel that a umask can only narrow; whether a file system honours modes is its own affair (<c>ArmFilesTests</c> observes it on the host's).</summary>
    public static void ReplacePrivate(string path, string text) => Commit(path, Utf8.GetBytes(text), PrivateMode, betweenStagingAndRename: null);

    /// <summary><see cref="ReplacePrivate(string, string)"/> for bytes that are not ours to re-encode: the coai settings are copied as they are, byte for byte.</summary>
    public static void ReplacePrivate(string path, byte[] bytes) => Commit(path, bytes, PrivateMode, betweenStagingAndRename: null);

    /// <summary>The same with a seam that runs once, after the staged file is written and before the rename, handed the staged file's path: the only place a test can see the mode a file has while it already holds the secret.</summary>
    internal static void ReplacePrivate(string path, string text, Action<string>? betweenStagingAndRename) => Commit(path, Utf8.GetBytes(text), PrivateMode, betweenStagingAndRename);

    private static void Commit(string path, byte[] bytes, UnixFileMode? createMode, Action<string>? betweenStagingAndRename)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + ArtifactCommit.StagingMarker + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 4096, Options = FileOptions.WriteThrough };
            if (createMode is { } mode && !OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = mode;
            }

            using (var stream = new FileStream(staging, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            betweenStagingAndRename?.Invoke(staging);
            File.Move(staging, path, overwrite: true);
        }
        catch
        {
            RemoveStaged(staging);
            throw;
        }
    }

    /// <summary>Best effort, on the way out of a failure that travels on: a staged file that cannot be removed is no reason to hide the failure that matters.</summary>
    private static void RemoveStaged(string staging)
    {
        try
        {
            File.Delete(staging);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The original failure is the one the caller needs; a leftover named ….staging-<guid> is read by nothing (the class's residual).
        }
    }

    /// <summary>The prompt of attempt <paramref name="n"/>. A prompt is immutable once its attempt has started: when the stream exists this throws (a caller that tries is wrong). Otherwise the prompt is replaced — a
    /// prompt without a stream belongs to a launch that never happened (E2 §8 item 45) — and the answer says whether one was already there.</summary>
    public bool WritePrompt(string step, int n, string text)
    {
        if (File.Exists(paths.Stream(step, n)))
        {
            throw new InvalidOperationException($"{Path.GetFileName(paths.Prompt(step, n))} cannot be written: the stream of attempt {n} exists, and a prompt is immutable once its attempt has started");
        }

        var existed = File.Exists(paths.Prompt(step, n));
        Replace(paths.Prompt(step, n), text);
        return existed;
    }

    public void Delete(string path) => File.Delete(path);

    /// <summary>One line and a newline, flushed to disk.</summary>
    public void AppendTiming(TimingLine line)
    {
        Directory.CreateDirectory(paths.Out);
        using var stream = new FileStream(paths.Timing, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(Utf8.GetBytes(TimingLines.Write(line) + "\n"));
        stream.Flush(flushToDisk: true);
    }

    public IReadOnlyList<TimingLine> ReadTiming() => ReadText(paths.Timing).Match(TimingLines.Read, _ => []);

    /// <summary>The whole file as UTF-8, read while a writer may still hold it; a failure when it is absent or cannot be read.</summary>
    public Outcome<string> ReadText(string path)
    {
        if (!File.Exists(path))
        {
            return Outcome<string>.Failure($"{Path.GetFileName(path)} does not exist");
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Utf8);
            return Outcome<string>.Success(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"{Path.GetFileName(path)} cannot be read: {ex.Message}");
        }
    }

    /// <summary>The last <paramref name="characters"/> of the file; <c>""</c> when it is absent or unreadable.</summary>
    public string Tail(string path, int characters) =>
        ReadText(path).Match(text => text.Length <= characters ? text : text[^characters..], _ => string.Empty);

    public DateTimeOffset ModifiedUtc(string path) => new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
}
