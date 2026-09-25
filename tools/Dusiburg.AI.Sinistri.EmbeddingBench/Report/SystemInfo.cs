using System.Diagnostics;
using Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;
using Dusiburg.AI.Sinistri.EmbeddingBench.Onnx;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Report;

/// <summary>Versioni dei runtime e dell'hardware, per rendere il report confrontabile quando si ripete la prova.</summary>
internal sealed record SystemInfo(IReadOnlyList<(string Voce, string Valore)> Voci)
{
    public static async Task<SystemInfo> CollectAsync(OllamaAdmin ollama, CancellationToken cancellationToken)
    {
        List<(string, string)> voci =
        [
            ("Processore", await CimAsync("Win32_Processor", "Name", cancellationToken)),
            ("Driver NPU", await CimAsync("Win32_PnPSignedDriver -Filter \"DeviceName='NPU Compute Accelerator Device'\"", "DriverVersion", cancellationToken)),
            ("GPU", await RunAsync("nvidia-smi", ["--query-gpu=name,memory.total,driver_version", "--format=csv,noheader"], cancellationToken)),
            ("Ollama", await Safe(() => ollama.VersionAsync(cancellationToken))),
            ("FastFlowLM", await RunAsync("flm", ["version"], cancellationToken)),
            ("EP VitisAI (catalogo Windows ML)", await WindowsMlRuntime.CatalogEpVersionAsync() ?? "non installato"),
            (".NET", Environment.Version.ToString()),
            ("Windows", Environment.OSVersion.VersionString)
        ];

        return new SystemInfo(voci);
    }

    public static async Task<string> VramUsedAsync(CancellationToken cancellationToken) =>
        await RunAsync("nvidia-smi", ["--query-gpu=memory.used", "--format=csv,noheader"], cancellationToken);

    /// <summary>Proprietà WMI lette con PowerShell, per non aggiungere dipendenze al tool.</summary>
    private static Task<string> CimAsync(string classAndFilter, string property, CancellationToken cancellationToken) =>
        RunAsync("powershell", ["-NoProfile", "-Command", $"(Get-CimInstance {classAndFilter} | Select-Object -First 1).{property}"], cancellationToken);

    private static async Task<string> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, RedirectStandardError = true })!;
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return output.Trim();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return $"{fileName} non trovato";
        }
    }

    private static async Task<string> Safe(Func<Task<string>> read)
    {
        try
        {
            return await read();
        }
        catch (HttpRequestException exception)
        {
            return $"non raggiungibile ({exception.Message})";
        }
    }
}
