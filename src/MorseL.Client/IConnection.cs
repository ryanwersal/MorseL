using System;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Client.Middleware;

namespace MorseL.Client
{
    public interface IConnection
    {
        string ConnectionId { get; set; }
        bool IsConnected { get; }

        event Action<Exception> Closed;
        event Action Connected;
        event Action<Exception> Error;

        void AddMiddleware(IMiddleware middleware);
        Task DisposeAsync(CancellationToken ct = default(CancellationToken));
        Task Invoke(string methodName, params object[] args);
        Task<object> Invoke(string methodName, Type returnType, CancellationToken cancellationToken, params object[] args);
        Task<object> Invoke(string methodName, Type returnType, params object[] args);
        Task<T> Invoke<T>(string methodName, CancellationToken cancellationToken, params object[] args);
        Task<T> Invoke<T>(string methodName, params object[] args);
        void On(string methodName, Type[] types, Action<object[]> handler);
        Task StartAsync(CancellationToken ct = default(CancellationToken));
    }
}