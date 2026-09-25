using Dusiburg.AI.Sinistri.Data.Schema;

namespace Dusiburg.AI.Sinistri.Tests.Data;

/// <summary>Esecuzione degli script come sqlcmd: separatori GO e variabili $(Nome).</summary>
public class SqlScriptRunnerTests
{
    [Test]
    public void SplitBatches_GoSuRigaPropria_MaiuscoloMinuscoloEConSpazi()
    {
        //SETUP
        const string script = "CREATE TABLE a (x int);\nGO\nCREATE TABLE b (x int);\r\n  go  \nSELECT 1;\nGo";

        //SUT
        IReadOnlyList<string> batches = SqlScriptRunner.SplitBatches(script);

        Assert.That(batches, Is.EqualTo(new[] { "CREATE TABLE a (x int);", "CREATE TABLE b (x int);", "SELECT 1;" }));
    }

    [Test]
    public void SplitBatches_GoDentroStringaOCommento_NonSepara()
    {
        //SETUP
        const string script = "SELECT 'riga uno\nGO\nriga ''tre''';\n/* commento\nGO\n*/\n-- GO\nSELECT 2;\nGO\nSELECT 3;";

        //SUT
        IReadOnlyList<string> batches = SqlScriptRunner.SplitBatches(script);

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[0], Does.StartWith("SELECT 'riga uno").And.EndWith("SELECT 2;"));
        Assert.That(batches[1], Is.EqualTo("SELECT 3;"));
    }

    [Test]
    public void ReplaceVariables_SostituisceTutteLeOccorrenze()
    {
        //SETUP
        var variables = new Dictionary<string, string> { [SqlScriptVariables.EmbeddingDimensions] = "768" };

        //SUT
        string script = SqlScriptRunner.ReplaceVariables("a VECTOR($(EmbeddingDimensions)), b VECTOR($(EmbeddingDimensions))", variables);

        Assert.That(script, Is.EqualTo("a VECTOR(768), b VECTOR(768)"));
    }

    [Test]
    public void ReplaceVariables_VariabileMancante_Errore()
    {
        //SUT
        Assert.That(() => SqlScriptRunner.ReplaceVariables("CREATE DATABASE [$(DatabaseName)]", new Dictionary<string, string>()),
            Throws.InvalidOperationException.With.Message.Contains("$(DatabaseName)"));
    }

    [TestCase("Sinistri;DROP DATABASE master")]
    [TestCase("Sinistri]")]
    [TestCase("")]
    public void ValidDatabaseName_NomeNonValido_Errore(string name)
    {
        //SUT
        Assert.That(() => SqlScriptVariables.ValidDatabaseName(name), Throws.ArgumentException);
    }

    [TestCase(0)]
    [TestCase(1999)]
    public void ValidDimensions_FuoriIntervallo_Errore(int dimensions)
    {
        //SUT
        Assert.That(() => SqlScriptVariables.ValidDimensions(dimensions), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void LoadScript_ScriptIncorporatiNellAssembly()
    {
        //SUT
        string schema = SqlScriptRunner.LoadScript(DatabaseInitializer.SchemaScript);

        Assert.That(schema, Does.Contain("VECTOR($(EmbeddingDimensions))"));
        Assert.That(SqlScriptRunner.LoadScript(DatabaseInitializer.CreateDatabaseScript), Does.Contain("$(DatabaseName)"));
    }
}
