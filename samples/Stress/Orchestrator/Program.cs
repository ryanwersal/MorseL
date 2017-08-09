using Microsoft.Extensions.CommandLineUtils;
using Serilog;

namespace Orchestrator
{
    public class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "MorseL Stress Test Conductor";
            app.HelpOption("-?|-h|--help");
            var clients = app.Option("--clients", "Number of connections to establish", CommandOptionType.SingleValue, false);

            app.OnExecute(async () =>
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.ColoredConsole()
                    .CreateLogger();

                await new Conductor.Conductor(int.Parse(clients.Value())).StartAsync();
                return 0;
            });

            app.Execute(args);
        }
    }
}