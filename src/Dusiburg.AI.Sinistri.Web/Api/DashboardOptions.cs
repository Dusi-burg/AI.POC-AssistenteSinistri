namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>
/// Indirizzo del dashboard di Aspire, passato dall'AppHost (<c>SINISTRI_DASHBOARD_URL</c>): serve al link "apri la traccia nel
/// dashboard" della scheda. Senza valore (Web avviata da sola, test) il link non compare.
/// </summary>
public sealed class DashboardOptions
{
    public const string Key = "SINISTRI_DASHBOARD_URL";

    [Microsoft.Extensions.Configuration.ConfigurationKeyName(Key)]
    public string? Url { get; set; }
}
