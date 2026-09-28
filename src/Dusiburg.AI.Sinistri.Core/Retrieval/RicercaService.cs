using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Core.Retrieval;

/// <summary>
/// Ricerca di clausole e storico (fase-5.md §5): vettore della denuncia come "query" del profilo di embedding, poi repository.
/// Usato da CLI, API e scheda (Fase 6). Le statistiche arrivano dall'SQL, mai dal modello.
/// </summary>
public sealed class RicercaService(
    IEmbeddingService embeddingService,
    IClausolaRepository clausole,
    ISinistroRepository sinistri,
    IOptions<RetrievalOptions> retrieval)
{
    public async Task<RisultatoRicercaClausole> CercaClausoleAsync(string testo, Prodotto prodotto, int? top, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        float[] vettore = await embeddingService.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(testo, causa: null), cancellationToken);
        TimeSpan embedding = cronometro.Elapsed;

        cronometro.Restart();
        IReadOnlyList<ClausolaTrovata> trovate = await clausole.CercaPertinentiAsync(
            vettore, prodotto, top ?? retrieval.Value.TopClausole, retrieval.Value.DistanzaMaxClausolaIntegrativa,
            retrieval.Value.ArticoloFranchigiaBase, cancellationToken);

        return new RisultatoRicercaClausole(trovate, new TempiRicerca(embedding, cronometro.Elapsed));
    }

    /// <summary>Se la causa è tra i filtri la si antepone al testo, come nel vettore dei sinistri indicizzati.</summary>
    public async Task<RisultatoRicercaStorico> CercaStoricoAsync(string testo, FiltriStorico filtri, int? top, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        float[] vettore = await embeddingService.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(testo, filtri.Causa), cancellationToken);
        TimeSpan embedding = cronometro.Elapsed;

        cronometro.Restart();
        IReadOnlyList<SinistroSimile> simili = await sinistri.CercaSimiliAsync(vettore, filtri, top ?? retrieval.Value.TopSinistri, cancellationToken);
        StatisticheSimili statistiche = await sinistri.CalcolaStatisticheAsync([.. simili.Select(s => s.Id)], cancellationToken);

        return new RisultatoRicercaStorico(simili, statistiche, new TempiRicerca(embedding, cronometro.Elapsed));
    }
}
