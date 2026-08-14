namespace Bench.Cli;

/// <summary>The entry point, kept thin on purpose: parse, dispatch, exit. Everything a command does is
/// a use case in the Application layer, so the CLI and the API can never drift into two different
/// behaviours wearing one name.</summary>
public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>The testable seam: writers are injected so the contract can be asserted without a
    /// process launch — and the exit-code contract is the part most worth asserting.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var command = CommandLine.Parse(args);

        return command.Verb switch
        {
            "plan" => PlanCommand.Run(command, output, error),
            "version" => Version(output),
            "" or "help" => Help(output),
            _ => Unknown(command.Verb, error),
        };
    }

    private static int Version(TextWriter output)
    {
        output.WriteLine("bench 0.1.0");
        return ExitCodes.Pass;
    }

    private static int Help(TextWriter output)
    {
        output.WriteLine("bench — measure any repository at any commit, through any engine");
        output.WriteLine();
        output.WriteLine("  bench plan --repo <url> --commit <40-hex> --suite-file <path>");
        output.WriteLine("             [--repeats N] [--subjects id@local,id@cloud] [--lanes a,b]");
        output.WriteLine("             [--engine qln|mindex|http|noretrieval] [--exclude glob,glob] [--json]");
        output.WriteLine("  bench version");
        output.WriteLine();
        output.WriteLine("exit codes: 0 pass · 1 regression · 3 environment · 4 configuration · 5 no report");
        return ExitCodes.Pass;
    }

    private static int Unknown(string verb, TextWriter error)
    {
        error.WriteLine($"bench: unknown command '{verb}' — try 'bench help'");
        return ExitCodes.Configuration;
    }
}
