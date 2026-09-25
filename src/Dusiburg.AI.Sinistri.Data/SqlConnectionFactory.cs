using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data;

/// <summary>Connessioni al database <c>Sinistri</c> dalla connection string <c>sql</c> (D1), negli user-secrets dell'AppHost.</summary>
public sealed class SqlConnectionFactory(string? connectionString)
{
    public const string ConnectionStringName = "sql";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(connectionString);

    /// <summary>Server e database di destinazione, per messaggi e controlli (mai la connection string completa).</summary>
    public (string Server, string Database) Target
    {
        get
        {
            SqlConnectionStringBuilder builder = Builder();

            return (builder.DataSource, builder.InitialCatalog);
        }
    }

    public SqlConnection CreateConnection() => new(Builder().ConnectionString);

    /// <summary>Stesso server, database <c>master</c>: serve a controllare il server anche prima che esista il database applicativo.</summary>
    public SqlConnection CreateMasterConnection()
    {
        SqlConnectionStringBuilder builder = Builder();
        builder.InitialCatalog = "master";

        return new SqlConnection(builder.ConnectionString);
    }

    private SqlConnectionStringBuilder Builder() => IsConfigured
        ? new SqlConnectionStringBuilder(connectionString)
        : throw new InvalidOperationException(
            $"ConnectionStrings:{ConnectionStringName} non configurata: impostarla negli user-secrets dell'AppHost " +
            "(dotnet user-secrets set ConnectionStrings:sql \"...\" --project src/Dusiburg.AI.Sinistri.AppHost).");
}
