using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Benchmark;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Benchmark;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Cli.Benchmark;

/// <summary>
/// Database del banco di prova (fase-10.md §10.1), <c>Sinistri_bench_&lt;n&gt;</c>: con l'indice DiskANN la tabella dei sinistri diventa di
/// sola lettura, quindi l'indice non può stare nel database della demo. Preparazione ripetibile e riprendibile:
/// <list type="number">
/// <item>dati sintetici con <paramref name="sinistri"/> sinistri (stesso seed della demo); DB ricreato solo se il conteggio non torna;</item>
/// <item>solo <c>Sinistro.Embedding</c> (quello del pilastro B), un vettore per testo distinto: ~26.000 testi per 50.000 sinistri;</item>
/// <item>indice DiskANN, creato dopo i vettori.</item>
/// </list>
/// </summary>
internal sealed class DatabaseBench(int sinistri)
{
    public string Database { get; } = $"Sinistri_bench_{sinistri}";

    public async Task<DatasetBench> PreparaAsync(IServiceProvider servizi, TextWriter output, CancellationToken cancellationToken)
    {
        SinistriOptions opzioni = servizi.GetRequiredService<SinistriOptions>();
        SqlConnectionFactory connessioni = servizi.GetRequiredService<SqlConnectionFactory>();
        EmbeddingRepository embedding = servizi.GetRequiredService<EmbeddingRepository>();

        int seed = servizi.GetRequiredService<IOptions<SeedOptions>>().Value.RandomSeed;
        DatiSintetici dati = SyntheticDataGenerator.Genera(seed, DateOnly.FromDateTime(DateTime.Today), sinistri);
        int testiDistinti = dati.Sinistri.Select(s => EmbeddingTextBuilder.Sinistro(s.Causa, s.Descrizione, s.EsitoPerizia)).Distinct().Count();

        if (!await UtilizzabileAsync(embedding, dati.Sinistri.Count, opzioni, cancellationToken))
        {
            output.WriteLine($"Database {Database}: lo ricreo con {dati.Sinistri.Count} sinistri...");

            await using (SqlConnection target = connessioni.CreateConnection())
            {
                await DatabaseInitializer.RecreateAsync(target.ConnectionString, opzioni.EmbeddingDimensions, cancellationToken);
            }

            await using SqlConnection connection = connessioni.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await SeedRepository.InsertAsync(connection, dati, cancellationToken);
        }

        TimeSpan? durataEmbedding = await VettorizzaAsync(servizi, embedding, opzioni, output, cancellationToken);

        output.WriteLine("Indice DiskANN...");
        TimeSpan? durataIndice = await servizi.GetRequiredService<RicercaApprossimataRepository>().CreaIndiceAsync(cancellationToken);
        output.WriteLine(durataIndice is { } d ? $"Indice creato in {d:mm\\:ss}." : "Indice già presente.");

        return new DatasetBench(Database, dati.Sinistri.Count, testiDistinti, durataEmbedding, durataIndice);
    }

    /// <summary>Stesso numero di sinistri e vettori (completi o parziali) dello stesso modello: si riprende da lì.</summary>
    private static async Task<bool> UtilizzabileAsync(EmbeddingRepository embedding, int attesi, SinistriOptions opzioni, CancellationToken cancellationToken)
    {
        try
        {
            if (await embedding.CountAsync(TabellaEmbedding.Sinistri, soloMancanti: false, cancellationToken) != attesi)
            {
                return false;
            }

            EmbeddingInfo? info = await embedding.ReadEmbeddingInfoAsync(cancellationToken);

            return info is null || (info.Modello == opzioni.EmbeddingModel && info.Dimensioni == opzioni.EmbeddingDimensions);
        }
        catch (SqlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Solo i sinistri senza vettore, un embedding per testo distinto (i template ripetono molti testi); se interrotto riprende da lì.
    /// Null se non c'era niente da fare.
    /// </summary>
    private static async Task<TimeSpan?> VettorizzaAsync(
        IServiceProvider servizi, EmbeddingRepository embedding, SinistriOptions opzioni, TextWriter output, CancellationToken cancellationToken)
    {
        List<TestoDaVettorizzare> mancanti = [];

        for (int dopoId = 0; ;)
        {
            IReadOnlyList<TestoDaVettorizzare> pagina = await embedding.ReadPageAsync(TabellaEmbedding.Sinistri, soloMancanti: true, dopoId, 5_000, cancellationToken);

            if (pagina.Count == 0)
            {
                break;
            }

            mancanti.AddRange(pagina);
            dopoId = pagina[^1].Id;
        }

        if (mancanti.Count == 0)
        {
            return null;
        }

        IEmbeddingService servizio = servizi.GetRequiredService<IEmbeddingService>();
        int batch = servizi.GetRequiredService<IOptions<RetrievalOptions>>().Value.EmbeddingBatchSize;
        IGrouping<string, TestoDaVettorizzare>[] perTesto = [.. mancanti.GroupBy(r => r.Testo)];
        var cronometro = Stopwatch.StartNew();
        int fatti = 0;

        output.WriteLine($"Vettorizzo {perTesto.Length} testi distinti per {mancanti.Count} sinistri con {opzioni.EmbeddingModel}...");

        foreach (IGrouping<string, TestoDaVettorizzare>[] gruppo in perTesto.Chunk(batch))
        {
            IReadOnlyList<float[]> vettori = await servizio.EmbedDocumentsAsync([.. gruppo.Select(g => g.Key)], cancellationToken);
            await embedding.UpdateAsync(TabellaEmbedding.Sinistri,
                [.. gruppo.SelectMany((g, i) => g.Select(r => (r.Id, vettori[i])))], cancellationToken);

            fatti += gruppo.Length;
            double restanti = cronometro.Elapsed.TotalSeconds / fatti * (perTesto.Length - fatti);
            output.Write($"\r{fatti}/{perTesto.Length} testi · {cronometro.Elapsed:mm\\:ss} · restano ~{TimeSpan.FromSeconds(restanti):mm\\:ss}   ");
        }

        output.WriteLine();
        await embedding.WriteEmbeddingInfoAsync(opzioni.EmbeddingModel, EnumMetadata.FromConfiguration(opzioni.EmbeddingProvider),
            opzioni.EmbeddingDimensions, DateTime.Now, cancellationToken);

        return cronometro.Elapsed;
    }
}
