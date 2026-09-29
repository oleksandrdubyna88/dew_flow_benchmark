using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>D5 of the fidelity plan: an assessor row with no executable reference launches its runtime's bare word, and on
/// Windows the npm install of <c>codex</c> is a <c>.cmd</c>/<c>.ps1</c> shim — the launcher found nothing, and all 16 findings of
/// a campaign were recorded one by one as "assessment failed". The word is now resolved the way a shell resolves it, before
/// anything is sent: a native executable is launched by its full path, a shim or nothing is refused naming what was found.</summary>
public sealed class CliExecutableTests
{
    private const string PathExt = ".COM;.EXE;.BAT;.CMD;.VBS;.JS;.WS;.MSC;.PS1";

    [Fact]
    public void A_native_executable_on_the_path_is_resolved_to_its_full_path()
    {
        var found = Resolve(@"C:\tools;C:\npm", [@"C:\npm\codex.exe"]);

        found.Ok().Should().Be(@"C:\npm\codex.exe");
    }

    [Fact]
    public void A_word_that_resolves_only_to_an_npm_shim_is_refused_naming_the_shim_and_the_flag()
    {
        var found = Resolve(@"C:\npm", [@"C:\npm\codex", @"C:\npm\codex.cmd", @"C:\npm\codex.ps1"]);

        found.Reason().Should().Contain(@"C:\npm\codex.cmd").And.Contain("--executable-ref").And.Contain("shim");
    }

    [Fact]
    public void The_first_path_directory_with_a_match_decides_as_a_shell_does()
    {
        Resolve(@"C:\npm;C:\native", [@"C:\npm\codex.cmd", @"C:\native\codex.exe"]).Reason()
            .Should().Contain("codex.cmd", "the shell would run the shim first, so the shim is what the word means here");
        Resolve(@"C:\native;C:\npm", [@"C:\npm\codex.cmd", @"C:\native\codex.exe"]).Ok().Should().Be(@"C:\native\codex.exe");
    }

    [Fact]
    public void A_word_on_no_path_directory_is_refused_by_name()
    {
        Resolve(@"C:\tools", []).Reason().Should().Contain("'codex'").And.Contain("not on PATH").And.Contain("--executable-ref");
    }

    /// <summary>Code round, 2026-09-29: an empty or partial PATHEXT made no candidate at all, and a real <c>codex.exe</c>
    /// was refused as "not on PATH". The native extensions are always candidates, after the configured order.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(".BAT;.CMD")]
    public void A_native_executable_is_found_whatever_PATHEXT_says(string pathExt)
    {
        CliExecutable.Resolve("codex", @"C:\npm", pathExt, windows: true, p => string.Equals(p, @"C:\npm\codex.exe", StringComparison.OrdinalIgnoreCase))
            .Ok().Should().Be(@"C:\npm\codex.exe");
    }

    [Fact]
    public void Elsewhere_the_extensionless_file_is_the_executable()
    {
        CliExecutable.Resolve("codex", "/usr/bin:/home/u/.npm/bin", string.Empty, windows: false, p => p == "/home/u/.npm/bin/codex")
            .Ok().Should().Be("/home/u/.npm/bin/codex");
    }

    private static Domain.Outcome<string> Resolve(string path, IReadOnlyList<string> files) =>
        CliExecutable.Resolve("codex", path, PathExt, windows: true, p => files.Contains(p, StringComparer.OrdinalIgnoreCase));
}
