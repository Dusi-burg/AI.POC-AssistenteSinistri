using System.Diagnostics;

namespace Dusiburg.AI.Sinistri.Core.Health;

/// <summary>
/// Esito di un controllo. Nomi diversi da <c>HealthStatus</c>/<c>HealthCheckResult</c> di ASP.NET, con cui convivono nell'API.
/// </summary>
public enum ProbeStatus
{
    Ok,

    /// <summary>Solo informativo, non incide sull'esito (es. posizionamento dei modelli).</summary>
    Info,

    /// <summary>Da sistemare ma non bloccante (es. database non ancora creato).</summary>
    Warning,

    Error
}

/// <param name="Numero">Numero del controllo nella tabella di <c>fase-1.md</c> §6: ordina l'output.</param>
public sealed record ProbeResult(int Numero, string Nome, ProbeStatus Stato, string Dettaglio, TimeSpan Durata)
{
    /// <summary>Controllo non eseguito perché ne è fallito uno da cui dipende.</summary>
    public static ProbeResult Skipped(int numero, string nome, string motivo) =>
        new(numero, nome, ProbeStatus.Error, $"non eseguito: {motivo}", TimeSpan.Zero);

    /// <summary>Esegue un passo misurandone la durata; un'eccezione diventa un esito <see cref="ProbeStatus.Error"/> con il messaggio.</summary>
    public static async Task<ProbeResult> MeasureAsync(int numero, string nome, Func<Task<(ProbeStatus Stato, string Dettaglio)>> step)
    {
        long start = Stopwatch.GetTimestamp();

        try
        {
            (ProbeStatus stato, string dettaglio) = await step();

            return new ProbeResult(numero, nome, stato, dettaglio, Stopwatch.GetElapsedTime(start));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProbeResult(numero, nome, ProbeStatus.Error, exception.Message, Stopwatch.GetElapsedTime(start));
        }
    }
}

/// <summary>Gruppo di controlli su una dipendenza esterna (SQL, runtime dei modelli).</summary>
public interface IHealthProbe
{
    Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken);
}

public sealed record ProbeReport(IReadOnlyList<ProbeResult> Controlli)
{
    public const int ExitOk = 0;

    public const int ExitError = 1;

    public bool HasErrors => Controlli.Any(c => c.Stato == ProbeStatus.Error);

    public bool HasWarnings => Controlli.Any(c => c.Stato == ProbeStatus.Warning);

    /// <summary>Codice di uscita della CLI: 0 se tutti OK o Warning, 1 se almeno un errore.</summary>
    public int ExitCode => HasErrors ? ExitError : ExitOk;
}
