using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Test 16 of E4.S2: every program the arm's clone can name that a worktree add, a checkout, a submodule update or a cherry-pick would start — each PROVEN LIVE with the host's own git first (the plant fires without the harness), then absent under the harness. The editor rows are the
/// exception that proves nothing by removal: git 2.53 does not open an editor for a one-commit non-interactive pick (M-j), so their break-it is the argv pin of test 15, said there.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    public static TheoryData<string> PlantRows() =>
    [
        "smudge", "post-checkout", "update-cmd", "submodule-smudge",
        "merge-driver", "merge-default", "core-editor", "sequence-editor", "gpg-program", "gpg-ssh-program", "gpg-x509-program", "post-commit", "merge-driver-named-a=b", "filter-process-on-the-pick",
        "includeIf-onbranch-add", "includeIf-gitdir-add", "includeIf-onbranch-attach", "includeIf-gitdir-attach", "includeIf-onbranch-pick", "global-includeIf-gitdir-submodule", "global-includeIf-onbranch-submodule",
    ];

    private static string Plant(WorktreeKit kit, string name, string body = "exit 0") => Sentinel.PlantProgram(kit.Temp, name, body);

    private static void Hook(WorktreeKit kit, string hook, string plant) => ShimFiles.Write(Path.Combine(kit.Clone, ".git", "hooks", hook), $"#!/bin/sh\n'{plant}'\n");

    private static Task ConfigAsync(WorktreeKit kit, string key, string value) => kit.GitAsync(kit.Clone, "config", "--", key, value);

    private static Task ProveLiveAsync(WorktreeKit kit, string folder, params string[] question) =>
        Sentinel.ProveLiveAsync(kit.Temp, kit.HostEnvironment, Ct, folder, null, question);

    [Theory]
    [MemberData(nameof(PlantRows))]
    public async Task Worktree_add_checkout_and_cherry_pick_of_the_arms_clone_run_no_program_the_clone_names(string row)
    {
        using var kit = await KitAsync();
        if (row is "smudge" or "post-checkout" or "update-cmd" or "submodule-smudge" || row.EndsWith("-add", StringComparison.Ordinal) || row.EndsWith("-attach", StringComparison.Ordinal) || row.EndsWith("-submodule", StringComparison.Ordinal))
        {
            await EnterRowAsync(kit, row);
        }
        else
        {
            await PickRowAsync(kit, row);
        }
    }

    private static async Task LiveWorktreeAsync(WorktreeKit kit, string branch = "live", string? attachBranch = null, params string[] then)
    {
        var live = kit.Temp.Combine("live-wt");
        string[] add = attachBranch is null ? ["worktree", "add", "-q", live, "-b", branch, "HEAD"] : ["worktree", "add", "-q", live, attachBranch];
        if (then.Length == 0)
        {
            await ProveLiveAsync(kit, kit.Clone, add);
        }
        else
        {
            await kit.HostGitAsync(kit.Clone, add);
            await Sentinel.ProveLiveAsync(kit.Temp, kit.HostEnvironment, Ct, live, null, then);
        }

        await kit.HostGitAsync(kit.Clone, "worktree", "remove", "--force", "--force", live);
        await kit.HostGitAsync(kit.Clone, "branch", "-D", attachBranch ?? branch);
        File.Delete(Sentinel.Marker(kit.Temp));
    }

    private static async Task EnterRowAsync(WorktreeKit kit, string row)
    {
        string? check = null;
        switch (row)
        {
            case "smudge":
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt filter=x\n", "attributes");
                await kit.CommitInAsync(kit.Clone, "f.txt", "the blob\n", "f");
                await ConfigAsync(kit, "filter.x.smudge", Plant(kit, "smudge", "cat"));
                await LiveWorktreeAsync(kit);
                check = "f.txt";
                break;
            case "post-checkout":
                Hook(kit, "post-checkout", Plant(kit, "hook"));
                await LiveWorktreeAsync(kit);
                break;
            case "update-cmd":
                await ConfigAsync(kit, "submodule..agents/conventions.update", "!" + Plant(kit, "update"));
                await LiveWorktreeAsync(kit, then: ["-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q", "--", ".agents/conventions"]);
                break;
            case "includeIf-onbranch-add" or "includeIf-gitdir-add":
                await IncludeIfFilterAsync(kit, row);
                await LiveWorktreeAsync(kit, branch: "tmp/live");
                check = "a.txt";
                break;
            case "includeIf-onbranch-attach" or "includeIf-gitdir-attach":
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt filter=y\n", "attributes");
                await AttachSetupAsync(kit);
                await IncludeIfFilterAsync(kit, row, commitAttributes: false);
                await kit.HostGitAsync(kit.Clone, "branch", "tmp/live");
                await LiveWorktreeAsync(kit, attachBranch: "tmp/live");
                check = "a.txt";
                break;
            default:
                await SubmoduleSmudgeAsync(kit, row);
                break;
        }

        var ready = await kit.ReadyAsync(kit.Sut(), "t1", "t2");

        Sentinel.Ran(kit.Temp).Should().BeFalse($"{row}: no program the clone names ran");
        ready.Places.Should().HaveCount(2);
        File.Exists(Path.Combine(kit.Wt("t1"), ".agents", "conventions", "tools", "low-priority.mjs")).Should().BeTrue("the submodule was checked out (--checkout)");
        if (check is not null)
        {
            File.ReadAllText(Path.Combine(kit.Wt("t1"), check)).Should().Be(check == "a.txt" ? "one\ntwo\nthree\n" : "the blob\n", "the file's content is the blob's");
        }

        if (row.EndsWith("-submodule", StringComparison.Ordinal) || row == "submodule-smudge")
        {
            File.ReadAllText(Path.Combine(kit.Wt("t1"), ".agents", "conventions", "r.md")).Should().Be("rules doc\n");
        }
    }

    /// <summary>A filter the clone names only under a condition its WORKTREE meets (<c>onbranch:tmp/**</c>, <c>gitdir:**/worktrees/**</c>): the enumeration in the clone sees nothing, the checkout in the worktree runs it (M-x).</summary>
    private static async Task IncludeIfFilterAsync(WorktreeKit kit, string row, bool commitAttributes = true)
    {
        if (commitAttributes)
        {
            await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt filter=y\n", "attributes");
        }

        var included = kit.Temp.File($"[filter \"y\"]\n\tsmudge = {Plant(kit, "include-smudge", "cat")}\n", "inc.cfg");
        var condition = row.StartsWith("includeIf-onbranch", StringComparison.Ordinal) ? "onbranch:tmp/**" : "gitdir:**/worktrees/**";
        File.AppendAllText(Path.Combine(kit.Clone, ".git", "config"), $"[includeIf \"{condition}\"]\n\tpath = {included}\n");
    }

    /// <summary>t1 entered and done with a commit, its folder removed: the attach road (the registration stays, prunable).</summary>
    private static async Task AttachSetupAsync(WorktreeKit kit)
    {
        await kit.ReadyAsync(kit.Sut(), "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        Directory.Delete(kit.Wt("t1"), recursive: true);
        kit.Set("t1", new StepEntry.Skip());
    }

    private static async Task SubmoduleSmudgeAsync(WorktreeKit kit, string row)
    {
        await File.WriteAllTextAsync(Path.Combine(kit.Bench.Rules, ".gitattributes"), "*.md filter=g\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(kit.Bench.Rules, "r.md"), "rules doc\n", Ct);
        await kit.GitAsync(kit.Bench.Rules, "add", "-A");
        await kit.CommitStagedAsync(kit.Bench.Rules, "rules gain a markdown file and an attribute");
        var module = Path.Combine(kit.Clone, ".agents", "conventions");
        await kit.GitAsync(module, "fetch", "-q", "origin", "main");
        await kit.GitAsync(module, "checkout", "-q", "FETCH_HEAD");
        await kit.GitAsync(kit.Clone, "add", ".agents/conventions");
        await kit.CommitStagedAsync(kit.Clone, "bump the rules");
        Directory.CreateDirectory(kit.Root.Environment["HOME"]);
        var smudge = $"[filter \"g\"]\n\tsmudge = {Plant(kit, "global-smudge", "cat")}\n";
        var global = row switch
        {
            "global-includeIf-gitdir-submodule" => $"[includeIf \"gitdir:**/modules/**\"]\n\tpath = {kit.Temp.File(smudge, "global-inc.cfg")}\n",
            "global-includeIf-onbranch-submodule" => $"[includeIf \"onbranch:main\"]\n\tpath = {kit.Temp.File(smudge, "global-inc.cfg")}\n",
            _ => smudge,
        };
        await File.WriteAllTextAsync(Path.Combine(kit.Root.Environment["HOME"], ".gitconfig"), global, Ct);
        await LiveWorktreeAsync(kit, then: ["-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q", "--", ".agents/conventions"]);
    }

    private static async Task PickRowAsync(WorktreeKit kit, string row)
    {
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        kit.Set("t1", new StepEntry.Skip());
        var merging = row is "merge-driver" or "merge-default" or "merge-driver-named-a=b";
        var plain = row is "filter-process-on-the-pick" ? "d.md" : "c1.txt";
        if (merging)
        {
            await kit.CommitInAsync(kit.Wt("t1"), "a.txt", "t1\ntwo\nthree\n", "t1 changes line 1");
            await kit.CommitInAsync(kit.Clone, "a.txt", "one\ntwo\narm\n", "the arm changes line 3");
        }
        else
        {
            await kit.CommitInAsync(kit.Wt("t1"), plain, "the blob\n", "t1 work");
        }

        var source = await kit.HeadAsync(kit.Wt("t1"));
        switch (row)
        {
            case "merge-driver":
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt merge=m\n", "attributes");
                await ConfigAsync(kit, "merge.m.driver", Plant(kit, "driver") + " %O %A %B");
                break;
            case "merge-default":
                await ConfigAsync(kit, "merge.default", "m");
                await ConfigAsync(kit, "merge.m.driver", Plant(kit, "driver") + " %O %A %B");
                break;
            case "merge-driver-named-a=b":
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt merge=a=b\n", "attributes");
                await ConfigAsync(kit, "merge.a=b.driver", Plant(kit, "driver") + " %O %A %B");
                break;
            case "core-editor":
                await ConfigAsync(kit, "core.editor", Plant(kit, "editor"));
                break;
            case "sequence-editor":
                await ConfigAsync(kit, "sequence.editor", Plant(kit, "sequence"));
                break;
            case "gpg-program":
            case "gpg-ssh-program":
            case "gpg-x509-program":
                await ConfigAsync(kit, "commit.gpgsign", "true");
                var signer = Plant(kit, "signer", "exit 1");
                await ConfigAsync(kit, row switch { "gpg-ssh-program" => "gpg.ssh.program", "gpg-x509-program" => "gpg.x509.program", _ => "gpg.program" }, signer);
                if (row != "gpg-program")
                {
                    await ConfigAsync(kit, "gpg.format", row == "gpg-ssh-program" ? "ssh" : "x509");
                }

                if (row == "gpg-ssh-program")
                {
                    await ConfigAsync(kit, "user.signingkey", kit.Temp.Combine("key.pub"));
                }

                break;
            case "post-commit":
                Hook(kit, "post-commit", Plant(kit, "hook"));
                break;
            case "includeIf-onbranch-pick":
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt filter=y\n", "attributes");
                File.AppendAllText(Path.Combine(kit.Clone, ".git", "config"), $"[includeIf \"onbranch:arm\"]\n\tpath = {kit.Temp.File($"[filter \"y\"]\n\tsmudge = {Plant(kit, "include-smudge", "cat")}\n", "inc.cfg")}\n");
                break;
            default:
                await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.md filter=zz\n", "attributes");
                await ConfigAsync(kit, "filter.zz.process", Plant(kit, "process"));
                break;
        }

        var armHead = await kit.HeadAsync(kit.Clone);
        if (row == "sequence-editor")
        {
            await ProveLiveAsync(kit, kit.Clone, "rebase", "-i", "HEAD~1");
            await kit.HostGitAsync(kit.Clone, "rebase", "--abort");
        }
        else
        {
            await ProveLiveAsync(kit, kit.Clone, row == "core-editor" ? ["cherry-pick", "--edit", source] : ["cherry-pick", source]);
            await kit.HostGitAsync(kit.Clone, "cherry-pick", "--abort");
        }

        if (row == "filter-process-on-the-pick")
        {
            await kit.GitAsync(kit.Clone, "config", "--unset", "filter.zz.process");
        }

        await kit.GitAsync(kit.Clone, "reset", "-q", "--hard", armHead);
        if (row == "filter-process-on-the-pick")
        {
            await ConfigAsync(kit, "filter.zz.process", Plant(kit, "process"));
        }

        File.Delete(Sentinel.Marker(kit.Temp));
        (await kit.HeadAsync(kit.Clone)).Should().Be(armHead, "the live proof is undone");

        var stopped = await sut.PickBackAsync("t1", Ct);

        Sentinel.Ran(kit.Temp).Should().BeFalse($"{row}: no program the clone names ran under the harness");
        if (merging)
        {
            if (row == "merge-default")
            {
                stopped.Should().BeNull("an emptied merge.default is git's built-in text merge on 2.53 (line 1 and line 3 do not overlap): the pick succeeds and no program ran — the conflict stop of M-k belongs to the named driver");
                return;
            }

            stopped.Should().NotBeNull($"{row}: the emptied driver is the conflict stop");
            stopped!.ExitCode.Should().Be(4);
            return;
        }

        stopped.Should().BeNull();
        (await kit.GitAsync(kit.Clone, "log", "-1", "--format=%s")).Trim().Should().Be("t1 work");
        if (row.StartsWith("gpg", StringComparison.Ordinal))
        {
            (await kit.GitAsync(kit.Clone, "cat-file", "-p", "HEAD")).Should().NotContain("gpgsig", "nothing signed");
        }

        if (row == "filter-process-on-the-pick")
        {
            File.ReadAllText(Path.Combine(kit.Clone, plain)).Should().Be("the blob\n", "the picked file's content is the blob's");
        }
    }
}
