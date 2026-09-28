using Bogus;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Ingestion.Seed;

namespace Dusiburg.AI.Sinistri.Tests.Seed;

public class TemplateTests
{
    /// <summary>Numero di template per causa della tabella "Parametri per causa" di fase-3.md.</summary>
    private static readonly Dictionary<CausaSinistro, int> TemplateAttesi = new()
    {
        [CausaSinistro.AcquaCondotta] = 15, [CausaSinistro.EventoAtmosferico] = 12, [CausaSinistro.FenomenoElettrico] = 10,
        [CausaSinistro.Incendio] = 8, [CausaSinistro.Furto] = 8, [CausaSinistro.Cristalli] = 8, [CausaSinistro.RcProprieta] = 8,
        [CausaSinistro.ErroreProgettuale] = 12, [CausaSinistro.ErroreDirezioneLavori] = 10, [CausaSinistro.SicurezzaCantiere] = 8,
        [CausaSinistro.PerditaPatrimoniale] = 8,
    };

    [Test]
    public void OgniSlotHaValori()
    {
        //SETUP
        var random = new Randomizer(1);

        foreach (ProfiloCausa profilo in CatalogoTemplate.Profili)
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> slot = CatalogoTemplate.Slot(profilo.Prodotto);
            IEnumerable<string> testi = profilo.Template.SelectMany(t => t.EsitiChiuso.Concat(t.EsitiRespinto).Prepend(t.Testo))
                .Concat(profilo.EsitiChiuso).Concat(profilo.EsitiRespinto);

            foreach (string testo in testi)
            {
                //SUT
                string compilato = CatalogoTemplate.Compila(testo, profilo.Prodotto, [], random);

                Assert.That(CatalogoTemplate.SlotUsati(testo), Is.All.Matches<string>(slot.ContainsKey), testo);
                Assert.That(compilato, Does.Not.Contain("{").And.Not.Contain("}"), testo);
            }
        }
    }

    [Test]
    public void Profili_CoprononoTutteLeCauseConIParametriDellaSpecifica()
    {
        //SUT
        IReadOnlyList<ProfiloCausa> profili = CatalogoTemplate.Profili;

        Assert.That(profili.Select(p => p.Causa), Is.EquivalentTo(Enum.GetValues<CausaSinistro>()));
        Assert.That(profili.ToDictionary(p => p.Causa, p => p.Template.Count), Is.EquivalentTo(TemplateAttesi));
        Assert.That(profili.Sum(p => p.Quota), Is.EqualTo(1.0).Within(1e-9));
        Assert.That(profili.SelectMany(p => p.Template).Where(t => t.Causa.ProdottoDellaCausa() != CatalogoTemplate.Profilo(t.Causa).Prodotto), Is.Empty);
        Assert.That(profili.Where(p => p.EsitiChiuso.Count < 2 || p.EsitiRespinto.Count < 2), Is.Empty);
        Assert.That(profili.SelectMany(p => p.Template).Where(t => t.Variante == VarianteEsito.TendenzaRespinto && t.EsitiRespinto.Count < 2), Is.Empty);
    }

    [Test]
    public void TemaDemo_OgniTemaHaAlmenoUnTemplate()
    {
        //SUT
        IEnumerable<TemaDemo> temi = CatalogoTemplate.Profili.SelectMany(p => p.Template).Select(t => t.Tema).Distinct();

        Assert.That(temi, Is.EquivalentTo(Enum.GetValues<TemaDemo>()));
    }
}
