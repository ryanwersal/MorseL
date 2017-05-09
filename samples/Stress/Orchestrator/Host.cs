using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
                Arguments = "Host.dll --host=localhost --port=5000",
                WorkingDirectory = "../Host/bin/Debug/netcoreapp1.1/",
//                RedirectStandardError = true,
//                RedirectStandardInput = true,
//                RedirectStandardOutput = true
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
