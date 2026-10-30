using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>The two MCP configuration files an arm's claude is started with (<c>--mcp-config</c>; <c>after6.sh:33</c>, <c>:34</c>): <see cref="None"/> for an arm without the gate and <see cref="Coai"/> for one with it. Both are TEXT; writing
/// them is <c>make</c>'s (E4.S4), and neither ends in a newline — the writer adds the one the shell's <c>console.log</c> added.
/// <para>
/// <b>The vault key (E1 §8 item 16).</b> The coai server reads its one vault key from its layered configuration, the copied settings file first and its own environment over it (<c>KeyVault.cs:35</c>, <c>:76-95</c> of the product).
/// Under the shell, claude inherited the harness's whole environment and handed it to the server, so an exported key reached it; under D19 the arm's claude never sees <c>COAI_*</c> or a secret-named variable
/// (<see cref="ArmChildEnvironment"/>), so the key travels in the one place the SERVER's environment is written — this file's <c>env</c> member — and never by widening the arm's environment. When the harness's own environment holds a
/// non-empty <see cref="VaultKeyVariable"/>, that one variable is added; nothing else of the harness's environment is (rejected: forwarding every <c>*_KEY</c>, a provider list nobody wrote down; and <c>${VAR}</c> expansion, which claude
/// does from its own environment, which D19 stripped). Providers that sign in through their own CLI homes need nothing.
/// </para>
/// <para>
/// <b>The consequence, stated.</b> An exported key is WRITTEN into <c>r/&lt;token&gt;/mcp-coai.json</c>, which <c>make</c> writes with mode <c>0600</c> — the same exposure as the copied settings file the panel already writes the key into —
/// and, like it, a file the experiment's published record must never carry (E5.S5 excludes <c>mcp-coai.json</c> and <c>coai-data/</c>). Nothing here logs, prints or returns the key anywhere but in the text of that one file.
/// </para>
/// <para>
/// <b>What this guarantees, exactly (E2 §8 item 34): the config REQUESTS the delivery.</b> Whether claude hands a stdio server the <c>env</c> member of its config is claude's behaviour, which no test of this module starts; the proof is
/// milestone M2's key check (E3.S5): the real <c>claude</c> over a generated file, the key absent from the copied settings file so this member is the only road, and the fallback named if the key does not arrive (the copied settings file, the shell's road).
/// </para></summary>
public static class McpConfig
{
    public const string VaultKeyVariable = "COAI_CREDS_KEY";

    /// <summary>The relaxed encoder keeps printable ASCII and printable BMP text as it is (<c>&lt; &amp; &gt; '</c>, accented letters), which is what <c>JSON.stringify</c> writes. It ESCAPES what <c>JSON.stringify</c> writes raw —
    /// characters outside the BMP (an emoji becomes a surrogate-pair escape), DEL, C1 controls, U+2028 and U+2029, U+FEFF and unassigned code points (all observed on .NET 10.0.14) — and replaces a lone surrogate with U+FFFD, where
    /// <c>JSON.stringify</c> writes it as an escape. So the PARSED value equals the input for every well-formed string, and the BYTES equal <c>JSON.stringify</c>'s only for printable BMP text; a lone surrogate is refused (<see cref="Text"/>),
    /// because the replacement would be a different command or a different key.</summary>
    private static readonly JsonWriterOptions Stringify = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 1,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The empty server list, compact as the shell's <c>echo</c> wrote it.</summary>
    public static string None() => """{"mcpServers":{}}""";

    /// <summary>The shell's object, indented by one space like <c>JSON.stringify(…, null, 1)</c>. <paramref name="coai"/> must be a configured gate (a caller that has none never asks for the file).</summary>
    public static string Coai(CoaiFacts coai, ArmPaths paths, IReadOnlyDictionary<string, string> harnessEnvironment)
    {
        if (!coai.Configured)
        {
            throw new ArgumentException("an experiment without a coai gate has no coai config to write", nameof(coai));
        }

        return Written(writer => Server(writer, coai, ServerEnvironment(coai, paths, harnessEnvironment)));
    }

    /// <summary>The text itself, or an <see cref="ArgumentException"/> naming <paramref name="field"/> when it holds a lone surrogate (half of an emoji): JSON text cannot hold one, the writer would put U+FFFD in its place.</summary>
    private static string Text(string value, string field)
    {
        try
        {
            Strict.GetByteCount(value);
            return value;
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException($"the {field} holds a lone surrogate (half of a character outside the BMP) and cannot be written as JSON — it would become U+FFFD, a different value", field);
        }
    }

    private static IEnumerable<(string Name, string Value)> ServerEnvironment(CoaiFacts coai, ArmPaths paths, IReadOnlyDictionary<string, string> harnessEnvironment)
    {
        yield return ("COAI_DATA_DIR", Text(paths.CoaiData, "COAI_DATA_DIR"));
        yield return ("COAI_DATA_SIDE", Text(coai.Side, "COAI_DATA_SIDE"));
        if (harnessEnvironment.GetValueOrDefault(VaultKeyVariable, string.Empty) is { Length: > 0 } key)
        {
            yield return (VaultKeyVariable, Text(key, VaultKeyVariable));
        }
    }

    private static void Server(Utf8JsonWriter writer, CoaiFacts coai, IEnumerable<(string Name, string Value)> environment)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("mcpServers");
        writer.WriteStartObject("coai");
        writer.WriteString("type", "stdio");
        writer.WriteString("command", Text(coai.Binary, "command"));
        writer.WriteStartArray("args");
        writer.WriteEndArray();
        writer.WriteStartObject("env");
        foreach (var (name, value) in environment)
        {
            writer.WriteString(name, value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string Written(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Stringify))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
