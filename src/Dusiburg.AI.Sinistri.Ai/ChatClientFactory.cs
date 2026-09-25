using Dusiburg.AI.Sinistri.Ai.Ollama;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;

namespace Dusiburg.AI.Sinistri.Ai;

/// <summary>Modello di chat pronto all'uso: client con logging e telemetria, opzioni di default.</summary>
public sealed record ChatModel(IChatClient ChatClient, string ModelId, ChatOptions DefaultOptions) : IDisposable
{
    public void Dispose() => ChatClient.Dispose();
}

/// <summary>Come <c>ModelClientFactory</c> di O2C, con il solo provider Ollama: la chat gira sempre sulla GPU (D18).</summary>
public sealed class ChatClientFactory(SinistriOptions options, ILoggerFactory loggerFactory)
{
    private const int MaxOutputTokens = 4_096;

    public ChatModel Create()
    {
        // OllamaApiClient implementa sia IChatClient sia IEmbeddingGenerator: il tipo esplicito scioglie l'ambiguità di AsBuilder.
        IChatClient ollama = OllamaClients.Create(options.OllamaEndpoint, OllamaClients.GenerationTimeout, options.OllamaChatModel);

        ChatClientBuilder builder = ollama
            .AsBuilder()
            .UseLogging(loggerFactory)
            .UseOpenTelemetry(loggerFactory, SinistriTelemetry.Sources.Chat);

        // Diagnostica opzionale (D19): ogni richiesta al modello salvata su file, per vedere il prompt reale.
        if (options.PromptCaptureDirectory is { } captureDirectory)
        {
            builder.Use(client => new PromptCaptureChatClient(client, captureDirectory));
        }

        return new ChatModel(builder.Build(), options.OllamaChatModel, DefaultOptions(options.OllamaContextLength));
    }

    internal static ChatOptions DefaultOptions(int contextLength)
    {
        var defaults = new ChatOptions { Temperature = 0.1f, MaxOutputTokens = MaxOutputTokens };

        // Qwen 3.5 con il thinking attivo consuma token e tempo senza migliorare una scheda strutturata.
        defaults.AddOllamaOption(OllamaOption.Think, false);
        defaults.AddOllamaOption(OllamaOption.NumCtx, contextLength);

        return defaults;
    }
}
