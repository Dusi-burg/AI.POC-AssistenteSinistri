# Fase 9b — Dati demo consultabili: pagine web e catalogo delle clausole

> Aggiunta dopo la Fase 9 su richiesta dell'utente (2026-09-28), per rendere più chiara la demo. Riprende l'impianto della Fase 8
> (Razor Pages, client HTTP tipizzato, l'API come unico accesso al DB) e si inserisce prima delle estensioni della Fase 10.

## Obiettivo

Durante la demo deve essere semplice **vedere i dati presenti nel DB**: le clausole di polizza con tutte le informazioni, le polizze, lo
storico dei sinistri. Oggi si vedono solo attraverso le ricerche (le clausole recuperate, i sinistri simili) o il dettaglio di un
singolo sinistro.

### Scelta: pagine web, più un Markdown per il solo catalogo delle clausole

Valutate le alternative (pagine web, documento `.md` generato dal DB, entrambi):

| Criterio | Pagine web | `.md` generato |
|---|---|---|
| Dove si fa la demo | nella Web: i dati si collegano alla scheda (articolo citato → catalogo, simile → storico, polizza → i suoi sinistri) | fuori dal flusso della demo |
| Dati che cambiano a ogni `DbInit` | letti sempre dal DB attuale | da rigenerare ogni volta: polizze e sinistri, e i numeri dei sinistri dipendono dalla data del seed |
| 410 sinistri | filtri, ordinamento, paginazione | illeggibile |
| Costo | contenuto: client HTTP, dettaglio del sinistro, ricerca delle polizze, presentazione e layout esistono già | basso |

Decisione:
- **pagine web** per clausole, polizze e storico;
- **`docs/clausole.md`** generato per il solo **catalogo delle clausole**, che è stabile (60 clausole scritte a mano in
  `db/003_seed_clausole.sql`, indipendenti dal seed): si stampa o si apre di fianco alla demo, e si legge anche su GitHub.
  Per polizze e sinistri niente Markdown.

---

## 1. Contratti (`Core/Consultazione`)

```csharp
public sealed record ClausolaCatalogo(int Id, Prodotto Prodotto, string Articolo, TipoClausola Tipo, string Titolo, string Testo);

public sealed record PolizzaElenco(
    string Numero, Prodotto Prodotto, string Contraente, string Provincia, DateOnly Decorrenza, DateOnly Scadenza,
    decimal Massimale, decimal Franchigia, int Sinistri, bool Demo);

public sealed record FiltriElencoSinistri(
    Prodotto? Prodotto = null, CausaSinistro? Causa = null, StatoSinistro? Stato = null, string? Provincia = null,
    int? AnnoDenuncia = null, int? RiparatoreId = null, string? NumeroPolizza = null, string? Testo = null);

public sealed record SinistroElenco(
    int Id, string Numero, string NumeroPolizza, Prodotto Prodotto, string Contraente, string? Riparatore,
    DateOnly DataEvento, DateOnly DataDenuncia, string Provincia, CausaSinistro Causa, StatoSinistro Stato,
    decimal? ImportoLiquidato, string Descrizione);

public sealed record Pagina<T>(IReadOnlyList<T> Righe, int Numero, int DimensionePagina, int Totale);
```

`IConsultazioneRepository` (Fase 8) si estende con `GetClausoleAsync(prodotto?, tipo?, testo?)`,
`ElencaPolizzeAsync(cerca?, soloDemo, pagina)` e `ElencaSinistriAsync(filtri, pagina)`.

---

## 2. Endpoint API

| Metodo | Percorso | Parametri | Output | Note |
|---|---|---|---|---|
| GET | `/api/clausole` | `prodotto`, `tipo`, `testo` | `ClausolaCatalogo[]` | 60 righe: niente paginazione; `testo` su articolo, titolo e testo |
| GET | `/api/polizze/elenco` | `cerca`, `soloDemo`, `pagina`, `dimensione` (25, max 100) | `Pagina<PolizzaElenco>` | con il numero di sinistri; le demo per prime, poi per numero |
| GET | `/api/sinistri` | filtri di `FiltriElencoSinistri`, `pagina`, `dimensione` (25, max 100), `ordine` | `Pagina<SinistroElenco>` | ordine di default: data di denuncia decrescente |
| GET | `/api/sinistri/{numero}/gemello` | — | numero del sinistro gemello o 404 | dalle coppie di `data/duplicati_attesi.json` (come il fraud-scan) |

Query (`ConsultazioneRepository`):
- **sinistri**: un'unica query con i filtri opzionali `(@p IS NULL OR col = @p)`, `COUNT(*) OVER ()` per il totale e
  `OFFSET … FETCH` per la pagina; join con polizza, contraente e riparatore; `testo` come `LIKE '%…%'` sulla descrizione (con
  l'escape dei caratteri jolly già usato per le polizze). Con ~400 righe basta; a 50.000 la paginazione tiene comunque bassi i tempi;
- **polizze**: `COUNT` dei sinistri con `LEFT JOIN … GROUP BY` (o sottoquery), stessa paginazione;
- **clausole**: lettura per prodotto e tipo, ordinata per prodotto e articolo in ordine numerico (2.10 dopo 2.9).

Validazione: pagina ≥ 1, dimensione 1–100, anno plausibile, provincia di 2 lettere; errori come `ProblemDetails` 400.

---

## 3. Pagine (menu "Dati demo" nella barra di navigazione)

### 3.1 `Pages/Dati/Clausole` — catalogo

- Schede per prodotto (Casa e fabbricati, RC professionale tecnici); dentro, sezioni per tipo con i colori della scheda
  (garanzie verdi, esclusioni arancioni, franchigie blu, definizioni grigie).
- Per ogni clausola: articolo, titolo, testo integrale; ancora `#<prodotto>-<articolo>` per il link diretto.
- Filtro testuale (GET) su articolo, titolo e testo; conteggio per tipo.
- Link in testa alla versione Markdown (`docs/clausole.md`).

### 3.2 `Pages/Dati/Polizze` e `Pages/Dati/Polizza`

- Elenco paginato: numero, prodotto, contraente e provincia, validità, massimale, franchigia, numero di sinistri; le polizze demo
  evidenziate e filtrabili ("solo demo"); ricerca per numero o contraente.
- Dettaglio: dati della polizza e tabella dei suoi sinistri (lo stesso componente dello storico), con il pulsante "Pre-istruttoria su
  questa polizza" che apre la pagina iniziale con la polizza precompilata.

### 3.3 `Pages/Dati/Storico` — sinistri

- Tabella paginata: numero, data denuncia, polizza, contraente, causa, stato, provincia, liquidato, descrizione troncata.
- Filtri in GET (l'indirizzo si può salvare e riaprire): prodotto, causa (per prodotto), stato, provincia, anno di denuncia, riparatore,
  testo nella descrizione; conteggio totale e riepilogo per stato dei risultati.
- Le coppie di quasi-duplicati attese marcate con un'icona che porta al gemello.

### 3.4 Collegamenti con le pagine della Fase 8

- **Scheda**: nel dialog di ogni clausola, link "apri nel catalogo"; il numero di polizza porta al dettaglio della polizza.
- **Dettaglio sinistro** (esiste già): link alla polizza e, se esiste, al gemello tra i duplicati attesi.
- **Antifrode**: i numeri dei sinistri portano già al dettaglio; nessun cambiamento.

Le pagine seguono le regole della Fase 8: nessun accesso diretto al DB, `SinistriApiClient` con il resilience handler standard (sono
letture), formati da `SinistriPresentation`, messaggio chiaro se l'API non risponde.

---

## 4. Catalogo Markdown — `docs/clausole.md`

- **Renderer puro** `ClausoleMarkdownRenderer` in `Core`: per prodotto e per tipo, ogni clausola con articolo, titolo e testo
  integrale; indice iniziale con i link alle sezioni; intestazione con la data e l'avvertenza "clausole fittizie".
- **Comando CLI** `export-clausole [--out docs/clausole.md]`: legge le clausole dal DB (`IConsultazioneRepository.GetClausoleAsync`)
  e scrive il file. Si rilancia solo quando cambiano le clausole in `db/003_seed_clausole.sql`.
- **Test di allineamento**: un test di integrazione carica le clausole del seed su `Sinistri_Test`, genera il Markdown e lo confronta
  con `docs/clausole.md` del repository; se qualcuno modifica le clausole senza rigenerare il catalogo, il test fallisce con
  l'indicazione del comando da eseguire. La data nell'intestazione è esclusa dal confronto.
- Il README e `docs/demo.md` rimandano al catalogo.

---

## 5. Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `ConsultazioneRepositoryTests.ElencaSinistri_FiltriEPaginazione` | integrazione | ogni filtro, totale con `COUNT(*) OVER ()`, pagine consecutive senza sovrapposizioni |
| `ConsultazioneRepositoryTests.ElencaPolizze_ConteggioSinistri` | integrazione | numero di sinistri per polizza, polizze demo per prime |
| `ConsultazioneRepositoryTests.GetClausole_OrdineNumerico` | integrazione | 2.10 dopo 2.9, filtri per prodotto, tipo e testo |
| `ClausoleMarkdownRendererTests` | unit | raggruppamento per prodotto e tipo, indice, ancore |
| `CatalogoClausoleTests.DocsAllineatoAlSeed` | integrazione | `docs/clausole.md` uguale al Markdown generato dal seed |
| `ApiEndpointTests.Sinistri_ValidazioneEPaginazione` | integrazione (Web.Tests) | 400 su pagina o dimensione non valide, forma della risposta |
| `WebPageTests.DatiStorico_FiltriNellIndirizzo` | integrazione (Web.Tests) | i filtri della query string arrivano all'API e restano nel form |
| `WebPageTests.DatiClausole_CatalogoPerTipo` | integrazione (Web.Tests) | sezioni per tipo, testo integrale, ancore |
| `WebPageTests.DatiPolizza_SinistriELinkPreIstruttoria` | integrazione (Web.Tests) | sinistri della polizza e link con la polizza precompilata |

## 6. Criteri di completamento

- Dalla Web, senza usare le ricerche: catalogo delle 60 clausole per prodotto e tipo, elenco delle ~200 polizze con i conteggi, storico
  dei 410 sinistri filtrabile e paginato.
- Collegamenti funzionanti: articolo nella scheda → catalogo; polizza → suoi sinistri → dettaglio → gemello; polizza → pre-istruttoria
  precompilata.
- `docs/clausole.md` generato con `export-clausole`, allineato al seed (test verde), linkato da README e `docs/demo.md`.
- `docs/demo.md` aggiornato con un passo "i dati della demo" nella scaletta.
- Build della solution senza warning e test verdi.

Stima: ½–1 giornata.

## 6 bis. Esito (2026-09-28)

Verificato con API e Web avviate sul DB `Sinistri` (410 sinistri), richieste HTTP senza browser:

| Pagina | Esito |
|---|---|
| Catalogo clausole | 35 clausole casa per tipo (definizioni, garanzie, esclusioni, franchigie), 25 RC; ricerca "sicurezza" su RC → 3; ancore `casafabbricati-art-2-10` |
| Polizze | 203 polizze in 9 pagine, le 3 demo in cima; "solo demo" → 3 |
| Polizza `CF-DEMO-000001` | dati, 3 sinistri denunciati, uno con il gemello tra i duplicati attesi, pulsante "Pre-istruttoria su questa polizza" |
| Storico | 410 sinistri in 17 pagine; filtri nell'indirizzo; causa acqua condotta + MI → 16; gemelli marcati ⧉ |
| Dettaglio sinistro | link alla polizza; per `SIN-2026-000089` l'avviso del gemello `SIN-2026-000153` |
| `docs/clausole.md` | 60 clausole generate con `export-clausole`, articoli in ordine numerico (2.9, 2.10, 2.11) |

Build della solution senza warning; **170 test verdi** (11 in più: 4 di consultazione e catalogo, 3 del renderer, 1 dell'API, 3 delle pagine).

### Scostamenti

| Punto | Previsto | Fatto | Motivo |
|---|---|---|---|
| Tipo del catalogo | `ClausolaCatalogo` | `ClausolaDettaglio` esistente (Fase 8) | stessi campi |
| Gemello | endpoint `/api/sinistri/{numero}/gemello` | endpoint **più** campo `Gemello` nelle righe di `/api/sinistri` | marcare 25 righe con una chiamata ciascuna sarebbe stato inutile; l'endpoint resta per il dettaglio |
| Lettura delle coppie attese | nel fraud-scan | classe `CoppieAttese` condivisa da fraud-scan e storico | un solo punto che conosce il file |
| Riepilogo per stato | "dei risultati" | tre conteggi con `dimensione = 1`, solo senza filtro di stato | l'API restituisce il totale, non un raggruppamento; tre chiamate da una riga costano meno di un endpoint in più |
| Pagina oltre l'ultima | — | si mostra l'ultima | trovato nella prova: stringendo i filtri da una pagina avanzata la tabella restava vuota |
| Catalogo nella Web | schede per prodotto | pill per prodotto (un prodotto per pagina, `?prodotto=`) | i link con l'ancora funzionano senza JavaScript |
| Link a `docs/clausole.md` dalla Web | link | citato nel testo | la Web non serve i file del repository |

## 7. Commit proposto (non eseguito)

```
fase 9b: pagine web dei dati demo e catalogo Markdown delle clausole

Menu "Dati demo" nella Web: catalogo delle clausole per prodotto e tipo con
ancore per articolo, elenco e dettaglio delle polizze con i sinistri e la
pre-istruttoria precompilata, storico dei sinistri con filtri nell'indirizzo,
paginazione e gemelli dei quasi-duplicati attesi. Collegamenti dalla scheda
al catalogo e alla polizza, dal sinistro alla polizza e al gemello.
API: GET /api/clausole, /api/polizze/elenco, /api/sinistri con validazione
e paginazione, /api/sinistri/{numero}/gemello. Comando export-clausole e
docs/clausole.md, con un test che lo confronta con il seed.

Verifica: build della solution, 170 test verdi, pagine provate su DB Sinistri.
```
