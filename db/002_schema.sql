-- Schema del POC (fase-2.md §2): tabelle al singolare (D2), enum come lookup (D3), chiave di business Sinistro.Numero (D4).
-- Variabili sqlcmd: $(EmbeddingDimensions) = EMBEDDING_DIMENSIONS, mai scritta a mano.
-- Esecuzione manuale: sqlcmd -S "(localdb)\localdev" -E -d Sinistri -f 65001 -i db\002_schema.sql -v EmbeddingDimensions=768
-- Istruzioni protette: lo script si può rieseguire. Le righe delle lookup le inserisce DbInit a partire dagli enum.

-- Lookup: Id = valore esplicito dell'enum, Name = nome del membro, Descrizione = etichetta italiana
IF OBJECT_ID(N'dbo.Prodotto', N'U') IS NULL
CREATE TABLE dbo.Prodotto (
    Id TINYINT NOT NULL CONSTRAINT PK_Prodotto PRIMARY KEY,
    Name VARCHAR(50) NOT NULL CONSTRAINT UQ_Prodotto_Name UNIQUE,
    Descrizione VARCHAR(100) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.TipoClausola', N'U') IS NULL
CREATE TABLE dbo.TipoClausola (
    Id TINYINT NOT NULL CONSTRAINT PK_TipoClausola PRIMARY KEY,
    Name VARCHAR(50) NOT NULL CONSTRAINT UQ_TipoClausola_Name UNIQUE,
    Descrizione VARCHAR(100) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.CausaSinistro', N'U') IS NULL
CREATE TABLE dbo.CausaSinistro (
    Id TINYINT NOT NULL CONSTRAINT PK_CausaSinistro PRIMARY KEY,
    Name VARCHAR(50) NOT NULL CONSTRAINT UQ_CausaSinistro_Name UNIQUE,
    Descrizione VARCHAR(100) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.StatoSinistro', N'U') IS NULL
CREATE TABLE dbo.StatoSinistro (
    Id TINYINT NOT NULL CONSTRAINT PK_StatoSinistro PRIMARY KEY,
    Name VARCHAR(50) NOT NULL CONSTRAINT UQ_StatoSinistro_Name UNIQUE,
    Descrizione VARCHAR(100) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.EmbeddingProvider', N'U') IS NULL
CREATE TABLE dbo.EmbeddingProvider (
    Id TINYINT NOT NULL CONSTRAINT PK_EmbeddingProvider PRIMARY KEY,
    Name VARCHAR(50) NOT NULL CONSTRAINT UQ_EmbeddingProvider_Name UNIQUE,
    Descrizione VARCHAR(100) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.Contraente', N'U') IS NULL
CREATE TABLE dbo.Contraente (
    Id INT IDENTITY CONSTRAINT PK_Contraente PRIMARY KEY,
    Nominativo NVARCHAR(200) NOT NULL,
    Provincia CHAR(2) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.Polizza', N'U') IS NULL
CREATE TABLE dbo.Polizza (
    Id INT IDENTITY CONSTRAINT PK_Polizza PRIMARY KEY,
    Numero VARCHAR(30) NOT NULL CONSTRAINT UQ_Polizza_Numero UNIQUE,
    ProdottoId TINYINT NOT NULL CONSTRAINT FK_Polizza_Prodotto REFERENCES dbo.Prodotto (Id),
    ContraenteId INT NOT NULL CONSTRAINT FK_Polizza_Contraente REFERENCES dbo.Contraente (Id),
    Decorrenza DATE NOT NULL,
    Scadenza DATE NOT NULL,
    Massimale DECIMAL(12, 2) NOT NULL,
    Franchigia DECIMAL(10, 2) NOT NULL,
    CONSTRAINT CK_Polizza_Date CHECK (Scadenza > Decorrenza)
);
GO

IF OBJECT_ID(N'dbo.Clausola', N'U') IS NULL
CREATE TABLE dbo.Clausola (
    Id INT IDENTITY CONSTRAINT PK_Clausola PRIMARY KEY,
    ProdottoId TINYINT NOT NULL CONSTRAINT FK_Clausola_Prodotto REFERENCES dbo.Prodotto (Id),
    Articolo NVARCHAR(20) NOT NULL,
    TipoClausolaId TINYINT NOT NULL CONSTRAINT FK_Clausola_TipoClausola REFERENCES dbo.TipoClausola (Id),
    Titolo NVARCHAR(200) NOT NULL,
    Testo NVARCHAR(MAX) NOT NULL,
    Embedding VECTOR($(EmbeddingDimensions)) NULL,
    CONSTRAINT UQ_Clausola_Prodotto_Articolo UNIQUE (ProdottoId, Articolo)
);
GO

IF OBJECT_ID(N'dbo.Riparatore', N'U') IS NULL
CREATE TABLE dbo.Riparatore (
    Id INT IDENTITY CONSTRAINT PK_Riparatore PRIMARY KEY,
    RagioneSociale NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.Sinistro', N'U') IS NULL
CREATE TABLE dbo.Sinistro (
    Id INT IDENTITY CONSTRAINT PK_Sinistro PRIMARY KEY,
    Numero VARCHAR(30) NOT NULL CONSTRAINT UQ_Sinistro_Numero UNIQUE,
    PolizzaId INT NOT NULL CONSTRAINT FK_Sinistro_Polizza REFERENCES dbo.Polizza (Id),
    RiparatoreId INT NULL CONSTRAINT FK_Sinistro_Riparatore REFERENCES dbo.Riparatore (Id),
    DataEvento DATE NOT NULL,
    DataDenuncia DATE NOT NULL,
    Provincia CHAR(2) NOT NULL,
    CausaSinistroId TINYINT NOT NULL CONSTRAINT FK_Sinistro_CausaSinistro REFERENCES dbo.CausaSinistro (Id),
    Descrizione NVARCHAR(MAX) NOT NULL,
    EsitoPerizia NVARCHAR(MAX) NULL,
    StatoSinistroId TINYINT NOT NULL CONSTRAINT FK_Sinistro_StatoSinistro REFERENCES dbo.StatoSinistro (Id),
    ImportoRiservato DECIMAL(12, 2) NULL,
    ImportoLiquidato DECIMAL(12, 2) NULL,
    Embedding VECTOR($(EmbeddingDimensions)) NULL,
    CONSTRAINT CK_Sinistro_Date CHECK (DataDenuncia >= DataEvento)
);
GO

-- Filtri SQL della ricerca ibrida (stato + data, Fase 5). La ricerca vettoriale è una scansione esatta: nessun indice DiskANN (Fase 10).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sinistro_Filtri' AND object_id = OBJECT_ID(N'dbo.Sinistro'))
CREATE INDEX IX_Sinistro_Filtri ON dbo.Sinistro (StatoSinistroId, DataEvento)
    INCLUDE (PolizzaId, Provincia, CausaSinistroId, ImportoLiquidato);
GO

-- Join verso Polizza e ricerca per contraente (Fase 7).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sinistro_PolizzaId' AND object_id = OBJECT_ID(N'dbo.Sinistro'))
CREATE INDEX IX_Sinistro_PolizzaId ON dbo.Sinistro (PolizzaId);
GO

-- Modello e runtime dell'ultimo calcolo degli embedding (scritta in Fase 4, letta da health):
-- vettori di modelli o runtime diversi non si mescolano, anche a parità di dimensione.
IF OBJECT_ID(N'dbo.EmbeddingInfo', N'U') IS NULL
CREATE TABLE dbo.EmbeddingInfo (
    Id TINYINT NOT NULL CONSTRAINT PK_EmbeddingInfo PRIMARY KEY CONSTRAINT CK_EmbeddingInfo_Singleton CHECK (Id = 1),
    Modello VARCHAR(100) NOT NULL,
    EmbeddingProviderId TINYINT NOT NULL CONSTRAINT FK_EmbeddingInfo_EmbeddingProvider REFERENCES dbo.EmbeddingProvider (Id),
    Dimensioni INT NOT NULL,
    AggiornatoIl DATETIME2(0) NOT NULL
);
GO
