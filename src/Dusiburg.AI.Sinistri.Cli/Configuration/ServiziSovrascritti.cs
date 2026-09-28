using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Ingestion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dusiburg.AI.Sinistri.Cli.Configuration;

/// <summary>
/// Servizi della CLI con alcune chiavi sovrascritte in memoria (database, manopole), senza toccare la configurazione: li usano
/// <c>eval --embedding-model</c> (DB per modello) e <c>bench-search</c> (DB del banco di prova).
/// </summary>
internal static class ServiziSovrascritti
{
    public static ServiceProvider Crea(
        IConfiguration configurazione, ILoggerFactory loggerFactory, string database, IReadOnlyDictionary<string, string?>? altre = null)
    {
        var connessione = new SqlConnectionStringBuilder(configurazione.GetConnectionString(SqlConnectionFactory.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName} non configurata."))
        {
            InitialCatalog = database
        };

        Dictionary<string, string?> chiavi = new(altre ?? new Dictionary<string, string?>())
        {
            [$"ConnectionStrings:{SqlConnectionFactory.ConnectionStringName}"] = connessione.ConnectionString
        };

        IConfiguration sovrascritta = new ConfigurationBuilder().AddConfiguration(configurazione).AddInMemoryCollection(chiavi).Build();

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddSinistriCore(sovrascritta);
        services.AddSinistriData(sovrascritta);
        services.AddSinistriAi(sovrascritta);
        services.AddSinistriIngestion(sovrascritta);

        return services.BuildServiceProvider();
    }
}
