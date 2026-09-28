using System.Text.Json;
using System.Text.Json.Serialization;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Core.Consultazione;

// Contratti tra API e UI (fase-8.md §2): la Web referenzia solo Core e parla con l'API in HTTP.

/// <summary>JSON di API e client: camelCase ed enum come stringhe, uguale sui due lati.</summary>
public static class SinistriJson
{
    public static readonly JsonSerializerOptions Opzioni = Configura(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Applica le stesse regole alle opzioni dell'host (<c>ConfigureHttpJsonOptions</c>).</summary>
    public static JsonSerializerOptions Configura(JsonSerializerOptions opzioni)
    {
        opzioni.Converters.Add(new JsonStringEnumConverter());

        return opzioni;
    }
}

/// <summary>Nomi degli eventi SSE di <c>POST /api/pre-istruttoria/stream</c>.</summary>
public static class EventiPreIstruttoria
{
    /// <summary>Dato: <see cref="PreIstruttoria.AvanzamentoPreIstruttoria"/> (durata null = passo appena iniziato).</summary>
    public const string Passo = "passo";

    /// <summary>Dato: <see cref="PreIstruttoria.EsitoPreIstruttoria"/>; ultimo evento in caso di successo.</summary>
    public const string Esito = "esito";

    /// <summary>Dato: <c>ProblemDetails</c> (status, title, detail); ultimo evento in caso di errore.</summary>
    public const string Errore = "errore";
}

public sealed record PolizzaVoce(
    string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateOnly Decorrenza, DateOnly Scadenza, bool Demo);

public sealed record RiparatoreVoce(int Id, string RagioneSociale);

public sealed record ClausolaDettaglio(int Id, Prodotto Prodotto, string Articolo, TipoClausola Tipo, string Titolo, string Testo);

public sealed record SinistroDettaglio(
    string Numero,
    string NumeroPolizza,
    Prodotto Prodotto,
    string Contraente,
    string? Riparatore,
    DateOnly DataEvento,
    DateOnly DataDenuncia,
    string Provincia,
    CausaSinistro Causa,
    string Descrizione,
    string? EsitoPerizia,
    StatoSinistro Stato,
    decimal? ImportoRiservato,
    decimal? ImportoLiquidato);

public sealed record StatisticheDataset(ConteggioSeed Conteggi, IReadOnlyList<DistribuzioneCausaStato> CausaStato, EmbeddingInfo? Embedding);

/// <summary>Modelli, soglie e parametri di retrieval in uso, in sola lettura per la pagina di stato.</summary>
public sealed record ConfigurazioneDemo(
    string ChatModel, string EmbeddingModel, string EmbeddingProvider, int EmbeddingDimensions,
    double SogliaDuplicatoCosine, double SogliaDuplicatoDenuncia, RetrievalOptions Retrieval);

public sealed record RichiestaRicercaClausole(string Testo, Prodotto Prodotto, int? Top = null);

/// <summary>Filtri di <see cref="Retrieval.FiltriStorico"/> in forma di richiesta: gli anni mancanti valgono <c>Retrieval:AnniStorico</c>.</summary>
public sealed record RichiestaRicercaSinistri(
    string Testo, Prodotto Prodotto, string? Provincia = null, decimal? ImportoMin = null, CausaSinistro? Causa = null,
    int? AnniStorico = null, int? Top = null);

public sealed record RichiestaFraudScan(int Mesi = 12, double? Soglia = null);

/// <summary>Letture per le pagine della demo (fase-8.md §2); le ricerche vettoriali restano nei repository di Retrieval e Antifrode.</summary>
public interface IConsultazioneRepository
{
    /// <summary>Polizze per numero o contraente (testo contenuto), le demo per prime; senza testo le prime <paramref name="top"/>.</summary>
    Task<IReadOnlyList<PolizzaVoce>> CercaPolizzeAsync(string? cerca, int top, CancellationToken cancellationToken);

    Task<IReadOnlyList<RiparatoreVoce>> GetRiparatoriAsync(CancellationToken cancellationToken);

    Task<ClausolaDettaglio?> GetClausolaAsync(int id, CancellationToken cancellationToken);

    Task<SinistroDettaglio?> GetSinistroAsync(string numero, CancellationToken cancellationToken);

    Task<StatisticheDataset> GetStatisticheDatasetAsync(CancellationToken cancellationToken);
}
