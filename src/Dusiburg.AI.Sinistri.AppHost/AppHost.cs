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
    "EMBEDDING_PROVIDER",
    "EMBEDDING_ENDPOINT",
    "EMBEDDING_MODEL",
    "EMBEDDING_NUM_GPU",
    "EMBEDDING_ONNX_PATH",
    "EMBEDDING_DIMENSIONS",
    "SOGLIA_DUPLICATO_COSINE",
    "SINISTRI_PROMPT_CAPTURE_DIR",
    "SINISTRI_WARMUP");

builder.AddProject<Projects.Dusiburg_AI_Sinistri_Web>("web")
    .WithReference(api)            // service discovery: la Web chiama http://api
    .WithHttpHealthCheck("/health");

builder.Build().Run();

internal static class AppHostExtensions
{
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
