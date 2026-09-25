# Fase 9 — Valutazione, test e README

> Riferimento: `PLAN.md` §4 Fase 9 e §7 (Definition of Done). Test con NUnit (decisione D5 di `fase-0.md`).

## Obiettivo

Misurare la qualità del retrieval delle clausole su un golden set (recall@5, MRR), permettere il confronto tra modelli di embedding, completare la suite di test e scrivere il README.

---

## 1. Golden set — `data/golden_set.json`

15 denunce scritte a mano, **diverse** dagli scenari demo (per non tarare il sistema solo sugli esempi della demo), con gli articoli attesi.

```json
{
  "versione": 1,
  "casi": [
    {
      "id": "G01",
      "prodotto": "CasaFabbricati",
      "denuncia": "Al rientro dalle vacanze abbiamo trovato la cucina allagata: si è staccato il tubo della lavastoviglie.",
      "attese": {
        "rilevanti": ["Art. 2.4", "Art. 2.5", "Art. 4.3"],
        "esclusioniDaNonPerdere": ["Art. 3.4"]
      },
      "note": "acqua condotta con possibile usura del raccordo"
    }
  ]
}
```

Distribuzione proposta dei 15 casi:

| Prodotto | Temi | N. |
|---|---|---|
| CasaFabbricati | acqua condotta (2), infiltrazione da tetto non manutenuto (1), gelo in seconda casa (1), grandine su auto/serramenti (1), vento e tegole (1), fulmine e apparecchi (1), incendio da canna fumaria (1), furto con e senza scasso (2), cristalli (1) | 11 |
| RcProfTecnici | errore di calcolo strutturale (1), direzione lavori e difformità (1), coordinatore sicurezza e sanzione (1), richiesta arrivata per un lavoro di prima della retroattività (1) | 4 |

- `rilevanti`: tutti gli articoli che un liquidatore considererebbe pertinenti (garanzie, esclusioni, franchigie). Le definizioni solo se decisive.
- `esclusioniDaNonPerdere`: sottoinsieme di esclusioni particolarmente importanti, per una metrica dedicata.
- I casi si scrivono **dopo** la revisione dei testi delle clausole (Fase 3) e si fanno rivedere all'utente insieme al primo report.

---

## 2. Metriche

Per ogni caso si esegue la **ricerca vettoriale pura** delle clausole (`top = 5`, senza clausole integrative: si misura il modello di embedding, non l'accorgimento della Fase 5) e poi, separatamente, la ricerca completa.

| Metrica | Definizione | Obiettivo |
|---|---|---|
| **recall@5** | per caso: \|rilevanti ∩ prime 5\| / \|rilevanti\|; media sui casi | ≥ 0.7 (Definition of Done, Q5 di `fase-0.md`) |
| **MRR** | per caso: 1 / posizione del primo articolo rilevante (0 se nessuno nelle prime 10); media | informativa |
| **hit@1** | quota di casi con un articolo rilevante in prima posizione | informativa |
| **recall esclusioni** (con clausole integrative) | quota di `esclusioniDaNonPerdere` presenti nel risultato completo della Fase 5 | informativa, misura l'effetto dell'accorgimento |

Nota: se un caso ha più di 5 articoli rilevanti, la recall@5 massima è < 1. Il golden set va scritto con **al più 4–5 articoli rilevanti** per caso, così la metrica resta leggibile.

---

## 3. Comando `eval`

```
eval [--golden data/golden_set.json] [--embedding-model <nome> --embedding-provider <ollama|openai-compatible|onnx> --embedding-dimensions <n>] [--top 5]
```

1. Legge il golden set, fa l'embedding di ogni denuncia, esegue le due ricerche.
2. Calcola le metriche (`EvalService` in `Core`, funzioni pure testabili).
3. Stampa la tabella per caso (id, recall@5, rank del primo rilevante, articoli mancanti) e le medie.
4. Salva il report in `eval/report_<yyyy-MM-dd_HHmm>.md`:
   - intestazione: data, modello di embedding e dimensioni, modello chat (informativo), parametri di retrieval, numero di casi;
   - tabella riassuntiva delle metriche;
   - tabella per caso con gli articoli trovati (✓/✗) e le distanze;
   - sezione "casi peggiori" (le 3 recall più basse) con le prime 5 clausole restituite, per capire dove migliorare i testi o i template.

### Confronto tra modelli di embedding (`--embedding-model`)

Approccio scelto (il più semplice da documentare e mantenere): **un database separato per modello**.

- Nome del DB derivato: `Sinistri_emb_<provider>_<modello normalizzato>` (es. `Sinistri_emb_onnx_qwen3_embedding_0_6b`).
- Con `--embedding-model` il comando: verifica che il modello sia installato; misura la dimensione con una chiamata di prova e la confronta con `--embedding-dimensions` (obbligatorio, per non derogare alla regola "dimensione mai implicita"); se il DB derivato non esiste lo crea con `DatabaseInitializer.RecreateAsync` (la stessa routine di `DbInit`, stesso `RandomSeed` → stessi dati) e poi esegue `embed` con quel modello; poi esegue la valutazione su quel DB.
- Le opzioni si sovrascrivono in memoria (`ConnectionStrings:sql`, `EMBEDDING_PROVIDER`, `EMBEDDING_MODEL`, `EMBEDDING_DIMENSIONS`), senza toccare il file di configurazione.
- Il report indica il modello; per il confronto si mettono a fianco due report. Un comando `eval --confronta <report1> <report2>` è fuori scope.
- Alternativa scartata: colonne vettoriali aggiuntive per modello nelle stesse tabelle → schema dinamico, query con nome colonna variabile, più complessità per un confronto occasionale.

Qui si **conferma sul golden set completo** la scelta fatta al CHECKPOINT 1b: si valutano il vincitore e i due migliori sconfitti del banco di prova (modelli e runtime sono già installati dalla Fase 1b). Se il golden set ribalta la classifica del banco, lo si segnala all'utente con i numeri prima di cambiare il default.

---

## 4. Suite di test completa

Due progetti, come in O2C (NUnit 4, `Assert.That`, Microsoft.Testing.Platform con `EnableNUnitRunner`, marcatori `//SETUP` / `//SUT` in ogni test):
- `tests/Dusiburg.AI.Sinistri.Tests`: Core, Data, Ai, Ingestion, DbInit;
- `tests/Dusiburg.AI.Sinistri.Web.Tests`: endpoint dell'API e pagine della Web (Fase 8).

| Area | Classi di test | Tipo | Progetto | Fase |
|---|---|---|---|---|
| Configurazione | `SinistriOptionsTests` | unit | Tests | 1 |
| Health | `HealthServiceTests` | unit | Tests | 1 |
| Script SQL | `SqlScriptRunnerTests` | unit | Tests | 2 |
| Schema | `DatabaseInitializerTests` | integrazione | Tests | 2 |
| Dati sintetici | `SinistriGeneratorTests`, `QuasiDuplicatiGeneratorTests`, `TemplateTests`, `DemoCatalogTests` | unit | Tests | 3 |
| Embedding | `EmbeddingTextBuilderTests`, `EmbeddingServiceTests`, `EmbeddingRepositoryTests` | unit + integrazione | Tests | 4 |
| Retrieval e statistiche | `ClausolaRepositoryTests`, `SinistroRepositoryTests` | integrazione | Tests | 5 |
| **Prompt builder** | `PromptBuilderTests` | unit | Tests | 6 |
| **Validazione articoli citati** | `CitazioniValidatorTests` | unit | Tests | 6 |
| **Parsing JSON** | `SchedaParserTests` | unit | Tests | 6 |
| Orchestrazione scheda | `PreIstruttoriaServiceTests` (repository e chat a copione, come `ScriptedChatClient` di O2C) | unit | Tests | 6 |
| Antifrode | `AntifrodeRepositoryTests`, `ValutazioneDuplicatiTests` | integrazione + unit | Tests | 7 |
| API e pagine | `ApiEndpointTests`, `WebPageTests`, `PresentationTests` | integrazione | Web.Tests | 8 |
| **Metriche di valutazione** | `EvalMetricsTests` (recall@k, MRR; casi limite: nessun rilevante trovato, più rilevanti di k) | unit | Tests | 9 |
| Golden set | `GoldenSetTests.TuttiGliArticoliEsistono` (ogni articolo atteso esiste nelle clausole del seed) | integrazione | Tests | 9 |

Organizzazione:
- **Unit** senza dipendenze esterne: veloci, sempre eseguiti.
- **Integrazione** (`[Category("Integration")]`) su LocalDB `localdev`, DB `Sinistri_Test` con `VECTOR(4)`, ricreato in un `[SetUpFixture]` con `DatabaseInitializer.RecreateAsync` (come i test di O2C con `O2CDatabaseInitializer`). Ogni classe ripulisce i propri dati in `[SetUp]`.
- **Nessun test chiama Ollama**: chat ed embedding sono sempre finti. Il comportamento reale del modello si verifica con `eval` e con il CHECKPOINT 6.
- "Calcolo statistiche" del piano: coperto da `SinistroRepositoryTests.CalcolaStatistiche_*` su DB di test. Tra le due opzioni del piano si sceglie il DB di test, perché la logica sta in SQL e un mock non la verificherebbe.

---

## 5. CI — `.github/workflows/ci.yml`

Copia del workflow di O2C (Windows, `setup-dotnet` da `global.json`, `sqllocaldb create localdev -s`, restore / build Release / `dotnet test --solution Dusiburg.AI.Sinistri.slnx`).

⚠️ **Punto da verificare:** la LocalDB preinstallata sui runner `windows-latest` di GitHub probabilmente **non è SQL Server 2025**, e senza la 2025 il tipo `VECTOR` non esiste: i test di integrazione fallirebbero in CI pur passando in locale. Opzioni, da decidere quando si arriva qui:
1. il primo job della CI stampa `sqllocaldb versions`; se la versione è < 17, i test marcati `[Category("RequiresSql2025")]` si escludono con un filtro (restano obbligatori in locale);
2. installare nel job il pacchetto LocalDB di SQL Server 2025 prima di creare l'istanza (fattibilità e tempi da verificare);
3. un job Linux con un container SQL Server 2025 per i soli test vettoriali.
Default proposto: l'opzione 1, subito; la 2 come miglioramento se è semplice.

---

## 6. README e documentazione (come O2C)

- **`README.md`** (inglese) e **`README.it.md`** (italiano), con i link reciproci in testa e il badge della CI.
- **`docs/architettura.md`**: specifica, cioè la fonte di verità. Pilastri A e B, schema del DB, formato dei testi vettorializzati, prompt, validazione, antifrode, decisioni D1–D20 con la loro motivazione.
- **`docs/demo.md`**: preparazione (`DbInit` → `embed` → AppHost), polizze e scenari del `DemoCatalog`, manopole dell'AppHost con i default, sequenza consigliata per una demo di 15 minuti.
- **`docs/il-progetto-in-breve.md`** (facoltativo, come in O2C): spiegazione per chi non sviluppa, cioè cosa fa l'assistente e perché le statistiche non le calcola il modello.
- `docs/images/`: schermate della Web e del dashboard Aspire (traccia di una pre-istruttoria).

Sezioni del README:
1. Cos'è il POC (pilastri A e B, antifrode, scheda) con il diagramma di `fase-8.md`.
2. Prerequisiti: .NET 10 SDK, SQL Server 2025 (LocalDB `localdev`), Ollama con `qwen3.5:9b` e `embeddinggemma`; hardware di riferimento (RTX 5060 8 GB + Ryzen AI 7 350) e posizionamento dei modelli (chat su GPU, embedding su CPU).
3. Configurazione: user-secrets dell'AppHost (`ConnectionStrings:sql`) e manopole con i default.
4. Da DB vuoto alla demo: `DbInit` → `Cli embed` → `AppHost` (e `Cli ask …` per la sola console).
5. Riferimento dei comandi CLI.
6. Risultati della valutazione (tabella del report più recente) e soglia antifrode scelta con precision/recall.
7. Limiti noti e sviluppi possibili (rimando alla Fase 10).
8. Avvertenza: tutti i dati sono sintetici e fittizi.

## 7. Criteri di completamento (Definition of Done del POC)

- `dotnet test` verde (unit + integrazione) in locale; CI verde (con l'eventuale esclusione dei test vettoriali documentata).
- Da DB vuoto: `DbInit` → `embed` → `ask …` produce una scheda leggibile con citazioni valide.
- `fraud-scan` individua almeno 8 delle 10 coppie attese.
- `eval` genera `eval/report_<data>.md` con **recall@5 ≥ 0.7**. Se non la raggiunge: report con l'analisi dei casi peggiori e una proposta di intervento, da discutere con l'utente.
- README (EN + IT) e `docs/` completi.
- Build della solution verde.

## 8. Commit proposto (non eseguito)

```
fase 9: golden set, comando eval, suite di test, CI e documentazione
```
