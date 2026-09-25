-- Crea il database del POC (fase-2.md §2). Variabili sqlcmd: $(DatabaseName)
-- Esecuzione manuale: sqlcmd -S "(localdb)\localdev" -E -f 65001 -i db\001_create_database.sql -v DatabaseName=Sinistri
IF DB_ID(N'$(DatabaseName)') IS NULL
    CREATE DATABASE [$(DatabaseName)];
GO
