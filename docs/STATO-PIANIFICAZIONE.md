# Stato del piano — aggiornato al 2026-09-25

> Punto di ripartenza per la prossima sessione.

## Dove siamo

- **CHECKPOINT 0 superato** (dettaglio in `fase-0.md`, fine file):
  - `bge-m3` installato, dimensione 1024, su CPU accanto a `qwen3.5:9b` su GPU;
  - D1–D20 confermate; Q5: recall@5 ≥ 0.7; Q6: script SQL + Dapper;
  - Fase 1b autorizzata solo per **FastFlowLM** e **modelli AMD ONNX** (`amd/bge-m3-onnx`, `amd/embeddinggemma-300m_npu_rai_1.8.0_npu_4K`, Ryzen AI Software 1.8 se serve). **Non** autorizzati: modelli Ollama aggiuntivi e `Qwen3.5-9B-NPU2`.
- **Fase 1 completata** (esito e scostamenti in `fase-1.md` §9 bis): solution `Dusiburg.AI.Sinistri.slnx`, AppHost Aspire, ServiceDefaults, opzioni validate, comando `health`, 35 test verdi.
- Il repository git **non** è ancora inizializzato: lo fa l'utente.

## Da fare alla ripresa

1. (Utente) `git init`, rami `main` e `develop`, primo commit con il testo proposto in `fase-1.md` §10.
2. **Fase 1b** — banco di prova degli embedding (`fase-1b.md`), limitato ai percorsi autorizzati:
   - CPU con Ollama: `bge-m3` (riferimento);
   - NPU con FastFlowLM: `Embedding-Gemma-300M-NPU2` (P2);
   - NPU con Windows ML/ONNX: `amd/bge-m3-onnx` (P3a) ed EmbeddingGemma per Ryzen AI 1.8 (P2b).
3. Poi **CHECKPOINT 1b**: scelta di modello e chip dell'embedding, quindi `EMBEDDING_DIMENSIONS` definitivo.
