using System.CommandLine;
using System.CommandLine.Parsing;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Cli.Commands;

/// <summary>
/// Opzioni con valori di dominio (fase-5.md §6): nome del membro dell'enum senza distinzione tra maiuscole e minuscole, più i codici
/// del piano (<c>CASA_FABBRICATI</c>, <c>RC_PROF_TECNICI</c>).
/// </summary>
internal static class OpzioniDominio
{
    private static readonly Dictionary<string, Prodotto> AliasProdotto = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CASA_FABBRICATI"] = Prodotto.CasaFabbricati,
        ["RC_PROF_TECNICI"] = Prodotto.RcProfTecnici,
    };

    public static Option<Prodotto> OpzioneProdotto() => new("--prodotto")
    {
        Description = $"Prodotto: {string.Join(", ", Enum.GetNames<Prodotto>())} (anche CASA_FABBRICATI, RC_PROF_TECNICI).",
        Required = true,
        CustomParser = risultato => Parse(risultato, AliasProdotto)
    };

    public static Option<CausaSinistro?> OpzioneCausa() => new("--causa")
    {
        Description = $"Causa: {string.Join(", ", Enum.GetNames<CausaSinistro>())}.",
        CustomParser = risultato => Parse<CausaSinistro>(risultato, [])
    };

    private static TEnum Parse<TEnum>(ArgumentResult risultato, Dictionary<string, TEnum> alias) where TEnum : struct, Enum
    {
        string valore = risultato.Tokens.Single().Value;

        if (alias.TryGetValue(valore, out TEnum daAlias))
        {
            return daAlias;
        }

        if (Enum.TryParse(valore, ignoreCase: true, out TEnum membro) && Enum.IsDefined(membro))
        {
            return membro;
        }

        risultato.AddError($"Valore '{valore}' non valido: usare {string.Join(", ", Enum.GetNames<TEnum>().Concat(alias.Keys))}.");

        return default;
    }
}
