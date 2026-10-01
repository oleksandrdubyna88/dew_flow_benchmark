namespace Bench.Tests.Infrastructure;

/// <summary>The checkout the test binary was built from — found by the solution file above it, so a test can read a checked-in
/// sample without guessing a relative path.</summary>
internal static class TestRepository
{
    public static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("no *.slnx above the test binary — a checked-in sample is read from the checkout, and its place cannot be guessed");
    }
}
