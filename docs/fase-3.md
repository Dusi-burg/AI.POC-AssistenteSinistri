# Fase 3 — Dati sintetici

> Riferimento: `PLAN.md` §4 Fase 3. Tutti i dati sono **fittizi**: nessuna compagnia, persona o condizione di polizza reale.
> Al termine della fase si propone uno **stop "morbido"**: l'utente rilegge i testi delle clausole, da cui dipendono retrieval, scheda e golden set.

## Obiettivo

Popolare il DB con ~60 clausole scritte a mano, anagrafiche generate con Bogus (seed deterministico) e ~400 sinistri generati da template, tra cui 10 coppie di quasi-duplicati registrate in `data/duplicati_attesi.json`. Il seed è il passo 6 del tool `DbInit` (`fase-2.md` §5), con l'opzione `--parafrasa-con-llm`.

---

## 3a. Clausole — `db/003_seed_clausole.sql`

### Forma dello script

- `DbInit` lo esegue su un DB appena creato, ma lo script è comunque rieseguibile a mano con `sqlcmd`: `MERGE` su `(ProdottoId, Articolo)`; se il testo cambia la riga viene aggiornata e il suo `Embedding` rimesso a `NULL`. Così, mentre si limano i testi delle clausole, basta `sqlcmd` + `embed --solo-mancanti` senza ricreare tutto il DB.
- Id delle lookup risolti per nome, per leggibilità:
  ```sql
  DECLARE @Casa TINYINT = (SELECT Id FROM dbo.Prodotto WHERE Name = N'CasaFabbricati');
  DECLARE @Garanzia TINYINT = (SELECT Id FROM dbo.TipoClausola WHERE Name = N'Garanzia');
  ...
  MERGE dbo.Clausola AS t
  USING (VALUES
    (@Casa, N'Art. 2.4', @Garanzia, N'Acqua condotta', N'La Società indennizza i danni materiali e diretti ...'),
    ...
  ) AS s (ProdottoId, Articolo, TipoClausolaId, Titolo, Testo)
  ON t.ProdottoId = s.ProdottoId AND t.Articolo = s.Articolo
  WHEN MATCHED AND (t.Testo <> s.Testo OR t.Titolo <> s.Titolo OR t.TipoClausolaId <> s.TipoClausolaId)
    THEN UPDATE SET Titolo = s.Titolo, Testo = s.Testo, TipoClausolaId = s.TipoClausolaId, Embedding = NULL
  WHEN NOT MATCHED THEN INSERT (...) VALUES (...);
  ```
- Ogni clausola: 2–6 frasi, linguaggio assicurativo italiano plausibile ("La Società indennizza…", "Sono esclusi i danni…", "Il pagamento dell'indennizzo è effettuato previa detrazione…").
- Numerazione: `Art. 1.x` definizioni/oggetto, `Art. 2.x` garanzie, `Art. 3.x` esclusioni, `Art. 4.x` franchigie, scoperti e limiti. La stessa numerazione vale per entrambi i prodotti (la unique è su prodotto + articolo; il retrieval filtra sempre per prodotto).

### CASA_FABBRICATI (35 clausole)

| Articolo | Tipo | Titolo |
|---|---|---|
| Art. 1.1 | Definizione | Fabbricato |
| Art. 1.2 | Definizione | Contenuto |
| Art. 1.3 | Definizione | Scoppio |
| Art. 1.4 | Definizione | Esplosione |
| Art. 1.5 | Definizione | Acqua condotta |
| Art. 1.6 | Definizione | Eventi atmosferici |
| Art. 1.7 | Definizione | Fenomeno elettrico |
| Art. 1.8 | Definizione | Scasso ed effrazione |
| Art. 1.9 | Definizione | Franchigia e scoperto |
| Art. 2.1 | Garanzia | Incendio, fulmine, esplosione e scoppio |
| Art. 2.2 | Garanzia | Eventi atmosferici |
| Art. 2.3 | Garanzia | Grandine su serramenti, lastre e pannelli solari (estensione, operante solo se richiamata in polizza) |
| Art. 2.4 | Garanzia | Acqua condotta |
| Art. 2.5 | Garanzia | Ricerca e riparazione del guasto |
| Art. 2.6 | Garanzia | Fenomeno elettrico |
| Art. 2.7 | Garanzia | Furto e rapina del contenuto |
| Art. 2.8 | Garanzia | Guasti cagionati dai ladri |
| Art. 2.9 | Garanzia | Responsabilità civile della proprietà del fabbricato |
| Art. 2.10 | Garanzia | Cristalli |
| Art. 2.11 | Garanzia | Spese di demolizione e sgombero |
| Art. 3.1 | Esclusione | Infiltrazioni dovute a mancata manutenzione |
| Art. 3.2 | Esclusione | Umidità, stillicidio e condensa |
| Art. 3.3 | Esclusione | Gelo su impianti di locali non riscaldati o non in uso |
| Art. 3.4 | Esclusione | Usura, corrosione e vetustà degli impianti |
| Art. 3.5 | Esclusione | Fabbricati in costruzione o in ristrutturazione |
| Art. 3.6 | Esclusione | Grandine su serramenti, pannelli solari e fotovoltaici (salvo Art. 2.3) |
| Art. 3.7 | Esclusione | Fenomeno elettrico: guasti meccanici, usura e apparecchi con oltre 10 anni |
| Art. 3.8 | Esclusione | Furto senza segni di scasso |
| Art. 3.9 | Esclusione | Dolo del contraente o dell'assicurato |
| Art. 3.10 | Esclusione | Rigurgito di fognature e allagamenti da falda |
| Art. 4.1 | Franchigia | Franchigia frontale per sinistro |
| Art. 4.2 | Franchigia | Scoperto eventi atmosferici (10%, minimo 500 €) |
| Art. 4.3 | Franchigia | Franchigia acqua condotta e limite ricerca guasto (5.000 € per anno) |
| Art. 4.4 | Franchigia | Limite fenomeno elettrico (3.000 € per anno, franchigia 150 €) |
| Art. 4.5 | Franchigia | Scoperto furto (20% in assenza di impianto antifurto) |

### RC_PROF_TECNICI (25 clausole)

| Articolo | Tipo | Titolo |
|---|---|---|
| Art. 1.1 | Garanzia | Oggetto dell'assicurazione (RC professionale di ingegneri e architetti) |
| Art. 1.2 | Definizione | Sinistro |
| Art. 1.3 | Definizione | Richiesta di risarcimento |
| Art. 1.4 | Definizione | Claims made |
| Art. 1.5 | Definizione | Retroattività |
| Art. 1.6 | Definizione | Perdite patrimoniali |
| Art. 1.7 | Definizione | Garanzia postuma |
| Art. 2.1 | Garanzia | Errori di progettazione e di calcolo |
| Art. 2.2 | Garanzia | Direzione lavori |
| Art. 2.3 | Garanzia | Coordinatore per la sicurezza (D.Lgs. 81/08) |
| Art. 2.4 | Garanzia | Perdite patrimoniali cagionate a terzi |
| Art. 2.5 | Garanzia | Spese legali e di resistenza |
| Art. 2.6 | Garanzia | Costi di rifacimento dell'opera |
| Art. 3.1 | Esclusione | Dolo dell'assicurato |
| Art. 3.2 | Esclusione | Attività per cui l'assicurato non è abilitato |
| Art. 3.3 | Esclusione | Multe, ammende e sanzioni |
| Art. 3.4 | Esclusione | Circostanze note prima della decorrenza |
| Art. 3.5 | Esclusione | Collaudo di opere progettate o dirette dall'assicurato |
| Art. 3.6 | Esclusione | Penali contrattuali e ritardi nella consegna |
| Art. 3.7 | Esclusione | Danni da inquinamento |
| Art. 3.8 | Esclusione | Attività svolte all'estero |
| Art. 4.1 | Franchigia | Franchigia fissa per sinistro |
| Art. 4.2 | Franchigia | Scoperto sulle perdite patrimoniali |
| Art. 4.3 | Franchigia | Massimale per sinistro e per anno assicurativo |
| Art. 4.4 | Franchigia | Sottolimite per l'attività di coordinatore della sicurezza |

### Coppie "garanzia vs esclusione" volutamente vicine

| Tema | Garanzia | Esclusione | Serve a |
|---|---|---|---|
| Acqua | Casa 2.4 Acqua condotta | Casa 3.1 Infiltrazioni / 3.2 Umidità | scenario demo 1 |
| Grandine | Casa 2.2 Eventi atmosferici, 2.3 Grandine su fragili | Casa 3.6 Grandine su pannelli | scenario demo 2 |
| Elettrico | Casa 2.6 Fenomeno elettrico | Casa 3.7 Guasti meccanici / apparecchi vecchi | scenario demo 4 |
| Furto | Casa 2.7 Furto | Casa 3.8 Furto senza scasso | test di pertinenza |
| Gelo | Casa 2.4 Acqua condotta | Casa 3.3 Gelo su impianti non in uso | test di pertinenza |
| Progettazione | RC 2.1 Errori di progettazione, 2.6 Rifacimento | RC 3.4 Circostanze note, 3.5 Collaudo di opere proprie | scenario demo 3 |
| Sicurezza | RC 2.3 Coordinatore sicurezza | RC 3.3 Multe e sanzioni | test di pertinenza |

I testi completi si scrivono in fase di sviluppo; **questa tabella di titoli è l'oggetto della review**. Gli articoli attesi per gli scenari demo (usati anche nel golden set di Fase 9) sono indicati in `fase-6.md`.

---

## 3b. Anagrafiche (Bogus, locale `it`, seed `Seed:RandomSeed`)

| Entità | Quantità | Regole |
|---|---|---|
| `Contraente` | 150 | `Nominativo` = nome e cognome (80%) o ragione sociale (20%, tipicamente studi tecnici); `Provincia` da un elenco pesato (sotto) |
| `Riparatore` | 12 | ragioni sociali plausibili di idraulici, elettricisti, serramentisti, imprese edili ("Idrotermica Rossi Snc"…) |
| `Polizza` | 200 | 70% `CasaFabbricati` (140), 30% `RcProfTecnici` (60); ogni contraente ha 1–2 polizze; le polizze RC sono assegnate preferibilmente ai contraenti "studio tecnico" |

- **Province** (sigle): Lombardia `MI BG BS MB VA CO MN PV CR LC LO SO`, Veneto `VE VR PD VI TV RO BL`, Emilia-Romagna `BO MO RE PR FE RA FC RN PC`. Pesi maggiori a MI, BG, BS, VR, PD, BO (città più popolose), così i filtri per provincia della demo trovano dati.
- **Numero polizza**: `CF-{anno decorrenza}-{progressivo:D6}` per casa, `RP-{anno}-{progressivo:D6}` per RC.
- **Date**: decorrenza casuale negli ultimi 6 anni, durata 1–3 anni; circa l'80% delle polizze è in vigore oggi.
- **Massimale / franchigia**: casa massimale 100.000–600.000 €, franchigia 150 / 250 / 500 €; RC massimale 250.000 / 500.000 / 1.000.000 / 2.000.000 €, franchigia 1.000 / 2.500 / 5.000 €.
- **Polizze demo fisse** (create per prime, prima della parte casuale, sempre uguali):

  | Numero | Prodotto | Contraente | Provincia | Validità |
  |---|---|---|---|---|
  | `CF-DEMO-000001` | CasaFabbricati | Mario Bianchi (fittizio) | MI | da oggi −1 anno a oggi +2 anni |
  | `CF-DEMO-000002` | CasaFabbricati | Laura Verdi (fittizia) | BO | da oggi −2 anni a oggi +1 anno |
  | `RP-DEMO-000001` | RcProfTecnici | Studio Tecnico Ing. Neri (fittizio) | VR | da oggi −3 anni a oggi +1 anno |

  Sono i numeri da usare negli scenari demo e nella UI.

**Determinismo:** a parità di `RandomSeed` il contenuto è identico. Le date sono calcolate **rispetto alla data di esecuzione di `DbInit`** (così "ultimi 5 anni" e "ultimi 12 mesi" restano sensati anche tra mesi); quindi due seed in giorni diversi producono date traslate, ma stessa struttura.

---

## 3c. Sinistri (~400, generatore a template)

### Template

Per ogni causa una lista di **8–15 descrizioni base con slot**, in una classe per prodotto (`TemplateCasaFabbricati`, `TemplateRcProfTecnici`) nel progetto `Ingestion`. Esempio:

```csharp
new DescrizioneTemplate(
    Causa: CausaSinistro.AcquaCondotta,
    Testo: "{Circostanza} si è verificata una perdita dal {Impianto} {Stanza}, con danni a {Danno}.",
    Variante: VarianteEsito.Standard)   // oppure VarianteEsito.TendenzaRespinto
```

Slot comuni: `{Stanza}` (bagno, cucina, soggiorno, lavanderia, sottotetto…), `{Materiale}` (parquet, controsoffitto in cartongesso, piastrelle, intonaco…), `{Oggetto}` (televisore, caldaia, inverter, frigorifero, quadro elettrico…), `{Circostanza}` (durante la notte, al rientro dalle ferie, dopo un forte temporale…), `{Danno}`.

Ogni template ha anche 2–4 **esiti di perizia** coerenti ("Il perito ha accertato la rottura del raccordo del flessibile…", "Il perito rileva infiltrazioni pregresse dovute a guaine deteriorate, riconducibili a mancata manutenzione (Art. 3.1)…").

### Parametri per causa

| Causa | Prodotto | Template | Quota sinistri | Importo liquidato (Chiuso) | % Respinto | Template "tendenza respinto" (70% respinto) |
|---|---|---|---|---|---|---|
| AcquaCondotta | Casa | 15 | 22% | 800–15.000 € | 20% | infiltrazioni da manutenzione, stillicidio, gelo in seconda casa |
| EventoAtmosferico | Casa | 12 | 14% | 1.500–25.000 € | 15% | grandine su pannelli/serramenti |
| FenomenoElettrico | Casa | 10 | 12% | 300–8.000 € | 10% | guasto meccanico, apparecchio vetusto |
| Incendio | Casa | 8 | 5% | 2.000–80.000 € | 5% | — |
| Furto | Casa | 8 | 7% | 500–12.000 € | 25% | nessun segno di scasso |
| Cristalli | Casa | 8 | 4% | 200–2.500 € | 10% | — |
| RcProprieta | Casa | 8 | 6% | 1.000–20.000 € | 15% | — |
| ErroreProgettuale | RC | 12 | 12% | 5.000–150.000 € | 20% | errore noto prima della decorrenza |
| ErroreDirezioneLavori | RC | 10 | 8% | 5.000–100.000 € | 20% | collaudo di opera propria |
| SicurezzaCantiere | RC | 8 | 5% | 10.000–200.000 € | 25% | richiesta di rimborso di sanzioni |
| PerditaPatrimoniale | RC | 8 | 5% | 3.000–60.000 € | 30% | penali per ritardo |

Regole comuni:
- il sinistro è su una polizza del **prodotto giusto** per la causa;
- `DataEvento` dentro la validità della polizza e negli ultimi 5 anni; `DataDenuncia` = evento + 1–30 giorni;
- `Provincia` = provincia del contraente nel 90% dei casi, altrimenti una provincia vicina;
- `RiparatoreId` valorizzato per le cause casa (80%), `NULL` per le RC;
- ~10% dei sinistri `Aperto`: `ImportoRiservato` valorizzato, `ImportoLiquidato` NULL, `EsitoPerizia` NULL o "Perizia in corso";
- `Chiuso`: `ImportoLiquidato` = importo stimato − franchigia della polizza, mai oltre il massimale, arrotondato a 10 €;
- `Respinto`: `ImportoLiquidato` NULL, esito di perizia che richiama il motivo (e spesso l'articolo di esclusione);
- `Numero` = `SIN-{anno denuncia}-{progressivo:D6}`.

### Copertura garantita per la demo

Oltre alla parte casuale, il generatore **assicura** alcuni casi che gli scenari demo devono trovare:
- almeno 8 sinistri `FenomenoElettrico`, provincia `MI`, `Chiuso`, liquidato > 5.000 €, con descrizioni su sovratensioni, quadri elettrici e inverter (scenario 5);
- almeno 6 sinistri `AcquaCondotta` "tubo rotto / parquet / controsoffitto" chiusi (scenario 1);
- almeno 5 sinistri grandine su pannelli solari, in maggioranza `Respinto` (scenario 2);
- almeno 5 sinistri `ErroreProgettuale` su solai o strutture (scenario 3).

### Quasi-duplicati (10 coppie)

- Per 10 sinistri "originali" si genera un **gemello** con la stessa descrizione riformulata leggermente: sinonimi da un dizionario (`perdita` ↔ `fuoriuscita d'acqua`, `danneggiato` ↔ `rovinato`…), ordine delle frasi invertito, dettaglio secondario cambiato (stanza o data). Riformulazione deterministica, senza LLM.
- Coppie 1–5: **stesso contraente** (gemello su un'altra polizza dello stesso contraente, o sulla stessa).
- Coppie 6–10: **contraenti diversi, stesso riparatore**.
- Entrambi i sinistri di ogni coppia hanno `DataDenuncia` negli **ultimi 10 mesi**, così `fraud-scan --mesi 12` (Fase 7) può trovarli; a distanza di 1–4 mesi l'uno dall'altro.
- File `data/duplicati_attesi.json`, scritto da `DbInit` nella cartella `data/` della radice del repository, trovata risalendo fino al file `.slnx` (si usano i `Numero`, stabili tra un reset e l'altro):
  ```json
  {
    "generatoIl": "2026-09-24",
    "randomSeed": 20260924,
    "coppie": [
      { "originale": "SIN-2026-000123", "duplicato": "SIN-2026-000391", "tipo": "StessoContraente" },
      { "originale": "SIN-2025-000045", "duplicato": "SIN-2026-000402", "tipo": "StessoRiparatore" }
    ]
  }
  ```

⚠️ **Rischio noto:** sinistri generati dallo stesso template sono semanticamente molto vicini anche quando *non* sono duplicati. In Fase 7 questo può far crescere i falsi positivi a soglie alte. Mitigazioni: molti template per causa, slot numerosi, frasi di contorno variabili (1–2 frasi opzionali per descrizione) e, se serve, `--parafrasa-con-llm`. La Fase 7 misura il problema invece di nasconderlo.

### Opzione `--parafrasa-con-llm`

- Passa ogni descrizione (tranne i gemelli, che devono restare riformulazioni controllate) a `IChatClient` con prompt: *"Riscrivi questa descrizione come la scriverebbe un cliente che denuncia un sinistro, in prima persona, 2–4 frasi, senza aggiungere fatti nuovi né importi."*
- Temperatura 0.7, un solo tentativo; in caso di errore si tiene il testo originale.
- Barra di avanzamento a console e stima del tempo (circa 400 chiamate: prevedibilmente 15–30 minuti su RTX 5060).
- Il risultato non è deterministico: lo si dichiara nel riepilogo.

---

## Seed dentro `DbInit` (passo 6, D17)

Il DB è appena stato ricreato, quindi non servono controlli su "dati già presenti" né opzioni `--force`.

1. Esegue `003_seed_clausole.sql`.
2. Genera e inserisce contraenti, riparatori, polizze (prima quelle demo del `DemoCatalog`) e sinistri, in una transazione. Bastano Dapper e `INSERT` a batch: con ~400 righe `SqlBulkCopy` non serve.
3. Con `--parafrasa-con-llm`: parafrasi delle descrizioni prima dell'inserimento (servono Ollama e le manopole `OLLAMA_*`).
4. Scrive `data/duplicati_attesi.json`.
5. Stampa i conteggi per tabella e due tabelle di distribuzione: sinistri per causa × stato, e sinistri per prodotto × provincia (prime 10).

**`DemoCatalog` (Core)**, sul modello di quello di O2C: è la tabella unica dei dati demo. Contiene le 3 polizze demo con i loro contraenti e i 6 scenari di `PLAN.md` §5 (testo, polizza, filtri). Da lì partono il seed, i pulsanti della UI (Fase 8) e i test che verificano la coerenza degli scenari (`DemoCatalogTests`: ogni scenario punta a una polizza demo del prodotto giusto).

Classi coinvolte: (Ingestion) `SyntheticDataGenerator` (orchestratore), `AnagraficheGenerator`, `SinistriGenerator`, `DescrizioneTemplate` e cataloghi di template, `QuasiDuplicatiGenerator`, `DescrizioneParafrasatore` (opzionale, LLM); (Data) `SeedRepository` per gli insert; (Core) `DemoCatalog`.

## Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `SinistriGeneratorTests.Generate_SameSeed_SameOutput` | unit | determinismo (a parità di seed e data di riferimento) |
| `SinistriGeneratorTests.Generate_CausaCoerenteConProdotto` | unit | nessun sinistro RC su polizza casa e viceversa |
| `SinistriGeneratorTests.Generate_DateDentroValiditaPolizza` | unit | `DataEvento` tra decorrenza e scadenza, `DataDenuncia >= DataEvento` |
| `SinistriGeneratorTests.Generate_StatoCoerenteConImporti` | unit | Aperto → liquidato NULL; Respinto → liquidato NULL; Chiuso → liquidato ≤ massimale |
| `QuasiDuplicatiGeneratorTests.Generate_DieciCoppieNegliUltimiDieciMesi` | unit | 5 + 5 coppie, vincoli su contraente/riparatore e date |
| `TemplateTests.OgniSlotHaValori` | unit | nessun template con slot non risolti (`{` residue) |
| `DemoCatalogTests.ScenariCoerenti` | unit | ogni scenario demo punta a una polizza demo del prodotto giusto; numeri di polizza univoci |

## Criteri di completamento

- `dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit` stampa: 150 contraenti, 12 riparatori, ~200 polizze (+3 demo), 60 clausole, ~400 sinistri (+10 gemelli).
- Distribuzione per causa/stato vicina alla tabella dei parametri (±5 punti percentuali); ~10% `Aperto`.
- `data/duplicati_attesi.json` contiene 10 coppie esistenti nel DB.
- Le query di copertura demo (es. `FenomenoElettrico` + `MI` + `Chiuso` + liquidato > 5.000) restituiscono almeno le quantità garantite.
- Build della solution e test verdi.

## Commit proposto (non eseguito)

```
fase 3: clausole di polizza e generatore di dati sintetici
```
