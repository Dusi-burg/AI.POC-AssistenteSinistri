using Dapper;
using Dusiburg.AI.Sinistri.Core.Health;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data;

/// <summary>Controlli 1–4 di <c>health</c>: connessione, versione ≥ 17 (SQL Server 2025), tipo <c>VECTOR</c>, database applicativo.</summary>
public sealed class SqlHealthProbe(SqlConnectionFactory connectionFactory) : IHealthProbe
{
    private const string ConnessioneNome = "Connessione SQL";
    private const string VersioneNome = "Versione SQL Server";
    private const string VectorNome = "Supporto VECTOR";
    private const string DatabaseNome = "Database applicativo";

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

        return
        [
            connessione,
            await ProbeResult.MeasureAsync(2, VersioneNome, () => CheckVersionAsync(connection, cancellationToken)),
            await ProbeResult.MeasureAsync(3, VectorNome, () => CheckVectorAsync(connection, cancellationToken)),
            await ProbeResult.MeasureAsync(4, DatabaseNome, () => CheckDatabaseAsync(connection, cancellationToken))
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
            ? (ProbeStatus.Warning, $"il database '{database}' non esiste ancora: lo crea tools/Dusiburg.AI.Sinistri.DbInit (Fase 2)")
            : (ProbeStatus.Ok, $"'{database}' presente");
    }

    private static IEnumerable<ProbeResult> SkipAfterConnection(string motivo) =>
    [
        ProbeResult.Skipped(2, VersioneNome, motivo),
        ProbeResult.Skipped(3, VectorNome, motivo),
        ProbeResult.Skipped(4, DatabaseNome, motivo)
    ];

    private static InvalidOperationException MissingConnectionString() =>
        new($"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName} non configurata: impostarla negli user-secrets dell'AppHost.");
}
