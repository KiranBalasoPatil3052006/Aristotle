using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class RunSelectTool
{
    [McpServerTool(Name = "run_select", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Execute one safe SELECT query for public company data. Sensitive and restricted fields are blocked.")]
    public static async Task<string> RunSelectAsync(
        DatabaseService databaseService,
        ILogger<RunSelectTool> logger,
        [Description("A single SELECT statement for public columns only. Sensitive fields, restricted fields, wildcard selects, comments, and multiple statements are blocked.")]
        string query,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: run_select");
        return await databaseService.RunSelectAsync(query, cancellationToken);
    }
}
