using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Options;

namespace Dusiburg.AI.Sinistri.Tests.Dominio;

public class EnumMetadataTests
{
    [Test]
    public void OgniCausa_DichiaraIlProdotto()
    {
        //SUT
        Assert.That(() => Enum.GetValues<CausaSinistro>().Select(c => c.ProdottoDellaCausa()).ToList(), Throws.Nothing);
    }

    [Test]
    public void Cause_FiltratePerProdotto()
    {
        //SUT
        IReadOnlyList<CausaSinistro> rc = Prodotto.RcProfTecnici.Cause();

        Assert.That(rc, Is.EqualTo(new[]
        {
            CausaSinistro.ErroreProgettuale, CausaSinistro.ErroreDirezioneLavori, CausaSinistro.SicurezzaCantiere, CausaSinistro.PerditaPatrimoniale
        }));
        Assert.That(Prodotto.CasaFabbricati.Cause(), Has.Count.EqualTo(7));
    }

    [Test]
    public void Descrizione_DaAttributoOppureNome()
    {
        //SUT
        Assert.That(CausaSinistro.AcquaCondotta.Descrizione(), Is.EqualTo("Acqua condotta"));
        Assert.That(CausaSinistro.Furto.Descrizione(), Is.EqualTo("Furto"));
    }

    [Test]
    public void FromConfiguration_OgniProviderDellaConfigurazioneHaIlSuoMembro()
    {
        //SUT
        Assert.That(EmbeddingProviders.All.Select(EnumMetadata.FromConfiguration), Is.EquivalentTo(Enum.GetValues<EmbeddingProvider>()));
    }
}
