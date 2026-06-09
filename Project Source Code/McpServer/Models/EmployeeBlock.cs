namespace McpServer.Models;

public sealed record EmployeeBlock(
    int EmployeeId,
    int BlockId,
    string SeatNumber,
    string DeskLocation,
    string AssignedFrom);
