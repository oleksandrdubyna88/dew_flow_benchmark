using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The one predicate both environments ask (plan D19, ledger 2.2; E4.S0): a name is secret-shaped when the shared <c>CoaiEnvironment.IsSecretName</c> says so, or it is one of the whole names, ends with one of the endings, or is an <c>npm_config_*</c> that names an auth setting — whatever the case.
/// <para>
/// What this does NOT prove: a secret under an innocent name (<c>KEEP_ME=hunter2</c> passes, as it always did — the rule is by name); or which environment calls the predicate — that is <c>ArmChildEnvironmentTests</c>' and <c>StepRunnerTests</c>'. The moved lists lost no name: <c>TargetEnvironmentTests</c> run unedited over them.
/// </para></summary>
public sealed class SecretShapedTests
{
    /// <summary>Rows DERIVED from the lists the predicate holds (so a name added to a list is a row at once), plus the shared rule's own and the npm auth settings, and <c>GITHUB_PAT</c> fixed: without it, removing <c>_PAT</c> from the ending list would also remove the derived row and nothing would be red here.</summary>
    public static TheoryData<string> SecretShapedNames()
    {
        var rows = new TheoryData<string>();
        string[] names =
        [
            .. SecretShaped.WholeNames,
            .. SecretShaped.Endings.Select(ending => "X" + ending),
            "MY_KEY", "MY_TOKEN", "MY_SECRET", "MY_PASSWORD", "COAI_CREDS_KEY", "GITHUB_PAT", "npm_config__authToken", "npm_config_//registry/:_authToken",
        ];
        foreach (var name in names)
        {
            rows.Add(name);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(SecretShapedNames))]
    public void The_predicate_is_the_shared_list_or_the_targets_own_rules_whatever_the_case(string name)
    {
        foreach (var spelling in new[] { name, name.ToLowerInvariant(), Mixed(name) })
        {
            SecretShaped.IsSecretShaped(spelling).Should().BeTrue($"IsSecretShaped(\"{spelling}\") is a secret-shaped name, whatever its case");
        }
    }

    [Theory]
    [InlineData("npm_config_cache"), InlineData("npm_config_registry"), InlineData("NODE_PATH"), InlineData("PATHEXT"), InlineData("MY_PATH"), InlineData("PWD"), InlineData("OLDPWD"), InlineData("BYPASS"), InlineData("COMPASS"), InlineData("PASS"), InlineData("KEYS"), InlineData("AUTHOR"), InlineData("OAUTH_ENABLED"), InlineData("DATABASE_HOST"), InlineData("HTTP_PROXY"), InlineData("KEEP_ME")]
    public void A_name_that_only_looks_like_one_of_them_is_not_secret_shaped(string name)
    {
        SecretShaped.IsSecretShaped(name).Should().BeFalse($"IsSecretShaped(\"{name}\") only looks like a secret: the rule ends with a name, it does not search one — the file's one positive-direction test");
    }

    private static string Mixed(string name) =>
        string.Concat(name.Select((letter, index) => index % 2 == 0 ? char.ToUpperInvariant(letter) : char.ToLowerInvariant(letter)));
}
