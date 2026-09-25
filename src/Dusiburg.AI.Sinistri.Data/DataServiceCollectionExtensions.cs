using Dusiburg.AI.Sinistri.Core.Health;
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

        return services;
    }
}
