using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WebSocketManager.Client;

public class Program
{
    private static Connection _connection;

    public static void Main(string[] args)
    {
        StartConnectionAsync();

        _connection.On("receiveMessage", new [] { typeof(string), typeof(string) }, (arguments) =>
        {
            Console.WriteLine($"{arguments[0]} said: {arguments[1]}");
        });

        Console.WriteLine("// Type your message and hit Enter to send. Type '/quit' or '/exit' to close.");
        while (true)
        {
            var line = Console.ReadLine();
            if (line == "/quit" || line == "/exit") break;

            if (line == "/ping")
            {
                Ping().Wait();
            }
            else
            {
                SendMessage(line).Wait();
            }
        }

        StopConnectionAsync().Wait();
    }

    public static async Task StartConnectionAsync()
    {
        _connection = new Connection();
        await _connection.StartAsync(new Uri("ws://localhost:65110/chat"));
    }

    public static async Task StopConnectionAsync()
    {
        await _connection.DisposeAsync();
    }

    public static async Task SendMessage(string message)
    {
        await _connection.Invoke<object>("SendMessage", _connection.ConnectionId, message);
    }

    public static async Task Ping()
    {
        var result = await _connection.Invoke<string>("Ping");
        Debug.WriteLine(result);
    }
}