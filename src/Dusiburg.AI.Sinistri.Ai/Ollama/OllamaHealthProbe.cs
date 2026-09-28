using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace Dusiburg.AI.Sinistri.Ai.Ollama;

/// <summary>
/// Controlli 5–8 e 10 di <c>health</c>: Ollama raggiungibile, modelli presenti, dimensione dell'embedding uguale a
/// <c>EMBEDDING_DIMENSIONS</c>, posizionamento dei modelli (chat su GPU, embedding fuori dalla VRAM, D18).
/// </summary>
public sealed class OllamaHealthProbe(SinistriOptions options, EmbeddingGeneratorFactory embeddingFactory) : IHealthProbe
{
    private const string RaggiungibileNome = "Ollama raggiungibile";
    private const string ChatNome = "Modello chat presente";
    private const string EmbeddingNome = "Modello embedding presente";
    private const string DimensioneNome = "Dimensione embedding";
    private const string PosizionamentoNome = "Posizionamento modelli";

    public async Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken)
    {
        using OllamaApiClient ollama = OllamaClients.Create(options.OllamaEndpoint, OllamaClients.ProbeTimeout);

        ProbeResult raggiungibile = await ProbeResult.MeasureAsync(5, RaggiungibileNome, async () =>
        {
            string version = await ollama.GetVersionAsync(cancellationToken);

            return (ProbeStatus.Ok, $"{options.OllamaEndpoint} (versione {version})");
        });

        if (raggiungibile.Stato == ProbeStatus.Error)
        {
            const string motivo = "Ollama non raggiungibile";

            return
            [
                raggiungibile,
                ProbeResult.Skipped(6, ChatNome, motivo),
                ProbeResult.Skipped(7, EmbeddingNome, motivo),
                ProbeResult.Skipped(8, DimensioneNome, motivo),
                ProbeResult.Skipped(10, PosizionamentoNome, motivo)
            ];
        }

        IReadOnlyList<Model> installed = [.. await ollama.ListLocalModelsAsync(cancellationToken)];
        ProbeResult embeddingPresente = await ProbeResult.MeasureAsync(7, EmbeddingNome, () => CheckEmbeddingModelAsync(installed, cancellationToken));

        return
        [
            raggiungibile,
            await ProbeResult.MeasureAsync(6, ChatNome, () => Task.FromResult(CheckInstalled(installed, options.OllamaChatModel))),
            embeddingPresente,
            embeddingPresente.Stato == ProbeStatus.Error
                ? ProbeResult.Skipped(8, DimensioneNome, "modello di embedding non disponibile")
                : await ProbeResult.MeasureAsync(8, DimensioneNome, () => CheckDimensionsAsync(cancellationToken)),
            await ProbeResult.MeasureAsync(10, PosizionamentoNome, () => CheckPlacementAsync(ollama, cancellationToken))
        ];
    }

    private async Task<(ProbeStatus, string)> CheckEmbeddingModelAsync(IReadOnlyList<Model> installed, CancellationToken cancellationToken) =>
        options.EmbeddingProvider switch
        {
            EmbeddingProviders.Ollama => CheckInstalled(installed, options.EmbeddingModel),
            EmbeddingProviders.OpenAiCompatible => await CheckServedModelAsync(cancellationToken),
            _ => (ProbeStatus.Error, $"{SinistriOptions.Keys.EmbeddingProvider}={options.EmbeddingProvider}: non ancora disponibile nell'applicazione")
        };

    /// <summary>
    /// Server raggiungibile (<c>GET {endpoint}/models</c>). Il modello non si cerca nell'elenco: FastFlowLM vi mostra il catalogo
    /// dei modelli di chat e non quello di embedding caricato con <c>--embed 1</c>. La prova reale è il controllo 8.
    /// </summary>
    private async Task<(ProbeStatus, string)> CheckServedModelAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = OllamaClients.ProbeTimeout };
        Uri models = new(options.EmbeddingEndpoint.AbsoluteUri.TrimEnd('/') + "/models");

        using HttpResponseMessage response = await http.GetAsync(models, cancellationToken);

        return response.IsSuccessStatusCode
            ? (ProbeStatus.Ok, $"server {options.EmbeddingEndpoint} raggiungibile; '{options.EmbeddingModel}' verificato dal controllo 8")
            : (ProbeStatus.Error, $"{models} ha risposto {(int)response.StatusCode}");
    }

    private static (ProbeStatus, string) CheckInstalled(IReadOnlyList<Model> installed, string model) =>
        installed.FirstOrDefault(m => OllamaClients.SameModel(m.Name, model)) is { } found
            ? (ProbeStatus.Ok, $"{found.Name} ({found.Size / 1_000_000_000.0:0.0} GB)")
            : (ProbeStatus.Error, $"'{model}' non installato: eseguire ollama pull {model}");

    private async Task<(ProbeStatus, string)> CheckDimensionsAsync(CancellationToken cancellationToken)
    {
        using IEmbeddingGenerator<string, Embedding<float>> generator = embeddingFactory.Create();

        Embedding<float> embedding = await generator.GenerateAsync("prova", cancellationToken: cancellationToken);
        int measured = embedding.Vector.Length;

        return measured == options.EmbeddingDimensions
            ? (ProbeStatus.Ok, $"{measured} = {SinistriOptions.Keys.EmbeddingDimensions}")
            : (ProbeStatus.Error,
                $"il modello '{options.EmbeddingModel}' restituisce vettori da {measured} ma {SinistriOptions.Keys.EmbeddingDimensions}={options.EmbeddingDimensions}: " +
                "allineare la configurazione al modello");
    }

    private async Task<(ProbeStatus, string)> CheckPlacementAsync(OllamaApiClient ollama, CancellationToken cancellationToken)
    {
        IReadOnlyList<RunningModel> running = [.. await ollama.ListRunningModelsAsync(cancellationToken)];
        RunningModel? chat = running.FirstOrDefault(m => OllamaClients.SameModel(m.Name, options.OllamaChatModel));
        RunningModel? embedding = options.EmbeddingProvider == EmbeddingProviders.Ollama
            ? running.FirstOrDefault(m => OllamaClients.SameModel(m.Name, options.EmbeddingModel))
            : null;

        string chatText = chat is null ? "chat non caricata" : $"chat {Placement(chat)}";
        string embeddingText = options.EmbeddingProvider != EmbeddingProviders.Ollama
            ? $"embedding su {options.EmbeddingProvider}"
            : embedding is null ? "embedding non caricato" : $"embedding {Placement(embedding)}";

        // D18: un embedding in VRAM può costringere Ollama a scaricare la chat durante la demo.
        bool embeddingInVram = embedding is not null && embedding.SizeVram > 0;
        bool chatOffGpu = chat is not null && chat.SizeVram < chat.Size;

        ProbeStatus stato = embeddingInVram || chatOffGpu ? ProbeStatus.Warning : ProbeStatus.Info;

        return (stato, $"{chatText}; {embeddingText}");
    }

    private static string Placement(RunningModel model)
    {
        if (model.Size <= 0 || model.SizeVram <= 0)
        {
            return "100% CPU";
        }

        long gpuPercent = model.SizeVram * 100 / model.Size;

        return gpuPercent >= 100 ? "100% GPU" : $"{gpuPercent}% GPU / {100 - gpuPercent}% CPU";
    }
}
