using McpServer.Database;
using McpServer.Security;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

var databasePath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : throw new InvalidOperationException("Database path argument is required.");

Environment.SetEnvironmentVariable("EMPLOYEES_DB_PATH", databasePath);

var environment = new SmokeHostEnvironment
{
    ApplicationName = "SmokeTest",
    ContentRootPath = Directory.GetCurrentDirectory(),
    EnvironmentName = Environments.Development
};

var databaseService = new DatabaseService(environment, NullLogger<DatabaseService>.Instance);
await databaseService.InitializeAsync(CancellationToken.None);

Console.WriteLine("PUBLIC_TABLES");
Console.WriteLine(await databaseService.ListTablesAsync(CancellationToken.None));

Console.WriteLine("PUBLIC_SELECT");
Console.WriteLine(await databaseService.RunSelectAsync("SELECT Name, Email FROM Employees ORDER BY Name LIMIT 2", CancellationToken.None));

Console.WriteLine("HR_BLOCK");
Console.WriteLine(await databaseService.GetHrBlockAsync(CancellationToken.None));

Console.WriteLine("EMPLOYEE_BLOCK_ALL");
Console.WriteLine(await databaseService.GetEmployeeBlockAsync("all", CancellationToken.None));

Console.WriteLine("COMPANY_LOCATION_MAP");
Console.WriteLine(await databaseService.GetCompanyLocationMapAsync(CancellationToken.None));

Console.WriteLine("PUBLIC_INFO_WITH_BLOCK");
Console.WriteLine(await databaseService.GetEmployeePublicInfoAsync("Kiran", CancellationToken.None));

Console.WriteLine("BLOCKED_SALARY_SELECT");
try
{
    Console.WriteLine(await databaseService.RunSelectAsync("SELECT Name, Salary FROM Employees ORDER BY Name LIMIT 2", CancellationToken.None));
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

Console.WriteLine("BLOCKED_PASSWORD_SELECT");
try
{
    Console.WriteLine(await databaseService.RunSelectAsync("SELECT PasswordHash FROM AuthorizedMembers LIMIT 1", CancellationToken.None));
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

var authorizationService = new AuthorizationService(databaseService, NullLogger<AuthorizationService>.Instance);

Console.WriteLine("BAD_LOGIN");
var badLogin = await authorizationService.LoginAsync("Priya", "HR", "hr@company.com", "wrong", CancellationToken.None);
Console.WriteLine($"{badLogin.Success}: {badLogin.Message}");

Console.WriteLine("GOOD_LOGIN");
var goodLogin = await authorizationService.LoginAsync("Priya", "HR", "hr@company.com", "Priya@123", CancellationToken.None);
Console.WriteLine($"{goodLogin.Success}: {goodLogin.Message}");

Console.WriteLine("AUTHORIZED_SALARY");
var decision = authorizationService.AuthorizeSensitiveAccess(goodLogin.User!, "all", "salary");
Console.WriteLine($"{decision.IsAllowed}: {decision.Message}");
Console.WriteLine(await databaseService.GetSensitiveEmployeeDataAsync("all", decision.DataType, CancellationToken.None));

Console.WriteLine("RESTRICTED_PASSWORD_REQUEST");
var passwordDecision = authorizationService.AuthorizeSensitiveAccess(goodLogin.User!, "all", "password");
Console.WriteLine($"{passwordDecision.IsAllowed}: {passwordDecision.Message}");

if (args.Length > 1)
{
    var exportPath = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
    await File.WriteAllTextAsync(exportPath, await databaseService.GetCompanyDocumentationDataAsync(CancellationToken.None));

    Console.WriteLine("DOCUMENTATION_EXPORT");
    Console.WriteLine(exportPath);
}

sealed class SmokeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "";

    public string ApplicationName { get; set; } = "";

    public string ContentRootPath { get; set; } = "";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
