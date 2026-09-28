using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.PreIstruttoria;

/// <summary>
/// Polizza e contraente per <c>Numero</c>: seek su <c>UQ_Polizza_Numero</c> (parametro ANSI della stessa lunghezza della colonna
/// <c>VARCHAR(30)</c>, niente conversione implicita) e join per PK su <c>Contraente</c>.
/// </summary>
public sealed class PolizzaRepository(SqlConnectionFactory connectionFactory) : IPolizzaRepository
{
    public async Task<DatiPolizza?> GetByNumeroAsync(string numero, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        Riga? riga = await connection.QuerySingleOrDefaultAsync<Riga>(new CommandDefinition(
            """
            SELECT p.Numero, p.ProdottoId AS Prodotto, c.Nominativo AS Contraente, c.Provincia,
                   p.Decorrenza, p.Scadenza, p.Massimale, p.Franchigia
            FROM dbo.Polizza AS p
            JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
            WHERE p.Numero = @numero
            """,
            new { numero = new DbString { Value = numero.Trim(), IsAnsi = true, Length = 30 } },
            cancellationToken: cancellationToken));

        return riga is null
            ? null
            : new DatiPolizza(riga.Numero, riga.Prodotto, riga.Contraente, riga.Provincia,
                DateOnly.FromDateTime(riga.Decorrenza), DateOnly.FromDateTime(riga.Scadenza), riga.Massimale, riga.Franchigia);
    }

    private sealed record Riga(
        string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateTime Decorrenza, DateTime Scadenza, decimal Massimale, decimal Franchigia);
}
