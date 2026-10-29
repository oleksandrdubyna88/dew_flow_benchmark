using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>What <see cref="ExecutableResolution.Resolve"/> answered. Three states, closed: the exit code is decided on the state, never on the words of a
/// sentence (a reason that happens to mention D17 is still the host's lack if it is <see cref="Absent"/>).</summary>
public abstract record ExecutableAnswer
{
    private ExecutableAnswer() { }

    /// <summary>The REAL path: the one the kernel opens, launched and printed (D17 c).</summary>
    public sealed record Found(string Real) : ExecutableAnswer;

    /// <summary>The host lacks it — a bare name on no PATH entry, a spelled path with no directory entry. Exit 3 (D16: the CLI is not there).</summary>
    public sealed record Absent(string Reason) : ExecutableAnswer;

    /// <summary>The configuration names what the harness will not run: every D17 sentence, a spelled path that exists but the kernel cannot resolve, a platform with
    /// no resolver, a spelled path that is not an executable regular file. Exit 4.</summary>
    public sealed record Refused(string Reason) : ExecutableAnswer;
}

/// <summary>The host's answers about paths, handed in as functions so the deciders stay pure.</summary>
/// <param name="RealPath">The kernel's real path, or the operating system's reason.</param>
/// <param name="Present">The lstat-level question "is there a directory entry by this name": <c>Ok(true)</c> (a dangling link included), <c>Ok(false)</c> (nothing
/// there), <c>Fail</c> (the kernel could not say).</param>
/// <param name="IsExecutableFile">Follows links: an executable regular file at the end.</param>
public sealed record HostPaths(Func<string, Outcome<string>> RealPath, Func<string, Outcome<bool>> Present, Func<string, bool> IsExecutableFile);
