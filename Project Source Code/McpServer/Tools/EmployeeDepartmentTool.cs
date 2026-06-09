using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class EmployeeDepartmentTool
{
    [McpServerTool(Name = "get_employees_by_department", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return all employees belonging to a department.")]
    public static async Task<string> GetEmployeesByDepartmentAsync(
        DatabaseService databaseService,
        ILogger<EmployeeDepartmentTool> logger,
        [Description("Department name, for example Engineering, Finance, Human Resources, Marketing, or Sales.")]
        string departmentName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_employees_by_department for {DepartmentName}", departmentName);
        return await databaseService.GetEmployeesByDepartmentAsync(departmentName, cancellationToken);
    }
}
