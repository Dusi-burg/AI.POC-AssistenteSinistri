using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.DbInit;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Uso: dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit [-- "<connection string>"] [--no-seed] [--allow-non-local] [--parafrasa-con-llm]
// Senza connection string usa ConnectionStrings__sql dall'ambiente o, in mancanza, (localdb)\localdev database Sinistri.
// La dimensione dei vettori è EMBEDDING_DIMENSIONS dall'ambiente, altrimenti il default di SinistriOptions (D12).
// Il seed dei dati sintetici usa Seed__RandomSeed dall'ambiente, altrimenti SyntheticDataGenerator.DefaultRandomSeed.
const string AllowNonLocal = "--allow-non-local";
const string NoSeed = "--no-seed";
const string ParafrasaConLlm = "--parafrasa-con-llm";
const string RandomSeedKey = "Seed:RandomSeed";

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

IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
SinistriOptions options;
int randomSeed;

try
{
    options = SinistriOptions.FromConfiguration(configuration);
    randomSeed = configuration.GetValue(RandomSeedKey, SyntheticDataGenerator.DefaultRandomSeed);
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

Console.WriteLine($"Ricreo il database '{target.InitialCatalog}' su '{target.DataSource}' con VECTOR({options.EmbeddingDimensions})...");

await DatabaseInitializer.RecreateAsync(connectionString, options.EmbeddingDimensions, CancellationToken.None);

if (args.Contains(NoSeed))
{
    Console.WriteLine("Fatto: schema creato, lookup popolate dagli enum, nessun dato demo.");
    return 0;
}

// Passo 6 (fase-3.md): clausole, dati sintetici, duplicati attesi. Gli embedding restano un passo separato della CLI (Fase 4).
DateOnly oggi = DateOnly.FromDateTime(DateTime.Today);
DatiSintetici dati = SyntheticDataGenerator.Genera(randomSeed, oggi);
Console.WriteLine($"Dati sintetici generati con RandomSeed={randomSeed} e data di riferimento {oggi:yyyy-MM-dd}.");

if (args.Contains(ParafrasaConLlm))
{
    dati = await Parafrasi.ApplicaAsync(dati, options, CancellationToken.None);
}

await using (var connection = new SqlConnection(connectionString))
{
    await connection.OpenAsync();
    await SeedRepository.SeedClausoleAsync(connection, CancellationToken.None);
    await SeedRepository.InsertAsync(connection, dati, CancellationToken.None);

    string duplicati = await DuplicatiAttesiFile.ScriviAsync(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory), dati, CancellationToken.None);
    Console.WriteLine($"Coppie di quasi-duplicati attese: {dati.Coppie.Count}, scritte in {duplicati}.");

    await Riepilogo.StampaAsync(connection, target.InitialCatalog, options.EmbeddingDimensions, Console.Out, CancellationToken.None);
}

return 0;
