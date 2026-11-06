var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{Environment.GetEnvironmentVariable("PORT")}");
var app = builder.Build();
app.Run(async ctx =>
{
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync($"{{\"path\":\"{ctx.Request.Path}\",\"method\":\"{ctx.Request.Method}\"}}");
});
app.Run();
