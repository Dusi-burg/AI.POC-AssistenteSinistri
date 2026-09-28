using System.CommandLine;
using Dusiburg.AI.Sinistri.Cli.Valutazione;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Valutazione;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// <c>eval [--golden file] [--top 5] [--embedding-model nome --embedding-provider p --embedding-dimensions n]</c> (fase-9.md §3):
/// recall@k, MRR e hit@1 del retrieval delle clausole sul golden set, report in <c>eval/report_&lt;data&gt;.md</c>.
/// </summary>
internal static class EvalCommand
{
    public static Command Create(IServiceProvider services)
    {
        var golden = new Option<FileInfo?>("--golden") { Description = "Golden set (default data/golden_set.json)." };
        var top = new Option<int>("--top") { Description = "k della recall@k.", DefaultValueFactory = _ => EvalService.KDefault };
        var modello = new Option<string?>("--embedding-model") { Description = "Modello alternativo: valutato su un DB dedicato (Sinistri_emb_…)." };
        var provider = new Option<string>("--embedding-provider")
        {
            Description = $"Runtime del modello alternativo: {string.Join(", ", EmbeddingProviders.All)}.",
            DefaultValueFactory = _ => EmbeddingProviders.Ollama
        };
        var dimensioni = new Option<int?>("--embedding-dimensions") { Description = "Dimensione del modello alternativo (obbligatoria con --embedding-model)." };
        var cartella = new Option<DirectoryInfo?>("--report-dir") { Description = "Cartella del report (default eval/)." };

        var command = new Command("eval", "Valuta il retrieval delle clausole sul golden set e salva il report.")
        {
            golden, top, modello, provider, dimensioni, cartella
        };

        top.Validators.Add(risultato =>
        {
            if (risultato.GetValueOrDefault<int>() is < 1 or > EvalService.MaxRank)
            {
                risultato.AddError($"--top deve essere tra 1 e {EvalService.MaxRank}.");
            }
        });
        command.Validators.Add(risultato =>
        {
            if (risultato.GetValue(modello) is not null && risultato.GetValue(dimensioni) is null)
            {
                risultato.AddError("--embedding-dimensions è obbligatorio con --embedding-model: la dimensione non è mai implicita.");
            }
        });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string radice = Directory.GetParent(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory))!.FullName;
            string percorsoGolden = parseResult.GetValue(golden)?.FullName ?? Path.Combine(radice, "data", GoldenSetFile.NomeFile);
            string cartellaReport = parseResult.GetValue(cartella)?.FullName ?? Path.Combine(radice, "eval");

            try
            {
                GoldenSet goldenSet = await GoldenSetFile.LeggiAsync(percorsoGolden, cancellationToken);
                ServiceProvider? alternativi = null;
                IServiceProvider servizi = services;

                if (parseResult.GetValue(modello) is { } nome)
                {
                    var alternativo = new ModelloAlternativo(nome, parseResult.GetValue(provider)!, parseResult.GetValue(dimensioni)!.Value);
                    alternativi = alternativo.CreaServizi(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<ILoggerFactory>());
                    await alternativo.PreparaAsync(alternativi, Console.Out, cancellationToken);
                    servizi = alternativi;
                }

                await using (alternativi)
                {
                    string database = servizi.GetRequiredService<SqlConnectionFactory>().Target.Database;
                    RisultatoEval risultato = await servizi.GetRequiredService<EvalService>().ValutaAsync(
                        goldenSet, parseResult.GetValue(top), database, new AvanzamentoCasi(), cancellationToken);

                    Console.WriteLine();
                    Write(risultato, Console.Out);

                    Directory.CreateDirectory(cartellaReport);
                    string report = Path.Combine(cartellaReport, EvalReportRenderer.NomeFile(risultato));
                    await File.WriteAllTextAsync(report, EvalReportRenderer.Render(risultato), cancellationToken);
                    Console.WriteLine($"Report salvato in {report}");
                }

                return 0;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or IOException or System.Text.Json.JsonException)
            {
                Console.WriteLine();
                await Console.Error.WriteLineAsync($"eval non completato: {exception.Message}");

                return SinistriCli.ExitError;
            }
        });

        return command;
    }

    internal static void Write(RisultatoEval risultato, TextWriter output)
    {
        output.WriteLine($"Modello {risultato.EmbeddingModel} ({risultato.EmbeddingProvider}, {risultato.EmbeddingDimensions}) · DB {risultato.Database} · " +
            $"{risultato.Casi.Count} casi in {Formati.Secondi(risultato.Durata)}");
        output.WriteLine();
        output.WriteLine($"{"Caso",-5}{"Prodotto",-16}{$"R@{risultato.K}",6}{"Rank",6}{"Escl.",7}  Mancanti");

        foreach (RisultatoCaso caso in risultato.Casi)
        {
            string esclusioni = caso.RecallEsclusioni is { } e ? Decimale(e) : "—";
            output.WriteLine($"{caso.Caso.Id,-5}{caso.Caso.Prodotto,-16}{Decimale(caso.RecallAtK),6}{caso.RankPrimoRilevante?.ToString() ?? "—",6}{esclusioni,7}  " +
                $"{string.Join(", ", caso.Mancanti)}");
        }

        output.WriteLine();
        output.WriteLine($"recall@{risultato.K} {Decimale(risultato.RecallMedia)} (obiettivo ≥ {Decimale(EvalReportRenderer.ObiettivoRecall)}) · " +
            $"MRR {Decimale(risultato.Mrr)} · hit@1 {Decimale(risultato.Hit1)} · " +
            $"recall esclusioni {(risultato.RecallEsclusioniMedia is { } r ? Decimale(r) : "—")}");
    }

    private static string Decimale(double valore) => valore.ToString("0.00", Formati.Italiano);

    /// <summary>Caso in corso sulla stessa riga; sincrono, a differenza di <see cref="Progress{T}"/>, per non mescolarsi alla tabella.</summary>
    private sealed class AvanzamentoCasi : IProgress<string>
    {
        public void Report(string value) => Console.Write($"\rcaso {value}");
    }
}
