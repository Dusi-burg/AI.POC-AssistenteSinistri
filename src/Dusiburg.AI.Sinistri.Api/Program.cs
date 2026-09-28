using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Api.Endpoints;
using Dusiburg.AI.Sinistri.Api.Health;
using Dusiburg.AI.Sinistri.Api.Problemi;
using Dusiburg.AI.Sinistri.Api.Warmup;
using Dusiburg.AI.Sinistri.Core;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Manopole (D12) dall'AppHost, connection string "sql" (D1); il database lo crea tools/Dusiburg.AI.Sinistri.DbInit.
builder.Services.AddSinistriCore(builder.Configuration);
builder.Services.AddSinistriData(builder.Configuration);
builder.Services.AddSinistriAi(builder.Configuration);

builder.Services.AddSingleton<HealthReportCache>();
builder.Services.AddHealthChecks().AddCheck<SinistriHealthCheck>(SinistriHealthCheck.Name);

// JSON camelCase con enum come stringhe, uguale al client della Web (SinistriJson); errori come ProblemDetails (fase-8.md §2).
builder.Services.ConfigureHttpJsonOptions(options => SinistriJson.Configura(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SinistriExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHostedService<WarmupService>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapGet("/", () => new { service = "Dusiburg.AI.Sinistri.Api", phase = 8, openApi = "/openapi/v1.json" }).ExcludeFromDescription();

app.MapSistemaEndpoints();
app.MapPolizzaEndpoints();
app.MapPreIstruttoriaEndpoints();
app.MapRicercaEndpoints();
app.MapAntifrodeEndpoints();

app.LogConnectionStringPresence(SqlConnectionFactory.ConnectionStringName);

app.Run();

/// <summary>Tipo di riferimento dell'assembly per <c>WebApplicationFactory</c> nei test.</summary>
public sealed class ApiEntryPoint;
