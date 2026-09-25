using System.Text.Encodings.Web;
using System.Text.Unicode;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// API tramite service discovery (AppHost: WithReference(api)).
builder.Services.AddHttpClient<SinistriApiClient>((services, client) =>
    client.BaseAddress = new Uri(
        services.GetRequiredService<IConfiguration>()[SinistriApiClient.BaseAddressConfigurationKey] ?? SinistriApiClient.DefaultBaseAddress));

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

app.Run();

/// <summary>Tipo di riferimento dell'assembly per <c>WebApplicationFactory</c> nei test.</summary>
public sealed class WebEntryPoint;
