using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace McpServer.Database;

public sealed class DatabaseService
{
    private const int MaxRows = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blocks",
        "Departments",
        "DepartmentBlocks",
        "Employees",
        "EmployeeBlocks",
        "Projects",
        "EmployeeProjects"
    };

    private static readonly HashSet<string> SensitiveColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Salary",
        "PersonalPhoneNumber",
        "HomeAddress",
        "PerformanceRating",
        "BankAccountInformation"
    };

    private static readonly HashSet<string> RestrictedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "ApiKey",
        "APIKey",
        "Secret",
        "Token"
    };

    private static readonly Regex BlockedKeywordRegex = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|REPLACE|PRAGMA|ATTACH|DETACH|VACUUM|REINDEX)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SensitiveColumnRegex = new(
        @"\b(Salary|PersonalPhoneNumber|Personal\s+Phone\s+Number|HomeAddress|Home\s+Address|PerformanceRating|Performance\s+Rating|BankAccountInformation|Bank\s+Account(?:\s+Information)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RestrictedColumnRegex = new(
        @"\b(Password|PasswordHash|Password\s+Hash|ApiKey|APIKey|API\s+Key|Secret|Secrets|Token|Tokens)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ProtectedAuthorizationTableRegex = new(
        @"\b(AuthorizedMembers|Members|Users|VerificationSessions)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MetadataTableRegex = new(
        @"\b(sqlite_master|sqlite_schema|sqlite_temp_master|sqlite_temp_schema)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IHostEnvironment environment, ILogger<DatabaseService> logger)
    {
        _logger = logger;
        DatabasePath = ResolveDatabasePath(environment.ContentRootPath);
    }

    public string DatabasePath { get; }

    public Task<SqliteConnection> OpenReadOnlyConnectionAsync(CancellationToken cancellationToken)
    {
        return OpenConnectionAsync(readOnly: true, cancellationToken);
    }

    public Task<SqliteConnection> OpenReadWriteConnectionAsync(CancellationToken cancellationToken)
    {
        return OpenConnectionAsync(readOnly: false, cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

            await using var connection = await OpenConnectionAsync(readOnly: false, cancellationToken);
            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
            await CreateSchemaAsync(connection, cancellationToken);
            await EnsureEmployeeSecurityColumnsAsync(connection, cancellationToken);
            await DropLegacyVerificationTablesAsync(connection, cancellationToken);
            await SeedDataAsync(connection, cancellationToken);

            _logger.LogInformation("SQLite database ready at {DatabasePath}", DatabasePath);
        }
        catch (Exception ex) when (ex is not McpException)
        {
            _logger.LogError(ex, "Database initialization failed");
            throw new McpException($"Database initialization failed: {ex.Message}", ex);
        }
    }

    public async Task<string> ListTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing public table list query");

        const string query = """
            SELECT name AS TableName
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('Blocks', 'Departments', 'DepartmentBlocks', 'Employees', 'EmployeeBlocks', 'Projects', 'EmployeeProjects')
            ORDER BY name;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetSchemaAsync(string tableName, CancellationToken cancellationToken)
    {
        var normalizedTableName = NormalizeTableName(tableName);
        _logger.LogInformation("Executing schema query for table {TableName}", normalizedTableName);

        await using var connection = await OpenConnectionAsync(readOnly: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""PRAGMA table_info("{normalizedTableName}");""";

        var columns = new List<object?>();

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new
                {
                    cid = reader.GetInt32(0),
                    name = reader.GetString(1),
                    type = reader.GetString(2),
                    notNull = reader.GetInt32(3) == 1,
                    defaultValue = reader.IsDBNull(4) ? null : reader.GetValue(4),
                    primaryKey = reader.GetInt32(5) == 1
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read schema for table {TableName}", normalizedTableName);
            throw new McpException($"Failed to read schema for table '{normalizedTableName}': {ex.Message}", ex);
        }

        return JsonSerializer.Serialize(new
        {
            table = normalizedTableName,
            columns
        }, JsonOptions);
    }

    public async Task<string> RunSelectAsync(string query, CancellationToken cancellationToken)
    {
        ValidateSelectQuery(query);
        _logger.LogInformation("Executing user SELECT query: {Query}", query);

        try
        {
            return await ExecuteTrustedQueryAsync(query.Trim(), cancellationToken);
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Invalid SQL query rejected by SQLite");
            throw new McpException($"Invalid SQL: {ex.Message}", ex);
        }
    }

    public async Task<string> GetEmployeeCountAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing employee count query");

        const string query = """
            SELECT COUNT(*) AS EmployeeCount
            FROM Employees;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetEmployeesByDepartmentAsync(string departmentName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            throw new McpException("Department name is required.");
        }

        _logger.LogInformation("Executing employees-by-department query for {DepartmentName}", departmentName);

        const string query = """
            SELECT e.EmployeeId, e.Name, e.Email, d.DepartmentName
            FROM Employees e
            INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
            WHERE d.DepartmentName = @departmentName COLLATE NOCASE
            ORDER BY e.Name;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken, command =>
        {
            command.Parameters.AddWithValue("@departmentName", departmentName.Trim());
        });
    }

    public async Task<string> GetDepartmentSummaryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing department summary query");

        const string query = """
            SELECT d.DepartmentName, COUNT(e.EmployeeId) AS EmployeeCount
            FROM Departments d
            LEFT JOIN Employees e ON e.DepartmentId = d.DepartmentId
            GROUP BY d.DepartmentId, d.DepartmentName
            ORDER BY d.DepartmentName;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetActiveProjectsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing active projects query");

        const string query = """
            SELECT ProjectId, ProjectName, Status
            FROM Projects
            WHERE Status = 'Active' COLLATE NOCASE
            ORDER BY ProjectName;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetEmployeeProjectsAsync(string employeeName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            throw new McpException("Employee name is required.");
        }

        _logger.LogInformation("Executing employee projects query for {EmployeeName}", employeeName);

        const string query = """
            SELECT e.Name AS EmployeeName, p.ProjectName, p.Status
            FROM Employees e
            INNER JOIN EmployeeProjects ep ON ep.EmployeeId = e.EmployeeId
            INNER JOIN Projects p ON p.ProjectId = ep.ProjectId
            WHERE e.Name = @employeeName COLLATE NOCASE
            ORDER BY p.ProjectName;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken, command =>
        {
            command.Parameters.AddWithValue("@employeeName", employeeName.Trim());
        });
    }

    public async Task<string> GetBlocksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing block list query");

        const string query = """
            SELECT b.BlockId,
                   b.BlockCode,
                   b.BlockName,
                   b.Campus,
                   b.Building,
                   b.Floor,
                   b.Wing,
                   b.LocationDescription,
                   b.Purpose,
                   d.DepartmentName AS PrimaryDepartment,
                   COUNT(eb.EmployeeId) AS EmployeeCount
            FROM Blocks b
            LEFT JOIN DepartmentBlocks db ON db.BlockId = b.BlockId AND db.IsPrimary = 1
            LEFT JOIN Departments d ON d.DepartmentId = db.DepartmentId
            LEFT JOIN EmployeeBlocks eb ON eb.BlockId = b.BlockId
            WHERE b.IsActive = 1
            GROUP BY b.BlockId,
                     b.BlockCode,
                     b.BlockName,
                     b.Campus,
                     b.Building,
                     b.Floor,
                     b.Wing,
                     b.LocationDescription,
                     b.Purpose,
                     d.DepartmentName
            ORDER BY b.BlockCode;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetBlockEmployeesAsync(string blockNameOrCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blockNameOrCode))
        {
            throw new McpException("Block name or code is required.");
        }

        _logger.LogInformation("Executing block employees query for {BlockNameOrCode}", blockNameOrCode);

        const string query = """
            SELECT b.BlockCode,
                   b.BlockName,
                   b.LocationDescription,
                   e.EmployeeId,
                   e.Name AS EmployeeName,
                   e.Email AS WorkEmail,
                   d.DepartmentName AS Department,
                   eb.SeatNumber,
                   eb.DeskLocation
            FROM Blocks b
            INNER JOIN EmployeeBlocks eb ON eb.BlockId = b.BlockId
            INNER JOIN Employees e ON e.EmployeeId = eb.EmployeeId
            INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
            WHERE b.BlockCode = @blockNameOrCode COLLATE NOCASE
               OR b.BlockName = @blockNameOrCode COLLATE NOCASE
            ORDER BY d.DepartmentName, e.Name;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken, command =>
        {
            command.Parameters.AddWithValue("@blockNameOrCode", blockNameOrCode.Trim());
        });
    }

    public async Task<string> GetEmployeeBlockAsync(string employeeName, CancellationToken cancellationToken)
    {
        var returnAllEmployees = string.IsNullOrWhiteSpace(employeeName) ||
            employeeName.Trim().Equals("all", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("Executing employee block query for {EmployeeName}", employeeName);

        var query = returnAllEmployees
            ? """
                SELECT e.EmployeeId,
                       e.Name AS EmployeeName,
                       e.Email AS WorkEmail,
                       d.DepartmentName AS Department,
                       b.BlockCode,
                       b.BlockName,
                       b.Campus,
                       b.Building,
                       b.Floor,
                       b.Wing,
                       b.LocationDescription,
                       eb.SeatNumber,
                       eb.DeskLocation
                FROM Employees e
                INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
                INNER JOIN EmployeeBlocks eb ON eb.EmployeeId = e.EmployeeId
                INNER JOIN Blocks b ON b.BlockId = eb.BlockId
                ORDER BY d.DepartmentName, e.Name;
                """
            : """
                SELECT e.EmployeeId,
                       e.Name AS EmployeeName,
                       e.Email AS WorkEmail,
                       d.DepartmentName AS Department,
                       b.BlockCode,
                       b.BlockName,
                       b.Campus,
                       b.Building,
                       b.Floor,
                       b.Wing,
                       b.LocationDescription,
                       eb.SeatNumber,
                       eb.DeskLocation
                FROM Employees e
                INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
                INNER JOIN EmployeeBlocks eb ON eb.EmployeeId = e.EmployeeId
                INNER JOIN Blocks b ON b.BlockId = eb.BlockId
                WHERE e.Name = @employeeName COLLATE NOCASE
                ORDER BY e.Name;
                """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken, command =>
        {
            if (!returnAllEmployees)
            {
                command.Parameters.AddWithValue("@employeeName", employeeName.Trim());
            }
        });
    }

    public async Task<string> GetHrBlockAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing HR block query");

        const string query = """
            SELECT d.DepartmentName,
                   b.BlockCode,
                   b.BlockName,
                   b.Campus,
                   b.Building,
                   b.Floor,
                   b.Wing,
                   b.LocationDescription,
                   b.Purpose,
                   COUNT(eb.EmployeeId) AS EmployeeCount
            FROM Departments d
            INNER JOIN DepartmentBlocks db ON db.DepartmentId = d.DepartmentId
            INNER JOIN Blocks b ON b.BlockId = db.BlockId
            LEFT JOIN EmployeeBlocks eb ON eb.BlockId = b.BlockId
            WHERE d.DepartmentName = 'Human Resources' COLLATE NOCASE
              AND db.IsPrimary = 1
              AND b.IsActive = 1
            GROUP BY d.DepartmentName,
                     b.BlockCode,
                     b.BlockName,
                     b.Campus,
                     b.Building,
                     b.Floor,
                     b.Wing,
                     b.LocationDescription,
                     b.Purpose
            ORDER BY b.BlockCode;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetCompanyLocationMapAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing company location map query");

        const string query = """
            SELECT e.EmployeeId,
                   e.Name AS EmployeeName,
                   e.Email AS WorkEmail,
                   d.DepartmentName AS Department,
                   b.BlockCode,
                   b.BlockName,
                   b.Campus,
                   b.Building,
                   b.Floor,
                   b.Wing,
                   b.LocationDescription,
                   eb.SeatNumber,
                   eb.DeskLocation,
                   COALESCE(GROUP_CONCAT(p.ProjectName, ', '), '') AS Projects
            FROM Employees e
            INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
            INNER JOIN EmployeeBlocks eb ON eb.EmployeeId = e.EmployeeId
            INNER JOIN Blocks b ON b.BlockId = eb.BlockId
            LEFT JOIN EmployeeProjects ep ON ep.EmployeeId = e.EmployeeId
            LEFT JOIN Projects p ON p.ProjectId = ep.ProjectId
            GROUP BY e.EmployeeId,
                     e.Name,
                     e.Email,
                     d.DepartmentName,
                     b.BlockCode,
                     b.BlockName,
                     b.Campus,
                     b.Building,
                     b.Floor,
                     b.Wing,
                     b.LocationDescription,
                     eb.SeatNumber,
                     eb.DeskLocation
            ORDER BY d.DepartmentName, e.Name;
            """;

        return await ExecuteTrustedQueryAsync(query, cancellationToken);
    }

    public async Task<string> GetEmployeePublicInfoAsync(string employeeName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeName))
        {
            throw new McpException("Employee name is required.");
        }

        _logger.LogInformation("Executing public employee info query for {EmployeeName}", employeeName);

        const string query = """
            SELECT e.Name AS EmployeeName,
                   e.Email AS WorkEmail,
                   d.DepartmentName AS Department,
                   b.BlockCode,
                   b.BlockName,
                   b.LocationDescription,
                   eb.SeatNumber,
                   eb.DeskLocation,
                   p.ProjectName AS ProjectName
            FROM Employees e
            INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
            LEFT JOIN EmployeeBlocks eb ON eb.EmployeeId = e.EmployeeId
            LEFT JOIN Blocks b ON b.BlockId = eb.BlockId
            LEFT JOIN EmployeeProjects ep ON ep.EmployeeId = e.EmployeeId
            LEFT JOIN Projects p ON p.ProjectId = ep.ProjectId
            WHERE e.Name = @employeeName COLLATE NOCASE
            ORDER BY p.ProjectName;
            """;

        await using var connection = await OpenConnectionAsync(readOnly: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@employeeName", employeeName.Trim());

        string? name = null;
        string? workEmail = null;
        string? department = null;
        string? blockCode = null;
        string? blockName = null;
        string? locationDescription = null;
        string? seatNumber = null;
        string? deskLocation = null;
        var projects = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var employeeNameOrdinal = reader.GetOrdinal("EmployeeName");
        var workEmailOrdinal = reader.GetOrdinal("WorkEmail");
        var departmentOrdinal = reader.GetOrdinal("Department");
        var blockCodeOrdinal = reader.GetOrdinal("BlockCode");
        var blockNameOrdinal = reader.GetOrdinal("BlockName");
        var locationDescriptionOrdinal = reader.GetOrdinal("LocationDescription");
        var seatNumberOrdinal = reader.GetOrdinal("SeatNumber");
        var deskLocationOrdinal = reader.GetOrdinal("DeskLocation");
        var projectNameOrdinal = reader.GetOrdinal("ProjectName");

        while (await reader.ReadAsync(cancellationToken))
        {
            name ??= reader.GetString(employeeNameOrdinal);
            workEmail ??= reader.GetString(workEmailOrdinal);
            department ??= reader.GetString(departmentOrdinal);
            blockCode ??= reader.IsDBNull(blockCodeOrdinal) ? null : reader.GetString(blockCodeOrdinal);
            blockName ??= reader.IsDBNull(blockNameOrdinal) ? null : reader.GetString(blockNameOrdinal);
            locationDescription ??= reader.IsDBNull(locationDescriptionOrdinal) ? null : reader.GetString(locationDescriptionOrdinal);
            seatNumber ??= reader.IsDBNull(seatNumberOrdinal) ? null : reader.GetString(seatNumberOrdinal);
            deskLocation ??= reader.IsDBNull(deskLocationOrdinal) ? null : reader.GetString(deskLocationOrdinal);

            if (!reader.IsDBNull(projectNameOrdinal))
            {
                projects.Add(reader.GetString(projectNameOrdinal));
            }
        }

        if (name is null)
        {
            return JsonSerializer.Serialize(new
            {
                rowCount = 0,
                message = "Employee not found."
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            rowCount = 1,
            employee = new
            {
                name,
                workEmail,
                department,
                block = new
                {
                    blockCode,
                    blockName,
                    locationDescription,
                    seatNumber,
                    deskLocation
                },
                projects
            }
        }, JsonOptions);
    }

    public async Task<string> GetSensitiveEmployeeDataAsync(string employeeName, string dataType, CancellationToken cancellationToken)
    {
        var (columnName, displayColumnName) = GetSensitiveColumn(dataType);
        var returnAllEmployees = string.IsNullOrWhiteSpace(employeeName) ||
            employeeName.Trim().Equals("all", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("Executing whitelisted sensitive employee data query for {EmployeeName} and {DataType}", employeeName, dataType);

        var query = returnAllEmployees
            ? $"""
                SELECT e.EmployeeId,
                       e.Name AS EmployeeName,
                       e.Email AS WorkEmail,
                       d.DepartmentName AS Department,
                       e.{columnName} AS {displayColumnName}
                FROM Employees e
                INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
                ORDER BY e.Name;
                """
            : $"""
                SELECT e.EmployeeId,
                       e.Name AS EmployeeName,
                       e.Email AS WorkEmail,
                       d.DepartmentName AS Department,
                       e.{columnName} AS {displayColumnName}
                FROM Employees e
                INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
                WHERE e.Name = @employeeName COLLATE NOCASE
                ORDER BY e.Name;
                """;

        await using var connection = await OpenConnectionAsync(readOnly: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = query;

        if (!returnAllEmployees)
        {
            command.Parameters.AddWithValue("@employeeName", employeeName.Trim());
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var employeeIdOrdinal = reader.GetOrdinal("EmployeeId");
        var employeeNameOrdinal = reader.GetOrdinal("EmployeeName");
        var workEmailOrdinal = reader.GetOrdinal("WorkEmail");
        var departmentOrdinal = reader.GetOrdinal("Department");
        var sensitiveOrdinal = reader.GetOrdinal(displayColumnName);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EmployeeId"] = reader.GetInt32(employeeIdOrdinal),
                ["EmployeeName"] = reader.GetString(employeeNameOrdinal),
                ["WorkEmail"] = reader.GetString(workEmailOrdinal),
                ["Department"] = reader.GetString(departmentOrdinal),
                [displayColumnName] = FormatSensitiveValue(dataType, reader.IsDBNull(sensitiveOrdinal) ? null : reader.GetValue(sensitiveOrdinal))
            });
        }

        return JsonSerializer.Serialize(new
        {
            rowCount = rows.Count,
            message = rows.Count == 0 ? "Employee not found." : null,
            rows
        }, JsonOptions);
    }

    public async Task<string> GetCompanyDocumentationDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing redacted company documentation export");

        await using var connection = await OpenConnectionAsync(readOnly: true, cancellationToken);

        var departments = await QueryRowsAsync(connection, """
            SELECT DepartmentId, DepartmentName
            FROM Departments
            ORDER BY DepartmentId;
            """, cancellationToken);

        var blocks = await QueryRowsAsync(connection, """
            SELECT BlockId,
                   BlockCode,
                   BlockName,
                   Campus,
                   Building,
                   Floor,
                   Wing,
                   LocationDescription,
                   Purpose,
                   IsActive
            FROM Blocks
            ORDER BY BlockId;
            """, cancellationToken);

        var departmentBlocks = await QueryRowsAsync(connection, """
            SELECT db.DepartmentId,
                   d.DepartmentName,
                   db.BlockId,
                   b.BlockCode,
                   b.BlockName,
                   db.IsPrimary,
                   db.Notes
            FROM DepartmentBlocks db
            INNER JOIN Departments d ON d.DepartmentId = db.DepartmentId
            INNER JOIN Blocks b ON b.BlockId = db.BlockId
            ORDER BY d.DepartmentName, b.BlockCode;
            """, cancellationToken);

        var employees = await QueryRowsAsync(connection, """
            SELECT e.EmployeeId,
                   e.Name,
                   e.Email,
                   e.DepartmentId,
                   d.DepartmentName,
                   b.BlockId,
                   b.BlockCode,
                   b.BlockName,
                   eb.SeatNumber,
                   eb.DeskLocation
            FROM Employees e
            INNER JOIN Departments d ON d.DepartmentId = e.DepartmentId
            LEFT JOIN EmployeeBlocks eb ON eb.EmployeeId = e.EmployeeId
            LEFT JOIN Blocks b ON b.BlockId = eb.BlockId
            ORDER BY e.EmployeeId;
            """, cancellationToken);

        var employeeBlocks = await QueryRowsAsync(connection, """
            SELECT eb.EmployeeId,
                   e.Name AS EmployeeName,
                   eb.BlockId,
                   b.BlockCode,
                   b.BlockName,
                   eb.SeatNumber,
                   eb.DeskLocation,
                   eb.AssignedFrom
            FROM EmployeeBlocks eb
            INNER JOIN Employees e ON e.EmployeeId = eb.EmployeeId
            INNER JOIN Blocks b ON b.BlockId = eb.BlockId
            ORDER BY e.EmployeeId;
            """, cancellationToken);

        var projects = await QueryRowsAsync(connection, """
            SELECT ProjectId, ProjectName, Status
            FROM Projects
            ORDER BY ProjectId;
            """, cancellationToken);

        var employeeProjects = await QueryRowsAsync(connection, """
            SELECT ep.EmployeeId,
                   e.Name AS EmployeeName,
                   ep.ProjectId,
                   p.ProjectName,
                   p.Status
            FROM EmployeeProjects ep
            INNER JOIN Employees e ON e.EmployeeId = ep.EmployeeId
            INNER JOIN Projects p ON p.ProjectId = ep.ProjectId
            ORDER BY e.EmployeeId, p.ProjectName;
            """, cancellationToken);

        return JsonSerializer.Serialize(new
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                securityNote = "This export is intended for documentation. Sensitive employee fields are not included. Passwords and password hashes are restricted and never exported.",
                excludedRestrictedColumns = RestrictedColumns.OrderBy(column => column, StringComparer.OrdinalIgnoreCase).ToArray(),
                excludedSensitiveColumns = SensitiveColumns.OrderBy(column => column, StringComparer.OrdinalIgnoreCase).ToArray(),
                tables = new
                {
                    Departments = departments,
                    Blocks = blocks,
                    DepartmentBlocks = departmentBlocks,
                    Employees = employees,
                    EmployeeBlocks = employeeBlocks,
                    Projects = projects,
                    EmployeeProjects = employeeProjects
                }
            }, JsonOptions);
    }

    public async Task<(bool Success, int MemberId, string Name, string Email, string Role, string Message)> VerifyAuthorizedMemberAsync(
        string name,
        string roleName,
        string emailAddress,
        string password,
        CancellationToken cancellationToken)
    {
        // Per requirement: trim leading/trailing spaces before validating.
        var trimmedEmail = emailAddress?.Trim();
        var trimmedPassword = password?.Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(roleName) ||
            string.IsNullOrWhiteSpace(trimmedEmail) ||
            string.IsNullOrWhiteSpace(trimmedPassword))
        {
            return (false, 0, "", "", "", "Name, role name, email address, and password are required.");
        }

        _logger.LogInformation("Authorization attempt for email: {Email}", trimmedEmail);

        int memberId;
        string memberName;
        string memberEmail;
        string storedHash;
        string memberRole;

        await using (var connection = await OpenConnectionAsync(readOnly: false, cancellationToken))
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT AuthorizedMemberId, Name, Email, PasswordHash, RoleName
                    FROM AuthorizedMembers
                    WHERE Name = @name COLLATE NOCASE
                      AND RoleName = @roleName COLLATE NOCASE
                      AND Email = @email COLLATE NOCASE
                      AND IsActive = 1
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("@name", name.Trim());
                command.Parameters.AddWithValue("@roleName", roleName.Trim());
                command.Parameters.AddWithValue("@email", trimmedEmail!);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    _logger.LogWarning("Authorization failed: no active authorized member matched email: {Email}", trimmedEmail);
                    return (false, 0, "", "", "", "Invalid authorization details.");
                }

                memberId = reader.GetInt32(0);
                memberName = reader.GetString(1);
                memberEmail = reader.GetString(2);
                storedHash = reader.GetString(3);
                memberRole = reader.GetString(4);
            }

            if (!PasswordMatches(trimmedPassword!, storedHash))
            {
                _logger.LogWarning("Authorization failed: invalid password for email: {Email}", trimmedEmail);
                return (false, 0, "", "", "", "Invalid authorization details.");
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = """
                UPDATE AuthorizedMembers
                SET LastVerifiedAt = @lastVerifiedAt
                WHERE AuthorizedMemberId = @memberId;
                """;
            updateCommand.Parameters.AddWithValue("@lastVerifiedAt", DateTimeOffset.UtcNow.ToString("O"));
            updateCommand.Parameters.AddWithValue("@memberId", memberId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Authorization successful for member: {Name} ({Email}) with role: {Role}", memberName, memberEmail, memberRole);
        return (true, memberId, memberName, memberEmail, memberRole, "Verification successful.");
    }

    private static string ResolveDatabasePath(string contentRootPath)
    {
        var configuredPath = Environment.GetEnvironmentVariable("EMPLOYEES_DB_PATH");
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(contentRootPath, "Data", "employees.db")
            : configuredPath;

        return Path.GetFullPath(path);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(bool readOnly, CancellationToken cancellationToken)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var connection = new SqliteConnection(connectionStringBuilder.ToString());

        try
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogInformation("SQLite connection opened in {Mode} mode", readOnly ? "read-only" : "read-write");
            return connection;
        }
        catch (Exception ex)
        {
            await connection.DisposeAsync();
            _logger.LogError(ex, "SQLite connection failed for {DatabasePath}", DatabasePath);
            throw new McpException($"Could not connect to SQLite database at '{DatabasePath}': {ex.Message}", ex);
        }
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string schema = """
            CREATE TABLE IF NOT EXISTS Departments (
                DepartmentId INTEGER PRIMARY KEY,
                DepartmentName TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS Employees (
                EmployeeId INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE,
                DepartmentId INTEGER NOT NULL,
                Salary INTEGER NOT NULL DEFAULT 0,
                PersonalPhoneNumber TEXT NOT NULL DEFAULT '',
                HomeAddress TEXT NOT NULL DEFAULT '',
                PerformanceRating TEXT NOT NULL DEFAULT '',
                BankAccountInformation TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (DepartmentId) REFERENCES Departments (DepartmentId)
            );

            CREATE TABLE IF NOT EXISTS Blocks (
                BlockId INTEGER PRIMARY KEY,
                BlockCode TEXT NOT NULL UNIQUE,
                BlockName TEXT NOT NULL UNIQUE,
                Campus TEXT NOT NULL,
                Building TEXT NOT NULL,
                Floor TEXT NOT NULL,
                Wing TEXT NOT NULL,
                LocationDescription TEXT NOT NULL,
                Purpose TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS DepartmentBlocks (
                DepartmentId INTEGER NOT NULL,
                BlockId INTEGER NOT NULL,
                IsPrimary INTEGER NOT NULL DEFAULT 1,
                Notes TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (DepartmentId, BlockId),
                FOREIGN KEY (DepartmentId) REFERENCES Departments (DepartmentId),
                FOREIGN KEY (BlockId) REFERENCES Blocks (BlockId)
            );

            CREATE TABLE IF NOT EXISTS Projects (
                ProjectId INTEGER PRIMARY KEY,
                ProjectName TEXT NOT NULL UNIQUE,
                Status TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS EmployeeBlocks (
                EmployeeId INTEGER PRIMARY KEY,
                BlockId INTEGER NOT NULL,
                SeatNumber TEXT NOT NULL,
                DeskLocation TEXT NOT NULL,
                AssignedFrom TEXT NOT NULL,
                FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId),
                FOREIGN KEY (BlockId) REFERENCES Blocks (BlockId)
            );

            CREATE TABLE IF NOT EXISTS EmployeeProjects (
                EmployeeId INTEGER NOT NULL,
                ProjectId INTEGER NOT NULL,
                PRIMARY KEY (EmployeeId, ProjectId),
                FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId),
                FOREIGN KEY (ProjectId) REFERENCES Projects (ProjectId)
            );

            CREATE TABLE IF NOT EXISTS AuthorizedMembers (
                AuthorizedMemberId INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                RoleName TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                LastVerifiedAt TEXT
            );
            """;

        await ExecuteNonQueryAsync(connection, schema, cancellationToken);
    }

    private static async Task DropLegacyVerificationTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, "DROP TABLE IF EXISTS VerificationSessions;", cancellationToken);
        await ExecuteNonQueryAsync(connection, "DROP TABLE IF EXISTS Members;", cancellationToken);
        // Legacy table name from older versions. Must not exist.
        await ExecuteNonQueryAsync(connection, "DROP TABLE IF EXISTS users;", cancellationToken);
    }

    private static async Task EnsureEmployeeSecurityColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """PRAGMA table_info("Employees");""";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Salary"] = "INTEGER NOT NULL DEFAULT 0",
            ["PersonalPhoneNumber"] = "TEXT NOT NULL DEFAULT ''",
            ["HomeAddress"] = "TEXT NOT NULL DEFAULT ''",
            ["PerformanceRating"] = "TEXT NOT NULL DEFAULT ''",
            ["BankAccountInformation"] = "TEXT NOT NULL DEFAULT ''"
        };

        foreach (var column in requiredColumns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            await ExecuteNonQueryAsync(connection, $"""ALTER TABLE Employees ADD COLUMN {column.Key} {column.Value};""", cancellationToken);
        }
    }

    private static async Task SeedDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await SeedDepartmentsAsync(connection, transaction, cancellationToken);
        await SeedAuthorizedMembersAsync(connection, transaction, cancellationToken);
        await SeedEmployeesAsync(connection, transaction, cancellationToken);
        await SeedBlocksAsync(connection, transaction, cancellationToken);
        await SeedDepartmentBlocksAsync(connection, transaction, cancellationToken);
        await SeedProjectsAsync(connection, transaction, cancellationToken);
        await SeedEmployeeBlocksAsync(connection, transaction, cancellationToken);
        await SeedEmployeeProjectsAsync(connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedDepartmentsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var departments = new (int Id, string Name)[]
        {
            (1, "Engineering"),
            (2, "Human Resources"),
            (3, "Finance"),
            (4, "Sales"),
            (5, "Marketing")
        };

        foreach (var department in departments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Departments (DepartmentId, DepartmentName)
                VALUES (@id, @name);
                """;
            command.Parameters.AddWithValue("@id", department.Id);
            command.Parameters.AddWithValue("@name", department.Name);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedBlocksAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var blocks = new (int Id, string Code, string Name, string Campus, string Building, string Floor, string Wing, string Location, string Purpose)[]
        {
            (1, "BLK-A", "Atria Block", "Main Campus", "Atria Tower", "Floor 1", "North Wing", "Main Campus, Atria Tower, Floor 1, North Wing", "Engineering collaboration and platform delivery"),
            (2, "BLK-HR", "People Block", "Main Campus", "Atria Tower", "Floor 2", "East Wing", "Main Campus, Atria Tower, Floor 2, East Wing", "Human Resources and employee support"),
            (3, "BLK-FN", "Ledger Block", "Main Campus", "Cedar Tower", "Floor 3", "South Wing", "Main Campus, Cedar Tower, Floor 3, South Wing", "Finance operations and planning"),
            (4, "BLK-SL", "Market Block", "North Campus", "Orion Tower", "Floor 1", "West Wing", "North Campus, Orion Tower, Floor 1, West Wing", "Sales operations and regional coordination"),
            (5, "BLK-MK", "Brand Block", "North Campus", "Orion Tower", "Floor 2", "Creative Wing", "North Campus, Orion Tower, Floor 2, Creative Wing", "Marketing campaign and creative workspace"),
            (6, "BLK-EX", "Executive Block", "Main Campus", "Cedar Tower", "Floor 5", "Executive Wing", "Main Campus, Cedar Tower, Floor 5, Executive Wing", "Leadership, executive reviews, and cross-functional decisions")
        };

        foreach (var block in blocks)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Blocks (
                    BlockId,
                    BlockCode,
                    BlockName,
                    Campus,
                    Building,
                    Floor,
                    Wing,
                    LocationDescription,
                    Purpose,
                    IsActive
                )
                VALUES (
                    @id,
                    @code,
                    @name,
                    @campus,
                    @building,
                    @floor,
                    @wing,
                    @location,
                    @purpose,
                    1
                )
                ON CONFLICT(BlockId) DO UPDATE SET
                    BlockCode = excluded.BlockCode,
                    BlockName = excluded.BlockName,
                    Campus = excluded.Campus,
                    Building = excluded.Building,
                    Floor = excluded.Floor,
                    Wing = excluded.Wing,
                    LocationDescription = excluded.LocationDescription,
                    Purpose = excluded.Purpose,
                    IsActive = excluded.IsActive;
                """;
            command.Parameters.AddWithValue("@id", block.Id);
            command.Parameters.AddWithValue("@code", block.Code);
            command.Parameters.AddWithValue("@name", block.Name);
            command.Parameters.AddWithValue("@campus", block.Campus);
            command.Parameters.AddWithValue("@building", block.Building);
            command.Parameters.AddWithValue("@floor", block.Floor);
            command.Parameters.AddWithValue("@wing", block.Wing);
            command.Parameters.AddWithValue("@location", block.Location);
            command.Parameters.AddWithValue("@purpose", block.Purpose);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedDepartmentBlocksAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var departmentBlocks = new (int DepartmentId, int BlockId, int IsPrimary, string Notes)[]
        {
            (1, 1, 1, "Primary Engineering workspace"),
            (2, 2, 1, "Primary HR block and employee support desk"),
            (3, 3, 1, "Primary Finance workspace"),
            (4, 4, 1, "Primary Sales workspace"),
            (5, 5, 1, "Primary Marketing workspace"),
            (1, 6, 0, "Shared leadership review rooms"),
            (3, 6, 0, "Executive finance review rooms"),
            (4, 6, 0, "Executive sales review rooms")
        };

        foreach (var departmentBlock in departmentBlocks)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO DepartmentBlocks (DepartmentId, BlockId, IsPrimary, Notes)
                VALUES (@departmentId, @blockId, @isPrimary, @notes)
                ON CONFLICT(DepartmentId, BlockId) DO UPDATE SET
                    IsPrimary = excluded.IsPrimary,
                    Notes = excluded.Notes;
                """;
            command.Parameters.AddWithValue("@departmentId", departmentBlock.DepartmentId);
            command.Parameters.AddWithValue("@blockId", departmentBlock.BlockId);
            command.Parameters.AddWithValue("@isPrimary", departmentBlock.IsPrimary);
            command.Parameters.AddWithValue("@notes", departmentBlock.Notes);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedAuthorizedMembersAsync(SqliteConnection connection, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        // Only these members are allowed to access sensitive information.
        var members = new (int Id, string Name, string RoleName, string Email, string PasswordHash)[]
        {
            (1, "Priya", "HR", "hr@company.com", HashPassword("Priya@123")),
            (2, "Arun", "Manager", "arun.manager@company.com", HashPassword("Manager@123")),
            (3, "Rahul", "Director", "rahul.director@company.com", HashPassword("Director@123")),
            (4, "Ramesh", "CEO", "ceo@company.com", HashPassword("CEO@123"))
        };

        foreach (var member in members)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO AuthorizedMembers (AuthorizedMemberId, Name, RoleName, Email, PasswordHash, IsActive, CreatedAt)
                VALUES (@id, @name, @roleName, @email, @passwordHash, 1, @createdAt)
                ON CONFLICT(AuthorizedMemberId) DO UPDATE SET
                    Name = excluded.Name,
                    RoleName = excluded.RoleName,
                    Email = excluded.Email,
                    PasswordHash = excluded.PasswordHash,
                    IsActive = excluded.IsActive;
                """;
            command.Parameters.AddWithValue("@id", member.Id);
            command.Parameters.AddWithValue("@name", member.Name);
            command.Parameters.AddWithValue("@roleName", member.RoleName);
            command.Parameters.AddWithValue("@email", member.Email);
            command.Parameters.AddWithValue("@passwordHash", member.PasswordHash);
            command.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedEmployeesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var employees = new (int Id, string Name, string Email, int DepartmentId, int Salary, string PersonalPhoneNumber, string HomeAddress, string PerformanceRating, string BankAccountInformation)[]
        {
            (1, "Aarav Mehta", "aarav.mehta@contoso.example", 1, 118000, "+91-90000-00001", "12 MG Road, Pune", "Exceeds Expectations", "INDB000000001"),
            (2, "Sophia Carter", "sophia.carter@contoso.example", 1, 116000, "+91-90000-00002", "43 Park Street, Bengaluru", "Exceeds Expectations", "INDB000000002"),
            (3, "Liam Johnson", "liam.johnson@contoso.example", 1, 104000, "+91-90000-00003", "19 Lake View, Hyderabad", "Meets Expectations", "INDB000000003"),
            (4, "Priya Nair", "priya.nair@contoso.example", 1, 99000, "+91-90000-00004", "21 Palm Grove, Kochi", "Meets Expectations", "INDB000000004"),
            (5, "Noah Williams", "noah.williams@contoso.example", 2, 85000, "+91-90000-00005", "8 HR Colony, Mumbai", "Meets Expectations", "INDB000000005"),
            (6, "Emma Brown", "emma.brown@contoso.example", 2, 83000, "+91-90000-00006", "6 Central Avenue, Delhi", "Meets Expectations", "INDB000000006"),
            (7, "Rohan Desai", "rohan.desai@contoso.example", 2, 81000, "+91-90000-00007", "10 Garden Road, Ahmedabad", "Needs Improvement", "INDB000000007"),
            (8, "Olivia Davis", "olivia.davis@contoso.example", 3, 92000, "+91-90000-00008", "22 Finance Lane, Chennai", "Exceeds Expectations", "INDB000000008"),
            (9, "Mia Wilson", "mia.wilson@contoso.example", 3, 90000, "+91-90000-00009", "17 River Road, Nashik", "Meets Expectations", "INDB000000009"),
            (10, "Ethan Miller", "ethan.miller@contoso.example", 3, 88000, "+91-90000-00010", "14 Hill Street, Jaipur", "Meets Expectations", "INDB000000010"),
            (11, "Ananya Rao", "ananya.rao@contoso.example", 3, 94000, "+91-90000-00011", "7 Residency Road, Mysuru", "Exceeds Expectations", "INDB000000011"),
            (12, "James Taylor", "james.taylor@contoso.example", 4, 78000, "+91-90000-00012", "3 Sales Park, Gurugram", "Meets Expectations", "INDB000000012"),
            (13, "Isabella Anderson", "isabella.anderson@contoso.example", 4, 79000, "+91-90000-00013", "32 Metro Road, Noida", "Meets Expectations", "INDB000000013"),
            (14, "Lucas Thomas", "lucas.thomas@contoso.example", 4, 76000, "+91-90000-00014", "18 Market Street, Pune", "Needs Improvement", "INDB000000014"),
            (15, "Neha Sharma", "neha.sharma@contoso.example", 4, 82000, "+91-90000-00015", "9 Sector Road, Chandigarh", "Exceeds Expectations", "INDB000000015"),
            (16, "Benjamin Moore", "benjamin.moore@contoso.example", 5, 73000, "+91-90000-00016", "5 Brand Avenue, Mumbai", "Meets Expectations", "INDB000000016"),
            (17, "Charlotte Martin", "charlotte.martin@contoso.example", 5, 75000, "+91-90000-00017", "11 Media Street, Delhi", "Meets Expectations", "INDB000000017"),
            (18, "Kabir Singh", "kabir.singh@contoso.example", 5, 71000, "+91-90000-00018", "2 Campaign Road, Jaipur", "Needs Improvement", "INDB000000018"),
            (19, "Amelia White", "amelia.white@contoso.example", 5, 77000, "+91-90000-00019", "28 Creative Lane, Bengaluru", "Exceeds Expectations", "INDB000000019"),
            (20, "Daniel Clark", "daniel.clark@contoso.example", 1, 97000, "+91-90000-00020", "16 Tech Park, Hyderabad", "Meets Expectations", "INDB000000020"),
            (21, "Arun", "arun@company.com", 1, 80000, "+91-90000-00021", "44 Engineering Road, Pune", "Meets Expectations", "INDB000000021"),
            (22, "Kiran", "kiran@company.com", 1, 60000, "+91-90000-00022", "2 Developer Colony, Kolhapur", "Meets Expectations", "INDB000000022"),
            (23, "Arun Manager", "arun.manager@company.com", 1, 150000, "+91-90000-00023", "81 Leadership Street, Pune", "Exceeds Expectations", "INDB000000023"),
            (24, "Priya HR", "priya.hr@company.com", 2, 135000, "+91-90000-00024", "6 People Ops Road, Mumbai", "Exceeds Expectations", "INDB000000024"),
            (25, "Rahul Director", "rahul.director@company.com", 4, 220000, "+91-90000-00025", "15 Executive Enclave, Delhi", "Exceeds Expectations", "INDB000000025"),
            (26, "CEO Admin", "ceo@company.com", 3, 300000, "+91-90000-00026", "1 Board Avenue, Bengaluru", "Exceeds Expectations", "INDB000000026")
        };

        foreach (var employee in employees)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Employees (
                    EmployeeId,
                    Name,
                    Email,
                    DepartmentId,
                    Salary,
                    PersonalPhoneNumber,
                    HomeAddress,
                    PerformanceRating,
                    BankAccountInformation
                )
                VALUES (
                    @id,
                    @name,
                    @email,
                    @departmentId,
                    @salary,
                    @personalPhoneNumber,
                    @homeAddress,
                    @performanceRating,
                    @bankAccountInformation
                )
                ON CONFLICT(EmployeeId) DO UPDATE SET
                    Name = excluded.Name,
                    Email = excluded.Email,
                    DepartmentId = excluded.DepartmentId,
                    Salary = excluded.Salary,
                    PersonalPhoneNumber = excluded.PersonalPhoneNumber,
                    HomeAddress = excluded.HomeAddress,
                    PerformanceRating = excluded.PerformanceRating,
                    BankAccountInformation = excluded.BankAccountInformation;
                """;
            command.Parameters.AddWithValue("@id", employee.Id);
            command.Parameters.AddWithValue("@name", employee.Name);
            command.Parameters.AddWithValue("@email", employee.Email);
            command.Parameters.AddWithValue("@departmentId", employee.DepartmentId);
            command.Parameters.AddWithValue("@salary", employee.Salary);
            command.Parameters.AddWithValue("@personalPhoneNumber", employee.PersonalPhoneNumber);
            command.Parameters.AddWithValue("@homeAddress", employee.HomeAddress);
            command.Parameters.AddWithValue("@performanceRating", employee.PerformanceRating);
            command.Parameters.AddWithValue("@bankAccountInformation", employee.BankAccountInformation);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedProjectsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var projects = new (int Id, string Name, string Status)[]
        {
            (1, "Atlas Platform Modernization", "Active"),
            (2, "Benefits Portal Refresh", "Active"),
            (3, "Quarterly Forecast Automation", "Active"),
            (4, "North Region CRM Rollout", "Active"),
            (5, "Brand Awareness Campaign", "Active"),
            (6, "Data Warehouse Migration", "Planning"),
            (7, "Security Compliance Review", "Active"),
            (8, "Customer Insights Dashboard", "Completed"),
            (9, "Payroll Integration", "On Hold"),
            (10, "Partner Enablement Toolkit", "Active")
        };

        foreach (var project in projects)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Projects (ProjectId, ProjectName, Status)
                VALUES (@id, @name, @status);
                """;
            command.Parameters.AddWithValue("@id", project.Id);
            command.Parameters.AddWithValue("@name", project.Name);
            command.Parameters.AddWithValue("@status", project.Status);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedEmployeeBlocksAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var assignments = new (int EmployeeId, int BlockId, string SeatNumber, string DeskLocation, string AssignedFrom)[]
        {
            (1, 1, "A-101", "Pod A1, desk 01", "2026-01-01"),
            (2, 1, "A-102", "Pod A1, desk 02", "2026-01-01"),
            (3, 1, "A-103", "Pod A1, desk 03", "2026-01-01"),
            (4, 1, "A-104", "Pod A2, desk 04", "2026-01-01"),
            (5, 2, "HR-201", "HR service row, desk 01", "2026-01-01"),
            (6, 2, "HR-202", "HR service row, desk 02", "2026-01-01"),
            (7, 2, "HR-203", "HR service row, desk 03", "2026-01-01"),
            (8, 3, "F-301", "Finance pod, desk 01", "2026-01-01"),
            (9, 3, "F-302", "Finance pod, desk 02", "2026-01-01"),
            (10, 3, "F-303", "Finance pod, desk 03", "2026-01-01"),
            (11, 3, "F-304", "Finance planning row, desk 04", "2026-01-01"),
            (12, 4, "S-101", "Sales west row, desk 01", "2026-01-01"),
            (13, 4, "S-102", "Sales west row, desk 02", "2026-01-01"),
            (14, 4, "S-103", "Sales west row, desk 03", "2026-01-01"),
            (15, 4, "S-104", "Sales west row, desk 04", "2026-01-01"),
            (16, 5, "M-201", "Creative pod, desk 01", "2026-01-01"),
            (17, 5, "M-202", "Creative pod, desk 02", "2026-01-01"),
            (18, 5, "M-203", "Campaign row, desk 03", "2026-01-01"),
            (19, 5, "M-204", "Campaign row, desk 04", "2026-01-01"),
            (20, 1, "A-105", "Platform row, desk 05", "2026-01-01"),
            (21, 1, "A-106", "Platform row, desk 06", "2026-01-01"),
            (22, 1, "A-107", "Platform row, desk 07", "2026-01-01"),
            (23, 6, "EX-501", "Executive engineering office", "2026-01-01"),
            (24, 2, "HR-204", "HR leadership office", "2026-01-01"),
            (25, 6, "EX-502", "Executive sales office", "2026-01-01"),
            (26, 6, "EX-503", "Executive finance office", "2026-01-01")
        };

        foreach (var assignment in assignments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO EmployeeBlocks (EmployeeId, BlockId, SeatNumber, DeskLocation, AssignedFrom)
                VALUES (@employeeId, @blockId, @seatNumber, @deskLocation, @assignedFrom)
                ON CONFLICT(EmployeeId) DO UPDATE SET
                    BlockId = excluded.BlockId,
                    SeatNumber = excluded.SeatNumber,
                    DeskLocation = excluded.DeskLocation,
                    AssignedFrom = excluded.AssignedFrom;
                """;
            command.Parameters.AddWithValue("@employeeId", assignment.EmployeeId);
            command.Parameters.AddWithValue("@blockId", assignment.BlockId);
            command.Parameters.AddWithValue("@seatNumber", assignment.SeatNumber);
            command.Parameters.AddWithValue("@deskLocation", assignment.DeskLocation);
            command.Parameters.AddWithValue("@assignedFrom", assignment.AssignedFrom);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedEmployeeProjectsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var assignments = new (int EmployeeId, int ProjectId)[]
        {
            (1, 1), (2, 1), (3, 6), (4, 7), (20, 1),
            (5, 2), (6, 2), (7, 9),
            (8, 3), (9, 3), (10, 8), (11, 7),
            (12, 4), (13, 4), (14, 10), (15, 10),
            (16, 5), (17, 5), (18, 8), (19, 10),
            (21, 1), (22, 1), (23, 1), (24, 2), (25, 10), (26, 7)
        };

        foreach (var assignment in assignments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO EmployeeProjects (EmployeeId, ProjectId)
                VALUES (@employeeId, @projectId);
                """;
            command.Parameters.AddWithValue("@employeeId", assignment.EmployeeId);
            command.Parameters.AddWithValue("@projectId", assignment.ProjectId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string NormalizeTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new McpException("Table name is required.");
        }

        var normalized = AllowedTables.FirstOrDefault(table => string.Equals(table, tableName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
        {
            throw new McpException($"Invalid table name '{tableName}'. Allowed tables are: {string.Join(", ", AllowedTables)}.");
        }

        return normalized;
    }

    private static (string ColumnName, string DisplayColumnName) GetSensitiveColumn(string dataType)
    {
        return dataType switch
        {
            "salary" => ("Salary", "Salary"),
            "personal_phone_number" => ("PersonalPhoneNumber", "PersonalPhoneNumber"),
            "home_address" => ("HomeAddress", "HomeAddress"),
            "performance_rating" => ("PerformanceRating", "PerformanceRating"),
            "bank_account_information" => ("BankAccountInformation", "BankAccountInformation"),
            _ => throw new McpException("Unsupported sensitive data type.")
        };
    }

    private static string FormatSensitiveValue(string dataType, object? value)
    {
        if (value is null)
        {
            return "(not set)";
        }

        if (dataType == "salary" &&
            decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var salary))
        {
            return $"INR {salary:N0}";
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(not set)";
    }

    private static string HashPassword(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hash);
    }

    private static bool PasswordMatches(string password, string storedHash)
    {
        try
        {
            var storedHashBytes = Convert.FromBase64String(storedHash);
            var submittedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(storedHashBytes, submittedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ValidateSelectQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("Query is required and must be a SELECT statement.");
        }

        var trimmedQuery = query.Trim();
        if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException("Only SELECT queries are allowed. Queries must start with SELECT.");
        }

        if (trimmedQuery.Contains(';'))
        {
            throw new McpException("Multiple statements are not allowed. Remove semicolons and submit a single SELECT query.");
        }

        if (trimmedQuery.Contains("--", StringComparison.Ordinal) ||
            trimmedQuery.Contains("/*", StringComparison.Ordinal) ||
            trimmedQuery.Contains("*/", StringComparison.Ordinal))
        {
            throw new McpException("SQL comments are not allowed in ad hoc queries.");
        }

        var queryWithoutStringLiterals = RemoveStringLiterals(trimmedQuery);

        if (MetadataTableRegex.IsMatch(queryWithoutStringLiterals))
        {
            throw new McpException("Access denied. Database metadata tables are not queryable.");
        }

        if (ProtectedAuthorizationTableRegex.IsMatch(queryWithoutStringLiterals))
        {
            throw new McpException("Access denied. Authorization data is not queryable.");
        }

        if (queryWithoutStringLiterals.Contains('*', StringComparison.Ordinal))
        {
            throw new McpException("Wildcard SELECT statements are not allowed. Specify public columns explicitly.");
        }

        var restrictedMatch = RestrictedColumnRegex.Match(queryWithoutStringLiterals);
        if (restrictedMatch.Success)
        {
            throw new McpException("Access denied. Restricted information.");
        }

        var sensitiveMatch = SensitiveColumnRegex.Match(queryWithoutStringLiterals);
        if (sensitiveMatch.Success)
        {
            throw new McpException("Sensitive information requested. If you are authorized, provide your name, role name, email address, and password before requesting this information.");
        }

        var blockedMatch = BlockedKeywordRegex.Match(queryWithoutStringLiterals);
        if (blockedMatch.Success)
        {
            throw new McpException($"Blocked SQL keyword '{blockedMatch.Value}'. This tool only executes read-only SELECT queries.");
        }
    }

    private static string RemoveStringLiterals(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var inSingleQuotedString = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var current = sql[i];

            if (current == '\'')
            {
                builder.Append(' ');

                if (inSingleQuotedString && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    builder.Append(' ');
                    continue;
                }

                inSingleQuotedString = !inSingleQuotedString;
                continue;
            }

            builder.Append(inSingleQuotedString ? ' ' : current);
        }

        return builder.ToString();
    }

    private static async Task<List<Dictionary<string, object?>>> QueryRowsAsync(
        SqliteConnection connection,
        string query,
        CancellationToken cancellationToken,
        Action<SqliteCommand>? configureCommand = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = 15;
        configureCommand?.Invoke(command);

        var rows = new List<Dictionary<string, object?>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task<string> ExecuteTrustedQueryAsync(
        string query,
        CancellationToken cancellationToken,
        Action<SqliteCommand>? configureCommand = null)
    {
        _logger.LogInformation("Executing SQL query");

        await using var connection = await OpenConnectionAsync(readOnly: true, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = 15;
        configureCommand?.Invoke(command);

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<string>();
        var truncated = false;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                columns.Add(reader.GetName(index));
            }

            if (columns.Any(column => RestrictedColumns.Contains(column)))
            {
                throw new McpException("Access denied. Restricted information.");
            }

            if (columns.Any(column => SensitiveColumns.Contains(column)))
            {
                throw new McpException("Sensitive information requested. If you are authorized, provide your name, role name, email address, and password before requesting this information.");
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= MaxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }

                rows.Add(row);
            }
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "SQLite query execution failed");
            throw new McpException($"Database query failed: {ex.Message}", ex);
        }

        return JsonSerializer.Serialize(new
        {
            rowCount = rows.Count,
            truncated,
            message = rows.Count == 0 ? "No rows found." : null,
            columns,
            rows
        }, JsonOptions);
    }
}
