using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Benchmark;
using Dusiburg.AI.Sinistri.Data.Retrieval;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// Indice DiskANN e <c>VECTOR_SEARCH</c> sui dati di <see cref="DatiRetrieval"/> (fase-10.md §10.1). Documenta anche i limiti verificati
/// su SQL Server 2025 RTM-CU3: filtri applicati dopo la ricerca approssimata e tabella in sola lettura con l'indice. L'indice resta
/// su <c>Sinistri_Test</c>, che le altre classi ricreano prima di usarlo.
/// </summary>
[Category("Integration")]
[NonParallelizable]
public class RicercaApprossimataRepositoryTests
{
    private static readonly FiltriStorico Base = new(Prodotto.CasaFabbricati, AnniStorico: 5);

    private readonly RicercaApprossimataRepository _approssimata = new(new SqlConnectionFactory(TestDatabase.ConnectionString));
    private readonly SinistroRepository _esatta = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task PreparaAsync()
    {
        await DatiRetrieval.RicreaAsync(CancellationToken.None);

        // Tutte le righe indicizzate devono avere il vettore: S-7 ne è senza.
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await connection.ExecuteAsync("DELETE FROM dbo.Sinistro WHERE Embedding IS NULL");
    }

    [Test, Order(1)]
    public async Task CreaIndice_UnaVolta()
    {
        //SUT
        TimeSpan? prima = await _approssimata.CreaIndiceAsync(CancellationToken);
        TimeSpan? seconda = await _approssimata.CreaIndiceAsync(CancellationToken);

        Assert.That(prima, Is.Not.Null);
        Assert.That(seconda, Is.Null, "indice già presente");
    }

    [Test, Order(2)]
    public async Task CercaSimili_ConTopNAmpio_UgualeAllaRicercaEsatta()
    {
        //SUT
        IReadOnlyList<SinistroSimile> esatti = await _esatta.CercaSimiliAsync(DatiRetrieval.Denuncia, Base, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> approssimati = await _approssimata.CercaSimiliAsync(DatiRetrieval.Denuncia, Base, 10, 100, CancellationToken);

        Assert.That(approssimati.Select(s => s.Numero), Is.EqualTo(esatti.Select(s => s.Numero)));
        Assert.That(approssimati.Select(s => s.Distanza), Is.EqualTo(esatti.Select(s => s.Distanza)).Within(1e-4));
    }

    [Test, Order(3)]
    public async Task CercaSimili_TopNStretto_FiltriDopoLaRicerca()
    {
        //SUT
        // I 2 vicini più prossimi sono S-1 [1,0,0,0] e S-4 (aperto) o S-6 (RC): i filtri li scartano quasi tutti.
        IReadOnlyList<SinistroSimile> stretto = await _approssimata.CercaSimiliAsync(DatiRetrieval.Denuncia, Base, 10, 2, CancellationToken);

        Assert.That(stretto, Has.Count.LessThan(3), "meno risultati di quanti la ricerca esatta ne trova (3)");
        Assert.That(stretto.Select(s => s.Stato), Has.None.EqualTo(StatoSinistro.Aperto));
    }

    [Test, Order(4)]
    public async Task ConIndice_TabellaInSolaLettura()
    {
        //SETUP
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        //SUT
        Assert.That(async () => await connection.ExecuteAsync("UPDATE dbo.Sinistro SET Provincia = 'BO' WHERE Numero = 'S-1'"),
            Throws.InstanceOf<SqlException>().With.Property(nameof(SqlException.Number)).EqualTo(42231));
    }
}
