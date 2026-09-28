using System.Security.Cryptography;
using System.Text;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Gate;

/// <summary><c>ProductPin.Read</c>: which bytes of the product a claim measures.
/// <para>
/// <b>The hash covers the deployment set, not only the file named.</b> A framework-dependent .NET app's
/// <c>coai-mcp.exe</c> is an apphost whose bytes barely move between builds; the code is in <c>coai-mcp.dll</c> beside it
/// and in its project-reference assemblies. So when a sibling <c>&lt;name&gt;.dll</c> exists the pin is the SHA-256 of a
/// manifest — every <c>.dll</c>, <c>.exe</c> and <c>.json</c> under the binary's folder, relative path and SHA-256 each,
/// sorted — and a rebuild that changed one dependency moves it. A self-contained single file is hashed alone.
/// </para>
/// <para>
/// <b>The dirty check is scoped</b>: <c>git status --porcelain --untracked-files=no -- &lt;tree&gt;</c>, where the tree is
/// the nearest directory above the binary holding a <c>*.csproj</c> — the product's own project — or the whole checkout
/// when there is none, and the pin says which. Untracked files are excluded on purpose: a scratch file beside the
/// source is not a changed product. A binary outside any checkout has an empty git sha and a dirty count that is
/// <i>not captured</i>.
/// </para></summary>
public sealed class ProductPinReader : IProductPinReader
{
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);
    private static readonly string[] DeploymentExtensions = [".dll", ".exe", ".json"];

    public async Task<Outcome<ProductPin>> ReadAsync(string executable, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(executable);

        if (!File.Exists(full))
        {
            return Outcome<ProductPin>.Failure($"the product binary '{executable}' does not exist — pass --coai-exe with the coai-mcp you mean to measure");
        }

        var sha = await HashAsync(full, cancellationToken);
        var version = await VersionAsync(full, cancellationToken);

        if (version is Outcome<string>.Fail fail)
        {
            return Outcome<ProductPin>.Failure(fail.Reason);
        }

        var (git, dirty, tree) = await CheckoutAsync(Path.GetDirectoryName(full)!, cancellationToken);

        return ProductPin.Hashed(sha, ((Outcome<string>.Ok)version).Value, git, dirty, tree);
    }

    /// <summary>The deployment set's hash — or the file's own when it stands alone.</summary>
    public static async Task<string> HashAsync(string executable, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(executable)!;
        var sibling = Path.Combine(folder, Path.GetFileNameWithoutExtension(executable) + ".dll");

        if (!File.Exists(sibling))
        {
            return await FileHashAsync(executable, cancellationToken);
        }

        var manifest = new StringBuilder();
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => DeploymentExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => (Relative: Path.GetRelativePath(folder, f).Replace('\\', '/'), Full: f))
            .OrderBy(f => f.Relative, StringComparer.Ordinal);

        foreach (var (relative, file) in files)
        {
            manifest.Append(relative).Append('\t').Append(await FileHashAsync(file, cancellationToken)).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static async Task<string> FileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static async Task<Outcome<string>> VersionAsync(string executable, CancellationToken cancellationToken)
    {
        var attempt = await ProcessRunner.RunAsync(executable, ["--version"], Path.GetDirectoryName(executable)!, VersionTimeout, cancellationToken);

        return attempt switch
        {
            ProcessAttempt.Completed c when c.Result.Ok && FirstLine(c.Result.StandardOutput).Length > 0 =>
                Outcome<string>.Success(FirstLine(c.Result.StandardOutput)),
            ProcessAttempt.Completed c => Outcome<string>.Failure($"the product answered --version with exit {c.Result.ExitCode} and no version text"),
            _ => Outcome<string>.Failure($"the product's --version {attempt.Describe}"),
        };
    }

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.TrimEntries).FirstOrDefault(l => l.Length > 0) ?? string.Empty;

    private static async Task<(string Git, CapturedCount Dirty, string Tree)> CheckoutAsync(string folder, CancellationToken cancellationToken)
    {
        var top = await GitCommand.ReadAsync(folder, GitTimeout, cancellationToken, "rev-parse", "--show-toplevel");

        if (top is not Outcome<string>.Ok { Value: var root } || root.Trim().Length == 0)
        {
            return (string.Empty, CapturedCount.Unavailable("the binary sits under no git checkout"), string.Empty);
        }

        var checkout = Path.GetFullPath(root.Trim());
        var sha = await GitCommand.ReadAsync(checkout, GitTimeout, cancellationToken, "rev-parse", "--short", "HEAD");
        var tree = ProjectTree(folder, checkout);
        var status = await GitCommand.ReadAsync(
            checkout, GitTimeout, cancellationToken, ["status", "--porcelain", "--untracked-files=no", "--", tree.Length > 0 ? tree : "."]);

        var dirty = status is Outcome<string>.Ok ok
            ? CapturedCount.Number(ok.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length)
            : CapturedCount.Unavailable("git status failed");

        return (sha is Outcome<string>.Ok s ? s.Value.Trim() : string.Empty, dirty, tree);
    }

    /// <summary>The nearest folder at or above the binary that holds a <c>*.csproj</c>, relative to the checkout —
    /// empty when there is none (the whole checkout is then checked).</summary>
    private static string ProjectTree(string folder, string checkout)
    {
        for (var dir = new DirectoryInfo(folder); dir is not null && IsWithin(dir.FullName, checkout); dir = dir.Parent)
        {
            if (dir.EnumerateFiles("*.csproj").Any())
            {
                var relative = Path.GetRelativePath(checkout, dir.FullName).Replace('\\', '/');
                return relative == "." ? string.Empty : relative;
            }
        }

        return string.Empty;
    }

    private static bool IsWithin(string path, string root) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).StartsWith(
            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
