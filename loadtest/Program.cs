using Flurl.Http;
using Microsoft.AspNetCore.SignalR.Client;

static async Task ShowMessageFromSignalR(string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkGreen;
    if (msg == "[$ENDED$]")
    {
        await Console.Out.WriteLineAsync();
    }
    else
    {
        await Console.Out.WriteAsync(msg);
    }
    Console.ForegroundColor = ConsoleColor.White;
};

try
{
    var isStreaming = true;
    List<string> questions =
    [
        "Tell me a joke",
        "What is the weather in London?",
        "What is the time?",
        "How the stock market is doing?",
        "Is a tomato a fruit or a vegetable?",
        "What is the capital of France?",
    ];

    const string Host = "https://localhost:7279";
    var connection = new HubConnectionBuilder()
        .WithUrl($"{Host}/hub")
        .Build();

    connection.On<string>("AiResponse", ShowMessageFromSignalR);
    connection.On<string>("AiStreamResponse", ShowMessageFromSignalR);

    await connection.StartAsync();

    foreach (var question in questions)
    {
        await Console.Out.WriteAsync($"Question: {question}");
        var response = await $"{Host}/api/chat/{connection.ConnectionId}/{question}?streaming={isStreaming}".GetJsonAsync<AiResponse>();
        await Console.Out.WriteLineAsync($" (Wait: {response.WaitingCount} | QueueNo: {response.QueueNo})");
    }

    await Console.In.ReadLineAsync();
    await connection.DisposeAsync();
    Console.WriteLine("Connection closed");
    await connection.StopAsync();
    await connection.DisposeAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

public sealed record AiResponse(string UserId, ulong QueueNo, ulong WaitingCount);