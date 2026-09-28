using Bogus;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>
/// Sinistri da template (fase-3.md §3c): prima i casi che gli scenari demo devono trovare, poi la parte casuale fino alla quota di
/// ogni causa. Gli Id sono provvisori (ordine di generazione): <see cref="SyntheticDataGenerator"/> rinumera per data di denuncia.
/// </summary>
internal sealed class SinistriGenerator(Randomizer random, DateOnly oggi, Anagrafiche anagrafiche, int numeroSinistri = SinistriGenerator.SinistriDefault)
{
    /// <summary>Sinistri della demo; il banco di prova della Fase 10.1 ne genera di più con le stesse proporzioni.</summary>
    public const int SinistriDefault = 400;

    private const float QuotaAperti = 0.10f;
    private const float QuotaConRiparatore = 0.80f;
    private const float QuotaProvinciaContraente = 0.90f;
    private const double RespintiTendenza = 0.70;
    private const int AnniStorico = 5;
    private const int GiorniAperti = 180;

    private readonly Dictionary<int, ContraenteSintetico> contraenti = anagrafiche.Contraenti.ToDictionary(c => c.Id);

    /// <summary>Id provvisori dei sinistri della copertura demo: il generatore dei quasi-duplicati non li tocca.</summary>
    public HashSet<int> IdCoperturaDemo { get; } = [];

    public List<SinistroSintetico> Genera()
    {
        List<SinistroSintetico> sinistri = [];

        foreach (Richiesta richiesta in CoperturaDemo())
        {
            SinistroSintetico sinistro = Crea(richiesta, sinistri.Count + 1);
            sinistri.Add(sinistro);
            IdCoperturaDemo.Add(sinistro.Id);
        }

        foreach (ProfiloCausa profilo in CatalogoTemplate.Profili)
        {
            SinistroSintetico[] forzati = [.. sinistri.Where(s => s.Causa == profilo.Causa)];

            foreach (Richiesta richiesta in Casuali(profilo, forzati))
            {
                sinistri.Add(Crea(richiesta, sinistri.Count + 1));
            }
        }

        return sinistri;
    }

    /// <summary>Cosa generare: template, stato ed eventuali vincoli della copertura demo.</summary>
    private sealed record Richiesta(
        ProfiloCausa Profilo, DescrizioneTemplate Template, StatoSinistro Stato, string? Provincia = null, decimal? LiquidatoMinimo = null);

    /// <summary>Casi garantiti per gli scenari demo 1, 2, 3 e 5.</summary>
    private IEnumerable<Richiesta> CoperturaDemo()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return Tema(CausaSinistro.FenomenoElettrico, TemaDemo.SovratensioneQuadroInverter, StatoSinistro.Chiuso) with
            {
                Provincia = "MI",
                LiquidatoMinimo = 5_000m
            };
        }

        for (int i = 0; i < 6; i++)
        {
            yield return Tema(CausaSinistro.AcquaCondotta, TemaDemo.TuboParquetControsoffitto, StatoSinistro.Chiuso);
        }

        for (int i = 0; i < 5; i++)
        {
            yield return Tema(CausaSinistro.EventoAtmosferico, TemaDemo.GrandinePannelli, i < 4 ? StatoSinistro.Respinto : StatoSinistro.Chiuso);
        }

        for (int i = 0; i < 5; i++)
        {
            yield return Tema(CausaSinistro.ErroreProgettuale, TemaDemo.SolaioStrutture, StatoSinistro.Chiuso);
        }
    }

    private Richiesta Tema(CausaSinistro causa, TemaDemo tema, StatoSinistro stato)
    {
        ProfiloCausa profilo = CatalogoTemplate.Profilo(causa);

        return new Richiesta(profilo, random.ArrayElement(profilo.Template.Where(t => t.Tema == tema).ToArray()), stato);
    }

    /// <summary>
    /// Parte casuale di una causa, fino alla sua quota. Stati per conteggio esatto (10% aperti, quota di respinti della tabella,
    /// al netto dei casi forzati) e poi mescolati: con 16–88 sinistri per causa, estrazioni indipendenti si scosterebbero troppo.
    /// I template "tendenza respinto" coprono circa metà dei respinti e sono respinti al 70%; il resto usa i template standard.
    /// </summary>
    private IEnumerable<Richiesta> Casuali(ProfiloCausa profilo, SinistroSintetico[] forzati)
    {
        DescrizioneTemplate[] tendenza = [.. profilo.Template.Where(t => t.Variante == VarianteEsito.TendenzaRespinto)];
        DescrizioneTemplate[] standard = [.. profilo.Template.Where(t => t.Variante == VarianteEsito.Standard)];

        int totale = (int)Math.Round(numeroSinistri * profilo.Quota);
        int aperti = (int)Math.Round(totale * QuotaAperti);
        int respinti = Math.Max(0, (int)Math.Round(totale * profilo.QuotaRespinti) - forzati.Count(s => s.Stato == StatoSinistro.Respinto));
        int chiusi = totale - forzati.Length - aperti - respinti;

        // Sinistri da template "tendenza": metà dei respinti diviso 0,7, e non più della quota di template di quel tipo.
        double quotaTendenza = tendenza.Length == 0 ? 0 : Math.Min((double)tendenza.Length / profilo.Template.Count, 0.5 * respinti / RespintiTendenza / (respinti + chiusi));
        int daTendenza = (int)Math.Round(quotaTendenza * (respinti + chiusi));
        int respintiTendenza = Math.Min(respinti, (int)Math.Round(daTendenza * RespintiTendenza));
        int chiusiTendenza = Math.Min(chiusi, daTendenza - respintiTendenza);

        List<Richiesta> richieste = [];
        Aggiungi(richieste, respintiTendenza, StatoSinistro.Respinto, tendenza);
        Aggiungi(richieste, respinti - respintiTendenza, StatoSinistro.Respinto, standard);
        Aggiungi(richieste, chiusiTendenza, StatoSinistro.Chiuso, tendenza);
        Aggiungi(richieste, chiusi - chiusiTendenza, StatoSinistro.Chiuso, standard);
        Aggiungi(richieste, aperti, StatoSinistro.Aperto, standard);

        return random.Shuffle(richieste);

        void Aggiungi(List<Richiesta> lista, int quanti, StatoSinistro stato, DescrizioneTemplate[] template)
        {
            for (int i = 0; i < quanti; i++)
            {
                lista.Add(new Richiesta(profilo, random.ArrayElement(template), stato));
            }
        }
    }

    private SinistroSintetico Crea(Richiesta richiesta, int id)
    {
        Prodotto prodotto = richiesta.Profilo.Prodotto;
        DateOnly da = richiesta.Stato == StatoSinistro.Aperto ? oggi.AddDays(-GiorniAperti) : oggi.AddYears(-AnniStorico);
        (PolizzaSintetica polizza, DateOnly evento) = PolizzaEData(prodotto, da, oggi.AddDays(-1),
            p => richiesta.Provincia is null || contraenti[p.ContraenteId].Provincia == richiesta.Provincia);
        DateOnly denuncia = Min(evento.AddDays(random.Number(1, 30)), oggi);

        Dictionary<string, string> slot = [];
        string descrizione = Descrizione(richiesta.Template, prodotto, slot);
        (string? esito, decimal? riservato, decimal? liquidato) = Esito(richiesta, polizza, slot);

        return new SinistroSintetico(
            id,
            Numero: string.Empty,
            polizza.Id,
            prodotto == Prodotto.CasaFabbricati && random.Bool(QuotaConRiparatore) ? random.Scegli(anagrafiche.Riparatori).Id : null,
            evento,
            denuncia,
            richiesta.Provincia ?? Provincia(polizza),
            richiesta.Profilo.Causa,
            descrizione,
            esito,
            richiesta.Stato,
            riservato,
            liquidato);
    }

    /// <summary>Una polizza del prodotto la cui validità interseca la finestra, e una data dell'evento dentro l'intersezione.</summary>
    private (PolizzaSintetica Polizza, DateOnly Evento) PolizzaEData(Prodotto prodotto, DateOnly da, DateOnly a, Func<PolizzaSintetica, bool> filtro)
    {
        PolizzaSintetica[] candidate = [.. anagrafiche.Polizze.Where(p => p.Prodotto == prodotto && p.Decorrenza <= a && p.Scadenza >= da && filtro(p))];
        PolizzaSintetica polizza = candidate.Length > 0
            ? random.ArrayElement(candidate)
            : throw new InvalidOperationException($"Nessuna polizza {prodotto} valida tra {da} e {a} per i vincoli richiesti.");

        return (polizza, random.Data(Max(da, polizza.Decorrenza), Min(a, polizza.Scadenza)));
    }

    private string Descrizione(DescrizioneTemplate template, Prodotto prodotto, Dictionary<string, string> slot)
    {
        string testo = CatalogoTemplate.Compila(template.Testo, prodotto, slot, random);
        int frasi = random.WeightedRandom(new[] { 0, 1, 2 }, new[] { 0.4f, 0.4f, 0.2f });
        IEnumerable<string> contorno = random.Shuffle(CatalogoTemplate.FrasiDiContorno(prodotto)).Take(frasi);

        return string.Join(' ', [testo, .. contorno]);
    }

    /// <summary>Esito di perizia e importi coerenti con lo stato (regole comuni di fase-3.md §3c).</summary>
    private (string? Esito, decimal? Riservato, decimal? Liquidato) Esito(Richiesta richiesta, PolizzaSintetica polizza, Dictionary<string, string> slot)
    {
        ProfiloCausa profilo = richiesta.Profilo;
        decimal minimo = Math.Max(profilo.ImportoMinimo, polizza.Franchigia + 100m);

        if (richiesta.LiquidatoMinimo is { } liquidatoMinimo)
        {
            minimo = Math.Max(minimo, liquidatoMinimo + polizza.Franchigia + 10m);
        }

        decimal stimato = random.Importo(minimo, Math.Max(minimo, profilo.ImportoMassimo));

        return richiesta.Stato switch
        {
            StatoSinistro.Aperto => (random.Bool() ? null : "Perizia in corso.", Math.Round(stimato / 100m, MidpointRounding.AwayFromZero) * 100m, null),
            StatoSinistro.Respinto => (EsitoDa(richiesta.Template.EsitiRespinto, profilo.EsitiRespinto, profilo.Prodotto, slot), null, null),
            _ => (EsitoDa(richiesta.Template.EsitiChiuso, profilo.EsitiChiuso, profilo.Prodotto, slot), null,
                Math.Min(RandomizerExtensions.ArrotondaA10(stimato - polizza.Franchigia), polizza.Massimale)),
        };
    }

    private string EsitoDa(IReadOnlyList<string> delTemplate, IReadOnlyList<string> dellaCausa, Prodotto prodotto, Dictionary<string, string> slot) =>
        CatalogoTemplate.Compila(random.Scegli(delTemplate.Count > 0 ? delTemplate : dellaCausa), prodotto, slot, random);

    /// <summary>Provincia del contraente nel 90% dei casi, altrimenti una provincia vicina.</summary>
    private string Provincia(PolizzaSintetica polizza)
    {
        string provincia = contraenti[polizza.ContraenteId].Provincia;

        return random.Bool(QuotaProvinciaContraente) ? provincia : Province.Vicina(provincia, random);
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
