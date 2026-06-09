using System.Text.RegularExpressions;
using McpServer.Database;
using Microsoft.Extensions.Logging;

namespace McpServer.Security;

public sealed record CurrentUser(int UserId, string UserName, string Email, string Role);

public sealed record SensitiveDataType(string CanonicalName, string DisplayName);

public sealed record AuthorizationDecision(
    bool IsAllowed,
    bool IsRestricted,
    string DataType,
    string DisplayName,
    string Message,
    CurrentUser User);

public sealed class AuthorizationService
{
    private static readonly Regex DataTypeCleaner = new("[^a-z0-9]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, SensitiveDataType> SensitiveDataTypes =
        new Dictionary<string, SensitiveDataType>(StringComparer.OrdinalIgnoreCase)
        {
            ["salary"] = new("salary", "Salary"),
            ["compensation"] = new("salary", "Salary"),
            ["personalphonenumber"] = new("personal_phone_number", "Personal Phone Number"),
            ["personalphone"] = new("personal_phone_number", "Personal Phone Number"),
            ["phone"] = new("personal_phone_number", "Personal Phone Number"),
            ["homeaddress"] = new("home_address", "Home Address"),
            ["address"] = new("home_address", "Home Address"),
            ["performancerating"] = new("performance_rating", "Performance Rating"),
            ["performance"] = new("performance_rating", "Performance Rating"),
            ["rating"] = new("performance_rating", "Performance Rating"),
            ["bankaccountinformation"] = new("bank_account_information", "Bank Account Information"),
            ["bankaccount"] = new("bank_account_information", "Bank Account Information"),
            ["bank"] = new("bank_account_information", "Bank Account Information")
        };

    private static readonly HashSet<string> RestrictedDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwords",
        "passwordhash",
        "apikey",
        "apikeys",
        "secret",
        "secrets",
        "token",
        "tokens"
    };

    private static readonly HashSet<string> DirectorDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "salary",
        "performance_rating"
    };

    private readonly DatabaseService _databaseService;
    private readonly ILogger<AuthorizationService> _logger;

    // Store authenticated users (email -> User mapping)
    private readonly Dictionary<string, CurrentUser> _authenticatedUsers = new(StringComparer.OrdinalIgnoreCase);

    public AuthorizationService(DatabaseService databaseService, ILogger<AuthorizationService> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<(bool Success, CurrentUser? User, string Message)> LoginAsync(
        string name,
        string roleName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var (success, memberId, memberName, memberEmail, role, message) =
            await _databaseService.VerifyAuthorizedMemberAsync(name, roleName, email, password, cancellationToken);

        if (!success)
        {
            return (false, null, message);
        }

        var user = new CurrentUser(memberId, memberName, memberEmail, role);
        _authenticatedUsers[memberEmail] = user;

        _logger.LogInformation("User authenticated: {Email} ({Role})", memberEmail, role);
        return (true, user, message);
    }

    public bool IsAuthenticated(string email)
    {
        return _authenticatedUsers.ContainsKey(email);
    }

    public CurrentUser? GetAuthenticatedUser(string email)
    {
        _authenticatedUsers.TryGetValue(email, out var user);
        return user;
    }

    public void Logout(string email)
    {
        if (_authenticatedUsers.Remove(email))
        {
            _logger.LogInformation("User logged out: {Email}", email);
        }
    }

    public bool IsRestrictedDataType(string dataType)
    {
        return RestrictedDataTypes.Contains(NormalizeDataTypeKey(dataType));
    }

    public bool TryGetSensitiveDataType(string dataType, out SensitiveDataType sensitiveDataType)
    {
        return SensitiveDataTypes.TryGetValue(NormalizeDataTypeKey(dataType), out sensitiveDataType!);
    }

    public AuthorizationDecision AuthorizeSensitiveAccess(
        CurrentUser user,
        string employeeName,
        string dataType)
    {
        if (IsRestrictedDataType(dataType))
        {
            _logger.LogWarning(
                "Restricted data request denied for user {Email}. Employee={EmployeeName}, DataType={DataType}",
                user.Email,
                employeeName,
                dataType);

            return new AuthorizationDecision(
                IsAllowed: false,
                IsRestricted: true,
                DataType: dataType,
                DisplayName: dataType,
                Message: "Access denied. Restricted information.",
                User: user);
        }

        if (!TryGetSensitiveDataType(dataType, out var sensitiveDataType))
        {
            _logger.LogWarning(
                "Unsupported sensitive data type requested by {Email}. Employee={EmployeeName}, DataType={DataType}",
                user.Email,
                employeeName,
                dataType);

            return new AuthorizationDecision(
                IsAllowed: false,
                IsRestricted: false,
                DataType: dataType,
                DisplayName: dataType,
                Message: "Unsupported sensitive data type.",
                User: user);
        }

        _logger.LogInformation(
            "Sensitive data request received. User={Email}, Role={Role}, Employee={EmployeeName}, DataType={DataType}",
            user.Email,
            user.Role,
            employeeName,
            sensitiveDataType.CanonicalName);

        var role = user.Role.Trim();
        var isAllowed = role switch
        {
            "Admin" => true,
            "HR" => sensitiveDataType.CanonicalName == "salary",
            "Director" => DirectorDataTypes.Contains(sensitiveDataType.CanonicalName),
            "Manager" => DirectorDataTypes.Contains(sensitiveDataType.CanonicalName),
            _ => false
        };

        if (!isAllowed)
        {
            _logger.LogWarning(
                "Sensitive data authorization failed. User={Email}, Role={Role}, Employee={EmployeeName}, DataType={DataType}",
                user.Email,
                user.Role,
                employeeName,
                sensitiveDataType.CanonicalName);

            return new AuthorizationDecision(
                IsAllowed: false,
                IsRestricted: false,
                DataType: sensitiveDataType.CanonicalName,
                DisplayName: sensitiveDataType.DisplayName,
                Message: "Access denied. Your verified role is not authorized to access this sensitive information.",
                User: user);
        }

        return new AuthorizationDecision(
            IsAllowed: true,
            IsRestricted: false,
            DataType: sensitiveDataType.CanonicalName,
            DisplayName: sensitiveDataType.DisplayName,
            Message: "Authorized.",
            User: user);
    }

    private static string NormalizeDataTypeKey(string dataType)
    {
        return DataTypeCleaner.Replace(dataType.Trim().ToLowerInvariant(), string.Empty);
    }
}
