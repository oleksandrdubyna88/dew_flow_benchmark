using System.Text;
using Bench.Domain;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>Where the console finds a brief template: the experiment root's own copy when the operator put one there, else
/// the one shipped beside the binary. The caller names a template stem, never a file: the name comes from
/// <see cref="BriefTemplates.NameOf"/>, which refuses a separator and <c>..</c>, so no operator text can reach outside the two folders. A template is read whole,
/// as strict UTF-8. An override that exists and cannot be read is a refusal naming it — never a silent fall back to the
/// shipped file, which would run an experiment on text the operator did not mean. What a template may contain is the
/// filler's business.</summary>
public sealed class BriefTemplateFiles(string shippedDirectory, string overridesDirectory)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public Outcome<string> Read(string stem) => BriefTemplates.NameOf(stem).Match(ReadNamed, Outcome<string>.Failure);

    private Outcome<string> ReadNamed(string templateName) =>
        new[] { overridesDirectory, shippedDirectory }
            .Select(directory => Path.Combine(directory, templateName))
            .FirstOrDefault(File.Exists) is { } path
            ? Load(path)
            : Outcome<string>.Failure($"no brief template '{templateName}' in {overridesDirectory} or {shippedDirectory}");

    private static Outcome<string> Load(string path)
    {
        try
        {
            return Outcome<string>.Success(File.ReadAllText(path, StrictUtf8));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Outcome<string>.Failure($"the brief template {path} cannot be read as UTF-8 text: {ex.Message}");
        }
    }
}
