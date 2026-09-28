using Microsoft.ML.OnnxRuntime;
using Microsoft.Windows.AI.MachineLearning;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Onnx;

/// <summary>Chip su cui gira la sessione ONNX.</summary>
internal enum OnnxDevice
{
    Cpu,
    Npu
}

/// <summary>
/// ONNX Runtime di Windows ML con l'execution provider VitisAI per la NPU XDNA 2.
/// <para>
/// L'EP arriva dal catalogo di Windows ML (pacchetto in <c>C:\Program Files\WindowsApps</c>), ma da lì il compilatore AIE
/// fallisce: riceve il percorso delle sue risorse in forma breve 8.3 troncato a <c>C:\PROGRA~1\WindowsApps\</c>. Per il banco
/// si usa quindi una copia locale dello stesso EP (modalità "bring your own EP" di Windows ML), in <see cref="LocalEpDirectory"/>.
/// </para>
/// </summary>
internal static class WindowsMlRuntime
{
    public const string VitisAi = "VitisAIExecutionProvider";

    private static readonly Lock Gate = new();
    private static bool _registered;

    public static string AppDataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dusiburg.AI.Sinistri");

    public static string LocalEpDirectory => Path.Combine(AppDataRoot, "vitisai-ep");

    /// <summary>
    /// Il compilatore VitisAI scrive file di firma nella cartella corrente: si lavora in una cartella dedicata, fuori dal repository.
    /// </summary>
    public static string WorkDirectory => Path.Combine(AppDataRoot, "work");

    /// <summary>
    /// Pacchetto dell'EP VitisAI installato dal catalogo di Windows ML, dal nome della cartella
    /// (es. <c>Microsoft.WinML.AMD.NPU.EP.Framework.2_1.8.75.0_x64__…</c>); la copia locale usata dal banco deve coincidere.
    /// </summary>
    public static async Task<string?> CatalogEpVersionAsync()
    {
        ExecutionProvider? provider = ExecutionProviderCatalog.GetDefault().FindAllProviders().FirstOrDefault(p => p.Name == VitisAi);

        if (provider is null || provider.ReadyState == ExecutionProviderReadyState.NotPresent)
        {
            return null;
        }

        // Il percorso della libreria è valorizzato solo dopo EnsureReadyAsync (nessun download: il pacchetto è già presente).
        await provider.EnsureReadyAsync();

        return string.IsNullOrEmpty(provider.LibraryPath)
            ? null
            : Directory.GetParent(provider.LibraryPath)?.Parent?.Name.Split("_x64_")[0];
    }

    public static SessionOptions CreateSessionOptions(OnnxDevice device)
    {
        var options = new SessionOptions();

        if (device == OnnxDevice.Npu)
        {
            EnsureVitisAiRegistered();

            OrtEpDevice[] npu = [.. OrtEnv.Instance().GetEpDevices().Where(d => d.EpName == VitisAi)];

            if (npu.Length == 0)
            {
                throw new InvalidOperationException("Nessun dispositivo NPU esposto dall'EP VitisAI.");
            }

            options.AppendExecutionProvider(OrtEnv.Instance(), npu, new Dictionary<string, string>());
        }

        return options;
    }

    private static void EnsureVitisAiRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            string library = Path.Combine(LocalEpDirectory, "onnxruntime_vitisai_ep.dll");

            if (!File.Exists(library))
            {
                throw new InvalidOperationException(
                    $"EP VitisAI non trovato in {LocalEpDirectory}: copiare lì il contenuto della cartella ExecutionProvider del pacchetto " +
                    "Microsoft.WinML.AMD.NPU.EP.Framework (vedi docs/fase-1b.md).");
            }

            Directory.CreateDirectory(WorkDirectory);
            Environment.CurrentDirectory = WorkDirectory;

            OrtEnv.Instance().RegisterExecutionProviderLibrary(VitisAi, library);
            _registered = true;
        }
    }
}
