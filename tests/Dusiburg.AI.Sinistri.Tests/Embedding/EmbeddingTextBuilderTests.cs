using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;

namespace Dusiburg.AI.Sinistri.Tests.Embedding;

public class EmbeddingTextBuilderTests
{
    [Test]
    public void Sinistro_SenzaEsito_OmetteEsito()
    {
        //SUT
        string conEsito = EmbeddingTextBuilder.Sinistro(CausaSinistro.AcquaCondotta, "Si è rotto il tubo in bagno.", "Rottura accertata.");
        string senzaEsito = EmbeddingTextBuilder.Sinistro(CausaSinistro.AcquaCondotta, "Si è rotto il tubo in bagno.", null);
        string esitoVuoto = EmbeddingTextBuilder.Sinistro(CausaSinistro.AcquaCondotta, "Si è rotto il tubo in bagno.", " ");

        Assert.That(conEsito, Is.EqualTo("Causa: Acqua condotta. Si è rotto il tubo in bagno. Esito perizia: Rottura accertata."));
        Assert.That(senzaEsito, Is.EqualTo("Causa: Acqua condotta. Si è rotto il tubo in bagno."));
        Assert.That(esitoVuoto, Is.EqualTo(senzaEsito));
    }

    [Test]
    public void Clausola_UsaEtichettaLeggibile()
    {
        //SUT
        string testo = EmbeddingTextBuilder.Clausola(TipoClausola.Franchigia, "Scoperto furto", "Si applica uno scoperto del 20%.");

        Assert.That(testo, Is.EqualTo("Franchigia / scoperto / limite - Scoperto furto. Si applica uno scoperto del 20%."));
    }

    [Test]
    public void Denuncia_CausaSoloSeNota()
    {
        //SUT
        string conCausa = EmbeddingTextBuilder.Denuncia("Grandine sui pannelli.", CausaSinistro.EventoAtmosferico);
        string senzaCausa = EmbeddingTextBuilder.Denuncia("Grandine sui pannelli.", null);

        Assert.That(conCausa, Is.EqualTo("Causa: Evento atmosferico. Grandine sui pannelli."));
        Assert.That(senzaCausa, Is.EqualTo("Grandine sui pannelli."));
    }
}
