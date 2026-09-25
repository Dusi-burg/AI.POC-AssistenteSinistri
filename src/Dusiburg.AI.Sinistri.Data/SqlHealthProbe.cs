using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data;

/// <summary>
/// Controlli 1–4 e 9 di <c>health</c>: connessione, versione ≥ 17 (SQL Server 2025), tipo <c>VECTOR</c>, database applicativo,
/// dimensione delle colonne <c>VECTOR</c> e modello degli embedding salvati rispetto alla configurazione.
/// </summary>
public sealed class SqlHealthProbe(SqlConnectionFactory connectionFactory, SinistriOptions options) : IHealthProbe
{
    private const string ConnessioneNome = "Connessione SQL";
    private const string VersioneNome = "Versione SQL Server";
    private const string VectorNome = "Supporto VECTOR";
    private const string DatabaseNome = "Database applicativo";
    private const string SchemaNome = "Schema ed embedding nel DB";

    private const int MinimumMajorVersion = 17;

    public async Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken)
    {
        if (!connectionFactory.IsConfigured)
        {
            ProbeResult missing = await ProbeResult.MeasureAsync(1, ConnessioneNome, () => throw MissingConnectionString());

            return [missing, .. SkipAfterConnection("connection string mancante")];
        }

        await using SqlConnection connection = connectionFactory.CreateMasterConnection();

        ProbeResult connessione = await ProbeResult.MeasureAsync(1, ConnessioneNome, async () =>
        {
            await connection.OpenAsync(cancellationToken);

            return (ProbeStatus.Ok, $"{connectionFactory.Target.Server}");
        });

        if (connessione.Stato == ProbeStatus.Error)
        {
            return [connessione, .. SkipAfterConnection("connessione non riuscita")];
        }

        ProbeResult database = await ProbeResult.MeasureAsync(4, DatabaseNome, () => CheckDatabaseAsync(connection, cancellationToken));

        return
        [
            connessione,
            await ProbeResult.MeasureAsync(2, VersioneNome, () => CheckVersionAsync(connection, cancellationToken)),
            await ProbeResult.MeasureAsync(3, VectorNome, () => CheckVectorAsync(connection, cancellationToken)),
            database,
            database.Stato == ProbeStatus.Ok
                ? await ProbeResult.MeasureAsync(9, SchemaNome, () => CheckSchemaAsync(cancellationToken))
                : new ProbeResult(9, SchemaNome, database.Stato, "non eseguito: database non disponibile", TimeSpan.Zero)
        ];
    }

    private static async Task<(ProbeStatus, string)> CheckVersionAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        (int major, string version, string edition) = await connection.QuerySingleAsync<(int, string, string)>(new CommandDefinition(
            """
            SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int),
                   CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
                   CAST(SERVERPROPERTY('Edition') AS nvarchar(128))
            """,
            cancellationToken: cancellationToken));

        string dettaglio = $"{version} ({edition})";

        return major >= MinimumMajorVersion
            ? (ProbeStatus.Ok, dettaglio)
            : (ProbeStatus.Error, $"{dettaglio}: serve SQL Server 2025 (versione {MinimumMajorVersion}.x) per il tipo VECTOR");
    }

    private static async Task<(ProbeStatus, string)> CheckVectorAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        double distance = await connection.QuerySingleAsync<double>(new CommandDefinition(
            "SELECT VECTOR_DISTANCE('cosine', CAST('[1,0]' AS VECTOR(2)), CAST('[0,1]' AS VECTOR(2)))",
            cancellationToken: cancellationToken));

        return (ProbeStatus.Ok, $"VECTOR_DISTANCE disponibile (coseno di vettori ortogonali = {distance:0.###})");
    }

    private async Task<(ProbeStatus, string)> CheckDatabaseAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string database = connectionFactory.Target.Database;

        if (string.IsNullOrWhiteSpace(database))
        {
            return (ProbeStatus.Error, "la connection string non indica il database (Database=Sinistri)");
        }

        int? databaseId = await connection.QuerySingleAsync<int?>(new CommandDefinition(
            "SELECT DB_ID(@database)", new { database }, cancellationToken: cancellationToken));

        return databaseId is null
            ? (ProbeStatus.Warning, $"il database '{database}' non esiste ancora: crearlo con dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit")
            : (ProbeStatus.Ok, $"'{database}' presente");
    }

    /// <summary>
    /// Colonne <c>VECTOR</c> della stessa dimensione di <c>EMBEDDING_DIMENSIONS</c>. Se gli embedding sono già stati calcolati
    /// (<c>EmbeddingInfo</c>), stesso modello e stesso runtime della configurazione: altrimenti la ricerca confronterebbe vettori incompatibili.
    /// </summary>
    private async Task<(ProbeStatus, string)> CheckSchemaAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        IReadOnlyDictionary<string, int> columns = await DatabaseInitializer.ReadVectorDimensionsAsync(connection, cancellationToken);
        string[] wrong = [.. columns.Where(c => c.Value != options.EmbeddingDimensions).Select(c => $"{c.Key} VECTOR({c.Value})")];

        if (columns.Count == 0 || wrong.Length > 0)
        {
            string found = columns.Count == 0 ? "nessuna colonna VECTOR" : string.Join(", ", wrong);

            return (ProbeStatus.Error,
                $"il database ha {found} ma {SinistriOptions.Keys.EmbeddingDimensions}={options.EmbeddingDimensions}: rieseguire DbInit");
        }

        EmbeddingInfo? info = await DatabaseInitializer.ReadEmbeddingInfoAsync(connection, cancellationToken);
        string colonne = $"{columns.Count} colonne VECTOR({options.EmbeddingDimensions})";

        if (info is null)
        {
            return (ProbeStatus.Ok, $"{colonne}; embedding non ancora calcolati: eseguire embed");
        }

        EmbeddingProvider provider = EnumMetadata.FromConfiguration(options.EmbeddingProvider);

        if (info.Modello != options.EmbeddingModel || info.Provider != provider || info.Dimensioni != options.EmbeddingDimensions)
        {
            return (ProbeStatus.Error,
                $"embedding calcolati con {info.Modello} ({info.Provider}, {info.Dimensioni}) ma la configurazione usa " +
                $"{options.EmbeddingModel} ({provider}, {options.EmbeddingDimensions}): rieseguire embed");
        }

        // Conteggio su due tabelle da poche centinaia di righe: scansione accettabile, nessun indice su Embedding.
        int mancanti = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT (SELECT COUNT(*) FROM dbo.Clausola WHERE Embedding IS NULL) + (SELECT COUNT(*) FROM dbo.Sinistro WHERE Embedding IS NULL)",
            cancellationToken: cancellationToken));
        string dettaglio = $"{colonne}; embedding di {info.Modello} ({info.Provider}) del {info.AggiornatoIl:yyyy-MM-dd HH:mm}";

        return mancanti == 0
            ? (ProbeStatus.Ok, dettaglio)
            : (ProbeStatus.Warning, $"{dettaglio}; {mancanti} righe senza embedding: eseguire embed --solo-mancanti");
    }

    private static IEnumerable<ProbeResult> SkipAfterConnection(string motivo) =>
    [
        ProbeResult.Skipped(2, VersioneNome, motivo),
        ProbeResult.Skipped(3, VectorNome, motivo),
        ProbeResult.Skipped(4, DatabaseNome, motivo),
        ProbeResult.Skipped(9, SchemaNome, motivo)
    ];

    private static InvalidOperationException MissingConnectionString() =>
        new($"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName} non configurata: impostarla negli user-secrets dell'AppHost.");
}
