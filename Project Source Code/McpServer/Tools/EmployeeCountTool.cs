using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class EmployeeCountTool
{
    [McpServerTool(Name = "get_employee_count", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return total employee count.")]
    public static async Task<string> GetEmployeeCountAsync(
        DatabaseService databaseService,
        ILogger<EmployeeCountTool> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_employee_count");
        return await databaseService.GetEmployeeCountAsync(cancellationToken);
    }
}
