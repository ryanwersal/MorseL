using Microsoft.Extensions.CommandLineUtils;
using Serilog;

namespace Orchestrator
{
    public class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "MorseL Stress Test Conductor"
            };
            app.HelpOption("-?|-h|--help");
            var clients = app.Option("--clients", "Number of client processes to create", CommandOptionType.SingleValue, false);

            app.OnExecute(async () =>
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.ColoredConsole()
                    .CreateLogger();

                using (var conductor = new Conductor.Conductor(int.Parse(clients.Value())))
                {
                    await conductor.StartAsync();
                }

                return 0;
            });

            app.Execute(args);
        }
    }
}
