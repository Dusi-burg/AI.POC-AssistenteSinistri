using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Schema;

/// <summary>
/// Esegue gli script di <c>db/</c> (risorse incorporate nell'assembly) come farebbe <c>sqlcmd</c>: variabili <c>$(Nome)</c>
/// sostituite e batch separati dalle righe <c>GO</c>. I valori ammessi sono validati da <see cref="SqlScriptVariables"/>.
/// </summary>
public static partial class SqlScriptRunner
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    public static async Task RunAsync(
        SqlConnection connection, string scriptName, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        string script = ReplaceVariables(LoadScript(scriptName), variables);

        foreach (string batch in SplitBatches(script))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = (int)CommandTimeout.TotalSeconds };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static string LoadScript(string scriptName)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(scriptName)
            ?? throw new InvalidOperationException($"Script {scriptName} non incorporato nell'assembly Data.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    /// <summary>Sostituisce <c>$(Nome)</c>; una variabile usata nello script e non fornita è un errore, come in sqlcmd.</summary>
    public static string ReplaceVariables(string script, IReadOnlyDictionary<string, string> variables) =>
        VariablePattern().Replace(script, match => variables.TryGetValue(match.Groups[1].Value, out string? value)
            ? value
            : throw new InvalidOperationException($"Variabile sqlcmd $({match.Groups[1].Value}) non valorizzata."));

    /// <summary>
    /// Divide lo script sulle righe che contengono solo <c>GO</c> (maiuscolo o minuscolo, con spazi), ignorando quelle dentro
    /// una stringa o un commento a blocco su più righe.
    /// </summary>
    public static IReadOnlyList<string> SplitBatches(string script)
    {
        List<string> batches = [];
        var current = new StringBuilder();
        var state = new LexerState();

        foreach (string line in script.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!state.InString && !state.InBlockComment && line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                AddBatch(batches, current);
                continue;
            }

            state.Scan(line);
            current.AppendLine(line);
        }

        AddBatch(batches, current);

        return batches;
    }

    private static void AddBatch(List<string> batches, StringBuilder current)
    {
        string batch = current.ToString().Trim();

        if (batch.Length > 0)
        {
            batches.Add(batch);
        }

        current.Clear();
    }

    [GeneratedRegex(@"\$\((\w+)\)")]
    private static partial Regex VariablePattern();

    /// <summary>Stato tra una riga e l'altra: dentro una stringa <c>'…'</c> o un commento <c>/* … */</c>.</summary>
    private sealed class LexerState
    {
        public bool InString { get; private set; }

        public bool InBlockComment { get; private set; }

        public void Scan(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (InBlockComment)
                {
                    if (current == '*' && next == '/') { InBlockComment = false; i++; }
                }
                else if (InString)
                {
                    // '' dentro una stringa è un apice letterale: il toggle doppio lascia lo stato invariato.
                    if (current == '\'') { InString = false; }
                }
                else if (current == '-' && next == '-')
                {
                    return;
                }
                else if (current == '/' && next == '*')
                {
                    InBlockComment = true;
                    i++;
                }
                else if (current == '\'')
                {
                    InString = true;
                }
            }
        }
    }
}

/// <summary>Valori ammessi nelle variabili degli script: solo interi validati e nomi di database semplici (niente SQL injection).</summary>
public static partial class SqlScriptVariables
{
    public const string DatabaseName = "DatabaseName";
    public const string EmbeddingDimensions = "EmbeddingDimensions";

    public static string ValidDatabaseName(string name) => DatabaseNamePattern().IsMatch(name)
        ? name
        : throw new ArgumentException($"Nome di database '{name}' non valido: ammessi solo lettere, cifre e underscore.", nameof(name));

    public static string ValidDimensions(int dimensions) => dimensions is > 0 and <= Core.Options.SinistriOptions.MaxEmbeddingDimensions
        ? dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, $"Dimensione del vettore fuori dall'intervallo 1–{Core.Options.SinistriOptions.MaxEmbeddingDimensions}.");

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex DatabaseNamePattern();
}
