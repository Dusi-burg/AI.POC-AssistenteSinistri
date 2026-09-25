# Fase 1b — Banco di prova degli embedding (modello e chip)

> Si inserisce tra la Fase 1 (scaffolding) e la Fase 2 (schema): la dimensione di `VECTOR(n)` e il posizionamento dei modelli si fissano **dopo** questa prova, con dati misurati.
> Nasce dalla richiesta di (a) provare l'embedding sulla NPU del Ryzen AI 7 350 con la chat sulla GPU e (b) cercare un modello di embedding più potente di `bge-m3`.
> Chiusura con **CHECKPOINT 1b**: l'utente sceglie modello e chip sulla base del report.

---

## 1. Perché serve una prova e non una scelta a tavolino

- **Qualità:** i punteggi pubblici (MMTEB) sono medie su molte lingue e molti compiti. Il nostro caso è specifico: italiano, linguaggio assicurativo, garanzie ed esclusioni **semanticamente vicine**. Serve una misura sul nostro tipo di testo.
- **Chip:** ogni acceleratore richiede un runtime diverso e modelli preparati apposta (`fase-0.md` §2 bis). L'NPU in particolare ha un catalogo limitato e un supporto agli embedding ancora giovane: le segnalazioni recenti di FastFlowLM parlano di vettori errati in una versione e di chiamate `/v1/embeddings` in errore su Linux. Va provato sulla macchina reale.
- **Convivenza:** l'obiettivo è che chat ed embedding lavorino **insieme** senza contendersi la VRAM. Anche questo si misura.

---

## 2. Candidati

### Modelli

Punteggi dalle schede ufficiali (MTEB Multilingual, media per task), da prendere come ordine di grandezza:

| Modello | Parametri | Dimensioni | MMTEB (media) | Contesto | Licenza | Note |
|---|---|---|---|---|---|---|
| `bge-m3` (riferimento attuale) | 0.6B | 1024 | 59.56 | 8K | MIT | nessun prefisso richiesto; su NPU esiste l'export ONNX ufficiale AMD per il Vitis AI EP (P3a, contesto ridotto a 512 token) |
| **EmbeddingGemma-300m** | 0.3B | 768 (MRL: 512/256/128) | 61.15 | 2K | Gemma (termini Google) | prefissi obbligatori: query `task: search result \| query: …`, documento `title: none \| text: …`; **l'unico disponibile su NPU con FastFlowLM**; su NPU esiste anche la versione ufficiale AMD per Ryzen AI 1.8 (P2b) |
| multilingual-e5-large-instruct | 0.6B | 1024 | 63.22 | 512 | MIT | prefisso istruzione sulla query; contesto corto (le clausole ci stanno) |
| **Qwen3-Embedding-0.6B** | 0.6B | 1024 (MRL) | 64.33 | 32K | Apache 2.0 | *instruction-aware*: la query va preceduta da un'istruzione (`Instruct: … \nQuery: …`) |
| **Qwen3-Embedding-4B** | 4B | 2560 (MRL → **1024 o 1536**) | 69.45 | 32K | Apache 2.0 | il più potente praticabile; va ridotto con MRL perché `VECTOR` in `float32` arriva a 1998 dimensioni; pesante per la CPU |
| Qwen3-Embedding-8B | 8B | 4096 (MRL) | 70.58 | 32K | Apache 2.0 | **solo di riferimento**: in VRAM contende la GPU alla chat, su CPU è lento; si misura solo se il 4B mostra un salto di qualità netto |

Sulla carta **Qwen3-Embedding-0.6B** batte `bge-m3` a parità di peso (64.3 contro 59.6), e il **4B** è nettamente più forte. EmbeddingGemma è leggermente sopra `bge-m3`. Sull'NPU sono disponibili "pronti" EmbeddingGemma (FastFlowLM e AMD) e `bge-m3` (AMD); Qwen3-Embedding no. Prefissi e istruzioni diversi per modello vanno gestiti nel codice (§6).

### Percorsi hardware

| Id | Percorso | Chip | Runtime | Modelli provati | Integrazione .NET |
|---|---|---|---|---|---|
| **P1** | Ollama, `num_gpu = 0` | CPU | Ollama (già installato) | bge-m3, qwen3-embedding:0.6b, qwen3-embedding:4b, embeddinggemma, (e5 se presente nella libreria Ollama) | OllamaSharp (come oggi) |
| **P2** | FastFlowLM | **NPU** | `flm` (installer `.msi` da ~17 MB, driver NPU ≥ 32.0.203.311: installato .329 ✅) | `Embedding-Gemma-300M-NPU2` (unico embedding del catalogo; compagno `Gemma3-270M-NPU2`) | endpoint OpenAI-compatibile `http://127.0.0.1:52625/v1/embeddings` |
| **P2b** | ONNX Runtime GenAI (OGA) + Ryzen AI 1.8 | **NPU** | nel processo .NET, nessun server | [`amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K`](https://huggingface.co/amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K): EmbeddingGemma **già quantizzato da AMD** (pesi UINT4 AWQ, attivazioni BFP16, contesto 4K), licenza Gemma | `Microsoft.ML.OnnxRuntimeGenAI` da C#, se espone l'API di embedding (da verificare); altrimenti solo misura da Python |
| **P3a** | Windows ML / ONNX Runtime + Vitis AI EP | **NPU** (con fallback CPU dei nodi non supportati) | nel processo .NET, nessun server | [`amd/bge-m3-onnx`](https://huggingface.co/amd/bge-m3-onnx): `bge-m3` **esportato da AMD** per il Vitis AI EP (opset 17, input fisso `(1, 512)`, pesi float32 ~2,2 GB), licenza MIT | `Microsoft.ML.OnnxRuntime` / Windows ML da C#; tokenizer in .NET |
| P3b | come P3a | **NPU** | nel processo .NET | Qwen3-Embedding-0.6B esportato in ONNX e **quantizzato in proprio** (Quark oppure Olive). Il [`onnx-community/Qwen3-Embedding-0.6B-ONNX`](https://huggingface.co/onnx-community/Qwen3-Embedding-0.6B-ONNX) è ONNX generico e non è pronto per l'NPU | come P3a |
| P4 | Lemonade Server | NPU / iGPU / CPU | server AMD | da verificare se gli embedding girano davvero sull'NPU o su llama.cpp (GPU/CPU) | endpoint OpenAI-compatibile |
| P0 | Ollama su GPU | GPU | Ollama | bge-m3 | solo come **riferimento di velocità**, per mostrare il costo dello scambio di modelli in VRAM |
| P5 (facoltativo) | Chat su NPU | NPU | FastFlowLM | `Qwen3.5-9B-NPU2`, lo **stesso modello di chat** di Ollama in versione NPU | solo misura: velocità e qualità di una scheda di pre-istruttoria su NPU rispetto alla GPU. Non cambia l'architettura, serve alla demo ("stesso modello, due chip") |

- **P2** è il percorso più rapido per avere l'NPU al lavoro, ma è vincolato a EmbeddingGemma: nella [collection XDNA2 di FastFlowLM](https://huggingface.co/collections/FastFlowLM/flm-models-xdna2) (verificata il 2026-09-24, 45 modelli) l'**unico modello di embedding è `Embedding-Gemma-300M-NPU2`**, e non ci sono reranker. FastFlowLM carica l'embedding **solo insieme a un modello di chat** (`flm serve <llm> --embed 1`): come "compagno" si usa il più piccolo del catalogo, **`Gemma3-270M-NPU2`**, oppure `Qwen3.5-0.8B-NPU2`, che resta caricato inutilizzato con un consumo minimo di RAM. I tag esatti per `flm` si leggono con `flm list`.
- **P2b** (trovato il 2026-09-25) è lo **stesso modello** di P2 nella versione ufficiale AMD, annunciata nelle [note di rilascio di Ryzen AI 1.8](https://ryzenai.docs.amd.com/en/latest/relnotes.html) ("New embedding model support: embeddinggemma-300m"). Rispetto a P2 non serve tenere caricato un modello di chat "compagno" e gira dentro il processo .NET, ma usa il flusso **OGA** (ONNX Runtime GenAI), non un semplice `InferenceSession`. Da verificare prima di provarlo: se richiede l'installazione di Ryzen AI Software 1.8 e se il pacchetto NuGet di OGA espone l'embedding in C#. Aggiunge un confronto utile: EmbeddingGemma su NPU con due runtime diversi, contro lo stesso modello su CPU con Ollama.
- **P3a** (trovato il 2026-09-25) è il percorso NPU **più promettente per la qualità in italiano**: `bge-m3` è multilingue e AMD lo pubblica già esportato per il Vitis AI EP, quindi niente export fatto da noi. Punti da verificare:
  - i pesi sono in **float32** e non quantizzati: va controllato se il Vitis AI EP li converte da solo in BF16 al momento della compilazione per l'NPU XDNA2, o se la quantizzazione (BF16 con Quark) resta a nostro carico;
  - l'input ha forma **fissa `(1, 512)`**: un testo alla volta, riempito o troncato a 512 token. Le clausole ci stanno, ma nel banco si misura il throughput senza batch, e il testo più lungo del corpus va controllato in token;
  - quanti nodi finiscono in fallback su CPU (lo dice il report di compilazione del Vitis AI EP).
- **P3b** è il ramo "potente ma incerto": porta sull'NPU Qwen3-Embedding-0.6B, ma nessuno lo pubblica pronto per l'NPU AMD (verificato il 2026-09-25), quindi export ONNX e quantizzazione sono a nostro carico, e non tutti gli operatori potrebbero girare sull'NPU. Si tenta **solo dopo P3a**, che fa da prova del runtime. **Tempo massimo: 1 giornata** per P3a + P3b insieme; se non converge si documenta e ci si ferma.
- **P4** si verifica solo in lettura (documentazione e catalogo): lo si prova solo se offre sull'NPU un embedding diverso da quelli di P2, P2b e P3.
- Di conseguenza sull'NPU un embedding diverso da EmbeddingGemma è disponibile **pronto** solo con P3a (`bge-m3`); il più potente (Qwen3-Embedding-0.6B) richiede P3b. Su CPU (P1) i modelli più potenti si provano comunque tutti.
- Il posizionamento della **chat non cambia**: `qwen3.5:9b` su GPU con Ollama. P5 è solo una misura informativa.

---

## 3. Preparazione (azioni che richiedono conferma dell'utente)

Installazioni e download **non** si fanno di iniziativa: si chiede prima.

1. Ollama: `ollama pull bge-m3`, `ollama pull qwen3-embedding:0.6b`, `ollama pull qwen3-embedding:4b`, `ollama pull embeddinggemma` (tag esatti da verificare sulla libreria Ollama al momento).
2. FastFlowLM: installazione da `flm-setup.msi` (GitHub `FastFlowLM/FastFlowLM`), poi `flm list` per i tag esatti, e `flm serve <tag di Gemma3-270M> --embed 1`. Per P5 anche il download di `Qwen3.5-9B-NPU2` (diversi GB: si chiede a parte).
3. P2b: download di `amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K` da Hugging Face; pacchetto NuGet OGA solo nel progetto del banco di prova. Se serve Ryzen AI Software 1.8 si chiede a parte (è un'installazione di sistema).
4. P3a: download di `amd/bge-m3-onnx` (~2,2 GB) da Hugging Face; nessuna installazione di sistema, pacchetti NuGet ONNX Runtime/Windows ML solo nel progetto del banco di prova.
5. P3b (e P3a, se la conversione in BF16 non è automatica): Python per export e quantizzazione, in un ambiente virtuale dedicato.

---

## 4. Banco di prova: `tools/Dusiburg.AI.Sinistri.EmbeddingBench`

Console .NET nella solution (resta utile anche dopo: riprova quando escono nuovi modelli o nuove versioni del runtime NPU).

### Dati — `data/embedding_bench.json`

~25 casi scritti a mano, sul dominio del POC:

```json
{
  "documenti": [
    { "id": "CASA-2.4", "testo": "Garanzia - Acqua condotta. La Società indennizza i danni materiali e diretti causati da fuoriuscita di acqua a seguito di rottura accidentale di impianti idrici..." },
    { "id": "CASA-3.1", "testo": "Esclusione - Infiltrazioni dovute a mancata manutenzione. Sono esclusi i danni da infiltrazioni ..." }
  ],
  "casi": [
    {
      "query": "Si è rotto il tubo sotto il lavello e l'acqua ha rovinato il parquet della cucina",
      "rilevanti": ["CASA-2.4", "CASA-2.5"],
      "distrattori": ["CASA-3.1", "CASA-3.2"]
    }
  ]
}
```

- I documenti sono **bozze brevi delle clausole della Fase 3** (una trentina, entrambi i prodotti), così il banco anticipa il retrieval reale.
- Ogni caso ha un **distrattore vicino** (garanzia contro esclusione sullo stesso tema): è la difficoltà tipica del dominio.
- Più una decina di **coppie di sinistri riformulati** (stessa storia detta con parole diverse) e di coppie diverse ma sullo stesso tema, per stimare in anticipo la separazione utile all'antifrode (Fase 7).

### Misure per ogni combinazione modello × percorso

| Misura | Come |
|---|---|
| **Qualità retrieval** | hit@1, MRR, recall@3 sui casi |
| **Margine sul distrattore** | distanza (distrattore più vicino) − distanza (rilevante più vicino), media e minimo: più è alto, più il modello separa garanzia ed esclusione |
| **Separazione duplicati** | distanze delle coppie riformulate contro quelle delle coppie "stesso tema": serve a capire se una soglia antifrode è praticabile |
| **Latenza singola query** (a caldo) | p50 / p95 su 50 ripetizioni |
| **Throughput indicizzazione** | testi/secondo su batch da 16 (stima del tempo per i ~470 testi del POC) |
| **Primo caricamento** | tempo della prima richiesta a freddo |
| **Memoria** | RAM di processo, VRAM (`nvidia-smi`), `ollama ps` (dove gira il modello) |
| **Convivenza con la chat** | stessa misura di latenza con una generazione di `qwen3.5:9b` in corso su GPU; latenza della chat con e senza embedding in parallelo. Si verifica che **nessun modello venga scaricato dalla VRAM** |
| **Correttezza NPU** | lo **stesso modello** su NPU e su CPU (EmbeddingGemma: FastFlowLM e OGA contro Ollama; `bge-m3`: P3a contro Ollama e ONNX su NPU contro ONNX su CPU; per P3b: ONNX su NPU contro ONNX su CPU): similarità coseno tra i due vettori dello stesso testo ≥ 0.99 in media. Intercetta problemi come la regressione segnalata su FastFlowLM |
| **Determinismo** | stesso testo due volte → distanza ~0 |
| Consumi (facoltativo) | a batteria, variazione di scarica durante l'indicizzazione (indicativo) |

### Report

`eval/embedding-bench_<yyyy-MM-dd>.md`: tabella riassuntiva modello × percorso, dettaglio per caso (dove ogni modello sbaglia), versioni di Ollama, FastFlowLM e driver NPU, raccomandazione motivata.

---

## 5. Regola di scelta (proposta)

1. Si scartano le combinazioni con **correttezza NPU < 0.99**, errori, o che **scaricano la chat dalla VRAM**.
2. Tra le rimaste vince la **qualità** (MRR, poi margine sul distrattore), purché la latenza della singola query a caldo sia **≤ 300 ms** e l'indicizzazione completa stia sotto i **15 minuti**.
3. A parità sostanziale di qualità (differenza di MRR ≤ 0.02) si preferisce l'**NPU** (consumi, e il valore per la demo: "tre acceleratori, ognuno col suo compito"), poi la CPU.
4. Dimensioni: ≤ 1998 (limite di `VECTOR` in float32); per i modelli MRL si prova anche la versione ridotta (es. Qwen3-4B a 1024 e a 1536) e si tiene la più piccola che non perde qualità.

La scelta definitiva è confermata in Fase 9 sul golden set completo, con il confronto già previsto (`eval --embedding-model`), ora esteso al percorso (`--embedding-provider`).

---

## 6. Effetti sul resto del piano

- **Manopole** (Fase 1): `EMBEDDING_PROVIDER` (`ollama` | `openai-compatible` | `onnx` | `onnx-genai`, quest'ultimo solo se vince P2b), `EMBEDDING_ENDPOINT`, `EMBEDDING_MODEL` (sostituisce la vecchia `OLLAMA_EMBEDDING_MODEL`), `EMBEDDING_DIMENSIONS`, `EMBEDDING_NUM_GPU` (solo `ollama`), `EMBEDDING_ONNX_PATH` (solo `onnx` e `onnx-genai`).
- **Profili di embedding** (Fase 4): ogni modello ha prefissi diversi per query e documento (EmbeddingGemma, Qwen3, e5) ed eventualmente una riduzione MRL. Un `EmbeddingProfile` in `Core` (prefisso query, prefisso documento, dimensione nativa, dimensione usata) è selezionato dal nome del modello; `EmbeddingTextBuilder` lo applica. Senza prefissi corretti i modelli instruction-aware perdono qualità in modo evidente: il banco li usa già.
- **Riduzione MRL:** se il runtime non accetta la dimensione ridotta nella richiesta (il parametro `dimensions` di Ollama va verificato), `EmbeddingService` tronca il vettore e lo rinormalizza.
- **`EmbeddingInfo`** (Fase 2) registra anche provider e profilo: vettori calcolati da runtime diversi non si mescolano mai.
- **Health** (Fase 1): i controlli 7, 8 e 10 diventano "per provider"; per `openai-compatible` si interroga `/v1/models`; per `onnx` si carica il modello e si verifica quale execution provider è attivo (NPU o CPU).
- **AppHost** (Fase 1): se vince P2, FastFlowLM si avvia dall'AppHost con `AddExecutable("flm", "flm", …, "serve", <llm>, "--embed", "1")`, come la risorsa `rabbitmq-wsl` di O2C: parte e si ferma con la demo e ha i log nel dashboard.
- **Fase 10.5** si riduce al reranker su NPU (e a P3, se qui è stato rimandato).

---

## 7. Criteri di completamento e CHECKPOINT 1b

- Report `eval/embedding-bench_<data>.md` con almeno P1 (4 modelli), P2 e P3a misurati; P2b e P3b misurati **oppure** documentati come non riusciti, con il motivo.
- Per ogni percorso NPU: correttezza rispetto al riferimento su CPU verificata.
- Convivenza con la chat verificata: nessuno scaricamento del modello di chat.
- Raccomandazione motivata: modello, percorso, dimensione.

**CHECKPOINT 1b:** l'utente sceglie. La scelta fissa `EMBEDDING_PROVIDER`, `EMBEDDING_MODEL` ed `EMBEDDING_DIMENSIONS`, che la Fase 2 usa per lo schema.

Stima: ½ giornata per P1 + P2 e il banco; fino a 1 giornata aggiuntiva per P2b, P3a e P3b.

## 8. Commit proposto (non eseguito)

```
fase 1b: banco di prova degli embedding su CPU e NPU e scelta del modello
```
