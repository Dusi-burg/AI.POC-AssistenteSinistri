using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data.PreIstruttoria;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Connessioni al database e probe SQL; i repository arrivano dalla Fase 2.</summary>
    public static IServiceCollection AddSinistriData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new SqlConnectionFactory(configuration.GetConnectionString(SqlConnectionFactory.ConnectionStringName)));
        services.AddSingleton<IHealthProbe, SqlHealthProbe>();
        services.AddSingleton<EmbeddingRepository>();
        services.AddSingleton<IClausolaRepository, ClausolaRepository>();
        services.AddSingleton<ISinistroRepository, SinistroRepository>();
        services.AddSingleton<IPolizzaRepository, PolizzaRepository>();

        return services;
    }
}
