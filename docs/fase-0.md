# Fase 0 — Verifiche preliminari e decisioni trasversali

> Riferimento: `PLAN.md` §2 "Verifiche preliminari" e CHECKPOINT 0.
> Questa fase non produce codice: fissa l'ambiente reale e le decisioni che valgono per tutte le fasi successive.
> L'impianto (Aspire, Razor Pages, DbInit, prefisso, git, CI) è allineato al POC `AI.POC-OrderToCash`, in modo da essere **omogeneo** con quello.
> **Va approvata prima di iniziare la Fase 1.**

---

## 1. Esito delle verifiche (eseguite il 2026-09-24)

| Verifica | Comando | Esito | Stato |
|---|---|---|---|
| SDK .NET | `dotnet --list-sdks` | `10.0.401` | ✅ |
| Modelli Ollama | `ollama list` | solo `qwen3.5:9b` (9.7B, Q4_K_M, 6.6 GB) | ⚠️ manca il modello di embedding |
| Versione Ollama | `ollama --version` | `0.34.4` | ✅ |
| SQL Server su `localhost` | `sqlcmd -S localhost -E` | **non raggiungibile**: non c'è un'istanza SQL Server installata come servizio (c'è solo `SQLWriter`) | ❌ (non serve, vedi D1) |
| SQL Server LocalDB `localdev` | `sqlcmd -S "(localdb)\localdev" -E` | `SQL Server 2025 (RTM-CU3) 17.0.4025.3, Express Edition` | ✅ |
| Tipo `VECTOR` su LocalDB | `SELECT VECTOR_DISTANCE('cosine', CAST('[1,0,0]' AS VECTOR(3)), CAST('[0.9,0.1,0]' AS VECTOR(3)))` | `0.00611…` | ✅ |
| CPU / RAM | `Win32_Processor`, `Win32_ComputerSystem` | AMD Ryzen AI 7 350 (Radeon 860M), 31 GB RAM | ✅ |
| GPU | `nvidia-smi` | RTX 5060 Laptop, 8151 MiB | ✅ |
| NPU | `Get-PnpDevice` | "NPU Compute Accelerator Device" (XDNA 2), driver 32.0.203.329, stato OK; **nessun runtime** che la usi (FastFlowLM, Lemonade, Foundry Local assenti) | ℹ️ vedi §2 bis |
| Dimensione embedding misurata | `POST /api/embed` con `bge-m3` | **non misurabile**: il modello non è installato | ⏳ |
| `bge-m3` installato (2026-09-25) | `ollama pull bge-m3` | `bge-m3:latest` (1.2 GB), dimensione misurata **1024** | ✅ |
| Embedding su CPU accanto alla chat (D18) | `ollama ps` con `num_gpu: 0` | `bge-m3` **100% CPU**, `qwen3.5:9b` **100% GPU** (5.5 GB), caricati insieme | ✅ |
| Latenza embedding su CPU | `total_duration` di Ollama | ~20 ms per frase breve, ~660 ms per un batch da 16 | ✅ |
| Endpoint `localhost` vs `127.0.0.1` | `Invoke-RestMethod` / `HttpClient` | `localhost` costa **~2 s per ogni nuova connessione** (tentativo IPv6 prima di IPv4); con `127.0.0.1` 35 ms. Default dell'endpoint: **`http://127.0.0.1:11434`** | ⚠️ gestito |

### Azioni richieste all'utente prima della Fase 1

1. **Scaricare il modello di embedding**: `ollama pull bge-m3` (~1.2 GB). Dopo il download misurare la dimensione:
   ```powershell
   (Invoke-RestMethod -Method Post -Uri http://localhost:11434/api/embed -Body '{"model":"bge-m3","input":"prova"}' -ContentType 'application/json').embeddings[0].Count
   ```
   Valore atteso: `1024`, da usare come `EMBEDDING_DIMENSIONS`.
   `bge-m3` è il default provvisorio della Fase 1: la scelta definitiva del modello di embedding arriva dal banco di prova della **Fase 1b**.
2. **Verificare l'embedding su CPU** (decisione D18): stessa chiamata con `"options":{"num_gpu":0}` e controllo con `ollama ps` che `bge-m3` risulti `100% CPU` mentre `qwen3.5:9b` resta sulla GPU.
3. **Confermare le decisioni della sezione 2** e rispondere alle domande aperte della sezione 3 (restano Q5 e Q6).
4. **Autorizzare in anticipo le installazioni della Fase 1b** (oppure rimandare la decisione a quel momento): pull di 3 modelli Ollama aggiuntivi (~4–5 GB in totale) e installazione di FastFlowLM (`flm-setup.msi`).

---

## 2. Decisioni trasversali (differenze rispetto a `PLAN.md`)

Ogni riga va confermata o rifiutata in review. La colonna "Fonte" indica da dove nasce la scelta.

| # | Argomento | `PLAN.md` dice | Proposta | Fonte |
|---|---|---|---|---|
| D1 | Server SQL | `Server=localhost` | `(localdb)\localdev`, database **`Sinistri`**; connection string di nome **`sql`** negli **user-secrets dell'AppHost** (come `O2C`) | ambiente + O2C |
| D2 | Nomi tabella | plurali (`Contraenti`, `Polizze`…) | **singolari**: `Contraente`, `Polizza`, `Clausola`, `Riparatore`, `Sinistro` | skill `db-operations` §4 |
| D3 | Colonne "enum" (`Prodotto`, `Tipo`, `Causa`, `Stato`) | `NVARCHAR` | **FK verso tabelle di lookup** (PK `tinyint` = valore esplicito dell'enum, `Name` univoco, righe generate dall'enum) | `db-operations` §4 |
| D4 | Chiave di business del sinistro | assente | colonna `Sinistro.Numero` (`SIN-2025-000123`, univoca) | stabilità di `duplicati_attesi.json` |
| D5 | Framework di test | xUnit | **NUnit 4** (`Assert.That`) + NUnit.Analyzers, Microsoft.Testing.Platform, marcatori `//SETUP` / `//SUT` | preferenza utente + O2C |
| D6 | Commit a fine fase | commit automatico | **nessun commit automatico**: si propone il testo; nessuna riga di attribuzione a Claude | preferenza utente |
| D7 | Radice della solution | cartella `InsuranceRagPoc/` | la cartella esistente `AI.POC-AssistenteSinistri` è la radice | — |
| D8 | Nomi e formato solution | `InsuranceRag.*`, `.sln` | **`Dusiburg.AI.Sinistri.*`**, `Dusiburg.AI.Sinistri.slnx`, cartelle `src/`, `tests/`, `tools/`, `docs/`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` | O2C (Q1) |
| D9 | Filtro per causa nella ricerca storico | assente | parametro opzionale `@causaId` / `--causa` | scenario demo 5 |
| D10 | "Almeno 1 esclusione e 1 franchigia" | seconda query + merge | **una sola query** con `ROW_NUMBER()` per tipo e soglia di distanza configurabile | semplicità |
| D11 | Statistiche sui simili | "sui sinistri simili restituiti" | query che riceve gli `Id` dei simili (`OPENJSON`) | coerenza tabella/statistiche |
| D12 | Configurazione | `appsettings.json` della CLI | **"manopole" impostate sull'AppHost** (user-secrets, riga di comando o variabili d'ambiente) e inoltrate ai servizi con `WithConfigurationEnvironment`, con default nel codice (come `ModelOptions` di O2C); la CLI legge gli stessi user-secrets dell'AppHost (`AppHostSecrets`) | O2C |
| D13 | Test delle query vettoriali | — | DB dedicato `Sinistri_Test` su `localdev` con **vettori a 4 dimensioni** scritti a mano | test deterministici senza Ollama |
| D14 | Fase 10 punto 3 (Minimal API + HTML) | estensione opzionale | già coperto dalla Fase 8 | — |
| D15 | Avvio e osservabilità | — | **AppHost Aspire** (`Dusiburg.AI.Sinistri.AppHost`) + **`ServiceDefaults`** (OpenTelemetry, health check, service discovery): API e Web partono da lì, i passi della pre-istruttoria (embedding, SQL, LLM) sono span visibili nel dashboard | O2C (Q3) |
| D16 | UI web | "va benissimo con .NET" | **Razor Pages** con client HTTP tipizzato verso l'API (come `Crm.Web` / `Erp.Web`) | O2C (Q2) |
| D17 | Creazione del DB e seed | comandi CLI `db init` (idempotente), `db reset`, `seed` | tool **`tools/Dusiburg.AI.Sinistri.DbInit`** che ricrea sempre il DB da zero (schema + lookup + clausole + dati sintetici), con `--no-seed` e `--allow-non-local` come in O2C; niente `db init` idempotente | O2C |
| D18 | Posizionamento dei modelli su 8 GB di VRAM | — | chat `qwen3.5:9b` sempre su **GPU**; l'embedding **mai sulla GPU**, per non contendere la VRAM. Il chip (**NPU** o **CPU**) e il modello (`bge-m3` o uno più potente) si scelgono con il **banco di prova della Fase 1b**; fino ad allora default `bge-m3` su CPU | VRAM + NPU + richiesta utente |
| D19 | Diagnostica dei prompt | — | `PromptCaptureChatClient` come in O2C: con `SINISTRI_PROMPT_CAPTURE_DIR` valorizzato ogni richiesta al modello viene salvata su file | O2C |
| D20 | Repository, CI e documentazione | commit per fase | `git init` con rami **`main` e `develop`** (git flow come O2C), `.github/workflows/ci.yml` su Windows con LocalDB, `README.md` (EN) + `README.it.md`, cartella `docs/` (`architettura.md`, `demo.md`), `LICENSE` | O2C (Q4) |

---

## 2 bis. CPU, GPU e NPU: chi fa cosa

| Chip | Punto di forza | Limite | Runtime sul PC |
|---|---|---|---|
| GPU RTX 5060 | velocissima sui modelli grandi (chat 9B) | 8 GB di VRAM | Ollama |
| CPU Ryzen AI 7 350 | esegue qualsiasi modello, 31 GB di RAM | più lenta; per un embedding piccolo va bene | Ollama |
| NPU XDNA 2 | consumi minimi, lavora in parallelo agli altri due | solo modelli compilati per l'NPU, catalogo limitato | nessuno, **da installare** |

- **Ollama non usa l'NPU AMD.** Per usarla serve un secondo runtime accanto a Ollama. Il **driver NPU c'è già** (32.0.203.329; FastFlowLM richiede ≥ 32.0.203.311). Mancano il runtime e un modello di embedding compilato per l'NPU.
- **Percorsi NPU individuati** (dettaglio in `fase-1b.md`):
  - **FastFlowLM**: server OpenAI-compatibile, pronto subito, ma offre solo **EmbeddingGemma-300m** (768 dimensioni) e carica l'embedding solo insieme a un piccolo modello di chat;
  - **Windows ML / ONNX Runtime con Vitis AI EP**: dentro il processo .NET, con qualsiasi modello ONNX quantizzato per l'NPU (per esempio Qwen3-Embedding-0.6B), ma con più lavoro e più incertezza;
  - **Lemonade** (AMD): da verificare se gli embedding girano davvero sull'NPU.
- **Modelli più potenti di `bge-m3`** (MMTEB 59.6): Qwen3-Embedding-0.6B (64.3, stesso peso), Qwen3-Embedding-4B (69.5, riducibile a 1024/1536 dimensioni con MRL), multilingual-e5-large-instruct (63.2), EmbeddingGemma-300m (61.2).
- **Vincolo fisso:** la chat resta sulla RTX 5060 e l'embedding non va mai in VRAM, così nessun modello viene scaricato durante la demo.
- Cambiare modello o runtime **cambia i vettori** (anche a parità di nome, per quantizzazioni diverse): si rifà `embed` completo; `EmbeddingInfo` (Fase 2) impedisce di mescolarli.

La scelta si fa con dati misurati nel **banco di prova della Fase 1b** (qualità sul nostro dominio, latenza, convivenza con la chat, correttezza dei vettori NPU confrontati con la CPU).

---

## 3. Domande aperte

### Risolte guardando AI.POC-OrderToCash

| # | Domanda | Risposta |
|---|---|---|
| Q1 | Prefisso dei progetti | `Dusiburg.AI.Sinistri.*` (D8) |
| Q2 | Tecnologia della UI | Razor Pages (D16) |
| Q3 | Avvio demo | AppHost Aspire (D15) |
| Q4 | Repository git | sì: `main` + `develop`, CI, README bilingue (D20) |
| Q7 | Esperimento NPU | **sì, anticipato**: diventa la Fase 1b (banco di prova embedding su CPU e NPU + ricerca di un modello più potente), prima dello schema |

### Ancora aperte

| # | Domanda | Default proposto se non si risponde |
|---|---|---|
| Q5 | Soglia recall@5 della Definition of Done (≥ 0.7 "da discutere"): va bene? | sì |
| Q6 | O2C crea lo schema con **EF Core** (`EnsureCreated`, lookup con `HasData`), il piano con **script SQL + Dapper**. Allinearsi a EF anche qui? | **no**: si tengono script SQL e Dapper, perché il cuore della demo sono le query T-SQL vettoriali e ibride, che devono restare leggibili così come sono. Il DB si crea comunque con il tool `DbInit` (D17), come in O2C |

---

## 4. Regole operative valide per tutte le fasi

- Una fase alla volta; ogni fase si chiude con:
  1. build della **solution completa** (`dotnet build Dusiburg.AI.Sinistri.slnx`), mai del singolo progetto (skill `verify-build`);
  2. `dotnet test` quando esistono test;
  3. verifica dei criteri di completamento scritti nel file della fase;
  4. riepilogo all'utente + **testo del commit proposto** (non eseguito), in italiano, oggetto `fase N: <descrizione>`.
- Stop obbligatorio ai CHECKPOINT: **0** (questo file), **1b** (scelta di modello e chip dell'embedding), **6** (schede delle 4 domande demo). Stop "morbido" a fine Fase 3 (revisione dei testi delle clausole).
- Nomi: dominio in italiano (`Sinistro`, `Polizza`, `Clausola`, `PreIstruttoriaService`), infrastruttura in inglese (`Repository`, `Service`, `Options`).
- Metodi leggibili in una schermata (50–100 righe al massimo), nomi espliciti, `var` solo quando il tipo è evidente (mai sul ritorno di un metodo). Commenti in italiano con i riferimenti alle decisioni (`D12`, …) come in O2C.
- Nessun nome di modello Ollama hardcoded nel codice applicativo: default in un'unica classe di opzioni, sovrascrivibili dall'AppHost.
- Nessuna dimensione vettoriale implicita: sempre `EMBEDDING_DIMENSIONS` (anche nel testo SQL generato).
- Operazioni distruttive sul DB solo tramite `DbInit`, che rifiuta server diversi da LocalDB senza `--allow-non-local`.
- Tutti i dati sono sintetici; nomi di persone e ragioni sociali generati da Bogus (locale `it`), nessuna compagnia reale.
- Se un'API di libreria differisce da quanto scritto nei file di fase, si adatta e lo si segnala nel riepilogo di fase.

---

## 5. Indice delle fasi

| File | Fase | Checkpoint |
|---|---|---|
| `fase-0.md` | Verifiche e decisioni (questo file) | **CHECKPOINT 0** |
| `fase-1.md` | Scaffolding: solution, AppHost, ServiceDefaults, configurazione, `health` | — |
| `fase-1b.md` | **Banco di prova degli embedding**: modelli (bge-m3, Qwen3-Embedding, EmbeddingGemma, e5) × chip (CPU, NPU con FastFlowLM, NPU con Windows ML) | **CHECKPOINT 1b** |
| `fase-2.md` | Database, schema, lookup, tool `DbInit` | — |
| `fase-3.md` | Dati sintetici: clausole, anagrafiche, sinistri, duplicati attesi | stop "morbido" sui testi |
| `fase-4.md` | Embedding: servizio, scrittura vettori, comando `embed` | — |
| `fase-5.md` | Retrieval: clausole (pilastro A), storico ibrido (pilastro B), statistiche | — |
| `fase-6.md` | Scheda di pre-istruttoria: prompt, JSON, validazione, rendering, comando `ask` | **CHECKPOINT 6** |
| `fase-7.md` | Antifrode: controllo sulla nuova denuncia, `fraud-scan`, taratura soglia | — |
| `fase-8.md` | API + UI Razor Pages per la demo live, sotto Aspire | — |
| `fase-9.md` | Golden set, `eval`, test, README, docs, CI | — |
| `fase-10.md` | Estensioni opzionali (solo su richiesta), incluso il reranker su NPU | — |

**CHECKPOINT 0 — superato il 2026-09-25:**
- (a) `bge-m3` installato, dimensione 1024, su CPU accanto a `qwen3.5:9b` su GPU;
- (b) D1–D20 confermate in blocco;
- (c) Q5: sì, recall@5 ≥ 0.7; Q6: script SQL + Dapper;
- (d) Fase 1b autorizzata per **FastFlowLM** e **modelli AMD ONNX** (`amd/bge-m3-onnx`, `amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K`, Ryzen AI Software 1.8 se serve). **Non** autorizzati: i modelli Ollama aggiuntivi (Qwen3-Embedding, EmbeddingGemma, e5) e `Qwen3.5-9B-NPU2`.

Testo originale del checkpoint: attendere (a) il pull di `bge-m3` con la dimensione misurata e la prova su CPU, (b) la conferma delle decisioni D1–D20, (c) le risposte a Q5–Q6, (d) l'autorizzazione (anche differita) alle installazioni della Fase 1b.
