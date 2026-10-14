using Bench.Domain.Scouts;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The pinned settings under an explicit effort (the pilot, 2026-10-14): <c>--effort high</c> makes the artefacts pin
/// <c>{"effortLevel": "high"}</c> in <c>pinned-settings.effort-high.json</c> — whatever the operator's settings say per model — and never touch
/// the operator-configuration file; a file found again that says anything else is refused, as is a level Claude Code does not accept.</summary>
public sealed class ScoutArtifactsEffortTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private string UserSettings()
    {
        var file = Path.Combine(_root.Path, "settings.json");
        File.WriteAllText(file, """{ "effortLevel": "xhigh", "modelSettings": { "opus": { "effortLevel": "high" } } }""");
        return file;
    }

    [Fact]
    public void An_explicit_effort_pins_one_level_in_its_own_file_and_leaves_the_operator_configuration_alone()
    {
        var artifacts = new ScoutArtifacts(Path.Combine(_root.Path, "artifacts")) { Effort = "high" };

        var pinned = artifacts.PinnedSettings([UserSettings()]).Ok();

        pinned.Should().Be(artifacts.PinnedSettingsFile).And.EndWith("pinned-settings.effort-high.json");
        File.ReadAllText(pinned).Should().Be(ScoutPinnedSettings.Explicit("high").Ok(), "one level for every model, whatever the user settings say per model");
        File.Exists(new ScoutArtifacts(Path.Combine(_root.Path, "artifacts")).PinnedSettingsFile).Should().BeFalse("the operator-configuration file is another condition");
        artifacts.PinnedSettings([UserSettings()]).Ok().Should().Be(pinned, "found again unchanged, it is the same pin");
    }

    [Fact]
    public void An_explicit_effort_file_that_says_anything_else_and_an_unknown_level_are_refused()
    {
        var artifacts = new ScoutArtifacts(Path.Combine(_root.Path, "artifacts")) { Effort = "high" };
        Directory.CreateDirectory(Path.GetDirectoryName(artifacts.PinnedSettingsFile)!);
        File.WriteAllText(artifacts.PinnedSettingsFile, "{\n  \"effortLevel\": \"xhigh\"\n}\n");

        artifacts.PinnedSettings([UserSettings()]).Reason().Should().Contain("effort-high").And.Contain("delete it");
        (new ScoutArtifacts(Path.Combine(_root.Path, "other")) { Effort = "turbo" }).PinnedSettings([UserSettings()]).Reason().Should().Contain("turbo");
    }

    public void Dispose() => _root.Dispose();
}
