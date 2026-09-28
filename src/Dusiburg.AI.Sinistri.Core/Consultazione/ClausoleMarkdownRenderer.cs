using System.Text;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Consultazione;

/// <summary>
/// <c>docs/clausole.md</c> (fase-9b.md §4): il catalogo delle clausole per prodotto e per tipo, con le stesse ancore della pagina web
/// (<see cref="OrdineArticoli.Ancora"/>). Puro: le clausole arrivano dal DB tramite il comando <c>export-clausole</c>.
/// </summary>
public static class ClausoleMarkdownRenderer
{
    /// <summary>Riga con la data di generazione: l'unica che cambia tra due esportazioni delle stesse clausole.</summary>
    public const string PrefissoRigaGenerazione = "> Generato con `export-clausole` il ";

    /// <summary>Ordine delle sezioni, uguale alla pagina web: prima ciò che definisce, poi ciò che copre, esclude e limita.</summary>
    public static readonly IReadOnlyList<TipoClausola> OrdineTipi =
        [TipoClausola.Definizione, TipoClausola.Garanzia, TipoClausola.Esclusione, TipoClausola.Franchigia];

    public static string Render(IReadOnlyList<ClausolaDettaglio> clausole, DateOnly generatoIl)
    {
        var md = new StringBuilder();
        Prodotto[] prodotti = [.. Enum.GetValues<Prodotto>().Where(p => clausole.Any(c => c.Prodotto == p))];

        Riga(md, "# Catalogo delle clausole di polizza");
        Riga(md);
        Riga(md, "> Clausole **fittizie**, scritte per la demo dell'assistente di pre-istruttoria: non sono condizioni di polizza reali.");
        Riga(md, $"{PrefissoRigaGenerazione}{generatoIl:yyyy-MM-dd} dal database; la fonte è `db/003_seed_clausole.sql`.");
        Riga(md, "> Lo stesso catalogo, con la ricerca, è nella pagina *Dati demo → Catalogo clausole* della Web.");
        Riga(md);
        Riga(md, "## Indice");
        Riga(md);

        foreach (Prodotto prodotto in prodotti)
        {
            string conteggi = string.Join(", ", OrdineTipi
                .Select(t => (Tipo: t, Numero: clausole.Count(c => c.Prodotto == prodotto && c.Tipo == t)))
                .Where(t => t.Numero > 0)
                .Select(t => $"{Plurale(t.Tipo).ToLowerInvariant()} {t.Numero}"));
            Riga(md, $"- [{prodotto.Descrizione()}](#{AncoraTitolo(prodotto.Descrizione())}): {conteggi}");
        }

        foreach (Prodotto prodotto in prodotti)
        {
            Riga(md);
            Riga(md, $"## {prodotto.Descrizione()}");

            foreach (TipoClausola tipo in OrdineTipi)
            {
                ClausolaDettaglio[] delTipo = [.. clausole.Where(c => c.Prodotto == prodotto && c.Tipo == tipo)
                    .OrderBy(c => OrdineArticoli.Chiave(c.Articolo))];

                if (delTipo.Length == 0)
                {
                    continue;
                }

                Riga(md);
                Riga(md, $"### {Plurale(tipo)}");

                foreach (ClausolaDettaglio clausola in delTipo)
                {
                    Riga(md);
                    Riga(md, $"<a id=\"{OrdineArticoli.Ancora(prodotto, clausola.Articolo)}\"></a>");
                    Riga(md);
                    Riga(md, $"#### {clausola.Articolo} — {clausola.Titolo}");
                    Riga(md);
                    Riga(md, clausola.Testo);
                }
            }
        }

        return md.ToString();
    }

    /// <summary>Il testo senza la riga della data, per confrontare due esportazioni.</summary>
    public static string SenzaData(string markdown) =>
        string.Join('\n', markdown.Replace("\r\n", "\n").Split('\n').Where(r => !r.StartsWith(PrefissoRigaGenerazione, StringComparison.Ordinal)));

    private static string Plurale(TipoClausola tipo) => tipo switch
    {
        TipoClausola.Definizione => "Definizioni",
        TipoClausola.Garanzia => "Garanzie",
        TipoClausola.Esclusione => "Esclusioni",
        _ => "Franchigie, scoperti e limiti"
    };

    /// <summary>Ancora che GitHub assegna a un titolo: minuscole, spazi in trattini, punteggiatura tolta.</summary>
    private static string AncoraTitolo(string titolo) =>
        new string([.. titolo.ToLowerInvariant().Replace(' ', '-').Where(c => char.IsLetterOrDigit(c) || c == '-')]);

    private static void Riga(StringBuilder md, string testo = "") => md.Append(testo).Append('\n');
}
