using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class DepartmentSummaryTool
{
    [McpServerTool(Name = "get_department_summary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("For each department, return department name and employee count.")]
    public static async Task<string> GetDepartmentSummaryAsync(
        DatabaseService databaseService,
        ILogger<DepartmentSummaryTool> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_department_summary");
        return await databaseService.GetDepartmentSummaryAsync(cancellationToken);
    }
}
