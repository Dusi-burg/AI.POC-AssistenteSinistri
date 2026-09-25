# Fase 7 — Antifrode (quasi-duplicati)

> Riferimento: `PLAN.md` §4 Fase 7. I duplicati attesi sono quelli generati in Fase 3 (`data/duplicati_attesi.json`, 10 coppie).

## Obiettivo

1. Su una **nuova denuncia** segnalare i sinistri recenti molto simili, evidenziando stesso contraente o stesso riparatore.
2. Comando batch `fraud-scan` che trova le coppie sotto soglia tra i sinistri di un periodo.
3. Misurare precision/recall rispetto ai duplicati attesi e suggerire la soglia.

---

## 1. Contratti (`Core`)

```csharp
public enum MotivoSegnalazione : byte   // solo codice, non persistito: nessuna tabella di lookup
{
    StessoContraente = 1,
    StessoRiparatore = 2,
    StessoContraenteERiparatore = 3,
    SoloTestoSimile = 4,
}

public sealed record SegnalazioneDuplicato(       // nuova denuncia → sinistro esistente
    string NumeroSinistro, DateOnly DataDenuncia, CausaSinistro Causa, StatoSinistro Stato,
    string Contraente, string? Riparatore, double Distanza, MotivoSegnalazione Motivo, string Descrizione);

public sealed record CoppiaSospetta(               // fraud-scan: sinistro ↔ sinistro
    string NumeroA, string NumeroB, double Distanza, MotivoSegnalazione Motivo,
    int GiorniTraDenunce, string DescrizioneA, string DescrizioneB);
```

---

## 2. Controllo sulla nuova denuncia (`AntifrodeService.ControllaDenunciaAsync`)

Chiamato come passo 6 di `PreIstruttoriaService` (Fase 6). La denuncia si vettorializza **come documento** (`EmbedDocumentsAsync`), non come query: per i modelli instruction-aware (Fase 1b) il confronto tra denuncia e sinistri indicizzati deve essere simmetrico, altrimenti le distanze non sono comparabili con quelle di `fraud-scan`. Con `bge-m3` (nessun prefisso) i due vettori coincidono e la seconda chiamata si salta.

```sql
SELECT TOP (20)
       s.Numero, s.DataDenuncia, s.CausaSinistroId AS Causa, s.StatoSinistroId AS Stato,
       c.Nominativo AS Contraente, r.RagioneSociale AS Riparatore, s.Descrizione,
       CAST(CASE WHEN p.ContraenteId = @contraenteId THEN 1 ELSE 0 END AS BIT) AS StessoContraente,
       CAST(CASE WHEN @riparatoreId IS NOT NULL AND s.RiparatoreId = @riparatoreId THEN 1 ELSE 0 END AS BIT) AS StessoRiparatore,
       VECTOR_DISTANCE('cosine', s.Embedding, @q) AS Distanza
FROM dbo.Sinistro s
JOIN dbo.Polizza p     ON p.Id = s.PolizzaId
JOIN dbo.Contraente c  ON c.Id = p.ContraenteId
LEFT JOIN dbo.Riparatore r ON r.Id = s.RiparatoreId
WHERE s.DataDenuncia >= DATEADD(MONTH, -@mesi, CAST(GETDATE() AS DATE))
  AND s.Embedding IS NOT NULL
  AND VECTOR_DISTANCE('cosine', s.Embedding, @q) < @soglia
ORDER BY Distanza;
```

- **Qualsiasi prodotto** e **qualsiasi stato** (anche aperti): un duplicato si cerca ovunque.
- `@mesi` = `Retrieval:MesiControlloDuplicati` (24), `@soglia` = `SOGLIA_DUPLICATO_COSINE`.
- `@contraenteId` viene dalla polizza della denuncia; `@riparatoreId` dall'opzione `--riparatore` di `ask` (o dal campo della UI), se indicato.
- Il `Motivo` si ricava dai due flag. Nella scheda l'ordinamento mette prima stesso contraente, poi stesso riparatore, poi solo testo; a parità, per distanza.
- Differenza tra vettori: il sinistro storico è vettorizzato con l'esito di perizia, la denuncia nuova no (vedi `fase-4.md`). La distanza tra un sinistro e la sua ri-denuncia sarà quindi un po' più alta di quella tra due sinistri storici gemelli: la soglia per la nuova denuncia **potrebbe** dover essere più larga. Si misura sugli scenari e, se serve, si introduce `SogliaDuplicatoDenuncia` separata (da decidere al termine della fase).

---

## 3. `fraud-scan --mesi 12 [--soglia 0.08] [--valuta]`

Self-join vettoriale sui sinistri del periodo (`AntifrodeRepository.CercaCoppieAsync`):

```sql
WITH Periodo AS (
    SELECT s.Id, s.Numero, s.DataDenuncia, s.Descrizione, s.RiparatoreId, p.ContraenteId, s.Embedding
    FROM dbo.Sinistro s
    JOIN dbo.Polizza p ON p.Id = s.PolizzaId
    WHERE s.DataDenuncia >= DATEADD(MONTH, -@mesi, CAST(GETDATE() AS DATE))
      AND s.Embedding IS NOT NULL
)
SELECT a.Numero AS NumeroA, b.Numero AS NumeroB,
       VECTOR_DISTANCE('cosine', a.Embedding, b.Embedding) AS Distanza,
       CAST(CASE WHEN a.ContraenteId = b.ContraenteId THEN 1 ELSE 0 END AS BIT) AS StessoContraente,
       CAST(CASE WHEN a.RiparatoreId = b.RiparatoreId THEN 1 ELSE 0 END AS BIT) AS StessoRiparatore,
       ABS(DATEDIFF(DAY, a.DataDenuncia, b.DataDenuncia)) AS GiorniTraDenunce,
       a.Descrizione AS DescrizioneA, b.Descrizione AS DescrizioneB
FROM Periodo a
JOIN Periodo b ON a.Id < b.Id
WHERE VECTOR_DISTANCE('cosine', a.Embedding, b.Embedding) < @soglia
ORDER BY Distanza;
```

- Costo: con ~80 sinistri nei 12 mesi sono ~3.200 confronti; anche con tutti i 410 sarebbero ~84.000, trascurabili. Nessun indice necessario.
- `a.Id < b.Id` evita coppie doppie e autoconfronti.
- Output console: tabella `NumeroA | NumeroB | Distanza | Motivo | Giorni | DescrizioneA (60) | DescrizioneB (60)`, poi il conteggio per motivo.

### Valutazione (`--valuta`, o sempre quando esiste `data/duplicati_attesi.json`)

1. Si caricano le coppie attese (per `Numero`, ordinate: coppia non orientata).
2. Per ogni soglia in **0.05 / 0.08 / 0.12 / 0.15** (più quella indicata, se diversa) si esegue la query una volta alla soglia **massima** (0.15) e si filtrano in memoria le soglie minori: una sola query.
3. Per ogni soglia: `TP` = coppie trovate che sono attese, `FP` = trovate non attese, `FN` = attese non trovate.
   - precision = TP / (TP + FP), recall = TP / 10, F1.
4. Tabella:
   ```
   Soglia  Trovate  TP  FP  FN  Precision  Recall  F1
   0.05       7      6   1   4    0.86      0.60   0.71
   0.08      11      9   2   1    0.82      0.90   0.86   ← suggerita
   0.12      25     10  15   0    0.40      1.00   0.57
   0.15      60     10  50   0    0.17      1.00   0.29
   ```
   (numeri di esempio). **Soglia suggerita** = la più bassa con recall ≥ 0.8; a parità, F1 massimo.
5. Si stampa anche la **distribuzione delle distanze** delle 10 coppie attese (min / mediana / max) e la distanza della coppia non attesa più vicina: è il dato che spiega la scelta della soglia.
6. Le coppie attese non trovate sono elencate con la loro distanza effettiva, così si capisce se è un problema di soglia o di generazione dei dati.

La soglia suggerita **non** viene scritta in configurazione automaticamente: la si riporta nel riepilogo e dopo conferma dell'utente la si aggiorna a mano come default nel codice delle opzioni (`SOGLIA_DUPLICATO_COSINE`); resta sovrascrivibile dall'AppHost durante la demo.

---

## 4. Integrazione con la scheda (Fase 6)

- `EsitoPreIstruttoria.PossibiliDuplicati` valorizzato.
- Nel Markdown, sezione "Possibili duplicati" con un riquadro di avviso per ogni segnalazione: *"⚠️ SIN-2026-000123 (12/05/2026, stesso contraente) — distanza 0,041 — «descrizione…»"*.
- La segnalazione **non** entra nel prompt dell'LLM: l'antifrode è un controllo deterministico separato, mostrato accanto alla scheda (evita che il modello "motivi" su un indizio statistico).

## 5. Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `AntifrodeRepositoryTests.CercaCoppie_SottoSoglia` | integrazione (`VECTOR(4)`) | coppie con vettori quasi identici trovate, altre no; nessuna coppia doppia |
| `AntifrodeRepositoryTests.CercaCoppie_FuoriPeriodo_Esclusa` | integrazione | sinistro con denuncia oltre `mesi` escluso |
| `AntifrodeRepositoryTests.ControllaDenuncia_MotivoStessoContraente` | integrazione | flag e motivo corretti; stessa chiamata ripetuta con e senza riparatore |
| `ValutazioneDuplicatiTests.Calcola_PrecisionRecall` | unit | TP/FP/FN, coppie non orientate (A,B) = (B,A) |
| `ValutazioneDuplicatiTests.SuggerisciSoglia_RecallMinima` | unit | scelta della soglia secondo la regola del §3.4 |

## 6. Criteri di completamento

- `fraud-scan --mesi 12` individua **almeno 8 delle 10 coppie attese** con la soglia suggerita.
- La tabella precision/recall è stampata per le 4 soglie, con la distribuzione delle distanze.
- `ask` su una denuncia che riformula un sinistro esistente dello stesso contraente mostra la segnalazione "stesso contraente".
- Se la precision alla soglia scelta è bassa (molti falsi positivi dovuti ai template, rischio segnalato in `fase-3.md`), lo si dichiara nel riepilogo con le coppie non attese più vicine, proponendo interventi sui template.
- Build della solution e test verdi.

## 7. Commit proposto (non eseguito)

```
fase 7: controllo antifrode sui quasi-duplicati e comando fraud-scan
```
