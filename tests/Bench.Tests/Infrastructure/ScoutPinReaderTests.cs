using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>S5 — the pins on disk: the plugin directory resolved from the cache and the registry's user-scope install (fourteen version
/// folders sat under the cache on 2026-10-13, so the cache itself can pin nothing); the plugin hash blind to claude's <c>.in_use</c> markers;
/// the settings pin taken over the PINNED file, which must be there and hold only its two keys.</summary>
public sealed class ScoutPinReaderTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    public void Dispose() => _root.Dispose();

    private string Cache => Path.Combine(_root.Path, "cache", "feature-dev");

    private string Registry => Path.Combine(_root.Path, "installed_plugins.json");

    private string Pinned => Path.Combine(_root.Path, "scouts", "pinned-settings.json");

    private ScoutPinSources Sources => new(Cache, [Path.Combine(_root.Path, "settings.json")]) { PluginRegistry = Registry, PinnedSettingsFile = Pinned };

    [Fact]
    public void The_plugin_directory_is_the_registry_s_user_scope_install_under_the_cache_never_the_cache_of_every_version()
    {
        var current = Plugin("f359d43b4060");
        Plugin("d182ca456ca0");
        File.WriteAllText(Registry, RegistryJson(("local", Path.Combine(Cache, "d182ca456ca0")), ("user", current)));

        ScoutPluginDirectory.Resolve(Sources).Ok().Should().Be(Path.GetFullPath(current));

        File.WriteAllText(Registry, RegistryJson(("local", Path.Combine(Cache, "d182ca456ca0"))));
        ScoutPluginDirectory.Resolve(Sources).Reason().Should().Contain("user scope");
        File.Delete(Registry);
        ScoutPluginDirectory.Resolve(Sources).Reason().Should().Contain("2 version folder(s)").And.Contain("--plugin-registry");
        File.WriteAllText(Registry, RegistryJson(("user", Path.Combine(_root.Path, "elsewhere"))));
        ScoutPluginDirectory.Resolve(Sources).Reason().Should().Contain("outside the plugin cache");
    }

    [Fact]
    public void A_cache_that_is_itself_a_plugin_folder_is_the_plugin_directory()
    {
        Directory.CreateDirectory(Path.Combine(Cache, ".claude-plugin"));
        File.WriteAllText(Path.Combine(Cache, ".claude-plugin", "plugin.json"), "{\"name\":\"feature-dev\"}");

        ScoutPluginDirectory.Resolve(Sources).Ok().Should().Be(Path.GetFullPath(Cache));
        ScoutPluginDirectory.Resolve(Sources with { PluginCache = Path.Combine(_root.Path, "no-such") }).Reason().Should().Contain("not there");
    }

    [Fact]
    public void The_plugin_hash_ignores_claude_s_session_markers_and_moves_on_a_plugin_file()
    {
        var plugin = Plugin("f359d43b4060");
        var before = ScoutPinReader.FolderHash(plugin);

        Directory.CreateDirectory(Path.Combine(plugin, ".in_use"));
        File.WriteAllText(Path.Combine(plugin, ".in_use", "12345"), string.Empty);
        File.WriteAllText(Path.Combine(plugin, ".orphaned_at"), "2026-10-13");
        ScoutPinReader.FolderHash(plugin).Should().Be(before, "a session starting on this machine is not a plugin change");

        File.WriteAllText(Path.Combine(plugin, "agents", "code-explorer.md"), "# updated\n");
        ScoutPinReader.FolderHash(plugin).Should().NotBe(before);
    }

    [Fact]
    public async Task The_settings_pin_is_the_pinned_file_s_hash_and_a_missing_or_widened_file_refuses_the_read()
    {
        var current = Plugin("f359d43b4060");
        File.WriteAllText(Registry, RegistryJson(("user", current)));
        var reader = new ScoutPinReader(new ProductPinReader(), Sources);

        (await reader.ReadAsync(ScoutFake.Executable, TestContext.Current.CancellationToken)).Reason().Should().Contain("pinned settings file").And.Contain("not there");

        Directory.CreateDirectory(Path.GetDirectoryName(Pinned)!);
        File.WriteAllText(Pinned, "{\n  \"effortLevel\": \"high\"\n}\n");
        var pins = (await reader.ReadAsync(ScoutFake.Executable, TestContext.Current.CancellationToken)).Ok();
        pins.SettingsHash.Should().Be(ScoutPinReader.SettingsHash([Pinned]));
        pins.PluginDirectory.Should().Be(Path.GetFullPath(current));
        pins.PluginCacheHash.Should().Be(ScoutPinReader.FolderHash(current));

        File.WriteAllText(Pinned, "{\n  \"effortLevel\": \"high\",\n  \"hooks\": {}\n}\n");
        (await reader.ReadAsync(ScoutFake.Executable, TestContext.Current.CancellationToken)).Reason().Should().Contain("hooks");
    }

    private string Plugin(string version)
    {
        var folder = Path.Combine(Cache, version);
        Directory.CreateDirectory(Path.Combine(folder, ".claude-plugin"));
        Directory.CreateDirectory(Path.Combine(folder, "agents"));
        File.WriteAllText(Path.Combine(folder, ".claude-plugin", "plugin.json"), "{\"name\":\"feature-dev\"}");
        File.WriteAllText(Path.Combine(folder, "agents", "code-explorer.md"), $"# code explorer {version}\n");
        return folder;
    }

    private static string RegistryJson(params (string Scope, string Path)[] installs) =>
        new System.Text.Json.Nodes.JsonObject
        {
            ["version"] = 2,
            ["plugins"] = new System.Text.Json.Nodes.JsonObject
            {
                [ScoutPluginRegistry.FeatureDev] = new System.Text.Json.Nodes.JsonArray([.. installs.Select(i => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["scope"] = i.Scope, ["installPath"] = i.Path, ["version"] = "x" })]),
            },
        }.ToJsonString();
}
