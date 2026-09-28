using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.DbInit;

/// <summary>Riepilogo finale del seed (fase-3.md, passo 5): conteggi per tabella e due tabelle di distribuzione.</summary>
internal static class Riepilogo
{
    private const int TopProvince = 10;

    public static async Task StampaAsync(SqlConnection connection, string database, int dimensions, TextWriter output, CancellationToken cancellationToken)
    {
        ConteggioSeed conteggio = await SeedRepository.ReadConteggioAsync(connection, cancellationToken);
        IReadOnlyList<DistribuzioneCausaStato> causaStato = await SeedRepository.ReadCausaStatoAsync(connection, cancellationToken);
        IReadOnlyList<DistribuzioneProdottoProvincia> prodottoProvincia = await SeedRepository.ReadProdottoProvinciaAsync(connection, TopProvince, cancellationToken);

        output.WriteLine();
        output.WriteLine($"Contraenti {conteggio.Contraenti} · Riparatori {conteggio.Riparatori} · Polizze {conteggio.Polizze} · Clausole {conteggio.Clausole} · Sinistri {conteggio.Sinistri}");

        StampaCausaStato(causaStato, conteggio.Sinistri, output);

        output.WriteLine();
        output.WriteLine($"Sinistri per prodotto e provincia (prime {TopProvince})");
        foreach (DistribuzioneProdottoProvincia riga in prodottoProvincia)
        {
            output.WriteLine($"  {riga.Prodotto,-16} {riga.Provincia}  {riga.Sinistri,4}");
        }

        output.WriteLine();
        output.WriteLine($"Fatto: database {database} ricreato con VECTOR({dimensions}), lookup popolate dagli enum, {conteggio.Clausole} clausole, {conteggio.Sinistri} sinistri.");
        output.WriteLine("Embedding da calcolare: dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- embed");
    }

    private static void StampaCausaStato(IReadOnlyList<DistribuzioneCausaStato> righe, int totale, TextWriter output)
    {
        StatoSinistro[] stati = Enum.GetValues<StatoSinistro>();

        output.WriteLine();
        output.WriteLine("Sinistri per causa e stato");
        output.WriteLine($"  {"Causa",-22}{string.Concat(stati.Select(s => $"{s,10}"))}{"Totale",8}{"Quota",8}{"Respinti",10}");

        foreach (CausaSinistro causa in Enum.GetValues<CausaSinistro>())
        {
            int[] perStato = [.. stati.Select(s => righe.Where(r => r.Causa == causa && r.Stato == s).Sum(r => r.Sinistri))];
            int sinistri = perStato.Sum();
            double respinti = sinistri == 0 ? 0 : (double)perStato[Array.IndexOf(stati, StatoSinistro.Respinto)] / sinistri;

            output.WriteLine($"  {causa,-22}{string.Concat(perStato.Select(n => $"{n,10}"))}{sinistri,8}{(double)sinistri / totale,8:P0}{respinti,10:P0}");
        }
    }
}
