using System.Text.RegularExpressions;
using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion;
using Dusiburg.AI.Sinistri.Ingestion.Embedding;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dusiburg.AI.Sinistri.Cli.Valutazione;

/// <summary>
/// <c>eval --embedding-model</c> (fase-9.md §3): un database per modello, <c>Sinistri_emb_&lt;provider&gt;_&lt;modello&gt;</c>, sullo stesso
/// server. Le manopole si sovrascrivono in memoria e i servizi si ricreano con quella configurazione: il file di configurazione non
/// cambia. Il DB contiene le sole clausole (l'eval misura il pilastro A) e si ricrea solo se manca o se i vettori non sono del modello.
/// </summary>
internal sealed class ModelloAlternativo(string modello, string provider, int dimensioni)
{
    public const string PrefissoDatabase = "Sinistri_emb_";

    public string Database { get; } = $"{PrefissoDatabase}{Normalizza(provider)}_{Normalizza(modello)}";

    /// <summary>Servizi con le manopole del modello alternativo e la connection string del suo database.</summary>
    public ServiceProvider CreaServizi(IConfiguration configurazione, ILoggerFactory loggerFactory)
    {
        var connessione = new SqlConnectionStringBuilder(configurazione.GetConnectionString(SqlConnectionFactory.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName} non configurata."))
        {
            InitialCatalog = Database
        };

        IConfiguration sovrascritta = new ConfigurationBuilder()
            .AddConfiguration(configurazione)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName}"] = connessione.ConnectionString,
                [SinistriOptions.Keys.EmbeddingModel] = modello,
                [SinistriOptions.Keys.EmbeddingProvider] = provider,
                [SinistriOptions.Keys.EmbeddingDimensions] = dimensioni.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddSinistriCore(sovrascritta);
        services.AddSinistriData(sovrascritta);
        services.AddSinistriAi(sovrascritta);
        services.AddSinistriIngestion(sovrascritta);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Prima una chiamata di prova (modello installato, dimensione uguale a <c>--embedding-dimensions</c>), poi il DB: riusato se ha già
    /// tutte le clausole vettorizzate con questo modello, altrimenti ricreato come fa DbInit (schema, lookup, clausole) e vettorizzato.
    /// </summary>
    public async Task PreparaAsync(IServiceProvider servizi, TextWriter output, CancellationToken cancellationToken)
    {
        await servizi.GetRequiredService<IEmbeddingService>().EmbedQueryAsync("prova", cancellationToken);

        SqlConnectionFactory connessioni = servizi.GetRequiredService<SqlConnectionFactory>();
        EmbeddingRepository embedding = servizi.GetRequiredService<EmbeddingRepository>();

        if (await ProntoAsync(embedding, cancellationToken))
        {
            output.WriteLine($"Database {Database}: clausole già vettorizzate con {modello}.");

            return;
        }

        output.WriteLine($"Database {Database}: lo ricreo con VECTOR({dimensioni}) e vettorizzo le clausole con {modello}...");

        // Stessa routine di DbInit: il nome del DB è derivato, quindi il database Sinistri non si tocca mai.
        await using (SqlConnection target = connessioni.CreateConnection())
        {
            await DatabaseInitializer.RecreateAsync(target.ConnectionString, dimensioni, cancellationToken);
        }

        await using (SqlConnection connection = connessioni.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await SeedRepository.SeedClausoleAsync(connection, cancellationToken);
        }

        EmbedEsito esito = await servizi.GetRequiredService<EmbeddingPipeline>()
            .RunAsync(new EmbedRichiesta(SoloMancanti: false, [TabellaEmbedding.Clausole]), null, cancellationToken);

        output.WriteLine($"{esito.Tabelle.Sum(t => t.Vettori)} clausole vettorizzate in {esito.Durata:mm\\:ss}.");
    }

    private async Task<bool> ProntoAsync(EmbeddingRepository embedding, CancellationToken cancellationToken)
    {
        try
        {
            EmbeddingInfo? info = await embedding.ReadEmbeddingInfoAsync(cancellationToken);

            return info is not null
                && info.Modello == modello && info.Dimensioni == dimensioni
                && info.Provider == Core.Dominio.EnumMetadata.FromConfiguration(provider)
                && await embedding.CountAsync(TabellaEmbedding.Clausole, soloMancanti: false, cancellationToken) > 0
                && await embedding.CountAsync(TabellaEmbedding.Clausole, soloMancanti: true, cancellationToken) == 0;
        }
        catch (SqlException)
        {
            // Database inesistente o schema diverso: si ricrea.
            return false;
        }
    }

    private static string Normalizza(string nome) => Regex.Replace(nome.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
}
