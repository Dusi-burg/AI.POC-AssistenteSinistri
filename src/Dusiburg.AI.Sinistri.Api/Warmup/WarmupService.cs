using System.Diagnostics;
using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Api.Warmup;

/// <summary>
/// Warm-up all'avvio (fase-8.md §2): un embedding e una risposta minima della chat, così il primo utente della demo non paga il
/// caricamento dei modelli (~10 s per qwen3.5:9b). In background: l'API risponde subito. Si disattiva con <c>SINISTRI_WARMUP=false</c>.
/// </summary>
public sealed class WarmupService(
    SinistriOptions opzioni, IEmbeddingService embedding, ChatModel chat, ILogger<WarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opzioni.Warmup)
        {
            logger.LogInformation("Warm-up disattivato ({Chiave}=false)", SinistriOptions.Keys.Warmup);

            return;
        }

        await PassoAsync("embedding", () => embedding.EmbedQueryAsync("prova", stoppingToken), stoppingToken);
        await PassoAsync("chat", () =>
        {
            ChatOptions opzioniChat = chat.DefaultOptions.Clone();
            opzioniChat.ModelId = chat.ModelId;
            opzioniChat.MaxOutputTokens = 8;

            return chat.ChatClient.GetResponseAsync("Rispondi solo: ok", opzioniChat, stoppingToken);
        }, stoppingToken);
    }

    /// <summary>Un errore non ferma l'API: il probe di health mostrerà comunque lo stato dei modelli.</summary>
    private async Task PassoAsync<T>(string nome, Func<Task<T>> passo, CancellationToken stoppingToken)
    {
        var cronometro = Stopwatch.StartNew();

        try
        {
            await passo();
            logger.LogInformation("Warm-up {Passo} completato in {Durata} ms", nome, cronometro.ElapsedMilliseconds);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Warm-up {Passo} non riuscito", nome);
        }
    }
}
