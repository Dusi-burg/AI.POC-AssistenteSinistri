using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Onnx;

/// <summary>
/// <c>amd/bge-m3-onnx</c> con ONNX Runtime di Windows ML (P3a), su NPU (VitisAI, BF16 compilato al primo avvio) o su CPU.
/// Embedding denso di <c>bge-m3</c>: stato nascosto del token CLS, normalizzato. Un testo per volta: l'input ha forma fissa.
/// </summary>
internal sealed class BgeM3OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int SequenceLength = 512;
    public const int Dimensions = 1024;

    private readonly InferenceSession _session;
    private readonly XlmRobertaTokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;

    public BgeM3OnnxEmbeddingGenerator(string modelDirectory, OnnxDevice device)
    {
        using SessionOptions options = WindowsMlRuntime.CreateSessionOptions(device);

        _session = new InferenceSession(Path.Combine(modelDirectory, "bge-m3.onnx"), options);
        _tokenizer = XlmRobertaTokenizer.Load(Path.Combine(modelDirectory, "sentencepiece.bpe.model"));
        _metadata = new EmbeddingGeneratorMetadata($"windows-ml-{device.ToString().ToLowerInvariant()}", null, "amd/bge-m3-onnx", Dimensions);
    }

    /// <summary>Testi più lunghi di 510 token incontrati (troncati), per il report.</summary>
    public int TruncatedTexts { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        GeneratedEmbeddings<Embedding<float>> result = [];

        foreach (string text in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new Embedding<float>(Embed(text)) { ModelId = _metadata.DefaultModelId, CreatedAt = DateTimeOffset.UtcNow });
        }

        return Task.FromResult(result);
    }

    private float[] Embed(string text)
    {
        (long[] inputIds, long[] attentionMask, _, bool truncated) = _tokenizer.Encode(text, SequenceLength);

        if (truncated)
        {
            TruncatedTexts++;
        }

        NamedOnnxValue[] inputs =
        [
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, [1, SequenceLength])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, [1, SequenceLength]))
        ];

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _session.Run(inputs, ["last_hidden_state"]);
        Tensor<float> hidden = outputs[0].AsTensor<float>();

        float[] cls = new float[Dimensions];

        for (int i = 0; i < Dimensions; i++)
        {
            cls[i] = hidden[0, 0, i];
        }

        return VectorMath.Normalize(cls);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is not null
        ? null
        : serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose() => _session.Dispose();
}
