using System.Globalization;
using System.Text.RegularExpressions;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Core.PreIstruttoria;

public sealed record SchedaValidata(SchedaPreIstruttoria? Scheda, IReadOnlyList<Avviso> Avvisi);

/// <summary>
/// Validazione pura della scheda contro le clausole recuperate (fase-6.md §5): articoli normalizzati e riscritti nella forma del DB,
/// citazioni inesistenti rimosse, tipi incoerenti segnalati, importi non riconducibili a dati noti segnalati (euristica).
/// </summary>
public static partial class CitazioniValidator
{
    /// <summary>
    /// Importi ammessi: massimale e franchigia della polizza, statistiche dei simili e importi scritti nel testo delle clausole
    /// recuperate (es. "minimo 500 euro" dell'Art. 4.2), che il modello può legittimamente riportare.
    /// </summary>
    public static SchedaValidata Valida(
        SchedaPreIstruttoria scheda, IReadOnlyList<ClausolaTrovata> clausole, DatiPolizza polizza, StatisticheSimili statistiche)
    {
        Dictionary<string, ClausolaTrovata> perNumero = clausole
            .Select(c => (Numero: Numero(c.Articolo), Clausola: c))
            .Where(c => c.Numero is not null)
            .DistinctBy(c => c.Numero)
            .ToDictionary(c => c.Numero!, c => c.Clausola);
        List<Avviso> avvisi = [];

        GaranziaOperante[] garanzie = [.. scheda.GaranzieOperanti
            .Select(g => Risolvi(g.Articolo, TipoClausola.Garanzia, "garanzie operanti", perNumero, avvisi) is { } c ? g with { Articolo = c.Articolo } : null)
            .OfType<GaranziaOperante>()];

        EsclusioneDaVerificare[] esclusioni = [.. scheda.EsclusioniDaVerificare
            .Select(e => Risolvi(e.Articolo, TipoClausola.Esclusione, "esclusioni da verificare", perNumero, avvisi) is { } c ? e with { Articolo = c.Articolo } : null)
            .OfType<EsclusioneDaVerificare>()];

        FranchigiaApplicabile? franchigia = scheda.FranchigiaApplicabile is { } f
            && Risolvi(f.Articolo, TipoClausola.Franchigia, "franchigia applicabile", perNumero, avvisi) is { } clausola
            ? f with { Articolo = clausola.Articolo }
            : null;

        SchedaPreIstruttoria validata = scheda with { GaranzieOperanti = garanzie, EsclusioniDaVerificare = esclusioni, FranchigiaApplicabile = franchigia };
        avvisi.AddRange(ImportiNonVerificati(validata, clausole, polizza, statistiche));

        return new SchedaValidata(validata, avvisi);
    }

    /// <summary>Numero dell'articolo: da "Art. 2.4", "art 2.4", "Articolo 2.4" o "2.4" si estrae "2.4".</summary>
    public static string? Numero(string? articolo) =>
        articolo is null ? null : NumeroArticolo().Match(articolo) is { Success: true } match ? match.Value : null;

    private static ClausolaTrovata? Risolvi(
        string articolo, TipoClausola atteso, string sezione, Dictionary<string, ClausolaTrovata> perNumero, List<Avviso> avvisi)
    {
        if (Numero(articolo) is not { } numero || !perNumero.TryGetValue(numero, out ClausolaTrovata? clausola))
        {
            avvisi.Add(new Avviso(TipoAvviso.Citazione, $"{articolo} citato dal modello in {sezione} ma non tra le clausole recuperate: rimosso."));

            return null;
        }

        if (clausola.Tipo != atteso)
        {
            avvisi.Add(new Avviso(TipoAvviso.Citazione,
                $"{clausola.Articolo} ({clausola.Tipo.Descrizione()}) indicato in {sezione}: tipo incoerente, da verificare."));
        }

        return clausola;
    }

    private static IEnumerable<Avviso> ImportiNonVerificati(
        SchedaPreIstruttoria scheda, IReadOnlyList<ClausolaTrovata> clausole, DatiPolizza polizza, StatisticheSimili statistiche)
    {
        HashSet<decimal> ammessi = [polizza.Massimale, polizza.Franchigia, .. clausole.SelectMany(c => Importi(c.Testo + " " + c.Titolo))];

        foreach (decimal? statistica in new[] { statistiche.LiquidatoMin, statistiche.LiquidatoMediana, statistiche.LiquidatoMax })
        {
            if (statistica is { } valore)
            {
                ammessi.Add(valore);
                ammessi.Add(Math.Round(valore));
            }
        }

        IEnumerable<string> testi = scheda.GaranzieOperanti.Select(g => g.Motivazione)
            .Concat(scheda.EsclusioniDaVerificare.Select(e => e.CosaVerificare))
            .Concat(scheda.PuntiDaChiarireConCliente)
            .Append(scheda.FranchigiaApplicabile?.Descrizione ?? "")
            .Append(scheda.ValutazioneSintetica);

        return testi
            .SelectMany(Importi)
            .Where(importo => !ammessi.Contains(importo))
            .Distinct()
            .Select(importo => new Avviso(TipoAvviso.Importo,
                $"importo non verificato: {Formati.EuroIntero(importo)} non compare tra dati di polizza, statistiche e clausole."));
    }

    /// <summary>Importi seguiti o preceduti da "€" o "euro", in formato italiano ("5.000", "250,00") o semplice ("5000").</summary>
    internal static IEnumerable<decimal> Importi(string testo) =>
        ImportoInEuro().Matches(testo)
            .Select(m => m.Groups["prima"].Success ? m.Groups["prima"].Value : m.Groups["dopo"].Value)
            .Select(v => decimal.Parse(v.Replace(".", "", StringComparison.Ordinal), NumberStyles.Number, Formati.Italiano));

    [GeneratedRegex(@"\d+(\.\d+)*")]
    private static partial Regex NumeroArticolo();

    [GeneratedRegex(@"(?<prima>\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:,\d+)?)\s*(?:€|euro\b)|€\s*(?<dopo>\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:,\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ImportoInEuro();
}
