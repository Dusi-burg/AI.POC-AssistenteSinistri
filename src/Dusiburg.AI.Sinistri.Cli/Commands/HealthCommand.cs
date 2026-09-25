using System.CommandLine;
using Dusiburg.AI.Sinistri.Core.Health;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary><c>health</c>: SQL, Ollama, modelli, dimensione dell'embedding e posizionamento (fase-1.md §6).</summary>
internal static class HealthCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("health", "Controlla SQL Server, Ollama, modelli e dimensione dell'embedding.");

        command.SetAction(async (_, cancellationToken) =>
        {
            ProbeReport report = await services.GetRequiredService<HealthService>().RunAsync(cancellationToken);

            Write(report, Console.Out);

            return report.ExitCode;
        });

        return command;
    }

    internal static void Write(ProbeReport report, TextWriter output)
    {
        int nameWidth = Math.Max("Controllo".Length, report.Controlli.Max(c => c.Nome.Length));

        output.WriteLine($" #  {"Controllo".PadRight(nameWidth)}  Esito     Durata  Dettaglio");

        foreach (ProbeResult controllo in report.Controlli)
        {
            string durata = $"{controllo.Durata.TotalMilliseconds:0} ms";

            output.WriteLine($"{controllo.Numero,2}  {controllo.Nome.PadRight(nameWidth)}  {Label(controllo.Stato),-7} {durata,8}  {controllo.Dettaglio}");
        }

        output.WriteLine();
        output.WriteLine(report.HasErrors
            ? "Esito: ERRORE — almeno un controllo non è superato."
            : report.HasWarnings ? "Esito: OK con avvisi." : "Esito: OK.");
    }

    private static string Label(ProbeStatus stato) => stato switch
    {
        ProbeStatus.Ok => "OK",
        ProbeStatus.Info => "INFO",
        ProbeStatus.Warning => "AVVISO",
        _ => "ERRORE"
    };
}
