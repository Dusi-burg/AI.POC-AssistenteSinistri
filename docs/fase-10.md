# Fase 10 — Estensioni opzionali (solo su richiesta dell'utente)

> Riferimento: `PLAN.md` §4 Fase 10. **Nessuna di queste estensioni si avvia senza richiesta esplicita.**
> Il punto 3 del piano (Minimal API + pagina HTML) è già coperto dalla Fase 8 (decisione D14): qui restano 6 estensioni, indipendenti tra loro (10.5 e 10.6 aggiunte dopo il confronto con O2C e la verifica dell'hardware).
> Per ognuna: obiettivo, verifiche preliminari (lo stato delle funzionalità SQL Server 2025 va ricontrollato sulla documentazione al momento), passi, criterio di riuscita, stima.

---

## 10.1 Indice vettoriale DiskANN + `VECTOR_SEARCH`

**Obiettivo:** confrontare la ricerca esatta (`VECTOR_DISTANCE` + `ORDER BY`) con la ricerca approssimata su indice, con **50.000 sinistri sintetici**.

Verifiche preliminari:
- se `CREATE VECTOR INDEX` e `VECTOR_SEARCH` sono ancora in preview (e se serve `ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON`);
- se sono disponibili su **LocalDB / Express** o solo sulle edizioni superiori;
- limitazioni note della versione installata (in passato: tabella in sola lettura dopo la creazione dell'indice, filtri applicati *dopo* la ricerca approssimata e quindi possibili risultati < `TOP_N`).

Passi:
1. `seed --sinistri 50000` (nuova opzione del generatore; embedding con `embed`, stima ~1–2 ore su RTX 5060: da confermare, si può ridurre a 20k).
2. `CREATE VECTOR INDEX IX_Sinistro_Embedding ON dbo.Sinistro(Embedding) WITH (METRIC = 'cosine', TYPE = 'diskann');`
3. Variante della query B con `VECTOR_SEARCH(TABLE = dbo.Sinistro AS s, COLUMN = Embedding, SIMILAR_TO = @q, METRIC = 'cosine', TOP_N = @k)` e i filtri SQL applicati sul risultato; `TOP_N` sovradimensionato (es. 5 × `top`) per compensare i filtri.
4. Comando `bench-search --ripetizioni 50`: tempi medi/p95 esatta vs indice, e **overlap** dei risultati (quanti dei top 10 esatti compaiono nei top 10 approssimati).

Riuscita: tabella tempi + overlap nel report `eval/bench_<data>.md`. Stima: 1 giornata (esclusi i tempi di embedding).

---

## 10.2 Embedding lato SQL (`CREATE EXTERNAL MODEL` + `AI_GENERATE_EMBEDDINGS`)

**Obiettivo:** calcolare gli embedding direttamente in T-SQL e confrontare con la pipeline C# (tempi, identità dei vettori).

Verifiche preliminari:
- disponibilità su LocalDB / Express di `CREATE EXTERNAL MODEL`, `AI_GENERATE_EMBEDDINGS` e dell'opzione `external rest endpoint enabled` (`sp_configure`);
- requisito di endpoint **HTTPS**: Ollama espone solo HTTP → serve un reverse proxy TLS locale (es. Caddy) davanti a `localhost:11434`;
- per il certificato del proxy si riusa la CA di casa **Dusiburg Root CA**, già attendibile sul PC, invece di crearne una nuova.

Passi:
1. Reverse proxy TLS `https://ollama.localhost:11443` → `http://localhost:11434` con certificato emesso da Dusiburg Root CA.
2. `CREATE EXTERNAL MODEL OllamaEmbedding WITH (LOCATION = 'https://…/api/embed', API_FORMAT = 'Ollama', MODEL_TYPE = EMBEDDINGS, MODEL = 'bge-m3');` (sintassi da verificare).
3. Script `db/900_embed_sql.sql`: `UPDATE dbo.Clausola SET Embedding = AI_GENERATE_EMBEDDINGS(<testo> USE MODEL OllamaEmbedding)`, con lo stesso formato di testo di `EmbeddingTextBuilder` riscritto in T-SQL.
4. Confronto: distanza coseno tra vettore C# e vettore SQL per ogni riga (atteso ~0), tempi totali.

Riuscita: tabella di confronto nel README. Stima: 1 giornata (il proxy TLS è la parte incerta).

---

## 10.3 Ricerca ibrida con full-text + Reciprocal Rank Fusion

**Obiettivo:** combinare ricerca lessicale e vettoriale sulle clausole (e sullo storico), per catturare termini esatti ("fotovoltaico", "D.Lgs. 81/08") che l'embedding può diluire.

Verifica preliminare **bloccante**: la ricerca full-text (`CONTAINSTABLE`) **non è disponibile su LocalDB** secondo la documentazione nota; va verificato sull'istanza. Se manca:
- opzione a) istanza SQL Server 2025 Express/Developer completa;
- opzione b) componente lessicale BM25 in C# in memoria (60 clausole: banale), fondendo i ranking con RRF lato applicazione.
Si chiede all'utente quale strada seguire.

Passi (con full-text disponibile):
1. Catalogo e indice full-text su `Clausola(Titolo, Testo)` con lingua italiana (LCID 1040).
2. Query con due CTE (rank lessicale da `CONTAINSTABLE(... , FREETEXT…)`, rank vettoriale da `VECTOR_DISTANCE`) e fusione `RRF = Σ 1 / (60 + rank)`, `k = 60`.
3. `eval --modalita vettoriale|ibrida` per confrontare recall@5 e MRR sul golden set.

Riuscita: la modalità ibrida migliora (o no, documentato) le metriche della Fase 9. Stima: ½–1 giornata.

---

## 10.4 Tool calling (agente)

**Obiettivo:** invece della pipeline fissa della Fase 6, un agente che decide quali informazioni recuperare.

Passi:
1. Funzioni esposte al modello con `AIFunctionFactory.Create` (Microsoft.Extensions.AI) e `FunctionInvokingChatClient` nella pipeline del `ChatClientBuilder`:
   - `dati_polizza(numero)`,
   - `cerca_clausole(testo, prodotto)`,
   - `cerca_storico(testo, prodotto, provincia?, importoMin?, causa?)`,
   - `statistiche_simili(testo, prodotto, …)`.
2. Nuovo servizio `PreIstruttoriaAgentService` con lo stesso output (`EsitoPreIstruttoria`) e la stessa **validazione delle citazioni** (le clausole valide sono quelle effettivamente restituite dai tool durante la conversazione).
3. Verifica preliminare: supporto del tool calling di `qwen3.5:9b` in Ollama e comportamento con `think = false`.
4. Comando `ask --agente` e interruttore nella UI; log delle chiamate ai tool mostrato nella scheda (utile in demo per far vedere il "ragionamento").
5. Limite di sicurezza: massimo 6 chiamate a tool per richiesta.

Riuscita: le 4 schede demo generate anche in modalità agente, confrontate con la pipeline fissa (qualità, tempi, numero di chiamate). Stima: 1 giornata.

---

## 10.5 Reranker sull'NPU (e completamento del percorso ONNX)

> Gran parte dell'esperimento NPU è stato anticipato nella **Fase 1b** (embedding su NPU contro CPU, ricerca di un modello più potente). Qui restano due estensioni.

**a) Reranker.** Un *cross-encoder* riordina le 10–15 clausole candidate della ricerca vettoriale prima di passarne 5 al modello di chat. Di solito migliora la precisione proprio sui casi difficili del dominio (garanzia contro esclusione vicine).
- Candidati: Qwen3-Reranker-0.6B, bge-reranker-v2-m3 (FastFlowLM **non** ha reranker nel catalogo XDNA2, verificato il 2026-09-24: sull'NPU si passa da ONNX con Windows ML come il percorso P3 della Fase 1b, oppure da Lemonade se nel frattempo li supporta).
- `IRerankService` in `Core`, attivato da una manopola `RERANKER_MODEL` (vuoto = disattivato); si applica in `RicercaService` dopo la query SQL.
- Misura: `eval --modalita rerank` contro la ricerca vettoriale pura (recall@5, MRR, latenza aggiunta). Si tiene solo se migliora la qualità senza superare ~300 ms di latenza aggiunta.

**b) Percorso ONNX su NPU (P3), se in Fase 1b è stato rimandato.** Stessi passi e stesse misure della Fase 1b §2–§4, con un tempo massimo di 1 giornata.

Riuscita: tabella comparativa nel README; il reranker entra nella configurazione di default solo se i numeri lo giustificano. Stima: 1 giornata.

---

## 10.6 Provider Anthropic per la chat (come in O2C)

**Obiettivo:** confrontare in demo il modello locale con un modello in cloud sulla stessa scheda, riusando la `ModelClientFactory` di O2C (`MODEL_PROVIDER = ollama | anthropic`, chiave in `Parameters:anthropic-api-key` negli user-secrets dell'AppHost).

Passi:
1. Porting di `ModelOptions` / `ModelClientFactory` con i due provider. Anthropic ha regole diverse sui parametri (per esempio niente `temperature` sui modelli più recenti, come annotato in O2C) e usa l'output strutturato del proprio SDK.
2. L'embedding resta **sempre locale**: cambia solo il modello della chat.
3. Nella Web, un selettore "modello" sulla pagina Pre-istruttoria (solo se la chiave è configurata); nella CLI, `ask --provider anthropic`.
4. Confronto sulle 4 schede demo: citazioni valide, avvisi della validazione, tempi, costo per scheda.

Nota: la POC nasce "tutto in locale" (non-obiettivi del piano). Questa estensione manda in cloud il testo di denunce **sintetiche**; va comunque dichiarato in demo. Stima: ½ giornata.

---

## Commit proposti (non eseguiti), uno per estensione

```
fase 10.1: indice vettoriale DiskANN e confronto con la ricerca esatta
fase 10.2: embedding calcolati in SQL con external model
fase 10.3: ricerca ibrida full-text + vettoriale con reciprocal rank fusion
fase 10.4: pre-istruttoria in modalità agente con tool calling
fase 10.5: reranker delle clausole su NPU
fase 10.6: provider Anthropic per la chat in alternativa a Ollama
```
