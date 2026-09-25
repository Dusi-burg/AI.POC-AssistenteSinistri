namespace Dusiburg.AI.Sinistri.Core.Telemetry;

/// <summary>Nomi delle sorgenti di telemetria: ogni passo della pre-istruttoria è uno span visibile nel dashboard (D15).</summary>
public static class SinistriTelemetry
{
    /// <summary>Filtro usato da ServiceDefaults per raccogliere tutte le sorgenti del POC.</summary>
    public const string SourceWildcard = "Dusiburg.AI.Sinistri.*";

    public static class Sources
    {
        public const string Core = "Dusiburg.AI.Sinistri.Core";
        public const string Data = "Dusiburg.AI.Sinistri.Data";
        public const string Chat = "Dusiburg.AI.Sinistri.Ai.Chat";
        public const string Embedding = "Dusiburg.AI.Sinistri.Ai.Embedding";
    }
}
