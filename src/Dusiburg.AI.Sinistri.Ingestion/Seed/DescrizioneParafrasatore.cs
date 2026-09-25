using Bogus;
using Dusiburg.AI.Sinistri.Core.Seed;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>
/// Opzione <c>--parafrasa-con-llm</c> di DbInit (fase-3.md): riscrive le descrizioni come le scriverebbe un cliente. Un solo
/// tentativo a temperatura 0.7; in caso di errore resta il testo originale. Il risultato non è deterministico.
/// </summary>
public sealed class DescrizioneParafrasatore(IChatClient chatClient, ChatOptions opzioniDefault)
{
    internal const string Istruzione =
        "Riscrivi questa descrizione come la scriverebbe un cliente che denuncia un sinistro, in prima persona, 2–4 frasi, " +
        "senza aggiungere fatti nuovi né importi. Rispondi solo con il testo riscritto.";

    private const float Temperatura = 0.7f;

    public async Task<string> ParafrasaAsync(string descrizione, CancellationToken cancellationToken)
    {
        ChatOptions opzioni = opzioniDefault.Clone();
        opzioni.Temperature = Temperatura;

        try
        {
            ChatResponse risposta = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.System, Istruzione), new ChatMessage(ChatRole.User, descrizione)], opzioni, cancellationToken);
            string testo = risposta.Text.Trim().Trim('"', '«', '»').Trim();

            return testo.Length > 0 ? testo : descrizione;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return descrizione;
        }
    }

    /// <summary>
    /// Parafrasa tutte le descrizioni tranne quelle dei gemelli, che vengono rigenerate dall'originale parafrasato con la
    /// riformulazione controllata: la coppia resta un quasi-duplicato. <paramref name="avanzamento"/> riceve il numero di testi fatti.
    /// </summary>
    public async Task<DatiSintetici> ParafrasaAsync(DatiSintetici dati, IProgress<int>? avanzamento, CancellationToken cancellationToken)
    {
        Dictionary<string, string> originaleDelGemello = dati.Coppie.ToDictionary(c => c.Duplicato, c => c.Originale);
        Dictionary<string, SinistroSintetico> parafrasati = [];
        int fatti = 0;

        foreach (SinistroSintetico sinistro in dati.Sinistri.Where(s => !originaleDelGemello.ContainsKey(s.Numero)))
        {
            string descrizione = await ParafrasaAsync(sinistro.Descrizione, cancellationToken);
            parafrasati[sinistro.Numero] = sinistro with { Descrizione = descrizione };
            avanzamento?.Report(++fatti);
        }

        var random = new Randomizer(dati.RandomSeed);
        SinistroSintetico[] sinistri =
        [
            .. dati.Sinistri.Select(s => originaleDelGemello.TryGetValue(s.Numero, out string? originale)
                ? s with { Descrizione = QuasiDuplicatiGenerator.Riformula(parafrasati[originale].Descrizione, random) }
                : parafrasati[s.Numero])
        ];

        return dati with { Sinistri = sinistri };
    }

    public static int DaParafrasare(DatiSintetici dati) => dati.Sinistri.Count - dati.Coppie.Count;
}
