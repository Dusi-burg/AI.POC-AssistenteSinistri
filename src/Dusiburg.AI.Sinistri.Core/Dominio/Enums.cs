using System.ComponentModel;

namespace Dusiburg.AI.Sinistri.Core.Dominio;

// Enum di dominio (D3): ogni membro ha un valore esplicito, che è la PK della tabella di lookup omonima.
// Le righe delle lookup si generano da qui (DatabaseInitializer.SyncLookupsAsync), mai a mano.

public enum Prodotto : byte
{
    [Description("Casa e fabbricati")] CasaFabbricati = 1,
    [Description("RC professionale tecnici")] RcProfTecnici = 2,
}

public enum TipoClausola : byte
{
    Definizione = 1,
    Garanzia = 2,
    Esclusione = 3,
    [Description("Franchigia / scoperto / limite")] Franchigia = 4,
}

public enum CausaSinistro : byte
{
    [ProdottoCausa(Prodotto.CasaFabbricati), Description("Acqua condotta")] AcquaCondotta = 1,
    [ProdottoCausa(Prodotto.CasaFabbricati), Description("Evento atmosferico")] EventoAtmosferico = 2,
    [ProdottoCausa(Prodotto.CasaFabbricati), Description("Fenomeno elettrico")] FenomenoElettrico = 3,
    [ProdottoCausa(Prodotto.CasaFabbricati)] Incendio = 4,
    [ProdottoCausa(Prodotto.CasaFabbricati)] Furto = 5,
    [ProdottoCausa(Prodotto.CasaFabbricati)] Cristalli = 6,
    [ProdottoCausa(Prodotto.CasaFabbricati), Description("RC della proprietà")] RcProprieta = 7,
    [ProdottoCausa(Prodotto.RcProfTecnici), Description("Errore progettuale")] ErroreProgettuale = 20,
    [ProdottoCausa(Prodotto.RcProfTecnici), Description("Errore di direzione lavori")] ErroreDirezioneLavori = 21,
    [ProdottoCausa(Prodotto.RcProfTecnici), Description("Coordinamento sicurezza")] SicurezzaCantiere = 22,
    [ProdottoCausa(Prodotto.RcProfTecnici), Description("Perdita patrimoniale")] PerditaPatrimoniale = 23,
}

public enum StatoSinistro : byte
{
    Aperto = 1,
    Chiuso = 2,
    Respinto = 3,
}

/// <summary>Runtime con cui sono stati calcolati i vettori salvati (Fase 1b): registrato in <c>EmbeddingInfo</c>.</summary>
public enum EmbeddingProvider : byte
{
    Ollama = 1,
    [Description("OpenAI-compatibile (FastFlowLM, Lemonade)")] OpenAiCompatible = 2,
    [Description("ONNX Runtime / Windows ML")] Onnx = 3,
}

/// <summary>Prodotto a cui appartiene una causa: lo usano il generatore dei dati sintetici e la UI per filtrare le cause.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ProdottoCausaAttribute(Prodotto prodotto) : Attribute
{
    public Prodotto Prodotto { get; } = prodotto;
}
