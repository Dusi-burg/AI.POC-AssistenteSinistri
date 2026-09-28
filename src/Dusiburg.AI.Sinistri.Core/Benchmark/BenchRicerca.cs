using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Core.Benchmark;

// Banco di prova della ricerca nello storico (fase-10.md §10.1): scansione esatta contro indice DiskANN con VECTOR_SEARCH.

/// <summary>Interrogazione del banco: testo e filtri; <see cref="Selettiva"/> se i filtri scartano la gran parte dei vicini.</summary>
public sealed record InterrogazioneBench(string Id, string Testo, FiltriStorico Filtri, bool Selettiva);

/// <summary>Metodo misurato: la ricerca esatta, oppure <c>VECTOR_SEARCH</c> con <c>TOP_N = FattoreTopN × k</c>.</summary>
public sealed record MetodoBench(string Nome, int? FattoreTopN)
{
    public static readonly MetodoBench Esatta = new("esatta", null);

    public static MetodoBench Indice(int fattore) => new($"indice, TOP_N = {fattore}×k", fattore);
}

/// <summary>
/// Una misura: durata della query SQL e confronto con il risultato esatto della stessa interrogazione, per Id (<see cref="Overlap"/>) e
/// per distanza (<see cref="OverlapDistanza"/>, che conta come giusti i pari merito).
/// </summary>
public sealed record MisuraBench(
    MetodoBench Metodo, InterrogazioneBench Interrogazione, TimeSpan Durata, int Risultati, double Overlap, double OverlapDistanza);

public sealed record RigaBench(
    MetodoBench Metodo, bool Selettive, int Interrogazioni, double MediaMs, double P50Ms, double P95Ms,
    double OverlapMedio, double OverlapMinimo, double OverlapDistanzaMedio, double OverlapDistanzaMinimo, double RisultatiMedi, int SottoK);

/// <summary>Dati del database del banco, per l'intestazione del report.</summary>
public sealed record DatasetBench(string Database, int Sinistri, int TestiDistinti, TimeSpan? DurataEmbedding, TimeSpan? DurataIndice);

public sealed record RisultatoBench(
    DateTimeOffset Eseguito, DatasetBench Dataset, string EmbeddingModel, int K, int Ripetizioni, IReadOnlyList<RigaBench> Righe,
    TimeSpan Durata);

public interface IRicercaApprossimataRepository
{
    /// <summary>
    /// Come <see cref="ISinistroRepository.CercaSimiliAsync"/>, ma sui <paramref name="topN"/> vicini restituiti dall'indice vettoriale:
    /// i filtri SQL si applicano dopo, quindi i risultati possono essere meno di <paramref name="top"/>.
    /// </summary>
    Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(float[] vettore, FiltriStorico filtri, int top, int topN, CancellationToken cancellationToken);
}

/// <summary>Metriche del banco, pure.</summary>
public static class BenchMetrics
{
    /// <summary>|esatti ∩ approssimati| / |esatti| sugli Id (1 se la ricerca esatta non trova nulla).</summary>
    public static double Overlap(IReadOnlyCollection<int> esatti, IReadOnlyCollection<int> approssimati) =>
        esatti.Count == 0 ? 1 : (double)esatti.Count(approssimati.Contains) / esatti.Count;

    /// <summary>Tolleranza sulle distanze: i vettori dello stesso testo sono identici, le differenze sono solo di arrotondamento.</summary>
    public const double TolleranzaDistanza = 1e-6;

    /// <summary>
    /// Quota dei risultati esatti "coperti" dall'indice considerando i pari merito: un risultato approssimato conta se la sua distanza non
    /// supera quella del k-esimo esatto. Molti sinistri hanno lo stesso testo e quindi lo stesso vettore: a parità di distanza la ricerca
    /// esatta sceglie per Id, l'indice può restituirne un altro altrettanto vicino.
    /// </summary>
    public static double OverlapDistanza(IReadOnlyList<double> distanzeEsatte, IReadOnlyCollection<double> distanzeApprossimate)
    {
        if (distanzeEsatte.Count == 0)
        {
            return 1;
        }

        double limite = distanzeEsatte.Max() + TolleranzaDistanza;

        return Math.Min(distanzeEsatte.Count, distanzeApprossimate.Count(d => d <= limite)) / (double)distanzeEsatte.Count;
    }

    /// <summary>Percentile con il metodo "nearest rank" (p95 di 20 valori = il 19°).</summary>
    public static double Percentile(IEnumerable<double> valori, double percentile)
    {
        double[] ordinati = [.. valori.Order()];

        if (ordinati.Length == 0)
        {
            return 0;
        }

        int rank = (int)Math.Ceiling(percentile / 100 * ordinati.Length);

        return ordinati[Math.Clamp(rank, 1, ordinati.Length) - 1];
    }

    public static IReadOnlyList<RigaBench> Riepiloga(IEnumerable<MisuraBench> misure, int k) =>
    [
        .. misure
            .GroupBy(m => (m.Metodo, m.Interrogazione.Selettiva))
            .OrderBy(g => g.Key.Selettiva).ThenBy(g => g.Key.Metodo.FattoreTopN ?? 0)
            .Select(g =>
            {
                double[] ms = [.. g.Select(m => m.Durata.TotalMilliseconds)];
                // Overlap e risultati non dipendono dalla ripetizione: si contano una volta per interrogazione.
                MisuraBench[] perInterrogazione = [.. g.GroupBy(m => m.Interrogazione.Id).Select(i => i.First())];

                return new RigaBench(
                    g.Key.Metodo, g.Key.Selettiva, perInterrogazione.Length, ms.Average(), Percentile(ms, 50), Percentile(ms, 95),
                    perInterrogazione.Average(m => m.Overlap), perInterrogazione.Min(m => m.Overlap),
                    perInterrogazione.Average(m => m.OverlapDistanza), perInterrogazione.Min(m => m.OverlapDistanza),
                    perInterrogazione.Average(m => m.Risultati), perInterrogazione.Count(m => m.Risultati < k));
            })
    ];
}

/// <summary>
/// Esegue il banco: un embedding per interrogazione (fuori dalle misure), poi per ogni ripetizione la ricerca esatta e le varianti con
/// l'indice, alternate per non favorire nessuna con la cache. Si misura solo la query SQL.
/// </summary>
public sealed class BenchRicercaService(
    IEmbeddingService embeddingService, ISinistroRepository esatta, IRicercaApprossimataRepository approssimata)
{
    public async Task<IReadOnlyList<MisuraBench>> MisuraAsync(
        IReadOnlyList<InterrogazioneBench> interrogazioni, IReadOnlyList<int> fattori, int k, int ripetizioni,
        IProgress<string>? avanzamento, CancellationToken cancellationToken)
    {
        List<MisuraBench> misure = [];

        foreach (InterrogazioneBench interrogazione in interrogazioni)
        {
            avanzamento?.Report(interrogazione.Id);
            float[] vettore = await embeddingService.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(interrogazione.Testo, interrogazione.Filtri.Causa), cancellationToken);

            // Riscaldamento e risultato di riferimento.
            IReadOnlyList<SinistroSimile> riferimento = await esatta.CercaSimiliAsync(vettore, interrogazione.Filtri, k, cancellationToken);
            HashSet<int> idEsatti = [.. riferimento.Select(s => s.Id)];
            double[] distanzeEsatte = [.. riferimento.Select(s => s.Distanza)];

            foreach (int fattore in fattori)
            {
                await approssimata.CercaSimiliAsync(vettore, interrogazione.Filtri, k, fattore * k, cancellationToken);
            }

            for (int r = 0; r < ripetizioni; r++)
            {
                var cronometro = Stopwatch.StartNew();
                IReadOnlyList<SinistroSimile> esatti = await esatta.CercaSimiliAsync(vettore, interrogazione.Filtri, k, cancellationToken);
                misure.Add(new MisuraBench(MetodoBench.Esatta, interrogazione, cronometro.Elapsed, esatti.Count, 1, 1));

                foreach (int fattore in fattori)
                {
                    cronometro.Restart();
                    IReadOnlyList<SinistroSimile> trovati = await approssimata.CercaSimiliAsync(vettore, interrogazione.Filtri, k, fattore * k, cancellationToken);
                    misure.Add(new MisuraBench(MetodoBench.Indice(fattore), interrogazione, cronometro.Elapsed, trovati.Count,
                        BenchMetrics.Overlap(idEsatti, [.. trovati.Select(s => s.Id)]),
                        BenchMetrics.OverlapDistanza(distanzeEsatte, [.. trovati.Select(s => s.Distanza)])));
                }
            }
        }

        return misure;
    }
}
