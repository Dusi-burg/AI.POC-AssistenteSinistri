using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Valutazione;

/// <summary><c>data/golden_set.json</c> (fase-9.md §1): denunce scritte a mano, diverse dagli scenari demo, con gli articoli attesi.</summary>
public sealed record GoldenSet(int Versione, IReadOnlyList<CasoGolden> Casi);

public sealed record CasoGolden(string Id, Prodotto Prodotto, string Denuncia, AtteseGolden Attese, string? Note = null);

/// <param name="Rilevanti">Articoli che un liquidatore considererebbe pertinenti: al più 4–5, così la recall@5 resta leggibile.</param>
/// <param name="EsclusioniDaNonPerdere">Esclusioni importanti, per la recall sul risultato completo con le clausole integrative.</param>
public sealed record AtteseGolden(IReadOnlyList<string> Rilevanti, IReadOnlyList<string> EsclusioniDaNonPerdere);

public static class GoldenSetFile
{
    public const string NomeFile = "golden_set.json";

    public static async Task<GoldenSet> LeggiAsync(string percorso, CancellationToken cancellationToken)
    {
        await using FileStream file = File.OpenRead(percorso);

        return await JsonSerializer.DeserializeAsync<GoldenSet>(file, SinistriJson.Opzioni, cancellationToken)
            ?? throw new InvalidOperationException($"{percorso} vuoto.");
    }
}
