# Fase 8 — API e UI web per la demo live

> Riferimento: `PLAN.md` §1 punto 7 e §4 Fase 8. Impianto come `Crm.Web` / `Erp.Web` di `AI.POC-OrderToCash`: **Razor Pages** (D16), avvio da **AppHost Aspire** (D15).
> Include il punto 3 della Fase 10 (Minimal API + pagina), decisione D14.

## Obiettivo

Condurre **tutta la demo dal browser**: pre-istruttoria, ricerca clausole, ricerca storico ibrida, antifrode, stato del sistema. La logica resta nei servizi applicativi di `Core`; endpoint e pagine sono sottili (convenzione `code-operations`: niente logica di business negli endpoint).

---

## 1. Architettura

```mermaid
flowchart LR
    B([Browser]) --> W["Web<br/>Razor Pages · :5202"]
    W -- "HttpClient tipizzato<br/>service discovery" --> A["Api<br/>Minimal API · :5201"]
    A --> S["Servizi Core<br/>PreIstruttoria · Ricerca · Antifrode · Health"]
    S --> D[("LocalDB localdev<br/>Sinistri")]
    S --> G{{"Ollama · GPU<br/>qwen3.5:9b"}}
    S --> C{{"Ollama · CPU<br/>bge-m3"}}
    H["AppHost Aspire<br/>dashboard, tracce, manopole"] -.-> A
    H -.-> W
```

- **Api** (`Dusiburg.AI.Sinistri.Api`): minimal API con `AddServiceDefaults()` / `MapDefaultEndpoints()`, connection string `sql` dall'AppHost, stesse estensioni DI della CLI (`AddSinistriCore/Data/Ai`). OpenAPI su `/openapi/v1.json` e un file `Api.http` con le richieste pronte, come `Erp.Api.http` in O2C.
- **Web** (`Dusiburg.AI.Sinistri.Web`): Razor Pages, **nessun accesso al DB né a Ollama**, solo HTTP verso l'API (stessa regola di O2C per le UI). Client tipizzato `SinistriApiClient` con base address `http://api` (service discovery Aspire, `WithReference(api)`), più una classe `…Presentation` per formattazioni ed etichette (come `DealPresentation` in O2C). Testi italiani con `WebEncoderOptions` su `UnicodeRanges.All`, come in O2C.
- Porte fisse nei `launchSettings.json`: Api `5201`, Web `5202` (O2C usa 5101–5106).
- Nessuna autenticazione (non-obiettivo del piano).

### Timeout e resilienza dei client HTTP (attenzione)

ServiceDefaults attiva per tutti i client il resilience handler standard, che taglia i tentativi a pochi secondi e ritenta. Una pre-istruttoria dura 15–40 s e non va ripetuta. Quindi la Web registra **due** client:
- `SinistriApiClient` per le **letture** (polizze, health, ricerche): resilienza standard, i retry non fanno danni (come in `Erp.Web`);
- `PreIstruttoriaApiClient` per `POST /api/pre-istruttoria` e `POST /api/antifrode/scan`: **senza** retry, timeout di tentativo e totale a 5 minuti (resilience handler rimosso o configurato apposta per quel client; la forma esatta va verificata sulla versione di `Microsoft.Extensions.Http.Resilience`).

---

## 2. Endpoint API

Tutti sotto `/api`, JSON camelCase, enum come stringhe, errori come `ProblemDetails` (404 polizza inesistente, 422 polizza non in vigore, 503 Ollama/SQL non raggiungibili).

| Metodo | Percorso | Input | Output | Servizio |
|---|---|---|---|---|
| GET | `/health` | — | health check ASP.NET (anche per Aspire) | ServiceDefaults + probe |
| GET | `/api/health` | — | lista dei controlli con dettaglio (come `health` della CLI) | `HealthService` |
| GET | `/api/statistiche-dataset` | — | conteggi per tabella, sinistri per causa × stato, `EmbeddingInfo` | `DatasetService` |
| GET | `/api/polizze?cerca=&top=20` | testo su numero o contraente | elenco polizze, demo per prime | `PolizzaQueryService` |
| GET | `/api/polizze/{numero}` | — | `DatiPolizza` | idem |
| GET | `/api/riparatori` | — | `Id`, ragione sociale | idem |
| GET | `/api/scenari-demo` | — | i 6 scenari del `DemoCatalog` | `DemoCatalog` |
| POST | `/api/pre-istruttoria` | `RichiestaPreIstruttoria` | `EsitoPreIstruttoria` | `PreIstruttoriaService` |
| POST | `/api/pre-istruttoria/markdown` | `EsitoPreIstruttoria` | `text/markdown` (download) | `SchedaMarkdownRenderer` |
| POST | `/api/clausole/ricerca` | testo, prodotto, top | `RisultatoRicercaClausole` | `RicercaService` |
| GET | `/api/clausole/{id}` | — | clausola completa | `ClausolaRepository` |
| POST | `/api/sinistri/ricerca` | testo + `FiltriStorico` + top | simili + statistiche + tempi | `RicercaService` |
| GET | `/api/sinistri/{numero}` | — | dettaglio sinistro | `SinistroRepository` |
| POST | `/api/antifrode/scan` | mesi, soglia | coppie + precision/recall se esistono i duplicati attesi | `AntifrodeService` |

- Gli endpoint accettano `CancellationToken`: chiudendo la pagina la generazione si interrompe.
- **Warm-up all'avvio** dell'API (`IHostedService`): un embedding e una chat minima, così il primo utente della demo non paga il caricamento dei modelli. Si disattiva con la manopola `SINISTRI_WARMUP=false`, inoltrata dall'AppHost.
- Endpoint suddivisi per area in classi statiche `…Endpoints.MapXxx()` (`PolizzaEndpoints`, `PreIstruttoriaEndpoints`, …), come `OrderEndpoints` in O2C.

---

## 3. Pagine (Razor Pages)

Layout condiviso (`Pages/Shared/_Layout.cshtml`): barra di navigazione, indicatore di stato (verde/rosso da `/api/health`), nomi dei modelli in uso e dove girano (GPU/CPU, da `/api/ps` tramite `health`). CSS semplice in `wwwroot/css`, come nelle UI di O2C; JavaScript minimo e solo dove serve (indicatore di attesa, pannello della clausola).

### 3.1 `Pages/Index` — Pre-istruttoria (pagina iniziale)

- Form: polizza (campo con elenco suggerito dall'API; le 3 polizze demo in cima), testo della denuncia, campi facoltativi data evento, causa (filtrata per prodotto), riparatore.
- **Pulsanti degli scenari demo 1–4**: link `?scenario=1…4` che precompilano il form dal `DemoCatalog`.
- "Genera scheda" = POST della pagina → handler `OnPostAsync` → `PreIstruttoriaApiClient`. Durante l'attesa uno script di poche righe disabilita il pulsante e mostra un indicatore con il tempo trascorso.
- Risultato (stessa pagina):
  - card della scheda: garanzie (verde), esclusioni da verificare (arancione), franchigia (blu), punti da chiarire, valutazione sintetica;
  - ogni **articolo citato è un link** che apre il testo integrale della clausola in un pannello (`<dialog>` HTML con il testo già presente nella pagina, niente chiamate extra): è il punto chiave della demo, la citazione si verifica;
  - tabella dei sinistri simili (numero → pagina di dettaglio `Pages/Sinistri/Dettaglio`);
  - riquadro statistiche con la dicitura *"calcolate dal database, non dal modello"*;
  - riquadro antifrode, avvisi della validazione, clausole consultate (distanza, "integrativa");
  - tempi per passo e link **"apri la traccia nel dashboard"** (trace id della richiesta, come i collegamenti fra portali di O2C);
  - pulsante "Scarica Markdown".

### 3.2 `Pages/Clausole/Ricerca` — pilastro A

Testo + prodotto → tabella con la distanza mostrata anche come barra orizzontale. Mostra "cosa vede il modello" e la vicinanza tra garanzie ed esclusioni.

### 3.3 `Pages/Storico/Ricerca` — pilastro B

Testo + filtri (prodotto, provincia, importo minimo, causa, anni) → tabella dei simili e riquadro statistiche. Il link "scenario 5" compila i filtri. Tempi separati (embedding e query SQL) per far vedere che la ricerca ibrida è una sola query.

### 3.4 `Pages/Antifrode/Index`

Mesi + soglia → coppie con le due descrizioni affiancate e il motivo. Se esiste `duplicati_attesi.json`: tabella precision/recall per le soglie standard, coppie attese marcate ✓.

### 3.5 `Pages/Stato/Index`

Risultato di `/api/health`, conteggi del dataset, `EmbeddingInfo`, parametri di retrieval in sola lettura. `DbInit` ed `embed` restano **solo da riga di comando** (distruttivi o lunghi, non servono in demo).

### Streaming (facoltativo)

Con Razor Pages la risposta arriva in un'unica volta dopo 15–40 s; l'indicatore di attesa e i tempi per passo bastano per la demo, e il dettaglio dei passi si vede comunque nel dashboard di Aspire. Variante da decidere in review: endpoint SSE `POST /api/pre-istruttoria/stream` (un evento per passo completato) consumato da un piccolo script nella pagina. Stima: +½ giornata.

---

## 4. Avvio della demo

```powershell
dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit        # una tantum: DB + dati sintetici
dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- embed      # una tantum: vettori
dotnet run --project src/Dusiburg.AI.Sinistri.AppHost           # api + web + dashboard
```

- Il dashboard di Aspire mostra `api` e `web` con lo stato di salute. Se Ollama è spento, `api` è rossa con il messaggio del probe.
- Le manopole (`OLLAMA_CHAT_MODEL`, `SOGLIA_DUPLICATO_COSINE`, …) si cambiano sull'AppHost (user-secrets o riga di comando) senza toccare i progetti, come in O2C; documentate in `docs/demo.md` (Fase 9).
- `launchSettings.json` dell'AppHost con `launchBrowser` sul dashboard; dal dashboard si apre la Web.

---

## 5. Test introdotti in questa fase (`tests/Dusiburg.AI.Sinistri.Web.Tests`)

Come `Dusiburg.AI.O2C.Web.Tests`: `WebApplicationFactory` su Api e Web; per la Web l'API è sostituita da uno `StubApiHandler` con risposte preparate.

| Test | Cosa verifica |
|---|---|
| `ApiEndpointTests.Health_RestituisceControlli` | 200 e forma della risposta (probe finti) |
| `ApiEndpointTests.PreIstruttoria_PolizzaInesistente_404` | `ProblemDetails` 404 |
| `ApiEndpointTests.PreIstruttoria_PolizzaNonInVigore_422` | `ProblemDetails` 422 |
| `ApiEndpointTests.SinistriRicerca_PassaIFiltri` | i filtri arrivano al servizio (stessa chiamata ripetuta con filtri diversi) |
| `WebPageTests.Index_Scenario1_PrecompilaIlForm` | `?scenario=1` → polizza e testo precompilati |
| `WebPageTests.Index_EsitoConCitazioni_MostraLinkAlleClausole` | ogni articolo citato ha il suo `<dialog>` con il testo |
| `WebPageTests.Index_ApiNonRaggiungibile_MessaggioChiaro` | nessuna eccezione a video |
| `PresentationTests.*` | formattazione di importi, distanze, stati ed etichette in italiano |

## 6. Criteri di completamento — checklist manuale della demo

Con l'AppHost avviato:

- [ ] Dashboard: `api` e `web` sane; pagina Stato tutta verde, conteggi coerenti con la Fase 3, embedding su CPU e chat su GPU.
- [ ] Scenari demo 1–4 dalla pagina Pre-istruttoria: scheda completa, articoli cliccabili, statistiche, tempi, traccia nel dashboard.
- [ ] Una denuncia che riformula un sinistro esistente mostra il riquadro antifrode.
- [ ] Polizza inesistente → messaggio d'errore chiaro.
- [ ] Scenario 5 dalla pagina Ricerca storico; scenario 6 dalla pagina Antifrode con precision/recall.
- [ ] Download del Markdown di una scheda.
- [ ] Spegnendo Ollama: `api` rossa nel dashboard, indicatore rosso nella Web, messaggio comprensibile.
- [ ] Build della solution e test verdi.

## 7. Commit proposto (non eseguito)

```
fase 8: API minimale e interfaccia Razor Pages per la demo, avviate da Aspire
```
