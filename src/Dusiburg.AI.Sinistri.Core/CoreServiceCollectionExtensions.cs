using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Opzioni e servizi applicativi; i probe e i repository li registrano Data e Ai.</summary>
    public static IServiceCollection AddSinistriCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Le manopole si leggono una volta: un valore non valido si scopre alla prima risoluzione, con il messaggio in italiano.
        services.AddSingleton(_ => SinistriOptions.FromConfiguration(configuration));

        services.AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection(RetrievalOptions.SectionName))
            .Validate(options => options.Validate().Count == 0, "Sezione Retrieval non valida: controllare i valori in appsettings.json.")
            .ValidateOnStart();

        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<HealthService>();
        services.AddSingleton<RicercaService>();
        services.AddSingleton<AntifrodeService>();

        return services;
    }
}
