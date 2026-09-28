using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Seed;

// Conteggi del dataset: riepilogo di DbInit (fase-3.md) e pagina di stato della UI (Fase 8).

public sealed record ConteggioSeed(int Contraenti, int Riparatori, int Polizze, int Clausole, int Sinistri);

public sealed record DistribuzioneCausaStato(CausaSinistro Causa, StatoSinistro Stato, int Sinistri);

public sealed record DistribuzioneProdottoProvincia(Prodotto Prodotto, string Provincia, int Sinistri);
