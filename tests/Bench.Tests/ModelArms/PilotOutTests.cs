using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Where the attended pilots' scaffolds may write (<see cref="PilotOut"/>): only under the folder the operator names in <c>BENCH_PILOT_OUT</c>, which must be absolute, an existing folder, outside the temp root (a WSL restart empties it; no sweep may collect what must outlive the run)
/// and outside every git checkout. Every refusal names <c>BENCH_PILOT_OUT</c>, so a scaffold that fails says what to set.
/// <para>
/// What this does NOT prove: that a scaffold calls <see cref="PilotOut.Required"/> first (the scaffolds are run by hand, and killed after their first child in <c>PilotScaffoldKillTests</c>), or a folder whose real path the kernel cannot place (refused, naming why).
/// </para></summary>
public sealed class PilotOutTests
{
    private const string Variable = "BENCH_PILOT_OUT";

    private static string NoCheckout(string path) => string.Empty;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_unset_or_empty_variable_is_refused_naming_it(string? value)
    {
        using var elsewhere = new TempFolder();

        PilotOut.Resolve(value, elsewhere.Path, NoCheckout).Reason().Should().Contain(Variable);
    }

    [Fact]
    public void A_relative_path_is_refused_naming_the_variable()
    {
        using var elsewhere = new TempFolder();

        PilotOut.Resolve("x", elsewhere.Path, NoCheckout).Reason().Should().Contain(Variable).And.Contain("absolute");
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_refused_naming_it()
    {
        using var temp = new TempFolder();
        using var elsewhere = new TempFolder();
        var missing = temp.Combine("not-made");

        PilotOut.Resolve(missing, elsewhere.Path, NoCheckout).Reason().Should().Contain(Variable).And.Contain(missing);
    }

    [Fact]
    public void A_folder_at_or_under_the_temp_root_is_refused_even_through_a_link()
    {
        using var root = new TempFolder();
        var under = root.Folder("below", "again");
        using var elsewhere = new TempFolder();
        var link = elsewhere.Combine("link-to-the-root");
        Directory.CreateSymbolicLink(link, root.Folder("target"));

        PilotOut.Resolve(root.Path, root.Path, NoCheckout).Reason().Should().Contain(Variable).And.Contain("temp");
        PilotOut.Resolve(under, root.Path, NoCheckout).Reason().Should().Contain(Variable).And.Contain("temp");
        PilotOut.Resolve(link, root.Path, NoCheckout).Reason().Should().Contain(Variable, "the link is judged where it points, not by its spelling");
    }

    [Fact]
    public void A_folder_at_or_under_the_real_temp_root_is_refused()
    {
        using var folder = new TempFolder();

        PilotOut.Resolve(TempDir.Root, TempDir.Root, NoCheckout).Reason().Should().Contain(Variable);
        PilotOut.Resolve(folder.Path, TempDir.Root, NoCheckout).Reason().Should().Contain(Variable, "every owned folder of a run is under the temp root");
    }

    [Fact]
    public void A_folder_inside_a_git_checkout_is_refused_naming_the_checkout()
    {
        using var folder = new TempFolder();
        using var elsewhere = new TempFolder();

        PilotOut.Resolve(folder.Path, elsewhere.Path, _ => "/work/checkout").Reason().Should().Contain(Variable).And.Contain("/work/checkout");
    }

    [Fact]
    public void An_existing_absolute_folder_outside_the_temp_root_and_every_checkout_is_accepted()
    {
        using var folder = new TempFolder();
        using var elsewhere = new TempFolder();

        PilotOut.Resolve(folder.Path, elsewhere.Path, NoCheckout).Ok().Should().Be(folder.Path);
    }
}
