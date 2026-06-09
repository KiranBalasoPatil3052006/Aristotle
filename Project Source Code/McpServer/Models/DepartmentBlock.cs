namespace McpServer.Models;

public sealed record DepartmentBlock(int DepartmentId, int BlockId, bool IsPrimary, string Notes);
