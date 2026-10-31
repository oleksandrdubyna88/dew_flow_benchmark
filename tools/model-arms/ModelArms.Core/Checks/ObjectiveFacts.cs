using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Checks;

/// <summary>How each of the four stages of <c>checks</c> ended, as the exit code of the tool: <c>Unavailable</c> when it was not recorded — killed at its wall, never started, or a re-read of logs nobody ran — which is never the same fact as 0.
/// <c>Suite</c> is the main suite (the report's <c>mcp.exit</c>) and <c>Extension</c> the extension's (its <c>ts.exit</c>).</summary>
public sealed record StageExits(CapturedCount Build, CapturedCount Suite, CapturedCount Format, CapturedCount Extension);

/// <summary>Everything <see cref="ObjectiveReport"/> needs, gathered by the caller (E3.S5) so that nothing here reads a file or runs git: the arm and its short head, the commits since <c>base</c> (<c>Unavailable</c> when git did not say), the count of
/// uncommitted files, the exits, the TEXT of the suite's log, of the CTRF (a failed outcome says why it could not be read — absent, a link, too large) and of the extension's log, the scenarios of <c>scenarios.json</c> with whether the target's tests
/// name each (<see cref="Present"/>, keyed by scenario id; an id with no entry reads as not present), the baseline, <c>suite.namePrefix</c>, the texts of <c>git diff --numstat</c> and <c>--shortstat</c> over <c>base..HEAD</c>, and the steps
/// that ended on their deadline.</summary>
public sealed record ObjectiveFacts(
    string Arm,
    string HeadShort,
    CapturedCount Commits,
    int UncommittedFiles,
    StageExits Exits,
    string SuiteLog,
    Outcome<string> Ctrf,
    string ExtensionLog,
    IReadOnlyList<Scenario> Scenarios,
    IReadOnlyDictionary<string, bool> Present,
    Baseline Baseline,
    string NamePrefix,
    string Numstat,
    string Shortstat,
    IReadOnlyList<string> StepsOnDeadline);
