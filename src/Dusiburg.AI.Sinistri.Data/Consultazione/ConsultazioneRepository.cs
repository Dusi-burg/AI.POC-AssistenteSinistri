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

    public async Task<IReadOnlyList<ClausolaDettaglio>> GetClausoleAsync(
        Prodotto? prodotto, TipoClausola? tipo, string? testo, CancellationToken cancellationToken)
    {
        string? like = string.IsNullOrWhiteSpace(testo) ? null : $"%{EscapeLike(testo.Trim())}%";
        await using SqlConnection connection = connectionFactory.CreateConnection();

        IEnumerable<ClausolaDettaglio> righe = await connection.QueryAsync<ClausolaDettaglio>(new CommandDefinition(
            """
            SELECT Id, ProdottoId AS Prodotto, Articolo, TipoClausolaId AS Tipo, Titolo, Testo
            FROM dbo.Clausola
            WHERE (@prodottoId IS NULL OR ProdottoId = @prodottoId)
              AND (@tipoId IS NULL OR TipoClausolaId = @tipoId)
              AND (@like IS NULL OR Articolo LIKE @like ESCAPE '\' OR Titolo LIKE @like ESCAPE '\' OR Testo LIKE @like ESCAPE '\')
            """,
            new { prodottoId = (byte?)prodotto, tipoId = (byte?)tipo, like },
            cancellationToken: cancellationToken));

        // 60 righe: l'ordine numerico degli articoli (2.10 dopo 2.9) si fa qui, non con l'ordinamento alfabetico di SQL.
        return [.. righe.OrderBy(c => c.Prodotto).ThenBy(c => OrdineArticoli.Chiave(c.Articolo))];
    }

    public async Task<Pagina<PolizzaElenco>> ElencaPolizzeAsync(
        string? cerca, bool soloDemo, int pagina, int dimensione, CancellationToken cancellationToken)
    {
        string? like = string.IsNullOrWhiteSpace(cerca) ? null : $"%{EscapeLike(cerca.Trim())}%";
        await using SqlConnection connection = connectionFactory.CreateConnection();

        IReadOnlyList<RigaPolizzaElenco> righe = [.. await connection.QueryAsync<RigaPolizzaElenco>(new CommandDefinition(
            """
            WITH Polizze AS (
                SELECT p.Id, p.Numero, p.ProdottoId AS Prodotto, c.Nominativo AS Contraente, c.Provincia, p.Decorrenza, p.Scadenza,
                       p.Massimale, p.Franchigia,
                       CAST(CASE WHEN p.Numero IN (SELECT value FROM OPENJSON(@demo)) THEN 1 ELSE 0 END AS bit) AS Demo
                FROM dbo.Polizza AS p
                JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
                WHERE (@like IS NULL OR p.Numero LIKE @like ESCAPE '\' OR c.Nominativo LIKE @like ESCAPE '\')
            )
            SELECT p.Numero, p.Prodotto, p.Contraente, p.Provincia, p.Decorrenza, p.Scadenza, p.Massimale, p.Franchigia, p.Demo,
                   (SELECT COUNT(*) FROM dbo.Sinistro AS s WHERE s.PolizzaId = p.Id) AS Sinistri,
                   COUNT(*) OVER () AS Totale
            FROM Polizze AS p
            WHERE @soloDemo = 0 OR p.Demo = 1
            ORDER BY p.Demo DESC, p.Numero
            OFFSET @salta ROWS FETCH NEXT @dimensione ROWS ONLY;
            """,
            new
            {
                demo = JsonSerializer.Serialize(DemoCatalog.Polizze.Select(p => p.Numero)),
                like,
                soloDemo,
                salta = (pagina - 1) * dimensione,
                dimensione
            },
            cancellationToken: cancellationToken))];

        return new Pagina<PolizzaElenco>(
            [.. righe.Select(r => new PolizzaElenco(r.Numero, r.Prodotto, r.Contraente, r.Provincia, DateOnly.FromDateTime(r.Decorrenza),
                DateOnly.FromDateTime(r.Scadenza), r.Massimale, r.Franchigia, r.Sinistri, r.Demo))],
            pagina, dimensione, righe.Count == 0 ? await ContaPolizzeAsync(connection, like, soloDemo, cancellationToken) : righe[0].Totale);
    }

    public async Task<Pagina<SinistroElenco>> ElencaSinistriAsync(
        FiltriElencoSinistri filtri, int pagina, int dimensione, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        // Filtri opzionali (@p IS NULL OR col = @p): con ~400 righe il piano non conta; il totale arriva con COUNT(*) OVER ().
        IReadOnlyList<RigaSinistroElenco> righe = [.. await connection.QueryAsync<RigaSinistroElenco>(new CommandDefinition(
            $"""
            SELECT s.Id, s.Numero, p.Numero AS NumeroPolizza, p.ProdottoId AS Prodotto, c.Nominativo AS Contraente, r.RagioneSociale AS Riparatore,
                   s.DataEvento, s.DataDenuncia, s.Provincia, s.CausaSinistroId AS Causa, s.StatoSinistroId AS Stato, s.ImportoLiquidato,
                   s.Descrizione, COUNT(*) OVER () AS Totale
            FROM dbo.Sinistro AS s
            JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
            JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
            LEFT JOIN dbo.Riparatore AS r ON r.Id = s.RiparatoreId
            {FiltriSinistri}
            ORDER BY s.DataDenuncia DESC, s.Id DESC
            OFFSET @salta ROWS FETCH NEXT @dimensione ROWS ONLY;
            """,
            ParametriSinistri(filtri, pagina, dimensione),
            cancellationToken: cancellationToken))];

        int totale = righe.Count > 0
            ? righe[0].Totale
            : await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                $"""
                SELECT COUNT(*)
                FROM dbo.Sinistro AS s
                JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
                {FiltriSinistri}
                """,
                ParametriSinistri(filtri, pagina, dimensione), cancellationToken: cancellationToken));

        return new Pagina<SinistroElenco>(
            [.. righe.Select(r => new SinistroElenco(r.Id, r.Numero, r.NumeroPolizza, r.Prodotto, r.Contraente, r.Riparatore,
                DateOnly.FromDateTime(r.DataEvento), DateOnly.FromDateTime(r.DataDenuncia), r.Provincia, r.Causa, r.Stato,
                r.ImportoLiquidato, r.Descrizione))],
            pagina, dimensione, totale);
    }

    /// <summary>Filtri comuni alla pagina e al conteggio (una pagina oltre l'ultima non ha righe da cui leggere il totale).</summary>
    private const string FiltriSinistri =
        """
        WHERE (@prodottoId IS NULL OR p.ProdottoId = @prodottoId)
          AND (@causaId IS NULL OR s.CausaSinistroId = @causaId)
          AND (@statoId IS NULL OR s.StatoSinistroId = @statoId)
          AND (@provincia IS NULL OR s.Provincia = @provincia)
          AND (@anno IS NULL OR (s.DataDenuncia >= DATEFROMPARTS(@anno, 1, 1) AND s.DataDenuncia < DATEFROMPARTS(@anno + 1, 1, 1)))
          AND (@riparatoreId IS NULL OR s.RiparatoreId = @riparatoreId)
          AND (@numeroPolizza IS NULL OR p.Numero = @numeroPolizza)
          AND (@like IS NULL OR s.Descrizione LIKE @like ESCAPE '\')
        """;

    private static object ParametriSinistri(FiltriElencoSinistri filtri, int pagina, int dimensione) => new
    {
        prodottoId = (byte?)filtri.Prodotto,
        causaId = (byte?)filtri.Causa,
        statoId = (byte?)filtri.Stato,
        provincia = new DbString
        {
            Value = string.IsNullOrWhiteSpace(filtri.Provincia) ? null : filtri.Provincia.Trim().ToUpperInvariant(),
            IsAnsi = true, IsFixedLength = true, Length = 2
        },
        anno = filtri.AnnoDenuncia,
        riparatoreId = filtri.RiparatoreId,
        numeroPolizza = new DbString { Value = string.IsNullOrWhiteSpace(filtri.NumeroPolizza) ? null : filtri.NumeroPolizza.Trim(), IsAnsi = true, Length = 30 },
        like = string.IsNullOrWhiteSpace(filtri.Testo) ? null : $"%{EscapeLike(filtri.Testo.Trim())}%",
        salta = (pagina - 1) * dimensione,
        dimensione
    };

    private static async Task<int> ContaPolizzeAsync(SqlConnection connection, string? like, bool soloDemo, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM dbo.Polizza AS p
            JOIN dbo.Contraente AS c ON c.Id = p.ContraenteId
            WHERE (@like IS NULL OR p.Numero LIKE @like ESCAPE '\' OR c.Nominativo LIKE @like ESCAPE '\')
              AND (@soloDemo = 0 OR p.Numero IN (SELECT value FROM OPENJSON(@demo)))
            """,
            new { like, soloDemo, demo = JsonSerializer.Serialize(DemoCatalog.Polizze.Select(p => p.Numero)) },
            cancellationToken: cancellationToken));

    /// <summary>Caratteri jolly di <c>LIKE</c> presi alla lettera (con <c>ESCAPE '\'</c>).</summary>
    internal static string EscapeLike(string testo) =>
        testo.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");

    private sealed record RigaPolizza(
        string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateTime Decorrenza, DateTime Scadenza, bool Demo);

    private sealed record RigaPolizzaElenco(
        string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateTime Decorrenza, DateTime Scadenza,
        decimal Massimale, decimal Franchigia, bool Demo, int Sinistri, int Totale);

    private sealed record RigaSinistroElenco(
        int Id, string Numero, string NumeroPolizza, Prodotto Prodotto, string Contraente, string? Riparatore, DateTime DataEvento,
        DateTime DataDenuncia, string Provincia, CausaSinistro Causa, StatoSinistro Stato, decimal? ImportoLiquidato, string Descrizione,
        int Totale);

    private sealed record RigaSinistro(
        string Numero, string NumeroPolizza, Prodotto Prodotto, string Contraente, string? Riparatore, DateTime DataEvento,
        DateTime DataDenuncia, string Provincia, CausaSinistro Causa, string Descrizione, string? EsitoPerizia, StatoSinistro Stato,
        decimal? ImportoRiservato, decimal? ImportoLiquidato);
}
