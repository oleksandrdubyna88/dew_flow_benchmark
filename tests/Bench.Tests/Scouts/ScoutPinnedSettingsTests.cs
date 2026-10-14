using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>S5 — the pinned settings file (plan §3.4): projected from the operator's user settings with ONLY <c>effortLevel</c> and
/// <c>modelSettings</c>, read back strictly (a third key is refused by name — the launch loads every key the file holds), and compared
/// with the user settings it was pinned from (a drift names the key and both values).</summary>
public sealed class ScoutPinnedSettingsTests
{
    /// <summary>The operator's own shape on 2026-10-13: permissions with a directory grant, plugins, a theme, the effort — and a comment.</summary>
    private const string UserSettings = """
        {
          // the operator's user settings
          "permissions": { "allow": ["Bash(gh run *)"], "additionalDirectories": ["D:\\somewhere\\granted"] },
          "model": "opus[1m]",
          "enabledPlugins": { "feature-dev@claude-plugins-official": true },
          "effortLevel": "xhigh",
          "modelSettings": { "claude-opus-5-5": { "effortLevel": "high" } },
          "hooks": { "SubagentStart": [] },
          "theme": "dark",
        }
        """;

    private const string Pinned = "{\n  \"effortLevel\": \"xhigh\",\n  \"modelSettings\": {\n    \"claude-opus-5-5\": {\n      \"effortLevel\": \"high\"\n    }\n  }\n}\n";

    [Fact]
    public void The_projection_copies_only_the_two_keys_in_a_canonical_text()
    {
        ScoutPinnedSettings.Project(UserSettings).Ok().Should().Be(Pinned, "the grant, the plugins, the hooks and the theme must never reach a scout");
        ScoutPinnedSettings.Project("{ \"theme\": \"dark\" }").Ok().Should().Be("{}\n", "a settings file without the two keys pins an empty object, not an absence");
        ScoutPinnedSettings.Project("not json").Reason().Should().Contain("not JSON");
    }

    [Fact]
    public void A_pinned_file_carrying_any_other_key_is_refused_by_name()
    {
        ScoutPinnedSettings.Verify(Pinned).Ok().Should().Be(Pinned);
        ScoutPinnedSettings.Verify("{ \"effortLevel\": \"high\", \"permissions\": { \"additionalDirectories\": [\"C:/x\"] } }").Reason()
            .Should().Contain("permissions").And.Contain("effortLevel, modelSettings");
        ScoutPinnedSettings.Verify("[1, 2]").Reason().Should().Contain("not an object");
        ScoutPinnedSettings.Verify("{").Reason().Should().Contain("not JSON");
    }

    [Fact]
    public void A_pinned_file_that_no_longer_matches_the_user_settings_names_the_key_and_both_values()
    {
        ScoutPinnedSettings.Drift(Pinned, UserSettings).Should().BeEmpty("the file still says what the user settings say");
        ScoutPinnedSettings.Drift(Pinned, UserSettings.Replace("\"xhigh\"", "\"medium\"", StringComparison.Ordinal))
            .Should().Contain("effortLevel").And.Contain("\"xhigh\"").And.Contain("\"medium\"");
        ScoutPinnedSettings.Drift(Pinned, "{ \"effortLevel\": \"xhigh\" }").Should().Contain("modelSettings").And.Contain("absent");
    }

    /// <summary>The pilot's finding (2026-10-14): projected from these settings, a SONNET child ran at <c>xhigh</c> and an OPUS child at
    /// <c>high</c> — read from a persisted child transcript's <c>effort</c>. A MODEL comparison needs one explicit level for every model:
    /// <c>--effort &lt;level&gt;</c> pins <c>{"effortLevel": level}</c> alone, in a file of its own, so a run's settings pin says which
    /// condition it measured — the operator's configuration (no flag) or one effort for all.</summary>
    [Fact]
    public void An_explicit_effort_pins_one_level_for_every_model_in_a_file_of_its_own()
    {
        ScoutPinnedSettings.Explicit("high").Ok().Should().Be("{\n  \"effortLevel\": \"high\"\n}\n", "no modelSettings: no model may differ");
        ScoutPinnedSettings.Explicit("XHigh").Ok().Should().Contain("\"xhigh\"");
        ScoutPinnedSettings.Explicit("turbo").Reason().Should().Contain("turbo").And.Contain("low, medium, high, xhigh, max");
        ScoutPinnedSettings.FileNameFor("high").Should().Be("pinned-settings.effort-high.json");
        ScoutPinnedSettings.FileNameFor(string.Empty).Should().Be(ScoutPinnedSettings.FileName, "no flag: the operator's configuration, as before");
    }
}
