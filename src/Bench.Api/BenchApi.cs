using Bench.Application;
using Bench.Contracts;
using Bench.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bench.Api;

/// <summary>The HTTP door onto the same use cases the CLI drives. Thin by construction: if an endpoint
/// ever needs logic of its own, that logic belongs in the Application layer where both surfaces can
/// reach it.</summary>
public static class BenchApi
{
    public static IEndpointRouteBuilder MapBenchApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/health", () => Results.Ok(new HealthDto("ok")));

        group.MapPost("/plan", (PlanRequestDto request) =>
            PlanRequestHandler.Handle(request).Match(
                Results.Ok,
                reason => Results.BadRequest(new ProblemDto(reason))));

        return app;
    }
}

public sealed record HealthDto(string Status);

public sealed record ProblemDto(string Reason);
