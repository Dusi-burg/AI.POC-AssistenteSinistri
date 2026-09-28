using static Dusiburg.AI.Sinistri.Core.Dominio.CausaSinistro;
using static Dusiburg.AI.Sinistri.Ingestion.Seed.Template;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>
/// Template delle cause del prodotto Casa e fabbricati. I valori degli slot includono l'articolo quando serve
/// (<c>{Materiale}</c> = "il parquet"), così le frasi restano corrette con qualunque valore; <c>{Circostanza}</c> apre sempre la frase.
/// </summary>
internal static class TemplateCasaFabbricati
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Slot { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        ["Stanza"] = ["bagno", "cucina", "soggiorno", "lavanderia", "camera da letto", "taverna", "corridoio", "mansarda"],
        ["Materiale"] = ["il parquet", "il controsoffitto in cartongesso", "le piastrelle", "l'intonaco", "il pavimento in laminato", "la tinteggiatura delle pareti", "il battiscopa"],
        ["Oggetto"] = ["il televisore", "la caldaia", "l'inverter", "il frigorifero", "il quadro elettrico", "la lavatrice", "il computer", "il climatizzatore", "il cancello automatico", "il piano a induzione"],
        ["Circostanza"] = ["Durante la notte", "Al rientro dalle ferie", "Nel fine settimana", "Nel primo pomeriggio", "Mentre ero al lavoro", "In tarda serata", "Di prima mattina"],
        ["Danno"] = ["macchie estese sul soffitto", "il rigonfiamento del pavimento", "il distacco dell'intonaco", "danni alla tinteggiatura delle pareti", "il deterioramento del battiscopa", "l'imbarcamento delle doghe del parquet"],
        ["Impianto"] = ["tubo di mandata", "raccordo del flessibile", "sifone del lavello", "tubo del termosifone", "collettore dell'impianto di riscaldamento", "tubo di scarico della lavatrice", "rubinetto d'arresto"],
        ["Copertura"] = ["le tegole del tetto", "la guaina della terrazza", "il comignolo", "la grondaia", "l'antenna televisiva", "la tettoia del posto auto"],
        ["Evento"] = ["un forte temporale", "una violenta grandinata", "una tromba d'aria", "una raffica di vento molto forte", "un nubifragio"],
        ["Bene"] = ["gioielli", "un computer portatile", "denaro contante", "orologi", "una bicicletta elettrica", "argenteria", "una macchina fotografica"],
        ["Accesso"] = ["forzando la porta d'ingresso", "rompendo il vetro della porta finestra", "scardinando una finestra al piano terra", "forzando la tapparella della camera"],
    };

    /// <summary>Frasi di contorno opzionali (0–2 per descrizione): riducono la somiglianza tra sinistri dello stesso template.</summary>
    public static IReadOnlyList<string> FrasiDiContorno { get; } =
    [
        "Ho scattato alcune fotografie dei danni.",
        "Resto a disposizione per il sopralluogo del perito.",
        "L'amministratore di condominio è stato informato.",
        "Ho conservato le parti sostituite.",
        "Allego il preventivo del riparatore.",
        "Il fatto è stato notato anche dai vicini.",
        "Chiedo di essere contattato al più presto.",
    ];

    public static IReadOnlyList<ProfiloCausa> Profili { get; } =
    [
        new(AcquaCondotta, 0.22, 800m, 15_000m, 0.20,
        [
            Demo(AcquaCondotta, TemaDemo.TuboParquetControsoffitto,
                "{Circostanza} si è rotto un tubo dell'impianto idrico nel bagno del piano superiore e l'acqua è scesa in soggiorno, danneggiando il parquet e il controsoffitto in cartongesso.",
                "Il perito ha accertato la rottura accidentale della tubazione di adduzione sotto traccia; i danni al parquet e al controsoffitto sono riconducibili all'evento. Danno indennizzabile ai sensi dell'Art. 2.4, con rimborso delle spese di ricerca del guasto (Art. 2.5).",
                "Sopralluogo eseguito: rottura del tubo confermata dall'idraulico intervenuto, parquet imbarcato su circa 20 mq e controsoffitto da rifare. Liquidazione al netto della franchigia (Art. 4.3)."),
            Demo(AcquaCondotta, TemaDemo.TuboParquetControsoffitto,
                "Abbiamo trovato il parquet del soggiorno allagato: si era rotto il tubo di mandata sotto il pavimento del bagno. L'acqua è filtrata anche nel controsoffitto del piano inferiore, che si è staccato in più punti.",
                "Accertata la rottura improvvisa del tubo di mandata sotto il pavimento del bagno; parquet e controsoffitto danneggiati dall'acqua fuoriuscita. Danno indennizzabile ai sensi dell'Art. 2.4.",
                "Il perito conferma la dinamica descritta; rimborsate le spese di demolizione e ripristino del pavimento per la ricerca del guasto (Art. 2.5) entro il limite dell'Art. 4.3."),
            Demo(AcquaCondotta, TemaDemo.TuboParquetControsoffitto,
                "Si è verificata la rottura improvvisa di una tubazione in cucina. L'acqua ha invaso il pavimento in parquet e ha raggiunto il controsoffitto dell'appartamento sottostante.",
                "Rottura accidentale della tubazione confermata; danni al parquet e al controsoffitto del piano inferiore indennizzati ai sensi degli Art. 2.4 e 2.9.",
                "Il tecnico ha individuato la rottura del tubo in cucina; il parquet va sostituito e il controsoffitto sottostante ripristinato. Liquidazione al netto della franchigia (Art. 4.3)."),
            Standard(AcquaCondotta, "{Circostanza} si è rotto il {Impianto} in {Stanza}, causando {Danno}."),
            Standard(AcquaCondotta, "Perdita d'acqua dal {Impianto} in {Stanza}: l'acqua è uscita per diverse ore danneggiando {Materiale}."),
            Standard(AcquaCondotta, "Abbiamo notato una macchia d'acqua che si allargava sul soffitto in {Stanza}; l'idraulico ha individuato la rottura del {Impianto}. Il danno ha interessato anche {Materiale}."),
            Standard(AcquaCondotta, "{Circostanza} è scoppiato il flessibile della lavatrice in {Stanza} e l'acqua ha allagato il locale, rovinando {Materiale}."),
            Standard(AcquaCondotta, "Il tubo del termosifone in {Stanza} si è crepato improvvisamente e l'acqua calda è fuoriuscita sul pavimento, causando {Danno}."),
            Standard(AcquaCondotta, "Rottura della tubazione di scarico della doccia: l'acqua è filtrata nel solaio e ha provocato {Danno} nella stanza sottostante."),
            Standard(AcquaCondotta, "L'idraulico ha trovato una perdita dal collettore del riscaldamento in {Stanza}. Prima della riparazione l'acqua ha danneggiato {Materiale} e ha raggiunto il vicino di sotto."),
            Standard(AcquaCondotta, "{Circostanza} la caldaia ha iniziato a perdere acqua dal circuito e il pavimento in {Stanza} si è allagato, causando {Danno}."),
            Standard(AcquaCondotta, "Si è staccato il raccordo sotto il lavello della cucina e l'acqua è uscita per tutta la notte, danneggiando {Materiale} e i mobili."),
            Respinto(AcquaCondotta,
                "Dopo le ultime piogge sono comparse infiltrazioni d'acqua dal terrazzo sul soffitto in {Stanza}, con {Danno}. Le infiltrazioni si ripresentano da qualche anno a ogni pioggia intensa.",
                [
                    "Il perito rileva infiltrazioni pregresse dovute a guaine deteriorate del terrazzo, riconducibili a mancata manutenzione (Art. 3.1). Nessuna rottura accidentale di impianti: sinistro respinto.",
                    "Accertate infiltrazioni ricorrenti da una copertura non manutenuta da anni; il danno rientra nell'esclusione dell'Art. 3.1.",
                ],
                "Il perito ha individuato la rottura di un tubo di scarico del terrazzo, non visibile, come causa delle infiltrazioni: danno indennizzabile ai sensi dell'Art. 2.4."),
            Respinto(AcquaCondotta,
                "Da alcuni mesi il muro in {Stanza} è umido e si è formata muffa; togliendo le piastrelle è emersa una piccola perdita dal tubo, che gocciolava da tempo.",
                [
                    "Il perito accerta uno stillicidio protratto nel tempo da una giunzione della tubazione, con umidità e muffe diffuse: danno escluso ai sensi dell'Art. 3.2.",
                    "Danno da trasudamento e umidità sviluppatosi lentamente, non da rottura improvvisa: si applica l'esclusione dell'Art. 3.2.",
                ],
                "La perdita è risultata dovuta a una rottura recente del raccordo; riconosciuti i danni e le spese di ricerca del guasto (Art. 2.4 e 2.5)."),
            Respinto(AcquaCondotta,
                "Nella casa di montagna, rimasta chiusa e senza riscaldamento durante l'inverno, si sono rotte per il gelo le tubazioni del bagno. Al nostro arrivo l'acqua aveva allagato il piano terra, rovinando {Materiale}.",
                [
                    "L'abitazione secondaria era disabitata e non riscaldata da oltre due mesi e l'impianto non era stato svuotato: danno escluso ai sensi dell'Art. 3.3.",
                    "Rottura da gelo in locali non riscaldati: si applica l'esclusione dell'Art. 3.3, sinistro respinto.",
                ],
                "Il riscaldamento risultava acceso in modalità antigelo e guasto per un blocco della caldaia: danno da gelo indennizzabile ai sensi dell'Art. 2.4."),
        ],
        [
            "Il perito ha accertato la rottura accidentale dell'impianto idrico e la conseguente fuoriuscita di acqua condotta. Danno indennizzabile ai sensi dell'Art. 2.4.",
            "Sopralluogo eseguito: rottura improvvisa confermata dall'idraulico, danni ai pavimenti e alle pareti coerenti con la dinamica descritta. Liquidazione al netto della franchigia (Art. 4.3).",
            "Accertata la fuoriuscita di acqua da una tubazione rotta; rimborsate anche le spese di ricerca e riparazione del guasto (Art. 2.5).",
        ],
        [
            "Il perito non ha riscontrato rotture degli impianti: le macchie sono dovute a condensa e umidità di risalita (Art. 3.2).",
            "La perdita deriva dalla corrosione di una tubazione vetusta, già oggetto di precedenti interventi e mai sostituita: danno escluso ai sensi degli Art. 3.1 e 3.4.",
        ]),

        new(EventoAtmosferico, 0.14, 1_500m, 25_000m, 0.15,
        [
            Standard(EventoAtmosferico, "A causa di {Evento} si sono staccate alcune tegole del tetto e l'acqua piovana è entrata in mansarda, causando {Danno}."),
            Standard(EventoAtmosferico, "{Circostanza} {Evento} ha danneggiato {Copertura}. Nei giorni seguenti è stato necessario l'intervento di un lattoniere."),
            Standard(EventoAtmosferico, "Una tromba d'aria ha sradicato un albero del giardino condominiale, che è caduto sulla recinzione e sul muro di cinta."),
            Standard(EventoAtmosferico, "Il vento fortissimo ha divelto parte della tettoia del posto auto e ha spostato le tegole del tetto. Durante il temporale successivo l'acqua è entrata in {Stanza}."),
            Standard(EventoAtmosferico, "Dopo {Evento} abbiamo trovato il comignolo crollato sul tetto e diverse tegole rotte; dal soffitto in {Stanza} gocciola acqua."),
            Standard(EventoAtmosferico, "La grandine di grosse dimensioni ha forato la guaina della terrazza e ha ammaccato la lattoneria del tetto. Con le piogge successive si sono formate infiltrazioni in {Stanza}."),
            Standard(EventoAtmosferico, "{Circostanza} una raffica di vento ha rovesciato l'antenna televisiva che, cadendo, ha rotto parte del manto di copertura."),
            Standard(EventoAtmosferico, "Un nubifragio ha fatto cedere la grondaia sul lato nord della casa; l'acqua è scesa lungo la parete ed è entrata in {Stanza}, causando {Danno}."),
            Standard(EventoAtmosferico, "Il vento ha scoperchiato parte del tetto: diverse file di coppi sono finite in strada e la pioggia è entrata nel sottotetto."),
            RespintoDemo(EventoAtmosferico, TemaDemo.GrandinePannelli,
                "Il cliente dice che la grandine ha rotto i pannelli solari sul tetto: diversi moduli fotovoltaici hanno i vetri incrinati e l'impianto produce molto meno di prima.",
                [
                    "Il perito accerta rotture da grandine sui moduli fotovoltaici. L'estensione dell'Art. 2.3 non è richiamata in polizza: danno escluso ai sensi dell'Art. 3.6.",
                    "Danni da grandine ai pannelli solari confermati, ma la polizza non comprende l'estensione grandine su enti fragili (Art. 2.3): si applica l'esclusione dell'Art. 3.6.",
                ],
                "La polizza richiama l'estensione dell'Art. 2.3: danni da grandine ai pannelli indennizzati entro il 20% della somma assicurata, con lo scoperto dell'Art. 4.2."),
            RespintoDemo(EventoAtmosferico, TemaDemo.GrandinePannelli,
                "{Circostanza} una violenta grandinata ha danneggiato i pannelli solari termici sul tetto e ha rotto i vetri dei lucernari della mansarda.",
                [
                    "Grandine su pannelli solari e lucernari: enti esclusi dall'Art. 3.6 in assenza dell'estensione dell'Art. 2.3, non richiamata in polizza.",
                    "Il perito conferma la grandinata ma i danni riguardano solo pannelli e lucernari, esclusi ai sensi dell'Art. 3.6.",
                ],
                "Estensione grandine su enti fragili (Art. 2.3) presente in polizza: danno a pannelli e lucernari indennizzato con lo scoperto dell'Art. 4.2."),
            RespintoDemo(EventoAtmosferico, TemaDemo.GrandinePannelli,
                "La grandinata ha spaccato le tapparelle e i vetri di due finestre esposte a ovest e ha incrinato alcuni pannelli fotovoltaici sul tetto.",
                [
                    "Danni da grandine a serramenti e pannelli fotovoltaici, esclusi ai sensi dell'Art. 3.6: l'estensione dell'Art. 2.3 non è operante.",
                    "Nessun danno al tetto o alle strutture: i soli serramenti e pannelli danneggiati rientrano nell'esclusione dell'Art. 3.6.",
                ],
                "Riconosciuti i danni a serramenti e pannelli in forza dell'estensione dell'Art. 2.3, con scoperto del 10% (Art. 4.2)."),
        ],
        [
            "Il perito ha verificato la violenza dell'evento atmosferico su più edifici della zona e i danni alla copertura. Danno indennizzabile ai sensi dell'Art. 2.2 con lo scoperto dell'Art. 4.2.",
            "Accertati i danni al tetto causati dal vento e le conseguenti infiltrazioni per bagnamento. Liquidazione con scoperto del 10% (Art. 4.2).",
        ],
        [
            "Non risultano eventi atmosferici di intensità eccezionale nella zona alla data indicata; le infiltrazioni derivano da coperture deteriorate (Art. 3.1).",
            "Il danno è dovuto ad acqua piovana entrata da serramenti lasciati aperti (Art. 3.10).",
        ]),

        new(FenomenoElettrico, 0.12, 300m, 6_000m, 0.10,
        [
            Demo(FenomenoElettrico, TemaDemo.SovratensioneQuadroInverter,
                "Durante un temporale un fulmine caduto nelle vicinanze ha provocato una sovratensione che ha danneggiato il quadro elettrico e l'inverter dell'impianto fotovoltaico.",
                "Il perito ha accertato danni da sovratensione al quadro elettrico e all'inverter, compatibili con l'attività di fulmini registrata nella zona. Danno indennizzabile ai sensi dell'Art. 2.6.",
                "Verificata con il tecnico la bruciatura delle schede dell'inverter e degli interruttori del quadro per sovratensione. Liquidazione con franchigia ed entro il limite dell'Art. 4.4."),
            Demo(FenomenoElettrico, TemaDemo.SovratensioneQuadroInverter,
                "{Circostanza} una sovratensione sulla rete ha bruciato l'inverter del fotovoltaico e alcuni interruttori del quadro elettrico generale; da allora l'impianto è fermo.",
                "Il distributore conferma un'anomalia di tensione sulla linea nella data indicata; inverter e interruttori del quadro danneggiati da fenomeno elettrico (Art. 2.6).",
                "Accertata la sovratensione di rete; sostituiti inverter e componenti del quadro elettrico, liquidati al netto della franchigia (Art. 4.4)."),
            Demo(FenomenoElettrico, TemaDemo.SovratensioneQuadroInverter,
                "Dopo un forte temporale è saltata la corrente e al ripristino il quadro elettrico era annerito. Risultano danneggiati anche l'inverter, la caldaia e il cancello automatico.",
                "Il perito conferma i danni da scarica atmosferica al quadro elettrico, all'inverter, alla scheda della caldaia e alla centralina del cancello. Danno indennizzabile ai sensi dell'Art. 2.6.",
                "Danni multipli da sovratensione accertati; indennizzo entro il limite annuo dell'Art. 4.4."),
            Standard(FenomenoElettrico, "Dopo un temporale si è bruciata la caldaia e anche il televisore del soggiorno non si accende più."),
            Standard(FenomenoElettrico, "{Circostanza} un fulmine ha colpito l'antenna del condominio e da quel momento {Oggetto} non funziona più."),
            Standard(FenomenoElettrico, "Un calo di tensione seguito da un picco ha danneggiato diversi apparecchi: il tecnico ha trovato la scheda bruciata e {Oggetto} non funziona più."),
            Standard(FenomenoElettrico, "Durante un temporale estivo è scattato più volte il salvavita; il giorno dopo {Oggetto} e il modem erano fuori uso."),
            Standard(FenomenoElettrico, "Una scarica elettrica dovuta a un guasto della rete di distribuzione ha danneggiato l'impianto di allarme e la centralina del riscaldamento."),
            Respinto(FenomenoElettrico,
                "Da qualche giorno la lavatrice faceva rumori strani e ieri si è fermata del tutto; il tecnico parla di un problema alla scheda elettronica.",
                [
                    "Il tecnico incaricato dal perito ha riscontrato un guasto meccanico del motore per usura, non riconducibile a un fenomeno elettrico esterno: danno escluso ai sensi dell'Art. 3.7.",
                    "Nessuna anomalia sulla rete nella data indicata; il danno deriva da un difetto interno dell'apparecchio (Art. 3.7).",
                ],
                "Il tecnico ha rilevato tracce di sovratensione sulla scheda elettronica: danno riconducibile a fenomeno elettrico (Art. 2.6)."),
            Respinto(FenomenoElettrico,
                "Durante un temporale si è guastata la vecchia caldaia, installata più di quindici anni fa, e il televisore del soggiorno acquistato nel 2010.",
                [
                    "Gli apparecchi danneggiati hanno oltre 10 anni di vita dall'installazione: danno escluso ai sensi dell'Art. 3.7.",
                    "Accertata l'età della caldaia e del televisore, entrambi oltre i dieci anni: si applica l'esclusione dell'Art. 3.7.",
                ],
                "Riconosciuto il solo danno alla scheda di controllo della caldaia, sostituita due anni fa; esclusi gli apparecchi con oltre 10 anni (Art. 3.7)."),
        ],
        [
            "Il perito ha accertato danni da sovratensione agli apparecchi elettrici, coerenti con il temporale segnalato. Danno indennizzabile ai sensi dell'Art. 2.6, al netto della franchigia (Art. 4.4).",
            "Il tecnico ha confermato la bruciatura delle schede elettroniche per fenomeno elettrico esterno; liquidazione entro il limite dell'Art. 4.4.",
        ],
        [
            "Il guasto è dovuto all'usura dei componenti e non a un fenomeno elettrico (Art. 3.7).",
            "L'apparecchio danneggiato ha più di 10 anni di vita: danno escluso (Art. 3.7).",
        ]),

        new(Incendio, 0.05, 2_000m, 80_000m, 0.05,
        [
            Standard(Incendio, "{Circostanza} si è sviluppato un incendio in cucina partito dal piano cottura; le fiamme hanno danneggiato i mobili e il fumo ha annerito le pareti di tutto l'appartamento."),
            Standard(Incendio, "Un cortocircuito nella ciabatta elettrica del soggiorno ha provocato un principio d'incendio che ha bruciato il divano e danneggiato {Materiale}."),
            Standard(Incendio, "Le fiamme partite dalla canna fumaria del camino hanno raggiunto il tetto in legno, distruggendo parte della copertura prima dell'intervento dei Vigili del fuoco."),
            Standard(Incendio, "Un incendio scoppiato nel box auto del vicino si è propagato alla nostra cantina, danneggiando le pareti e quanto vi era custodito."),
            Standard(Incendio, "Durante la notte un fulmine ha colpito il tetto e ha innescato un incendio nel sottotetto; i Vigili del fuoco hanno dovuto rimuovere parte delle travi."),
            Standard(Incendio, "Lo scoppio della caldaia a gas ha provocato danni alla parete del locale tecnico e ai serramenti vicini."),
            Standard(Incendio, "Un incendio è partito dall'asciugatrice in lavanderia; il fumo ha annerito le pareti e i soffitti del piano e ha rovinato gli arredi."),
            Standard(Incendio, "Un mozzicone di sigaretta gettato da un piano superiore ha incendiato la tenda del balcone e le fiamme hanno danneggiato la porta finestra e {Materiale}."),
        ],
        [
            "I Vigili del fuoco hanno redatto il verbale di intervento; il perito conferma l'origine accidentale dell'incendio. Danno indennizzabile ai sensi dell'Art. 2.1, comprese le spese di sgombero (Art. 2.11).",
            "Accertati danni da incendio e da fumo al fabbricato e al contenuto; liquidazione al netto della franchigia (Art. 4.1).",
        ],
        [
            "Le indagini hanno evidenziato l'origine dolosa dell'incendio, riconducibile all'Assicurato (Art. 3.9).",
            "Il fabbricato era in ristrutturazione, con il tetto smontato al momento del sinistro: danno escluso ai sensi dell'Art. 3.5.",
        ]),

        new(Furto, 0.07, 500m, 12_000m, 0.25,
        [
            Standard(Furto, "{Circostanza} i ladri sono entrati in casa {Accesso} e hanno portato via {Bene}."),
            Standard(Furto, "Al rientro abbiamo trovato la porta blindata forzata e la casa a soqquadro: mancano {Bene} e alcuni ricordi di famiglia."),
            Standard(Furto, "I ladri si sono introdotti dal balcone {Accesso} e hanno rubato {Bene}; hanno anche danneggiato la serratura della porta interna."),
            Standard(Furto, "Durante le vacanze estive ignoti hanno scassinato la porta della cantina e rubato {Bene} e alcuni attrezzi da lavoro."),
            Standard(Furto, "Tentato furto nella notte: i ladri hanno danneggiato la tapparella e il serramento della camera ma sono fuggiti quando è scattato l'allarme."),
            Standard(Furto, "Mentre dormivamo i ladri sono entrati {Accesso} e hanno preso {Bene} dal soggiorno. Abbiamo sporto denuncia ai Carabinieri il mattino seguente."),
            Respinto(Furto,
                "Al rientro a casa ci siamo accorti che mancavano {Bene}. Non ci sono segni di effrazione: probabilmente era rimasta aperta la finestra del bagno.",
                [
                    "Nessun segno di scasso sui mezzi di chiusura e finestra lasciata aperta: furto escluso ai sensi dell'Art. 3.8.",
                    "La denuncia ai Carabinieri non riporta tracce di effrazione; si applica l'esclusione dell'Art. 3.8.",
                ],
                "Rilevati segni di forzatura sul serramento della finestra, non notati inizialmente: furto indennizzabile ai sensi dell'Art. 2.7, con lo scoperto dell'Art. 4.5."),
            Respinto(Furto,
                "Dalla casa sono spariti {Bene}; la porta era chiusa e non risulta forzata. In quei giorni avevano le chiavi anche la collaboratrice domestica e un idraulico.",
                [
                    "Furto senza scasso né effrazione, commesso da persona con accesso ai locali: escluso ai sensi dell'Art. 3.8.",
                    "Nessuna traccia di introduzione forzata; le chiavi erano in possesso di terzi autorizzati (Art. 3.8).",
                ],
                "Le indagini hanno accertato l'uso di una chiave falsa, risultante dalla denuncia: furto equiparato allo scasso (Art. 1.8) e indennizzabile con lo scoperto dell'Art. 4.5."),
        ],
        [
            "Verificati i segni di scasso sulla porta d'ingresso e la denuncia all'Autorità. Furto indennizzabile ai sensi dell'Art. 2.7, con lo scoperto dell'Art. 4.5.",
            "Il perito ha accertato l'effrazione e la sottrazione dei beni denunciati; riconosciuti anche i guasti ai serramenti (Art. 2.8).",
        ],
        [
            "Nessun segno di effrazione sui mezzi di chiusura (Art. 3.8).",
            "Le dichiarazioni sulle circostanze del furto sono risultate contraddittorie e non documentate (Art. 3.9).",
        ]),

        new(Cristalli, 0.04, 200m, 2_500m, 0.10,
        [
            Standard(Cristalli, "Durante le pulizie è caduto un mobile che ha rotto il vetro della porta finestra del soggiorno."),
            Standard(Cristalli, "{Circostanza} una pallonata dei ragazzi del cortile ha rotto il vetro della finestra della cucina."),
            Standard(Cristalli, "Si è rotto improvvisamente lo specchio a parete del bagno, staccatosi dai supporti."),
            Standard(Cristalli, "La lastra in cristallo del tavolo del soggiorno si è spaccata per un colpo accidentale."),
            Standard(Cristalli, "Chiudendo con forza la porta per il vento si è rotto il vetro del portoncino d'ingresso."),
            Standard(Cristalli, "Il box doccia in cristallo temperato è esploso improvvisamente in mille pezzi."),
            Standard(Cristalli, "Un vaso caduto dal balcone del piano di sopra ha rotto la vetrata del nostro terrazzo."),
            Standard(Cristalli, "Spostando i mobili si è incrinato il vetro dell'anta della vetrinetta in {Stanza}."),
        ],
        [
            "Accertata la rottura accidentale della lastra; rimborsato il costo di sostituzione ai sensi dell'Art. 2.10.",
            "Rottura verificata con fotografie e fattura del vetraio; danno indennizzabile (Art. 2.10) al netto della franchigia.",
        ],
        [
            "Il vetro presentava solo rigature e scheggiature superficiali, escluse dall'Art. 2.10.",
            "La rottura è avvenuta durante lavori di manutenzione del serramento: esclusa ai sensi dell'Art. 2.10.",
        ]),

        new(RcProprieta, 0.06, 1_000m, 20_000m, 0.15,
        [
            Standard(RcProprieta, "L'acqua fuoriuscita da un tubo rotto nel nostro bagno ha danneggiato il soffitto e i mobili dell'appartamento del vicino al piano di sotto."),
            Standard(RcProprieta, "Un pezzo di cornicione si è staccato dalla facciata ed è caduto sull'auto di un vicino parcheggiata in strada."),
            Standard(RcProprieta, "{Circostanza} una tegola spostata dal vento è caduta dal nostro tetto colpendo di striscio un passante, che chiede il rimborso delle spese mediche."),
            Standard(RcProprieta, "Le radici dell'albero del nostro giardino hanno sollevato e rotto la pavimentazione del cortile confinante."),
            Standard(RcProprieta, "Il vicino lamenta infiltrazioni nel suo box provenienti dal nostro giardino pensile, dopo la rottura dell'impianto di irrigazione."),
            Standard(RcProprieta, "Un vaso di fiori caduto dal nostro balcone ha danneggiato la tenda e il tavolino del bar al piano terra."),
            Standard(RcProprieta, "Il cancello automatico, per un malfunzionamento, si è chiuso sull'auto di un ospite danneggiandone la fiancata."),
            Standard(RcProprieta, "Una lastra di ghiaccio scivolata dal nostro tetto ha sfondato il lucernario del laboratorio artigiano confinante."),
        ],
        [
            "Accertata la responsabilità dell'Assicurato quale proprietario del fabbricato; risarcito il terzo danneggiato ai sensi dell'Art. 2.9.",
            "Danno al terzo documentato con preventivo e fotografie; liquidazione entro il massimale (Art. 2.9) al netto della franchigia.",
        ],
        [
            "Il danno lamentato dal terzo deriva da infiltrazioni pregresse per mancata manutenzione, già note (Art. 3.1).",
            "Non è stata dimostrata la responsabilità dell'Assicurato: il danno è riconducibile a lavori eseguiti dal terzo stesso.",
        ]),
    ];
}
