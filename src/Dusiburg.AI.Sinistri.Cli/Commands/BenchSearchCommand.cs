using System.CommandLine;
using System.Diagnostics;
using Dusiburg.AI.Sinistri.Cli.Benchmark;
using Dusiburg.AI.Sinistri.Cli.Configuration;
using Dusiburg.AI.Sinistri.Core.Benchmark;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Core.Valutazione;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// <c>bench-search [--sinistri 50000] [--ripetizioni 50] [--k 10]</c> (fase-10.md §10.1): ricerca esatta del pilastro B contro
/// <c>VECTOR_SEARCH</c> sull'indice DiskANN, su un database dedicato. Report in <c>eval/bench_&lt;data&gt;_&lt;n&gt;.md</c>.
/// </summary>
internal static class BenchSearchCommand
{
    /// <summary><c>TOP_N</c> dell'indice in multipli di k: 5× è il valore del piano, 1× e 20× mostrano il compromesso.</summary>
    public static readonly IReadOnlyList<int> Fattori = [1, 5, 20];

    /// <summary>Provincia dei filtri selettivi: la più frequente del seed (~11% dei sinistri).</summary>
    public const string ProvinciaSelettiva = "MI";

    public static Command Create(IServiceProvider services)
    {
        var sinistri = new Option<int>("--sinistri") { Description = "Sinistri del database del banco.", DefaultValueFactory = _ => 50_000 };
        var ripetizioni = new Option<int>("--ripetizioni") { Description = "Ripetizioni di ogni interrogazione.", DefaultValueFactory = _ => 50 };
        var k = new Option<int>("--k") { Description = "Risultati per interrogazione.", DefaultValueFactory = _ => 10 };
        var cartella = new Option<DirectoryInfo?>("--report-dir") { Description = "Cartella del report (default eval/)." };

        var command = new Command("bench-search", "Confronta la ricerca esatta nello storico con l'indice DiskANN (VECTOR_SEARCH).")
        {
            sinistri, ripetizioni, k, cartella
        };

        // Validatori sulle singole opzioni: quello del comando non vede i default delle opzioni non indicate.
        sinistri.Validators.Add(r => { if (r.GetValueOrDefault<int>() < SyntheticDataGenerator.SinistriDefault) r.AddError($"--sinistri almeno {SyntheticDataGenerator.SinistriDefault}."); });
        ripetizioni.Validators.Add(r => { if (r.GetValueOrDefault<int>() < 1) r.AddError("--ripetizioni almeno 1."); });
        k.Validators.Add(r => { if (r.GetValueOrDefault<int>() is < 1 or > 50) r.AddError("--k tra 1 e 50."); });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var cronometro = Stopwatch.StartNew();
            string radice = Directory.GetParent(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory))!.FullName;
            string cartellaReport = parseResult.GetValue(cartella)?.FullName ?? Path.Combine(radice, "eval");
            var database = new DatabaseBench(parseResult.GetValue(sinistri));

            try
            {
                await using ServiceProvider servizi = ServiziSovrascritti.Crea(
                    services.GetRequiredService<IConfiguration>(), services.GetRequiredService<ILoggerFactory>(), database.Database);

                DatasetBench dataset = await database.PreparaAsync(servizi, Console.Out, cancellationToken);
                GoldenSet golden = await GoldenSetFile.LeggiAsync(Path.Combine(radice, "data", GoldenSetFile.NomeFile), cancellationToken);
                IReadOnlyList<InterrogazioneBench> interrogazioni =
                    Interrogazioni(golden, servizi.GetRequiredService<IOptions<RetrievalOptions>>().Value.AnniStorico);

                Console.WriteLine($"Misuro {interrogazioni.Count} interrogazioni × {parseResult.GetValue(ripetizioni)} ripetizioni...");
                IReadOnlyList<MisuraBench> misure = await servizi.GetRequiredService<BenchRicercaService>().MisuraAsync(
                    interrogazioni, Fattori, parseResult.GetValue(k), parseResult.GetValue(ripetizioni), new AvanzamentoInterrogazioni(), cancellationToken);
                Console.WriteLine();

                var risultato = new RisultatoBench(DateTimeOffset.Now, dataset, servizi.GetRequiredService<SinistriOptions>().EmbeddingModel,
                    parseResult.GetValue(k), parseResult.GetValue(ripetizioni), BenchMetrics.Riepiloga(misure, parseResult.GetValue(k)), cronometro.Elapsed);
                string markdown = BenchReportRenderer.Render(risultato);

                Directory.CreateDirectory(cartellaReport);
                string report = Path.Combine(cartellaReport, BenchReportRenderer.NomeFile(risultato));
                await File.WriteAllTextAsync(report, markdown, cancellationToken);

                Console.WriteLine(markdown);
                Console.WriteLine($"Report salvato in {report}");

                return 0;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or IOException
                                                or Microsoft.Data.SqlClient.SqlException)
            {
                Console.WriteLine();
                await Console.Error.WriteLineAsync($"bench-search non completato: {exception.Message}");

                return SinistriCli.ExitError;
            }
        });

        return command;
    }

    /// <summary>
    /// Filtri di base: le 15 denunce del golden set e i 4 scenari di pre-istruttoria, con i filtri della scheda (prodotto, stato, anni).
    /// Filtri selettivi: lo scenario 5 e le denunce del golden set ristrette alla provincia di Milano.
    /// </summary>
    internal static IReadOnlyList<InterrogazioneBench> Interrogazioni(GoldenSet golden, int anniStorico)
    {
        List<InterrogazioneBench> interrogazioni = [];

        foreach (CasoGolden caso in golden.Casi)
        {
            interrogazioni.Add(new InterrogazioneBench(caso.Id, caso.Denuncia, new FiltriStorico(caso.Prodotto, anniStorico), Selettiva: false));
        }

        foreach (DemoScenario scenario in DemoCatalog.Scenari.Where(s => s.Tipo == TipoScenario.PreIstruttoria))
        {
            interrogazioni.Add(new InterrogazioneBench($"S{scenario.Numero}", scenario.Testo,
                new FiltriStorico(scenario.Prodotto!.Value, anniStorico), Selettiva: false));
        }

        DemoScenario storico = DemoCatalog.Scenari.Single(s => s.Tipo == TipoScenario.RicercaStorico);
        interrogazioni.Add(new InterrogazioneBench($"S{storico.Numero}", storico.Testo,
            new FiltriStorico(storico.Prodotto!.Value, anniStorico, storico.Provincia, storico.ImportoMinimo, storico.Causa), Selettiva: true));

        foreach (CasoGolden caso in golden.Casi)
        {
            interrogazioni.Add(new InterrogazioneBench($"{caso.Id}-{ProvinciaSelettiva}", caso.Denuncia,
                new FiltriStorico(caso.Prodotto, anniStorico, ProvinciaSelettiva), Selettiva: true));
        }

        return interrogazioni;
    }

    private sealed class AvanzamentoInterrogazioni : IProgress<string>
    {
        public void Report(string value) => Console.Write($"\rinterrogazione {value}      ");
    }
}
