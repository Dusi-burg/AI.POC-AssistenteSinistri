using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Api.Health;
using Dusiburg.AI.Sinistri.Core;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Manopole (D12) dall'AppHost, connection string "sql" (D1); il database lo crea tools/Dusiburg.AI.Sinistri.DbInit.
builder.Services.AddSinistriCore(builder.Configuration);
builder.Services.AddSinistriData(builder.Configuration);
builder.Services.AddSinistriAi(builder.Configuration);

builder.Services.AddSingleton<HealthReportCache>();
builder.Services.AddHealthChecks().AddCheck<SinistriHealthCheck>(SinistriHealthCheck.Name);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => new { service = "Dusiburg.AI.Sinistri.Api", phase = 1 });

// Esito dettagliato dei controlli, per la UI (Fase 8) e per la diagnostica durante la demo.
app.MapGet("/api/health", async (HealthReportCache cache, CancellationToken cancellationToken) =>
{
    ProbeReport report = await cache.GetAsync(cancellationToken);

    return Results.Ok(report);
});

app.LogConnectionStringPresence(SqlConnectionFactory.ConnectionStringName);

app.Run();

/// <summary>Tipo di riferimento dell'assembly per <c>WebApplicationFactory</c> nei test.</summary>
public sealed class ApiEntryPoint;
