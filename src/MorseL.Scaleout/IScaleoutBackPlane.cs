
using System;
using System.Threading.Tasks;
using MorseL.Sockets;

namespace MorseL.Scaleout
{
    public interface IScaleoutBackPlane
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        Task Register(Connection connection);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        Task UnRegister(Connection connection);
    }

    public class ScaleoutBackPlane : IScaleoutBackPlane
    {
        public Task Register(Connection connection)
        {
            return Task.CompletedTask;
        }

        public Task UnRegister(Connection connection)
        {
            return Task.CompletedTask;
        }
    }
}
