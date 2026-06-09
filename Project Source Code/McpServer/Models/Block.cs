namespace McpServer.Models;

public sealed record Block(
    int BlockId,
    string BlockCode,
    string BlockName,
    string Campus,
    string Building,
    string Floor,
    string Wing,
    string LocationDescription,
    string Purpose,
    bool IsActive);
