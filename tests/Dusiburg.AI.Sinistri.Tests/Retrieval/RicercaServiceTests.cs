using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Tests.Retrieval;

/// <summary>RicercaService con embedding e repository finti: testo della query, parametri di default, statistiche sui simili restituiti.</summary>
public class RicercaServiceTests
{
    [Test]
    public async Task CercaStorico_CausaNelTestoEStatisticheSuiSimili()
    {
        //SETUP
        var embedding = new EmbeddingFinto();
        var sinistri = new SinistriFinti();
        var service = new RicercaService(embedding, new ClausoleFinte(), sinistri, Microsoft.Extensions.Options.Options.Create(new RetrievalOptions { TopSinistri = 7 }));
        var filtri = new FiltriStorico(Prodotto.CasaFabbricati, 5, Causa: CausaSinistro.FenomenoElettrico);

        //SUT
        RisultatoRicercaStorico risultato = await service.CercaStoricoAsync("sovratensione sul quadro", filtri, top: null, CancellationToken.None);

        Assert.That(embedding.Query, Is.EqualTo(new[] { "Causa: Fenomeno elettrico. sovratensione sul quadro" }));
        Assert.That(sinistri.TopRichiesto, Is.EqualTo(7));
        Assert.That(sinistri.IdStatistiche, Is.EqualTo(new[] { 11, 12 }));
        Assert.That(risultato.Simili, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CercaClausole_TestoSenzaCausaEParametriDiDefault()
    {
        //SETUP
        var embedding = new EmbeddingFinto();
        var clausole = new ClausoleFinte();
        var service = new RicercaService(embedding, clausole, new SinistriFinti(), Microsoft.Extensions.Options.Options.Create(new RetrievalOptions { TopClausole = 4, DistanzaMaxClausolaIntegrativa = 0.3 }));

        //SUT
        await service.CercaClausoleAsync("tubo rotto", Prodotto.CasaFabbricati, top: null, CancellationToken.None);
        await service.CercaClausoleAsync("tubo rotto", Prodotto.CasaFabbricati, top: 2, CancellationToken.None);

        Assert.That(embedding.Query, Is.EqualTo(new[] { "tubo rotto", "tubo rotto" }));
        Assert.That(clausole.Richieste, Is.EqualTo(new[] { (4, 0.3), (2, 0.3) }));
    }

    private sealed class EmbeddingFinto : IEmbeddingService
    {
        public List<string> Query { get; } = [];

        public EmbeddingProfile Profile { get; } = EmbeddingProfile.ForModel("embeddinggemma");

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken)
        {
            Query.Add(text);

            return Task.FromResult(new[] { 1f, 0f, 0f, 0f });
        }

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ClausoleFinte : IClausolaRepository
    {
        public List<(int Top, double DistanzaMax)> Richieste { get; } = [];

        public Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
            float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, string? articoloFranchigiaBase, CancellationToken cancellationToken)
        {
            Richieste.Add((top, distanzaMaxIntegrativa));

            return Task.FromResult<IReadOnlyList<ClausolaTrovata>>([]);
        }
    }

    private sealed class SinistriFinti : ISinistroRepository
    {
        public int TopRichiesto { get; private set; }

        public IReadOnlyCollection<int> IdStatistiche { get; private set; } = [];

        public Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken)
        {
            TopRichiesto = top;

            return Task.FromResult<IReadOnlyList<SinistroSimile>>(
            [
                Simile(11), Simile(12)
            ]);
        }

        public Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken)
        {
            IdStatistiche = sinistriIds;

            return Task.FromResult(new StatisticheSimili(sinistriIds.Count, 0, 0, null, null, null));
        }

        private static SinistroSimile Simile(int id) => new(id, $"SIN-{id}", new DateOnly(2026, 1, 1), "MI", CausaSinistro.FenomenoElettrico,
            "-", null, StatoSinistro.Chiuso, 1000m, 0.1);
    }
}
