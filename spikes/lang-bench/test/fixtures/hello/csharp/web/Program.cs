WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{Environment.GetEnvironmentVariable("PORT")}");
WebApplication app = builder.Build();
app.MapGet("/hello", () => Results.Json(new { hello = "world" }));
app.Run();
