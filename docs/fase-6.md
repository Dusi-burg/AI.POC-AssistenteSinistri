# Fase 6 — Scheda di pre-istruttoria (generazione)

> Riferimento: `PLAN.md` §4 Fase 6 e §6 (rischi). Chiusura con **CHECKPOINT 6**.

## Obiettivo

Dato il testo di una denuncia e un numero di polizza, produrre una scheda di pre-istruttoria in JSON con citazioni degli articoli, validarla contro le clausole recuperate e mostrarla in Markdown. Comando `ask`.

---

## 1. Contratti (`Core`)

```csharp
public sealed record RichiestaPreIstruttoria(
    string Denuncia,
    string NumeroPolizza,
    DateOnly? DataEvento = null,
    CausaSinistro? CausaIndicata = null,
    int? RiparatoreId = null);          // usato dal controllo antifrode (Fase 7)

public sealed record SchedaPreIstruttoria(               // = schema JSON richiesto al modello
    IReadOnlyList<GaranziaOperante> GaranzieOperanti,
    IReadOnlyList<EsclusioneDaVerificare> EsclusioniDaVerificare,
    FranchigiaApplicabile? FranchigiaApplicabile,
    IReadOnlyList<string> PuntiDaChiarireConCliente,
    string ValutazioneSintetica);

public sealed record GaranziaOperante(string Articolo, string Motivazione);
public sealed record EsclusioneDaVerificare(string Articolo, string CosaVerificare);
public sealed record FranchigiaApplicabile(string Articolo, string Descrizione);

public sealed record EsitoPreIstruttoria(
    DatiPolizza Polizza,
    SchedaPreIstruttoria? Scheda,                        // null se si è finiti nel fallback testuale
    string? TestoLibero,                                 // valorizzato solo nel fallback
    IReadOnlyList<ClausolaTrovata> Clausole,
    IReadOnlyList<SinistroSimile> Simili,
    StatisticheSimili Statistiche,
    IReadOnlyList<Avviso> Avvisi,                        // validazione, polizza, parsing
    IReadOnlyList<SegnalazioneDuplicato> PossibiliDuplicati,  // vuota fino alla Fase 7
    TempiEsecuzione Tempi);                              // polizza, embedding, clausole, storico, statistiche, LLM, totale
```

`DatiPolizza` = numero, prodotto, contraente, provincia, decorrenza, scadenza, massimale, franchigia.

---

## 2. `PreIstruttoriaService.GeneraAsync` — passi

| # | Passo | Dettaglio | Errore / avviso |
|---|---|---|---|
| 1 | Carica polizza + contraente | `IPolizzaRepository.GetByNumeroAsync` (lookup su chiave univoca `Numero`) | polizza inesistente → `PolizzaNonTrovataException` (messaggio chiaro, codice di uscita 2) |
| 1b | Controllo validità | se `DataEvento` è indicata e fuori da `[Decorrenza, Scadenza]` → **errore** "polizza non in vigore alla data evento"; se non è indicata si controlla la data odierna e, se scaduta, solo **avviso** | |
| 2 | Embedding della denuncia | `EmbeddingTextBuilder` (con la causa se indicata) → `IEmbeddingService.EmbedQueryAsync` (prefisso "query" del profilo) | |
| 3 | Retrieval A | `CercaPertinentiAsync(prodotto della polizza, TopClausole, soglia integrativa)` | nessuna clausola → errore (DB senza embedding) |
| 3b | Retrieval B + statistiche | `CercaSimiliAsync(prodotto, AnniStorico, top = TopSinistri)` + `CalcolaStatisticheAsync(Id dei simili)` | nessun simile → la scheda si genera comunque, sezione vuota |
| 4 | Prompt + chiamata LLM | `PromptBuilder` → `IChatClient.GetResponseAsync` (vedi §3–4) | |
| 5 | Parsing + validazione | `SchedaParser` + `CitazioniValidator` (vedi §5) | retry / fallback |
| 6 | (Fase 7) controllo duplicati | `AntifrodeService.ControllaDenunciaAsync` | |

Tutti i passi sono cronometrati (`Stopwatch`) e i tempi finiscono in `TempiEsecuzione`.

---

## 3. Prompt (`PromptBuilder`, progetto `Ai`)

### System prompt (italiano)

```
Sei un assistente di pre-istruttoria sinistri per un ufficio liquidazione.
Il tuo compito è preparare una scheda sintetica che aiuti il liquidatore a impostare la pratica.

Regole:
1. Usa SOLO le clausole fornite nella sezione CLAUSOLE. Non citare articoli che non compaiono lì.
2. Ogni garanzia, esclusione o franchigia deve riportare l'articolo esattamente come scritto (es. "Art. 2.4").
3. Se un'informazione necessaria non è presente nella denuncia o nelle clausole, dichiaralo e inseriscila tra i punti da chiarire con il cliente.
4. Non inventare importi. Gli unici numeri che puoi riportare sono quelli presenti in DATI POLIZZA e STATISTICHE.
5. Le esclusioni vanno indicate come "da verificare", spiegando quale circostanza di fatto le renderebbe applicabili.
6. Tono professionale, frasi brevi, italiano.
7. Rispondi solo con un oggetto JSON conforme allo schema richiesto, senza testo prima o dopo.
```

### Messaggio utente (struttura)

```
DATI POLIZZA
Numero: CF-DEMO-000001 | Prodotto: Casa e fabbricati | Contraente: … (MI)
Validità: 2025-09-24 → 2028-09-24 | Massimale: 300.000,00 € | Franchigia: 250,00 €

DENUNCIA
<testo della denuncia>  [Data evento: … se indicata]

CLAUSOLE
[C1] Art. 2.4 (Garanzia) — Acqua condotta
<testo>
[C2] Art. 3.1 (Esclusione) — Infiltrazioni dovute a mancata manutenzione
<testo>
…

SINISTRI SIMILI (storico, i 5 più vicini)
[S1] SIN-2025-000123 | 2025-03 | Acqua condotta | Chiuso | liquidato 3.200,00 €
     Descrizione: <max 300 caratteri> | Esito perizia: <max 200 caratteri>
…

STATISTICHE (calcolate sul database, non ricalcolarle)
Casi simili: 10 | Respinti: 2 (20,0%) | Liquidato min/mediana/max: 850 € / 3.100 € / 9.800 €

Compila la scheda di pre-istruttoria.
```

- Si passano al modello i **primi `SinistriNelPrompt` (5)** dei `TopSinistri` (10): meno token, e la tabella completa la si mostra comunque all'utente.
- Budget stimato: system ~300 token, clausole 5–7 × ~150, sinistri 5 × ~120, dati ~150 → **~2.500–3.000 token** in ingresso + ~600 in uscita. Con `num_ctx = 8192` c'è margine.
- Il `PromptBuilder` è puro (input → lista di `ChatMessage`), senza I/O: è la parte più testata.

---

## 4. Chiamata al modello

- `ChatOptions`: parte dalle opzioni di default del client (Fase 1: `Temperature = 0.1`, `think = false`, `num_ctx = OLLAMA_NUM_CTX`), `ModelId = OLLAMA_CHAT_MODEL`, `ResponseFormat` = **JSON con schema** generato dal tipo `SchedaPreIstruttoria` (in `Microsoft.Extensions.AI` c'è l'output strutturato, `ChatResponseFormat.ForJsonSchema…` / `GetResponseAsync<T>`; OllamaSharp lo traduce nel parametro `format` di Ollama, che accetta uno schema JSON). Se la traduzione dello schema non funziona si ripiega su `ChatResponseFormat.Json` (JSON generico) e lo schema si descrive nel prompt.
- `think = false` e `num_ctx` si passano con `AddOllamaOption(OllamaOption.Think, false)` e `AddOllamaOption(OllamaOption.NumCtx, …)`, le stesse API già usate in O2C con `qwen3.5:9b`. Là si è visto che con il thinking attivo il modello a volte lascia la risposta dentro il ragionamento; qui produrrebbe testo fuori dal JSON e raddoppierebbe i tempi.
- **Diagnostica (D19):** con `SINISTRI_PROMPT_CAPTURE_DIR` valorizzato, `PromptCaptureChatClient` salva ogni richiesta (messaggi, opzioni, schema) su file, come in O2C. Serve al CHECKPOINT 6 per mostrare il prompt reale e ripetere una richiesta a mano.
- **Telemetria:** ogni passo di §2 è un'`Activity` della sorgente `Dusiburg.AI.Sinistri.PreIstruttoria` (tag: numero polizza, prodotto, numero di clausole e simili, esito della validazione); la chiamata al modello ha già lo span di `UseOpenTelemetry`. Nel dashboard di Aspire una pre-istruttoria si vede come una traccia unica: embedding → SQL → LLM → validazione.
- Nomi JSON in camelCase come nello schema del piano (`garanzieOperanti`, `articolo`, …); deserializzazione con `JsonSerializerOptions` camelCase e `PropertyNameCaseInsensitive`.
- **Streaming a console** (rischio "lentezza" del piano): con l'output JSON lo streaming dei token non è leggibile. Si mostra invece l'avanzamento per passi con i tempi (`✓ embedding 0,4 s`, `✓ clausole 0,1 s`, `… generazione scheda 18 s`). Un'opzione `--raw` stampa i token JSON man mano, utile solo per debug.

---

## 5. Parsing, validazione, retry e fallback

### Parsing (`SchedaParser`)

1. `JsonSerializer.Deserialize<SchedaPreIstruttoria>`; prima si tolgono eventuali recinti Markdown (```json … ```) e testo attorno all'oggetto (primo `{` … ultimo `}`).
2. JSON non valido o campi obbligatori mancanti (`valutazioneSintetica` vuota) → **un retry**: si aggiunge alla conversazione la risposta ricevuta e il messaggio *"La risposta non è un JSON valido secondo lo schema (errore: …). Rispondi di nuovo solo con il JSON."*
3. Secondo fallimento → **fallback testuale**: nuova chiamata senza `ResponseFormat`, chiedendo la stessa scheda in testo con titoli di sezione; `Scheda = null`, `TestoLibero` valorizzato, avviso "scheda non strutturata: citazioni non validate".

### Validazione delle citazioni (`CitazioniValidator`)

| Regola | Azione |
|---|---|
| Normalizzazione: da `"Art. 2.4"`, `"art 2.4"`, `"Articolo 2.4"`, `"2.4"` si estrae il numero (`\d+(\.\d+)*`) e si confronta con quello delle clausole recuperate | — |
| Articolo citato **non** tra le clausole recuperate | voce **rimossa** + avviso "Art. X citato dal modello ma non tra le clausole recuperate: rimosso" |
| Articolo valido ma di tipo diverso dalla sezione (es. un'esclusione in `garanzieOperanti`) | voce mantenuta + avviso "tipo incoerente" |
| `franchigiaApplicabile` con articolo non valido | impostata a `null` + avviso |
| Articolo normalizzato riscritto nella forma canonica del DB (`Art. 2.4`) | — |
| Importi in `valutazioneSintetica` / motivazioni (regex su `€` / `euro`) non presenti tra massimale, franchigia e statistiche | avviso "importo non verificato: …" (euristica, non si modifica il testo) |

La validazione è pura (scheda + clausole → scheda pulita + avvisi) e completamente testabile senza LLM.

---

## 6. Rendering Markdown (`SchedaMarkdownRenderer`, `Core`)

Usato dalla console, da `--out` e dall'API (download `.md` in Fase 8):

```markdown
# Scheda di pre-istruttoria — polizza CF-DEMO-000001
> Generata il 2026-09-24 10:32 · modello qwen3.5:9b · tempo totale 21,4 s

## Dati polizza
| Numero | Prodotto | Contraente | Validità | Massimale | Franchigia |

## Denuncia
> testo

## Garanzie operanti
- **Art. 2.4 — Acqua condotta**: motivazione…

## Esclusioni da verificare
- **Art. 3.1 — Infiltrazioni…**: cosa verificare…

## Franchigia applicabile
**Art. 4.3** — descrizione…

## Punti da chiarire con il cliente
1. …

## Valutazione sintetica
…

## Sinistri simili (5 più vicini)
| Numero | Data | Causa | Stato | Liquidato | Distanza |

## Statistiche sui casi simili (da database)
| Casi | Respinti | Liquidato min | Mediana | Max |

## Possibili duplicati (Fase 7)

## Avvisi
- ⚠️ …

## Clausole consultate
| # | Articolo | Tipo | Titolo | Distanza | Integrativa |
```

A console lo stesso Markdown si stampa così com'è (leggibile anche senza rendering).

---

## 7. Comando CLI

```
ask "<denuncia>" --polizza <numero> [--data-evento 2026-09-20] [--causa AcquaCondotta] [--riparatore <id>] [--out scheda.md] [--raw]
```

Codici di uscita: `0` scheda generata (anche con avvisi), `2` polizza inesistente o non in vigore, `1` altri errori.

## 8. Test introdotti in questa fase

| Test | Cosa verifica |
|---|---|
| `PromptBuilderTests.Build_ContieneClausoleNumerateConArticolo` | ogni clausola compare come `[Cn] Art. x (Tipo) — Titolo` |
| `PromptBuilderTests.Build_LimitaSinistriNelPrompt` | con 10 simili ne entrano 5; descrizioni troncate a 300 caratteri |
| `PromptBuilderTests.Build_StatisticheFormattateInItaliano` | formattazione `it-IT` degli importi |
| `SchedaParserTests.Parse_JsonConRecintoMarkdown` | JSON racchiuso in ```json viene letto |
| `SchedaParserTests.Parse_JsonNonValido_Fallisce` | errore esplicito, nessuna eccezione non gestita |
| `CitazioniValidatorTests.Valida_ArticoloInesistente_RimossoConAvviso` | rimozione + avviso |
| `CitazioniValidatorTests.Valida_FormeDiverseDellArticolo` | `"art 2.4"`, `"Articolo 2.4"`, `"2.4"` riconosciuti |
| `CitazioniValidatorTests.Valida_TipoIncoerente_Avviso` | esclusione tra le garanzie → avviso |
| `PreIstruttoriaServiceTests.Genera_JsonNonValido_RiprovaPoiFallback` | chat client finto: 2 risposte non valide → fallback testuale; 1 non valida + 1 valida → scheda |
| `PreIstruttoriaServiceTests.Genera_PolizzaNonInVigore_Errore` | data evento fuori validità → eccezione dedicata |

## 9. Criteri di completamento e CHECKPOINT 6

Si eseguono i 4 scenari demo sulle polizze demo e si salvano le schede in `eval/checkpoint-6/scenario-N.md`:

| Scenario | Comando | Articoli che ci si aspetta di vedere citati |
|---|---|---|
| 1 | `ask "Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno." --polizza CF-DEMO-000001` | garanzie 2.4, 2.5 · esclusioni da verificare 3.1 / 3.4 · franchigia 4.3 |
| 2 | `ask "Il cliente dice che la grandine ha rotto i pannelli solari sul tetto." --polizza CF-DEMO-000001` | garanzie 2.2 (e 2.3 solo "se richiamata") · esclusione 3.6 · franchigia 4.2 · punto da chiarire: estensione 2.3 presente in polizza? |
| 3 | `ask "Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento." --polizza RP-DEMO-000001` | garanzie 2.1, 2.6 · esclusioni 3.4, 3.5 · franchigia 4.1 · punto da chiarire: data della richiesta di risarcimento (claims made, 1.4) |
| 4 | `ask "Dopo un temporale si è bruciata la caldaia e il televisore." --polizza CF-DEMO-000001` | garanzia 2.6 · esclusione 3.7 (età degli apparecchi) · franchigia 4.4 |

Criteri:
- ogni scheda si genera senza errori, con JSON valido (al più un retry);
- nessuna citazione non valida *sopravvissuta* (quelle rimosse compaiono negli avvisi);
- nessun importo inventato (nessun avviso "importo non verificato", o avvisi giustificati);
- tempo per scheda annotato (atteso 15–40 s con cambio di modello incluso).

**CHECKPOINT 6:** mostrare all'utente le 4 schede e chiedere feedback sul prompt (tono, completezza, sezioni) prima di proseguire. Le modifiche al prompt si fanno qui, non nelle fasi successive.

## 9 bis. Esito (2026-09-25) — CHECKPOINT 6 superato

### Seconda versione, dopo il feedback (schede attuali in `eval/checkpoint-6/`)

Modifiche approvate al CHECKPOINT e applicate:
1. **Prompt**, nuove regole 3, 4, 6 e 7 del system prompt: ogni articolo in una sola sezione e una sola volta, franchigie solo come franchigia applicabile; motivazione legata al contenuto della clausola citata; nessun calcolo con gli importi; solo le esclusioni che i fatti potrebbero rendere applicabili. La regola del formato di risposta diventa la 9.
2. **Franchigia di base**: se nessuna franchigia è tra le clausole recuperate, la query aggiunge `Retrieval:ArticoloFranchigiaBase` (default `Art. 4.1`, "franchigia frontale" casa e "franchigia fissa" RC, stessa numerazione) come integrativa, a qualunque distanza. Vale anche per `search-clausole`. Alzare la soglia per le franchigie non bastava: nello scenario 3 la franchigia RC più vicina è la 4.4 (sottolimite coordinatore), non la 4.1.

| Scenario | Tempo | Citati | Avvisi |
|---|---|---|---|
| 1 | 26,9 s (con caricamento del modello) | G 2.4, 2.5 · E 3.4, 3.5 · F 4.3 | nessuno |
| 2 | 16,4 s | G 2.3, **2.2** · E 3.6 · F 4.2 · punto: estensione 2.3 richiamata e premio pagato? | nessuno |
| 3 | 16,9 s | G 2.1, 2.6, 1.1 · E 3.5 · **F 4.1** (2.500 € di polizza) · punto: data della richiesta (1.4) | nessuno |
| 4 | 12,4 s | G 2.6 · E 3.7 (una volta) · F 4.4 | nessuno |

Risolti: 2.6 al posto della 2.2 e calcolo inventato (scenario 2), franchigia 4.1 assente (scenario 3), 4.4 tra le garanzie e 3.7 ripetuta (scenario 4). Build della solution senza warning; 123 test verdi (in più: franchigia di base nel repository delle clausole, nuove regole nel prompt).

Imprecisioni residue, che la validazione non può intercettare (l'articolo esiste ed è del tipo giusto; l'importo è nelle clausole):
- scenario 1: l'esclusione 3.5 (ristrutturazione) resta, con una circostanza da verificare formulata in modo sensato; la 3.1 attesa non è tra le clausole recuperate;
- scenario 3: l'esclusione 3.4 (circostanze note) non è tra le clausole recuperate;
- scenario 4: "franchigia 150 €" invece dei 250 € di polizza, che l'Art. 4.4 dice di applicare se più elevati.

Sono materiale per il golden set della Fase 9.

### Prima versione (prima del feedback)

Schede generate su un DB di verifica (DbInit + `embed`, poi eliminato) con `qwen3.5:9b`:

| Scenario | Tempo | Citati (dopo la validazione) | Atteso | Avvisi |
|---|---|---|---|---|
| 1 | 17,2 s | G 2.4, 2.5 · E 3.4, 3.5 · F 4.3 | G 2.4, 2.5 · E 3.1/3.4 · F 4.3 | nessuno |
| 2 | 14,4 s | G 2.3, **2.6** · E 3.6 · F 4.2 · punto: estensione 2.3 richiamata? | G 2.2 (2.3 se richiamata) · E 3.6 · F 4.2 | importo non verificato: 210.000 € (calcolo inventato, 20% del massimale) |
| 3 | 14,5 s | G 2.1, 2.6 · E 3.5 · F — · punti: data della richiesta (1.4), retroattività (1.5) | G 2.1, 2.6 · E 3.4, 3.5 · F 4.1 | Art. 4.1 citato ma non recuperato: rimosso |
| 4 | 16,0 s | G 2.6, **4.4** · E 3.7 (due volte) · F 4.4 | G 2.6 · E 3.7 · F 4.4 | 4.4 tra le garanzie: tipo incoerente |

Criteri:
- JSON valido in tutte e 4 le schede al primo tentativo (nessun retry, nessun fallback). Lo schema generato dal tipo arriva a Ollama: il modello usa esattamente le chiavi camelCase, che il prompt non elenca;
- nessuna citazione non valida sopravvissuta: l'unica (4.1 nello scenario 3) è stata rimossa e compare negli avvisi;
- un importo inventato (scenario 2), segnalato dalla validazione;
- tempo per scheda 14–17 s a modello caricato; la prima esecuzione, con il caricamento del modello, 27 s;
- consumo misurato: ~2.000 token in ingresso e ~500 in uscita, sotto il budget stimato;
- polizza inesistente e data evento fuori validità: messaggio chiaro, codice di uscita 2;
- build della solution senza warning; 122 test verdi (17 nuovi: prompt, parser, validatore, servizio con chat finta, Markdown, polizza per numero).

Osservazioni per il feedback sul prompt:
1. **Scenario 2**: al posto dell'Art. 2.2 (Eventi atmosferici) il modello cita l'Art. 2.6 (Fenomeno elettrico) con una motivazione da eventi atmosferici. La validazione non lo intercetta, perché l'articolo esiste ed è una garanzia.
2. **Scenario 2**: calcolo inventato (20% di 300.000 = 210.000 €) nella valutazione, intercettato come importo non verificato.
3. **Scenario 3**: la franchigia fissa 4.1 non è tra le clausole recuperate (problema già aperto dalla Fase 5): il modello la cita comunque, e la validazione la rimuove.
4. **Scenario 4**: la 4.4 compare anche tra le garanzie (avviso "tipo incoerente") e la 3.7 due volte, con due circostanze diverse.
5. **Scenario 1**: esclusione 3.5 (ristrutturazione) poco pertinente; manca la 3.1 attesa, che non è tra le clausole recuperate.

Scostamenti:

| Punto | Previsto | Fatto | Motivo |
|---|---|---|---|
| Dove sta `PreIstruttoriaService` | non indicato | progetto Ai, accanto a `PromptBuilder` | usa `IChatClient`; Core resta senza dipendenze dai modelli |
| Embedding della denuncia | per ricerca | uno solo, usato per clausole e storico | un calcolo in meno |
| `EsitoPreIstruttoria` | campi di §1 | in più richiesta, modello e data di generazione | servono all'intestazione e alla sezione "Denuncia" del Markdown |
| Regola 4 del system prompt | numeri solo da DATI POLIZZA e STATISTICHE | anche da CLAUSOLE | le clausole contengono importi (minimo 500 €, limite 5.000 €) che il modello deve poter riportare |
| Importi ammessi dalla validazione | massimale, franchigia, statistiche | in più gli importi scritti nelle clausole recuperate | come sopra, altrimenti avvisi su citazioni corrette |
| Sinistri simili nel Markdown | "5 più vicini" | tutti i `TopSinistri` (10) | le statistiche sono calcolate su questi (D11): la tabella deve corrispondere |
| Contratti Fase 7 | — | `SegnalazioneDuplicato` e `MotivoSegnalazione` già nella forma di `fase-7.md`, lista vuota | l'esito non cambia forma in Fase 7 |
| Avviso di retry | — | un avviso `Parsing` anche quando il secondo tentativo riesce | il criterio "al più un retry" resta verificabile dalla scheda |

## 10. Commit proposto (non eseguito)

```
fase 6: generazione della scheda di pre-istruttoria con validazione delle citazioni

PreIstruttoriaService (Ai): polizza, embedding della denuncia, clausole,
storico e statistiche, poi la scheda chiesta a qwen3.5 con schema JSON
generato dal tipo, un retry e il fallback in testo libero. PromptBuilder
puro, SchedaParser, CitazioniValidator (articoli normalizzati, citazioni
inesistenti rimosse, tipi incoerenti e importi non verificati segnalati),
SchedaMarkdownRenderer. Comando ask con avanzamento per passi, --out e --raw;
span di telemetria per ogni passo. Dopo il CHECKPOINT 6: regole del prompt
su sezioni, motivazioni, calcoli ed esclusioni; franchigia di base
(Retrieval:ArticoloFranchigiaBase) aggiunta se nessuna franchigia è recuperata.

Verifica: build della solution, 123 test verdi, 4 schede del CHECKPOINT 6
in eval/checkpoint-6 con JSON valido al primo tentativo e nessun avviso.
```
