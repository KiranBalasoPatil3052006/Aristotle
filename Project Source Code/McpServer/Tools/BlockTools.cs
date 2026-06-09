using System.ComponentModel;
using McpServer.Database;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class BlockTools
{
    [McpServerTool(Name = "get_blocks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return all company blocks with campus, building, floor, wing, purpose, primary department, and employee count.")]
    public static async Task<string> GetBlocksAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_blocks");
        return await databaseService.GetBlocksAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_block_employees", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return public employee details and seats for employees assigned to a block by block code or block name.")]
    public static async Task<string> GetBlockEmployeesAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        [Description("Block code or block name, for example BLK-HR, People Block, BLK-A, or Atria Block.")]
        string blockNameOrCode,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_block_employees for {BlockNameOrCode}", blockNameOrCode);
        return await databaseService.GetBlockEmployeesAsync(blockNameOrCode, cancellationToken);
    }

    [McpServerTool(Name = "get_employee_block", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return public block, location, seat, department, and work email details for one employee. Use all for every employee.")]
    public static async Task<string> GetEmployeeBlockAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        [Description("Employee full name, for example Aarav Mehta or Sophia Carter. Use all to return every employee.")]
        string employeeName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_employee_block for {EmployeeName}", employeeName);
        return await databaseService.GetEmployeeBlockAsync(employeeName, cancellationToken);
    }

    [McpServerTool(Name = "get_hr_block", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return the Human Resources block location and employee count.")]
    public static async Task<string> GetHrBlockAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_hr_block");
        return await databaseService.GetHrBlockAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_company_location_map", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return every employee with public department, block, seat, and project information.")]
    public static async Task<string> GetCompanyLocationMapAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_company_location_map");
        return await databaseService.GetCompanyLocationMapAsync(cancellationToken);
    }

    [McpServerTool(Name = "export_company_documentation_data", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Export public documentation data for all company tables. Sensitive employee fields are excluded, and passwords/password hashes are never returned.")]
    public static async Task<string> ExportCompanyDocumentationDataAsync(
        DatabaseService databaseService,
        ILogger<BlockTools> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: export_company_documentation_data");
        return await databaseService.GetCompanyDocumentationDataAsync(cancellationToken);
    }
}
