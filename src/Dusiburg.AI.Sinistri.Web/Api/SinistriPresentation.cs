using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>
/// Regole di presentazione della UI (come <c>ErpPresentation</c> di O2C): classi Bootstrap per stati e tipi, formati italiani.
/// I formati numerici sono quelli di <see cref="Formati"/>, uguali a CLI e Markdown.
/// </summary>
public static class SinistriPresentation
{
    public static string ProbeClass(ProbeStatus stato) => stato switch
    {
        ProbeStatus.Ok => "text-bg-success",
        ProbeStatus.Info => "text-bg-info",
        ProbeStatus.Warning => "text-bg-warning",
        _ => "text-bg-danger"
    };

    public static string ProbeEtichetta(ProbeStatus stato) => stato switch
    {
        ProbeStatus.Ok => "OK",
        ProbeStatus.Info => "info",
        ProbeStatus.Warning => "avviso",
        _ => "errore"
    };

    /// <summary>Indicatore del layout: rosso se c'è un errore, giallo con soli avvisi, verde altrimenti.</summary>
    public static ProbeStatus Complessivo(ProbeReport report) =>
        report.HasErrors ? ProbeStatus.Error : report.HasWarnings ? ProbeStatus.Warning : ProbeStatus.Ok;

    public static string StatoSinistroClass(StatoSinistro stato) => stato switch
    {
        StatoSinistro.Chiuso => "text-bg-success",
        StatoSinistro.Respinto => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    /// <summary>Colori della scheda (fase-8.md §3.1): garanzie verdi, esclusioni arancioni, franchigie blu.</summary>
    public static string TipoClausolaClass(TipoClausola tipo) => tipo switch
    {
        TipoClausola.Garanzia => "text-bg-success",
        TipoClausola.Esclusione => "text-bg-warning",
        TipoClausola.Franchigia => "text-bg-primary",
        _ => "text-bg-secondary"
    };

    public static string MotivoClass(MotivoSegnalazione motivo) => motivo switch
    {
        MotivoSegnalazione.SoloTestoSimile => "alert-secondary",
        _ => "alert-warning"
    };

    public static string Euro(decimal? importo) => Formati.EuroIntero(importo);

    public static string Distanza(double distanza) => Formati.Distanza(distanza);

    public static string Secondi(TimeSpan durata) => Formati.Secondi(durata);

    public static string Percentuale(double valore) => (valore * 100).ToString("0", Formati.Italiano) + "%";

    public static string Decimale(double valore) => valore.ToString("0.00", Formati.Italiano);

    /// <summary>
    /// Larghezza della barra della distanza coseno (0 = identico): piena a distanza 0, vuota a <paramref name="scala"/>. Il valore
    /// va nello stile CSS, quindi con il punto decimale indipendentemente dalla cultura.
    /// </summary>
    public static string BarraVicinanza(double distanza, double scala = 1.0) =>
        Math.Clamp((1 - distanza / scala) * 100, 0, 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>Id dell'elemento <c>&lt;dialog&gt;</c> con il testo della clausola (una per articolo nella pagina).</summary>
    public static string DialogClausola(int idClausola) => $"clausola-{idClausola}";

    /// <summary>Link alla traccia nel dashboard di Aspire; null se l'indirizzo del dashboard o il trace id non sono noti.</summary>
    public static string? LinkTraccia(string? dashboard, string? traceId) =>
        string.IsNullOrWhiteSpace(dashboard) || string.IsNullOrWhiteSpace(traceId) ? null : $"{dashboard.TrimEnd('/')}/traces/detail/{traceId}";
}
