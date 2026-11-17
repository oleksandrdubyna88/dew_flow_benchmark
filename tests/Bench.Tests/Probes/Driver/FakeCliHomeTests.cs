using System.Collections;
using Bench.Infrastructure.Process;
using Bench.Tests.Leftovers;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes.Driver;

/// <summary>E6 S5 (D6) — where the fake CLI's home is. An id of the owner form names an owned folder directly under the launcher's temp root, and the fake
/// NEVER makes it: a launch whose home is missing is refused at the first write, so no test can leave a folder behind by accident. The probe driver
/// (<see cref="FakeCli"/>) mints such an id from its own owned folder, and every launch of the fake runs under a temp root the test owns.</summary>
public sealed class FakeCliHomeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>This process's environment, minus the fake's own variables, with the temp root of the child set to <paramref name="temp"/> (the launcher's one door: <c>TMPDIR</c>, <c>TEMP</c>, <c>TMP</c>).</summary>
    private static Dictionary<string, string> Under(string temp)
    {
        var env = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(e => (Name: (string)e.Key, Value: e.Value as string ?? string.Empty))
            .Where(e => !e.Name.StartsWith("FAKE_CLI_", StringComparison.Ordinal))
            .ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        env["TMPDIR"] = temp;
        env["TEMP"] = temp;
        env["TMP"] = temp;

        return env;
    }

    private static async Task<ProcessResult> Launch(string temp, params string[] argv) =>
        (await ProcessRunner.RunAsync(FakeCli.Executable, argv, temp, TimeSpan.FromSeconds(60), "prompt", Under(temp), Ct))
            .Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;

    [Fact]
    public async Task An_owner_form_model_id_whose_folder_is_missing_is_refused_at_the_first_write()
    {
        using var temp = TempDir.For("fakecli-temp");
        var id = Owned.NameFor("fakecli");

        var result = await Launch(temp.Path, "-p", "--model", id);

        result.ExitCode.Should().Be(3, "the fake never makes its own home");
        result.StandardError.Should().Contain(temp.At(id), "the refusal names the home the test should have made");
        Directory.Exists(temp.At(id)).Should().BeFalse();
        Directory.Exists(temp.At("bench-fake-cli")).Should().BeFalse("an owner-form id has no folder of its own beside a shared parent");
    }

    [Fact]
    public async Task The_version_answers_without_a_home()
    {
        using var temp = TempDir.For("fakecli-temp");

        var result = await Launch(temp.Path, "--model", Owned.NameFor("fakecli"), "--version");

        result.ExitCode.Should().Be(0);
        Directory.EnumerateFileSystemEntries(temp.Path).Should().BeEmpty("a version answer writes nothing");
    }

    [Fact]
    public async Task A_probe_subject_lives_in_its_owned_folder()
    {
        string leaf;
        string home;
        using (var fake = new FakeCli())
        {
            home = fake.Home;
            leaf = Path.GetFileName(home);
            Owned.FullName().IsMatch(leaf).Should().BeTrue("the subject's model id is the owned folder's own name");
            File.Exists(home + ".owner").Should().BeTrue("the folder is claimed first");
            fake.Model.Should().Be(leaf);

            var result = (await ProcessRunner.RunAsync(FakeCli.Executable, ["-p", "--model", fake.Model], TempDir.Root, TimeSpan.FromSeconds(60), "prompt", Ct))
                .Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;

            result.ExitCode.Should().Be(0);
            fake.Calls().Should().ContainSingle("the launch logged its call in the owned home's events folder");
        }

        Directory.Exists(home).Should().BeFalse();
        File.Exists(home + ".owner").Should().BeFalse();
    }
}
