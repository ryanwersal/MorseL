using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketManager.Client
{
    internal struct InvocationRequest
    {
        public Type ResultType { get; }
        public CancellationToken CancellationToken { get; }
        public CancellationTokenRegistration Registration { get; }
        public TaskCompletionSource<object> Completion { get; }

        public InvocationRequest(CancellationToken cancellationToken, Type resultType)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Completion = tcs;
            CancellationToken = cancellationToken;
            Registration = cancellationToken.Register(() => tcs.TrySetCanceled());
            ResultType = resultType;
        }
    }
}
