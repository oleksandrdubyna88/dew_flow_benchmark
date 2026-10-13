using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Scouts;

/// <summary>Claude's plugin registry (<c>~/.claude/plugins/installed_plugins.json</c>, version 2), read for ONE fact: which version folder of a
/// plugin the operator's sessions load. The cache (<c>~/.claude/plugins/cache/&lt;marketplace&gt;/&lt;plugin&gt;/</c>) keeps every version ever
/// installed, fourteen of them on 2026-10-13, so <c>--plugin-dir</c> cannot name the cache — it names the registry's USER-scope install,
/// the one a session loads whatever project it runs in. A local-scope entry is bound to one project path and is not the scouts' condition.</summary>
public static class ScoutPluginRegistry
{
    public const string FeatureDev = "feature-dev@claude-plugins-official";

    public const string UserScope = "user";

    /// <summary>The user-scope install path of <paramref name="pluginId"/>, or a refusal naming what the registry lacks.</summary>
    public static Outcome<string> InstallPath(string registryJson, string pluginId)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(registryJson, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            return Outcome<string>.Failure($"the plugin registry is not JSON — {ex.Message}");
        }

        if (root?["plugins"]?[pluginId] is not JsonArray installs)
        {
            return Outcome<string>.Failure($"the plugin registry lists no install of {pluginId} — install it for the user (claude plugin install {pluginId}) so there is one plugin directory to pin");
        }

        var path = installs.OfType<JsonObject>().Where(i => Text(i, "scope") == UserScope).Select(i => Text(i, "installPath")).FirstOrDefault(p => p.Length > 0) ?? string.Empty;

        return path.Length > 0
            ? Outcome<string>.Success(path)
            : Outcome<string>.Failure($"the plugin registry lists {installs.Count} install(s) of {pluginId} and none at user scope — the scouts pin the user-scope install, the one the operator's sessions load in every project; enable the plugin for the user");
    }

    private static string Text(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
}
