using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Retrieval;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Ricerca A su LocalDB con le distanze note di <see cref="DatiRetrieval"/>.</summary>
[Category("Integration")]
[NonParallelizable]
public class ClausolaRepositoryTests
{
    private readonly ClausolaRepository _repository = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task PreparaAsync() => await DatiRetrieval.RicreaAsync(CancellationToken.None);

    [Test]
    public async Task CercaPertinenti_OrdinaPerDistanza()
    {
        //SUT
        IReadOnlyList<ClausolaTrovata> trovate = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 2, 0.0, null, CancellationToken);

        Assert.That(trovate.Select(c => c.Articolo), Is.EqualTo(new[] { "Art. 2.1", "Art. 2.2" }));
        Assert.That(trovate.Select(c => c.Rank), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(trovate[0].Distanza, Is.EqualTo(0).Within(1e-6));
        Assert.That(trovate[1].Distanza, Is.EqualTo(1 - (1 / Math.Sqrt(1.01))).Within(1e-5));
        Assert.That(trovate[0].Tipo, Is.EqualTo(TipoClausola.Garanzia));
    }

    [Test]
    public async Task CercaPertinenti_AggiungeEsclusioneEntroSoglia()
    {
        //SUT
        IReadOnlyList<ClausolaTrovata> trovate = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 0.45, null, CancellationToken);
        IReadOnlyList<ClausolaTrovata> giaTraLePrime = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 4, 0.45, null, CancellationToken);

        Assert.That(trovate.Select(c => (c.Articolo, c.Integrativa)),
            Is.EqualTo(new[] { ("Art. 2.1", false), ("Art. 2.2", false), ("Art. 1.1", false), ("Art. 3.1", true) }));
        Assert.That(trovate[3].Rank, Is.EqualTo(4));
        Assert.That(giaTraLePrime.Select(c => (c.Articolo, c.Integrativa)).Last(), Is.EqualTo(("Art. 3.1", false)));
        Assert.That(giaTraLePrime, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task CercaPertinenti_NonAggiungeEsclusioneOltreSoglia()
    {
        //SUT
        IReadOnlyList<ClausolaTrovata> sogliaBassa = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 0.2, null, CancellationToken);
        IReadOnlyList<ClausolaTrovata> sogliaAlta = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 1.5, null, CancellationToken);

        Assert.That(sogliaBassa.Select(c => c.Articolo), Is.EqualTo(new[] { "Art. 2.1", "Art. 2.2", "Art. 1.1" }));
        Assert.That(sogliaAlta.Where(c => c.Integrativa).Select(c => c.Articolo), Is.EqualTo(new[] { "Art. 3.1", "Art. 4.1" }));
        Assert.That(sogliaAlta.Select(c => c.Articolo), Has.None.EqualTo("Art. 4.2").And.None.EqualTo("Art. 3.2"));
    }

    [Test]
    public async Task CercaPertinenti_FranchigiaDiBaseSoloSeNessunaFranchigia()
    {
        //SUT
        IReadOnlyList<ClausolaTrovata> senzaFranchigie = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 0.2, "Art. 4.1", CancellationToken);
        IReadOnlyList<ClausolaTrovata> franchigiaGiaScelta = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 1.5, "Art. 4.1", CancellationToken);
        IReadOnlyList<ClausolaTrovata> baseSenzaEmbedding = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 0.2, "Art. 4.2", CancellationToken);
        IReadOnlyList<ClausolaTrovata> baseNonFranchigia = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 3, 0.2, "Art. 3.2", CancellationToken);

        Assert.That(senzaFranchigie.Select(c => (c.Articolo, c.Rank, c.Integrativa)),
            Is.EqualTo(new[] { ("Art. 2.1", 1, false), ("Art. 2.2", 2, false), ("Art. 1.1", 3, false), ("Art. 4.1", 6, true) }));
        Assert.That(franchigiaGiaScelta.Count(c => c.Articolo == "Art. 4.1"), Is.EqualTo(1));
        Assert.That(baseSenzaEmbedding, Has.Count.EqualTo(3));
        Assert.That(baseNonFranchigia, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task CercaPertinenti_FiltraPerProdotto()
    {
        //SUT
        IReadOnlyList<ClausolaTrovata> rc = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.RcProfTecnici, 10, 2.0, null, CancellationToken);
        IReadOnlyList<ClausolaTrovata> casa = await _repository.CercaPertinentiAsync(DatiRetrieval.Denuncia, Prodotto.CasaFabbricati, 10, 2.0, null, CancellationToken);

        Assert.That(rc.Select(c => c.Titolo), Is.EqualTo(new[] { "Garanzia RC identica alla denuncia" }));
        Assert.That(casa, Has.Count.EqualTo(6));
        Assert.That(casa.Select(c => c.Titolo), Has.None.Contains("RC"));
    }
}
