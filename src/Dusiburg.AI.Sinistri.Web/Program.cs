using System.Text.Encodings.Web;
using System.Text.Unicode;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Web.Api;
using Dusiburg.AI.Sinistri.Web.PreIstruttoria;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// API tramite service discovery (AppHost: WithReference(api)). Due client (fase-8.md §1):
// - letture e ricerche: brevi e idempotenti, con il resilience handler standard di ServiceDefaults;
// - pre-istruttoria e fraud-scan: niente resilience handler (taglierebbe il tentativo dopo pochi secondi e ripeterebbe una
//   generazione da 15-40 s), timeout del client a 5 minuti.
string BaseAddress(IServiceProvider services) =>
    services.GetRequiredService<IConfiguration>()[SinistriApiClient.BaseAddressConfigurationKey] ?? SinistriApiClient.DefaultBaseAddress;

builder.Services.AddHttpClient<SinistriApiClient>((services, client) => client.BaseAddress = new Uri(BaseAddress(services)));
// RemoveAllResilienceHandlers è marcata sperimentale (Microsoft.Extensions.Http.Resilience 10.10): l'alternativa sarebbe
// riconfigurare per nome la pipeline "<client>-standard" (retry spenti, timeout e circuit breaker allungati insieme), più fragile.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<PreIstruttoriaApiClient>((services, client) =>
    {
        client.BaseAddress = new Uri(BaseAddress(services));
        client.Timeout = PreIstruttoriaApiClient.Timeout;
    })
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EsitiRecenti>();
builder.Services.Configure<DashboardOptions>(builder.Configuration);

// JSON dello stream verso il browser e del corpo inviato dallo script: stesse regole dell'API (enum come stringhe).
builder.Services.ConfigureHttpJsonOptions(options => SinistriJson.Configura(options.SerializerOptions));

// Testi in italiano: nelle pagine le lettere accentate restano leggibili invece di diventare entità numeriche.
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapPreIstruttoriaStream();

app.Run();

/// <summary>Tipo di riferimento dell'assembly per <c>WebApplicationFactory</c> nei test.</summary>
public sealed class WebEntryPoint;
