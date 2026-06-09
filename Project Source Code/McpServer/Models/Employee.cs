namespace McpServer.Models;

public sealed record Employee(
    int EmployeeId,
    string Name,
    string Email,
    int DepartmentId,
    int Salary,
    string PersonalPhoneNumber,
    string HomeAddress,
    string PerformanceRating,
    string BankAccountInformation);
