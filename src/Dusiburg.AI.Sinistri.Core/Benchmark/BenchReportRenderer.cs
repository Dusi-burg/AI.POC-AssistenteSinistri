using System.Globalization;
using System.Text;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Core.Benchmark;

/// <summary>Report <c>eval/bench_&lt;data&gt;_&lt;n&gt;.md</c> del banco di prova della ricerca (fase-10.md §10.1).</summary>
public static class BenchReportRenderer
{
    public static string NomeFile(RisultatoBench bench) => $"bench_{bench.Eseguito:yyyy-MM-dd_HHmm}_{bench.Dataset.Sinistri}.md";

    public static string Render(RisultatoBench bench)
    {
        var md = new StringBuilder();
        DatasetBench dataset = bench.Dataset;

        Riga(md, $"# Ricerca nello storico: scansione esatta e indice DiskANN — {bench.Eseguito.ToString("yyyy-MM-dd HH:mm", Formati.Italiano)}");
        Riga(md);
        Riga(md, "| Parametro | Valore |");
        Riga(md, "|---|---|");
        Riga(md, $"| Database | {dataset.Database} |");
        Riga(md, $"| Sinistri | {dataset.Sinistri.ToString("N0", Formati.Italiano)} ({dataset.TestiDistinti.ToString("N0", Formati.Italiano)} testi distinti vettorizzati) |");
        Riga(md, $"| Modello di embedding | `{bench.EmbeddingModel}` |");
        Riga(md, $"| Embedding in questa esecuzione | {(dataset.DurataEmbedding is { } e ? e.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "già presenti")} |");
        Riga(md, $"| Creazione dell'indice | {(dataset.DurataIndice is { } i ? i.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "già presente")} |");
        Riga(md, $"| k, ripetizioni | {bench.K}, {bench.Ripetizioni} per interrogazione |");
        Riga(md, $"| Durata del banco | {Formati.Secondi(bench.Durata)} |");
        Riga(md);

        foreach (bool selettive in new[] { false, true })
        {
            RigaBench[] righe = [.. bench.Righe.Where(r => r.Selettive == selettive)];

            if (righe.Length == 0)
            {
                continue;
            }

            Riga(md, selettive
                ? $"## Filtri selettivi ({righe[0].Interrogazioni} interrogazioni: provincia, causa, importo)"
                : $"## Filtri di base ({righe[0].Interrogazioni} interrogazioni: prodotto, stato, ultimi 5 anni)");
            Riga(md);
            Riga(md, $"| Metodo | Media ms | p50 ms | p95 ms | Overlap@{bench.K} per Id (medio / minimo) | Overlap@{bench.K} per distanza (medio / minimo) | Risultati medi | Interrogazioni con meno di {bench.K} |");
            Riga(md, "|---|---|---|---|---|---|---|---|");

            foreach (RigaBench r in righe)
            {
                Riga(md, $"| {r.Metodo.Nome} | {Ms(r.MediaMs)} | {Ms(r.P50Ms)} | {Ms(r.P95Ms)} | {Decimale(r.OverlapMedio)} / {Decimale(r.OverlapMinimo)} | " +
                    $"{Decimale(r.OverlapDistanzaMedio)} / {Decimale(r.OverlapDistanzaMinimo)} | " +
                    $"{r.RisultatiMedi.ToString("0.0", Formati.Italiano)} | {r.SottoK} |");
            }

            Riga(md);
        }

        Riga(md, "Overlap per Id: quota dei risultati della ricerca esatta presenti anche in quella con l'indice. Overlap per distanza: " +
            "come sopra, ma un risultato dell'indice conta se non è più lontano del k-esimo esatto (molti sinistri hanno lo stesso testo e " +
            "quindi lo stesso vettore: a parità di distanza la ricerca esatta sceglie per Id, l'indice può restituirne un altro). Con l'indice i filtri SQL si " +
            "applicano ai `TOP_N` vicini restituiti da `VECTOR_SEARCH`: se i filtri ne scartano molti, i risultati sono meno di k.");

        return md.ToString();
    }

    private static string Ms(double valore) => valore.ToString("0.0", Formati.Italiano);

    private static string Decimale(double valore) => valore.ToString("0.00", Formati.Italiano);

    private static void Riga(StringBuilder md, string testo = "") => md.Append(testo).Append('\n');
}
