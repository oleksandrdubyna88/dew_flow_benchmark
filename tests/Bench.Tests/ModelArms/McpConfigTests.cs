using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The two MCP configuration files an arm's claude is started with (<c>--mcp-config</c>): <c>mcp-none.json</c> for the arms without the gate and <c>mcp-coai.json</c> for the arms with it, the shell's shapes
/// (<c>after6.sh:33</c>, <c>:34</c>). The one new member is the vault key (E1 §8 item 16, E2 §8 item 14): the coai server reads its key from its own environment, and under D19 the arm's claude has none of
/// <c>COAI_*</c>, so the config is the one place the key can be requested from.
/// <para>
/// What this does NOT prove (E2 §8 item 34): that claude hands a stdio server the <c>env</c> member of its config — that is claude's behaviour, and no test here starts claude. These tests prove what the module controls:
/// the written text REQUESTS the key for the coai server, and the arm's own environment never carries it. The proof that the key arrives is milestone M2's key check (E3.S5) with the real <c>claude</c>.
/// </para></summary>
public sealed class McpConfigTests
{
    private static readonly ArmPaths Paths = new("/e", "tok");

    private static readonly CoaiFacts Coai = new("/c/coai-mcp", "/v/settings.json", "wsl-ubuntu");

    private static IReadOnlyDictionary<string, string> Env(params (string Name, string Value)[] variables) =>
        variables.ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.Ordinal);

    /// <summary>The <c>coai</c> server of a config text, asserted present first so a missing server is one clear failure and not a null dereference.</summary>
    private static JsonObject Server(string text)
    {
        var server = JsonNode.Parse(text)!["mcpServers"]!["coai"];
        server.Should().NotBeNull("the config names the coai server");
        return server!.AsObject();
    }

    private static JsonObject ServerEnv(string text) => Server(text)["env"]!.AsObject();

    private static IReadOnlyList<string> Names(JsonObject variables) => [.. variables.Select(member => member.Key)];

    /// <summary>The path as it sits inside a JSON string on this host (a Windows separator is escaped).</summary>
    private static string Json(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    [Fact]
    public void Mcp_coai_json_requests_the_vault_key_for_the_coai_server_and_the_arm_never_sees_it()
    {
        var harness = Env((McpConfig.VaultKeyVariable, "k-the-vault-key"), ("HOME", "/h"));

        var env = ServerEnv(McpConfig.Coai(Coai, Paths, harness));

        env.ToJsonString().Should().Be(
            new JsonObject { ["COAI_DATA_DIR"] = Paths.CoaiData, ["COAI_DATA_SIDE"] = "wsl-ubuntu", [McpConfig.VaultKeyVariable] = "k-the-vault-key" }.ToJsonString(),
            "the data folder, the side and — only because the harness holds one — the key, in that order and nothing else (not HOME)");

        ArmChildEnvironment.Of(harness, Paths).Keys.Should().NotContain(
            name => string.Equals(name, McpConfig.VaultKeyVariable, StringComparison.OrdinalIgnoreCase),
            "the key travels in the config file's env member and never in the arm's own environment (D19)");

        Names(ServerEnv(McpConfig.Coai(Coai, Paths, Env(("HOME", "/h"))))).Should().Equal("COAI_DATA_DIR", "COAI_DATA_SIDE");
        Names(ServerEnv(McpConfig.Coai(Coai, Paths, Env((McpConfig.VaultKeyVariable, ""))))).Should().Equal(
            ["COAI_DATA_DIR", "COAI_DATA_SIDE"], "an empty key is no key: nothing is requested");
    }

    [Fact]
    public void A_key_with_quotes_stays_one_value_of_the_config()
    {
        const string Hostile = "k\",\"COAI_DATA_DIR\":\"/elsewhere\\";

        var env = ServerEnv(McpConfig.Coai(Coai, Paths, Env((McpConfig.VaultKeyVariable, Hostile))));

        env[McpConfig.VaultKeyVariable]!.GetValue<string>().Should().Be(Hostile);
        env["COAI_DATA_DIR"]!.GetValue<string>().Should().Be(Paths.CoaiData, "the key is data: it cannot add or replace a member");
    }

    [Fact]
    public void Mcp_coai_json_is_the_shells_shape()
    {
        var text = McpConfig.Coai(Coai, Paths, Env());

        var expected = string.Join(
            "\n",
            "{",
            " \"mcpServers\": {",
            "  \"coai\": {",
            "   \"type\": \"stdio\",",
            "   \"command\": \"/c/coai-mcp\",",
            "   \"args\": [],",
            "   \"env\": {",
            $"    \"COAI_DATA_DIR\": \"{Json(Paths.CoaiData)}\",",
            "    \"COAI_DATA_SIDE\": \"wsl-ubuntu\"",
            "   }",
            "  }",
            " }",
            "}");
        text.Should().Be(expected, "JSON.stringify(…, null, 1) of the shell's object: indented by one space per level, an empty list as []");
        Server(text)["type"]!.GetValue<string>().Should().Be("stdio");
    }

    [Fact]
    public void Mcp_coai_json_keeps_a_path_readable_as_JSON_stringify_does()
    {
        var odd = new CoaiFacts("/c/o\"d <&> ünï/coai-mcp", "/v/settings.json", "wsl-ubuntu");

        var text = McpConfig.Coai(odd, Paths, Env());

        Server(text)["command"]!.GetValue<string>().Should().Be(odd.Binary);
        text.Should().Contain("ünï").And.Contain("<&>", "JSON.stringify writes non-ASCII and < & > as they are; only the quote and the backslash are escaped");
    }

    [Fact]
    public void A_gate_less_experiment_has_no_coai_config_to_write()
    {
        var act = () => McpConfig.Coai(CoaiFacts.None, Paths, Env());

        act.Should().Throw<ArgumentException>("an experiment without a gate never asks for the file; writing one with no binary is a caller's bug");
    }

    [Fact]
    public void Mcp_none_is_an_empty_server_list()
    {
        McpConfig.None().Should().Be("""{"mcpServers":{}}""");
    }
}
