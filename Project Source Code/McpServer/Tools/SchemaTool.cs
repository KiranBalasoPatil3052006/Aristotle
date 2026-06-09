using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class SchemaTool
{
    [McpServerTool(Name = "get_schema", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return schema and columns for the specified table.")]
    public static async Task<string> GetSchemaAsync(
        DatabaseService databaseService,
        ILogger<SchemaTool> logger,
        [Description("Table name. Allowed values: Blocks, Departments, DepartmentBlocks, Employees, EmployeeBlocks, Projects, EmployeeProjects.")]
        string tableName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_schema for {TableName}", tableName);
        return await databaseService.GetSchemaAsync(tableName, cancellationToken);
    }
}
