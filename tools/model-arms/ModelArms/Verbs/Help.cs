using System.Reflection;
using Bench.Cli;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>The three verbs that need no experiment root: <c>version</c>, <c>help</c> and the answer to a verb the dispatch does not know.</summary>
public static class Help
{
    private const string Name = "model-arms";

    public static Task<int> Version(VerbInput input)
    {
        var version = typeof(Help).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        input.Output.WriteLine($"{Name} {version}");
        return Task.FromResult(ExitCodes.Pass);
    }

    public static Task<int> Usage(VerbInput input)
    {
        input.Output.WriteLine($"{Name} <verb> [arguments] [flags]");
        foreach (var usage in ModelArmsCommandLine.Usages)
        {
            input.Output.WriteLine($"  {Name} {usage}");
        }

        input.Output.WriteLine("A verb that reads an experiment takes --experiment <root>, else the MODEL_ARMS_EXPERIMENT variable.");
        return Task.FromResult(ExitCodes.Pass);
    }

    public static Task<int> Unknown(VerbInput input)
    {
        input.Error.WriteLine($"{Name}: '{input.Verb}' is not a verb — {Name} takes: {string.Join(", ", ModelArmsCommandLine.Verbs)}");
        return Task.FromResult(ExitCodes.Configuration);
    }
}
