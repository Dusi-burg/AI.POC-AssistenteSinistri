using System.CommandLine;
using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// <c>fraud-scan --mesi 12 [--soglia 0.05] [--valuta] [--duplicati-attesi file]</c> (fase-7.md §3): coppie di sinistri sotto soglia e,
/// se esiste <c>data/duplicati_attesi.json</c> (o con <c>--valuta</c>), precision/recall per le soglie standard.
/// </summary>
internal static class FraudScanCommand
{
    public static Command Create(IServiceProvider services)
    {
        var mesi = new Option<int>("--mesi") { Description = "Finestra sulla data di denuncia, in mesi.", DefaultValueFactory = _ => 12 };
        var soglia = new Option<double?>("--soglia") { Description = "Distanza coseno massima (default SOGLIA_DUPLICATO_COSINE)." };
        var valuta = new Option<bool>("--valuta") { Description = "Confronta con le coppie attese; errore se il file non c'è." };
        var fileAttese = new Option<FileInfo?>("--duplicati-attesi") { Description = "File delle coppie attese (default data/duplicati_attesi.json)." };

        var command = new Command("fraud-scan", "Possibili duplicati tra i sinistri denunciati nel periodo.") { mesi, soglia, valuta, fileAttese };

        mesi.Validators.Add(risultato =>
        {
            if (risultato.GetValueOrDefault<int>() <= 0)
            {
                risultato.AddError("--mesi deve essere maggiore di zero.");
            }
        });
        soglia.Validators.Add(risultato =>
        {
            if (risultato.GetValueOrDefault<double?>() is <= 0 or > 2)
            {
                risultato.AddError("--soglia deve essere compresa tra 0 (escluso) e 2.");
            }
        });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string percorso = parseResult.GetValue(fileAttese)?.FullName
                ?? Path.Combine(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory), DuplicatiAttesiFile.NomeFile);
            IReadOnlyList<CoppiaDuplicati>? attese = null;

            if (File.Exists(percorso))
            {
                attese = (await DuplicatiAttesiFile.LeggiAsync(percorso, cancellationToken)).Coppie;
            }
            else if (parseResult.GetValue(valuta))
            {
                await Console.Error.WriteLineAsync($"File delle coppie attese non trovato: {percorso}");

                return SinistriCli.ExitError;
            }

            RisultatoFraudScan risultato = await services.GetRequiredService<AntifrodeService>()
                .ScansionaAsync(parseResult.GetValue(mesi), parseResult.GetValue(soglia), attese, cancellationToken);

            Write(risultato, attese, percorso, Console.Out);

            return 0;
        });

        return command;
    }

    internal static void Write(RisultatoFraudScan risultato, IReadOnlyList<CoppiaDuplicati>? attese, string percorsoAttese, TextWriter output)
    {
        output.WriteLine($"Sinistri denunciati negli ultimi {risultato.Mesi} mesi, soglia {Formati.Distanza(risultato.Soglia)}: " +
            $"{risultato.Coppie.Count} coppie ({Formati.Secondi(risultato.Durata)}).");

        // Prima le coppie con un legame tra i sinistri, poi quelle con il solo testo simile (per lo più testi ripetuti dai template).
        Coppie(output, "Coppie con stesso contraente o riparatore", [.. risultato.Coppie.Where(c => c.ConLegame)], attese);
        Coppie(output, "Coppie con solo testo simile", [.. risultato.Coppie.Where(c => !c.ConLegame)], attese);

        output.WriteLine();
        output.WriteLine("Per motivo: " + string.Join(" · ", Enum.GetValues<MotivoSegnalazione>()
            .Select(m => $"{m.Descrizione()} {risultato.Coppie.Count(c => c.Motivo == m)}")));

        if (risultato.Valutazione is { } valutazione && attese is not null)
        {
            Valutazione(output, valutazione, attese.Count, risultato.Soglia, percorsoAttese);
        }
    }

    private static void Coppie(TextWriter output, string titolo, IReadOnlyList<CoppiaSospetta> coppie, IReadOnlyList<CoppiaDuplicati>? attese)
    {
        output.WriteLine();
        output.WriteLine($"{titolo} ({coppie.Count})");

        if (coppie.Count == 0)
        {
            return;
        }

        string colonnaAttesa = attese is null ? "" : "Att. ";
        output.WriteLine($"{colonnaAttesa}{"NumeroA",-16}{"NumeroB",-16}{"Distanza",9}  {"Motivo",-31}{"Giorni",6}  DescrizioneA | DescrizioneB");

        foreach (CoppiaSospetta c in coppie)
        {
            string attesa = attese is null ? "" : ValutazioneDuplicati.Attesa(c, attese) ? " ✓   " : "     ";
            output.WriteLine($"{attesa}{c.NumeroA,-16}{c.NumeroB,-16}{Formati.Distanza(c.Distanza),9}  {c.Motivo.Descrizione(),-31}" +
                $"{c.GiorniTraDenunce,6}  {Formati.Tronca(c.DescrizioneA, 60)} | {Formati.Tronca(c.DescrizioneB, 60)}");
        }
    }

    private static void Valutazione(TextWriter output, ValutazioneFraudScan valutazione, int numeroAttese, double soglia, string percorso)
    {
        output.WriteLine();
        output.WriteLine($"Valutazione rispetto a {percorso} ({numeroAttese} coppie attese; suggerita la soglia più bassa con recall ≥ " +
            $"{ValutazioneDuplicati.RecallMinima.ToString("0.0", Formati.Italiano)})");

        Tabella(output, "Tutte le coppie", valutazione.Tutte, valutazione.SuggeritaTutte);
        Tabella(output, "Solo coppie con stesso contraente o riparatore", valutazione.ConLegame, valutazione.SuggeritaConLegame);

        output.WriteLine();

        if (ValutazioneDuplicati.Distribuzione(valutazione.DistanzeAttese.Where(d => d.Distanza is not null).Select(d => d.Distanza!.Value)) is { } d)
        {
            output.WriteLine($"Distanze delle coppie attese: min {Formati.Distanza(d.Min)} · mediana {Formati.Distanza(d.Mediana)} · max {Formati.Distanza(d.Max)}");
        }

        output.WriteLine($"Coppia non attesa più vicina: {Descrivi(valutazione.NonAttesaPiuVicina)}");
        output.WriteLine($"Coppia non attesa più vicina con stesso contraente o riparatore: {Descrivi(valutazione.NonAttesaConLegamePiuVicina)}");

        List<DistanzaCoppiaAttesa> mancanti = [.. valutazione.DistanzeAttese.Where(a => !a.InPeriodo || a.Distanza is not { } x || x >= soglia)];
        output.WriteLine();
        output.WriteLine(mancanti.Count == 0
            ? $"Tutte le coppie attese sono sotto la soglia {Formati.Distanza(soglia)}."
            : $"Coppie attese non trovate alla soglia {Formati.Distanza(soglia)} ({mancanti.Count}):");

        foreach (DistanzaCoppiaAttesa a in mancanti)
        {
            string distanza = a.Distanza is { } valore ? Formati.Distanza(valore) : "n.d. (sinistro mancante o senza embedding)";
            string periodo = a.InPeriodo ? "" : " · fuori periodo";
            output.WriteLine($"  {a.Coppia.Originale} ↔ {a.Coppia.Duplicato}  distanza {distanza} ({a.Coppia.Tipo}){periodo}");
        }
    }

    private static void Tabella(TextWriter output, string titolo, IReadOnlyList<RigaValutazione> righe, RigaValutazione? suggerita)
    {
        output.WriteLine();
        output.WriteLine(titolo);
        output.WriteLine($"{"Soglia",6}{"Trovate",9}{"TP",5}{"FP",6}{"FN",5}{"Precision",11}{"Recall",8}{"F1",7}");

        foreach (RigaValutazione r in righe)
        {
            string nota = r == suggerita ? "   ← suggerita" : "";
            output.WriteLine($"{Formati.Distanza(r.Soglia),6}{r.Trovate,9}{r.VeriPositivi,5}{r.FalsiPositivi,6}{r.FalsiNegativi,5}" +
                $"{Decimale(r.Precision),11}{Decimale(r.Recall),8}{Decimale(r.F1),7}{nota}");
        }

        if (suggerita is null)
        {
            output.WriteLine($"Nessuna soglia raggiunge la recall minima {ValutazioneDuplicati.RecallMinima.ToString("0.0", Formati.Italiano)}.");
        }
    }

    private static string Decimale(double valore) => valore.ToString("0.00", Formati.Italiano);

    private static string Descrivi(CoppiaSospetta? coppia) => coppia is null
        ? "nessuna sotto la soglia più alta valutata"
        : $"{coppia.NumeroA} ↔ {coppia.NumeroB} a {Formati.Distanza(coppia.Distanza)} ({coppia.Motivo.Descrizione()})";
}
