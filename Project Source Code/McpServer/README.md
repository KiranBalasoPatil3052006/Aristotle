# Company SQLite MCP Server

Production-ready MCP server for Claude Desktop that exposes safe, read-only access to a seeded company SQLite database.

## Tech Stack

- C# / .NET 8
- SQLite via `Microsoft.Data.Sqlite`
- Official MCP C# SDK via `ModelContextProtocol`
- Stdio transport for Claude Desktop
- Visual Studio Code friendly console project

## Project Structure

```text
McpServer/
|-- Program.cs
|-- McpServer.csproj
|-- Database/
|   |-- DatabaseService.cs
|-- Models/
|   |-- Employee.cs
|   |-- Department.cs
|   |-- Project.cs
|-- Tools/
|   |-- ListTablesTool.cs
|   |-- SchemaTool.cs
|   |-- RunSelectTool.cs
|   |-- BlockTools.cs
|   |-- EmployeeCountTool.cs
|   |-- EmployeeDepartmentTool.cs
|   |-- DepartmentSummaryTool.cs
|   |-- ActiveProjectsTool.cs
|   |-- EmployeeProjectsTool.cs
|-- Data/
|   |-- employees.db
|-- README.md
```

## Code File Guide

- `Program.cs` configures dependency injection, stderr logging, stdio MCP transport, tool discovery, and database initialization.
- `Database/DatabaseService.cs` owns SQLite connection handling, schema creation, seed data, query validation, safe SELECT execution, and JSON result formatting.
- `Models/Employee.cs`, `Models/Department.cs`, and `Models/Project.cs` provide simple typed records for the domain model.
- `Tools/ListTablesTool.cs` exposes `list_tables()`.
- `Tools/SchemaTool.cs` exposes `get_schema(tableName)`.
- `Tools/RunSelectTool.cs` exposes `run_select(query)` with SELECT-only validation.
- `Tools/BlockTools.cs` exposes block, employee-location, HR block, company-location-map, and documentation-export tools.
- `Tools/EmployeeCountTool.cs` exposes `get_employee_count()`.
- `Tools/EmployeeDepartmentTool.cs` exposes `get_employees_by_department(departmentName)`.
- `Tools/DepartmentSummaryTool.cs` exposes `get_department_summary()`.
- `Tools/ActiveProjectsTool.cs` exposes `get_active_projects()`.
- `Tools/EmployeeProjectsTool.cs` exposes `get_employee_projects(employeeName)`.

## NuGet Packages

```powershell
dotnet add package ModelContextProtocol --version 1.4.0
dotnet add package Microsoft.Data.Sqlite --version 10.0.8
dotnet add package Microsoft.Extensions.Hosting --version 10.0.8
dotnet add package Microsoft.Extensions.Logging.Console --version 10.0.8
```

## Database

The server creates and seeds `Data/employees.db` automatically at startup.

Tables:

- `Blocks`: `BlockId`, `BlockCode`, `BlockName`, `Campus`, `Building`, `Floor`, `Wing`, `LocationDescription`, `Purpose`, `IsActive`
- `Departments`: `DepartmentId`, `DepartmentName`
- `DepartmentBlocks`: `DepartmentId`, `BlockId`, `IsPrimary`, `Notes`
- `Employees`: `EmployeeId`, `Name`, `Email`, `DepartmentId`, plus sensitive employee fields
- `EmployeeBlocks`: `EmployeeId`, `BlockId`, `SeatNumber`, `DeskLocation`, `AssignedFrom`
- `Projects`: `ProjectId`, `ProjectName`, `Status`
- `EmployeeProjects`: `EmployeeId`, `ProjectId`

Seed data:

- 6 blocks with campus/building/floor/wing location details
- 5 departments
- 26 employees
- 26 employee block assignments
- 10 projects
- 26 employee-project assignments

## Security Behavior

`run_select(query)` applies layered safety checks:

- Requires the query to start with `SELECT`
- Blocks multiple statements and semicolons
- Blocks SQL comments
- Blocks `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, and other write/admin keywords
- Executes ad hoc queries through a read-only SQLite connection
- Returns at most 100 rows per tool call
- Blocks sensitive employee fields unless an authorized member logs in and uses `get_sensitive_employee_data`
- Always blocks restricted data such as passwords, password hashes, API keys, secrets, and tokens

`export_company_documentation_data()` returns public documentation data only. It excludes sensitive employee fields and never returns password hashes.

All app-specific lookup tools use parameterized SQL.

## Install

```powershell
cd "C:\Users\KIRAN BALASO PATIL\Desktop\MCP\McpServer"
dotnet restore
```

## Build

```powershell
dotnet build
```

## Initialize Database

```powershell
dotnet run -- --init-db
```

This creates:

```text
C:\Users\KIRAN BALASO PATIL\Desktop\MCP\McpServer\Data\employees.db
```

## Run As MCP Server

```powershell
dotnet run
```

The process listens over stdio. It is meant to be launched by Claude Desktop, not used as a normal HTTP server.

## Claude Desktop Configuration

Edit this file on Windows:

```text
%APPDATA%\Claude\claude_desktop_config.json
```

Example:

```json
{
  "mcpServers": {
    "company-sqlite": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\KIRAN BALASO PATIL\\Desktop\\MCP\\McpServer\\McpServer.csproj"
      ],
      "env": {
        "EMPLOYEES_DB_PATH": "C:\\Users\\KIRAN BALASO PATIL\\Desktop\\MCP\\McpServer\\Data\\employees.db"
      }
    }
  }
}
```

Restart Claude Desktop after saving the file.

## Testing

Build and initialize:

```powershell
dotnet build
dotnet run -- --init-db
```

Then connect from Claude Desktop and try these prompts:

- `List the available database tables.`
- `Show me the schema for Employees.`
- `How many employees are in the company?`
- `List all Engineering employees.`
- `Give me the department summary.`
- `Show active projects.`
- `Which projects are assigned to Aarav Mehta?`
- `Show all company blocks.`
- `Where is the HR block?`
- `Where does Kiran sit?`
- `Show every employee with department, block, seat, and project information.`
- `Export company documentation data.`
- `Run this SQL: SELECT Name, Email FROM Employees ORDER BY Name`
- `Try this unsafe SQL and explain what happens: DROP TABLE Employees`
- `Try to show password hashes and explain why access is denied.`

## Error Handling

The server returns MCP tool errors for:

- Database connection failures
- Invalid table names
- Invalid SQL
- Non-SELECT SQL
- SQL injection patterns such as comments or multiple statements

Empty result sets return a JSON response with `rowCount: 0` and `message: "No rows found."`.
