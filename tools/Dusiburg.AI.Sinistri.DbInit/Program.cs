using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Uso: dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit [-- "<connection string>"] [--no-seed] [--allow-non-local]
// Senza connection string usa ConnectionStrings__sql dall'ambiente o, in mancanza, (localdb)\localdev database Sinistri.
// La dimensione dei vettori è EMBEDDING_DIMENSIONS dall'ambiente, altrimenti il default di SinistriOptions (D12).
const string AllowNonLocal = "--allow-non-local";
const string NoSeed = "--no-seed";

string connectionString = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__sql")
    ?? DatabaseInitializer.LocalConnectionString;

var target = new SqlConnectionStringBuilder(connectionString);

// Il tool cancella il database: fuori da LocalDB serve una conferma esplicita.
if (!target.DataSource.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase) && !args.Contains(AllowNonLocal))
{
    Console.Error.WriteLine($"Il server '{target.DataSource}' non è LocalDB: il database verrebbe cancellato. Aggiungi {AllowNonLocal} per procedere.");
    return 1;
}

SinistriOptions options;

try
{
    options = SinistriOptions.FromConfiguration(new ConfigurationBuilder().AddEnvironmentVariables().Build());
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

Console.WriteLine($"Ricreo il database '{target.InitialCatalog}' su '{target.DataSource}' con VECTOR({options.EmbeddingDimensions})...");

await DatabaseInitializer.RecreateAsync(connectionString, options.EmbeddingDimensions, CancellationToken.None);

// I dati sintetici (clausole, anagrafiche, sinistri) arrivano con la Fase 3.
bool seed = !args.Contains(NoSeed);

Console.WriteLine(seed
    ? "Fatto: schema creato, lookup popolate dagli enum. Il seed dei dati sintetici arriva con la Fase 3."
    : "Fatto: schema creato, lookup popolate dagli enum, nessun dato demo.");

return 0;
