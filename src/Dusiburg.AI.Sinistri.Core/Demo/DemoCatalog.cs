using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Demo;

/// <summary>
/// Tabella unica dei dati demo (fase-3.md, come <c>DemoCatalog</c> di O2C): le polizze fisse create per prime dal seed e gli
/// scenari di <c>PLAN.md</c> §5. Da qui partono il seed, i pulsanti della UI (Fase 8) e i test di coerenza degli scenari.
/// </summary>
public static class DemoCatalog
{
    /// <summary>Validità espressa in anni rispetto alla data del seed, così le polizze demo sono sempre in vigore.</summary>
    public static IReadOnlyList<DemoPolizza> Polizze { get; } =
    [
        new("CF-DEMO-000001", Prodotto.CasaFabbricati, "Mario Bianchi", "MI", DecorrenzaAnni: -1, ScadenzaAnni: 2, Massimale: 300_000m, Franchigia: 250m),
        new("CF-DEMO-000002", Prodotto.CasaFabbricati, "Laura Verdi", "BO", DecorrenzaAnni: -2, ScadenzaAnni: 1, Massimale: 200_000m, Franchigia: 500m),
        new("RP-DEMO-000001", Prodotto.RcProfTecnici, "Studio Tecnico Ing. Neri", "VR", DecorrenzaAnni: -3, ScadenzaAnni: 1, Massimale: 1_000_000m, Franchigia: 2_500m),
    ];

    public static IReadOnlyList<DemoScenario> Scenari { get; } =
    [
        new(1, TipoScenario.PreIstruttoria, Prodotto.CasaFabbricati,
            "Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno.", NumeroPolizza: "CF-DEMO-000001"),
        new(2, TipoScenario.PreIstruttoria, Prodotto.CasaFabbricati,
            "Il cliente dice che la grandine ha rotto i pannelli solari sul tetto.", NumeroPolizza: "CF-DEMO-000001"),
        new(3, TipoScenario.PreIstruttoria, Prodotto.RcProfTecnici,
            "Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento.", NumeroPolizza: "RP-DEMO-000001"),
        new(4, TipoScenario.PreIstruttoria, Prodotto.CasaFabbricati,
            "Dopo un temporale si è bruciata la caldaia e il televisore.", NumeroPolizza: "CF-DEMO-000001"),
        new(5, TipoScenario.RicercaStorico, Prodotto.CasaFabbricati,
            "sovratensione ha danneggiato il quadro elettrico e l'inverter",
            Causa: CausaSinistro.FenomenoElettrico, Provincia: "MI", ImportoMinimo: 5_000m),
        new(6, TipoScenario.FraudScan, Prodotto: null, "Possibili duplicati tra i sinistri denunciati negli ultimi 12 mesi.", Mesi: 12),
    ];

    public static DemoPolizza Polizza(string numero) =>
        Polizze.SingleOrDefault(p => p.Numero == numero) ?? throw new ArgumentException($"Polizza demo {numero} inesistente.", nameof(numero));
}

public sealed record DemoPolizza(
    string Numero, Prodotto Prodotto, string Contraente, string Provincia, int DecorrenzaAnni, int ScadenzaAnni, decimal Massimale, decimal Franchigia)
{
    public DateOnly Decorrenza(DateOnly oggi) => oggi.AddYears(DecorrenzaAnni);

    public DateOnly Scadenza(DateOnly oggi) => oggi.AddYears(ScadenzaAnni);
}

public enum TipoScenario
{
    PreIstruttoria = 1,
    RicercaStorico = 2,
    FraudScan = 3,
}

/// <summary>Scenario demo: testo e polizza per la pre-istruttoria, filtri per la ricerca nello storico, finestra per il fraud-scan.</summary>
public sealed record DemoScenario(
    int Numero,
    TipoScenario Tipo,
    Prodotto? Prodotto,
    string Testo,
    string? NumeroPolizza = null,
    CausaSinistro? Causa = null,
    string? Provincia = null,
    decimal? ImportoMinimo = null,
    int? Mesi = null);
