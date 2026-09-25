# Stato del piano — aggiornato al 2026-09-25

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file).
- **Fase 1 completata** (`fase-1.md` §9 bis): solution, AppHost Aspire, comando `health`.
- **Fase 1b completata, CHECKPOINT 1b superato** (`fase-1b.md` §7 bis, report `eval/embedding-bench_2026-09-25.md`):
  - scelta **A**: `embeddinggemma` su CPU con Ollama, **768 dimensioni** (MRR 0,785, 17 ms), ora default di `SinistriOptions`;
  - scartati: FastFlowLM 1.0.6 (vettori errati), `bge-m3` su GPU (dimezza la chat); `bge-m3` su NPU con Windows ML funziona ma è rimandato (Fase 10).
- Aperto per la Fase 4: prefissi EmbeddingGemma obbligatori (`EmbeddingProfile`).
- Aperto per la Fase 6: con `OLLAMA_NUM_CTX=8192` la chat è all'88% in GPU.
- Aperto per la Fase 7: `SOGLIA_DUPLICATO_COSINE=0.08` è troppo bassa (soglia misurata per `embeddinggemma`: 0,18).

## Da fare alla ripresa

1. (Utente) commit della Fase 1b con il testo di `fase-1b.md` §8.
2. **Fase 2**: schema con `VECTOR(768)`, lookup, tool `DbInit` (`fase-2.md`).
