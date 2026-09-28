using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Ricreazione del database, lookup dagli enum e controllo 9 di health, su LocalDB.</summary>
[Category("Integration")]
[NonParallelizable]
public class DatabaseInitializerTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Recreate_CreaTabelleELookup()
    {
        //SETUP
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        //SUT
        string[] tables = [.. await connection.QueryAsync<string>("SELECT name FROM sys.tables ORDER BY name")];

        Assert.That(tables, Is.EquivalentTo(new[]
        {
            "CausaSinistro", "Clausola", "Contraente", "EmbeddingInfo", "EmbeddingProvider", "Polizza",
            "Prodotto", "Riparatore", "Sinistro", "StatoSinistro", "TipoClausola"
        }));
        Assert.That(await LookupAsync(connection, "CausaSinistro"), Is.EquivalentTo(Expected<CausaSinistro>()));
        Assert.That(await LookupAsync(connection, "StatoSinistro"), Is.EquivalentTo(Expected<StatoSinistro>()));
        Assert.That(await LookupAsync(connection, "Prodotto"), Does.Contain((1, "CasaFabbricati", "Casa e fabbricati")));
    }

    [Test]
    public async Task Recreate_DueVolte_RipartedaZero()
    {
        //SETUP
        await using (SqlConnection connection = await TestDatabase.OpenAsync())
        {
            await connection.ExecuteAsync("INSERT INTO dbo.Riparatore (RagioneSociale) VALUES (N'Idraulica di prova S.r.l.')");
        }

        //SUT
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);

        await using SqlConnection reopened = await TestDatabase.OpenAsync();
        Assert.That(await reopened.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Riparatore"), Is.Zero);
        Assert.That(await reopened.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.TipoClausola"), Is.EqualTo(Enum.GetValues<TipoClausola>().Length));
    }

    [Test]
    public async Task ReadVectorDimensions_ColonneDiClausolaESinistro()
    {
        //SETUP
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        //SUT
        IReadOnlyDictionary<string, int> dimensions = await DatabaseInitializer.ReadVectorDimensionsAsync(connection, CancellationToken);

        Assert.That(dimensions, Is.EquivalentTo(new Dictionary<string, int>
        {
            ["Clausola.Embedding"] = 4, ["Sinistro.Embedding"] = 4, ["Sinistro.EmbeddingAntifrode"] = 4
        }));
        Assert.That(await DatabaseInitializer.ReadEmbeddingInfoAsync(connection, CancellationToken), Is.Null);
    }

    [Test]
    public async Task HealthControllo9_DimensioneDiversaDallaConfigurazione_Errore()
    {
        //SETUP
        SqlHealthProbe coerente = Probe(new() { ["EMBEDDING_DIMENSIONS"] = "4" });
        SqlHealthProbe incoerente = Probe(new() { ["EMBEDDING_DIMENSIONS"] = "768" });

        //SUT
        ProbeResult ok = (await coerente.RunAsync(CancellationToken)).Single(r => r.Numero == 9);
        ProbeResult ko = (await incoerente.RunAsync(CancellationToken)).Single(r => r.Numero == 9);

        Assert.That(ok.Stato, Is.EqualTo(ProbeStatus.Ok), ok.Dettaglio);
        Assert.That(ko.Stato, Is.EqualTo(ProbeStatus.Error));
        Assert.That(ko.Dettaglio, Does.Contain("VECTOR(4)").And.Contain("EMBEDDING_DIMENSIONS=768"));
    }

    [Test]
    public async Task HealthControllo9_EmbeddingDiUnAltroModello_Errore()
    {
        //SETUP
        await using (SqlConnection connection = await TestDatabase.OpenAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO dbo.EmbeddingInfo (Id, Modello, EmbeddingProviderId, Dimensioni, AggiornatoIl) VALUES (1, 'bge-m3', 1, 4, SYSDATETIME())");
        }

        SqlHealthProbe probe = Probe(new() { ["EMBEDDING_DIMENSIONS"] = "4", ["EMBEDDING_MODEL"] = "embeddinggemma" });

        try
        {
            //SUT
            ProbeResult result = (await probe.RunAsync(CancellationToken)).Single(r => r.Numero == 9);

            Assert.That(result.Stato, Is.EqualTo(ProbeStatus.Error));
            Assert.That(result.Dettaglio, Does.Contain("bge-m3").And.Contain("embeddinggemma").And.Contain("rieseguire embed"));
        }
        finally
        {
            await using SqlConnection connection = await TestDatabase.OpenAsync();
            await connection.ExecuteAsync("DELETE FROM dbo.EmbeddingInfo");
        }
    }

    private static SqlHealthProbe Probe(Dictionary<string, string?> settings) => new(
        new SqlConnectionFactory(TestDatabase.ConnectionString),
        SinistriOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));

    private static async Task<IReadOnlyList<(int Id, string Name, string Descrizione)>> LookupAsync(SqlConnection connection, string table) =>
        [.. await connection.QueryAsync<(int, string, string)>($"SELECT Id, Name, Descrizione FROM dbo.{table}")];

    private static IEnumerable<(int Id, string Name, string Descrizione)> Expected<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => (Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture), v.ToString(), v.Descrizione()));
}
