using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Retrieval;

/// <summary>Clausola trovata dalla ricerca A (fase-5.md §2). <see cref="Integrativa"/>: esclusione o franchigia aggiunta oltre le prime.</summary>
public sealed record ClausolaTrovata(
    int Id, string Articolo, TipoClausola Tipo, string Titolo, string Testo, double Distanza, int Rank, bool Integrativa);

/// <summary>Filtri SQL della ricerca ibrida dello storico (fase-5.md §3). I null non filtrano.</summary>
public sealed record FiltriStorico(
    Prodotto Prodotto,
    int AnniStorico,
    string? Provincia = null,
    decimal? ImportoMin = null,
    CausaSinistro? Causa = null,
    IReadOnlyCollection<int>? EscludiSinistriIds = null);

public sealed record SinistroSimile(
    int Id, string Numero, DateOnly DataEvento, string Provincia, CausaSinistro Causa,
    string Descrizione, string? EsitoPerizia, StatoSinistro Stato, decimal? ImportoLiquidato, double Distanza);

/// <summary>Statistiche sull'insieme esatto dei simili mostrati (D11): min, mediana e max solo sui chiusi.</summary>
public sealed record StatisticheSimili(
    int NumeroCasi, int Respinti, decimal PercentualeRespinti, decimal? LiquidatoMin, decimal? LiquidatoMediana, decimal? LiquidatoMax);

/// <summary>Tempi mostrati a console e nella UI: calcolo del vettore della denuncia e query SQL.</summary>
public sealed record TempiRicerca(TimeSpan Embedding, TimeSpan Query);

public sealed record RisultatoRicercaClausole(IReadOnlyList<ClausolaTrovata> Clausole, TempiRicerca Tempi);

public sealed record RisultatoRicercaStorico(IReadOnlyList<SinistroSimile> Simili, StatisticheSimili Statistiche, TempiRicerca Tempi);

public interface IClausolaRepository
{
    /// <summary>
    /// Le prime <paramref name="top"/> per distanza, più la migliore esclusione e la migliore franchigia entro la soglia (D10); se
    /// nessuna franchigia è tra queste, la franchigia di base <paramref name="articoloFranchigiaBase"/> (null: nessuna).
    /// </summary>
    Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
        float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, string? articoloFranchigiaBase,
        CancellationToken cancellationToken);
}

public interface ISinistroRepository
{
    /// <summary>Sinistri chiusi o respinti del prodotto, filtrati in SQL e ordinati per distanza.</summary>
    Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken);

    Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken);
}
