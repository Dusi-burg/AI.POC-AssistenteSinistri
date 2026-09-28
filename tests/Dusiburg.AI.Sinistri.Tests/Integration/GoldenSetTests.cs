using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Valutazione;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// <c>data/golden_set.json</c> coerente con le clausole del seed (fase-9.md §4): un articolo scritto male renderebbe la recall falsamente
/// bassa senza alcun errore visibile.
/// </summary>
[Category("Integration")]
[NonParallelizable]
public class GoldenSetTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task TuttiGliArticoliEsistono()
    {
        //SETUP
        GoldenSet golden = await GoldenSetFile.LeggiAsync(
            Path.Combine(DuplicatiAttesiFile.CartellaDati(TestContext.CurrentContext.TestDirectory), GoldenSetFile.NomeFile), CancellationToken);
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);

        //SUT
        HashSet<(Prodotto, string)> clausole = [.. await connection.QueryAsync<(Prodotto, string)>("SELECT ProdottoId, Articolo FROM dbo.Clausola")];
        Dictionary<(Prodotto, string), TipoClausola> tipi = (await connection.QueryAsync<(Prodotto Prodotto, string Articolo, TipoClausola Tipo)>(
            "SELECT ProdottoId, Articolo, TipoClausolaId FROM dbo.Clausola")).ToDictionary(r => (r.Prodotto, r.Articolo), r => r.Tipo);

        string[] inesistenti = [.. golden.Casi.SelectMany(c => c.Attese.Rilevanti.Concat(c.Attese.EsclusioniDaNonPerdere)
            .Where(a => !clausole.Contains((c.Prodotto, a))).Select(a => $"{c.Id}: {a}"))];
        string[] nonEsclusioni = [.. golden.Casi.SelectMany(c => c.Attese.EsclusioniDaNonPerdere
            .Where(a => tipi.TryGetValue((c.Prodotto, a), out TipoClausola tipo) && tipo != TipoClausola.Esclusione).Select(a => $"{c.Id}: {a}"))];

        Assert.That(golden.Casi, Has.Count.EqualTo(15));
        Assert.That(golden.Casi.Select(c => c.Id), Is.Unique);
        Assert.That(inesistenti, Is.Empty, "articoli attesi che non esistono tra le clausole del prodotto");
        Assert.That(nonEsclusioni, Is.Empty, "esclusioni da non perdere che non sono esclusioni");
        Assert.That(golden.Casi.SelectMany(c => c.Attese.EsclusioniDaNonPerdere.Where(e => !c.Attese.Rilevanti.Contains(e))), Is.Empty,
            "ogni esclusione da non perdere è anche rilevante");
        Assert.That(golden.Casi.Select(c => c.Attese.Rilevanti.Count), Has.All.InRange(1, 5), "al più 5 rilevanti: recall@5 leggibile");
        Assert.That(golden.Casi.Count(c => c.Prodotto == Prodotto.RcProfTecnici), Is.EqualTo(4));
    }
}
