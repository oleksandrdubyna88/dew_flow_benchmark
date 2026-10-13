using System.Diagnostics;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The path audit's link port on a REAL disk (§3.2): a junction (Windows — creating a symbolic link needs a privilege an ordinary
/// session lacks, a junction does not) or a symbolic link (elsewhere) inside a tree that points out of it resolves to where it points,
/// and the audit then judges the call outside.</summary>
public sealed class FileSystemLinkTargetsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("scout-links-");

    public void Dispose()
    {
        // A junction is deleted as a directory entry, never followed: the folder it points at survives its removal.
        foreach (var link in _root.EnumerateDirectories("*", SearchOption.AllDirectories).Where(d => d.LinkTarget is not null).ToList())
        {
            link.Delete();
        }

        _root.Delete(recursive: true);
    }

    [Fact]
    public void A_link_inside_the_tree_that_points_out_of_it_resolves_to_its_target_and_the_audit_judges_it_outside()
    {
        var tree = _root.CreateSubdirectory("tree");
        var outside = _root.CreateSubdirectory("outside");
        File.WriteAllText(Path.Combine(outside.FullName, "secret.txt"), "secret");
        MakeDirectoryLink(Path.Combine(tree.FullName, "vendor"), outside.FullName);
        var treePath = Normal(tree.FullName);

        FileSystemLinkTargets.Resolve($"{treePath}/vendor/secret.txt").Should().Be($"{Normal(outside.FullName)}/secret.txt");
        FileSystemLinkTargets.Resolve($"{treePath}/src/missing.cs").Should().Be($"{treePath}/src/missing.cs", "a path that does not exist is kept as written");

        var stream = Bench.Tests.Scouts.ScoutStreams.Edit(
            Bench.Tests.Scouts.ScoutStreams.Cell([new Bench.Tests.Scouts.ChildSpec("p", Tools: [new Bench.Tests.Scouts.ToolSpec("Read", """{"file_path":"vendor/secret.txt"}""", Result: "secret")])]),
            line =>
            {
                if ((string?)line["subtype"] == "init")
                {
                    line["cwd"] = treePath;
                }

                return line;
            });

        PathAudit.Audit(Bench.Domain.Probes.ClaudeSubagents.Read(stream), treePath, FileSystemLinkTargets.Here).Single().Finding.Should().Be(PathFinding.OutsideContent);
    }

    private static string Normal(string path) => path.Replace('\\', '/').TrimEnd('/');

    private static void MakeDirectoryLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.WaitForExit(TimeSpan.FromSeconds(30)).Should().BeTrue("mklink /J answers at once");
        process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
    }
}
