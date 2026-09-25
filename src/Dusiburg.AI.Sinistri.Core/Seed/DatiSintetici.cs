using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Seed;

// Righe generate dal seed (fase-3.md). Gli Id li assegna il generatore: il DB è appena ricreato, quindi SeedRepository
// li inserisce così come sono (IDENTITY_INSERT) e i riferimenti tra righe restano quelli del generatore.

public sealed record ContraenteSintetico(int Id, string Nominativo, string Provincia, bool StudioTecnico);

public sealed record RiparatoreSintetico(int Id, string RagioneSociale);

public sealed record PolizzaSintetica(
    int Id, string Numero, Prodotto Prodotto, int ContraenteId, DateOnly Decorrenza, DateOnly Scadenza, decimal Massimale, decimal Franchigia)
{
    public bool ValidaIl(DateOnly data) => data >= Decorrenza && data <= Scadenza;
}

public sealed record SinistroSintetico(
    int Id,
    string Numero,
    int PolizzaId,
    int? RiparatoreId,
    DateOnly DataEvento,
    DateOnly DataDenuncia,
    string Provincia,
    CausaSinistro Causa,
    string Descrizione,
    string? EsitoPerizia,
    StatoSinistro Stato,
    decimal? ImportoRiservato,
    decimal? ImportoLiquidato);

public enum TipoCoppiaDuplicati
{
    StessoContraente = 1,
    StessoRiparatore = 2,
}

/// <summary>Coppia di quasi-duplicati attesa dal fraud-scan (Fase 7), identificata dai <c>Numero</c>, stabili tra un reset e l'altro.</summary>
public sealed record CoppiaDuplicati(string Originale, string Duplicato, TipoCoppiaDuplicati Tipo);

public sealed record DatiSintetici(
    DateOnly GeneratoIl,
    int RandomSeed,
    IReadOnlyList<ContraenteSintetico> Contraenti,
    IReadOnlyList<RiparatoreSintetico> Riparatori,
    IReadOnlyList<PolizzaSintetica> Polizze,
    IReadOnlyList<SinistroSintetico> Sinistri,
    IReadOnlyList<CoppiaDuplicati> Coppie);
