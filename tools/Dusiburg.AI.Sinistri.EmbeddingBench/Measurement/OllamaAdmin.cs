using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

/// <summary>Chiamate di servizio a Ollama per il banco: modelli caricati, scaricamento, generazione di riferimento della chat.</summary>
internal sealed class OllamaAdmin : IDisposable
{
    public const string ChatModel = "qwen3.5:9b";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(BenchCandidates.OllamaEndpoint + "/"), Timeout = TimeSpan.FromMinutes(10) };

    public async Task<string> VersionAsync(CancellationToken cancellationToken)
    {
        JsonElement json = await _http.GetFromJsonAsync<JsonElement>("api/version", cancellationToken);

        return json.GetProperty("version").GetString() ?? "?";
    }

    public async Task<bool> IsInstalledAsync(string model, CancellationToken cancellationToken)
    {
        JsonElement json = await _http.GetFromJsonAsync<JsonElement>("api/tags", cancellationToken);

        return json.GetProperty("models").EnumerateArray().Any(m => SameModel(m.GetProperty("name").GetString(), model));
    }

    /// <summary>Riga per modello caricato: nome, dimensione, quota in VRAM (come <c>ollama ps</c>).</summary>
    public async Task<IReadOnlyList<(string Name, long Size, long SizeVram)>> RunningAsync(CancellationToken cancellationToken)
    {
        JsonElement json = await _http.GetFromJsonAsync<JsonElement>("api/ps", cancellationToken);

        return
        [
            .. json.GetProperty("models").EnumerateArray().Select(m =>
                (m.GetProperty("name").GetString() ?? "?", m.GetProperty("size").GetInt64(), m.GetProperty("size_vram").GetInt64()))
        ];
    }

    public async Task<string> PlacementAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<(string Name, long Size, long SizeVram)> running = await RunningAsync(cancellationToken);

        return running.Count == 0
            ? "nessun modello caricato"
            : string.Join("; ", running.Select(m => $"{m.Name} {(m.SizeVram == 0 ? "100% CPU" : $"{m.SizeVram * 100 / m.Size}% GPU")}"));
    }

    /// <summary>Scarica il modello dalla memoria, per misurare la prima richiesta a freddo.</summary>
    public static async Task StopAsync(string model, CancellationToken cancellationToken)
    {
        using Process process = Process.Start(new ProcessStartInfo("ollama", ["stop", model]) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("ollama non avviabile.");

        await process.WaitForExitAsync(cancellationToken);
    }

    /// <summary>Generazione di riferimento della chat sulla GPU: token al secondo e tempo di caricamento del modello.</summary>
    public async Task<(double TokensPerSecond, double LoadSeconds)> GenerateAsync(CancellationToken cancellationToken)
    {
        var request = new
        {
            model = ChatModel,
            prompt = "Descrivi in 200 parole come si istruisce un sinistro da acqua condotta in un condominio.",
            stream = false,
            think = false,
            options = new { num_predict = 300, temperature = 0.1, num_ctx = 8192 }
        };

        using HttpResponseMessage response = await _http.PostAsJsonAsync("api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        double tokens = json.GetProperty("eval_count").GetDouble();
        double evalSeconds = json.GetProperty("eval_duration").GetDouble() / 1e9;

        return (tokens / evalSeconds, json.GetProperty("load_duration").GetDouble() / 1e9);
    }

    public void Dispose() => _http.Dispose();

    private static bool SameModel(string? installed, string configured) =>
        string.Equals(WithTag(installed ?? ""), WithTag(configured), StringComparison.OrdinalIgnoreCase);

    private static string WithTag(string model) => model.Contains(':', StringComparison.Ordinal) ? model : model + ":latest";
}
