using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

/// <summary>Polizza, clausole, simili e statistiche fissi per i test della scheda.</summary>
internal static class DatiScheda
{
    public static readonly DatiPolizza Polizza = new("CF-DEMO-000001", Prodotto.CasaFabbricati, "Mario Bianchi", "MI",
        new DateOnly(2025, 9, 25), new DateOnly(2028, 9, 25), 300_000m, 250m);

    public static readonly IReadOnlyList<ClausolaTrovata> Clausole =
    [
        new(1, "Art. 2.4", TipoClausola.Garanzia, "Acqua condotta", "La Società indennizza i danni da acqua condotta.", 0.52, 1, false),
        new(2, "Art. 3.4", TipoClausola.Esclusione, "Usura", "Sono esclusi i danni da usura.", 0.54, 2, false),
        new(3, "Art. 2.5", TipoClausola.Garanzia, "Ricerca del guasto", "Spese di ricerca del guasto.", 0.57, 3, false),
        new(4, "Art. 4.3", TipoClausola.Franchigia, "Franchigia acqua condotta", "Rimborso fino a 5.000 euro con franchigia di 150 euro.", 0.65, 7, true),
    ];

    public static readonly StatisticheSimili Statistiche = new(10, 2, 20.0m, 850m, 3100m, 9800m);

    public static IReadOnlyList<SinistroSimile> Simili(int quanti) =>
    [
        .. Enumerable.Range(1, quanti).Select(i => new SinistroSimile(i, $"SIN-2025-{i:D6}", new DateOnly(2025, 3, 1), "MI", CausaSinistro.AcquaCondotta,
            new string('d', 400), new string('e', 250), StatoSinistro.Chiuso, 3200m, 0.1 * i))
    ];

    public static SchedaPreIstruttoria Scheda(
        IReadOnlyList<GaranziaOperante>? garanzie = null,
        IReadOnlyList<EsclusioneDaVerificare>? esclusioni = null,
        FranchigiaApplicabile? franchigia = null,
        string valutazione = "Sinistro indennizzabile salvo verifica dell'usura.") =>
        new(garanzie ?? [new GaranziaOperante("Art. 2.4", "Rottura accidentale.")],
            esclusioni ?? [new EsclusioneDaVerificare("Art. 3.4", "Se la rottura è dovuta a usura.")],
            franchigia ?? new FranchigiaApplicabile("Art. 4.3", "Franchigia di polizza."),
            ["Data della rottura"],
            valutazione);
}
