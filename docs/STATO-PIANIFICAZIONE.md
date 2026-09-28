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
- **Fase 9 completata** (`fase-9.md` §7 bis): golden set di 15 casi, comando `eval` con confronto dei modelli su DB dedicato; `embeddinggemma` confermato (recall@5 **0,76**, MRR 0,92; `bge-m3` 0,58); CI GitHub con esclusione dei test di integrazione su LocalDB precedenti alla 2025; README EN + IT, `docs/architettura.md`, `docs/demo.md`, `docs/il-progetto-in-breve.md`. 152 test verdi. **Definition of Done del POC raggiunta**, salvo la CI da verificare al primo push e le schermate.
- **Fase 10.1 completata** (`fase-10.md` §10.1 bis): indice DiskANN e `VECTOR_SEARCH` su LocalDB (anteprima, filtri dopo la ricerca, tabella di sola lettura) misurati con `bench-search` su 50.000 sinistri in un DB dedicato: esatta 96 ms, indice `TOP_N` = 20×k 14 ms con gli stessi risultati; con filtri selettivi il post-filtro fa perdere risultati. 159 test verdi.
- Ancora aperti, non bloccanti: esito di perizia nel vettore del sinistro (oggi risolto con il secondo vettore antifrode); imprecisioni del modello (articolo ripetuto in due voci, definizione tra le garanzie; `fase-6.md` §9 bis); interventi sui template del seed per i falsi positivi del fraud-scan (`fase-7.md` §6 bis); schermate in `docs/images/`.

## Da fare alla ripresa

1. (Utente) commit della Fase 10.1 (testo in `fase-10.md` §10.1 bis e nel riepilogo della sessione); controllo della CI al push.
2. (Utente) revisione del golden set, prova della demo nel browser e schermate per `docs/images/`.
3. **Fase 10**, altre estensioni facoltative su richiesta (`fase-10.md`): 10.2 embedding in T-SQL, 10.3 ricerca ibrida con full-text e RRF, 10.4 tool calling, 10.5 reranker su NPU, 10.6 provider Anthropic.
