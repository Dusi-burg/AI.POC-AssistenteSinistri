using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Consultazione;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Elenchi dei dati demo (fase-9b.md §2) sul seed completo in <c>Sinistri_Test</c>: i risultati si confrontano con i dati generati.</summary>
[Category("Integration")]
[NonParallelizable]
public class ConsultazioneRepositoryTests
{
    private static DatiSintetici _dati = null!;

    private readonly ConsultazioneRepository _repository = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task PreparaAsync()
    {
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken.None);
        _dati = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, DateOnly.FromDateTime(DateTime.Today));
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken.None);
        await SeedRepository.InsertAsync(connection, _dati, CancellationToken.None);
    }

    [Test]
    public async Task ElencaSinistri_FiltriEPaginazione()
    {
        //SETUP
        var nessuno = new FiltriElencoSinistri();
        var chiusiCasa = new FiltriElencoSinistri(Prodotto.CasaFabbricati, Stato: StatoSinistro.Chiuso);
        SinistroSintetico primo = _dati.Sinistri[0];
        string numeroPolizza = _dati.Polizze.Single(p => p.Id == primo.PolizzaId).Numero;
        HashSet<int> polizzeCasa = [.. _dati.Polizze.Where(p => p.Prodotto == Prodotto.CasaFabbricati).Select(p => p.Id)];

        //SUT
        Pagina<SinistroElenco> pagina1 = await _repository.ElencaSinistriAsync(nessuno, 1, 25, CancellationToken);
        Pagina<SinistroElenco> pagina2 = await _repository.ElencaSinistriAsync(nessuno, 2, 25, CancellationToken);
        Pagina<SinistroElenco> oltre = await _repository.ElencaSinistriAsync(nessuno, 100, 25, CancellationToken);
        Pagina<SinistroElenco> chiusi = await _repository.ElencaSinistriAsync(chiusiCasa, 1, 100, CancellationToken);
        Pagina<SinistroElenco> mi = await _repository.ElencaSinistriAsync(new FiltriElencoSinistri(Provincia: "mi"), 1, 1, CancellationToken);
        Pagina<SinistroElenco> anno = await _repository.ElencaSinistriAsync(new FiltriElencoSinistri(AnnoDenuncia: DateTime.Today.Year), 1, 1, CancellationToken);
        Pagina<SinistroElenco> polizza = await _repository.ElencaSinistriAsync(new FiltriElencoSinistri(NumeroPolizza: numeroPolizza), 1, 100, CancellationToken);
        Pagina<SinistroElenco> testo = await _repository.ElencaSinistriAsync(new FiltriElencoSinistri(Testo: "parquet"), 1, 1, CancellationToken);

        Assert.That(pagina1, Has.Property(nameof(Pagina<SinistroElenco>.Totale)).EqualTo(_dati.Sinistri.Count)
            .And.Property(nameof(Pagina<SinistroElenco>.Pagine)).EqualTo((_dati.Sinistri.Count + 24) / 25));
        Assert.That(pagina1.Righe, Has.Count.EqualTo(25));
        Assert.That(pagina1.Righe.Select(s => s.Id).Intersect(pagina2.Righe.Select(s => s.Id)), Is.Empty, "pagine senza sovrapposizioni");
        Assert.That(pagina1.Righe.Concat(pagina2.Righe).Select(s => s.DataDenuncia), Is.Ordered.Descending);
        Assert.That(oltre, Has.Property(nameof(Pagina<SinistroElenco>.Righe)).Empty.And.Property(nameof(Pagina<SinistroElenco>.Totale)).EqualTo(_dati.Sinistri.Count));

        Assert.That(chiusi.Totale, Is.EqualTo(_dati.Sinistri.Count(s => s.Stato == StatoSinistro.Chiuso && polizzeCasa.Contains(s.PolizzaId))));
        Assert.That(chiusi.Righe.Select(s => (s.Prodotto, s.Stato)), Has.All.EqualTo((Prodotto.CasaFabbricati, StatoSinistro.Chiuso)));
        Assert.That(mi.Totale, Is.EqualTo(_dati.Sinistri.Count(s => s.Provincia == "MI")));
        Assert.That(anno.Totale, Is.EqualTo(_dati.Sinistri.Count(s => s.DataDenuncia.Year == DateTime.Today.Year)));
        Assert.That(polizza.Righe.Select(s => s.NumeroPolizza), Has.All.EqualTo(numeroPolizza));
        Assert.That(polizza.Righe, Has.Count.EqualTo(_dati.Sinistri.Count(s => s.PolizzaId == primo.PolizzaId)));
        Assert.That(testo.Totale, Is.EqualTo(_dati.Sinistri.Count(s => s.Descrizione.Contains("parquet", StringComparison.OrdinalIgnoreCase))).And.GreaterThan(0));
    }

    [Test]
    public async Task ElencaPolizze_ConteggioSinistri()
    {
        //SUT
        Pagina<PolizzaElenco> tutte = await _repository.ElencaPolizzeAsync(null, soloDemo: false, 1, 100, CancellationToken);
        Pagina<PolizzaElenco> demo = await _repository.ElencaPolizzeAsync(null, soloDemo: true, 1, 100, CancellationToken);
        Pagina<PolizzaElenco> cerca = await _repository.ElencaPolizzeAsync("bianchi", soloDemo: false, 1, 100, CancellationToken);

        Assert.That(tutte.Totale, Is.EqualTo(_dati.Polizze.Count));
        Assert.That(tutte.Righe.Take(DemoCatalog.Polizze.Count).Select(p => p.Numero), Is.EquivalentTo(DemoCatalog.Polizze.Select(p => p.Numero)),
            "le polizze demo per prime");
        Assert.That(demo.Righe.Select(p => p.Demo), Has.All.True);
        Assert.That(demo.Righe, Has.Count.EqualTo(DemoCatalog.Polizze.Count));
        Assert.That(cerca.Righe.Select(p => p.Contraente), Has.All.Contains("Bianchi"));

        foreach (PolizzaElenco polizza in tutte.Righe)
        {
            int attesi = _dati.Sinistri.Count(s => s.PolizzaId == _dati.Polizze.Single(p => p.Numero == polizza.Numero).Id);
            Assert.That(polizza.Sinistri, Is.EqualTo(attesi), polizza.Numero);
        }
    }

    [Test]
    public async Task GetClausole_OrdineNumerico()
    {
        //SUT
        IReadOnlyList<ClausolaDettaglio> tutte = await _repository.GetClausoleAsync(null, null, null, CancellationToken);
        IReadOnlyList<ClausolaDettaglio> casa = await _repository.GetClausoleAsync(Prodotto.CasaFabbricati, null, null, CancellationToken);
        IReadOnlyList<ClausolaDettaglio> esclusioniRc = await _repository.GetClausoleAsync(Prodotto.RcProfTecnici, TipoClausola.Esclusione, null, CancellationToken);
        IReadOnlyList<ClausolaDettaglio> grandine = await _repository.GetClausoleAsync(null, null, "grandine", CancellationToken);

        Assert.That(tutte, Has.Count.EqualTo(60));
        Assert.That(casa, Has.Count.EqualTo(35));
        Assert.That(casa.Select(c => c.Articolo), Does.Contain("Art. 2.10"));
        Assert.That(casa.Select(c => c.Articolo).ToList().IndexOf("Art. 2.10"), Is.EqualTo(casa.Select(c => c.Articolo).ToList().IndexOf("Art. 2.9") + 1),
            "2.10 subito dopo 2.9");
        Assert.That(esclusioniRc.Select(c => (c.Prodotto, c.Tipo)), Has.All.EqualTo((Prodotto.RcProfTecnici, TipoClausola.Esclusione)));
        Assert.That(esclusioniRc, Has.Count.EqualTo(8));
        Assert.That(grandine.Select(c => c.Articolo), Is.SupersetOf(new[] { "Art. 2.3", "Art. 3.6" }));
    }
}
