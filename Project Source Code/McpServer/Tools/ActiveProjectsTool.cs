using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class ActiveProjectsTool
{
    [McpServerTool(Name = "get_active_projects", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return all active projects.")]
    public static async Task<string> GetActiveProjectsAsync(
        DatabaseService databaseService,
        ILogger<ActiveProjectsTool> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_active_projects");
        return await databaseService.GetActiveProjectsAsync(cancellationToken);
    }
}
