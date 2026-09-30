using System.CommandLine;
using System.Text;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// <c>export-clausole [--out docs/clausole.md]</c> (fase-9b.md §4): il catalogo delle clausole in Markdown, dal DB. Si rilancia solo
/// quando cambia <c>db/003_seed_clausole.sql</c>; un test di integrazione segnala il catalogo non allineato.
/// </summary>
internal static class ExportClausoleCommand
{
    public static Command Create(IServiceProvider services)
    {
        var output = new Option<FileInfo?>("--out") { Description = "File Markdown (default docs/clausole.md)." };
        var command = new Command("export-clausole", "Scrive il catalogo delle clausole in Markdown.") { output };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string percorso = parseResult.GetValue(output)?.FullName
                ?? Path.Combine(Directory.GetParent(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory))!.FullName, "docs", "clausole.md");

            try
            {
                IReadOnlyList<ClausolaDettaglio> clausole = await services.GetRequiredService<IConsultazioneRepository>()
                    .GetClausoleAsync(prodotto: null, tipo: null, testo: null, cancellationToken);

                if (clausole.Count == 0)
                {
                    await Console.Error.WriteLineAsync("Nessuna clausola nel database: eseguire DbInit.");

                    return SinistriCli.ExitError;
                }

                string markdown = ClausoleMarkdownRenderer.Render(clausole, DateOnly.FromDateTime(DateTime.Today));
                await File.WriteAllTextAsync(percorso, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
                Console.WriteLine($"{clausole.Count} clausole scritte in {percorso}");

                return 0;
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or Microsoft.Data.SqlClient.SqlException)
            {
                await Console.Error.WriteLineAsync($"export-clausole non completato: {exception.Message}");

                return SinistriCli.ExitError;
            }
        });

        return command;
    }
}
