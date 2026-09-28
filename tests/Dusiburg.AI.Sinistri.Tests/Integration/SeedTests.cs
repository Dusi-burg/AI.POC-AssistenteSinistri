using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.PreIstruttoria;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Seed di clausole e dati sintetici su LocalDB (DB <c>Sinistri_Test</c>): il passo 6 di DbInit senza il file JSON.</summary>
[Category("Integration")]
[NonParallelizable]
public class SeedTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Seed_InserisceClausoleEDatiSintetici()
    {
        //SETUP
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        DatiSintetici dati = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, DateOnly.FromDateTime(DateTime.Today));
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        //SUT
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);
        await SeedRepository.InsertAsync(connection, dati, CancellationToken);

        ConteggioSeed conteggio = await SeedRepository.ReadConteggioAsync(connection, CancellationToken);
        Assert.That(conteggio, Is.EqualTo(new ConteggioSeed(dati.Contraenti.Count, dati.Riparatori.Count, dati.Polizze.Count, 60, dati.Sinistri.Count)));
        Assert.That(await connection.QueryAsync<(string, int)>(
                "SELECT p.Name, COUNT(*) FROM dbo.Clausola AS c JOIN dbo.Prodotto AS p ON p.Id = c.ProdottoId GROUP BY p.Name"),
            Is.EquivalentTo(new[] { ("CasaFabbricati", 35), ("RcProfTecnici", 25) }));

        string[] numeri = [.. dati.Coppie.SelectMany(c => new[] { c.Originale, c.Duplicato })];
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Sinistro WHERE Numero IN @numeri", new { numeri }),
            Is.EqualTo(numeri.Length));

        // Query di copertura dello scenario 5 (fase-3.md, criteri di completamento).
        Assert.That(await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM dbo.Sinistro
            WHERE CausaSinistroId = 3 AND Provincia = 'MI' AND StatoSinistroId = 2 AND ImportoLiquidato > 5000
            """), Is.GreaterThanOrEqualTo(8));
        Assert.That((await SeedRepository.ReadCausaStatoAsync(connection, CancellationToken)).Sum(r => r.Sinistri), Is.EqualTo(dati.Sinistri.Count));
    }

    [Test]
    public async Task PolizzaRepository_PolizzaDemoPerNumero()
    {
        //SETUP
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        DateOnly oggi = DateOnly.FromDateTime(DateTime.Today);
        await using (SqlConnection connection = await TestDatabase.OpenAsync())
        {
            await SeedRepository.InsertAsync(connection, SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, oggi), CancellationToken);
        }
        var repository = new PolizzaRepository(new SqlConnectionFactory(TestDatabase.ConnectionString));

        //SUT
        DatiPolizza? demo = await repository.GetByNumeroAsync(" CF-DEMO-000001 ", CancellationToken);
        DatiPolizza? inesistente = await repository.GetByNumeroAsync("CF-XXXX-000000", CancellationToken);

        Assert.That(demo, Is.EqualTo(new DatiPolizza("CF-DEMO-000001", Prodotto.CasaFabbricati, "Mario Bianchi", "MI",
            oggi.AddYears(-1), oggi.AddYears(2), 300_000m, 250m)));
        Assert.That(inesistente, Is.Null);
    }

    [Test]
    public async Task Clausole_Rieseguite_RipristinanoIlTestoEAzzeranoLEmbedding()
    {
        //SETUP
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);
        await connection.ExecuteAsync("UPDATE dbo.Clausola SET Embedding = CAST('[1,0,0,0]' AS VECTOR(4))");
        await connection.ExecuteAsync("UPDATE dbo.Clausola SET Testo = N'testo modificato a mano' WHERE Articolo = N'Art. 2.4' AND ProdottoId = 1");

        //SUT
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Clausola"), Is.EqualTo(60));
        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Articolo FROM dbo.Clausola WHERE Embedding IS NULL"), Is.EqualTo("Art. 2.4"));
        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Testo FROM dbo.Clausola WHERE Articolo = N'Art. 2.4' AND ProdottoId = 1"),
            Does.StartWith("La Società indennizza i danni materiali e diretti causati al fabbricato e al contenuto da fuoriuscita di acqua condotta"));
    }
}
