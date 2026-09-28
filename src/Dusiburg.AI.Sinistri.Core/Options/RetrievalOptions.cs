namespace Dusiburg.AI.Sinistri.Core.Options;

/// <summary>
/// Parametri di retrieval meno "da demo" delle manopole: sezione <c>Retrieval</c> di <c>appsettings.json</c> di Api e Cli,
/// validata all'avvio.
/// </summary>
public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public int TopClausole { get; set; } = 5;

    /// <summary>
    /// Distanza massima entro cui si aggiunge un'esclusione o una franchigia non già tra le prime (D10). Tarata in Fase 5 sugli
    /// scenari demo con <c>embeddinggemma</c>: le pertinenti stanno sotto 0,69, quelle fuori tema sopra 0,70.
    /// </summary>
    public double DistanzaMaxClausolaIntegrativa { get; set; } = 0.70;

    /// <summary>
    /// Franchigia aggiunta quando nessuna franchigia è tra le clausole recuperate (CHECKPOINT 6): stessa numerazione nei due prodotti
    /// (casa "franchigia frontale", RC "franchigia fissa"). Vuota: nessuna aggiunta.
    /// </summary>
    public string? ArticoloFranchigiaBase { get; set; } = "Art. 4.1";

    public int TopSinistri { get; set; } = 10;

    public int SinistriNelPrompt { get; set; } = 5;

    public int AnniStorico { get; set; } = 5;

    public int MesiControlloDuplicati { get; set; } = 24;

    public int EmbeddingBatchSize { get; set; } = 16;

    /// <summary>Elenco dei problemi, vuoto se la configurazione è valida.</summary>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        RequirePositive(errors, nameof(TopClausole), TopClausole);
        RequirePositive(errors, nameof(TopSinistri), TopSinistri);
        RequirePositive(errors, nameof(SinistriNelPrompt), SinistriNelPrompt);
        RequirePositive(errors, nameof(AnniStorico), AnniStorico);
        RequirePositive(errors, nameof(MesiControlloDuplicati), MesiControlloDuplicati);
        RequirePositive(errors, nameof(EmbeddingBatchSize), EmbeddingBatchSize);

        if (DistanzaMaxClausolaIntegrativa is <= 0 or > 2)
        {
            errors.Add($"{SectionName}:{nameof(DistanzaMaxClausolaIntegrativa)} deve essere compresa tra 0 (escluso) e 2.");
        }

        if (SinistriNelPrompt > TopSinistri)
        {
            errors.Add($"{SectionName}:{nameof(SinistriNelPrompt)} non può superare {nameof(TopSinistri)}.");
        }

        return errors;
    }

    private static void RequirePositive(List<string> errors, string name, int value)
    {
        if (value <= 0)
        {
            errors.Add($"{SectionName}:{name} deve essere maggiore di zero.");
        }
    }
}

/// <summary>Sezione <c>Seed</c>: generatore dei dati sintetici deterministico (Fase 3).</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public int RandomSeed { get; set; } = 20260924;
}
