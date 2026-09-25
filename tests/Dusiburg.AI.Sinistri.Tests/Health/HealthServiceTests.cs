using Dusiburg.AI.Sinistri.Core.Health;

namespace Dusiburg.AI.Sinistri.Tests.Health;

/// <summary>Aggregazione dei probe e codice di uscita del comando <c>health</c>, con probe finti.</summary>
public class HealthServiceTests
{
    [Test]
    public async Task RunAsync_TuttiOkOWarning_ExitCodeZeroEOrdinePerNumero()
    {
        //SETUP
        var service = new HealthService(
        [
            new FakeProbe(Result(5, ProbeStatus.Ok), Result(10, ProbeStatus.Info)),
            new FakeProbe(Result(1, ProbeStatus.Ok), Result(4, ProbeStatus.Warning))
        ]);

        //SUT
        ProbeReport report = await service.RunAsync(CancellationToken.None);

        Assert.That(report.Controlli.Select(c => c.Numero), Is.EqualTo(new[] { 1, 4, 5, 10 }));
        Assert.That(report.HasErrors, Is.False);
        Assert.That(report.HasWarnings, Is.True);
        Assert.That(report.ExitCode, Is.EqualTo(ProbeReport.ExitOk));
    }

    [Test]
    public async Task RunAsync_UnProbeInErrore_ExitCodeUno()
    {
        //SETUP
        var service = new HealthService(
        [
            new FakeProbe(Result(1, ProbeStatus.Ok)),
            new FakeProbe(Result(8, ProbeStatus.Error))
        ]);

        //SUT
        ProbeReport report = await service.RunAsync(CancellationToken.None);

        Assert.That(report.HasErrors, Is.True);
        Assert.That(report.ExitCode, Is.EqualTo(ProbeReport.ExitError));
    }

    [Test]
    public async Task RunAsync_ProbeCheLanciaEccezione_DiventaErroreSenzaNascondereGliAltri()
    {
        //SETUP
        var service = new HealthService(
        [
            new FakeProbe(Result(1, ProbeStatus.Ok)),
            new ThrowingProbe("Ollama esploso")
        ]);

        //SUT
        ProbeReport report = await service.RunAsync(CancellationToken.None);

        Assert.That(report.Controlli, Has.Count.EqualTo(2));
        Assert.That(report.Controlli, Has.Some.Matches<ProbeResult>(c => c.Stato == ProbeStatus.Error && c.Dettaglio == "Ollama esploso"));
        Assert.That(report.ExitCode, Is.EqualTo(ProbeReport.ExitError));
    }

    [Test]
    public async Task MeasureAsync_PassoCheLancia_ErroreConIlMessaggio()
    {
        //SUT
        ProbeResult ok = await ProbeResult.MeasureAsync(3, "passo", () => Task.FromResult((ProbeStatus.Ok, "bene")));
        ProbeResult failed = await ProbeResult.MeasureAsync(3, "passo", () => throw new InvalidOperationException("male"));

        Assert.That((ok.Stato, ok.Dettaglio), Is.EqualTo((ProbeStatus.Ok, "bene")));
        Assert.That((failed.Stato, failed.Dettaglio), Is.EqualTo((ProbeStatus.Error, "male")));
    }

    private static ProbeResult Result(int numero, ProbeStatus stato) => new(numero, $"controllo {numero}", stato, "dettaglio", TimeSpan.Zero);

    private sealed class FakeProbe(params ProbeResult[] results) : IHealthProbe
    {
        public Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbeResult>>(results);
    }

    private sealed class ThrowingProbe(string message) : IHealthProbe
    {
        public Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(message);
    }
}
