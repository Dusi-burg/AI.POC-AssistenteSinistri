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

Chiamato come passo 6 di `PreIstruttoriaService` (Fase 6). La denuncia si vettorializza **come documento** (`EmbedDocumentsAsync`), non come query: per i modelli instruction-aware (Fase 1b) il confronto tra denuncia e sinistri indicizzati deve essere simmetrico, altrimenti le distanze non sono comparabili con quelle di `fraud-scan`. Con `embeddinggemma` (scelto al CHECKPOINT 1b) query e documento hanno prefissi diversi, quindi servono davvero due chiamate. Nel banco della Fase 1b le coppie sono state misurate con il prefisso `task: sentence similarity`: soglie migliori 0,18 per `embeddinggemma` e 0,26 per `bge-m3`, contro il default 0,08. La taratura del §4 va rifatta sui vettori "documento" salvati nel DB.

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

## 2 bis. Vettore antifrode della nuova denuncia (deciso il 2026-09-28)

Misura sulla riformulazione quasi letterale di `SIN-2026-000024` (contraente di `CF-DEMO-000001`), vettorizzata come documento:

| Confronto | Distanza |
|---|---|
| denuncia ↔ `Sinistro.Embedding` (causa + descrizione + **esito di perizia**) | 0,165 |
| denuncia con causa ↔ sinistro con causa, senza esito | 0,043 |
| denuncia senza causa ↔ sinistro con causa, senza esito | 0,095 |
| solo descrizione ↔ solo descrizione | **0,057** |

Sul vettore completo la riformulazione (0,165) è a ridosso del primo sinistro non legato (0,172): nessuna soglia separa i due casi. La colpa è dell'esito di perizia, che la denuncia nuova non ha, come previsto al §2.

Decisione (confermata dall'utente): nuova colonna `Sinistro.EmbeddingAntifrode VECTOR(n)` con la **sola descrizione** (`EmbeddingTextBuilder.Antifrode`), calcolata da `embed` come terza destinazione (`TabellaEmbedding.SinistriAntifrode`, `embed --solo SinistriAntifrode`). La denuncia si vettorizza allo stesso modo, senza la causa indicata, così la distanza non dipende da come è stata compilata la richiesta. Il controllo 9 di `health` conta anche i vettori antifrode mancanti.

Il fraud-scan resta sul vettore completo: sulla sola descrizione le coppie attese si allontanano (mediana 0,040 contro 0,012, massimo 0,143), perché il generatore crea i duplicati aggiungendo frasi di contorno ("Allego il preventivo…") e l'esito, uguale nelle due copie, le teneva vicine. Due vettori, quindi due soglie (§6 bis).

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
| `ValutazioneDuplicatiTests.Distribuzione_MinMedianaMax` | unit | mediana con numero pari e dispari di distanze |
| `ValutazioneDuplicatiTests.Valuta_TutteEConLegame` | unit | le due valutazioni del §6 bis e le coppie non attese più vicine |
| `AntifrodeRepositoryTests.DistanzeCoppie_AncheFuoriSogliaEPeriodo` | integrazione | distanza delle coppie attese a qualunque valore, fuori periodo, sinistro inesistente |
| `PreIstruttoriaServiceTests.Genera_ControlloAntifrode_SegnalazioniNellEsitoENonNelPrompt` | unit | vettore della sola descrizione, soglia della denuncia, nessun duplicato nel prompt |
| `SchedaMarkdownRendererTests` (esteso) | unit | riquadro della segnalazione con motivo, contraente e riparatore |

## 6. Criteri di completamento

- `fraud-scan --mesi 12` individua **almeno 8 delle 10 coppie attese** con la soglia suggerita.
- La tabella precision/recall è stampata per le 4 soglie, con la distribuzione delle distanze.
- `ask` su una denuncia che riformula un sinistro esistente dello stesso contraente mostra la segnalazione "stesso contraente".
- Se la precision alla soglia scelta è bassa (molti falsi positivi dovuti ai template, rischio segnalato in `fase-3.md`), lo si dichiara nel riepilogo con le coppie non attese più vicine, proponendo interventi sui template.
- Build della solution e test verdi.

## 6 bis. Esito (2026-09-28)

DB ricreato con DbInit (`data/duplicati_attesi.json` rigenerato: la numerazione dipende dalla data di riferimento) ed `embed` completo, 880 vettori in 31 s. Output in `eval/fase-7/`.

### fraud-scan (`eval/fase-7/fraud-scan-12-mesi.txt`)

310 sinistri denunciati negli ultimi 12 mesi, query in 0,3 s.

| Soglia | Tutte: trovate / TP / precision | Con stesso contraente o riparatore: trovate / TP / precision | Recall |
|---|---|---|---|
| **0,05** | 108 / 9 / 0,08 | **11 / 9 / 0,82** | **0,90** |
| 0,08 | 258 / 9 / 0,03 | 18 / 9 / 0,50 | 0,90 |
| 0,12 | 633 / 10 / 0,02 | 39 / 10 / 0,26 | 1,00 |
| 0,15 | 1180 / 10 / 0,01 | 71 / 10 / 0,14 | 1,00 |

- Soglia suggerita **0,05**, confermata dall'utente e ora default di `SOGLIA_DUPLICATO_COSINE` (era 0,08): 9 coppie attese su 10.
- Distanze delle coppie attese: min 0,002 · mediana 0,012 · max 0,089.
- Coppia attesa mancata: `SIN-2025-000049 ↔ SIN-2026-000012` a 0,089. È una riscrittura più pesante delle altre: frasi invertite, "mansarda" diventa "cucina", "perdita" diventa "fuoriuscita".
- **Precision bassa sul totale**, rischio già segnalato in `fase-3.md`: 97 coppie su 108 hanno il solo testo simile, tra cui 13 a distanza 0,000. Le descrizioni distinte sono 371 su 410: il generatore riusa lo stesso testo per contraenti diversi. Per questo la valutazione si calcola anche sulle sole coppie con un legame, e la console mostra prima quelle.
- Coppia non attesa più vicina con un legame: `SIN-2026-000196 ↔ SIN-2026-000264` a 0,024, stesso riparatore. Sono due template quasi uguali (grandinata / tromba d'aria sulle tegole) con lo stesso esito di perizia.

Interventi proposti sui template (non eseguiti, da valutare in Fase 9):
1. escludere nel generatore le descrizioni identiche tra sinistri diversi, o arricchire gli slot;
2. variare l'esito di perizia all'interno dello stesso template, oggi identico, che avvicina tutti i sinistri dello stesso template;
3. più riparatori (oggi 12 per 410 sinistri): "stesso riparatore" capita per caso in una coppia su 12.

### Controllo sulla nuova denuncia (`eval/fase-7/ask-riformulazione.md`)

Vettore della sola descrizione (§2 bis) con la nuova manopola `SOGLIA_DUPLICATO_DENUNCIA`, default **0,07**:

| Denuncia (`CF-DEMO-000001`, `RP-DEMO-000001` per lo scenario 3) | Sinistri entro 0,15 |
|---|---|
| Riformulazione di `SIN-2026-000024` | **`SIN-2026-000024` 0,057, stesso contraente** · poi solo testo 0,086, 0,108 |
| Scenario 1 | solo testo 0,132, 0,134, 0,149 |
| Scenario 2 | solo testo 0,089, 0,089, 0,099, 0,111 (il testo dello scenario è anche un template del seed) |
| Scenario 3 | solo testo 0,071, 0,118 |
| Scenario 4 | solo testo **0,048**, 0,074, 0,114, … |

Con 0,07 la riformulazione viene segnalata come "stesso contraente"; tra i 4 scenari demo compare una sola segnalazione (scenario 4, solo testo simile), in fondo alla lista. Il passo "antifrode" richiede meno di 0,1 s.

### Criteri

- `fraud-scan --mesi 12`: 9 coppie attese su 10 alla soglia suggerita ✓
- tabella precision/recall per le 4 soglie, con distribuzione delle distanze e coppie non attese più vicine ✓
- `ask` sulla riformulazione: segnalazione "stesso contraente" ✓
- precision bassa sul totale dichiarata, con le proposte sui template ✓
- build della solution senza warning; 133 test verdi (10 in più) ✓

### Scostamenti

| Punto | Previsto | Fatto | Motivo |
|---|---|---|---|
| Vettore della nuova denuncia | `Sinistro.Embedding` | `Sinistro.EmbeddingAntifrode` (sola descrizione), denuncia senza causa | §2 bis: l'esito di perizia rende incomparabili denuncia e sinistro |
| Soglie | una, `SOGLIA_DUPLICATO_COSINE` | due: `SOGLIA_DUPLICATO_COSINE` 0,05 (fraud-scan) e `SOGLIA_DUPLICATO_DENUNCIA` 0,07 (ask) | vettori diversi, distribuzioni diverse (§2 ultima riga) |
| Valutazione | su tutte le coppie | su tutte e sulle sole coppie con un legame | i testi ripetuti dai template rendono illeggibile la precision totale |
| Output del fraud-scan | una tabella per distanza | due blocchi: con legame, poi solo testo; colonna ✓ per le coppie attese | 108 coppie di cui 97 senza legame |
| Ordine nella nuova denuncia | nel servizio | in SQL (`ORDER BY StessoContraente DESC, StessoRiparatore DESC, Distanza`) prima del `TOP` | una segnalazione con legame non va persa dietro 20 "solo testo" |
| `TempiEsecuzione` | — | campo `Antifrode` | passo cronometrato come gli altri |
| Test | 5 | 10: in più distanze delle coppie attese, distribuzione, valutazione con legame, servizio (segnalazioni fuori dal prompt), Markdown | |
| `fraud-scan` in `ScansionaAsync` (Core) | logica nel comando | servizio con `RisultatoFraudScan` | riusabile dall'API in Fase 8 |

## 7. Commit proposto (non eseguito)

```
fase 7: controllo antifrode sui quasi-duplicati e comando fraud-scan

AntifrodeService (Core) e AntifrodeRepository (Data). fraud-scan: self-join
vettoriale sui sinistri del periodo, coppie con motivo (stesso contraente,
stesso riparatore, solo testo) e valutazione contro duplicati_attesi.json
su tutte le coppie e su quelle con un legame; soglia suggerita 0,05, ora
default di SOGLIA_DUPLICATO_COSINE (9 coppie attese su 10, precision 0,82
sulle coppie con un legame). Controllo della nuova denuncia come ultimo passo
della pre-istruttoria, fuori dal prompt: nuova colonna Sinistro.EmbeddingAntifrode
con la sola descrizione (l'esito di perizia allontanava la riformulazione di
un sinistro a 0,165), calcolata da embed, e manopola SOGLIA_DUPLICATO_DENUNCIA
(0,07). Sezione "Possibili duplicati" della scheda con motivo, contraente e
riparatore.

Verifica: build della solution, 133 test verdi, eval/fase-7 con fraud-scan
a 12 mesi e la scheda di una riformulazione segnalata come stesso contraente.
```
