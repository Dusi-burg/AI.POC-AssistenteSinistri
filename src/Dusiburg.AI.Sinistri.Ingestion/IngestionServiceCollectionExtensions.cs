using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>Generatore dei dati sintetici e pipeline di embedding: solo Cli e DbInit. Riempito nelle Fasi 3 e 4.</summary>
    public static IServiceCollection AddSinistriIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}
