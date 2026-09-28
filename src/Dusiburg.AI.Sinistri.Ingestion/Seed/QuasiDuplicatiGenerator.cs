using System.Globalization;
using System.Text.RegularExpressions;
using Bogus;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>Coppia prodotta dal generatore, con gli Id provvisori dei due sinistri.</summary>
internal sealed record CoppiaProvvisoria(int OriginaleId, int DuplicatoId, TipoCoppiaDuplicati Tipo);

/// <summary>
/// Quasi-duplicati (fase-3.md §3c): per 10 sinistri casa già generati si crea un gemello con la descrizione riformulata senza LLM.
/// Coppie 1–5 sullo stesso contraente, 6–10 su contraenti diversi con lo stesso riparatore; entrambe le denunce negli ultimi
/// 10 mesi, a 1–4 mesi di distanza, così il fraud-scan su 12 mesi (Fase 7) le trova.
/// </summary>
internal sealed partial class QuasiDuplicatiGenerator(Randomizer random, DateOnly oggi, Anagrafiche anagrafiche)
{
    public const int CoppieStessoContraente = 5;
    public const int CoppieStessoRiparatore = 5;

    /// <summary>Circa 10 mesi: la denuncia dell'originale cade tra 120 e 300 giorni fa, quella del gemello 30–120 giorni dopo.</summary>
    public const int GiorniFinestra = 300;

    private const int GiorniMinimiOriginale = 120;

    /// <summary>Sinonimi applicati nei due versi: se il testo contiene il primo termine si usa il secondo, altrimenti il contrario.</summary>
    private static readonly (string A, string B)[] Sinonimi =
    [
        ("perdita", "fuoriuscita d'acqua"),
        ("danneggiato", "rovinato"),
        ("danneggiata", "rovinata"),
        ("danneggiando", "rovinando"),
        ("improvvisamente", "all'improvviso"),
        ("temporale", "nubifragio"),
        ("abbiamo trovato", "abbiamo scoperto"),
        ("fotografie", "foto"),
        ("macchia", "chiazza"),
        ("perito", "tecnico della compagnia"),
    ];

    public IReadOnlyList<CoppiaProvvisoria> Genera(List<SinistroSintetico> sinistri, IReadOnlySet<int> esclusi)
    {
        Dictionary<int, PolizzaSintetica> polizze = anagrafiche.Polizze.ToDictionary(p => p.Id);
        DateOnly inizioFinestra = oggi.AddDays(-GiorniFinestra - 35);

        bool Coperta(PolizzaSintetica p) => p.Prodotto == Prodotto.CasaFabbricati && p.Decorrenza <= inizioFinestra && p.Scadenza >= oggi;

        List<SinistroSintetico> candidati = [.. random.Shuffle(sinistri.Where(s =>
            !esclusi.Contains(s.Id) && s.Stato != StatoSinistro.Aperto && Coperta(polizze[s.PolizzaId])))];

        List<CoppiaProvvisoria> coppie = [];

        foreach (SinistroSintetico originale in candidati.Take(CoppieStessoContraente))
        {
            int contraenteId = polizze[originale.PolizzaId].ContraenteId;
            PolizzaSintetica[] altre = [.. anagrafiche.Polizze.Where(p => p.ContraenteId == contraenteId && p.Id != originale.PolizzaId && Coperta(p))];
            PolizzaSintetica polizza = altre.Length > 0 ? random.ArrayElement(altre) : polizze[originale.PolizzaId];

            coppie.Add(Accoppia(sinistri, originale, polizza, originale.Provincia, TipoCoppiaDuplicati.StessoContraente));
        }

        foreach (SinistroSintetico originale in candidati.Skip(CoppieStessoContraente).Where(s => s.RiparatoreId is not null).Take(CoppieStessoRiparatore))
        {
            int contraenteId = polizze[originale.PolizzaId].ContraenteId;
            PolizzaSintetica polizza = random.ArrayElement(anagrafiche.Polizze.Where(p => p.ContraenteId != contraenteId && Coperta(p)).ToArray());
            string provincia = anagrafiche.Contraenti.Single(c => c.Id == polizza.ContraenteId).Provincia;

            coppie.Add(Accoppia(sinistri, originale, polizza, provincia, TipoCoppiaDuplicati.StessoRiparatore));
        }

        return coppie.Count == CoppieStessoContraente + CoppieStessoRiparatore
            ? coppie
            : throw new InvalidOperationException($"Generate solo {coppie.Count} coppie di quasi-duplicati: troppo pochi sinistri candidati.");
    }

    /// <summary>
    /// Riformulazione deterministica: sinonimi da dizionario, stanza cambiata (dettaglio secondario), ordine delle frasi invertito.
    /// Usata anche dopo la parafrasi via LLM, per rigenerare il gemello dall'originale parafrasato.
    /// </summary>
    public static string Riformula(string testo, Randomizer random)
    {
        string risultato = testo;

        foreach ((string a, string b) in Sinonimi)
        {
            risultato = Parola(a).IsMatch(risultato) ? Parola(a).Replace(risultato, b) : Parola(b).Replace(risultato, a);
        }

        risultato = CambiaDettaglio(risultato, "Stanza", "in ", random);

        if (risultato == testo)
        {
            risultato = CambiaDettaglio(risultato, "Circostanza", string.Empty, random);
        }

        // Ultima risorsa per una frase senza sinonimi, dettagli né separatori: una frase d'apertura, che l'inversione porta in testa.
        if (risultato == testo && FineFrase().Split(testo.Trim()).Length == 1 && testo.IndexOfAny([';', ':']) < 0)
        {
            risultato = $"{risultato.TrimEnd()} {AperturaGemello}";
        }

        return InvertiFrasi(risultato);
    }

    private const string AperturaGemello = "Segnalo il danno per l'apertura della pratica.";

    /// <summary>Sposta l'originale nella finestra degli ultimi 10 mesi e aggiunge il gemello in coda alla lista.</summary>
    private CoppiaProvvisoria Accoppia(
        List<SinistroSintetico> sinistri, SinistroSintetico originale, PolizzaSintetica polizzaGemello, string provinciaGemello, TipoCoppiaDuplicati tipo)
    {
        DateOnly denunciaOriginale = oggi.AddDays(-random.Number(GiorniMinimiOriginale, GiorniFinestra));
        DateOnly denunciaGemello = denunciaOriginale.AddDays(random.Number(30, 120));

        SinistroSintetico spostato = originale with
        {
            DataDenuncia = denunciaOriginale,
            DataEvento = denunciaOriginale.AddDays(-random.Number(1, 30))
        };
        sinistri[sinistri.IndexOf(originale)] = spostato;

        // Variato del ±15%, ma entro il massimale e l'importo massimo della causa (es. limite del fenomeno elettrico, Art. 4.4).
        decimal limite = Math.Min(polizzaGemello.Massimale, CatalogoTemplate.Profilo(originale.Causa).ImportoMassimo - polizzaGemello.Franchigia);
        decimal? liquidato = originale.ImportoLiquidato is { } importo
            ? Math.Min(RandomizerExtensions.ArrotondaA10(importo * (decimal)random.Double(0.85, 1.15)), limite)
            : null;

        var gemello = spostato with
        {
            Id = sinistri.Count + 1,
            PolizzaId = polizzaGemello.Id,
            DataDenuncia = denunciaGemello,
            DataEvento = denunciaGemello.AddDays(-random.Number(1, 30)),
            Provincia = provinciaGemello,
            Descrizione = Riformula(originale.Descrizione, random),
            ImportoLiquidato = liquidato
        };
        sinistri.Add(gemello);

        return new CoppiaProvvisoria(spostato.Id, gemello.Id, tipo);
    }

    /// <summary>Sostituisce il primo valore dello slot casa trovato nel testo (preceduto da <paramref name="prefisso"/>) con un altro valore.</summary>
    private static string CambiaDettaglio(string testo, string slot, string prefisso, Randomizer random)
    {
        IReadOnlyList<string> valori = CatalogoTemplate.Slot(Prodotto.CasaFabbricati)[slot];

        foreach (string valore in valori)
        {
            var pattern = new Regex($@"\b{Regex.Escape(prefisso + valore)}\b");

            if (pattern.IsMatch(testo))
            {
                string altro = random.Scegli(valori.Where(v => v != valore).ToArray());

                return pattern.Replace(testo, prefisso + altro, 1);
            }
        }

        return testo;
    }

    /// <summary>Frasi in ordine inverso; con una sola frase si scambiano le due parti attorno a ";" o ":".</summary>
    private static string InvertiFrasi(string testo)
    {
        string[] frasi = FineFrase().Split(testo.Trim());

        if (frasi.Length > 1)
        {
            return string.Join(' ', Enumerable.Reverse(frasi));
        }

        int separatore = testo.IndexOfAny([';', ':']);

        if (separatore < 0)
        {
            return testo;
        }

        string prima = testo[..separatore].Trim();
        string seconda = testo[(separatore + 1)..].Trim().TrimEnd('.');

        return $"{Maiuscola(seconda)}. {prima}.";
    }

    private static string Maiuscola(string testo) =>
        testo.Length == 0 ? testo : char.ToUpper(testo[0], CultureInfo.InvariantCulture) + testo[1..];

    private static Regex Parola(string parola) => new($@"\b{Regex.Escape(parola)}\b");

    [GeneratedRegex(@"(?<=\.)\s+(?=\p{Lu})")]
    private static partial Regex FineFrase();
}
