using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Core.Telemetry;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Core.Antifrode;

/// <summary>
/// Controllo deterministico dei quasi-duplicati (fase-7.md): sulla nuova denuncia (passo 6 della pre-istruttoria) e sui sinistri di un
/// periodo (<c>fraud-scan</c>). Nessun LLM: l'esito non entra nel prompt. Due vettori e due soglie (fase-7.md §6 bis): tra sinistri
/// storici il vettore completo e <c>SOGLIA_DUPLICATO_COSINE</c>; tra denuncia e sinistri la sola descrizione e
/// <c>SOGLIA_DUPLICATO_DENUNCIA</c>.
/// </summary>
public sealed class AntifrodeService(
    IEmbeddingService embeddingService,
    IAntifrodeRepository repository,
    SinistriOptions opzioni,
    IOptions<RetrievalOptions> retrieval)
{
    /// <summary>Segnalazioni massime sulla nuova denuncia.</summary>
    public const int MaxSegnalazioni = 20;

    private static readonly ActivitySource Source = new(SinistriTelemetry.Sources.Antifrode);

    public double SogliaFraudScan => opzioni.SogliaDuplicatoCosine;

    /// <summary>
    /// La denuncia si vettorizza come documento e come il vettore antifrode dei sinistri (solo il racconto, senza la causa indicata):
    /// il confronto è simmetrico. Contro il vettore completo l'esito di perizia allontanerebbe il sinistro dalla sua riformulazione
    /// (fase-7.md §2 bis).
    /// </summary>
    public async Task<IReadOnlyList<SegnalazioneDuplicato>> ControllaDenunciaAsync(RichiestaPreIstruttoria richiesta, CancellationToken cancellationToken)
    {
        IReadOnlyList<float[]> vettori = await embeddingService.EmbedDocumentsAsync(
            [EmbeddingTextBuilder.Antifrode(richiesta.Denuncia)], cancellationToken);

        return await repository.CercaDuplicatiDenunciaAsync(
            vettori[0], richiesta.NumeroPolizza, richiesta.RiparatoreId, retrieval.Value.MesiControlloDuplicati, opzioni.SogliaDuplicatoDenuncia,
            MaxSegnalazioni, cancellationToken);
    }

    /// <summary>
    /// Coppie sotto <paramref name="soglia"/> tra i sinistri degli ultimi <paramref name="mesi"/>. Con le coppie attese, una sola query
    /// alla soglia più alta tra quelle valutate e poi i filtri in memoria (fase-7.md §3).
    /// </summary>
    public async Task<RisultatoFraudScan> ScansionaAsync(
        int mesi, double? soglia, IReadOnlyList<CoppiaDuplicati>? attese, CancellationToken cancellationToken)
    {
        using Activity? activity = Source.StartActivity("fraud-scan");
        var cronometro = Stopwatch.StartNew();
        double sogliaScelta = soglia ?? SogliaFraudScan;
        IReadOnlyList<double> soglie = ValutazioneDuplicati.Soglie(sogliaScelta);
        double sogliaQuery = attese is null ? sogliaScelta : soglie[^1];

        IReadOnlyList<CoppiaSospetta> trovate = await repository.CercaCoppieAsync(mesi, sogliaQuery, cancellationToken);
        ValutazioneFraudScan? valutazione = attese is null
            ? null
            : Valuta(trovate, attese, soglie, await repository.DistanzeCoppieAsync(attese, mesi, cancellationToken));

        List<CoppiaSospetta> coppie = [.. trovate.Where(c => c.Distanza < sogliaScelta)];
        activity?.SetTag("sinistri.mesi", mesi);
        activity?.SetTag("sinistri.soglia", sogliaScelta);
        activity?.SetTag("sinistri.coppie", coppie.Count);

        return new RisultatoFraudScan(mesi, sogliaScelta, coppie, valutazione, cronometro.Elapsed);
    }

    internal static ValutazioneFraudScan Valuta(
        IReadOnlyList<CoppiaSospetta> trovate, IReadOnlyList<CoppiaDuplicati> attese, IReadOnlyList<double> soglie,
        IReadOnlyList<DistanzaCoppiaAttesa> distanze)
    {
        List<CoppiaSospetta> conLegame = [.. trovate.Where(c => c.ConLegame)];
        List<RigaValutazione> righeTutte = [.. soglie.Select(s => ValutazioneDuplicati.Calcola(trovate, attese, s))];
        List<RigaValutazione> righeConLegame = [.. soglie.Select(s => ValutazioneDuplicati.Calcola(conLegame, attese, s))];

        return new ValutazioneFraudScan(
            righeTutte, ValutazioneDuplicati.SuggerisciSoglia(righeTutte),
            righeConLegame, ValutazioneDuplicati.SuggerisciSoglia(righeConLegame),
            distanze,
            trovate.FirstOrDefault(c => !ValutazioneDuplicati.Attesa(c, attese)),
            conLegame.FirstOrDefault(c => !ValutazioneDuplicati.Attesa(c, attese)));
    }
}
