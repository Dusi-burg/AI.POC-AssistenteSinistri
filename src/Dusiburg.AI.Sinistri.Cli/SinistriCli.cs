using System.CommandLine;
using Dusiburg.AI.Sinistri.Cli.Commands;

namespace Dusiburg.AI.Sinistri.Cli;

/// <summary>
/// Riga di comando del POC. Exit code 0 esito positivo, 1 errore (configurazione, controlli falliti, rete, modello).
/// I comandi successivi (<c>embed</c>, <c>search-*</c>, <c>ask</c>, <c>fraud-scan</c>, <c>eval</c>) si aggiungono qui fase per fase.
/// </summary>
internal static class SinistriCli
{
    public const string VerboseOption = "--verbose";

    public const int ExitError = 1;

    public static async Task<int> InvokeAsync(string[] args, IServiceProvider services)
    {
        // Letta prima di costruire l'host (Program.cs); dichiarata qui perché compaia nell'help.
        var verbose = new Option<bool>(VerboseOption) { Description = "Mostra a console anche i log informativi.", Recursive = true };

        var root = new RootCommand("Assistente pre-istruttoria sinistri: RAG locale con SQL Server 2025 e Ollama.")
        {
            verbose,
            HealthCommand.Create(services),
            EmbedCommand.Create(services),
            SearchCommands.Clausole(services),
            SearchCommands.Sinistri(services),
            AskCommand.Create(services),
            FraudScanCommand.Create(services),
            EvalCommand.Create(services),
            BenchSearchCommand.Create(services)
        };

        return await root.Parse(args).InvokeAsync();
    }
}
