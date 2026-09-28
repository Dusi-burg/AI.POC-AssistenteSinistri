# Fase 1 — Scaffolding

> Riferimento: `PLAN.md` §3 e §4 Fase 1. Prerequisito: CHECKPOINT 0 approvato.
> Impianto allineato a `AI.POC-OrderToCash` (decisioni D8, D12, D15, D20 di `fase-0.md`).

## Obiettivo

Solution compilabile con tutti i progetti collegati, AppHost Aspire funzionante, ServiceDefaults, configurazione tipizzata e validata, e il comando `health` che controlla SQL, Ollama, modelli e dimensione dell'embedding.

---

## 1. Struttura dei file prodotti

```
AI.POC-AssistenteSinistri/
├─ PLAN.md, fase-0.md … fase-10.md
├─ README.md, README.it.md           # scheletro, completati in Fase 9
├─ LICENSE                           # stessa licenza di O2C
├─ Dusiburg.AI.Sinistri.slnx
├─ global.json                       # sdk 10.0.400 rollForward latestFeature; test.runner = Microsoft.Testing.Platform
├─ Directory.Build.props             # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel latest
├─ Directory.Packages.props          # versioni NuGet centralizzate
├─ .editorconfig, .gitignore, .gitattributes   # copiati da O2C
├─ .github/workflows/ci.yml          # Fase 9
├─ db/                               # script SQL (Fase 2)
├─ data/                             # golden set e duplicati attesi (Fasi 3 e 9)
├─ docs/                             # architettura.md, demo.md (Fase 9)
├─ src/
│  ├─ Dusiburg.AI.Sinistri.AppHost/
│  ├─ Dusiburg.AI.Sinistri.ServiceDefaults/
│  ├─ Dusiburg.AI.Sinistri.Core/
│  ├─ Dusiburg.AI.Sinistri.Data/
│  ├─ Dusiburg.AI.Sinistri.Ai/
│  ├─ Dusiburg.AI.Sinistri.Ingestion/
│  ├─ Dusiburg.AI.Sinistri.Cli/
│  ├─ Dusiburg.AI.Sinistri.Api/       # creato ora con /health, riempito in Fase 8
│  └─ Dusiburg.AI.Sinistri.Web/       # creato ora con /health, riempito in Fase 8
├─ tests/
│  ├─ Dusiburg.AI.Sinistri.Tests/     # unit + integrazione di Core, Data, Ai, Ingestion
│  └─ Dusiburg.AI.Sinistri.Web.Tests/ # endpoint API e pagine Web (Fase 8)
└─ tools/
   ├─ Dusiburg.AI.Sinistri.DbInit/    # creato ora vuoto, implementato in Fase 2
   └─ Dusiburg.AI.Sinistri.EmbeddingBench/  # banco di prova degli embedding (Fase 1b)
```

`Directory.Build.props`, `global.json`, `.editorconfig`, `.gitignore` e `.gitattributes` si copiano da `AI.POC-OrderToCash` così come sono.

---

## 2. Progetti, responsabilità e riferimenti

| Progetto | Tipo | Responsabilità | Riferimenti a progetti |
|---|---|---|---|
| `AppHost` | Aspire AppHost (`Aspire.AppHost.Sdk`) | Avvia `api` e `web`; dichiara `sql` come connection string esterna; inoltra le manopole ai servizi | Api, Web |
| `ServiceDefaults` | classlib | `AddServiceDefaults()` (OpenTelemetry, health check, resilienza, service discovery), `MapDefaultEndpoints()`; come in O2C, **senza** la parte di correlation id MCP (qui non serve) | — |
| `Core` | classlib | Enum di dominio, record, DTO della scheda, interfacce (`IPolizzaRepository`, `IClausolaRepository`, `ISinistroRepository`, `IEmbeddingService`, `IHealthProbe`…), opzioni, `DemoCatalog` (polizze e scenari demo, come `DemoCatalog` di O2C), **servizi applicativi** che orchestrano solo tramite interfacce (`HealthService`, `RicercaService`, `PreIstruttoriaService`, `AntifrodeService`, `EvalService`), nomi delle sorgenti di telemetria (`SinistriTelemetry`) | — |
| `Data` | classlib | `SqlConnectionFactory`, repository Dapper, query vettoriali e ibride, esecuzione script SQL, probe SQL | Core |
| `Ai` | classlib | `ModelClientFactory` (chat) ed `EmbeddingGeneratorFactory` su Ollama, `EmbeddingService`, `PromptBuilder`, parser/validatore della scheda, `PromptCaptureChatClient`, probe Ollama | Core |
| `Ingestion` | classlib | Generatore dati sintetici (Bogus), template, duplicati attesi, pipeline di embedding | Core, Data, Ai |
| `Cli` | console | `System.CommandLine`: `health`, `embed`, `search-*`, `ask`, `fraud-scan`, `eval`; legge gli user-secrets dell'AppHost (`AppHostSecrets`) e invia la telemetria al dashboard se è in esecuzione | Core, Data, Ai, Ingestion, ServiceDefaults |
| `Api` | web (minimal API) | Endpoint REST per la UI (Fase 8), `/health` | Core, Data, Ai, ServiceDefaults |
| `Web` | web (Razor Pages) | UI della demo (Fase 8), `/health` | Core (DTO), ServiceDefaults |
| `DbInit` (tools) | console | Ricrea il DB, applica lo schema, popola lookup, clausole e dati sintetici (Fasi 2–3) | Core, Data, Ingestion |
| `EmbeddingBench` (tools) | console | Banco di prova degli embedding (Fase 1b): stessi casi su più modelli e percorsi, report comparativo | Core, Ai |
| `Tests` | NUnit (MTP, `OutputType Exe`, `EnableNUnitRunner`) | Unit e integrazione | Core, Data, Ai, Ingestion, DbInit |
| `Web.Tests` | NUnit (MTP) | `WebApplicationFactory` su Api e Web | Api, Web |

Scelte:
- I servizi applicativi stanno in `Core` perché dipendono solo da interfacce; `Data` e `Ai` restano indipendenti tra loro.
- La CLI resta un progetto a sé, invece di essere la modalità "con argomenti" di un servizio come l'`Orchestrator` di O2C. Qui non c'è un worker da riusare, e i comandi di sviluppo (`embed`, `eval`) non devono finire nel processo dell'API.

---

## 3. Pacchetti NuGet (in `Directory.Packages.props`)

Si riprendono le versioni di O2C dove il pacchetto è lo stesso; per gli altri si usa l'**ultima stabile** al momento dello sviluppo (verificata con `dotnet package search`, le versioni non si inventano).

| Pacchetto | Versione di partenza | Usato da |
|---|---|---|
| `Aspire.AppHost.Sdk` (SDK del progetto) | 13.5.4 | AppHost |
| `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery` | 10.10.0 | ServiceDefaults |
| `OpenTelemetry.*` (Exporter OTLP, Extensions.Hosting, Instrumentation AspNetCore/Http/Runtime) | 1.18.0 | ServiceDefaults |
| `Microsoft.Extensions.Hosting` | 10.0.12 | Cli, DbInit |
| `Microsoft.Extensions.AI` | 10.10.0 | Ai |
| `OllamaSharp` | 5.4.30 | Ai |
| ~~`Microsoft.Extensions.AI.OpenAI`~~ | — | non usato: il provider `openai-compatible` è un client HTTP minimo in `Ai` (Fase 1b: il client OpenAI chiede i vettori in base64) |
| `Microsoft.Windows.AI.MachineLearning` (Windows ML, self-contained) + `Microsoft.ML.Tokenizers` | 2.4.89 / 2.0.0 | per ora solo `tools/…EmbeddingBench` (Fase 1b, P3a) |
| `System.CommandLine` | 2.0.12 | Cli |
| `Microsoft.Data.SqlClient` | ultima stabile (≥ 6.1, per `SqlVector<float>`) | Data |
| `Dapper` | ultima stabile | Data |
| `Bogus` | ultima stabile | Ingestion |
| `Microsoft.AspNetCore.OpenApi` | allineata a ASP.NET Core 10 | Api |
| `NUnit` / `NUnit3TestAdapter` / `NUnit.Analyzers` | 4.6.1 / 6.3.0 / 4.15.0 | Tests, Web.Tests |
| `Microsoft.Testing.Extensions.CodeCoverage` | 18.11.2 | Tests, Web.Tests |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | Web.Tests |

---

## 4. AppHost e configurazione

### `AppHost.cs`

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Risorse esterne (D1, D15): nessun container. Valore negli user-secrets dell'AppHost:
// ConnectionStrings:sql = Server=(localdb)\localdev;Database=Sinistri;Integrated Security=True;TrustServerCertificate=True
var sql = builder.AddConnectionString("sql");

// Porte fisse dai launchSettings: Api 5201, Web 5202 (O2C usa 5101-5106, così le due demo possono convivere).
var api = builder.AddProject<Projects.Dusiburg_AI_Sinistri_Api>("api")
    .WithReference(sql)
    .WithHttpHealthCheck("/health");

// Manopole della demo (D12): si impostano sull'AppHost e arrivano all'API; senza valore vale il default del codice.
api.WithConfigurationEnvironment(
    builder,
    "OLLAMA_ENDPOINT",
    "OLLAMA_CHAT_MODEL",
    "OLLAMA_NUM_CTX",
    "EMBEDDING_PROVIDER",
    "EMBEDDING_ENDPOINT",
    "EMBEDDING_MODEL",
    "EMBEDDING_NUM_GPU",
    "EMBEDDING_ONNX_PATH",
    "EMBEDDING_DIMENSIONS",
    "SOGLIA_DUPLICATO_COSINE",
    "SINISTRI_PROMPT_CAPTURE_DIR",
    "SINISTRI_WARMUP");

builder.AddProject<Projects.Dusiburg_AI_Sinistri_Web>("web")
    .WithReference(api)            // service discovery: la Web chiama http://api
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

`WithConfigurationEnvironment` è la stessa estensione dell'AppHost di O2C. Da valutare in sviluppo: rendere Ollama visibile nel dashboard come risorsa esterna con health check (API di Aspire per i servizi esterni, da verificare sulla versione 13.5). Così un Ollama spento si vede subito in rosso.

### Manopole e default (`Core/Options`)

Come `ModelOptions` di O2C: un record costruito da `IConfiguration` con **default nel codice** e validazione esplicita, messaggi d'errore in italiano.

| Chiave | Default | Note |
|---|---|---|
| `OLLAMA_ENDPOINT` | `http://localhost:11434` | |
| `OLLAMA_CHAT_MODEL` | `qwen3.5:9b` | nome da `ollama list` (Fase 0) |
| `OLLAMA_NUM_CTX` | `8192` | con 8 GB Ollama sceglierebbe 4096, troppo poco per il prompt della scheda (~3–4k token + risposta); O2C usa 16384 per via dei tool, qui 8192 basta e lascia VRAM |
| `EMBEDDING_PROVIDER` | `ollama` | `ollama`, `openai-compatible` (FastFlowLM, Lemonade) oppure `onnx` (Windows ML / ONNX Runtime nel processo); scelto in Fase 1b |
| `EMBEDDING_ENDPOINT` | vuoto | per `ollama` vale `OLLAMA_ENDPOINT`; per `openai-compatible` es. `http://127.0.0.1:52625/v1` |
| `EMBEDDING_MODEL` | `bge-m3` | default provvisorio fino al CHECKPOINT 1b |
| `EMBEDDING_ONNX_PATH` | vuoto | solo `onnx`: cartella con modello e tokenizer |
| `EMBEDDING_NUM_GPU` | `0` | solo `ollama`: D18, embedding mai in VRAM; valore vuoto = scelta automatica di Ollama |
| `EMBEDDING_DIMENSIONS` | `1024` | fissato al CHECKPOINT 1b in base al modello scelto; intervallo valido 1–1998 (limite di `VECTOR` in `float32`, da riverificare) |
| `SOGLIA_DUPLICATO_COSINE` | `0.08` | tarata in Fase 7 |
| `SINISTRI_PROMPT_CAPTURE_DIR` | vuoto | D19 |
| `SINISTRI_WARMUP` | `true` | warm-up dei modelli all'avvio dell'API (Fase 8) |

Parametri di retrieval meno "da demo" in `appsettings.json` di Api e Cli, sezione `Retrieval` (`RetrievalOptions` con `ValidateOnStart`):

```json
"Retrieval": {
  "TopClausole": 5,
  "DistanzaMaxClausolaIntegrativa": 0.45,
  "TopSinistri": 10,
  "SinistriNelPrompt": 5,
  "AnniStorico": 5,
  "MesiControlloDuplicati": 24,
  "EmbeddingBatchSize": 16
},
"Seed": { "RandomSeed": 20260924 }
```

### CLI e DbInit fuori dall'AppHost

- La CLI in Development legge dagli **user-secrets dell'AppHost** la connection string `sql` e, se il dashboard è attivo, l'endpoint OTLP (`AppHostSecrets.AddAppHostSecretsForCli`, stesso schema di O2C). Così i segreti stanno in un solo posto e le tracce di `ask` compaiono nel dashboard.
- `DbInit` segue O2C: connection string da argomento, poi `ConnectionStrings__sql` dall'ambiente, poi il default `(localdb)\localdev`, database `Sinistri`.

---

## 5. Composizione DI

```csharp
builder.AddServiceDefaults();                    // Api, Web, Cli
services.AddSinistriCore(configuration);         // opzioni + servizi applicativi
services.AddSinistriData(configuration);         // SqlConnectionFactory (connection string "sql"), repository, probe SQL
services.AddSinistriAi(configuration);           // IChatClient, IEmbeddingGenerator, EmbeddingService, probe Ollama
services.AddSinistriIngestion(configuration);    // solo Cli e DbInit
```

Client Ollama in `Ai`, sul modello di `ModelClientFactory` di O2C:
- `OllamaApiClient` con `HttpClient` **dedicato** (timeout 10 minuti, senza la resilienza standard di ServiceDefaults, i cui timeout da 10 s non reggono la generazione locale: stessa lezione di O2C);
- chat: `.AsBuilder().UseLogging().UseOpenTelemetry(...)` + eventuale `PromptCaptureChatClient`; opzioni di default `Temperature = 0.1`, `AddOllamaOption(OllamaOption.Think, false)`, `AddOllamaOption(OllamaOption.NumCtx, …)` (API già usate in O2C);
- embedding: `EmbeddingGeneratorFactory` crea l'`IEmbeddingGenerator<string, Embedding<float>>` in base a `EMBEDDING_PROVIDER`: `OllamaApiClient` (con `num_gpu` da `EMBEDDING_NUM_GPU`), generatore OpenAI-compatibile di `Microsoft.Extensions.AI` verso `EMBEDDING_ENDPOINT`, oppure generatore ONNX (Windows ML / ONNX Runtime con Vitis AI EP). Il resto del codice vede solo l'interfaccia (vedi `fase-1b.md` e `fase-4.md`).

---

## 6. Comando `health` (e `/health` dell'API)

`dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- health`

`HealthService` (Core) esegue i probe e restituisce `HealthCheckResult(string Nome, HealthStatus Stato, string Dettaglio, TimeSpan Durata)`. Gli stessi probe sono registrati come **health check ASP.NET** nell'API, così il dashboard di Aspire mostra l'API come "unhealthy" se Ollama o SQL non rispondono.

| # | Controllo | Implementazione | OK se |
|---|---|---|---|
| 1 | Connessione SQL | `SqlHealthProbe`: connessione a `master` sullo stesso server, `@@VERSION` | connessione riuscita |
| 2 | Versione SQL Server | `SERVERPROPERTY('ProductMajorVersion')` | `>= 17` |
| 3 | Supporto `VECTOR` | `SELECT VECTOR_DISTANCE('cosine', CAST('[1,0]' AS VECTOR(2)), CAST('[0,1]' AS VECTOR(2)))` | nessuna eccezione |
| 4 | Database applicativo | esistenza del DB `Sinistri` | **Warning** se non esiste: lo crea `DbInit` (Fase 2) |
| 5 | Ollama raggiungibile | `GET /api/version` | HTTP 200 |
| 6 | Modello chat presente | `GET /api/tags` vs `OLLAMA_CHAT_MODEL` | presente (altrimenti suggerisce `ollama pull <nome>`) |
| 7 | Modello embedding presente | per provider: `ollama` → `/api/tags`; `openai-compatible` → `GET /v1/models`; `onnx` → file presenti e caricabili | presente |
| 8 | Dimensione embedding | embedding di `"prova"`, lunghezza vs `EMBEDDING_DIMENSIONS` | uguali; altrimenti **errore esplicito** con i due valori |
| 9 | Dimensione colonne `VECTOR` e modello degli embedding nel DB | *(dalla Fase 2)* metadati delle colonne + `EmbeddingInfo` | coerenti con la configurazione |
| 10 | Posizionamento dei modelli | `GET /api/ps` di Ollama; per `onnx` l'execution provider effettivo (NPU o CPU); per `openai-compatible` il runtime dichiarato | informativo: chat su GPU, embedding su CPU o NPU (D18) |

Codice di uscita CLI: `0` se tutti OK o Warning, `1` se almeno un errore.

**Controllo all'avvio (requisito `PLAN.md` §3):** i comandi che usano l'embedding (`embed`, `ask`, `search-*`, `eval`) eseguono i controlli 7–9 e si fermano con un errore esplicito se non tornano. L'API li esegue all'avvio: se falliscono parte comunque, ma il suo health check è rosso e gli endpoint AI rispondono 503 con il messaggio.

---

## 7. Passi di esecuzione

1. `git init`, rami `main` → `develop` (si lavora su `develop`); file di impianto copiati da O2C.
2. `dotnet new`: aspire-apphost, aspire-servicedefaults, classlib, console, web, webapp (Razor Pages), nunit; `dotnet new sln --format slnx`, cartelle `/src/`, `/tests/`, `/tools/` come in O2C.
3. Opzioni, estensioni DI, `SqlConnectionFactory`, probe, `HealthService`, health check ASP.NET.
4. AppHost con `sql`, `api`, `web`; user-secrets inizializzati (`dotnet user-secrets set ConnectionStrings:sql "..." --project src/Dusiburg.AI.Sinistri.AppHost`).
5. CLI: `RootCommand` con `health` e l'opzione globale `--verbose`; `AppHostSecrets`.
6. Test minimi, build della solution, test.

## 8. Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `SinistriOptionsTests.FromConfiguration_Default` | unit | senza configurazione valgono i default |
| `SinistriOptionsTests.FromConfiguration_DimensioneNonValida_Errore` | unit | `EMBEDDING_DIMENSIONS=0` o `abc` → errore esplicito |
| `HealthServiceTests` | unit, con probe finti | codice di uscita 1 se un probe è in errore; aggregazione dei risultati |

## 9. Criteri di completamento

- `dotnet build Dusiburg.AI.Sinistri.slnx` senza errori né warning.
- `dotnet test` verde.
- `dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- health` stampa i controlli 1–3 e 5–8 **OK** (il 4 in Warning finché non esiste il DB).
- `dotnet run --project src/Dusiburg.AI.Sinistri.AppHost`: il dashboard mostra `api` e `web` in esecuzione e sani.
- Con `EMBEDDING_DIMENSIONS=768` impostato sull'AppHost, `health` e l'health check dell'API falliscono con un messaggio chiaro.

## 9 bis. Esito (2026-09-25)

Criteri di completamento tutti verificati:
- build di `Dusiburg.AI.Sinistri.slnx`: 0 errori, 0 warning;
- `dotnet test`: 35 test verdi (`Tests` 28, `Web.Tests` 7);
- `health`: controlli 1–3 e 5–8 OK, il 4 in avviso (database non ancora creato), il 10 informativo; exit code 0;
- `EMBEDDING_DIMENSIONS=768`: il controllo 8 fallisce con "il modello 'bge-m3' restituisce vettori da 1024 ma EMBEDDING_DIMENSIONS=768"; exit code 1. Anche `/health` dell'API risponde 503 (test `Health_ConUnControlloInErrore_Unhealthy`);
- AppHost: `api` risponde 200 su `/health` (`Degraded` solo per il DB mancante), `web` è `Healthy` e la home mostra la tabella dei controlli.

Scostamenti rispetto a questo file:

| Punto | Previsto | Fatto | Motivo |
|---|---|---|---|
| Default di `OLLAMA_ENDPOINT` | `http://localhost:11434` | `http://127.0.0.1:11434` | su questo PC `localhost` costa ~2 s per ogni nuova connessione (Fase 0) |
| Tipi dei controlli | `HealthCheckResult`, `HealthStatus` | `ProbeResult`, `ProbeStatus`, `ProbeReport` | i nomi di ASP.NET vanno in conflitto nell'API |
| Embedding su Ollama | `OllamaApiClient` come `IEmbeddingGenerator` | `OllamaEmbeddingGenerator` sopra `EmbedAsync` | così `num_gpu` (D18) arriva di sicuro alla richiesta; l'adapter di OllamaSharp non documenta come passarlo |
| `EMBEDDING_NUM_GPU` "automatico" | valore vuoto | valore `auto` | l'AppHost non inoltra i valori vuoti |
| `ServiceDefaults` | nessun riferimento | riferisce `Core` | nome delle sorgenti di telemetria (`SinistriTelemetry`), come `Shared` in O2C |
| API | solo `/health` | anche `GET /api/health` con l'esito dettagliato, in cache 30 s | Aspire interroga `/health` spesso e il controllo 8 calcola un embedding |
| Web | solo `/health` | home provvisoria con la tabella dei controlli, via `SinistriApiClient` | verifica end-to-end della catena Web → API sotto Aspire |
| `EMBEDDING_PROVIDER` `openai-compatible`/`onnx` | — | validati in configurazione, ma la factory risponde "arriva con la Fase 1b" | implementazione nella Fase 1b |
| Pacchetto `Microsoft.AspNetCore.OpenApi` | usato da Api | versione censita in `Directory.Packages.props`, non ancora referenziato | nessun endpoint da documentare fino alla Fase 8 |
| Git | `git init`, rami `main` e `develop` | **non eseguito** | commit e branch li gestisce l'utente |

Segreti impostati: `ConnectionStrings:sql` negli user-secrets dell'AppHost (`UserSecretsId` `fdf26212-2348-4c81-a6b5-de897910daf9`).

## 10. Commit proposto (non eseguito)

```
fase 1: scaffolding solution con AppHost Aspire e comando health
```
