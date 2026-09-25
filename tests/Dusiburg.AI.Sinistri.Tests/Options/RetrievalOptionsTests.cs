using Dusiburg.AI.Sinistri.Core.Options;

namespace Dusiburg.AI.Sinistri.Tests.Options;

public class RetrievalOptionsTests
{
    [Test]
    public void Validate_Default_NessunErrore()
    {
        //SUT
        IReadOnlyList<string> errors = new RetrievalOptions().Validate();

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_ValoriIncoerenti_ElencaTuttiGliErrori()
    {
        //SETUP
        var options = new RetrievalOptions { TopClausole = 0, TopSinistri = 3, SinistriNelPrompt = 5, DistanzaMaxClausolaIntegrativa = 0 };

        //SUT
        IReadOnlyList<string> errors = options.Validate();

        Assert.That(errors, Has.Count.EqualTo(3));
        Assert.That(errors, Has.Some.Contains("TopClausole"));
        Assert.That(errors, Has.Some.Contains("SinistriNelPrompt"));
        Assert.That(errors, Has.Some.Contains("DistanzaMaxClausolaIntegrativa"));
    }
}
