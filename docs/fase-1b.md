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

## 7 bis. Esito (2026-09-25)

Report completo: [`eval/embedding-bench_2026-09-25.md`](../eval/embedding-bench_2026-09-25.md). Banco ripetibile con
`dotnet run --project tools/Dusiburg.AI.Sinistri.EmbeddingBench` (opzioni `--candidates`, `--repeat`, `--skip-coexistence`, `--report`).

### Installato sul PC

| Cosa | Dove | Note |
|---|---|---|
| FastFlowLM 1.0.6 (`flm-setup.msi`, non firmato) | `C:\Program Files\flm`, modelli in `%USERPROFILE%\.flm` | `embed-gemma:300m` + compagno `qwen3:0.6b` (Gemma3-270M non è più nel catalogo); server con `flm serve qwen3:0.6b --embed 1`, porta 52625 |
| EP di Windows ML dal catalogo | pacchetti `Microsoft.WinML.*` in `C:\Program Files\WindowsApps` | `EnsureAndRegisterCertifiedAsync` ha scaricato **tutti** gli EP compatibili: VitisAI e RyzenAI Light (NPU AMD 1.8.75), MIGraphX/AMD GPU, NVIDIA TensorRT-RTX. Il codice del banco ora registra solo VitisAI |
| Copia locale dell'EP VitisAI | `%LOCALAPPDATA%\Dusiburg.AI.Sinistri\vitisai-ep` (703 MB) | vedi "P3a" sotto |
| `amd/bge-m3-onnx` + tokenizer BAAI | `%LOCALAPPDATA%\Dusiburg.AI.Sinistri\models\bge-m3-onnx` (2,2 GB) | |
| `amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K` | `…\models\embeddinggemma-300m-npu` (260 MB) | non utilizzabile (P2b) |
| Ollama `embeddinggemma` (621 MB) | Ollama | autorizzato il 2026-09-25 come riferimento CPU di P2 |
| Cache del compilatore VitisAI | `C:\Temp\dusim\vaip\.cache` (~1 GB) | scelta dall'EP; senza cache la prima sessione NPU di `bge-m3` compila per ~6,5 minuti, con la cache si apre in ~1 s |

Ryzen AI Software 1.8 **non** è servito: l'EP della NPU arriva con Windows ML.

### Risultati (25 casi, 32 clausole)

| Candidato | Chip | MRR | hit@1 | recall@3 | Margine medio | Latenza p50 | Stima 470 testi | Correttezza vs CPU | Esito regola §5 |
|---|---|---|---|---|---|---|---|---|---|
| `embeddinggemma` (Ollama) | CPU | **0,785** | 0,64 | **0,96** | **0,084** | **17 ms** | **11 s** | — | ammesso, **migliore** |
| `amd/bge-m3-onnx` (Windows ML + VitisAI) | **NPU** | 0,750 | 0,64 | 0,78 | 0,068 | 81 ms | 39 s | 0,9992 | ammesso |
| `bge-m3` (Ollama) | CPU | 0,730 | 0,60 | 0,78 | 0,069 | 40 ms | 26 s | — | ammesso |
| `bge-m3` (Ollama) | GPU | 0,730 | 0,60 | 0,78 | 0,069 | 16 ms | 7 s | — | escluso (D18): **dimezza la chat**, 37,6 → 19,0 token/s |
| `embed-gemma:300m` (FastFlowLM) | NPU | 0,138 | 0,04 | 0,08 | −0,007 | 240 ms | 124 s | **−0,04** | escluso: vettori errati |
| `amd/bge-m3-onnx` (Windows ML) | CPU | 0,730 | 0,60 | 0,78 | 0,069 | 1036 ms | 450 s | 1,0000 | escluso: troppo lento (input fisso a 512 token) |

- **Convivenza:** nessun percorso CPU o NPU rallenta la chat in modo apprezzabile (33,8–37,2 token/s contro 37,6) e nessuno la fa ricaricare.
- **Antifrode:** tutti i percorsi validi separano perfettamente riformulate e "stesso tema" (accuratezza 1,00). Il margine è di 0,078 con `bge-m3` e di 0,060 con `embeddinggemma`; soglie migliori rispettivamente 0,26 e 0,18. Il default `SOGLIA_DUPLICATO_COSINE = 0.08` è **troppo basso per entrambi**: va ritarato in Fase 7.
- **Determinismo:** distanza massima ~1e-16 ovunque.

### Problemi trovati

- **P2, FastFlowLM 1.0.6:** i vettori di `embed-gemma:300m` non hanno relazione con quelli dello stesso modello su CPU (coseno medio −0,04). La MRR di 0,138 è a livello del caso, sia in batch sia con un testo per richiesta. È la regressione temuta al §1: da segnalare al progetto FastFlowLM, e da riprovare con una versione successiva (il banco lo fa in un minuto).
- **P2b, EmbeddingGemma AMD per Ryzen AI 1.8:** non si apre con l'ONNX Runtime di Windows ML. Usa 224 operatori custom `com.ryzenai`, un `config.json` vuoto e pesi Q4 che l'EP non riconosce (`Unknown tensor data type`, poi crash nativo). Serve il flusso OGA di Ryzen AI Software, più il tokenizer Gemma, che è ad accesso controllato. Tentativo chiuso: lo stesso modello su NPU è già coperto da P2.
- **P3a, compilatore AIE e WindowsApps:** l'EP VitisAI installato dal catalogo non riesce a compilare i modelli float. Il compilatore riceve il percorso delle proprie risorse in forma breve 8.3, troncato a `C:\PROGRA~1\WindowsApps\` (`Failed to load VFS`). L'aggiramento è una **copia locale dello stesso EP**, registrata con `OrtEnv.RegisterExecutionProviderLibrary` (modalità "bring your own EP", uso di sviluppo e test). Con la copia la compilazione riesce:
  - conversione in BF16 automatica;
  - 98,6% degli operatori e il 99,999% dei GOPs sulla NPU;
  - coseno 0,9992 rispetto a Ollama.
- **Chat (non legato all'embedding):** con `OLLAMA_NUM_CTX = 8192` (default della Fase 1) `qwen3.5:9b` occupa 5,85 GB e Ollama ne tiene in VRAM solo 5,13 GB (88% GPU). Con 4096 è al 100%. Oggi la velocità resta ~37 token/s; da valutare in Fase 6 con il prompt reale (contesto 6144, oppure KV cache quantizzata lato server Ollama).
- **Non misurati per scelta dell'utente:** Qwen3-Embedding 0.6B/4B ed e5 (pull non autorizzati al CHECKPOINT 0), P3b (dipende da Qwen3), P5.

### Codice introdotto

- `Ai`: provider `openai-compatible` (`OpenAiCompatibleEmbeddingGenerator`, client HTTP minimo con `encoding_format: float`); controllo 7 di `health` per quel provider (FastFlowLM non elenca il modello di embedding in `/v1/models`: la prova reale è il controllo 8).
- `tools/Dusiburg.AI.Sinistri.EmbeddingBench` (`net10.0-windows10.0.26100.0`, Windows ML self-contained): dataset, candidati, misure, convivenza, report con la regola del §5, generatore ONNX di `bge-m3` (tokenizer XLM-R con numerazione fairseq, verificato a 0,99999 contro Ollama).
- `data/embedding_bench.json`: 32 bozze di clausole, 25 denunce con rilevanti e distrattori, 10 + 10 coppie per l'antifrode.

### Decisione richiesta (CHECKPOINT 1b)

| Opzione | Provider / modello / dimensioni | Pro | Contro |
|---|---|---|---|
| **A (consigliata)** | `ollama` / `embeddinggemma` / **768** su CPU | qualità migliore (MRR +0,035, recall@3 0,96 contro 0,78), la più veloce, nessun componente nuovo | prefissi obbligatori (`EmbeddingProfile` in Fase 4); licenza Gemma; niente NPU nella demo |
| B | `onnx` / `amd/bge-m3-onnx` / **1024** su NPU | NPU nella demo ("tre chip, tre compiti"), CPU libera, vettori corretti | qualità inferiore ad A; `Ai` deve passare a un TFM Windows; copia locale dell'EP, prima compilazione di 6,5 minuti, +1 GB di RAM nel processo dell'API |
| C | `ollama` / `bge-m3` / 1024 su CPU | default attuale, nessun prefisso | peggiore di A in tutto; nessun vantaggio rispetto a B oltre alla semplicità |
| D | prima di scegliere, `ollama pull qwen3-embedding:0.6b` (~640 MB) e rilancio del banco | chiude il confronto previsto dal piano | download aggiuntivo, da autorizzare |

Con A, il percorso NPU resta un'estensione possibile della Fase 10. Passare poi a `bge-m3` su NPU vorrebbe dire rifare gli embedding con `VECTOR(1024)`: con `DbInit` e il comando `embed` sono pochi minuti, e `EmbeddingInfo` (Fase 2) impedisce di mescolare vettori diversi.

**CHECKPOINT 1b — deciso il 2026-09-25: opzione A.**
- `EMBEDDING_PROVIDER=ollama`, `EMBEDDING_MODEL=embeddinggemma`, `EMBEDDING_DIMENSIONS=768`, su CPU (`EMBEDDING_NUM_GPU=0`): nuovi default di `SinistriOptions`;
- prefissi EmbeddingGemma (query `task: search result | query: `, documento `title: none | text: `) obbligatori da Fase 4 (`EmbeddingProfile`);
- percorso NPU (`bge-m3` con Windows ML) rimandato a eventuale estensione della Fase 10; `bge-m3` può essere rimosso da Ollama quando si vuole.

## 8. Commit proposto (non eseguito)

```
fase 1b: banco di prova degli embedding su CPU e NPU e scelta del modello

Banco tools/Dusiburg.AI.Sinistri.EmbeddingBench con dataset data/embedding_bench.json:
qualita' di retrieval, separazione dei quasi-duplicati, latenza, throughput,
determinismo, correttezza NPU contro CPU e convivenza con la chat su GPU.
Provider openai-compatible in Ai per i runtime NPU con server (FastFlowLM).
bge-m3 su NPU con Windows ML e EP VitisAI (copia locale dell'EP: da WindowsApps
il compilatore AIE riceve un percorso 8.3 troncato). FastFlowLM 1.0.6 restituisce
vettori errati; l'EmbeddingGemma AMD per Ryzen AI 1.8 richiede il flusso OGA.

Verifica: report eval/embedding-bench_2026-09-25.md, build della solution, test verdi.
```
