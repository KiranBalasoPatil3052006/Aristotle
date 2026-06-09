using System.ComponentModel;
using McpServer.Database;
using McpServer.Security;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class SensitiveDataProtectionTools
{
    [McpServerTool(Name = "member_login", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Authorize sensitive-data access by matching all four fields against the AuthorizedMembers table. Ask the user for these fields in this exact order before calling: name, role name, email address, password. Do not generate or request verification codes.")]
    public static async Task<string> MemberLoginAsync(
        AuthorizationService authorizationService,
        ILogger<SensitiveDataProtectionTools> logger,
        [Description("Authorized member name, for example Priya")]
        string name,
        [Description("Authorized member role name, for example HR or Manager")]
        string roleName,
        [Description("Authorized member email address, for example hr@company.com")]
        string emailAddress,
        [Description("Member password")]
        string password,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: member_login for email: {Email}", emailAddress);

        var (success, user, message) = await authorizationService.LoginAsync(name, roleName, emailAddress, password, cancellationToken);

        if (!success)
        {
            logger.LogWarning("Authorization failed for email: {Email}", emailAddress);
            return $"Verification failed: {message}";
        }

        logger.LogInformation("Authorization successful for member: {Name} ({Email}) with role: {Role}", user!.UserName, user.Email, user.Role);
        return $"Verification successful. Welcome {user.UserName} ({user.Role}). You can now access sensitive employee data.";
    }

    [McpServerTool(Name = "get_sensitive_employee_data", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return sensitive employee data only after member_login succeeds with name, role name, email address, and password. Supports salary, personal phone number, home address, performance rating, and bank account information. Restricted data is always denied.")]
    public static async Task<string> GetSensitiveEmployeeDataAsync(
        DatabaseService databaseService,
        AuthorizationService authorizationService,
        ILogger<SensitiveDataProtectionTools> logger,
        [Description("Authorized member email address used during member_login")]
        string memberEmail,
        [Description("Employee name, for example Arun, Aarav Mehta, or Sophia Carter. Use all to return every employee.")]
        string employeeName,
        [Description("Sensitive data type: salary, personal_phone_number, home_address, performance_rating, or bank_account_information. Restricted values such as password, password_hash, api_key, token, and secret are denied.")]
        string dataType,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Tool invoked: get_sensitive_employee_data for member: {Email}, employee: {EmployeeName}, data: {DataType}",
            memberEmail,
            employeeName,
            dataType);

        if (!authorizationService.IsAuthenticated(memberEmail))
        {
            logger.LogWarning("Sensitive data access denied - member not authenticated. Email={Email}", memberEmail);
            return "Sensitive information requested. Access is not available until authorization is completed. If you are authorized, provide your name, role name, email address, and password, then use member_login.";
        }

        var user = authorizationService.GetAuthenticatedUser(memberEmail);
        if (user is null)
        {
            return "Access denied. Could not retrieve authenticated member information.";
        }

        var authorization = authorizationService.AuthorizeSensitiveAccess(user, employeeName, dataType);
        if (!authorization.IsAllowed)
        {
            logger.LogWarning(
                "Sensitive data access denied by authorization. Member={Email}, Employee={EmployeeName}, DataType={DataType}, Reason={Message}",
                memberEmail,
                employeeName,
                dataType,
                authorization.Message);
            return authorization.Message;
        }

        var data = await databaseService.GetSensitiveEmployeeDataAsync(employeeName, authorization.DataType, cancellationToken);

        logger.LogInformation(
            "Sensitive data access granted. Member={Email}, Role={Role}, Employee={EmployeeName}, DataType={DataType}",
            user.Email,
            user.Role,
            employeeName,
            authorization.DataType);

        return $"Verification successful. These are the employee details and {authorization.DisplayName.ToLowerInvariant()} information:\n{data}";
    }

    [McpServerTool(Name = "get_employee_public_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return non-sensitive employee information: employee name, department, project names, work email, block location, and seat. No authentication required.")]
    public static async Task<string> GetEmployeePublicInfoAsync(
        DatabaseService databaseService,
        ILogger<SensitiveDataProtectionTools> logger,
        [Description("Employee name, for example Arun, Aarav Mehta, or Sophia Carter.")]
        string employeeName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool invoked: get_employee_public_info for {EmployeeName}", employeeName);
        return await databaseService.GetEmployeePublicInfoAsync(employeeName, cancellationToken);
    }

    [McpServerTool(Name = "member_logout", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Logout an authenticated member, ending their sensitive data access session.")]
    public static string MemberLogout(
        AuthorizationService authorizationService,
        ILogger<SensitiveDataProtectionTools> logger,
        [Description("Member email address")]
        string memberEmail)
    {
        logger.LogInformation("Tool invoked: member_logout for email: {Email}", memberEmail);
        authorizationService.Logout(memberEmail);
        return $"Member {memberEmail} has been logged out.";
    }
}
