using System;
using System.Threading.Tasks;
using WebSocketManager.Client;

public class Program
{
    private static Connection _connection;

    public static void Main(string[] args)
    {
        StartConnectionAsync();

        _connection.On("receiveMessage", (arguments) =>
        {
            Console.WriteLine($"{arguments[0]} said: {arguments[1]}");
        });

        Console.WriteLine("// Type your message and hit Enter to send. Type '/quit' or '/exit' to close.");
        while (true)
        {
            var line = Console.ReadLine();
            if (line == "/quit" || line == "/exit") break;

            SendMessage(line);
        }

        StopConnectionAsync();
    }

    public static async Task StartConnectionAsync()
    {
        _connection = new Connection();
        await _connection.StartConnectionAsync("ws://localhost:65110/chat");
    }

    public static async Task StopConnectionAsync()
    {
        await _connection.StopConnectionAsync();
    }

    public static async Task SendMessage(string message)
    {
        await _connection.Invoke("SendMessage", _connection.ConnectionId, message);
    }
}