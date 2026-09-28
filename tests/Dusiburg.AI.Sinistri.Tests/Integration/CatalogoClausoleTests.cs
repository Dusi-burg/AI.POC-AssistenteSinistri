using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Consultazione;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// <c>docs/clausole.md</c> allineato alle clausole del seed (fase-9b.md §4): se si modificano le clausole in
/// <c>db/003_seed_clausole.sql</c> senza rigenerare il catalogo, questo test lo segnala.
/// </summary>
[Category("Integration")]
[NonParallelizable]
public class CatalogoClausoleTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task DocsAllineatoAlSeed()
    {
        //SETUP
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        await using (SqlConnection connection = await TestDatabase.OpenAsync())
        {
            await SeedRepository.SeedClausoleAsync(connection, CancellationToken);
        }

        string radice = Directory.GetParent(DuplicatiAttesiFile.CartellaDati(TestContext.CurrentContext.TestDirectory))!.FullName;
        string nelRepository = await File.ReadAllTextAsync(Path.Combine(radice, "docs", "clausole.md"), CancellationToken);

        //SUT
        IReadOnlyList<ClausolaDettaglio> clausole = await new ConsultazioneRepository(new SqlConnectionFactory(TestDatabase.ConnectionString))
            .GetClausoleAsync(null, null, null, CancellationToken);
        string generato = ClausoleMarkdownRenderer.Render(clausole, DateOnly.FromDateTime(DateTime.Today));

        Assert.That(ClausoleMarkdownRenderer.SenzaData(nelRepository), Is.EqualTo(ClausoleMarkdownRenderer.SenzaData(generato)),
            "docs/clausole.md non è allineato alle clausole del seed: eseguire " +
            "dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- export-clausole (dopo DbInit)");
    }
}
