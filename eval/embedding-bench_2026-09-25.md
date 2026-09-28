# Banco di prova degli embedding — 2026-09-25 10:44

> Generato da `tools/Dusiburg.AI.Sinistri.EmbeddingBench` su `data/embedding_bench.json`: 32 clausole, 25 casi, 10 coppie riformulate e 10 coppie "stesso tema". Distanze coseno (1 − similarità), come `VECTOR_DISTANCE` di SQL Server.

## Sistema

| Voce | Valore |
|---|---|
| Processore | AMD Ryzen AI 7 350 w/ Radeon 860M |
| Driver NPU | 32.0.203.329 |
| GPU | NVIDIA GeForce RTX 5060 Laptop GPU, 8151 MiB, 592.82 |
| Ollama | 0.34.4 |
| FastFlowLM | FLM v1.0.6 |
| EP VitisAI (catalogo Windows ML) | Microsoft.WinML.AMD.NPU.EP.Framework.2_1.8.75.0 (copia locale per il compilatore AIE, vedi docs/fase-1b.md) |
| .NET | 10.0.12 |
| Windows | Microsoft Windows NT 10.0.26200.0 |

## Riepilogo

| Candidato | Percorso | Chip | Runtime | Modello | Dim. | hit@1 | MRR | recall@3 | Margine medio / min | Prima richiesta | Latenza p50 / p95 | Testi/s | Stima 470 testi | Correttezza vs CPU |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `p1-bge-m3` | P1 | CPU | Ollama (num_gpu=0) | bge-m3 | 1024 | 0,60 | 0,730 | 0,78 | 0,069 / -0,075 | 2179 ms | 40 / 47 ms | 17,9 | 26 s | — |
| `p1-embeddinggemma` | P1 | CPU | Ollama (num_gpu=0) | embeddinggemma | 768 | 0,64 | 0,785 | 0,96 | 0,084 / -0,022 | 1329 ms | 17 / 20 ms | 43,6 | 11 s | — |
| `p0-bge-m3-gpu` | P0 | GPU | Ollama (num_gpu=auto) | bge-m3 | 1024 | 0,60 | 0,730 | 0,78 | 0,069 / -0,074 | 2309 ms | 16 / 17 ms | 67,5 | 7 s | — |
| `p2-embed-gemma-npu` | P2 | NPU | FastFlowLM (batch 16) | embed-gemma:300m | 768 | 0,04 | 0,138 | 0,08 | -0,007 / -0,141 | 280 ms | 240 / 253 ms | 3,8 | 124 s | -0,0389 (min -0,1246) |
| `p2-embed-gemma-npu-single` | P2 | NPU | FastFlowLM (1 testo per richiesta) | embed-gemma:300m | 768 | 0,04 | 0,138 | 0,08 | -0,007 / -0,141 | 244 ms | 215 / 223 ms | 3,8 | 125 s | -0,0389 (min -0,1246) |
| `p3a-bge-m3-npu` | P3a | NPU | Windows ML + VitisAI | amd/bge-m3-onnx | 1024 | 0,64 | 0,750 | 0,78 | 0,068 / -0,075 | 2794 ms | 81 / 82 ms | 12,1 | 39 s | 0,9992 (min 0,9988) |
| `p3a-bge-m3-cpu` | P3a | CPU | Windows ML (CPU) | amd/bge-m3-onnx | 1024 | 0,60 | 0,730 | 0,78 | 0,069 / -0,074 | 2748 ms | 1036 / 1069 ms | 1,0 | 450 s | 1,0000 (min 0,9999) |

Note:
- `p1-bge-m3`: determinismo: distanza massima 2,2E-016; `ollama ps`: bge-m3:latest 100% CPU.
- `p1-embeddinggemma`: determinismo: distanza massima 2,2E-016; `ollama ps`: embeddinggemma:latest 100% CPU; bge-m3:latest 100% CPU.
- `p0-bge-m3-gpu`: determinismo: distanza massima 2,2E-016; `ollama ps`: bge-m3:latest 100% GPU; embeddinggemma:latest 100% CPU.
- `p2-embed-gemma-npu`: determinismo: distanza massima 1,1E-016.
- `p2-embed-gemma-npu-single`: determinismo: distanza massima 1,1E-016.
- `p3a-bge-m3-npu`: determinismo: distanza massima 2,2E-016; memoria del processo +1041 MB.
- `p3a-bge-m3-cpu`: determinismo: distanza massima 2,2E-016; memoria del processo +1189 MB.

## Separazione dei quasi-duplicati (antifrode, Fase 7)

| Candidato | Riformulate: media / max | Stesso tema: media / min | Gap (min tema − max riformulate) | Soglia migliore | Accuratezza |
|---|---|---|---|---|---|
| `p1-bge-m3` | 0,150 / 0,224 | 0,394 / 0,302 | 0,078 | 0,263 | 1,00 |
| `p1-embeddinggemma` | 0,097 / 0,153 | 0,286 / 0,213 | 0,060 | 0,183 | 1,00 |
| `p0-bge-m3-gpu` | 0,150 / 0,224 | 0,394 / 0,301 | 0,078 | 0,262 | 1,00 |
| `p2-embed-gemma-npu` | 0,103 / 0,218 | 0,146 / 0,079 | -0,139 | 0,106 | 0,75 |
| `p2-embed-gemma-npu-single` | 0,103 / 0,218 | 0,146 / 0,079 | -0,139 | 0,106 | 0,75 |
| `p3a-bge-m3-npu` | 0,151 / 0,223 | 0,394 / 0,301 | 0,078 | 0,262 | 1,00 |
| `p3a-bge-m3-cpu` | 0,150 / 0,223 | 0,393 / 0,301 | 0,078 | 0,262 | 1,00 |

## Convivenza con la chat (`qwen3.5:9b` su GPU)

| Candidato | Embedding p50 sotto carico | Chat da sola | Chat con embedding | Chat ricaricata | `ollama ps` dopo |
|---|---|---|---|---|---|
| `p1-bge-m3` | 55 ms | 37,6 token/s | 35,8 token/s | no | qwen3.5:9b 87% GPU; bge-m3:latest 100% CPU |
| `p1-embeddinggemma` | 27 ms | 37,6 token/s | 36,5 token/s | no | qwen3.5:9b 87% GPU; embeddinggemma:latest 100% CPU; bge-m3:latest 100% CPU |
| `p0-bge-m3-gpu` | 16 ms | 37,6 token/s | 19,0 token/s | no | bge-m3:latest 100% GPU; qwen3.5:9b 87% GPU; embeddinggemma:latest 100% CPU |
| `p2-embed-gemma-npu` | 231 ms | 37,6 token/s | 37,2 token/s | no | qwen3.5:9b 87% GPU; bge-m3:latest 100% GPU; embeddinggemma:latest 100% CPU |
| `p2-embed-gemma-npu-single` | 229 ms | 37,6 token/s | 35,8 token/s | no | qwen3.5:9b 87% GPU; bge-m3:latest 100% GPU; embeddinggemma:latest 100% CPU |
| `p3a-bge-m3-npu` | 83 ms | 37,6 token/s | 33,8 token/s | no | qwen3.5:9b 87% GPU; bge-m3:latest 100% GPU; embeddinggemma:latest 100% CPU |
| `p3a-bge-m3-cpu` | 1018 ms | 37,6 token/s | 33,9 token/s | no | qwen3.5:9b 87% GPU; bge-m3:latest 100% GPU; embeddinggemma:latest 100% CPU |

## Regola di scelta (fase-1b.md §5)

- `p1-bge-m3`: ammesso
- `p1-embeddinggemma`: ammesso
- `p0-bge-m3-gpu`: escluso — solo riferimento di velocità: l'embedding non va in VRAM (D18)
- `p2-embed-gemma-npu`: escluso — correttezza -0,039 < 0,99 rispetto a `p1-embeddinggemma`
- `p2-embed-gemma-npu-single`: escluso — correttezza -0,039 < 0,99 rispetto a `p1-embeddinggemma`
- `p3a-bge-m3-npu`: ammesso
- `p3a-bge-m3-cpu`: escluso — latenza p50 1036 ms > 300 ms

Migliore per qualità: `p1-embeddinggemma` (MRR 0,785).
Applicando la preferenza per la NPU entro 0,02 di MRR: **`p1-embeddinggemma`** — embeddinggemma su CPU (Ollama (num_gpu=0)), 768 dimensioni.

## Dettaglio per caso (posizione della prima clausola rilevante; 1 = corretta in cima)

| # | Denuncia | Rilevanti | `p1-bge-m3` | `p1-embeddinggemma` | `p0-bge-m3-gpu` | `p2-embed-gemma-npu` | `p2-embed-gemma-npu-single` | `p3a-bge-m3-npu` | `p3a-bge-m3-cpu` |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Si è rotto il tubo sotto il lavello e l'acqua ha rovinato il parquet della cucina | CASA-2.3 | 1 | **2** (CASA-2.4) | 1 | **26** (CASA-3.1) | **26** (CASA-3.1) | 1 | 1 |
| 2 | Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno | CASA-2.3, CASA-2.4 | 1 | 1 | 1 | **8** (RCP-3.4) | **8** (RCP-3.4) | 1 | 1 |
| 3 | Per trovare la perdita l'idraulico ha dovuto rompere le piastrelle del bagno e il massetto | CASA-2.4 | 1 | 1 | 1 | **32** (RCP-3.3) | **32** (RCP-3.3) | 1 | 1 |
| 4 | Da mesi c'è una macchia di muffa sul muro della camera, dietro il termosifone il muro è sempre umido | CASA-3.2 | 1 | 1 | 1 | **8** (CASA-3.1) | **8** (CASA-3.1) | 1 | 1 |
| 5 | Con le piogge l'acqua entra dal tetto perché le tegole sono vecchie e le grondaie non vengono pulite da anni | CASA-3.1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| 6 | Il vento fortissimo della tempesta ha scoperchiato parte del tetto e poi è entrata la pioggia in soffitta | CASA-2.2 | 1 | 1 | 1 | **23** (CASA-2.6) | **23** (CASA-2.6) | 1 | 1 |
| 7 | Il cliente dice che la grandine ha rotto i pannelli solari sul tetto | CASA-3.5, CASA-2.2 | 1 | 1 | 1 | **17** (CASA-3.1) | **17** (CASA-3.1) | 1 | 1 |
| 8 | La grandinata ha ammaccato le tapparelle e crepato il lucernario della mansarda | CASA-3.5 | 1 | 1 | 1 | **21** (CASA-3.3) | **21** (CASA-3.3) | 1 | 1 |
| 9 | Nella casa al mare chiusa per l'inverno sono scoppiati i tubi per il freddo e si è allagato il piano terra | CASA-3.3 | **4** (CASA-3.1) | 1 | **4** (CASA-3.1) | **13** (CASA-3.1) | **13** (CASA-3.1) | **4** (CASA-3.1) | **4** (CASA-3.1) |
| 10 | Dopo un temporale si è bruciata la caldaia e il televisore | CASA-2.5 | **4** (CASA-3.1) | 1 | **4** (CASA-3.1) | **25** (RCP-3.3) | **25** (RCP-3.3) | **3** (CASA-3.1) | **4** (CASA-3.1) |
| 11 | Una sovratensione ha danneggiato il quadro elettrico e l'inverter dell'impianto fotovoltaico | CASA-2.5 | 1 | 1 | 1 | **17** (CASA-3.1) | **17** (CASA-3.1) | 1 | 1 |
| 12 | La lavatrice ha smesso di funzionare, ha dieci anni e il tecnico dice che il motore è consumato | CASA-3.4 | **2** (CASA-3.1) | 1 | **2** (CASA-3.1) | **25** (CASA-3.1) | **25** (CASA-3.1) | **2** (CASA-3.1) | **2** (CASA-3.1) |
| 13 | Quanto resta a carico mio se il fulmine mi brucia il frigorifero? | CASA-4.3 | **3** (CASA-2.5) | **2** (CASA-2.5) | **3** (CASA-2.5) | **8** (CASA-3.1) | **8** (CASA-3.1) | **4** (CASA-2.5) | **3** (CASA-2.5) |
| 14 | C'è una franchigia sui danni da perdita d'acqua dell'impianto? | CASA-4.1 | 1 | 1 | 1 | **23** (CASA-3.1) | **23** (CASA-3.1) | 1 | 1 |
| 15 | I ladri hanno forzato la porta finestra e hanno portato via gioielli e computer | CASA-2.6 | 1 | **2** (CASA-3.6) | 1 | **7** (RCP-3.3) | **7** (RCP-3.3) | 1 | 1 |
| 16 | Furto nella seconda casa in montagna dove non andiamo da tre mesi | CASA-3.6 | 1 | 1 | 1 | **24** (CASA-2.6) | **24** (CASA-2.6) | 1 | 1 |
| 17 | L'acqua della mia lavatrice è filtrata nell'appartamento del vicino di sotto che chiede i danni | CASA-2.7 | **3** (CASA-2.3) | **3** (CASA-2.4) | **3** (CASA-2.3) | **29** (CASA-2.5) | **29** (CASA-2.5) | **3** (CASA-2.3) | **3** (CASA-2.3) |
| 18 | Un pallone ha rotto la vetrata del soggiorno | CASA-2.8 | **4** (CASA-3.5) | **8** (CASA-1.2) | **4** (CASA-3.5) | **4** (CASA-2.6) | **4** (CASA-2.6) | **4** (CASA-3.5) | **4** (CASA-3.5) |
| 19 | Il vento ha divelto la tenda da sole e l'antenna, la casa è ancora in ristrutturazione | CASA-3.7 | 1 | 1 | 1 | **5** (RCP-2.4) | **5** (RCP-2.4) | 1 | 1 |
| 20 | Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento | RCP-2.1 | 1 | **2** (RCP-1.1) | 1 | **18** (CASA-2.1) | **18** (CASA-2.1) | 1 | 1 |
| 21 | Il direttore dei lavori non si è accorto che l'impresa ha posato un'armatura diversa da quella di progetto | RCP-2.2 | **2** (RCP-2.1) | 1 | **2** (RCP-2.1) | **27** (CASA-2.1) | **27** (CASA-2.1) | **2** (RCP-2.1) | **2** (RCP-2.1) |
| 22 | Un operaio è caduto dal ponteggio e contestano all'architetto il ruolo di coordinatore della sicurezza | RCP-2.3 | 1 | 1 | 1 | **26** (RCP-1.2) | **26** (RCP-1.2) | 1 | 1 |
| 23 | Per un errore nella pratica edilizia il permesso di costruire è arrivato con sei mesi di ritardo e il cliente ha perso l'affitto | RCP-2.4 | **4** (CASA-3.6) | **3** (RCP-1.1) | **4** (CASA-3.6) | **8** (RCP-3.4) | **8** (RCP-3.4) | **4** (CASA-3.6) | **4** (CASA-3.6) |
| 24 | Il comune ha multato il cliente per una difformità e lui vuole che la multa la paghi il progettista | RCP-3.3 | **2** (RCP-1.1) | **3** (RCP-1.1) | **2** (RCP-1.1) | **2** (CASA-3.1) | **2** (CASA-3.1) | 1 | **2** (RCP-1.1) |
| 25 | Il professionista sapeva già delle crepe sull'edificio prima di stipulare la polizza e ora arriva la richiesta di risarcimento | RCP-3.4 | **12** (RCP-1.1) | **2** (RCP-1.1) | **12** (RCP-1.1) | **6** (RCP-2.1) | **6** (RCP-2.1) | **14** (RCP-1.1) | **12** (RCP-1.1) |

