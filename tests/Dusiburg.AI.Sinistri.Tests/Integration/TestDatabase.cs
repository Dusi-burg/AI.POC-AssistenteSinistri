using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// Database di test su LocalDB (D13): <c>Sinistri_Test</c> con vettori a 4 dimensioni scritti a mano, niente Ollama.
/// Ricreato una volta per l'esecuzione dei test di questo namespace.
/// </summary>
[SetUpFixture]
public sealed class TestDatabase
{
    public const int Dimensions = 4;

    public const string ConnectionString =
        @"Server=(localdb)\localdev;Database=Sinistri_Test;Integrated Security=True;TrustServerCertificate=True";

    [OneTimeSetUp]
    public async Task RecreateAsync() => await DatabaseInitializer.RecreateAsync(ConnectionString, Dimensions, CancellationToken.None);

    public static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        return connection;
    }
}
