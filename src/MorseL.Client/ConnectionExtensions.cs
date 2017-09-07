using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Client
{
    public static class ConnectionExtensions
    {
        public static void On(this Connection connection, string methodName, Action onData)
        {
            connection.On(methodName, new Type[0], data =>
            {
                onData.Invoke();
            });
        }
        public static void On<T1>(this Connection connection, string methodName, Action<T1> onData)
        {
            connection.On(methodName, new[] { typeof(T1) }, data =>
            {
                onData.Invoke((T1)data[0]);
            });
        }
        public static void On<T1, T2>(this Connection connection, string methodName, Action<T1, T2> onData)
        {
            connection.On(methodName, new[] { typeof(T1), typeof(T2) }, data =>
            {
                onData.Invoke((T1)data[0], (T2)data[1]);
            });
        }
        public static void On<T1, T2, T3>(this Connection connection, string methodName, Action<T1, T2, T3> onData)
        {
            connection.On(methodName, new[] { typeof(T1), typeof(T2), typeof(T3) }, data =>
            {
                onData.Invoke((T1)data[0], (T2)data[1], (T3)data[2]);
            });
        }
        public static void On<T1, T2, T3, T4>(this Connection connection, string methodName, Action<T1, T2, T3, T4> onData)
        {
            connection.On(methodName, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, data =>
            {
                onData.Invoke((T1)data[0], (T2)data[1], (T3)data[2], (T4)data[3]);
            });
        }
        public static void On<T1, T2, T3, T4, T5>(this Connection connection, string methodName, Action<T1, T2, T3, T4, T5> onData)
        {
            connection.On(methodName, new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) }, data =>
            {
                onData.Invoke((T1)data[0], (T2)data[1], (T3)data[2], (T4)data[3], (T5)data[4]);
            });
        }
    }
}
