using System.Text.Json;
using System.Text.Json.Serialization;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary><c>data/duplicati_attesi.json</c>: le coppie che il fraud-scan (Fase 7) deve trovare, per <c>Numero</c>.</summary>
public static class DuplicatiAttesiFile
{
    public const string NomeFile = "duplicati_attesi.json";

    private static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<string> ScriviAsync(string cartella, DatiSintetici dati, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cartella);
        string percorso = Path.Combine(cartella, NomeFile);

        await using FileStream file = File.Create(percorso);
        await JsonSerializer.SerializeAsync(file, new DuplicatiAttesi(dati.GeneratoIl, dati.RandomSeed, dati.Coppie), Opzioni, cancellationToken);

        return percorso;
    }

    public static async Task<DuplicatiAttesi> LeggiAsync(string percorso, CancellationToken cancellationToken)
    {
        await using FileStream file = File.OpenRead(percorso);

        return await JsonSerializer.DeserializeAsync<DuplicatiAttesi>(file, Opzioni, cancellationToken)
            ?? throw new InvalidOperationException($"{percorso} vuoto.");
    }

    /// <summary>Cartella <c>data/</c> della radice del repository, trovata risalendo da <paramref name="partenza"/> fino al file <c>.slnx</c>.</summary>
    public static string CartellaDati(string partenza)
    {
        for (DirectoryInfo? cartella = new(partenza); cartella is not null; cartella = cartella.Parent)
        {
            if (cartella.EnumerateFiles("*.slnx").Any())
            {
                return Path.Combine(cartella.FullName, "data");
            }
        }

        throw new InvalidOperationException($"Nessun file .slnx risalendo da {partenza}: impossibile trovare la radice del repository.");
    }
}

public sealed record DuplicatiAttesi(DateOnly GeneratoIl, int RandomSeed, IReadOnlyList<CoppiaDuplicati> Coppie);
