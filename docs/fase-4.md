# Fase 4 — Embedding

> Riferimento: `PLAN.md` §4 Fase 4. Prerequisito: modello, provider e dimensione dell'embedding scelti al **CHECKPOINT 1b** (`fase-1b.md`).

## Obiettivo

Calcolare con il modello scelto in Fase 1b (su CPU o NPU) e salvare in SQL il vettore di ogni clausola e di ogni sinistro. Comando `embed [--solo-mancanti]`.

---

## 1. `EmbeddingService` (progetto `Ai`)

```csharp
public interface IEmbeddingService   // in Core
{
    // Testo da cercare (denuncia, ricerca): applica il prefisso "query" del profilo
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken);

    // Testi da indicizzare (clausole, sinistri) o da confrontare in modo simmetrico (antifrode): prefisso "documento"
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);

    EmbeddingProfile Profile { get; }   // modello, provider, prefissi, dimensioni
}
```

Implementazione su `IEmbeddingGenerator<string, Embedding<float>>`, creato da `EmbeddingGeneratorFactory` (Fase 1) in base al provider scelto al CHECKPOINT 1b: Ollama (CPU), OpenAI-compatibile (FastFlowLM su NPU) oppure ONNX (Windows ML su NPU). Il servizio non sa su quale chip gira.

- **Due metodi, non uno:** i modelli *instruction-aware* (Qwen3-Embedding, EmbeddingGemma, e5-instruct) vogliono prefissi diversi per la query e per il documento. Usare il prefisso sbagliato degrada la qualità in modo evidente, quindi la distinzione è nell'interfaccia e non è lasciata al chiamante.
- `EmbedDocumentsAsync` chiama `GenerateAsync(texts)` una volta per batch (`Retrieval:EmbeddingBatchSize`, default 16). Il batching a livello di tabella lo fa la pipeline.
- **Riduzione MRL:** se il profilo ha una dimensione usata minore di quella nativa (es. Qwen3-Embedding-4B da 2560 a 1024), la si chiede al runtime quando lo supporta (parametro `dimensions`, da verificare per provider); altrimenti `EmbeddingService` tronca e **rinormalizza** il vettore.
- **Controllo di dimensione** su ogni vettore finale: lunghezza ≠ `EMBEDDING_DIMENSIONS` → `InvalidOperationException` con modello, provider, valore atteso e valore ricevuto.
- **Retry semplice:** fino a 3 tentativi con attesa 1 s, 3 s, 9 s su `HttpRequestException`, timeout e risposte 5xx; niente retry su 4xx (es. modello inesistente). Ciclo esplicito, senza librerie di resilienza; il `HttpClient` è quello dedicato di Fase 1, senza la resilienza standard di ServiceDefaults. Per il provider `onnx` (nel processo) il retry non serve.
- **Mai in VRAM (D18):** con il provider `ollama` ogni richiesta porta `num_gpu: 0` (`EMBEDDING_NUM_GPU`), come le opzioni della chat in O2C (`AddOllamaOption(OllamaOption.NumGpu, 0)`); con i provider NPU la GPU non è coinvolta per costruzione. Il controllo 10 di `health` mostra dove gira davvero l'embedding.
- `keep_alive` lungo (es. 30 minuti) con Ollama, così il modello resta caricato tra un batch e l'altro e durante la demo. Con FastFlowLM il modello resta caricato finché il server gira (avviato dall'AppHost).
- Latenze e throughput attesi: quelli misurati nel banco di prova della Fase 1b, riportati nel riepilogo di fase e confrontati con i valori reali della pipeline.

### `EmbeddingProfile` (Core)

Un record per modello supportato, scelto in base a `EMBEDDING_MODEL`. I prefissi sono quelli indicati nelle schede dei modelli e verificati nel banco di prova:

| Modello | Prefisso query | Prefisso documento | Dim. nativa → usata |
|---|---|---|---|
| `bge-m3` | — | — | 1024 → 1024 |
| Qwen3-Embedding (0.6B / 4B) | `Instruct: Dato il testo di una denuncia di sinistro, trova le clausole di polizza e i sinistri pertinenti\nQuery: ` (istruzione da tarare nel banco; le istruzioni si possono scrivere anche in inglese) | — | 1024 → 1024 · 2560 → 1024/1536 |
| **`embeddinggemma`** (scelto al CHECKPOINT 1b) | `task: search result \| query: ` | `title: none \| text: ` | 768 → 768 |
| multilingual-e5-large-instruct | `Instruct: …\nQuery: ` | — | 1024 → 1024 |

Un modello non presente nella tabella produce un errore esplicito all'avvio: non si usa un modello instruction-aware "senza profilo".

## 2. Testo da vettorizzare (`EmbeddingTextBuilder`, progetto `Core`)

| Entità | Formato |
|---|---|
| Clausola | `"{TipoClausola.Descrizione} - {Titolo}. {Testo}"` |
| Sinistro | `"Causa: {CausaSinistro.Descrizione}. {Descrizione} Esito perizia: {EsitoPerizia}"`; la parte `Esito perizia: …` si omette se `EsitoPerizia` è NULL |
| Denuncia nuova (Fasi 5–7) | `"Causa: {causa}. {testo}"` se la causa è nota, altrimenti solo `{testo}` |

Il prefisso del profilo (§1) si aggiunge **dopo**, in `EmbedQueryAsync` / `EmbedDocumentsAsync`: `EmbeddingTextBuilder` produce solo il contenuto. Clausole e sinistri sono sempre "documenti"; la denuncia nuova è una "query" per il retrieval (Fasi 5–6) e un "documento" per l'antifrode (Fase 7, confronto simmetrico con sinistri già indicizzati come documenti).

Si usano le **etichette leggibili** (`[Description]`, es. "Acqua condotta") e non i nomi dei membri (`AcquaCondotta`): il modello di embedding lavora meglio su testo naturale. La classe è in `Core` perché la usano sia la pipeline sia il retrieval, e deve produrre lo stesso formato in entrambi i casi.

⚠️ Scelta da validare in Fase 9: includere l'esito di perizia nel vettore del sinistro avvicina i casi con lo stesso motivo di rigetto, ma allontana leggermente la denuncia nuova (che l'esito non ce l'ha). Se il retrieval dello storico risulta debole, la variante "solo causa + descrizione" è un'alternativa da misurare.

## 3. Scrittura su SQL (progetto `Data`)

### Opzione preferita: tipo nativo `SqlVector<float>`

`Microsoft.Data.SqlClient` ≥ 6.1 espone `Microsoft.Data.SqlTypes.SqlVector<float>` e `SqlDbType.Vector`. Dapper non conosce il tipo, quindi si passa il parametro con un piccolo adattatore:

```csharp
internal sealed class VectorParameter(float[] values) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name)
    {
        var parameter = new SqlParameter(name, SqlDbType.Vector) { Value = new SqlVector<float>(values) };
        command.Parameters.Add(parameter);
    }
}

await connection.ExecuteAsync(
    "UPDATE dbo.Clausola SET Embedding = @embedding WHERE Id = @id",
    new { id, embedding = new VectorParameter(vector) });
```

### Alternativa (fallback): stringa JSON + `CAST`

Se il tipo nativo non funziona con LocalDB o con la versione del pacchetto: il vettore si serializza come `"[0.12,-0.03,...]"` (`JsonSerializer`, cultura invariante) e si converte in SQL con `CAST(@embedding AS VECTOR(n))`. Qui `n` è l'intero `EMBEDDING_DIMENSIONS` inserito nel testo SQL da un `SqlTextBuilder` (valore validato in Fase 1, mai testo libero).

Lo sviluppo parte dall'opzione nativa con un test di integrazione; se fallisce si passa al fallback e lo si segnala nel riepilogo di fase. Entrambe le strade sono dietro la stessa interfaccia `IVectorParameterFactory`, così il resto del codice non cambia.

### Aggiornamento a blocchi

- Lettura degli elementi da elaborare (`Id`, testo) in pagine da 200.
- Per ogni batch da 16: embedding → `UPDATE` delle 16 righe in una transazione (16 `UPDATE` parametrizzati sulla stessa connessione; con questi volumi non serve un TVP).
- In caso di errore su un batch dopo i retry: il comando si ferma, le righe già scritte restano (quindi `--solo-mancanti` riprende da lì).
- A fine elaborazione: `MERGE` su `dbo.EmbeddingInfo` (Id = 1) con modello, provider, dimensioni e data.

## 4. Comando `embed [--solo-mancanti] [--solo clausole|sinistri]`

1. Controllo all'avvio (Fase 1): modello presente e dimensione coerente con configurazione e colonne del DB.
2. **Controllo del modello:** se `EmbeddingInfo.Modello` o `EmbeddingInfo.Provider` sono valorizzati e diversi da `EMBEDDING_MODEL` / `EMBEDDING_PROVIDER`, `--solo-mancanti` viene rifiutato (i vettori nuovi e vecchi non sarebbero confrontabili): si chiede di rieseguire `embed` completo.
3. Senza `--solo-mancanti`: si ricalcola tutto (`Embedding = NULL` non serve, si sovrascrive).
4. Barra di avanzamento per tabella (`[clausole] 48/60`, `[sinistri] 160/410`), poi riepilogo:
   ```
   Clausole: 60 vettori in 00:00:07
   Sinistri: 410 vettori in 00:00:41
   Totale:   470 vettori in 00:00:12 (modello embeddinggemma, 768 dim)
   Righe con Embedding NULL: 0
   ```

Classi coinvolte: `EmbeddingService` (Ai), `EmbeddingTextBuilder` (Core), `EmbeddingPipeline` (Ingestion: pagine, batch, avanzamento), `EmbeddingRepository` (Data: lettura testi, update vettori, `EmbeddingInfo`), `EmbedCommand` (Cli).

## 5. Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `EmbeddingTextBuilderTests.Sinistro_SenzaEsito_OmetteEsito` | unit | formato con e senza esito di perizia |
| `EmbeddingTextBuilderTests.Clausola_UsaEtichettaLeggibile` | unit | "Franchigia / scoperto / limite - …" |
| `EmbeddingServiceTests.EmbedBatch_DimensioneErrata_Throws` | unit (generatore finto) | errore esplicito su dimensione diversa |
| `EmbeddingServiceTests.EmbedBatch_ErroreTransitorio_Riprova` | unit (generatore finto) | 2 errori e poi successo → risultato corretto, 3 chiamate |
| `EmbeddingRepositoryTests.UpdateVector_RoundTrip` | integrazione (`VECTOR(4)`) | scrittura e `VECTOR_DISTANCE` con un vettore noto (distanza 0 da sé stesso) |

## 6. Criteri di completamento

- `embed` su DB appena popolato: `SELECT COUNT(*) FROM dbo.Clausola WHERE Embedding IS NULL` e lo stesso su `Sinistro` danno 0.
- Tempo totale stampato a video.
- `embed --solo-mancanti` subito dopo non elabora nulla ("0 elementi da elaborare").
- `health` mostra `EmbeddingInfo` coerente con la configurazione.
- Il riepilogo di fase dice quale strada è stata usata (tipo nativo `SqlVector` o fallback JSON).
- Build della solution e test verdi.

## 7. Commit proposto (non eseguito)

```
fase 4: calcolo e salvataggio degli embedding di clausole e sinistri
```
