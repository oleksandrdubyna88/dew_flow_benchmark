using Microsoft.AspNetCore.Mvc.Testing;

namespace SnapTests;

public sealed class SmokeTests
{
    [Fact]
    public void The_test_host_runs() => Assert.Equal(2, 1 + 1);

    [Fact]
    public async Task The_web_app_answers_in_memory_through_the_test_host()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal("ok", await client.GetStringAsync("/", TestContext.Current.CancellationToken));
    }
}
