using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Cli.Configuration;

/// <summary>
/// CLI lanciata fuori dall'AppHost in Development (D12): connection string e manopole restano in un solo posto, gli
/// user-secrets dell'AppHost. Si leggono senza mai sovrascrivere valori già presenti (variabili d'ambiente, appsettings),
/// più il collegamento OTLP al dashboard se l'AppHost è in esecuzione.
/// </summary>
internal static class AppHostSecrets
{
    /// <summary><c>UserSecretsId</c> di <c>Dusiburg.AI.Sinistri.AppHost.csproj</c>.</summary>
    public const string AppHostUserSecretsId = "fdf26212-2348-4c81-a6b5-de897910daf9";

    /// <summary>Endpoint OTLP del dashboard da usare dalla CLI (default in appsettings.Development.json).</summary>
    public const string CliOtlpEndpointKey = "SINISTRI_CLI_OTLP_ENDPOINT";

    private const string SqlConnectionStringKey = "ConnectionStrings:sql";

    public static void AddAppHostSecretsForCli(this IConfigurationManager configuration)
    {
        IConfigurationRoot secrets = new ConfigurationBuilder().AddUserSecrets(AppHostUserSecretsId).Build();
        var values = new Dictionary<string, string?>();

        foreach (string key in SinistriOptions.Keys.All.Append(SqlConnectionStringKey))
        {
            if (string.IsNullOrEmpty(configuration[key]) && secrets[key] is { Length: > 0 } value)
            {
                values[key] = value;
            }
        }

        if (string.IsNullOrEmpty(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])
            && configuration[CliOtlpEndpointKey] is { Length: > 0 } endpoint
            && secrets["AppHost:OtlpApiKey"] is { Length: > 0 } otlpApiKey)
        {
            values["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
            values["OTEL_EXPORTER_OTLP_HEADERS"] = $"x-otlp-api-key={otlpApiKey}";
            values["OTEL_SERVICE_NAME"] = "sinistri-cli";
        }

        configuration.AddInMemoryCollection(values);
    }
}
