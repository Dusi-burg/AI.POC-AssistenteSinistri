using System.ComponentModel;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Core.PreIstruttoria;

// Contratti della pre-istruttoria (fase-6.md §1), usati da CLI, API (Fase 8) e valutazione (Fase 9).

public sealed record RichiestaPreIstruttoria(
    string Denuncia,
    string NumeroPolizza,
    DateOnly? DataEvento = null,
    CausaSinistro? CausaIndicata = null,
    int? RiparatoreId = null);

/// <summary>Scheda restituita dal modello: è anche lo schema JSON richiesto (nomi in camelCase).</summary>
public sealed record SchedaPreIstruttoria(
    IReadOnlyList<GaranziaOperante> GaranzieOperanti,
    IReadOnlyList<EsclusioneDaVerificare> EsclusioniDaVerificare,
    FranchigiaApplicabile? FranchigiaApplicabile,
    IReadOnlyList<string> PuntiDaChiarireConCliente,
    string ValutazioneSintetica);

public sealed record GaranziaOperante(string Articolo, string Motivazione);

public sealed record EsclusioneDaVerificare(string Articolo, string CosaVerificare);

public sealed record FranchigiaApplicabile(string Articolo, string Descrizione);

public sealed record DatiPolizza(
    string Numero, Prodotto Prodotto, string Contraente, string Provincia,
    DateOnly Decorrenza, DateOnly Scadenza, decimal Massimale, decimal Franchigia)
{
    public bool InVigoreIl(DateOnly data) => data >= Decorrenza && data <= Scadenza;
}

public enum TipoAvviso
{
    Polizza = 1,
    Parsing = 2,
    Citazione = 3,
    Importo = 4,
}

public sealed record Avviso(TipoAvviso Tipo, string Messaggio);

/// <summary>Motivo di una segnalazione antifrode (fase-7.md §1): solo codice, non persistito.</summary>
public enum MotivoSegnalazione : byte
{
    [Description("stesso contraente")]
    StessoContraente = 1,

    [Description("stesso riparatore")]
    StessoRiparatore = 2,

    [Description("stesso contraente e riparatore")]
    StessoContraenteERiparatore = 3,

    [Description("solo testo simile")]
    SoloTestoSimile = 4,
}

/// <summary>Nuova denuncia → sinistro esistente molto simile (fase-7.md §2): mostrata accanto alla scheda, mai passata al modello.</summary>
public sealed record SegnalazioneDuplicato(
    string NumeroSinistro, DateOnly DataDenuncia, CausaSinistro Causa, StatoSinistro Stato,
    string Contraente, string? Riparatore, double Distanza, MotivoSegnalazione Motivo, string Descrizione);

/// <summary>Avanzamento per passi mostrato a console (fase-6.md §4): durata null = passo appena iniziato.</summary>
public sealed record AvanzamentoPreIstruttoria(string Passo, TimeSpan? Durata);

public sealed record TempiEsecuzione(
    TimeSpan Polizza, TimeSpan Embedding, TimeSpan Clausole, TimeSpan Storico, TimeSpan Statistiche, TimeSpan Llm, TimeSpan Antifrode,
    TimeSpan Totale);

public sealed record EsitoPreIstruttoria(
    RichiestaPreIstruttoria Richiesta,
    DatiPolizza Polizza,
    SchedaPreIstruttoria? Scheda,
    string? TestoLibero,
    IReadOnlyList<ClausolaTrovata> Clausole,
    IReadOnlyList<SinistroSimile> Simili,
    StatisticheSimili Statistiche,
    IReadOnlyList<Avviso> Avvisi,
    IReadOnlyList<SegnalazioneDuplicato> PossibiliDuplicati,
    TempiEsecuzione Tempi,
    string Modello,
    DateTimeOffset GenerataIl);

public interface IPolizzaRepository
{
    /// <summary>Polizza e contraente per chiave di business; null se il numero non esiste.</summary>
    Task<DatiPolizza?> GetByNumeroAsync(string numero, CancellationToken cancellationToken);
}

/// <summary>Errori della richiesta (polizza inesistente o non in vigore): codice di uscita 2 della CLI, 4xx dell'API.</summary>
public abstract class PreIstruttoriaException(string message) : Exception(message);

public sealed class PolizzaNonTrovataException(string numero)
    : PreIstruttoriaException($"La polizza {numero} non esiste.");

public sealed class PolizzaNonInVigoreException(DatiPolizza polizza, DateOnly dataEvento)
    : PreIstruttoriaException($"La polizza {polizza.Numero} non è in vigore alla data evento {dataEvento:yyyy-MM-dd} (validità {polizza.Decorrenza:yyyy-MM-dd} → {polizza.Scadenza:yyyy-MM-dd}).");

/// <summary>Nessuna clausola recuperata: il DB non ha ancora gli embedding.</summary>
public sealed class ClausoleNonDisponibiliException(Prodotto prodotto)
    : PreIstruttoriaException($"Nessuna clausola con embedding per il prodotto {prodotto}: eseguire embed.");
