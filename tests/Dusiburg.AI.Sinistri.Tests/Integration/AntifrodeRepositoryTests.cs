using Dapper;
using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Antifrode;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// Query dell'antifrode su LocalDB con vettori a 4 dimensioni (D13), uguali in <c>Embedding</c> e <c>EmbeddingAntifrode</c>. Distanze:
/// A-1 [1,0,0,0] ↔ A-2 [1,0.1,0,0] 0,005 (stesso contraente); A-2 ↔ A-3 [1,0.3,0,0] 0,018 (nessun legame); A-1 ↔ A-3 0,042
/// (stesso riparatore); A-4 ortogonale a tutti; A-5 identico ad A-1 ma denunciato 30 mesi fa; A-6 senza vettore.
/// </summary>
[Category("Integration")]
[NonParallelizable]
public class AntifrodeRepositoryTests
{
    private const int RiparatoreR1 = 1;
    private const int RiparatoreR2 = 2;

    private readonly AntifrodeRepository _repository = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task PreparaAsync()
    {
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken.None);
        await using SqlConnection connection = await TestDatabase.OpenAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.Contraente (Nominativo, Provincia) VALUES (N'Contraente Uno', 'MI'), (N'Contraente Due', 'BO');
            INSERT INTO dbo.Riparatore (RagioneSociale) VALUES (N'Riparatore Uno'), (N'Riparatore Due');

            INSERT INTO dbo.Polizza (Numero, ProdottoId, ContraenteId, Decorrenza, Scadenza, Massimale, Franchigia) VALUES
                ('CF-TEST-000001', 1, 1, DATEADD(YEAR, -5, CAST(GETDATE() AS date)), DATEADD(YEAR, 1, CAST(GETDATE() AS date)), 300000, 250),
                ('RP-TEST-000002', 2, 2, DATEADD(YEAR, -5, CAST(GETDATE() AS date)), DATEADD(YEAR, 1, CAST(GETDATE() AS date)), 1000000, 2500);

            -- Qualsiasi prodotto e stato: A-3 è RC e respinto, A-2 aperto.
            INSERT INTO dbo.Sinistro (Numero, PolizzaId, RiparatoreId, DataEvento, DataDenuncia, Provincia, CausaSinistroId, Descrizione, StatoSinistroId, Embedding, EmbeddingAntifrode)
            SELECT Numero, PolizzaId, RiparatoreId, DATEADD(MONTH, -Mesi, CAST(GETDATE() AS date)), DATEADD(MONTH, -Mesi, CAST(GETDATE() AS date)),
                   'MI', Causa, Descrizione, Stato, CAST(Vettore AS VECTOR(4)), CAST(Vettore AS VECTOR(4))
            FROM (VALUES
                ('A-1', 1, 1,    1,  1, N'Tubo rotto in bagno', 2, '[1,0,0,0]'),
                ('A-2', 1, 2,    2,  1, N'Tubo rotto nel bagno', 1, '[1,0.1,0,0]'),
                ('A-3', 2, 1,    3, 20, N'Errore nel calcolo del solaio', 3, '[1,0.3,0,0]'),
                ('A-4', 2, 2,    3, 20, N'Ortogonale', 2, '[0,0,1,0]'),
                ('A-5', 1, 1,   30,  1, N'Tubo rotto in bagno, anni fa', 2, '[1,0,0,0]'),
                ('A-6', 2, NULL, 1,  1, N'Senza embedding', 2, NULL)
            ) AS v (Numero, PolizzaId, RiparatoreId, Mesi, Causa, Descrizione, Stato, Vettore);
            """);
    }

    [Test]
    public async Task CercaCoppie_SottoSoglia()
    {
        //SUT
        IReadOnlyList<CoppiaSospetta> a005 = await _repository.CercaCoppieAsync(mesi: 12, soglia: 0.05, CancellationToken);
        IReadOnlyList<CoppiaSospetta> a003 = await _repository.CercaCoppieAsync(mesi: 12, soglia: 0.03, CancellationToken);

        Assert.That(a005.Select(c => (c.NumeroA, c.NumeroB, c.Motivo)), Is.EqualTo(new[]
        {
            ("A-1", "A-2", MotivoSegnalazione.StessoContraente),
            ("A-2", "A-3", MotivoSegnalazione.SoloTestoSimile),
            ("A-1", "A-3", MotivoSegnalazione.StessoRiparatore),
        }));
        Assert.That(a005.Select(c => c.Distanza), Is.EqualTo(new[] { 0.00496, 0.01833, 0.04211 }).Within(0.0001));
        Assert.That(a005[0], Has.Property(nameof(CoppiaSospetta.GiorniTraDenunce)).InRange(28, 31)
            .And.Property(nameof(CoppiaSospetta.DescrizioneA)).EqualTo("Tubo rotto in bagno")
            .And.Property(nameof(CoppiaSospetta.DescrizioneB)).EqualTo("Tubo rotto nel bagno"));
        Assert.That(a003.Select(c => c.NumeroB), Is.EqualTo(new[] { "A-2", "A-3" }));
    }

    [Test]
    public async Task CercaCoppie_FuoriPeriodo_Esclusa()
    {
        //SUT
        IReadOnlyList<CoppiaSospetta> dodiciMesi = await _repository.CercaCoppieAsync(mesi: 12, soglia: 0.05, CancellationToken);
        IReadOnlyList<CoppiaSospetta> treAnni = await _repository.CercaCoppieAsync(mesi: 36, soglia: 0.05, CancellationToken);

        Assert.That(dodiciMesi.SelectMany(c => new[] { c.NumeroA, c.NumeroB }), Has.None.EqualTo("A-5"));
        Assert.That(treAnni.First(), Has.Property(nameof(CoppiaSospetta.NumeroA)).EqualTo("A-1")
            .And.Property(nameof(CoppiaSospetta.NumeroB)).EqualTo("A-5")
            .And.Property(nameof(CoppiaSospetta.Motivo)).EqualTo(MotivoSegnalazione.StessoContraenteERiparatore));
    }

    [Test]
    public async Task ControllaDenuncia_MotivoStessoContraente()
    {
        //SETUP
        float[] denuncia = [1f, 0f, 0f, 0f];

        //SUT
        IReadOnlyList<SegnalazioneDuplicato> senzaRiparatore = await _repository.CercaDuplicatiDenunciaAsync(
            denuncia, "CF-TEST-000001", riparatoreId: null, mesi: 12, soglia: 0.05, top: 20, CancellationToken);
        IReadOnlyList<SegnalazioneDuplicato> conRiparatore = await _repository.CercaDuplicatiDenunciaAsync(
            denuncia, "CF-TEST-000001", RiparatoreR2, mesi: 12, soglia: 0.05, top: 20, CancellationToken);
        IReadOnlyList<SegnalazioneDuplicato> primo = await _repository.CercaDuplicatiDenunciaAsync(
            denuncia, "CF-TEST-000001", RiparatoreR1, mesi: 12, soglia: 0.05, top: 1, CancellationToken);

        Assert.That(senzaRiparatore.Select(s => (s.NumeroSinistro, s.Motivo)), Is.EqualTo(new[]
        {
            ("A-1", MotivoSegnalazione.StessoContraente),
            ("A-2", MotivoSegnalazione.StessoContraente),
            ("A-3", MotivoSegnalazione.SoloTestoSimile),
        }));
        Assert.That(senzaRiparatore[2], Has.Property(nameof(SegnalazioneDuplicato.Contraente)).EqualTo("Contraente Due")
            .And.Property(nameof(SegnalazioneDuplicato.Riparatore)).EqualTo("Riparatore Uno")
            .And.Property(nameof(SegnalazioneDuplicato.Causa)).EqualTo(Core.Dominio.CausaSinistro.ErroreProgettuale)
            .And.Property(nameof(SegnalazioneDuplicato.Stato)).EqualTo(Core.Dominio.StatoSinistro.Respinto)
            .And.Property(nameof(SegnalazioneDuplicato.DataDenuncia)).EqualTo(DateOnly.FromDateTime(DateTime.Today).AddMonths(-3)));

        // Con il riparatore di A-2 la coppia contraente + riparatore passa davanti ad A-1, anche se più distante.
        Assert.That(conRiparatore.Select(s => (s.NumeroSinistro, s.Motivo)), Is.EqualTo(new[]
        {
            ("A-2", MotivoSegnalazione.StessoContraenteERiparatore),
            ("A-1", MotivoSegnalazione.StessoContraente),
            ("A-3", MotivoSegnalazione.SoloTestoSimile),
        }));
        Assert.That(primo.Select(s => s.NumeroSinistro), Is.EqualTo(new[] { "A-1" }));
    }

    [Test]
    public async Task DistanzeCoppie_AncheFuoriSogliaEPeriodo()
    {
        //SETUP
        IReadOnlyList<CoppiaDuplicati> coppie =
        [
            new("A-1", "A-2", TipoCoppiaDuplicati.StessoContraente),
            new("A-1", "A-5", TipoCoppiaDuplicati.StessoContraente),
            new("A-1", "A-4", TipoCoppiaDuplicati.StessoRiparatore),
            new("A-1", "X-9", TipoCoppiaDuplicati.StessoRiparatore),
        ];

        //SUT
        IReadOnlyList<DistanzaCoppiaAttesa> distanze = await _repository.DistanzeCoppieAsync(coppie, mesi: 12, CancellationToken);

        Assert.That(distanze.Select(d => d.Coppia), Is.EqualTo(coppie));
        Assert.That(distanze.Select(d => d.Distanza), Is.EqualTo(new double?[] { 0.00496, 0, 1, null }).Within(0.0001));
        Assert.That(distanze.Select(d => d.InPeriodo), Is.EqualTo(new[] { true, false, true, false }));
    }
}
