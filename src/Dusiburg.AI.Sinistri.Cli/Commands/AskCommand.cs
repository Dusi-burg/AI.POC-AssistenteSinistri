using System.CommandLine;
using Dusiburg.AI.Sinistri.Ai.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// <c>ask "&lt;denuncia&gt;" --polizza &lt;numero&gt;</c>: scheda di pre-istruttoria in Markdown (fase-6.md §7). Uscita 0 scheda generata
/// (anche con avvisi), 2 polizza inesistente o non in vigore, 1 altri errori.
/// </summary>
internal static class AskCommand
{
    public const int ExitPolizza = 2;

    public static Command Create(IServiceProvider services)
    {
        var denuncia = new Argument<string>("denuncia") { Description = "Testo della denuncia." };
        var polizza = new Option<string>("--polizza") { Description = "Numero di polizza (es. CF-DEMO-000001).", Required = true };
        var dataEvento = new Option<DateOnly?>("--data-evento") { Description = "Data dell'evento (aaaa-mm-gg): controlla la validità della polizza." };
        Option<CausaSinistro?> causa = OpzioniDominio.OpzioneCausa();
        var riparatore = new Option<int?>("--riparatore") { Description = "Id del riparatore (controllo antifrode, Fase 7)." };
        var output = new Option<FileInfo?>("--out") { Description = "Salva la scheda Markdown su file." };
        var raw = new Option<bool>("--raw") { Description = "Mostra i token JSON del modello man mano (debug)." };

        var command = new Command("ask", "Genera la scheda di pre-istruttoria di una denuncia.")
        {
            denuncia, polizza, dataEvento, causa, riparatore, output, raw
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var richiesta = new RichiestaPreIstruttoria(
                parseResult.GetValue(denuncia)!, parseResult.GetValue(polizza)!, parseResult.GetValue(dataEvento), parseResult.GetValue(causa),
                parseResult.GetValue(riparatore));
            Action<string>? tokenRaw = parseResult.GetValue(raw) ? token => Console.Write(token) : null;

            try
            {
                EsitoPreIstruttoria esito = await services.GetRequiredService<PreIstruttoriaService>()
                    .GeneraAsync(richiesta, new AvanzamentoConsole(), tokenRaw, cancellationToken);
                string markdown = SchedaMarkdownRenderer.Render(esito);

                Console.WriteLine();
                Console.WriteLine(markdown);

                if (parseResult.GetValue(output) is { } file)
                {
                    await File.WriteAllTextAsync(file.FullName, markdown, cancellationToken);
                    Console.WriteLine($"Scheda salvata in {file.FullName}");
                }

                return 0;
            }
            catch (PreIstruttoriaException exception)
            {
                await Console.Error.WriteLineAsync(exception.Message);

                return exception is ClausoleNonDisponibiliException ? SinistriCli.ExitError : ExitPolizza;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                await Console.Error.WriteLineAsync($"ask non completato: {exception.Message}");

                return SinistriCli.ExitError;
            }
        });

        return command;
    }

    /// <summary>Passi con i tempi (<c>✓ embedding 0,4 s</c>); la generazione mostra "…" finché il modello risponde.</summary>
    private sealed class AvanzamentoConsole : IProgress<AvanzamentoPreIstruttoria>
    {
        public void Report(AvanzamentoPreIstruttoria value)
        {
            if (value.Durata is { } durata)
            {
                Console.WriteLine($"\r✓ {value.Passo} {Formati.Secondi(durata)}          ");
            }
            else if (value.Passo == "generazione scheda")
            {
                Console.Write($"… {value.Passo} (modello locale, può richiedere decine di secondi)");
            }
        }
    }
}
