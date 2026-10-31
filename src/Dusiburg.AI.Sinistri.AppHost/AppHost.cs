var builder = DistributedApplication.CreateBuilder(args);

// Risorse esterne (D1, D15): nessun container. Valore negli user-secrets dell'AppHost:
// ConnectionStrings:sql = Server=(localdb)\localdev;Database=Sinistri;Integrated Security=True;TrustServerCertificate=True
var sql = builder.AddConnectionString("sql");

// Porte fisse dai launchSettings: Api 5201, Web 5202 (O2C usa 5101-5106, così le due demo possono convivere).
var api = builder.AddProject<Projects.Dusiburg_AI_Sinistri_Api>("api")
    .WithReference(sql)
    .WithHttpHealthCheck("/health");

// Manopole della demo (D12): si impostano sull'AppHost — riga di comando, user-secrets o variabili d'ambiente — e
// arrivano all'API; senza valore vale il default del codice (SinistriOptions). La CLI legge gli stessi user-secrets.
api.WithConfigurationEnvironment(
    builder,
    "OLLAMA_ENDPOINT",
    "OLLAMA_CHAT_MODEL",
    "OLLAMA_NUM_CTX",
    "OLLAMA_NUM_GPU",
    "EMBEDDING_PROVIDER",
    "EMBEDDING_ENDPOINT",
    "EMBEDDING_MODEL",
    "EMBEDDING_NUM_GPU",
    "EMBEDDING_ONNX_PATH",
    "EMBEDDING_DIMENSIONS",
    "SOGLIA_DUPLICATO_COSINE",
    "SOGLIA_DUPLICATO_DENUNCIA",
    "SOGLIA_DUPLICATO_POLIZZA",
    "SINISTRI_PROMPT_CAPTURE_DIR",
    "SINISTRI_WARMUP");

// Ollama non è un container: è quello installato sul PC. Se all'avvio non risponde, l'AppHost lo avvia come propria risorsa
// ("ollama serve") e lo ferma quando si chiude; se è già in esecuzione non lo tocca. Vale solo per l'indirizzo standard su questo
// PC: un endpoint remoto non si può avviare da qui, e un OLLAMA_ENDPOINT su un'altra porta è lo scenario "Ollama giù" della demo
// (docs/demo.md). Il default è quello di SinistriOptions.
var ollamaEndpoint = new Uri(builder.Configuration["OLLAMA_ENDPOINT"] is { Length: > 0 } configured ? configured : "http://127.0.0.1:11434");

if (builder.ExecutionContext.IsRunMode && ollamaEndpoint is { IsLoopback: true, Port: 11434 } && !AppHostExtensions.IsListening(ollamaEndpoint))
{
    string ollamaHost = $"{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";

    var ollama = builder.AddExecutable("ollama", "ollama", ".", "serve")
        .WithEnvironment("OLLAMA_HOST", ollamaHost);

    api.WaitFor(ollama);
}

var web =builder.AddProject<Projects.Dusiburg_AI_Sinistri_Web>("web")
    .WithReference(api)            // service discovery: la Web chiama http://api
    .WithHttpHealthCheck("/health");

// Link "apri la traccia nel dashboard" della scheda (fase-8.md §3.1): primo indirizzo del dashboard dai launchSettings dell'AppHost.
if (builder.Configuration["ASPNETCORE_URLS"]?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } dashboard)
{
    web.WithEnvironment("SINISTRI_DASHBOARD_URL", dashboard);
}

builder.Build().Run();

internal static class AppHostExtensions
{
    /// <summary>Qualcuno ascolta già a quell'indirizzo? Una prova di connessione breve, fatta una volta all'avvio.</summary>
    public static bool IsListening(Uri endpoint)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();

            return client.ConnectAsync(endpoint.Host, endpoint.Port).Wait(TimeSpan.FromSeconds(1)) && client.Connected;
        }
        catch (Exception exception) when (exception is AggregateException or System.Net.Sockets.SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Inoltra al servizio le impostazioni presenti nella configurazione dell'AppHost, saltando quelle non valorizzate:
    /// così una variabile della demo si imposta in un solo posto e non va replicata in ogni progetto.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithConfigurationEnvironment(
        this IResourceBuilder<ProjectResource> resource, IDistributedApplicationBuilder builder, params string[] names)
    {
        foreach (var name in names)
        {
            // Nelle variabili d'ambiente la sezione si scrive con il doppio underscore; nella configurazione con i due punti.
            if (builder.Configuration[name.Replace("__", ":", StringComparison.Ordinal)] is { Length: > 0 } value)
            {
                resource.WithEnvironment(name, value);
            }
        }

        return resource;
    }
}
