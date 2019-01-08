using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Orchestrator
{
    public class Host : IDisposable
    {
        private Process _process;

        public Task StartAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                FileName = "dotnet",
                Arguments = "Host.dll --host=127.0.0.1 --port=5000",
                WorkingDirectory = Path.GetFullPath("../../../../Host/bin/Debug/netcoreapp2.1/"),
            };
            _process = new Process { StartInfo = startInfo };
            _process.Start();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _process.Kill();
                _process?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
