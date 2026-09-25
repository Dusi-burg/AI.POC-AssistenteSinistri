# Stato del piano — aggiornato al 2026-09-25

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file).
- **Fase 1 completata** (`fase-1.md` §9 bis): solution, AppHost Aspire, comando `health`.
- **Fase 1b completata, CHECKPOINT 1b superato** (`fase-1b.md` §7 bis): `embeddinggemma` su CPU con Ollama, **768 dimensioni**.
- **Fase 2 completata** (`fase-2.md` §7 bis): schema con `VECTOR(768)`, lookup dagli enum, tool `DbInit`, controllo 9 di `health`.
- **Fase 3 completata** (`fase-3.md`, "Esito"): clausole, generatore di dati sintetici, `DemoCatalog`, seed in `DbInit`; limite dell'Art. 4.4 a 6.000 €.
- **Fase 4 completata** (`fase-4.md` §6 bis): comando `embed` con prefissi EmbeddingGemma, tipo nativo `SqlVector<float>`, `EmbeddingInfo`; 470 vettori in 24 s su CPU.
- Aperto per la Fase 6: con `OLLAMA_NUM_CTX=8192` la chat è all'88% in GPU.
- Aperto per la Fase 7: `SOGLIA_DUPLICATO_COSINE=0.08` è troppo bassa (soglia misurata per `embeddinggemma`: 0,18).
- Aperto per la Fase 9: includere o no l'esito di perizia nel vettore del sinistro (`fase-4.md` §2).

## Da fare alla ripresa

1. (Utente) `DbInit` e poi `embed` sul DB `Sinistri` (i diagrammi SSMS salvati nel DB andranno persi).
2. (Utente) commit delle Fasi 3 e 4 con i testi di `fase-3.md` e `fase-4.md`.
3. **Fase 5**: retrieval (ricerca di clausole e sinistri simili, filtri SQL, statistiche).
