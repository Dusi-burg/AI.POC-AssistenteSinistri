using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Ai;

/// <summary>
/// Diagnostica (D19, come in O2C): salva su file ogni richiesta al modello così come parte — istruzioni, messaggi, opzioni —
/// e la risposta ricevuta. Si attiva solo con <see cref="SinistriOptions.Keys.PromptCaptureDirectory"/>.
/// </summary>
public sealed class PromptCaptureChatClient(IChatClient inner, string directory) : DelegatingChatClient(inner)
{
    private static readonly JsonSerializerOptions Output = new(AIJsonUtilities.DefaultOptions) { WriteIndented = true };

    private static int _sequence;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];
        ChatResponse response = await base.GetResponseAsync(request, options, cancellationToken);

        await WriteAsync(request, options, response, cancellationToken);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];
        List<ChatResponseUpdate> updates = [];

        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(request, options, cancellationToken))
        {
            updates.Add(update);

            yield return update;
        }

        await WriteAsync(request, options, updates.ToChatResponse(), cancellationToken);
    }

    private async Task WriteAsync(IReadOnlyList<ChatMessage> request, ChatOptions? options, ChatResponse response, CancellationToken cancellationToken)
    {
        int sequence = Interlocked.Increment(ref _sequence);

        var capture = new JsonObject
        {
            ["sequence"] = sequence,
            ["capturedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["options"] = new JsonObject
            {
                ["modelId"] = options?.ModelId,
                ["temperature"] = options?.Temperature,
                ["responseFormat"] = options?.ResponseFormat?.GetType().Name,
                ["additionalProperties"] = options?.AdditionalProperties is { } extra
                    ? JsonSerializer.SerializeToNode(extra.ToDictionary(p => p.Key, p => p.Value?.ToString()), Output)
                    : null
            },
            ["instructions"] = options?.Instructions,
            ["messages"] = new JsonArray([.. request.Select(DescribeMessage)]),
            ["response"] = new JsonArray([.. response.Messages.Select(DescribeMessage)]),
            ["usage"] = JsonSerializer.SerializeToNode(response.Usage, Output)
        };

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{sequence:D3}.json");

        await File.WriteAllTextAsync(path, capture.ToJsonString(Output), cancellationToken);
    }

    private static JsonObject DescribeMessage(ChatMessage message) => new()
    {
        ["role"] = message.Role.Value,
        ["contents"] = new JsonArray([.. message.Contents.Select(DescribeContent)])
    };

    private static JsonObject DescribeContent(AIContent content) => content switch
    {
        TextContent text => new JsonObject { ["text"] = text.Text },
        _ => new JsonObject { ["content"] = content.GetType().Name }
    };
}
