using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Retrieval;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Ricerca B e statistiche su LocalDB con le distanze note di <see cref="DatiRetrieval"/>.</summary>
[Category("Integration")]
[NonParallelizable]
public class SinistroRepositoryTests
{
    private static readonly FiltriStorico Base = new(Prodotto.CasaFabbricati, AnniStorico: 5);

    private readonly SinistroRepository _repository = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task PreparaAsync() => await DatiRetrieval.RicreaAsync(CancellationToken.None);

    [Test]
    public async Task CercaSimili_EscludeAperti()
    {
        //SUT
        IReadOnlyList<SinistroSimile> simili = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base, 10, CancellationToken);

        Assert.That(simili.Select(s => s.Numero), Is.EqualTo(new[] { "S-1", "S-2", "S-3" }));
        Assert.That(simili.Select(s => s.Stato), Has.None.EqualTo(StatoSinistro.Aperto));
        Assert.That(simili[0], Has.Property(nameof(SinistroSimile.Provincia)).EqualTo("MI")
            .And.Property(nameof(SinistroSimile.Causa)).EqualTo(CausaSinistro.FenomenoElettrico)
            .And.Property(nameof(SinistroSimile.ImportoLiquidato)).EqualTo(6000m)
            .And.Property(nameof(SinistroSimile.DataEvento)).EqualTo(DateOnly.FromDateTime(DateTime.Today).AddYears(-1)));
    }

    [Test]
    public async Task CercaSimili_FiltriOpzionali()
    {
        //SETUP
        int[] s1 = await DatiRetrieval.IdAsync("S-1");

        //SUT
        IReadOnlyList<SinistroSimile> provincia = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { Provincia = "MI" }, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> importo = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { ImportoMin = 5000m }, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> causa = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { Causa = CausaSinistro.AcquaCondotta }, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> anni = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { AnniStorico = 10 }, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> escludi = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { EscludiSinistriIds = s1 }, 10, CancellationToken);
        IReadOnlyList<SinistroSimile> top = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base, 2, CancellationToken);
        IReadOnlyList<SinistroSimile> rc = await _repository.CercaSimiliAsync(DatiRetrieval.Denuncia, Base with { Prodotto = Prodotto.RcProfTecnici }, 10, CancellationToken);

        Assert.That(provincia.Select(s => s.Numero), Is.EqualTo(new[] { "S-1", "S-3" }));
        Assert.That(importo.Select(s => s.Numero), Is.EqualTo(new[] { "S-1" }));
        Assert.That(causa.Select(s => s.Numero), Is.EqualTo(new[] { "S-3" }));
        Assert.That(anni.Select(s => s.Numero), Is.EqualTo(new[] { "S-1", "S-2", "S-5", "S-3" }));
        Assert.That(escludi.Select(s => s.Numero), Is.EqualTo(new[] { "S-2", "S-3" }));
        Assert.That(top.Select(s => s.Numero), Is.EqualTo(new[] { "S-1", "S-2" }));
        Assert.That(rc.Select(s => s.Numero), Is.EqualTo(new[] { "S-6" }));
    }

    [Test]
    public async Task CalcolaStatistiche_Mediana()
    {
        //SETUP
        int[] conRespinto = await DatiRetrieval.IdAsync("S-1", "S-2", "S-3");
        int[] treChiusi = await DatiRetrieval.IdAsync("S-1", "S-2", "S-5");

        //SUT
        StatisticheSimili pari = await _repository.CalcolaStatisticheAsync(conRespinto, CancellationToken);
        StatisticheSimili dispari = await _repository.CalcolaStatisticheAsync(treChiusi, CancellationToken);

        Assert.That(pari, Is.EqualTo(new StatisticheSimili(3, 1, 33.3m, 3000m, 4500m, 6000m)));
        Assert.That(dispari, Is.EqualTo(new StatisticheSimili(3, 0, 0m, 3000m, 6000m, 8000m)));
    }

    [Test]
    public async Task CalcolaStatistiche_ListaVuota()
    {
        //SUT
        StatisticheSimili vuota = await _repository.CalcolaStatisticheAsync([], CancellationToken);

        Assert.That(vuota, Is.EqualTo(new StatisticheSimili(0, 0, 0m, null, null, null)));
    }
}
