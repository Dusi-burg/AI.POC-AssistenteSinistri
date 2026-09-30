using System.Globalization;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Consultazione;

// Consultazione dei dati demo dalla Web (fase-9b.md §1): elenchi paginati di polizze e sinistri, catalogo delle clausole.

public sealed record PolizzaElenco(
    string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateOnly Decorrenza, DateOnly Scadenza,
    decimal Massimale, decimal Franchigia, int Sinistri, bool Demo);

/// <summary>Filtri dello storico; i null non filtrano. <see cref="Testo"/> cerca nella descrizione.</summary>
public sealed record FiltriElencoSinistri(
    Prodotto? Prodotto = null,
    CausaSinistro? Causa = null,
    StatoSinistro? Stato = null,
    string? Provincia = null,
    int? AnnoDenuncia = null,
    int? RiparatoreId = null,
    string? NumeroPolizza = null,
    string? Testo = null);

/// <param name="Gemello">Sinistro con cui forma una coppia di quasi-duplicati attesa (<c>data/duplicati_attesi.json</c>), se c'è.</param>
public sealed record SinistroElenco(
    int Id, string Numero, string NumeroPolizza, Prodotto Prodotto, string Contraente, string? Riparatore,
    DateOnly DataEvento, DateOnly DataDenuncia, string Provincia, CausaSinistro Causa, StatoSinistro Stato,
    decimal? ImportoLiquidato, string Descrizione, string? Gemello = null);

/// <summary>Una pagina di un elenco: <see cref="Numero"/> parte da 1, <see cref="Totale"/> conta tutte le righe filtrate.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Righe, int Numero, int DimensionePagina, int Totale)
{
    public int Pagine => Totale == 0 ? 1 : (Totale + DimensionePagina - 1) / DimensionePagina;
}

public static class Paginazione
{
    public const int DimensioneDefault = 25;

    public const int DimensioneMax = 100;
}

/// <summary>Ordine delle clausole per articolo in senso numerico: "Art. 2.10" dopo "Art. 2.9".</summary>
public static class OrdineArticoli
{
    public static (int Capo, int Numero) Chiave(string articolo)
    {
        string[] parti = articolo.Replace("Art.", "", StringComparison.OrdinalIgnoreCase).Trim().Split('.');

        return (Numero(parti, 0), Numero(parti, 1));

        static int Numero(string[] parti, int i) =>
            parti.Length > i && int.TryParse(parti[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue;
    }

    /// <summary>Ancora del catalogo, uguale nella pagina web e in <c>docs/clausole.md</c>: <c>casafabbricati-art-2-10</c>.</summary>
    public static string Ancora(Prodotto prodotto, string articolo)
    {
        (int capo, int numero) = Chiave(articolo);

        return string.Create(CultureInfo.InvariantCulture, $"{prodotto.ToString().ToLowerInvariant()}-art-{capo}-{numero}");
    }
}
