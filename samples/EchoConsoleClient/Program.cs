using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Client;
using MorseL.Client.Middleware;
using MorseL.Client.WebSockets;

public class Program
{
    private static Connection _connection;

    public static void Main(string[] args)
    {
        StartConnectionAsync().Wait();

        _connection.On("receiveMessage", new [] { typeof(string), typeof(string) }, (arguments) =>
        {
            Console.WriteLine($"{arguments[0]} said: {arguments[1]}");
        });

        Console.WriteLine("// Type your message and hit Enter to send. Type '/quit' or '/exit' to close.");
        while (true)
        {
            var line = Console.In.ReadLineAsync().Result;
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
        _connection = new Connection("ws://localhost:5000/chat", config: option =>
        {
            option.EnableAutoSendPing = true;
            option.AutoSendPingIntervalSeconds = 5;
        }, securityConfig: option =>
        {
            option.Certificates.Add(new X509Certificate2("client.pfx"));
            option.AllowUnstrustedCertificate = true;
            option.AllowNameMismatchCertificate = true;
        }, logger: new LoggerFactory().AddConsole().CreateLogger<Program>());

        _connection.AddMiddleware(new Middleware());

        await _connection.StartAsync();
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

    private class Middleware : IMiddleware
    {
        public Task SendAsync(string data, TransmitDelegate next)
        {
            data = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
            return next(data);
        }

        public Task RecieveAsync(WebSocketPacket packet, RecieveDelegate next)
        {
            var data = Convert.FromBase64String(Encoding.UTF8.GetString(packet.Data));
            return next(new WebSocketPacket(packet.MessageType, data));
        }
    }
}