# PLAN — POC "Assistente pre-istruttoria sinistri" (RAG locale .NET + SQL Server 2025 + Ollama)

> Documento di esecuzione pensato per Claude Code.
> Eseguire **una fase alla volta**, rispettando i criteri di completamento e i checkpoint indicati.

---

## 0. Istruzioni per l'agente (leggere prima di iniziare)

- Lavora fase per fase, nell'ordine indicato. Al termine di ogni fase:
  1. esegui `dotnet build` e `dotnet test` (quando esistono test);
  2. verifica i **criteri di completamento** della fase;
  3. fai un commit git con messaggio `fase N: <descrizione>`;
  4. riassumi all'utente cosa è stato fatto e fermati ai **CHECKPOINT** segnalati.
- Non inventare nomi di modelli Ollama: leggi quelli installati con `ollama list` e usa la configurazione.
- La dimensione dei vettori **non va mai hardcodata**: deriva da configurazione (`EmbeddingDimensions`) e deve coincidere con il modello di embedding.
- Prima di operazioni distruttive sul DB (DROP, TRUNCATE, ricreazione database) chiedi conferma, salvo nel comando `db reset` esplicitamente invocato dall'utente.
- Tutti i dati sono **sintetici e fittizi**. Non usare dati reali di persone o compagnie.
- Codice, commenti e nomi di dominio: nomi in italiano per il dominio (Sinistro, Polizza, Clausola), inglese per l'infrastruttura (Repository, Service).
- Se una API di libreria non corrisponde a quanto scritto qui (versioni cambiate), verifica la documentazione/pacchetto installato e adatta, segnalandolo nel riepilogo di fase.

---

## 1. Obiettivo

Costruire un POC che, data una denuncia di sinistro scritta in linguaggio libero e un numero di polizza:

1. recuperi i dati della polizza (SQL classico);
2. trovi le **clausole di polizza pertinenti** (garanzie, esclusioni, franchigie) tramite ricerca vettoriale — *pilastro A, "catalogo semantico"*;
3. trovi i **sinistri storici simili** con ricerca ibrida (vettori + filtri SQL) — *pilastro B, "storico con ricerca ibrida"*;
4. calcoli in SQL le statistiche di liquidazione dei casi simili;
5. faccia generare al modello locale una **scheda di pre-istruttoria** con citazioni;
6. segnali **possibili duplicati/anomalie** (antifrode) tramite soglia di distanza vettoriale.
7. UI web (da usare in una demo live; API minimali per usare la UI web).

### Non-obiettivi
- Nessuna autenticazione, nessun dato reale, nessun deploy, funziona in locale con RTX 5060 8GB VRAM.

---

## 2. Stack e prerequisiti

| Componente | Scelta |
|---|---|
| Runtime | .NET 10 (o l'SDK più recente installato: verificare con `dotnet --list-sdks`) |
| Database | SQL Server 2025 locale, tipo nativo `VECTOR(n)`, `VECTOR_DISTANCE` |
| LLM | Ollama locale, modello chat Qwen ~9B (nome esatto da `ollama list`) |
| Embedding | Ollama, modello multilingua. Default consigliato: `bge-m3` (1024 dim). Alternative: `qwen3-embedding`, `nomic-embed-text` (768 dim, più debole in italiano) |
| Astrazione AI | `Microsoft.Extensions.AI` + `OllamaSharp` (`OllamaApiClient` implementa `IChatClient` e `IEmbeddingGenerator`) |
| Accesso dati | `Microsoft.Data.SqlClient` + `Dapper` |
| CLI | `System.CommandLine` |
| Dati sintetici | `Bogus` (locale `it`) |
| UI | va benissimo con .NET |
| Test | xUnit |

### Verifiche preliminari (Fase 0)
- `dotnet --list-sdks`
- `ollama list` → annotare nome esatto del modello chat e presenza del modello di embedding; se manca: proporre all'utente `ollama pull bge-m3`.
- Test connessione SQL Server: `sqlcmd -S localhost -E -Q "SELECT @@VERSION"` (o equivalente) → deve risultare SQL Server 2025 (versione 17.x).
- Test veloce Ollama: `curl http://localhost:11434/api/embed -d '{"model":"bge-m3","input":"prova"}'` → annotare la lunghezza del vettore restituito.

**CHECKPOINT 0:** riportare all'utente SDK, modelli trovati, dimensione embedding misurata, versione SQL Server. Attendere conferma prima di procedere.

---

## 3. Struttura della solution

```
InsuranceRagPoc/
├─ PLAN.md
├─ README.md
├─ InsuranceRagPoc.sln
├─ db/
│  ├─ 001_create_database.sql
│  ├─ 002_schema.sql
│  └─ 003_seed_clausole.sql
├─ data/
│  └─ golden_set.json            # domande di valutazione con clausole attese
├─ src/
│  ├─ InsuranceRag.Core/         # modelli di dominio, interfacce, DTO scheda
│  ├─ InsuranceRag.Data/         # repository Dapper, query vettoriali/ibride
│  ├─ InsuranceRag.Ai/           # embedding service, chat service, prompt builder
│  ├─ InsuranceRag.Ingestion/    # generatore dati sintetici + calcolo embedding
│  ├─ InsuranceRag.Cli/          # entry point console (System.CommandLine)
│  ├─ InsuranceRag.Api/          # API per web
│  └─ InsuranceRag.Web/          # UI web
└─ tests/
   └─ InsuranceRag.Tests/
```

### Configurazione (`src/InsuranceRag.Cli/appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=InsuranceRagPoc;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ChatModel": "<da ollama list>",
    "EmbeddingModel": "bge-m3",
    "EmbeddingDimensions": 1024
  },
  "Retrieval": {
    "TopClausole": 5,
    "TopSinistri": 10,
    "AnniStorico": 5,
    "SogliaDuplicatoCosine": 0.08
  }
}
```

All'avvio il programma deve verificare che la dimensione dell'embedding restituito da Ollama coincida con `EmbeddingDimensions`, altrimenti errore esplicito.

---

## 4. Fasi

### Fase 1 — Scaffolding
- Creare solution, progetti, riferimenti tra progetti, pacchetti NuGet.
- Configurazione con `Microsoft.Extensions.Hosting` (DI, logging, options pattern).
- Comando CLI `health` che verifica: connessione SQL, raggiungibilità Ollama, esistenza dei due modelli, dimensione embedding.

**Completamento:** `dotnet run --project src/InsuranceRag.Cli -- health` stampa tutti i controlli OK.

---

### Fase 2 — Database e schema
Script in `db/`, eseguibili anche da CLI con `db init` (idempotente) e `db reset` (distruttivo, chiede conferma).

```sql
CREATE TABLE dbo.Contraenti (
  Id INT IDENTITY PRIMARY KEY,
  Nominativo NVARCHAR(200) NOT NULL,
  Provincia CHAR(2) NOT NULL
);

CREATE TABLE dbo.Polizze (
  Id INT IDENTITY PRIMARY KEY,
  Numero NVARCHAR(30) NOT NULL UNIQUE,
  Prodotto NVARCHAR(50) NOT NULL,          -- CASA_FABBRICATI | RC_PROF_TECNICI
  ContraenteId INT NOT NULL REFERENCES dbo.Contraenti(Id),
  Decorrenza DATE NOT NULL,
  Scadenza DATE NOT NULL,
  Massimale DECIMAL(12,2) NOT NULL,
  Franchigia DECIMAL(10,2) NOT NULL
);

CREATE TABLE dbo.Clausole (
  Id INT IDENTITY PRIMARY KEY,
  Prodotto NVARCHAR(50) NOT NULL,
  Articolo NVARCHAR(20) NOT NULL,          -- es. 'Art. 4.2'
  Tipo NVARCHAR(20) NOT NULL,              -- Garanzia | Esclusione | Franchigia | Definizione
  Titolo NVARCHAR(200) NOT NULL,
  Testo NVARCHAR(MAX) NOT NULL,
  Embedding VECTOR(1024) NULL              -- dimensione = EmbeddingDimensions
);

CREATE TABLE dbo.Riparatori (
  Id INT IDENTITY PRIMARY KEY,
  RagioneSociale NVARCHAR(200) NOT NULL
);

CREATE TABLE dbo.Sinistri (
  Id INT IDENTITY PRIMARY KEY,
  PolizzaId INT NOT NULL REFERENCES dbo.Polizze(Id),
  RiparatoreId INT NULL REFERENCES dbo.Riparatori(Id),
  DataEvento DATE NOT NULL,
  DataDenuncia DATE NOT NULL,
  Provincia CHAR(2) NOT NULL,
  Causa NVARCHAR(50) NOT NULL,             -- AcquaCondotta | EventoAtmosferico | FenomenoElettrico | Incendio | Furto | ErroreProgettuale | ...
  Descrizione NVARCHAR(MAX) NOT NULL,
  EsitoPerizia NVARCHAR(MAX) NULL,
  Stato NVARCHAR(20) NOT NULL,             -- Aperto | Chiuso | Respinto
  ImportoRiservato DECIMAL(12,2) NULL,
  ImportoLiquidato DECIMAL(12,2) NULL,
  Embedding VECTOR(1024) NULL
);

CREATE INDEX IX_Sinistri_Filtri ON dbo.Sinistri (Stato, DataEvento) INCLUDE (PolizzaId, Provincia, Causa, ImportoLiquidato);
```

Nota: la dimensione `VECTOR(n)` va generata dallo script/CLI in base a `EmbeddingDimensions` (template con placeholder), non fissata a mano.

**Completamento:** `db init` crea DB e tabelle; rieseguito non dà errori.

---

### Fase 3 — Dati sintetici

#### 3a. Clausole (scritte a mano, `db/003_seed_clausole.sql`)
~60 clausole realistiche ma fittizie, due prodotti:

- **CASA_FABBRICATI** (~35): definizioni (Fabbricato, Contenuto, Scoppio, Acqua condotta…), garanzie (Incendio, Eventi atmosferici, Acqua condotta, Ricerca guasto, Fenomeno elettrico, Furto, RC della proprietà, Cristalli), esclusioni (infiltrazioni per mancata manutenzione, umidità e stillicidio, gelo su impianti non in uso, usura, fabbricati in costruzione, danni da grandine a serramenti/pannelli se non previsto…), franchigie/scoperti per garanzia.
- **RC_PROF_TECNICI** (~25): oggetto dell'assicurazione (RC per errori professionali di ingegneri/architetti), definizioni (Sinistro, Richiesta di risarcimento, Retroattività, Claims made), garanzie (errori di progettazione, direzione lavori, D.Lgs. 81/08 coordinatore sicurezza, perdite patrimoniali), esclusioni (dolo, attività non abilitate, multe e sanzioni, danni noti prima della decorrenza, collaudi di opere proprie…), franchigie/scoperti, massimali per anno.

Ogni clausola: 2–6 frasi, linguaggio assicurativo italiano plausibile. Includere volutamente coppie "garanzia vs esclusione" semanticamente vicine (serve a testare la pertinenza).

#### 3b. Anagrafiche (generatore C# con Bogus, seed deterministico)
- 150 contraenti, 12 riparatori, ~200 polizze (70% CASA_FABBRICATI, 30% RC_PROF_TECNICI), province lombarde/venete/emiliane.

#### 3c. Sinistri (~400)
Generatore C# a **template**: per ogni `Causa` una lista di 8–15 descrizioni base con slot (stanza, materiale, oggetto danneggiato, circostanza, danno) + range realistici di importo e probabilità di esito (Chiuso/Respinto) coerente con le esclusioni (es. infiltrazioni da manutenzione → spesso Respinto).
- Opzione `--parafrasa-con-llm`: passa ogni descrizione a Qwen per riformularla in stile "denuncia del cliente" (lento, facoltativo).
- **Inserire volutamente 10 coppie quasi-duplicate** (stessa descrizione riformulata leggermente), di cui 5 con lo stesso contraente e 5 con contraenti diversi ma stesso riparatore. Registrarne gli Id in `data/duplicati_attesi.json` per la valutazione.
- ~10% dei sinistri in stato `Aperto` (senza importo liquidato).

Comando CLI: `seed [--parafrasa-con-llm]`.

**Completamento:** conteggi a video per ogni tabella; distribuzione per causa/stato sensata.

---

### Fase 4 — Embedding

- `EmbeddingService` basato su `IEmbeddingGenerator<string, Embedding<float>>` (OllamaSharp), elaborazione a batch (es. 16 testi per chiamata), retry semplice.
- Testo da vettorizzare:
  - Clausole: `"{Tipo} - {Titolo}. {Testo}"`
  - Sinistri: `"Causa: {Causa}. {Descrizione} Esito perizia: {EsitoPerizia}"` (esito omesso se nullo)
- Scrittura su SQL: passare il vettore come stringa JSON (`"[0.12, -0.03, ...]"`) e convertire con `CAST(@v AS VECTOR(n))`. Se la versione di `Microsoft.Data.SqlClient` in uso supporta il tipo nativo `SqlVector<float>`, usarlo al suo posto (verificare).
- Comando CLI: `embed [--solo-mancanti]`.

**Completamento:** nessuna riga con `Embedding IS NULL`; tempo totale stampato a video.

---

### Fase 5 — Retrieval (pilastri A e B)

`InsuranceRag.Data` espone:

**A. Clausole pertinenti**
```sql
SELECT TOP (@top) Id, Articolo, Tipo, Titolo, Testo,
       VECTOR_DISTANCE('cosine', Embedding, CAST(@q AS VECTOR(1024))) AS Distanza
FROM dbo.Clausole
WHERE Prodotto = @prodotto
ORDER BY Distanza;
```
Accorgimento: garantire che nei risultati compaiano **almeno 1 esclusione e 1 franchigia** se esistono entro una distanza ragionevole (seconda query mirata per `Tipo` e merge). Il modello deve vedere anche ciò che *limita* la copertura.

**B. Sinistri simili (ricerca ibrida)**
```sql
SELECT TOP (@top) s.Id, s.DataEvento, s.Provincia, s.Causa, s.Descrizione,
       s.EsitoPerizia, s.Stato, s.ImportoLiquidato,
       VECTOR_DISTANCE('cosine', s.Embedding, CAST(@q AS VECTOR(1024))) AS Distanza
FROM dbo.Sinistri s
JOIN dbo.Polizze p ON p.Id = s.PolizzaId
WHERE p.Prodotto = @prodotto
  AND s.Stato IN ('Chiuso','Respinto')
  AND s.DataEvento >= DATEADD(YEAR, -@anni, GETDATE())
  AND (@provincia IS NULL OR s.Provincia = @provincia)
  AND (@importoMin IS NULL OR s.ImportoLiquidato >= @importoMin)
ORDER BY Distanza;
```

**Statistiche (calcolate in SQL, non dall'LLM)** sui sinistri simili restituiti: numero casi, % respinti, min / mediana (`PERCENTILE_CONT(0.5)`) / max liquidato.

Nota: con poche migliaia di righe la ricerca esatta è istantanea. Indice DiskANN + `VECTOR_SEARCH` sono un'estensione opzionale (fase 9).

Comandi CLI di debug: `search-clausole "<testo>" --prodotto X`, `search-sinistri "<testo>" --prodotto X [--provincia MI] [--importo-min 5000]`.

**Completamento:** i comandi restituiscono risultati sensati sulle domande demo (sez. 5).

---

### Fase 6 — Scheda di pre-istruttoria (generazione)

`PreIstruttoriaService.GeneraAsync(string denuncia, string numeroPolizza)`:

1. Carica polizza + contraente (errore chiaro se polizza inesistente o scaduta alla data evento, se indicata).
2. Embedding della denuncia.
3. Retrieval A e B + statistiche.
4. Prompt a Qwen (`IChatClient`) con:
   - **system prompt** in italiano: ruolo di assistente di pre-istruttoria; usa **solo** le clausole fornite; cita sempre l'articolo; se l'informazione non c'è, dichiaralo; non inventare importi; tono professionale e sintetico;
   - contesto: dati polizza, clausole numerate, sinistri simili sintetizzati, statistiche già calcolate;
   - richiesta di output in **JSON** con schema:
     ```json
     {
       "garanzieOperanti": [{"articolo": "", "motivazione": ""}],
       "esclusioniDaVerificare": [{"articolo": "", "cosaVerificare": ""}],
       "franchigiaApplicabile": {"articolo": "", "descrizione": ""},
       "puntiDaChiarireConCliente": [""],
       "valutazioneSintetica": ""
     }
     ```
   - temperatura bassa (0.1–0.2); usare JSON mode/`format` di Ollama se disponibile.
5. **Validazione post-generazione:** ogni `articolo` citato deve appartenere alle clausole recuperate; quelli non validi vengono rimossi e segnalati come warning. Se il JSON non è valido: un retry, poi fallback su testo libero.
6. Rendering in Markdown a console: dati polizza, sezioni della scheda, tabella dei 5 sinistri più simili, box statistiche (da SQL), eventuali warning antifrode (fase 7).

Comando CLI: `ask "<denuncia>" --polizza <numero> [--out scheda.md]`.

**CHECKPOINT 6:** mostrare all'utente le schede generate per le 4 domande demo e chiedere feedback sul prompt prima di proseguire.

---

### Fase 7 — Antifrode (quasi-duplicati)

- Su una nuova denuncia: cercare sinistri con distanza coseno < `SogliaDuplicatoCosine` negli ultimi 24 mesi (qualsiasi prodotto), evidenziando se stesso contraente o stesso riparatore.
- Comando batch `fraud-scan --mesi 12`: self-join vettoriale sui sinistri del periodo, elenco coppie sotto soglia con motivazione (stesso contraente / stesso riparatore / solo testo simile).
- Confronto con `data/duplicati_attesi.json`: stampare precision/recall e suggerire la soglia migliore provando 0.05 / 0.08 / 0.12 / 0.15.

**Completamento:** almeno 8 delle 10 coppie attese individuate con la soglia scelta.

---

### Fase 8 — UI Web

- Devono essere utilizzabili le funzionalità esposte per una DEMO live che sia completa via Web.

**Completamento:** UI web funzionante

---

### Fase 9 — Valutazione e test

- `data/golden_set.json`: 15 denunce di prova, ciascuna con gli articoli attesi (garanzie ed esclusioni rilevanti).
- Comando `eval`: calcola **recall@5** e **MRR** del retrieval delle clausole; salva un report Markdown in `eval/report_<data>.md`.
- Se l'utente ha più modelli di embedding installati, `eval --embedding-model <nome>` permette il confronto (richiede ri-embedding su tabelle/colonne separate o DB separato: scegliere l'approccio più semplice e documentarlo).
- Unit test: prompt builder, validazione articoli citati, parsing JSON, calcolo statistiche (con DB di test o mock del repository).

**Completamento:** `dotnet test` verde; report di valutazione generato.

---

### Fase 10 — Estensioni opzionali (solo su richiesta dell'utente)
1. **Indice vettoriale DiskANN** + `VECTOR_SEARCH` (verificare stato preview e comportamento dei filtri) e confronto tempi vs scansione esatta con 50k sinistri sintetici.
2. **Embedding lato SQL**: `CREATE EXTERNAL MODEL` puntato a Ollama + `AI_GENERATE_EMBEDDINGS` in T-SQL. Verificare i requisiti (es. endpoint HTTPS → potrebbe servire un reverse proxy TLS davanti a Ollama). Confrontare con la pipeline C#.
3. **Minimal API** (`POST /pre-istruttoria`) + pagina HTML semplice.
4. **Ricerca ibrida con full-text**: combinare `CONTAINSTABLE` e distanza vettoriale con Reciprocal Rank Fusion.
5. **Tool calling**: agente che decide se interrogare clausole, storico o statistiche.

---

## 5. Scenari demo (da usare nei checkpoint)

1. *"Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno."* — prodotto CASA_FABBRICATI
2. *"Il cliente dice che la grandine ha rotto i pannelli solari sul tetto."* — CASA_FABBRICATI (attesa tensione tra Eventi atmosferici ed esclusione pannelli/serramenti)
3. *"Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento."* — RC_PROF_TECNICI
4. *"Dopo un temporale si è bruciata la caldaia e il televisore."* — CASA_FABBRICATI (Fenomeno elettrico)
5. Ricerca storico: *"sinistri da fenomeno elettrico in provincia di MI sopra 5.000 € simili a: sovratensione ha danneggiato il quadro elettrico e l'inverter"*
6. `fraud-scan --mesi 12`

---

## 6. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| Qwen 9B cita articoli inesistenti | Validazione post-generazione contro le clausole recuperate |
| Importi inventati dall'LLM | Statistiche calcolate solo in SQL e stampate separatamente |
| Embedding debole in italiano | Modello multilingua (bge-m3), valutazione con golden set |
| JSON non valido | JSON mode Ollama, retry, fallback testuale |
| Dimensione vettore incoerente | Check all'avvio (`health`) e schema generato da config |
| Lentezza generazione locale | Streaming a console, parafrasi LLM dei dati solo opzionale |

---

## 7. Definition of Done del POC
- Da un DB vuoto: `db init` → `seed` → `embed` → `ask ...` produce una scheda leggibile con citazioni valide.
- `fraud-scan` individua la maggior parte dei duplicati inseriti.
- `eval` produce recall@5 ≥ 0.7 sul golden set (obiettivo indicativo, da discutere).
- README con prerequisiti, configurazione, comandi e risultati della valutazione.
