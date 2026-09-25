using Dapper;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// Dati della Fase 5 su <c>Sinistri_Test</c> con vettori a 4 dimensioni scritti a mano (D13). Con la denuncia <see cref="Denuncia"/>
/// le distanze coseno sono note a priori: [1,0,0,0] → 0; [1,0.1,0,0] → 0,005; [1,0.2,0,0] → 0,019; [1,0.3,0,0] → 0,042;
/// [1,0.4,0,0] → 0,072; [1,1,0,0] → 0,293; vettori ortogonali → 1.
/// </summary>
internal static class DatiRetrieval
{
    public static readonly float[] Denuncia = [1f, 0f, 0f, 0f];

    public static async Task RicreaAsync(CancellationToken cancellationToken)
    {
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, cancellationToken);
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.Clausola (ProdottoId, Articolo, TipoClausolaId, Titolo, Testo, Embedding) VALUES
                (1, N'Art. 2.1', 2, N'Garanzia vicinissima', N'-', CAST('[1,0,0,0]' AS VECTOR(4))),
                (1, N'Art. 2.2', 2, N'Garanzia vicina', N'-', CAST('[1,0.1,0,0]' AS VECTOR(4))),
                (1, N'Art. 1.1', 1, N'Definizione vicina', N'-', CAST('[1,0.3,0,0]' AS VECTOR(4))),
                (1, N'Art. 3.1', 3, N'Esclusione a 0,293', N'-', CAST('[1,1,0,0]' AS VECTOR(4))),
                (1, N'Art. 3.2', 3, N'Esclusione ortogonale', N'-', CAST('[0,0,1,0]' AS VECTOR(4))),
                (1, N'Art. 4.1', 4, N'Franchigia ortogonale', N'-', CAST('[0,1,0,0]' AS VECTOR(4))),
                (1, N'Art. 4.2', 4, N'Franchigia senza embedding', N'-', NULL),
                (2, N'Art. 2.1', 2, N'Garanzia RC identica alla denuncia', N'-', CAST('[1,0,0,0]' AS VECTOR(4)));

            INSERT INTO dbo.Contraente (Nominativo, Provincia) VALUES (N'Contraente di prova', 'MI');

            INSERT INTO dbo.Polizza (Numero, ProdottoId, ContraenteId, Decorrenza, Scadenza, Massimale, Franchigia) VALUES
                ('CF-TEST-000001', 1, 1, DATEADD(YEAR, -10, CAST(GETDATE() AS date)), DATEADD(YEAR, 1, CAST(GETDATE() AS date)), 300000, 250),
                ('RP-TEST-000001', 2, 1, DATEADD(YEAR, -10, CAST(GETDATE() AS date)), DATEADD(YEAR, 1, CAST(GETDATE() AS date)), 1000000, 2500);

            -- Stato: 1 aperto, 2 chiuso, 3 respinto. Causa: 1 acqua condotta, 3 fenomeno elettrico, 20 errore progettuale.
            INSERT INTO dbo.Sinistro (Numero, PolizzaId, DataEvento, DataDenuncia, Provincia, CausaSinistroId, Descrizione, StatoSinistroId, ImportoLiquidato, Embedding)
            SELECT Numero, PolizzaId, DATEADD(YEAR, -Anni, CAST(GETDATE() AS date)), DATEADD(YEAR, -Anni, CAST(GETDATE() AS date)),
                   Provincia, Causa, N'-', Stato, Liquidato, CAST(Vettore AS VECTOR(4))
            FROM (VALUES
                ('S-1', 1, 1, 'MI', 3, 2, 6000, '[1,0,0,0]'),
                ('S-2', 1, 1, 'BO', 3, 2, 3000, '[1,0.2,0,0]'),
                ('S-3', 1, 1, 'MI', 1, 3, NULL, '[1,0.4,0,0]'),
                ('S-4', 1, 0, 'MI', 3, 1, NULL, '[1,0,0,0]'),
                ('S-5', 1, 7, 'MI', 3, 2, 8000, '[1,0.3,0,0]'),
                ('S-6', 2, 1, 'VR', 20, 2, 20000, '[1,0,0,0]'),
                ('S-7', 1, 1, 'MI', 3, 2, 5000, NULL)
            ) AS v (Numero, PolizzaId, Anni, Provincia, Causa, Stato, Liquidato, Vettore);
            """);
    }

    public static async Task<int[]> IdAsync(params string[] numeri)
    {
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        Dictionary<string, int> ids = (await connection.QueryAsync<(string Numero, int Id)>("SELECT Numero, Id FROM dbo.Sinistro"))
            .ToDictionary(r => r.Numero, r => r.Id);

        return [.. numeri.Select(n => ids[n])];
    }
}
