using System;
using System.Threading.Tasks;
using MorseL;
using MorseL.Common;

public class ClientInvoker : IClientInvoker
    {
        private Func<string, object[],  Task> InvokeFunc { get; }
        private Func<Message, Task> SendMessageFunc { get; }
        private Func<string, Task> SubscribeFunc { get; }
        private Func<string, Task> UnsubscribeFunc { get; }

        public ClientInvoker(
            Func<string, object[], Task> invokeFunc, Func<Message, Task> sendMessageFunc,
            Func<string, Task> subscribeFunc, Func<string, Task> unsubscribeFunc)
        {
            InvokeFunc = invokeFunc;
            SendMessageFunc = sendMessageFunc;
            SubscribeFunc = subscribeFunc;
            UnsubscribeFunc = unsubscribeFunc;
        }

        public Task InvokeAsync(string methodName, params object[] args)
        {
            return InvokeFunc.Invoke(methodName, args);
        }

        public Task SendMessageAsync(Message message)
        {
            return SendMessageFunc.Invoke(message);
        }

        public Task Subscribe(string group)
        {
            return SubscribeFunc.Invoke(group);
        }

        public Task Unsubscribe(string group)
        {
            return UnsubscribeFunc.Invoke(group);
        }
    }