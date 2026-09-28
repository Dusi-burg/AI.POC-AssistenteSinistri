using System.CommandLine;
using System.Globalization;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>Comandi di debug della Fase 5: <c>search-clausole</c> e <c>search-sinistri</c> (fase-5.md §6).</summary>
internal static class SearchCommands
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public static Command Clausole(IServiceProvider services)
    {
        var testo = new Argument<string>("testo") { Description = "Testo della denuncia." };
        Option<Prodotto> prodotto = OpzioniDominio.OpzioneProdotto();
        var top = new Option<int?>("--top") { Description = "Numero di clausole (default Retrieval:TopClausole)." };

        var command = new Command("search-clausole", "Clausole pertinenti a una denuncia, con esclusioni e franchigie integrative.") { testo, prodotto, top };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            RisultatoRicercaClausole risultato = await services.GetRequiredService<RicercaService>().CercaClausoleAsync(
                parseResult.GetValue(testo)!, parseResult.GetValue(prodotto), parseResult.GetValue(top), cancellationToken);

            WriteClausole(risultato, Console.Out);

            return 0;
        });

        return command;
    }

    public static Command Sinistri(IServiceProvider services)
    {
        var testo = new Argument<string>("testo") { Description = "Testo della denuncia." };
        Option<Prodotto> prodotto = OpzioniDominio.OpzioneProdotto();
        var provincia = new Option<string?>("--provincia") { Description = "Sigla della provincia (es. MI)." };
        var importoMin = new Option<decimal?>("--importo-min") { Description = "Liquidato minimo in euro (esclude i respinti)." };
        Option<CausaSinistro?> causa = OpzioniDominio.OpzioneCausa();
        var anni = new Option<int?>("--anni") { Description = "Anni di storico (default Retrieval:AnniStorico)." };
        var top = new Option<int?>("--top") { Description = "Numero di sinistri (default Retrieval:TopSinistri)." };

        var command = new Command("search-sinistri", "Sinistri storici simili (ricerca ibrida) e statistiche di liquidazione.")
        {
            testo, prodotto, provincia, importoMin, causa, anni, top
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            RetrievalOptions retrieval = services.GetRequiredService<IOptions<RetrievalOptions>>().Value;
            var filtri = new FiltriStorico(
                parseResult.GetValue(prodotto),
                parseResult.GetValue(anni) ?? retrieval.AnniStorico,
                parseResult.GetValue(provincia)?.Trim().ToUpperInvariant(),
                parseResult.GetValue(importoMin),
                parseResult.GetValue(causa));

            RisultatoRicercaStorico risultato = await services.GetRequiredService<RicercaService>()
                .CercaStoricoAsync(parseResult.GetValue(testo)!, filtri, parseResult.GetValue(top), cancellationToken);

            WriteSinistri(risultato, Console.Out);

            return 0;
        });

        return command;
    }

    internal static void WriteClausole(RisultatoRicercaClausole risultato, TextWriter output)
    {
        output.WriteLine($" #  {"Articolo",-10}{"Tipo",-12}{"Distanza",9}  Titolo");

        foreach (ClausolaTrovata clausola in risultato.Clausole)
        {
            string integrativa = clausola.Integrativa ? "  (integrativa: limita la copertura)" : "";
            output.WriteLine($"{clausola.Rank,2}  {clausola.Articolo,-10}{clausola.Tipo,-12}{clausola.Distanza,9:0.0000}  {clausola.Titolo}{integrativa}");
        }

        output.WriteLine();
        output.WriteLine(Tempi(risultato.Tempi));
    }

    internal static void WriteSinistri(RisultatoRicercaStorico risultato, TextWriter output)
    {
        output.WriteLine($" #  {"Numero",-16}{"Data",-11}{"Prov",-5}{"Causa",-22}{"Stato",-9}{"Liquidato",11}{"Distanza",9}  Descrizione");

        int riga = 0;

        foreach (SinistroSimile sinistro in risultato.Simili)
        {
            string liquidato = sinistro.ImportoLiquidato is { } importo ? importo.ToString("N0", Italiano) + " €" : "—";
            string descrizione = sinistro.Descrizione.Length > 80 ? sinistro.Descrizione[..79] + "…" : sinistro.Descrizione;

            output.WriteLine($"{++riga,2}  {sinistro.Numero,-16}{sinistro.DataEvento,-11:yyyy-MM-dd}{sinistro.Provincia,-5}{sinistro.Causa,-22}" +
                $"{sinistro.Stato,-9}{liquidato,11}{sinistro.Distanza,9:0.0000}  {descrizione}");
        }

        StatisticheSimili statistiche = risultato.Statistiche;

        output.WriteLine();
        output.WriteLine("┌ Statistiche sui sinistri mostrati (calcolate in SQL)");
        output.WriteLine($"│ Casi: {statistiche.NumeroCasi} · respinti: {statistiche.Respinti} ({statistiche.PercentualeRespinti.ToString("0.0", Italiano)}%)");
        output.WriteLine($"│ Liquidato dei chiusi: min {Euro(statistiche.LiquidatoMin)} · mediana {Euro(statistiche.LiquidatoMediana)} · max {Euro(statistiche.LiquidatoMax)}");
        output.WriteLine("└");
        output.WriteLine(Tempi(risultato.Tempi));
    }

    private static string Euro(decimal? importo) => importo is { } valore ? valore.ToString("N0", Italiano) + " €" : "—";

    private static string Tempi(TempiRicerca tempi) =>
        $"Tempi: embedding {tempi.Embedding.TotalMilliseconds:0} ms · query SQL {tempi.Query.TotalMilliseconds:0} ms";
}
