using System.ComponentModel;
using System.Reflection;
using Dusiburg.AI.Sinistri.Core.Options;

namespace Dusiburg.AI.Sinistri.Core.Dominio;

public static class EnumMetadata
{
    /// <summary>Etichetta italiana leggibile: <c>[Description]</c> se presente, altrimenti il nome del membro.</summary>
    public static string Descrizione<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        Field(value)?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();

    public static Prodotto ProdottoDellaCausa(this CausaSinistro causa) =>
        Field(causa)?.GetCustomAttribute<ProdottoCausaAttribute>()?.Prodotto
        ?? throw new InvalidOperationException($"La causa {causa} non dichiara il prodotto ([ProdottoCausa]).");

    public static IReadOnlyList<CausaSinistro> Cause(this Prodotto prodotto) =>
        [.. Enum.GetValues<CausaSinistro>().Where(c => c.ProdottoDellaCausa() == prodotto)];

    /// <summary>Dal valore di <c>EMBEDDING_PROVIDER</c> (<see cref="EmbeddingProviders"/>) al membro dell'enum salvato nel DB.</summary>
    public static EmbeddingProvider FromConfiguration(string provider) => provider switch
    {
        EmbeddingProviders.Ollama => EmbeddingProvider.Ollama,
        EmbeddingProviders.OpenAiCompatible => EmbeddingProvider.OpenAiCompatible,
        EmbeddingProviders.Onnx => EmbeddingProvider.Onnx,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Provider di embedding sconosciuto.")
    };

    private static FieldInfo? Field<TEnum>(TEnum value) where TEnum : struct, Enum =>
        typeof(TEnum).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static);
}
