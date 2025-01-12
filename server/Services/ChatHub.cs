using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using server.Models;

namespace server.Services;

public sealed class ChatHub : Hub
{
    private static Dictionary<string, List<ChatMessage>> UserChatContext = [];

    public static List<ChatMessage> GetChatMessages(AiRequest request)
    {
        if (!UserChatContext.TryGetValue(request.UserId, out var history))
        {
            history = new();
            UserChatContext.Add(request.UserId, history);
        }
        history.Add(new ChatMessage(ChatRole.User, request.Prompt));
        return history;
    }

    public Task SendMessageToClient(string connectionId, string message)
        => Clients.Client(connectionId).SendAsync("AiResponse", message);
}