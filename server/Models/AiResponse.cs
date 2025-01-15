namespace server.Models;

public sealed record AiResponse(string UserId, ulong QueueNo, ulong WaitingCount);