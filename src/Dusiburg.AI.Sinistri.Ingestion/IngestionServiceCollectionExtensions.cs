using Dusiburg.AI.Sinistri.Ingestion.Embedding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>Pipeline di embedding (Fase 4): solo Cli. Il generatore dei dati sintetici (Fase 3) lo usa DbInit direttamente.</summary>
    public static IServiceCollection AddSinistriIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<EmbeddingPipeline>();

        return services;
    }
}
