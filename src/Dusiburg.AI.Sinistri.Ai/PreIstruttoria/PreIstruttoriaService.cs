using System.Diagnostics;
using System.Text;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Core.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Ai.PreIstruttoria;

/// <summary>
/// Pre-istruttoria (fase-6.md §2): polizza → embedding della denuncia → clausole e storico → prompt → scheda JSON validata.
/// Un solo vettore della denuncia per entrambe le ricerche. Ogni passo è cronometrato e ha il suo span di telemetria.
/// </summary>
public sealed class PreIstruttoriaService(
    IPolizzaRepository polizze,
    IEmbeddingService embeddingService,
    IClausolaRepository clausole,
    ISinistroRepository sinistri,
    ChatModel chat,
    IOptions<RetrievalOptions> retrieval,
    TimeProvider timeProvider)
{
    private static readonly ActivitySource Source = new(SinistriTelemetry.Sources.PreIstruttoria);

    /// <summary>Schema JSON della scheda, generato dal tipo con le stesse opzioni del parser (camelCase).</summary>
    internal static readonly ChatResponseFormat FormatoScheda = ChatResponseFormat.ForJsonSchema(
        AIJsonUtilities.CreateJsonSchema(typeof(SchedaPreIstruttoria), serializerOptions: SchedaParser.Opzioni),
        nameof(SchedaPreIstruttoria), "Scheda di pre-istruttoria di un sinistro");

    public async Task<EsitoPreIstruttoria> GeneraAsync(
        RichiestaPreIstruttoria richiesta, IProgress<AvanzamentoPreIstruttoria>? avanzamento, Action<string>? tokenRaw, CancellationToken cancellationToken)
    {
        using Activity? traccia = Source.StartActivity("pre-istruttoria");
        traccia?.SetTag("sinistri.polizza", richiesta.NumeroPolizza);
        var totale = Stopwatch.StartNew();
        List<Avviso> avvisi = [];
        RetrievalOptions opzioni = retrieval.Value;

        (DatiPolizza polizza, TimeSpan tPolizza) = await PassoAsync("polizza", avanzamento, () => CaricaPolizzaAsync(richiesta, avvisi, cancellationToken));
        traccia?.SetTag("sinistri.prodotto", polizza.Prodotto.ToString());

        (float[] vettore, TimeSpan tEmbedding) = await PassoAsync("embedding", avanzamento, () =>
            embeddingService.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(richiesta.Denuncia, richiesta.CausaIndicata), cancellationToken));

        (IReadOnlyList<ClausolaTrovata> trovate, TimeSpan tClausole) = await PassoAsync("clausole", avanzamento, () =>
            clausole.CercaPertinentiAsync(vettore, polizza.Prodotto, opzioni.TopClausole, opzioni.DistanzaMaxClausolaIntegrativa,
                opzioni.ArticoloFranchigiaBase, cancellationToken));

        if (trovate.Count == 0)
        {
            throw new ClausoleNonDisponibiliException(polizza.Prodotto);
        }

        (IReadOnlyList<SinistroSimile> simili, TimeSpan tStorico) = await PassoAsync("storico", avanzamento, () =>
            sinistri.CercaSimiliAsync(vettore, new FiltriStorico(polizza.Prodotto, opzioni.AnniStorico), opzioni.TopSinistri, cancellationToken));

        (StatisticheSimili statistiche, TimeSpan tStatistiche) = await PassoAsync("statistiche", avanzamento, () =>
            sinistri.CalcolaStatisticheAsync([.. simili.Select(s => s.Id)], cancellationToken));

        traccia?.SetTag("sinistri.clausole", trovate.Count);
        traccia?.SetTag("sinistri.simili", simili.Count);

        var contesto = new ContestoPrompt(richiesta, polizza, trovate, simili, statistiche, opzioni.SinistriNelPrompt);
        ((SchedaPreIstruttoria? scheda, string? testoLibero), TimeSpan tLlm) = await PassoAsync("generazione scheda", avanzamento, () =>
            GeneraSchedaAsync(contesto, avvisi, tokenRaw, cancellationToken));

        if (scheda is not null)
        {
            using Activity? validazione = Source.StartActivity("validazione");
            SchedaValidata validata = CitazioniValidator.Valida(scheda, trovate, polizza, statistiche);
            scheda = validata.Scheda;
            avvisi.AddRange(validata.Avvisi);
            validazione?.SetTag("sinistri.avvisi", validata.Avvisi.Count);
        }

        var tempi = new TempiEsecuzione(tPolizza, tEmbedding, tClausole, tStorico, tStatistiche, tLlm, totale.Elapsed);
        traccia?.SetTag("sinistri.esito", scheda is null ? "testo libero" : "scheda");

        return new EsitoPreIstruttoria(richiesta, polizza, scheda, testoLibero, trovate, simili, statistiche, avvisi, [], tempi,
            chat.ModelId, timeProvider.GetLocalNow());
    }

    /// <summary>Passi 1 e 1b: polizza inesistente o non in vigore alla data evento → errore; senza data evento, polizza scaduta oggi → avviso.</summary>
    private async Task<DatiPolizza> CaricaPolizzaAsync(RichiestaPreIstruttoria richiesta, List<Avviso> avvisi, CancellationToken cancellationToken)
    {
        DatiPolizza polizza = await polizze.GetByNumeroAsync(richiesta.NumeroPolizza, cancellationToken)
            ?? throw new PolizzaNonTrovataException(richiesta.NumeroPolizza);

        if (richiesta.DataEvento is { } dataEvento)
        {
            return polizza.InVigoreIl(dataEvento) ? polizza : throw new PolizzaNonInVigoreException(polizza, dataEvento);
        }

        DateOnly oggi = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        if (!polizza.InVigoreIl(oggi))
        {
            avvisi.Add(new Avviso(TipoAvviso.Polizza,
                $"La polizza non è in vigore oggi (validità {polizza.Decorrenza:yyyy-MM-dd} → {polizza.Scadenza:yyyy-MM-dd}): verificare la data dell'evento."));
        }

        return polizza;
    }

    /// <summary>JSON con schema; se non è valido un retry con l'errore, poi il fallback in testo libero (fase-6.md §5).</summary>
    private async Task<(SchedaPreIstruttoria? Scheda, string? TestoLibero)> GeneraSchedaAsync(
        ContestoPrompt contesto, List<Avviso> avvisi, Action<string>? tokenRaw, CancellationToken cancellationToken)
    {
        ChatOptions opzioniJson = Opzioni(FormatoScheda);
        List<ChatMessage> messaggi = PromptBuilder.Build(contesto);

        string risposta = await ChiamaAsync(messaggi, opzioniJson, tokenRaw, cancellationToken);
        RisultatoParsing parsing = SchedaParser.Parse(risposta);

        if (!parsing.Riuscito)
        {
            messaggi.Add(new ChatMessage(ChatRole.Assistant, risposta));
            messaggi.Add(new ChatMessage(ChatRole.User,
                $"La risposta non è un JSON valido secondo lo schema (errore: {parsing.Errore}). Rispondi di nuovo solo con il JSON."));

            string primoErrore = parsing.Errore ?? "";
            risposta = await ChiamaAsync(messaggi, opzioniJson, tokenRaw, cancellationToken);
            parsing = SchedaParser.Parse(risposta);

            avvisi.Add(new Avviso(TipoAvviso.Parsing, parsing.Riuscito
                ? $"prima risposta del modello non valida ({primoErrore}): scheda ottenuta al secondo tentativo."
                : $"risposta del modello non valida due volte ({primoErrore}; {parsing.Errore})."));
        }

        if (parsing.Riuscito)
        {
            return (parsing.Scheda, null);
        }

        string testo = await ChiamaAsync(PromptBuilder.Build(contesto, testoLibero: true), Opzioni(formato: null), tokenRaw, cancellationToken);
        avvisi.Add(new Avviso(TipoAvviso.Parsing, "scheda non strutturata: citazioni non validate."));

        return (null, testo.Trim());
    }

    /// <summary>Opzioni di default del client (temperatura, think = false, num_ctx) con il modello e il formato della risposta.</summary>
    private ChatOptions Opzioni(ChatResponseFormat? formato)
    {
        ChatOptions opzioni = chat.DefaultOptions.Clone();
        opzioni.ModelId = chat.ModelId;
        opzioni.ResponseFormat = formato;

        return opzioni;
    }

    /// <summary>Con <paramref name="tokenRaw"/> (opzione <c>--raw</c>) la risposta arriva in streaming e i token si mostrano man mano.</summary>
    private async Task<string> ChiamaAsync(List<ChatMessage> messaggi, ChatOptions opzioni, Action<string>? tokenRaw, CancellationToken cancellationToken)
    {
        if (tokenRaw is null)
        {
            ChatResponse risposta = await chat.ChatClient.GetResponseAsync(messaggi, opzioni, cancellationToken);

            return risposta.Text;
        }

        var testo = new StringBuilder();

        await foreach (ChatResponseUpdate update in chat.ChatClient.GetStreamingResponseAsync(messaggi, opzioni, cancellationToken))
        {
            tokenRaw(update.Text);
            testo.Append(update.Text);
        }

        return testo.ToString();
    }

    private static async Task<(T Risultato, TimeSpan Durata)> PassoAsync<T>(
        string nome, IProgress<AvanzamentoPreIstruttoria>? avanzamento, Func<Task<T>> passo)
    {
        using Activity? activity = Source.StartActivity(nome);
        avanzamento?.Report(new AvanzamentoPreIstruttoria(nome, null));
        var cronometro = Stopwatch.StartNew();

        T risultato = await passo();

        avanzamento?.Report(new AvanzamentoPreIstruttoria(nome, cronometro.Elapsed));

        return (risultato, cronometro.Elapsed);
    }
}
