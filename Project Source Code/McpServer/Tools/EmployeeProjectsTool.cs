using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class EmployeeProjectsTool
{
    [McpServerTool(Name = "get_employee_projects", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return projects assigned to a specific employee.")]
    public static async Task<string> GetEmployeeProjectsAsync(
        DatabaseService databaseService,
        ILogger<EmployeeProjectsTool> logger,
        [Description("Employee full name, for example Aarav Mehta or Sophia Carter.")]
        string employeeName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_employee_projects for {EmployeeName}", employeeName);
        return await databaseService.GetEmployeeProjectsAsync(employeeName, cancellationToken);
    }
}
