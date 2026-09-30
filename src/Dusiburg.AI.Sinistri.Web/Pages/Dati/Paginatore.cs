using Microsoft.AspNetCore.Http.Extensions;

namespace Dusiburg.AI.Sinistri.Web.Pages.Dati;

/// <summary>Link di una pagina di un elenco: stessa query string (filtri compresi) con il solo parametro <c>pagina</c> cambiato.</summary>
public static class Paginatore
{
    public static string Url(HttpRequest request, int pagina)
    {
        var query = new QueryBuilder(request.Query
            .Where(p => !p.Key.Equals("pagina", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string>(p.Key, v ?? ""))));

        if (pagina > 1)
        {
            query.Add("pagina", pagina.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return request.Path + query.ToQueryString().ToString();
    }
}

/// <summary>Barra di paginazione: pagina corrente, totale e link alle pagine vicine.</summary>
public sealed record PaginazioneVista(int Numero, int Pagine, int Totale, string Etichetta);
