using System.Text.RegularExpressions;
using Bogus;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>Accesso unico ai template dei due prodotti: profili per causa, valori degli slot, frasi di contorno.</summary>
internal static partial class CatalogoTemplate
{
    public static IReadOnlyList<ProfiloCausa> Profili { get; } = [.. TemplateCasaFabbricati.Profili, .. TemplateRcProfTecnici.Profili];

    public static ProfiloCausa Profilo(CausaSinistro causa) => Profili.Single(p => p.Causa == causa);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Slot(Prodotto prodotto) => prodotto switch
    {
        Prodotto.CasaFabbricati => TemplateCasaFabbricati.Slot,
        Prodotto.RcProfTecnici => TemplateRcProfTecnici.Slot,
        _ => throw new ArgumentOutOfRangeException(nameof(prodotto), prodotto, null)
    };

    public static IReadOnlyList<string> FrasiDiContorno(Prodotto prodotto) => prodotto switch
    {
        Prodotto.CasaFabbricati => TemplateCasaFabbricati.FrasiDiContorno,
        Prodotto.RcProfTecnici => TemplateRcProfTecnici.FrasiDiContorno,
        _ => throw new ArgumentOutOfRangeException(nameof(prodotto), prodotto, null)
    };

    public static IEnumerable<string> SlotUsati(string testo) => SlotPattern().Matches(testo).Select(m => m.Groups[1].Value);

    /// <summary>
    /// Sostituisce gli slot con valori casuali del prodotto; <paramref name="scelti"/> conserva i valori già usati, così
    /// descrizione ed esito di perizia dello stesso sinistro parlano della stessa stanza o dello stesso oggetto.
    /// </summary>
    public static string Compila(string testo, Prodotto prodotto, Dictionary<string, string> scelti, Randomizer random)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> slot = Slot(prodotto);

        return SlotPattern().Replace(testo, match =>
        {
            string nome = match.Groups[1].Value;

            if (!scelti.TryGetValue(nome, out string? valore))
            {
                valore = slot.TryGetValue(nome, out IReadOnlyList<string>? valori)
                    ? random.Scegli(valori)
                    : throw new InvalidOperationException($"Slot {{{nome}}} senza valori per il prodotto {prodotto}.");
                scelti[nome] = valore;
            }

            return valore;
        });
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex SlotPattern();
}
