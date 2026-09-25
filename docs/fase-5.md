# Fase 5 — Retrieval (pilastri A e B) e statistiche

> Riferimento: `PLAN.md` §4 Fase 5. Applica le decisioni D9 (filtro per causa), D10 (query unica per le clausole integrative) e D11 (statistiche sull'insieme mostrato) di `fase-0.md`.

## Obiettivo

Due ricerche e un calcolo statistico, esposti da repository in `Data` e usati dalle Fasi 6–9:
- **A.** clausole pertinenti a una denuncia, con la garanzia di mostrare anche ciò che *limita* la copertura;
- **B.** sinistri storici simili con ricerca ibrida (vettore + filtri SQL);
- **statistiche** di liquidazione calcolate in SQL, mai dall'LLM.

Comandi di debug: `search-clausole` e `search-sinistri`.

---

## 1. Parametro vettoriale nelle query

Tutte le query ricevono il vettore della denuncia come `@q`:
- con il tipo nativo (`SqlVector<float>`, Fase 4) si scrive `VECTOR_DISTANCE('cosine', c.Embedding, @q)`;
- con il fallback JSON si scrive `CAST(@q AS VECTOR({n}))`, con `{n}` = `EMBEDDING_DIMENSIONS` inserito da `SqlTextBuilder`.

Nelle query sotto si usa `@q` per brevità.

---

## 2. A — Clausole pertinenti (`IClausolaRepository.CercaPertinentiAsync`)

```csharp
Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
    float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, CancellationToken cancellationToken);

public sealed record ClausolaTrovata(
    int Id, string Articolo, TipoClausola Tipo, string Titolo, string Testo,
    double Distanza, int Rank, bool Integrativa);
```

Query unica (D10): le prime `@top` per distanza, **più** la migliore esclusione e la migliore franchigia se non sono già tra le prime e sono entro la soglia.

```sql
WITH Distanze AS (
    SELECT c.Id, c.Articolo, c.TipoClausolaId, c.Titolo, c.Testo,
           VECTOR_DISTANCE('cosine', c.Embedding, @q) AS Distanza
    FROM dbo.Clausola c
    WHERE c.ProdottoId = @prodottoId
      AND c.Embedding IS NOT NULL
),
Classificate AS (
    SELECT *,
           ROW_NUMBER() OVER (ORDER BY Distanza)                              AS RankGlobale,
           ROW_NUMBER() OVER (PARTITION BY TipoClausolaId ORDER BY Distanza) AS RankPerTipo
    FROM Distanze
)
SELECT Id, Articolo, TipoClausolaId AS Tipo, Titolo, Testo, Distanza,
       RankGlobale AS Rank,
       CAST(CASE WHEN RankGlobale > @top THEN 1 ELSE 0 END AS BIT) AS Integrativa
FROM Classificate
WHERE RankGlobale <= @top
   OR (RankPerTipo = 1
       AND TipoClausolaId IN (@esclusioneId, @franchigiaId)
       AND Distanza <= @distanzaMax)
ORDER BY Distanza;
```

- Se esclusione e franchigia sono già tra le prime `@top`, il risultato ha `@top` righe; altrimenti fino a `@top + 2`.
- `Integrativa = true` si mostra nella UI e nella console ("aggiunta per completezza: limita la copertura").
- `DistanzaMaxClausolaIntegrativa` (default 0.45) è un valore di partenza: va tarato guardando le distanze reali sugli scenari demo. Serve a non aggiungere un'esclusione che non c'entra niente solo perché è "la meno lontana".
- Le definizioni (`TipoClausola.Definizione`) restano nel ranking normale: possono comparire se pertinenti.

---

## 3. B — Sinistri simili, ricerca ibrida (`ISinistroRepository.CercaSimiliAsync`)

```csharp
public sealed record FiltriStorico(
    Prodotto Prodotto,
    int AnniStorico,
    string? Provincia = null,
    decimal? ImportoMin = null,
    CausaSinistro? Causa = null,
    IReadOnlyCollection<int>? EscludiSinistriIds = null);

Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(
    float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken);

public sealed record SinistroSimile(
    int Id, string Numero, DateOnly DataEvento, string Provincia, CausaSinistro Causa,
    string Descrizione, string? EsitoPerizia, StatoSinistro Stato,
    decimal? ImportoLiquidato, double Distanza);
```

```sql
SELECT TOP (@top)
       s.Id, s.Numero, s.DataEvento, s.Provincia, s.CausaSinistroId AS Causa,
       s.Descrizione, s.EsitoPerizia, s.StatoSinistroId AS Stato, s.ImportoLiquidato,
       VECTOR_DISTANCE('cosine', s.Embedding, @q) AS Distanza
FROM dbo.Sinistro s
JOIN dbo.Polizza p ON p.Id = s.PolizzaId
WHERE p.ProdottoId = @prodottoId
  AND s.StatoSinistroId IN (@chiusoId, @respintoId)
  AND s.DataEvento >= DATEADD(YEAR, -@anni, CAST(GETDATE() AS DATE))
  AND (@provincia IS NULL OR s.Provincia = @provincia)
  AND (@importoMin IS NULL OR s.ImportoLiquidato >= @importoMin)
  AND (@causaId IS NULL OR s.CausaSinistroId = @causaId)
  AND s.Embedding IS NOT NULL
ORDER BY Distanza;
```

Note:
- Solo sinistri **chiusi o respinti**: gli aperti non hanno esito e non aiutano la valutazione.
- `@importoMin` esclude di fatto i respinti (liquidato NULL): è il comportamento atteso per lo scenario 5 ("sopra 5.000 €").
- Gli **Id da escludere** (`EscludiSinistriIds`) servono in Fase 9 (golden set su sinistri esistenti) e in Fase 7; si passano come JSON (`AND s.Id NOT IN (SELECT value FROM OPENJSON(@escludi))`) solo se la lista non è vuota.
- Il pattern `(@p IS NULL OR col = @p)` può produrre piani non ottimali; con 400 righe è irrilevante. Se in Fase 10 si passa a 50k righe si valuta `OPTION (RECOMPILE)`.
- `CAST(GETDATE() AS DATE)` rende il filtro coerente con la colonna `DATE`.

---

## 4. Statistiche (`ISinistroRepository.CalcolaStatisticheAsync`)

Calcolate **sull'insieme esatto dei simili restituiti** (D11): la query riceve gli Id.

```csharp
public sealed record StatisticheSimili(
    int NumeroCasi, int Respinti, decimal PercentualeRespinti,
    decimal? LiquidatoMin, decimal? LiquidatoMediana, decimal? LiquidatoMax);

Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken);
```

```sql
WITH Simili AS (
    SELECT s.StatoSinistroId, s.ImportoLiquidato
    FROM dbo.Sinistro s
    WHERE s.Id IN (SELECT CAST(value AS INT) FROM OPENJSON(@ids))
),
Mediana AS (
    SELECT DISTINCT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ImportoLiquidato) OVER () AS Valore
    FROM Simili
    WHERE StatoSinistroId = @chiusoId AND ImportoLiquidato IS NOT NULL
)
SELECT COUNT(*)                                                         AS NumeroCasi,
       SUM(CASE WHEN StatoSinistroId = @respintoId THEN 1 ELSE 0 END)  AS Respinti,
       CAST(100.0 * SUM(CASE WHEN StatoSinistroId = @respintoId THEN 1 ELSE 0 END)
            / NULLIF(COUNT(*), 0) AS DECIMAL(5,1))                     AS PercentualeRespinti,
       MIN(CASE WHEN StatoSinistroId = @chiusoId THEN ImportoLiquidato END) AS LiquidatoMin,
       (SELECT CAST(Valore AS DECIMAL(12,2)) FROM Mediana)              AS LiquidatoMediana,
       MAX(CASE WHEN StatoSinistroId = @chiusoId THEN ImportoLiquidato END) AS LiquidatoMax
FROM Simili;
```

- `PERCENTILE_CONT` in SQL Server è una funzione finestra: da qui la CTE `Mediana` con `DISTINCT`.
- Min, mediana e max si calcolano **solo sui chiusi** (i respinti hanno liquidato NULL); la percentuale di respinti su tutti i casi.
- Lista vuota → tutti i valori a 0/NULL, senza eccezione.

---

## 5. Servizio applicativo di ricerca (`Core`)

`RicercaService` compone embedding e repository ed è usato da CLI, API e dalla scheda (Fase 6):

```csharp
Task<RisultatoRicercaClausole> CercaClausoleAsync(string testo, Prodotto prodotto, CancellationToken ct);
Task<RisultatoRicercaStorico>  CercaStoricoAsync(string testo, FiltriStorico filtri, CancellationToken ct);
// RisultatoRicercaStorico = simili + statistiche + tempi (embedding, query)
```

Ogni risultato riporta i **tempi** (embedding e query SQL), mostrati a console e nella UI.

---

## 6. Comandi CLI di debug

```
search-clausole "<testo>" --prodotto CasaFabbricati [--top 5]
search-sinistri "<testo>" --prodotto CasaFabbricati [--provincia MI] [--importo-min 5000] [--causa FenomenoElettrico] [--anni 5] [--top 10]
```

- `--prodotto` e `--causa` accettano il nome del membro dell'enum senza distinzione tra maiuscole e minuscole; per comodità anche i codici del piano (`CASA_FABBRICATI`, `RC_PROF_TECNICI`) tramite una tabella di alias.
- Output `search-clausole`: tabella `# | Articolo | Tipo | Titolo | Distanza | (integrativa)`.
- Output `search-sinistri`: tabella `# | Numero | Data | Prov | Causa | Stato | Liquidato | Distanza | Descrizione (80 caratteri)`, poi il riquadro delle statistiche.

## 7. Test introdotti in questa fase

Test di integrazione su `Sinistri_Test` con **vettori a 4 dimensioni scritti a mano** (D13): distanze note a priori, nessuna dipendenza da Ollama.

| Test | Cosa verifica |
|---|---|
| `ClausolaRepositoryTests.CercaPertinenti_OrdinaPerDistanza` | ordine e `top` rispettati |
| `ClausolaRepositoryTests.CercaPertinenti_AggiungeEsclusioneEntroSoglia` | esclusione fuori dalle prime `top` ma entro soglia → aggiunta con `Integrativa = true` |
| `ClausolaRepositoryTests.CercaPertinenti_NonAggiungeEsclusioneOltreSoglia` | stessa situazione con soglia più bassa → non aggiunta (due chiamate al SUT con soglie diverse) |
| `ClausolaRepositoryTests.CercaPertinenti_FiltraPerProdotto` | mai clausole dell'altro prodotto |
| `SinistroRepositoryTests.CercaSimili_EscludeAperti` | nessun sinistro `Aperto` nei risultati |
| `SinistroRepositoryTests.CercaSimili_FiltriOpzionali` | provincia, importo minimo, causa, anni: stessa ricerca ripetuta variando un filtro alla volta |
| `SinistroRepositoryTests.CalcolaStatistiche_Mediana` | mediana su numero pari e dispari di valori; respinti esclusi da min/mediana/max |
| `SinistroRepositoryTests.CalcolaStatistiche_ListaVuota` | valori a zero/NULL, nessuna eccezione |

## 8. Criteri di completamento

Sui dati reali (dopo `DbInit` + `embed`), controllando a mano i risultati:

| Scenario | Comando | Atteso |
|---|---|---|
| 1 | `search-clausole "Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno." --prodotto CasaFabbricati` | Art. 2.4 tra le prime 2; presenti 2.5, almeno un'esclusione (3.1, 3.2 o 3.4) e una franchigia (4.3) |
| 2 | `search-clausole "Il cliente dice che la grandine ha rotto i pannelli solari sul tetto." --prodotto CasaFabbricati` | 2.2, 2.3 e 3.6 presenti; 4.2 presente |
| 3 | `search-clausole "Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento." --prodotto RcProfTecnici` | 2.1 e 2.6 presenti; almeno un'esclusione (3.4 o 3.5) |
| 4 | `search-clausole "Dopo un temporale si è bruciata la caldaia e il televisore." --prodotto CasaFabbricati` | 2.6 presente; 3.7 e 4.4 presenti |
| 5 | `search-sinistri "sovratensione ha danneggiato il quadro elettrico e l'inverter" --prodotto CasaFabbricati --provincia MI --importo-min 5000 --causa FenomenoElettrico` | almeno 5 risultati, tutti MI, `FenomenoElettrico`, > 5.000 €; statistiche coerenti con la tabella |

Se uno scenario non è soddisfatto si interviene prima sui **testi delle clausole** (Fase 3) e sulla soglia integrativa, non sulle query; le modifiche si riportano nel riepilogo. Build della solution e test verdi.

## 9. Commit proposto (non eseguito)

```
fase 5: ricerca vettoriale delle clausole, ricerca ibrida dello storico e statistiche
```
