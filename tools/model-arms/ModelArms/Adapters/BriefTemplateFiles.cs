using System.Text;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>Where the console finds a brief template: the experiment root's own copy by name when the operator put one
/// there, else the one shipped beside the binary. A template is read whole, as UTF-8 (the shipped ones are byte for byte
/// the part-6 harness's). Reading is the console's job; what a template may contain is the filler's.</summary>
public sealed class BriefTemplateFiles(string shippedDirectory, string overridesDirectory)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public Outcome<string> Read(string templateName) =>
        new[] { overridesDirectory, shippedDirectory }
            .Select(directory => Path.Combine(directory, templateName))
            .FirstOrDefault(File.Exists) is { } path
            ? Outcome<string>.Success(File.ReadAllText(path, Utf8))
            : Outcome<string>.Failure($"no brief template '{templateName}' in {overridesDirectory} or {shippedDirectory}");
}
