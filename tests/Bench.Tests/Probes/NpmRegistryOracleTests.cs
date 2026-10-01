using System.Net;
using Bench.Domain.Probes;
using Bench.Infrastructure.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>D7's adapter against a scripted handler: one GET of the registry's <c>latest</c>, its <c>version</c> frozen as a
/// registry-sourced oracle; every way the read can fail is a refusal naming the cause — never an empty version.</summary>
public sealed class NpmRegistryOracleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_latest_version_is_read_from_one_get_of_the_registry()
    {
        var handler = new Scripted(HttpStatusCode.OK, """{"name":"@openai/codex","version":"0.52.0","dist":{}}""");

        var oracle = (await new NpmRegistryOracle(new HttpClient(handler)).LatestAsync(Ct)).Ok();

        oracle.Version.Should().Be("0.52.0");
        oracle.Source.Should().Be(OracleSource.Registry);
        handler.Requests.Should().Equal([NpmRegistryOracle.LatestUrl]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", "HTTP 429")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", "HTTP 503")]
    [InlineData(HttpStatusCode.OK, "<html>maintenance</html>", "not JSON")]
    [InlineData(HttpStatusCode.OK, """{"name":"@openai/codex"}""", "no 'version'")]
    [InlineData(HttpStatusCode.OK, """{"version":"latest"}""", "not a version")]
    public async Task A_read_that_does_not_yield_a_version_is_refused_naming_the_cause(HttpStatusCode status, string body, string cause)
    {
        var read = await new NpmRegistryOracle(new HttpClient(new Scripted(status, body))).LatestAsync(Ct);

        read.Reason().Should().Contain(cause);
    }

    [Fact]
    public async Task An_unreachable_registry_is_a_refusal_not_an_exception()
    {
        var read = await new NpmRegistryOracle(new HttpClient(new Unreachable())).LatestAsync(Ct);

        read.Reason().Should().Contain("unreachable").And.Contain("No such host is known");
    }

    private sealed class Scripted(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known. (registry.npmjs.org:443)");
    }
}
