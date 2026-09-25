# Stato del piano — aggiornato al 2026-09-25

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file).
- **Fase 1 completata** (`fase-1.md` §9 bis): solution, AppHost Aspire, comando `health`.
- **Fase 1b eseguita, in attesa del CHECKPOINT 1b** (`fase-1b.md` §7 bis, report `eval/embedding-bench_2026-09-25.md`):
  - `embeddinggemma` su CPU con Ollama: la qualità migliore (MRR 0,785) e la più veloce (17 ms);
  - `bge-m3` su NPU con Windows ML + VitisAI: funziona (MRR 0,750, 81 ms, coseno 0,9992 contro la CPU), ma richiede una copia locale dell'EP;
  - FastFlowLM 1.0.6: vettori errati, escluso; EmbeddingGemma AMD per Ryzen AI 1.8: non caricabile senza Ryzen AI Software;
  - `bge-m3` su GPU dimezza la velocità della chat: conferma D18.
- Aperto per la Fase 6: con `OLLAMA_NUM_CTX=8192` la chat è all'88% in GPU.
- Aperto per la Fase 7: `SOGLIA_DUPLICATO_COSINE=0.08` è troppo bassa (soglie misurate 0,18–0,26).

## Da fare alla ripresa

1. **CHECKPOINT 1b**: scelta tra le opzioni A–D di `fase-1b.md` §7 bis, che fissa `EMBEDDING_PROVIDER`, `EMBEDDING_MODEL` ed `EMBEDDING_DIMENSIONS`.
2. Allineare i default in `SinistriOptions` alla scelta.
3. Commit della Fase 1b (testo in `fase-1b.md` §8), poi **Fase 2** (schema e `DbInit`).
