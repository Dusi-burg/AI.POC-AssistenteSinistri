using System.Diagnostics;
using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dusiburg.AI.Sinistri.DbInit;

/// <summary>Opzione <c>--parafrasa-con-llm</c>: parafrasi via Ollama (manopole <c>OLLAMA_*</c>) con barra di avanzamento e stima del tempo.</summary>
internal static class Parafrasi
{
    public static async Task<DatiSintetici> ApplicaAsync(DatiSintetici dati, SinistriOptions options, CancellationToken cancellationToken)
    {
        using ChatModel chat = new ChatClientFactory(options, NullLoggerFactory.Instance).Create();
        var parafrasatore = new DescrizioneParafrasatore(chat.ChatClient, chat.DefaultOptions);
        int totale = DescrizioneParafrasatore.DaParafrasare(dati);
        var cronometro = Stopwatch.StartNew();

        Console.WriteLine($"Parafrasi di {totale} descrizioni con {chat.ModelId} (i gemelli dei quasi-duplicati si rigenerano dagli originali)...");

        var avanzamento = new Progress<int>(fatti =>
        {
            TimeSpan stimaResidua = cronometro.Elapsed / fatti * (totale - fatti);
            int pieni = fatti * 30 / totale;
            Console.Write($"\r  [{new string('#', pieni),-30}] {fatti}/{totale}  restano circa {stimaResidua:hh\\:mm\\:ss}   ");
        });

        DatiSintetici parafrasati = await parafrasatore.ParafrasaAsync(dati, avanzamento, cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"Parafrasi completata in {cronometro.Elapsed:hh\\:mm\\:ss}: le descrizioni non sono deterministiche (seed LLM).");

        return parafrasati;
    }
}
