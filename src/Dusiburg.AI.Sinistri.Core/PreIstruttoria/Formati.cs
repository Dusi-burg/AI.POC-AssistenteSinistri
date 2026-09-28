using System.Globalization;

namespace Dusiburg.AI.Sinistri.Core.PreIstruttoria;

/// <summary>Formattazione italiana di importi e percentuali, uguale nel prompt, nella scheda e nella console.</summary>
public static class Formati
{
    public static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>"300.000,00 €".</summary>
    public static string Euro(decimal importo) => importo.ToString("N2", Italiano) + " €";

    /// <summary>"3.100 €", oppure "—" se l'importo non c'è.</summary>
    public static string EuroIntero(decimal? importo) => importo is { } valore ? valore.ToString("N0", Italiano) + " €" : "—";

    public static string Percentuale(decimal valore) => valore.ToString("0.0", Italiano) + "%";

    public static string Distanza(double valore) => valore.ToString("0.000", Italiano);

    public static string Secondi(TimeSpan durata) => durata.TotalSeconds.ToString("0.0", Italiano) + " s";

    public static string Tronca(string testo, int massimo) => testo.Length <= massimo ? testo : testo[..(massimo - 1)] + "…";
}
