using McpServer.Database;
using McpServer.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = ResolveContentRootPath()
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
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
logger.LogInformation("Content root path: {ContentRootPath}", builder.Environment.ContentRootPath);

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

static string ResolveContentRootPath()
{
    var projectRoot = FindProjectRoot();
    if (projectRoot is not null)
    {
        return projectRoot;
    }

    var appDataDirectory = FindDirectoryWithData(AppContext.BaseDirectory);
    if (appDataDirectory is not null)
    {
        return appDataDirectory;
    }

    return Directory.GetCurrentDirectory();
}

static string? FindProjectRoot()
{
    foreach (var searchRoot in GetSearchRoots())
    {
        foreach (var directory in WalkToDriveRoot(searchRoot))
        {
            if (IsMcpServerProjectRoot(directory))
            {
                return directory;
            }

            var nestedProjectRoot = Path.Combine(directory, "McpServer");
            if (IsMcpServerProjectRoot(nestedProjectRoot))
            {
                return nestedProjectRoot;
            }
        }
    }

    return null;
}

static string? FindDirectoryWithData(string searchRoot)
{
    foreach (var directory in WalkToDriveRoot(searchRoot))
    {
        if (File.Exists(Path.Combine(directory, "Data", "employees.db")))
        {
            return directory;
        }
    }

    return null;
}

static IEnumerable<string> GetSearchRoots()
{
    yield return Directory.GetCurrentDirectory();
    yield return AppContext.BaseDirectory;
}

static IEnumerable<string> WalkToDriveRoot(string searchRoot)
{
    var directory = new DirectoryInfo(Path.GetFullPath(searchRoot));

    while (directory is not null)
    {
        yield return directory.FullName;
        directory = directory.Parent;
    }
}

static bool IsMcpServerProjectRoot(string path)
{
    return File.Exists(Path.Combine(path, "McpServer.csproj")) &&
        Directory.Exists(Path.Combine(path, "Data"));
}
