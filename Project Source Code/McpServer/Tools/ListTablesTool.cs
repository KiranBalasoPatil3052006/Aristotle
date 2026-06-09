using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class ListTablesTool
{
    [McpServerTool(Name = "list_tables", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return all database tables.")]
    public static async Task<string> ListTablesAsync(
        DatabaseService databaseService,
        ILogger<ListTablesTool> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: list_tables");
        return await databaseService.ListTablesAsync(cancellationToken);
    }
}
