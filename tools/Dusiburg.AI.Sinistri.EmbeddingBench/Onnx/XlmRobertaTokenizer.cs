using Microsoft.ML.Tokenizers;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Onnx;

/// <summary>
/// Tokenizer di <c>bge-m3</c> (XLM-RoBERTa) dal <c>sentencepiece.bpe.model</c> di BAAI, con la numerazione "fairseq" del modello:
/// <c>&lt;s&gt;</c>=0, <c>&lt;pad&gt;</c>=1, <c>&lt;/s&gt;</c>=2, <c>&lt;unk&gt;</c>=3 e gli id di SentencePiece spostati di 1.
/// Verificato contro Ollama: coseno 0,99999 tra i due vettori dello stesso testo.
/// </summary>
internal sealed class XlmRobertaTokenizer(SentencePieceTokenizer sentencePiece)
{
    private const long BeginId = 0;
    private const long PadId = 1;
    private const long EndId = 2;
    private const int UnknownId = 3;
    private const int FairseqOffset = 1;

    public static XlmRobertaTokenizer Load(string sentencePieceModelPath)
    {
        using FileStream stream = File.OpenRead(sentencePieceModelPath);

        return new XlmRobertaTokenizer(SentencePieceTokenizer.Create(stream, false, false));
    }

    /// <summary>Input a lunghezza fissa (il modello AMD ha forma <c>(1, 512)</c>): testo troncato, poi riempito con <c>&lt;pad&gt;</c>.</summary>
    public (long[] InputIds, long[] AttentionMask, int Tokens, bool Truncated) Encode(string text, int sequenceLength)
    {
        IReadOnlyList<int> pieces = sentencePiece.EncodeToIds(text, false, false);
        int kept = Math.Min(pieces.Count, sequenceLength - 2);

        long[] inputIds = new long[sequenceLength];
        long[] attentionMask = new long[sequenceLength];
        Array.Fill(inputIds, PadId);

        inputIds[0] = BeginId;

        for (int i = 0; i < kept; i++)
        {
            inputIds[i + 1] = pieces[i] == 0 ? UnknownId : pieces[i] + FairseqOffset;
        }

        inputIds[kept + 1] = EndId;
        Array.Fill(attentionMask, 1L, 0, kept + 2);

        return (inputIds, attentionMask, kept + 2, kept < pieces.Count);
    }
}
