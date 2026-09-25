using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Schema;

/// <summary>Modello e runtime con cui sono stati calcolati i vettori salvati (tabella <c>EmbeddingInfo</c>).</summary>
public sealed record EmbeddingInfo(string Modello, EmbeddingProvider Provider, int Dimensioni, DateTime AggiornatoIl);

/// <summary>
/// Ricrea da zero il database del POC (D17, come <c>O2CDatabaseInitializer</c>): drop, creazione, schema con la dimensione
/// dei vettori da configurazione, lookup dagli enum. Nessuna migration: l'unica operazione è "ricrea".
/// Lo usano <c>tools/Dusiburg.AI.Sinistri.DbInit</c> e i test di integrazione (DB <c>Sinistri_Test</c>, <c>VECTOR(4)</c>).
/// </summary>
public static class DatabaseInitializer
{
    public const string LocalConnectionString =
        @"Server=(localdb)\localdev;Database=Sinistri;Integrated Security=True;TrustServerCertificate=True";

    public const string CreateDatabaseScript = "001_create_database.sql";
    public const string SchemaScript = "002_schema.sql";

    /// <summary>Tabelle di lookup e rispettivi enum.</summary>
    public static readonly IReadOnlyList<(string Table, Type Enum)> Lookups =
    [
        ("Prodotto", typeof(Prodotto)),
        ("TipoClausola", typeof(TipoClausola)),
        ("CausaSinistro", typeof(CausaSinistro)),
        ("StatoSinistro", typeof(StatoSinistro)),
        ("EmbeddingProvider", typeof(EmbeddingProvider))
    ];

    public static async Task RecreateAsync(string connectionString, int embeddingDimensions, CancellationToken cancellationToken)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        string database = SqlScriptVariables.ValidDatabaseName(target.InitialCatalog);
        string dimensions = SqlScriptVariables.ValidDimensions(embeddingDimensions);

        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await DropIfExistsAsync(connection, database, cancellationToken);
            await SqlScriptRunner.RunAsync(connection, CreateDatabaseScript,
                new Dictionary<string, string> { [SqlScriptVariables.DatabaseName] = database }, cancellationToken);
        }

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await SqlScriptRunner.RunAsync(connection, SchemaScript,
                new Dictionary<string, string> { [SqlScriptVariables.EmbeddingDimensions] = dimensions }, cancellationToken);
            await SyncLookupsAsync(connection, cancellationToken);
        }
    }

    /// <summary>Allinea ogni tabella di lookup al suo enum: Id = valore esplicito, Name = nome del membro, Descrizione = etichetta.</summary>
    public static async Task SyncLookupsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        foreach ((string table, Type enumType) in Lookups)
        {
            foreach (Enum value in Enum.GetValues(enumType).Cast<Enum>())
            {
                // Nome della tabella da un elenco fisso nel codice, mai da input esterno.
                string sql = $"""
                    MERGE dbo.{table} AS target
                    USING (SELECT @Id AS Id, @Name AS Name, @Descrizione AS Descrizione) AS source ON target.Id = source.Id
                    WHEN MATCHED THEN UPDATE SET Name = source.Name, Descrizione = source.Descrizione
                    WHEN NOT MATCHED THEN INSERT (Id, Name, Descrizione) VALUES (source.Id, source.Name, source.Descrizione);
                    """;

                // Colonne VARCHAR: parametri ANSI della stessa lunghezza (Dapper di default manda NVARCHAR(4000)).
                var row = new
                {
                    Id = Convert.ToByte(value, System.Globalization.CultureInfo.InvariantCulture),
                    Name = new DbString { Value = value.ToString(), IsAnsi = true, Length = 50 },
                    Descrizione = new DbString { Value = Description(value), IsAnsi = true, Length = 100 }
                };
                await connection.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: cancellationToken));
            }
        }
    }

    /// <summary>Dimensione delle colonne <c>VECTOR</c>, da <c>sys.columns.vector_dimensions</c> (SQL Server 2025).</summary>
    public static async Task<IReadOnlyDictionary<string, int>> ReadVectorDimensionsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        IEnumerable<(string Column, int Dimensions)> rows = await connection.QueryAsync<(string, int)>(new CommandDefinition(
            """
            SELECT OBJECT_NAME(c.object_id) + N'.' + c.name, c.vector_dimensions
            FROM sys.columns AS c
            WHERE c.vector_dimensions IS NOT NULL AND OBJECTPROPERTY(c.object_id, 'IsUserTable') = 1
            """,
            cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.Column, r => r.Dimensions);
    }

    /// <summary>Lettura per PK (tabella a riga singola): null finché la Fase 4 non ha calcolato gli embedding.</summary>
    public static async Task<EmbeddingInfo?> ReadEmbeddingInfoAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<EmbeddingInfo>(new CommandDefinition(
            "SELECT Modello, EmbeddingProviderId AS Provider, Dimensioni, AggiornatoIl FROM dbo.EmbeddingInfo WHERE Id = 1",
            cancellationToken: cancellationToken));

    private static async Task DropIfExistsAsync(SqlConnection master, string database, CancellationToken cancellationToken)
    {
        // Le connessioni di questo processo rimaste nel pool terrebbero aperto il database.
        SqlConnection.ClearAllPools();

        await master.ExecuteAsync(new CommandDefinition(
            $"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """,
            cancellationToken: cancellationToken));
    }

    private static string Description(Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description
        ?? value.ToString();
}
