# Stato del piano — aggiornato al 2026-09-28

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file).
- **Fase 1 completata** (`fase-1.md` §9 bis): solution, AppHost Aspire, comando `health`.
- **Fase 1b completata, CHECKPOINT 1b superato** (`fase-1b.md` §7 bis): `embeddinggemma` su CPU con Ollama, **768 dimensioni**.
- **Fase 2 completata** (`fase-2.md` §7 bis): schema con `VECTOR(768)`, lookup dagli enum, tool `DbInit`, controllo 9 di `health`.
- **Fase 3 completata** (`fase-3.md`, "Esito"): clausole, generatore di dati sintetici, `DemoCatalog`, seed in `DbInit`; limite dell'Art. 4.4 a 6.000 €.
- **Fase 4 completata** (`fase-4.md` §6 bis): comando `embed` con prefissi EmbeddingGemma, tipo nativo `SqlVector<float>`, `EmbeddingInfo`; 470 vettori in 24 s su CPU.
- **Fase 5 completata** (`fase-5.md` §8 bis): ricerca di clausole e storico, statistiche in SQL, `search-clausole` / `search-sinistri`; soglia integrativa 0,70, Art. 2.4 e 3.7 ritoccati; 5 scenari soddisfatti.
- **Fase 6 completata, CHECKPOINT 6 superato** (`fase-6.md` §9 bis): comando `ask`, prompt rivisto al checkpoint, franchigia di base `Art. 4.1`; 4 schede in `eval/checkpoint-6` senza avvisi, 12–17 s ciascuna.
- **Fase 7 completata** (`fase-7.md` §2 bis e §6 bis): `fraud-scan` con valutazione (9 coppie attese su 10 a 0,05, precision 0,82 sulle coppie con un legame); controllo della nuova denuncia su `Sinistro.EmbeddingAntifrode` (sola descrizione) con `SOGLIA_DUPLICATO_DENUNCIA` 0,07; `embed` ora calcola 880 vettori (31 s). Output in `eval/fase-7`.
- **Fase 8 completata** (`fase-8.md`, "Streaming" e §6 bis): API minimale con `ProblemDetails`, OpenAPI e `Api.http`; UI Razor Pages con pre-istruttoria a passi in streaming (SSE), articoli citati che aprono la clausola, link alla traccia nel dashboard, ricerche, antifrode, stato. Verificata sotto l'AppHost con DB e Ollama reali e con Ollama simulato irraggiungibile; 146 test verdi.
- Aperto per la Fase 9: includere o no l'esito di perizia nel vettore del sinistro (`fase-4.md` §2; in Fase 7 serve al fraud-scan e ostacola il confronto con la denuncia nuova); imprecisioni residue delle schede del CHECKPOINT 6 come casi del golden set (`fase-6.md` §9 bis; lo scenario 4 ripete ancora l'Art. 3.7 tra le esclusioni); interventi sui template del seed per ridurre i falsi positivi del fraud-scan (`fase-7.md` §6 bis); `docs/demo.md` con le manopole (`fase-8.md` §4).

## Da fare alla ripresa

1. (Utente) prova della demo nel browser (impaginazione, passi a video, apertura della traccia nel dashboard), poi commit della Fase 8 con il testo di `fase-8.md` §7.
2. **Fase 9**: valutazione e test (`fase-9.md`).
