using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>
/// Coppie di quasi-duplicati inserite dal seed (<c>data/duplicati_attesi.json</c>): le usano il fraud-scan (valutazione) e lo storico
/// (gemello di un sinistro). Il file si cerca nella cartella <c>data/</c> della radice del repository, come fa la CLI: l'API gira sempre
/// dai sorgenti. Se manca, le funzioni che lo usano vanno avanti senza.
/// </summary>
internal static class CoppieAttese
{
    public static async Task<IReadOnlyList<CoppiaDuplicati>?> LeggiAsync(ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            string percorso = Path.Combine(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory), DuplicatiAttesiFile.NomeFile);

            return File.Exists(percorso) ? (await DuplicatiAttesiFile.LeggiAsync(percorso, cancellationToken)).Coppie : null;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Coppie attese non disponibili");

            return null;
        }
    }

    /// <summary>Numero del sinistro con cui <paramref name="numero"/> forma una coppia attesa, in entrambe le direzioni.</summary>
    public static string? Gemello(IReadOnlyList<CoppiaDuplicati>? coppie, string numero) =>
        coppie?.FirstOrDefault(c => c.Originale == numero)?.Duplicato ?? coppie?.FirstOrDefault(c => c.Duplicato == numero)?.Originale;
}
