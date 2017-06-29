using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Orchestrator
{
    public class TestClient
    {
        private readonly string _name;
        private Process _process;

        public TestClient(string name)
        {
            _name = name;
        }

        public Task StartAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                FileName = "dotnet",
                Arguments = $"Client.dll --name=\"{_name}\" --host=localhost --port=5000",
                WorkingDirectory = "../Client/bin/Debug/netcoreapp1.1/",
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
