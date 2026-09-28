using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

public enum VarianteEsito
{
    Standard = 1,

    /// <summary>Descrizione che richiama un'esclusione: il 70% dei sinistri non aperti generati da questo template è respinto.</summary>
    TendenzaRespinto = 2,
}

/// <summary>Casi che gli scenari demo devono trovare nello storico (fase-3.md, "Copertura garantita per la demo").</summary>
public enum TemaDemo
{
    Nessuno = 0,
    SovratensioneQuadroInverter = 1,
    TuboParquetControsoffitto = 2,
    GrandinePannelli = 3,
    SolaioStrutture = 4,
}

/// <summary>
/// Descrizione base con slot (<c>{Stanza}</c>, <c>{Oggetto}</c>…) ed esiti di perizia coerenti. Gli esiti vuoti ricadono su
/// quelli generici della causa (<see cref="ProfiloCausa"/>); gli slot degli esiti prendono gli stessi valori della descrizione.
/// </summary>
public sealed record DescrizioneTemplate(
    CausaSinistro Causa,
    string Testo,
    VarianteEsito Variante,
    IReadOnlyList<string> EsitiChiuso,
    IReadOnlyList<string> EsitiRespinto,
    TemaDemo Tema = TemaDemo.Nessuno);

/// <summary>Parametri di una causa (tabella "Parametri per causa" di fase-3.md) con i suoi template.</summary>
public sealed record ProfiloCausa(
    CausaSinistro Causa,
    double Quota,
    decimal ImportoMinimo,
    decimal ImportoMassimo,
    double QuotaRespinti,
    IReadOnlyList<DescrizioneTemplate> Template,
    IReadOnlyList<string> EsitiChiuso,
    IReadOnlyList<string> EsitiRespinto)
{
    public Prodotto Prodotto => Causa.ProdottoDellaCausa();
}

/// <summary>Costruttori compatti per i cataloghi di template.</summary>
internal static class Template
{
    public static DescrizioneTemplate Standard(CausaSinistro causa, string testo, params string[] esitiChiuso) =>
        new(causa, testo, VarianteEsito.Standard, esitiChiuso, []);

    public static DescrizioneTemplate Demo(CausaSinistro causa, TemaDemo tema, string testo, params string[] esitiChiuso) =>
        new(causa, testo, VarianteEsito.Standard, esitiChiuso, [], tema);

    public static DescrizioneTemplate Respinto(CausaSinistro causa, string testo, string[] esitiRespinto, params string[] esitiChiuso) =>
        new(causa, testo, VarianteEsito.TendenzaRespinto, esitiChiuso, esitiRespinto);

    public static DescrizioneTemplate RespintoDemo(CausaSinistro causa, TemaDemo tema, string testo, string[] esitiRespinto, params string[] esitiChiuso) =>
        new(causa, testo, VarianteEsito.TendenzaRespinto, esitiChiuso, esitiRespinto, tema);
}
