using McpServer.Database;
using McpServer.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
    options.IncludeScopes = false;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AuthorizationService>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");
var databaseService = host.Services.GetRequiredService<DatabaseService>();

logger.LogInformation("Starting MCP Server initialization...");

try
{
    logger.LogInformation("Initializing database...");
    await databaseService.InitializeAsync(CancellationToken.None);
    logger.LogInformation("Database initialized successfully");

    if (args.Contains("--init-db", StringComparer.OrdinalIgnoreCase))
    {
        logger.LogInformation("Database initialized at {DatabasePath}", databaseService.DatabasePath);
        return;
    }

    logger.LogInformation("Starting host...");
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled exception in MCP server");
    Console.Error.WriteLine($"FATAL: {ex}");
    Environment.Exit(1);
}
 
