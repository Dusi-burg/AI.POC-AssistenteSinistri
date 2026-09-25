# Stato del piano — aggiornato al 2026-09-25

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file).
- **Fase 1 completata** (`fase-1.md` §9 bis): solution, AppHost Aspire, comando `health`.
- **Fase 1b completata, CHECKPOINT 1b superato** (`fase-1b.md` §7 bis): `embeddinggemma` su CPU con Ollama, **768 dimensioni**.
- **Fase 2 completata** (`fase-2.md` §7 bis): schema con `VECTOR(768)`, lookup dagli enum, tool `DbInit`, controllo 9 di `health`.
- Aperto per la Fase 4: prefissi EmbeddingGemma obbligatori (`EmbeddingProfile`), scrittura di `EmbeddingInfo`.
- Aperto per la Fase 6: con `OLLAMA_NUM_CTX=8192` la chat è all'88% in GPU.
- Aperto per la Fase 7: `SOGLIA_DUPLICATO_COSINE=0.08` è troppo bassa (soglia misurata per `embeddinggemma`: 0,18).

## Da fare alla ripresa

1. (Utente) commit della Fase 2 con il testo di `fase-2.md` §8.
2. **Fase 3**: dati sintetici — clausole (`003_seed_clausole.sql`), anagrafiche e sinistri con Bogus, duplicati attesi; stop "morbido" per la revisione dei testi delle clausole.
