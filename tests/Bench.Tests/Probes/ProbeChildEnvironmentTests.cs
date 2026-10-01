using Bench.Domain.Gate;
using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2c, review finding 3: the environment a probe launches a CLI under is a positive list of NAMES — what a CLI needs to run
/// and to find its own login (measured live on 2026-10-01: claude 2.1.258, codex-cli 0.156.1 and agy 1.2.14 each started and answered
/// under exactly this set) — and never a <c>BENCH_*</c>, a <c>COAI_*</c> or a secret-named variable, while every secret-named value of
/// the parent is still scrubbed from the texts written.</summary>
public sealed class ProbeChildEnvironmentTests
{
    [Fact]
    public void Only_the_named_variables_pass_whatever_their_case_and_nothing_the_harness_owns_or_a_secret_ever_does()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Path"] = @"C:\Windows;C:\Users\operator\.local\bin",
            ["PATHEXT"] = ".COM;.EXE",
            ["SystemRoot"] = @"C:\Windows",
            ["USERPROFILE"] = @"C:\Users\operator",
            ["LOCALAPPDATA"] = @"C:\Users\operator\AppData\Local",
            ["TEMP"] = @"C:\Users\operator\AppData\Local\Temp",
            ["ProgramFiles(x86)"] = @"C:\Program Files (x86)",
            ["PROCESSOR_ARCHITECTURE"] = "AMD64",
            ["HOME"] = "/home/operator",
            ["BENCH_DB"] = "Host=db;Password=hunter2-planted",
            ["BENCH_ARTIFACT_ROOT"] = @"D:\bench-artifacts",
            ["COAI_CREDS_KEY"] = "a-vault-key-1234",
            ["OPENAI_API_KEY"] = "sk-operators-own-key-5678",
            ["GITHUB_TOKEN"] = "ghp_planted_token_9012",
            ["PSModulePath"] = @"C:\Modules",
            ["USERDOMAIN"] = "WORKGROUP",
            ["OneDrive"] = @"C:\Users\operator\OneDrive",
        };

        var child = ProbeChildEnvironment.Of(parent);

        child.Names.Should().BeEquivalentTo(["Path", "PATHEXT", "SystemRoot", "USERPROFILE", "LOCALAPPDATA", "TEMP", "ProgramFiles(x86)", "PROCESSOR_ARCHITECTURE", "HOME"],
            "the named set, case-insensitively, the prefixes included; nothing else — not the module path, not the domain, not OneDrive");
        child.CarriesSecret.Should().BeFalse("a CLI launch gets no vault key");
        child.Scrub("env: BENCH_DB=Host=db;Password=hunter2-planted key sk-operators-own-key-5678 token ghp_planted_token_9012 vault a-vault-key-1234")
            .Should().Be("env: BENCH_DB=[redacted] key [redacted] token [redacted] vault [redacted]", "every secret-named value of the parent is scrubbed — and every BENCH_* value, the database url carries a password");
        child.ToString().Should().NotContain("hunter2").And.NotContain("sk-operators");
    }

    [Theory]
    [InlineData("PATH", true)]
    [InlineData("path", true)]
    [InlineData("ProgramW6432", true)]
    [InlineData("PROCESSOR_LEVEL", true)]
    [InlineData("BENCH_CLAUDE", false)]
    [InlineData("bench_db", false)]
    [InlineData("COAI_VENDORS", false)]
    [InlineData("ANTHROPIC_API_KEY", false)]
    [InlineData("MY_SECRET", false)]
    [InlineData("DB_PASSWORD", false)]
    [InlineData("PSModulePath", false)]
    [InlineData("DOTNET_ROOT", false)]
    public void A_name_passes_only_when_it_is_on_the_list_and_never_when_the_harness_owns_it_or_it_names_a_secret(string name, bool passes) =>
        ProbeChildEnvironment.Passes(name).Should().Be(passes);

    [Fact]
    public void A_harness_variable_is_a_bench_or_a_coai_name_whatever_its_case()
    {
        CoaiEnvironment.IsHarnessVariable("BENCH_DB").Should().BeTrue();
        CoaiEnvironment.IsHarnessVariable("coai_data_dir").Should().BeTrue();
        CoaiEnvironment.IsHarnessVariable("PATH").Should().BeFalse();
        CoaiEnvironment.IsHarnessVariable("BENCHMARK_X").Should().BeFalse("the prefix is BENCH_ with the underscore");
    }
}
