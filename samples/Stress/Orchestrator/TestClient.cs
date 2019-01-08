using System;
using System.Diagnostics;
using System.IO;
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
                Arguments = $"Client.dll --name=\"{_name}\" --host=127.0.0.1 --port=5000",
                WorkingDirectory = Path.GetFullPath("../../../../Client/bin/Debug/netcoreapp2.1/"),
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
