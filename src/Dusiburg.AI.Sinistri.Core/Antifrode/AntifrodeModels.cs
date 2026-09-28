using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Core.Antifrode;

// Contratti dell'antifrode (fase-7.md §1): controllo della nuova denuncia e fraud-scan, usati da CLI e API (Fase 8).

/// <summary>fraud-scan: sinistro ↔ sinistro sotto soglia. <c>NumeroA</c> è sempre il sinistro inserito per primo (Id minore).</summary>
public sealed record CoppiaSospetta(
    string NumeroA, string NumeroB, double Distanza, MotivoSegnalazione Motivo,
    int GiorniTraDenunce, string DescrizioneA, string DescrizioneB)
{
    /// <summary>Stesso contraente o stesso riparatore: le coppie attese dal seed hanno sempre uno dei due legami.</summary>
    public bool ConLegame => Motivo != MotivoSegnalazione.SoloTestoSimile;
}

/// <summary>Distanza effettiva di una coppia attesa, anche sopra le soglie. Null se uno dei due sinistri manca o non ha embedding.</summary>
public sealed record DistanzaCoppiaAttesa(CoppiaDuplicati Coppia, double? Distanza, bool InPeriodo);

/// <summary>Una riga della tabella precision/recall (fase-7.md §3).</summary>
public sealed record RigaValutazione(
    double Soglia, int Trovate, int VeriPositivi, int FalsiPositivi, int FalsiNegativi, double Precision, double Recall, double F1);

/// <summary>
/// Valutazione rispetto a <c>data/duplicati_attesi.json</c>, calcolata due volte: su tutte le coppie e sulle sole coppie con stesso
/// contraente o riparatore. Le seconde escludono i testi ripetuti dai template del seed, che a distanza quasi nulla non sono duplicati.
/// </summary>
public sealed record ValutazioneFraudScan(
    IReadOnlyList<RigaValutazione> Tutte,
    RigaValutazione? SuggeritaTutte,
    IReadOnlyList<RigaValutazione> ConLegame,
    RigaValutazione? SuggeritaConLegame,
    IReadOnlyList<DistanzaCoppiaAttesa> DistanzeAttese,
    CoppiaSospetta? NonAttesaPiuVicina,
    CoppiaSospetta? NonAttesaConLegamePiuVicina);

public sealed record RisultatoFraudScan(
    int Mesi, double Soglia, IReadOnlyList<CoppiaSospetta> Coppie, ValutazioneFraudScan? Valutazione, TimeSpan Durata);

public interface IAntifrodeRepository
{
    /// <summary>
    /// Sinistri denunciati negli ultimi <paramref name="mesi"/>, di qualsiasi prodotto e stato, sotto soglia rispetto alla denuncia.
    /// Ordine: stesso contraente, poi stesso riparatore, poi solo testo; a parità per distanza.
    /// </summary>
    Task<IReadOnlyList<SegnalazioneDuplicato>> CercaDuplicatiDenunciaAsync(
        float[] vettoreDenuncia, string numeroPolizza, int? riparatoreId, int mesi, double soglia, int top, CancellationToken cancellationToken);

    /// <summary>Self-join sui sinistri denunciati negli ultimi <paramref name="mesi"/>: coppie sotto soglia, ordinate per distanza.</summary>
    Task<IReadOnlyList<CoppiaSospetta>> CercaCoppieAsync(int mesi, double soglia, CancellationToken cancellationToken);

    /// <summary>Distanza di ciascuna coppia attesa, a qualunque valore, e se entrambi i sinistri cadono nel periodo.</summary>
    Task<IReadOnlyList<DistanzaCoppiaAttesa>> DistanzeCoppieAsync(
        IReadOnlyList<CoppiaDuplicati> coppie, int mesi, CancellationToken cancellationToken);
}

public static class Motivi
{
    public static MotivoSegnalazione Da(bool stessoContraente, bool stessoRiparatore) => (stessoContraente, stessoRiparatore) switch
    {
        (true, true) => MotivoSegnalazione.StessoContraenteERiparatore,
        (true, false) => MotivoSegnalazione.StessoContraente,
        (false, true) => MotivoSegnalazione.StessoRiparatore,
        _ => MotivoSegnalazione.SoloTestoSimile
    };
}
