using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Cli;
using Dusiburg.AI.Sinistri.Cli.Configuration;
using Dusiburg.AI.Sinistri.Core;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

// Gli argomenti non vanno alla configurazione: li interpreta System.CommandLine. appsettings accanto all'eseguibile,
// anche con "dotnet run --project" lanciato da un'altra cartella.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });

if (builder.Environment.IsDevelopment())
{
    // Prima di AddServiceDefaults: può impostare anche l'endpoint OTLP del dashboard per la CLI.
    builder.Configuration.AddAppHostSecretsForCli();
}

builder.AddServiceDefaults();

// La console resta leggibile per l'esito; i log completi vanno comunque al dashboard via OTLP.
LogLevel consoleLevel = args.Contains(SinistriCli.VerboseOption) ? LogLevel.Information : LogLevel.Warning;
builder.Logging.AddFilter<ConsoleLoggerProvider>(level => level >= consoleLevel);

builder.Services.AddSinistriCore(builder.Configuration);
builder.Services.AddSinistriData(builder.Configuration);
builder.Services.AddSinistriAi(builder.Configuration);
builder.Services.AddSinistriIngestion(builder.Configuration);

using IHost host = builder.Build();

try
{
    // Manopole (SinistriOptions) e sezione Retrieval validate prima dei comandi: messaggio in chiaro, senza stack trace.
    host.Services.GetRequiredService<SinistriOptions>();
    await host.StartAsync();
}
catch (OptionsValidationException exception)
{
    await Console.Error.WriteLineAsync($"Configurazione non valida: {string.Join(" ", exception.Failures)}");

    return SinistriCli.ExitError;
}
catch (InvalidOperationException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);

    return SinistriCli.ExitError;
}

try
{
    return await SinistriCli.InvokeAsync(args, host.Services);
}
finally
{
    await host.StopAsync();
}
