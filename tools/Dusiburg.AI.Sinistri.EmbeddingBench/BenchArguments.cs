using System.Globalization;

namespace Dusiburg.AI.Sinistri.EmbeddingBench;

internal sealed record BenchArguments(IReadOnlySet<string>? Candidates, int Repeat, bool SkipCoexistence, string? ReportPath)
{
    public static BenchArguments Parse(string[] args)
    {
        IReadOnlySet<string>? candidates = null;
        int repeat = 50;
        bool skipCoexistence = false;
        string? reportPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--candidates":
                    candidates = new HashSet<string>(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--repeat":
                    repeat = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--report":
                    reportPath = Path.GetFullPath(args[++i]);
                    break;
                case "--skip-coexistence":
                    skipCoexistence = true;
                    break;
                default:
                    throw new ArgumentException($"Argomento sconosciuto: {args[i]}");
            }
        }

        return new BenchArguments(candidates, repeat, skipCoexistence, reportPath);
    }

    /// <summary>Cartella con <c>Dusiburg.AI.Sinistri.slnx</c>, risalendo dalla cartella corrente.</summary>
    public static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dusiburg.AI.Sinistri.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Lanciare il banco dalla cartella del repository (non trovo Dusiburg.AI.Sinistri.slnx).");
    }
}
