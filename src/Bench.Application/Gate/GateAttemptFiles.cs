using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>What the attempt's session left in the files the harness did not write through the store: the plan the
/// product reviews, the ledger, the stderr, the session file, the tap's exchanges, the shim's prompt files. A port, so the
/// cell runner reads no file itself and a test can hand it a directory.</summary>
public interface IGateAttemptFiles
{
    /// <summary>The plan text at <paramref name="planPath"/> in the clone, or a refusal naming the path.</summary>
    Outcome<string> ReadPlan(string clone, string planPath);

    /// <summary>The size in bytes of a file, zero when it does not exist — where a shared data directory's ledger stood
    /// before this session began.</summary>
    long SizeOf(string path);

    bool Exists(string path);

    /// <summary>A file's text from <paramref name="offset"/> on, empty when it does not exist.</summary>
    string ReadFrom(string path, long offset);

    /// <summary><c>state.config</c> of the session keyed by this repo and branch in a data directory, or empty.</summary>
    string SessionConfig(string dataDir, string repoPath, string branch);

    /// <summary>Every exchange the tap recorded in a folder, in call order, with the file names that hold it.</summary>
    IReadOnlyList<(HttpCallFacts Facts, IReadOnlyList<string> Files)> TapCalls(string tapDirectory);

    /// <summary>The shim's prompt and answer files of the named prompts' folders, read: (a name for the copy, the bytes),
    /// oldest first — the other harness's <c>copy_answer_files</c>. A folder that is gone yields nothing.</summary>
    IReadOnlyList<(string Name, byte[] Bytes)> ShimFiles(IReadOnlyList<string> promptFiles);
}
