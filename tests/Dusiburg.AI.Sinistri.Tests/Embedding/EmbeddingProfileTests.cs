using Dusiburg.AI.Sinistri.Core.Embedding;

namespace Dusiburg.AI.Sinistri.Tests.Embedding;

public class EmbeddingProfileTests
{
    [Test]
    public void ForModel_EmbeddingGemma_PrefissiDelBancoDiProva()
    {
        //SUT
        EmbeddingProfile profilo = EmbeddingProfile.ForModel("embeddinggemma");
        EmbeddingProfile conTag = EmbeddingProfile.ForModel("EmbeddingGemma:latest");

        Assert.That(profilo.Query("tubo rotto"), Is.EqualTo("task: search result | query: tubo rotto"));
        Assert.That(profilo.Document("tubo rotto"), Is.EqualTo("title: none | text: tubo rotto"));
        Assert.That(profilo.DimensioneNativa, Is.EqualTo(768));
        Assert.That(conTag, Is.EqualTo(profilo));
    }

    [Test]
    public void ForModel_TagDiversi_DimensioniNativeDiverse()
    {
        //SUT
        EmbeddingProfile piccolo = EmbeddingProfile.ForModel("qwen3-embedding:0.6b");
        EmbeddingProfile grande = EmbeddingProfile.ForModel("qwen3-embedding:4b");
        EmbeddingProfile bge = EmbeddingProfile.ForModel("bge-m3");

        Assert.That((piccolo.DimensioneNativa, grande.DimensioneNativa), Is.EqualTo((1024, 2560)));
        Assert.That(grande.Query("x"), Does.StartWith("Instruct: ").And.EndWith("\nQuery: x"));
        Assert.That(bge.Query("x"), Is.EqualTo("x"));
    }

    [Test]
    public void ForModel_ModelloSconosciuto_Errore()
    {
        //SUT
        Assert.That(() => EmbeddingProfile.ForModel("nomic-embed-text"),
            Throws.InvalidOperationException.With.Message.Contains("nomic-embed-text").And.Message.Contains("embeddinggemma"));
    }
}
