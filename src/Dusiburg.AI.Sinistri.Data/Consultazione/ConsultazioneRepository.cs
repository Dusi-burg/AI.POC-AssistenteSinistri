using System.Text.Json;
using Dapper;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Consultazione;

/// <summary>
/// Letture delle pagine della demo (fase-8.md §2). Clausola per PK, sinistro per <c>UQ_Sinistro_Numero</c>; la ricerca delle polizze
/// è un <c>LIKE '%testo%'</c> su ~200 righe (scansione accettata: serve solo a riempire l'elenco del form).
/// </summary>
public sealed class ConsultazioneRepository(SqlConnectionFactory connectionFactory) : IConsultazioneRepository
{
    public async Task<IReadOnlyList<PolizzaVoce>> CercaPolizzeAsync(string? cerca, int top, CancellationToken cancellationToken)
    {
        string? testo = string.IsNullOrWhiteSpace(cerca) ? null : cerca.Trim();
        await using SqlConnection connection = connectionFactory.CreateConnection();

        IEnumerable<RigaPolizza> righe = await connection.QueryAsync<RigaPolizza>(new CommandDefinition(
            """
            SELECT TOP (@top)
                   p.Numero, p.ProdottoId AS Prodotto, c.Nominativo AS Contraente, c.Provincia, p.Decorrenza, p.Scadenza,
                   CAST(CASE WHEN p.Numero IN (SELECT value FROM OPENJSON(@demo)) THEN 1 ELSE 0 END AS bit) AS Demo
            FROM dbo.Polizza AS p
            JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
            WHERE @like IS NULL OR p.Numero LIKE @like ESCAPE '\' OR c.Nominativo LIKE @like ESCAPE '\'
            ORDER BY Demo DESC, p.Numero;
            """,
            new
            {
                top,
                demo = JsonSerializer.Serialize(DemoCatalog.Polizze.Select(p => p.Numero)),
                like = testo is null ? null : $"%{EscapeLike(testo)}%"
            },
            cancellationToken: cancellationToken));

        return [.. righe.Select(r => new PolizzaVoce(
            r.Numero, r.Prodotto, r.Contraente, r.Provincia, DateOnly.FromDateTime(r.Decorrenza), DateOnly.FromDateTime(r.Scadenza), r.Demo))];
    }

    public async Task<IReadOnlyList<RiparatoreVoce>> GetRiparatoriAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        return [.. await connection.QueryAsync<RiparatoreVoce>(new CommandDefinition(
            "SELECT Id, RagioneSociale FROM dbo.Riparatore ORDER BY RagioneSociale", cancellationToken: cancellationToken))];
    }

    public async Task<ClausolaDettaglio?> GetClausolaAsync(int id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<ClausolaDettaglio>(new CommandDefinition(
            """
            SELECT Id, ProdottoId AS Prodotto, Articolo, TipoClausolaId AS Tipo, Titolo, Testo
            FROM dbo.Clausola
            WHERE Id = @id
            """,
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<SinistroDettaglio?> GetSinistroAsync(string numero, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        RigaSinistro? riga = await connection.QuerySingleOrDefaultAsync<RigaSinistro>(new CommandDefinition(
            """
            SELECT s.Numero, p.Numero AS NumeroPolizza, p.ProdottoId AS Prodotto, c.Nominativo AS Contraente, r.RagioneSociale AS Riparatore,
                   s.DataEvento, s.DataDenuncia, s.Provincia, s.CausaSinistroId AS Causa, s.Descrizione, s.EsitoPerizia,
                   s.StatoSinistroId AS Stato, s.ImportoRiservato, s.ImportoLiquidato
            FROM dbo.Sinistro AS s
            JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
            JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
            LEFT JOIN dbo.Riparatore AS r ON r.Id = s.RiparatoreId
            WHERE s.Numero = @numero
            """,
            new { numero = new DbString { Value = numero.Trim(), IsAnsi = true, Length = 30 } },
            cancellationToken: cancellationToken));

        return riga is null
            ? null
            : new SinistroDettaglio(riga.Numero, riga.NumeroPolizza, riga.Prodotto, riga.Contraente, riga.Riparatore,
                DateOnly.FromDateTime(riga.DataEvento), DateOnly.FromDateTime(riga.DataDenuncia), riga.Provincia, riga.Causa,
                riga.Descrizione, riga.EsitoPerizia, riga.Stato, riga.ImportoRiservato, riga.ImportoLiquidato);
    }

    /// <summary>Gli stessi conteggi del riepilogo di DbInit, più il modello degli embedding salvati.</summary>
    public async Task<StatisticheDataset> GetStatisticheDatasetAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return new StatisticheDataset(
            await SeedRepository.ReadConteggioAsync(connection, cancellationToken),
            await SeedRepository.ReadCausaStatoAsync(connection, cancellationToken),
            await DatabaseInitializer.ReadEmbeddingInfoAsync(connection, cancellationToken));
    }

    /// <summary>Caratteri jolly di <c>LIKE</c> presi alla lettera (con <c>ESCAPE '\'</c>).</summary>
    internal static string EscapeLike(string testo) =>
        testo.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");

    private sealed record RigaPolizza(
        string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateTime Decorrenza, DateTime Scadenza, bool Demo);

    private sealed record RigaSinistro(
        string Numero, string NumeroPolizza, Prodotto Prodotto, string Contraente, string? Riparatore, DateTime DataEvento,
        DateTime DataDenuncia, string Provincia, CausaSinistro Causa, string Descrizione, string? EsitoPerizia, StatoSinistro Stato,
        decimal? ImportoRiservato, decimal? ImportoLiquidato);
}
