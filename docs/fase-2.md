# Fase 2 — Database e schema

> Riferimento: `PLAN.md` §4 Fase 2. Applica le decisioni D2 (tabelle al singolare), D3 (enum come lookup), D4 (`Sinistro.Numero`) e D17 (tool `DbInit` come in O2C) di `fase-0.md`.

## Obiettivo

Script SQL in `db/`, eseguibili sia con `sqlcmd` sia dal tool `tools/Dusiburg.AI.Sinistri.DbInit`, che ricrea il database da zero (come in O2C). La dimensione di `VECTOR(n)` viene dalla configurazione, mai scritta a mano.

---

## 1. Enum di dominio (in `Core/Dominio`)

Ogni membro ha un **valore esplicito**, che diventa la PK della tabella di lookup. L'attributo `[Description]` fornisce l'etichetta italiana leggibile, usata nella UI e nel testo da vettorizzare.

```csharp
public enum Prodotto : byte
{
    [Description("Casa e fabbricati")] CasaFabbricati = 1,
    [Description("RC professionale tecnici")] RcProfTecnici = 2,
}

public enum TipoClausola : byte
{
    Definizione = 1,
    Garanzia = 2,
    Esclusione = 3,
    [Description("Franchigia / scoperto / limite")] Franchigia = 4,
}

public enum CausaSinistro : byte
{
    // CasaFabbricati
    [Description("Acqua condotta")] AcquaCondotta = 1,
    [Description("Evento atmosferico")] EventoAtmosferico = 2,
    [Description("Fenomeno elettrico")] FenomenoElettrico = 3,
    Incendio = 4,
    Furto = 5,
    Cristalli = 6,
    [Description("RC della proprietà")] RcProprieta = 7,
    // RcProfTecnici
    [Description("Errore progettuale")] ErroreProgettuale = 20,
    [Description("Errore di direzione lavori")] ErroreDirezioneLavori = 21,
    [Description("Coordinamento sicurezza")] SicurezzaCantiere = 22,
    [Description("Perdita patrimoniale")] PerditaPatrimoniale = 23,
}

public enum StatoSinistro : byte
{
    Aperto = 1,
    Chiuso = 2,
    Respinto = 3,
}

public enum EmbeddingProvider : byte   // infrastruttura (Fase 1b): con quale runtime sono stati calcolati i vettori
{
    Ollama = 1,
    OpenAiCompatible = 2,   // FastFlowLM, Lemonade
    Onnx = 3,               // Windows ML / ONNX Runtime nel processo
}
```

Il legame causa → prodotto (quali cause valgono per quale prodotto) è un attributo in codice (`[ProdottoCausa(Prodotto.CasaFabbricati)]`), usato dal generatore e dalla UI per filtrare le cause; non serve in tabella.

---

## 2. Schema (tabelle al singolare)

### `db/001_create_database.sql`

```sql
-- Variabili sqlcmd: $(DatabaseName)
IF DB_ID(N'$(DatabaseName)') IS NULL
    CREATE DATABASE [$(DatabaseName)];
GO
```

### `db/002_schema.sql`

Tutte le istruzioni sono protette (`IF OBJECT_ID(...) IS NULL`, `IF NOT EXISTS (SELECT 1 FROM sys.indexes ...)`), così lo script si può rieseguire.

```sql
-- Variabili sqlcmd: $(EmbeddingDimensions)

-- Lookup (righe inserite dalla CLI a partire dagli enum, vedi §4)
CREATE TABLE dbo.Prodotto (
  Id TINYINT NOT NULL CONSTRAINT PK_Prodotto PRIMARY KEY,
  Name VARCHAR(50) NOT NULL CONSTRAINT UQ_Prodotto_Name UNIQUE,
  Descrizione VARCHAR(100) NOT NULL
);
CREATE TABLE dbo.TipoClausola (   /* stessa forma */ );
CREATE TABLE dbo.CausaSinistro (  /* stessa forma */ );
CREATE TABLE dbo.StatoSinistro (  /* stessa forma */ );
CREATE TABLE dbo.EmbeddingProvider (  /* stessa forma: Ollama, OpenAiCompatible, Onnx (Fase 1b) */ );

CREATE TABLE dbo.Contraente (
  Id INT IDENTITY CONSTRAINT PK_Contraente PRIMARY KEY,
  Nominativo NVARCHAR(200) NOT NULL,
  Provincia CHAR(2) NOT NULL
);

CREATE TABLE dbo.Polizza (
  Id INT IDENTITY CONSTRAINT PK_Polizza PRIMARY KEY,
  Numero VARCHAR(30) NOT NULL CONSTRAINT UQ_Polizza_Numero UNIQUE,
  ProdottoId TINYINT NOT NULL CONSTRAINT FK_Polizza_Prodotto REFERENCES dbo.Prodotto(Id),
  ContraenteId INT NOT NULL CONSTRAINT FK_Polizza_Contraente REFERENCES dbo.Contraente(Id),
  Decorrenza DATE NOT NULL,
  Scadenza DATE NOT NULL,
  Massimale DECIMAL(12,2) NOT NULL,
  Franchigia DECIMAL(10,2) NOT NULL,
  CONSTRAINT CK_Polizza_Date CHECK (Scadenza > Decorrenza)
);

CREATE TABLE dbo.Clausola (
  Id INT IDENTITY CONSTRAINT PK_Clausola PRIMARY KEY,
  ProdottoId TINYINT NOT NULL CONSTRAINT FK_Clausola_Prodotto REFERENCES dbo.Prodotto(Id),
  Articolo NVARCHAR(20) NOT NULL,                 -- es. 'Art. 2.4'
  TipoClausolaId TINYINT NOT NULL CONSTRAINT FK_Clausola_TipoClausola REFERENCES dbo.TipoClausola(Id),
  Titolo NVARCHAR(200) NOT NULL,
  Testo NVARCHAR(MAX) NOT NULL,
  Embedding VECTOR($(EmbeddingDimensions)) NULL,
  CONSTRAINT UQ_Clausola_Prodotto_Articolo UNIQUE (ProdottoId, Articolo)
);

CREATE TABLE dbo.Riparatore (
  Id INT IDENTITY CONSTRAINT PK_Riparatore PRIMARY KEY,
  RagioneSociale NVARCHAR(200) NOT NULL
);

CREATE TABLE dbo.Sinistro (
  Id INT IDENTITY CONSTRAINT PK_Sinistro PRIMARY KEY,
  Numero VARCHAR(30) NOT NULL CONSTRAINT UQ_Sinistro_Numero UNIQUE,   -- 'SIN-2025-000123'
  PolizzaId INT NOT NULL CONSTRAINT FK_Sinistro_Polizza REFERENCES dbo.Polizza(Id),
  RiparatoreId INT NULL CONSTRAINT FK_Sinistro_Riparatore REFERENCES dbo.Riparatore(Id),
  DataEvento DATE NOT NULL,
  DataDenuncia DATE NOT NULL,
  Provincia CHAR(2) NOT NULL,
  CausaSinistroId TINYINT NOT NULL CONSTRAINT FK_Sinistro_CausaSinistro REFERENCES dbo.CausaSinistro(Id),
  Descrizione NVARCHAR(MAX) NOT NULL,
  EsitoPerizia NVARCHAR(MAX) NULL,
  StatoSinistroId TINYINT NOT NULL CONSTRAINT FK_Sinistro_StatoSinistro REFERENCES dbo.StatoSinistro(Id),
  ImportoRiservato DECIMAL(12,2) NULL,
  ImportoLiquidato DECIMAL(12,2) NULL,
  Embedding VECTOR($(EmbeddingDimensions)) NULL,
  CONSTRAINT CK_Sinistro_Date CHECK (DataDenuncia >= DataEvento)
);

CREATE INDEX IX_Sinistro_Filtri ON dbo.Sinistro (StatoSinistroId, DataEvento)
  INCLUDE (PolizzaId, Provincia, CausaSinistroId, ImportoLiquidato);
CREATE INDEX IX_Sinistro_PolizzaId ON dbo.Sinistro (PolizzaId);   -- join verso Polizza e ricerca per contraente (Fase 7)

-- Metadati dell'ultimo calcolo degli embedding (scritta in Fase 4, letta da health)
CREATE TABLE dbo.EmbeddingInfo (
  Id TINYINT NOT NULL CONSTRAINT PK_EmbeddingInfo PRIMARY KEY CONSTRAINT CK_EmbeddingInfo_Singleton CHECK (Id = 1),
  Modello VARCHAR(100) NOT NULL,
  EmbeddingProviderId TINYINT NOT NULL CONSTRAINT FK_EmbeddingInfo_EmbeddingProvider REFERENCES dbo.EmbeddingProvider(Id),
  Dimensioni INT NOT NULL,
  AggiornatoIl DATETIME2(0) NOT NULL
);
```

Note di progetto:
- **Indici:** con ~400 sinistri e ~60 clausole la ricerca vettoriale esatta è una scansione completa ed è istantanea. `IX_Sinistro_Filtri` serve per i filtri SQL della ricerca ibrida (stato + data); `IX_Sinistro_PolizzaId` per la join. Non si aggiunge un indice vettoriale (DiskANN è la Fase 10).
- **`EmbeddingInfo`** (aggiunta rispetto al piano): registra con quale modello e quale runtime sono stati calcolati i vettori. Senza questa informazione, cambiando modello o runtime (es. lo stesso EmbeddingGemma su CPU con Ollama e su NPU con FastFlowLM) a parità di dimensione, si confronterebbero vettori di modelli diversi senza accorgersene. `health` la confronta con la configurazione.
- `Sinistro.Provincia` resta `CHAR(2)` (sigla libera, non un enum di codice).
- **`VARCHAR` vs `NVARCHAR`:** codici e nomi tecnici (`Numero` di polizza e sinistro, `Name`/`Descrizione` delle lookup, `EmbeddingInfo.Modello`) sono `VARCHAR`; i testi liberi (nominativi, titoli, descrizioni, perizie) restano `NVARCHAR`. Le query Dapper che filtrano su una colonna `VARCHAR` passano il parametro come `DbString { IsAnsi = true, Length = … }`: con la collation `SQL_Latin1_General_CP1_CI_AS` un parametro `NVARCHAR` forza la conversione della colonna e trasforma la seek sull'indice univoco (es. `UQ_Polizza_Numero`, `UQ_Sinistro_Numero`) in una scansione.

---

## 3. Template SQL e dimensione del vettore

Gli script usano le **variabili sqlcmd** `$(DatabaseName)` e `$(EmbeddingDimensions)`. Così:
- da riga di comando si eseguono con `sqlcmd -S "(localdb)\localdev" -E -i db\002_schema.sql -v EmbeddingDimensions=768 -d Sinistri`;
- dal codice `SqlScriptRunner` (Data) legge il file, sostituisce `$(Nome)` con i valori di configurazione (solo interi validati e nomi di DB che rispettano la regex `^[A-Za-z0-9_]+$`, per evitare SQL injection), divide sui separatori `GO` e manda i batch in sequenza.

Gli script sono file incorporati nell'assembly `Data` (`EmbeddedResource`), così `DbInit` e i test li trovano senza dipendere dalla cartella di lavoro.

---

## 4. Popolamento delle lookup

Dopo lo schema, `DatabaseInitializer.SyncLookupsAsync()` allinea ogni tabella di lookup al rispettivo enum con un `MERGE` generico:

```csharp
Task SyncLookupAsync<TEnum>(SqlConnection connection, string tableName) where TEnum : struct, Enum
// per ogni valore: Id = (byte)valore, Name = nome del membro, Descrizione = [Description] o nome
```

Poiché il DB si ricrea sempre da zero (§5), il `MERGE` in pratica fa solo `INSERT`. Resta un `MERGE` perché la stessa routine serve anche ai test.

---

## 5. Tool `tools/Dusiburg.AI.Sinistri.DbInit` (D17)

Stesso schema del `DbInit` di O2C: **ricrea sempre il database da zero**, senza migration e senza modalità "incrementale".

```
dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit [-- "<connection string>"] [--no-seed] [--allow-non-local] [--parafrasa-con-llm]
```

| Passo | Dettaglio |
|---|---|
| 1. Connection string | da argomento, altrimenti `ConnectionStrings__sql` dall'ambiente, altrimenti il default `Server=(localdb)\localdev;Database=Sinistri;Integrated Security=True;TrustServerCertificate=True` |
| 2. Protezione | se il server non inizia con `(localdb)` e manca `--allow-non-local`: messaggio *"Il server 'X' non è LocalDB: il database verrebbe cancellato. Aggiungi --allow-non-local per procedere."*, codice di uscita 1 |
| 3. Drop | su `master`: `IF DB_ID(...) IS NOT NULL` → `ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ...` |
| 4. Create + schema | `001_create_database.sql`, poi `002_schema.sql` con `EmbeddingDimensions` = `EMBEDDING_DIMENSIONS` (ambiente, altrimenti default delle opzioni) |
| 5. Lookup | `SyncLookupsAsync` dai 5 enum |
| 6. Seed (se non c'è `--no-seed`) | clausole (`003_seed_clausole.sql`) e dati sintetici (`fase-3.md`); `--parafrasa-con-llm` attiva la parafrasi via Ollama |
| 7. Riepilogo | *"Fatto: database Sinistri ricreato con VECTOR(768), lookup popolate dagli enum, 60 clausole, 410 sinistri. Embedding da calcolare: dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- embed"* |

Il seed **non** calcola gli embedding: servono Ollama e qualche minuto, quindi restano un passo separato della CLI (`fase-4.md`). Così `DbInit` è veloce, deterministico e usabile anche dai test senza Ollama.

Rispetto a `PLAN.md` spariscono i comandi CLI `db init` (idempotente) e `db reset`: l'unica operazione è "ricrea", come in O2C. Non c'è una richiesta di conferma interattiva: la protezione è il vincolo su LocalDB, come in O2C.

La classe che fa il lavoro, `DatabaseInitializer.RecreateAsync(connectionString, embeddingDimensions, seed)`, sta in `Data` ed è usata anche dai test (DB `Sinistri_Test`, `VECTOR(4)`), come `O2CDatabaseInitializer` nei test di O2C.

### Lettura della dimensione delle colonne

`DatabaseInitializer.ReadVectorDimensionsAsync()` legge la dimensione di `Clausola.Embedding` e `Sinistro.Embedding` dai metadati di sistema. In fase di sviluppo va verificata la colonna esatta di `sys.columns` che espone la dimensione (es. `vector_dimensions`); in alternativa `max_length`, meno l'header, diviso 4. Serve al controllo 9 di `health`: se la configurazione chiede 768 e il DB ha 1024 → *"Il database ha VECTOR(1024) ma EMBEDDING_DIMENSIONS è 768: rieseguire DbInit."*

Classi coinvolte: `DatabaseInitializer`, `SqlScriptRunner` (Data), `Program.cs` di `DbInit`.

---

## 6. Test introdotti in questa fase

| Test | Tipo | Cosa verifica |
|---|---|---|
| `SqlScriptRunnerTests.SplitBatches_*` | unit | split su `GO` (maiuscolo/minuscolo, con spazi, non dentro stringhe o commenti su riga) |
| `SqlScriptRunnerTests.ReplaceVariables_*` | unit | sostituzione di `$(EmbeddingDimensions)`; variabile mancante → eccezione; nome DB non valido → eccezione |
| `DatabaseInitializerTests.Recreate_CreaTabelleELookup` | integrazione (LocalDB, DB `Sinistri_Test`, `VECTOR(4)`, `seed: false`) | 11 tabelle, lookup allineate agli enum |
| `DatabaseInitializerTests.Recreate_DueVolte_RipartedaZero` | integrazione | una riga inserita a mano sparisce alla seconda esecuzione |
| `DatabaseInitializerTests.ReadVectorDimensions` | integrazione | restituisce 4 |

I test di integrazione hanno la categoria `[Category("Integration")]` e ricreano il DB di test in un `[SetUpFixture]` con `DatabaseInitializer.RecreateAsync`.

## 7. Criteri di completamento

- `dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit -- --no-seed` crea il DB `Sinistri`, le 11 tabelle, gli indici e popola le 5 lookup.
- Rieseguito, riparte da zero senza errori.
- Con una connection string non LocalDB e senza `--allow-non-local` rifiuta con codice 1.
- `health` mostra ora anche il controllo 4 (DB presente) e il 9 (dimensione colonne) **OK**.
- Build della solution e test verdi.

## 7 bis. Esito (2026-09-25)

Criteri di completamento verificati:
- `DbInit -- --no-seed` crea `Sinistri` con 11 tabelle, `IX_Sinistro_Filtri`, `IX_Sinistro_PolizzaId` e le 5 lookup (2 prodotti, 4 tipi di clausola, 11 cause, 3 stati, 3 provider). Rieseguito riparte da zero;
- con `Server=sqlprod01` e senza `--allow-non-local`: rifiuto con codice 1. Con `EMBEDDING_DIMENSIONS=abc`: messaggio di configurazione non valida, codice 1;
- `health`: controlli 1–9 OK, con esito globale "OK". Con `EMBEDDING_DIMENSIONS=1024` il controllo 9 fallisce: *"il database ha Clausola.Embedding VECTOR(768), Sinistro.Embedding VECTOR(768) ma EMBEDDING_DIMENSIONS=1024: rieseguire DbInit"*;
- build della solution senza warning; 57 test verdi, compresi quelli di integrazione su `Sinistri_Test`.

Scostamenti:

| Punto | Previsto | Fatto | Motivo |
|---|---|---|---|
| Firma dell'inizializzatore | `RecreateAsync(connectionString, embeddingDimensions, seed)` | `RecreateAsync(connectionString, embeddingDimensions, cancellationToken)`: schema e lookup | il seed arriva con la Fase 3 e sarà un passo separato di `DbInit`, come in O2C; `--no-seed` è già accettato |
| Dimensione delle colonne | colonna di `sys.columns` da verificare | `sys.columns.vector_dimensions` (SQL Server 2025, verificato su LocalDB) | — |
| Controllo 9 di `health` | dimensione delle colonne | dimensione **e** `EmbeddingInfo` (modello, provider, dimensione) contro la configurazione; senza embedding calcolati è OK con la nota "comando embed, Fase 4" | un modello diverso a parità di dimensione darebbe vettori incompatibili senza errori |
| Metadati degli enum | — | `EnumMetadata` in Core: `Descrizione()`, `ProdottoDellaCausa()`, `Cause()`, mappa da `EMBEDDING_PROVIDER` all'enum `EmbeddingProvider` | usati da lookup, health e dalle fasi 3 e 8 |
| `--parafrasa-con-llm` | opzione di `DbInit` | non ancora | riguarda il seed (Fase 3) |

## 8. Commit proposto (non eseguito)

```
fase 2: schema database con tabelle di lookup e tool DbInit

Script db/001_create_database.sql e db/002_schema.sql con variabili sqlcmd,
incorporati nell'assembly Data ed eseguiti da SqlScriptRunner (split su GO,
variabili validate). DatabaseInitializer ricrea il database da zero come in O2C
e popola le lookup dagli enum di dominio. Tool DbInit con protezione LocalDB.
health: controllo 9 su dimensione delle colonne VECTOR ed EmbeddingInfo.

Verifica: build della solution, 57 test verdi (integrazione su Sinistri_Test
con VECTOR(4)), DbInit rieseguibile, health OK con VECTOR(768).
```
