using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Ingestion.Embedding;

/// <summary>Richiesta di <c>embed</c>: tutte le righe o solo quelle senza vettore, su entrambe le tabelle o su una sola.</summary>
public sealed record EmbedRichiesta(bool SoloMancanti, IReadOnlyList<TabellaEmbedding> Tabelle);

public sealed record EmbedAvanzamento(TabellaEmbedding Tabella, int Fatti, int Totale);

public sealed record EmbedEsitoTabella(TabellaEmbedding Tabella, int Vettori, TimeSpan Durata);

public sealed record EmbedEsito(IReadOnlyList<EmbedEsitoTabella> Tabelle, TimeSpan Durata, int RigheSenzaEmbedding);

/// <summary>
/// Calcolo e salvataggio dei vettori (fase-4.md §3–4): pagine da 200 righe per chiave, batch da <c>Retrieval:EmbeddingBatchSize</c>,
/// ogni batch scritto in una transazione. Se un batch fallisce dopo i retry il comando si ferma e le righe già scritte restano,
/// così <c>--solo-mancanti</c> riprende da lì. A fine lavoro aggiorna <c>EmbeddingInfo</c>.
/// </summary>
public sealed class EmbeddingPipeline(
    IEmbeddingService embeddingService,
    EmbeddingRepository repository,
    SinistriOptions options,
    IOptions<RetrievalOptions> retrieval,
    TimeProvider timeProvider)
{
    public const int DimensionePagina = 200;

    public async Task<EmbedEsito> RunAsync(EmbedRichiesta richiesta, IProgress<EmbedAvanzamento>? avanzamento, CancellationToken cancellationToken)
    {
        await VerificaAsync(richiesta, cancellationToken);

        var totale = Stopwatch.StartNew();
        List<EmbedEsitoTabella> tabelle = [];

        foreach (TabellaEmbedding tabella in richiesta.Tabelle)
        {
            tabelle.Add(await ElaboraAsync(tabella, richiesta.SoloMancanti, avanzamento, cancellationToken));
        }

        if (tabelle.Sum(t => t.Vettori) > 0)
        {
            await repository.WriteEmbeddingInfoAsync(options.EmbeddingModel, EnumMetadata.FromConfiguration(options.EmbeddingProvider),
                options.EmbeddingDimensions, timeProvider.GetLocalNow().DateTime, cancellationToken);
        }

        int senzaEmbedding = 0;

        foreach (TabellaEmbedding tabella in Enum.GetValues<TabellaEmbedding>())
        {
            senzaEmbedding += await repository.CountAsync(tabella, soloMancanti: true, cancellationToken);
        }

        return new EmbedEsito(tabelle, totale.Elapsed, senzaEmbedding);
    }

    /// <summary>
    /// Dimensione delle colonne uguale alla configurazione; con vettori già calcolati da un altro modello o runtime, niente lavoro
    /// parziale (<c>--solo-mancanti</c> o una sola tabella): vettori vecchi e nuovi non sarebbero confrontabili.
    /// </summary>
    private async Task VerificaAsync(EmbedRichiesta richiesta, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, int> colonne = await repository.ReadVectorDimensionsAsync(cancellationToken);
        string[] diverse = [.. colonne.Where(c => c.Value != options.EmbeddingDimensions).Select(c => $"{c.Key} VECTOR({c.Value})")];

        if (colonne.Count == 0 || diverse.Length > 0)
        {
            throw new InvalidOperationException(
                $"Il database ha {(colonne.Count == 0 ? "nessuna colonna VECTOR" : string.Join(", ", diverse))} ma " +
                $"{SinistriOptions.Keys.EmbeddingDimensions}={options.EmbeddingDimensions}: rieseguire DbInit.");
        }

        EmbeddingInfo? info = await repository.ReadEmbeddingInfoAsync(cancellationToken);
        EmbeddingProvider provider = EnumMetadata.FromConfiguration(options.EmbeddingProvider);
        bool parziale = richiesta.SoloMancanti || richiesta.Tabelle.Count < Enum.GetValues<TabellaEmbedding>().Length;

        if (parziale && info is not null && (info.Modello != options.EmbeddingModel || info.Provider != provider))
        {
            throw new InvalidOperationException(
                $"Gli embedding salvati sono di {info.Modello} ({info.Provider}) ma la configurazione usa {options.EmbeddingModel} ({provider}): " +
                "i vettori non sarebbero confrontabili. Rieseguire embed completo, senza --solo-mancanti e senza --solo.");
        }
    }

    private async Task<EmbedEsitoTabella> ElaboraAsync(
        TabellaEmbedding tabella, bool soloMancanti, IProgress<EmbedAvanzamento>? avanzamento, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        int totale = await repository.CountAsync(tabella, soloMancanti, cancellationToken);
        int fatti = 0;
        int dopoId = 0;

        avanzamento?.Report(new EmbedAvanzamento(tabella, 0, totale));

        while (true)
        {
            IReadOnlyList<TestoDaVettorizzare> pagina = await repository.ReadPageAsync(tabella, soloMancanti, dopoId, DimensionePagina, cancellationToken);

            if (pagina.Count == 0)
            {
                break;
            }

            foreach (TestoDaVettorizzare[] batch in pagina.Chunk(retrieval.Value.EmbeddingBatchSize))
            {
                IReadOnlyList<float[]> vettori = await embeddingService.EmbedDocumentsAsync([.. batch.Select(r => r.Testo)], cancellationToken);
                await repository.UpdateAsync(tabella, [.. batch.Select((r, i) => (r.Id, vettori[i]))], cancellationToken);

                fatti += batch.Length;
                avanzamento?.Report(new EmbedAvanzamento(tabella, fatti, totale));
            }

            dopoId = pagina[^1].Id;
        }

        return new EmbedEsitoTabella(tabella, fatti, cronometro.Elapsed);
    }
}
