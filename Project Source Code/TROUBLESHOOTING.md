# MCP Server Troubleshooting Guide

## Issue: "Server disconnected" or Timeout in Claude Desktop

### Root Causes Fixed

#### 1. **Column Access Bug in GetSchemaAsync** ✅ FIXED
- **Problem**: The `GetSchemaAsync` method was accessing SQLite PRAGMA columns by name instead of ordinal indices, which caused crashes when processing schema requests
- **Solution**: Changed to use ordinal indices (0-5) and added exception handling
- **File**: `Database/DatabaseService.cs` line 122-151

#### 2. **Missing Global Exception Handling** ✅ FIXED
- **Problem**: Unhandled exceptions during MCP request processing would crash the server silently
- **Solution**: Added try-catch wrapper in `Program.cs` to catch and log all exceptions
- **File**: `Program.cs`

#### 3. **SQLite Connection Timeouts** ✅ FIXED
- **Problem**: SQLite locks could cause the server to hang indefinitely during concurrent requests
- **Solution**: Added `BusyTimeout=30000` (30 second timeout) and `Pooling=true` to connection string
- **File**: `Database/DatabaseService.cs` line 379-404

#### 4. **Logging Configuration** ✅ FIXED
- **Problem**: Excessive logging could interfere with MCP protocol messages on stdout
- **Solution**: Configured logging to go to stderr only, using `LogToStandardErrorThreshold = LogLevel.Trace`
- **File**: `Program.cs`

## Claude Desktop Integration

### 1. Update Your Config File
Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "company-sqlite": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\KIRAN BALASO PATIL\\Desktop\\MCP\\McpServer\\bin\\Debug\\net8.0\\McpServer.dll"
      ],
      "env": {
        "EMPLOYEES_DB_PATH": "C:\\Users\\KIRAN BALASO PATIL\\Desktop\\MCP\\McpServer\\Data\\employees.db"
      }
    }
  }
}
```

### 2. Rebuild the Project
```powershell
cd "C:\Users\KIRAN BALASO PATIL\Desktop\MCP\McpServer"
dotnet build
```

### 3. Restart Claude Desktop
- Close Claude Desktop completely
- Reopen it
- The `company-sqlite` server should now appear in your Tools list

## Verification Steps

### Manual Testing
Test the server independently before using with Claude Desktop:

```powershell
cd "C:\Users\KIRAN BALASO PATIL\Desktop\MCP\McpServer"
$env:EMPLOYEES_DB_PATH = "C:\Users\KIRAN BALASO PATIL\Desktop\MCP\McpServer\Data\employees.db"
dotnet run
```

Expected output:
```
info: McpServer.Database.DatabaseService[0]
      SQLite connection opened in read-write mode
info: McpServer.Database.DatabaseService[0]
      SQLite database ready at [path]\employees.db
info: ModelContextProtocol.Server.StdioServerTransport[857250842]
      Server (stream) (McpServer) transport reading messages.
```

### Test Tools in Claude Desktop
Once integrated, test each tool:
1. **list_tables** - Should list: Departments, Employees, Projects, EmployeeProjects, Users
2. **get_schema Employees** - Should return column info
3. **get_employee_count** - Should return a number
4. **run_select "SELECT Name FROM Employees LIMIT 5"** - Should return employee names

## Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| "Server disconnected" after 4 minutes | Schema query crash | ✅ Fixed in GetSchemaAsync |
| Timeout on any tool | SQLite lock | ✅ Added BusyTimeout |
| No error messages | Missing exception logging | ✅ Added global exception handler |
| "Unresponsive" server | Hanging on I/O | ✅ Added timeouts |

## Changes Summary

### Program.cs
- Added global exception handling with logging
- Configured logging to stderr to avoid interfering with MCP protocol
- Added detailed initialization logging

### Database/DatabaseService.cs
1. **GetSchemaAsync Method** (lines 122-151)
   - Changed from `reader.GetInt32("cid")` to `reader.GetInt32(0)` (ordinal-based)
   - Added try-catch exception handling
   - All 6 columns now accessed by position not name

2. **OpenConnectionAsync Method** (lines 379-404)
   - Added `BusyTimeout=30000` to prevent indefinite waits
   - Added `Pooling=true` for connection reuse
   - Improved error logging

## Files Modified
- `Program.cs` - Exception handling & logging config
- `Database/DatabaseService.cs` - Schema query fix & connection timeout

## Next Steps

1. Rebuild: `dotnet build`
2. Update Claude Desktop config
3. Restart Claude Desktop
4. Test with the tools listed above

If you continue experiencing issues, check the debug output from `dotnet run` for detailed error messages.
