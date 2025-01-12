namespace server.Models;

public sealed record AiServer
{
    public required string Host { get; init; }
    public required string Model { get; init; }
}