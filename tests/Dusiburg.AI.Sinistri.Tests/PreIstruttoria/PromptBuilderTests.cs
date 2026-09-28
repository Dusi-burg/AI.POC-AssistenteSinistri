using Dusiburg.AI.Sinistri.Ai.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

public class PromptBuilderTests
{
    private static readonly RichiestaPreIstruttoria Richiesta =
        new("Rottura di un tubo nel bagno.", "CF-DEMO-000001", new DateOnly(2026, 9, 20), CausaSinistro.AcquaCondotta);

    [Test]
    public void Build_ContieneClausoleNumerateConArticolo()
    {
        //SETUP
        var contesto = new ContestoPrompt(Richiesta, DatiScheda.Polizza, DatiScheda.Clausole, DatiScheda.Simili(3), DatiScheda.Statistiche, 5);

        //SUT
        List<ChatMessage> messaggi = PromptBuilder.Build(contesto);

        Assert.That(messaggi.Select(m => m.Role), Is.EqualTo(new[] { ChatRole.System, ChatRole.User }));
        Assert.That(messaggi[0].Text, Does.Contain("Usa SOLO le clausole fornite").And.Contain("9. Rispondi solo con un oggetto JSON")
            .And.Contain("mai tra le garanzie").And.Contain("Non fare calcoli con gli importi").And.Contain("Indica solo le esclusioni che i fatti"));
        Assert.That(messaggi[1].Text, Does.Contain("[C1] Art. 2.4 (Garanzia) — Acqua condotta\nLa Società indennizza")
            .And.Contain("[C2] Art. 3.4 (Esclusione) — Usura")
            .And.Contain("[C4] Art. 4.3 (Franchigia / scoperto / limite) — Franchigia acqua condotta")
            .And.Contain("Data evento: 2026-09-20")
            .And.Contain("Causa indicata: Acqua condotta")
            .And.EndWith("Compila la scheda di pre-istruttoria."));
    }

    [Test]
    public void Build_LimitaSinistriNelPrompt()
    {
        //SETUP
        var contesto = new ContestoPrompt(Richiesta, DatiScheda.Polizza, DatiScheda.Clausole, DatiScheda.Simili(10), DatiScheda.Statistiche, 5);

        //SUT
        string testo = PromptBuilder.MessaggioUtente(contesto);

        Assert.That(testo, Does.Contain("SINISTRI SIMILI (storico, i 5 più vicini)").And.Contain("[S5] SIN-2025-000005").And.Not.Contain("[S6]"));
        Assert.That(testo, Does.Contain($"Descrizione: {new string('d', PromptBuilder.MaxDescrizione - 1)}… |"));
        Assert.That(testo, Does.Contain($"Esito perizia: {new string('e', PromptBuilder.MaxEsito - 1)}…\n"));
    }

    [Test]
    public void Build_StatisticheFormattateInItaliano()
    {
        //SETUP
        var contesto = new ContestoPrompt(Richiesta, DatiScheda.Polizza, DatiScheda.Clausole, DatiScheda.Simili(1), DatiScheda.Statistiche, 5);

        //SUT
        string testo = PromptBuilder.MessaggioUtente(contesto);

        Assert.That(testo, Does.Contain("Casi simili: 10 | Respinti: 2 (20,0%) | Liquidato min/mediana/max: 850 € / 3.100 € / 9.800 €"));
        Assert.That(testo, Does.Contain("Massimale: 300.000,00 € | Franchigia: 250,00 €"));
        Assert.That(testo, Does.Contain("[S1] SIN-2025-000001 | 2025-03 | Acqua condotta | Chiuso | liquidato 3.200,00 €"));
    }

    [Test]
    public void Build_TestoLibero_ChiedeSezioniInveceDelJson()
    {
        //SETUP
        var contesto = new ContestoPrompt(Richiesta, DatiScheda.Polizza, DatiScheda.Clausole, [], DatiScheda.Statistiche, 5);

        //SUT
        List<ChatMessage> messaggi = PromptBuilder.Build(contesto, testoLibero: true);

        Assert.That(messaggi[0].Text, Does.Contain("Rispondi in testo semplice").And.Not.Contain("oggetto JSON"));
        Assert.That(messaggi[1].Text, Does.Contain("Nessun sinistro simile nello storico."));
    }
}
