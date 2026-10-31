namespace Bench.Tests.ModelArms;

/// <summary>The recorded files of <c>Fixtures/model-arms</c>, read from where the test project copies them (its <c>Fixtures\**</c> item). Shared so the step runner's tests
/// read the same bytes the classifier's tests do.</summary>
internal static class ModelArmsFixtures
{
    /// <summary>The path of the fixture <paramref name="name"/> beside the test binary.</summary>
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "model-arms", name);

    /// <summary>The whole text of the fixture <paramref name="name"/>.</summary>
    public static string Read(string name) => File.ReadAllText(Path(name));
}
