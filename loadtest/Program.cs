using Flurl.Http;
using Microsoft.AspNetCore.SignalR.Client;

try
{
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

    connection.On<string>("AiResponse", Console.Out.WriteLineAsync);

    await connection.StartAsync();
    Console.WriteLine("Connection started, Enter message (or 'exit' to close).");

    foreach (var question in questions)
    {
        await $"{Host}/api/chat/{connection.ConnectionId}/{question}".GetAsync();
    }

    await Console.In.ReadLineAsync();
    await connection.DisposeAsync();
    Console.WriteLine("Connection closed");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}