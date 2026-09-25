using System.CommandLine;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Ingestion.Embedding;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary><c>embed [--solo-mancanti] [--solo clausole|sinistri]</c>: vettori di clausole e sinistri (fase-4.md §4).</summary>
internal static class EmbedCommand
{
    public static Command Create(IServiceProvider services)
    {
        var soloMancanti = new Option<bool>("--solo-mancanti") { Description = "Elabora solo le righe con Embedding NULL." };
        var solo = new Option<TabellaEmbedding?>("--solo") { Description = "Elabora una sola tabella: clausole o sinistri." };

        var command = new Command("embed", "Calcola e salva gli embedding di clausole e sinistri.") { soloMancanti, solo };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            TabellaEmbedding? tabella = parseResult.GetValue(solo);
            var richiesta = new EmbedRichiesta(parseResult.GetValue(soloMancanti), tabella is { } una ? [una] : Enum.GetValues<TabellaEmbedding>());

            try
            {
                EmbedEsito esito = await services.GetRequiredService<EmbeddingPipeline>()
                    .RunAsync(richiesta, new AvanzamentoSincrono(Stampa), cancellationToken);

                Write(esito, services.GetRequiredService<SinistriOptions>(), Console.Out);

                return 0;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or NotSupportedException)
            {
                Console.WriteLine();
                await Console.Error.WriteLineAsync($"embed non completato: {exception.Message}");

                return SinistriCli.ExitError;
            }
        });

        return command;
    }

    internal static void Write(EmbedEsito esito, SinistriOptions options, TextWriter output)
    {
        output.WriteLine();

        int vettori = esito.Tabelle.Sum(t => t.Vettori);

        if (vettori == 0)
        {
            output.WriteLine("0 elementi da elaborare.");
        }
        else
        {
            foreach (EmbedEsitoTabella tabella in esito.Tabelle)
            {
                output.WriteLine($"{$"{tabella.Tabella}:",-10}{tabella.Vettori} vettori in {tabella.Durata:hh\\:mm\\:ss}");
            }

            output.WriteLine($"{"Totale:",-10}{vettori} vettori in {esito.Durata:hh\\:mm\\:ss} (modello {options.EmbeddingModel}, {options.EmbeddingDimensions} dim)");
        }

        output.WriteLine($"Righe con Embedding NULL: {esito.RigheSenzaEmbedding}");
    }

    private static void Stampa(EmbedAvanzamento avanzamento)
    {
        string fine = avanzamento.Fatti == avanzamento.Totale ? Environment.NewLine : "";
        Console.Write($"\r[{avanzamento.Tabella.ToString().ToLowerInvariant()}] {avanzamento.Fatti}/{avanzamento.Totale}{fine}");
    }

    /// <summary>A differenza di <see cref="Progress{T}"/>, stampa nel thread della pipeline: l'avanzamento non si mescola al riepilogo.</summary>
    private sealed class AvanzamentoSincrono(Action<EmbedAvanzamento> stampa) : IProgress<EmbedAvanzamento>
    {
        public void Report(EmbedAvanzamento value) => stampa(value);
    }
}
