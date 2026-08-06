using ChatAppAI.McpServer.Services;
using ChatAppAI.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<RemoteEngineService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<RemoteEngineTools>();

var app = builder.Build();

app.MapMcp("/mcp");

await app.RunAsync();

