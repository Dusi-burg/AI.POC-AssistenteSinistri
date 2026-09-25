using static Dusiburg.AI.Sinistri.Core.Dominio.CausaSinistro;
using static Dusiburg.AI.Sinistri.Ingestion.Seed.Template;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>
/// Template delle cause del prodotto RC professionale tecnici. <c>{Committente}</c> e <c>{Circostanza}</c> aprono sempre la frase;
/// <c>{Opera}</c> include l'articolo ("una palazzina di quattro piani").
/// </summary>
internal static class TemplateRcProfTecnici
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Slot { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        ["Opera"] = ["una palazzina di quattro piani", "una villetta bifamiliare", "un capannone artigianale", "una scuola primaria", "un'autorimessa interrata", "un edificio residenziale", "una struttura ricettiva"],
        ["Committente"] = ["Il condominio", "Una società immobiliare", "Il committente privato", "Il Comune", "L'impresa appaltatrice"],
        ["Circostanza"] = ["A lavori ultimati", "Durante l'esecuzione dei lavori", "In fase di collaudo", "A pochi mesi dalla consegna", "Dopo il primo inverno"],
    };

    public static IReadOnlyList<string> FrasiDiContorno { get; } =
    [
        "Il committente ha inviato una diffida tramite il proprio legale.",
        "Sono disponibili gli elaborati di progetto e il giornale dei lavori.",
        "L'assicurato contesta la fondatezza della richiesta.",
        "È stata incaricata una consulenza tecnica di parte.",
        "La richiesta è pervenuta con raccomandata.",
        "È in corso un accertamento tecnico preventivo.",
    ];

    public static IReadOnlyList<ProfiloCausa> Profili { get; } =
    [
        new(ErroreProgettuale, 0.12, 5_000m, 150_000m, 0.20,
        [
            Demo(ErroreProgettuale, TemaDemo.SolaioStrutture,
                "{Committente} contesta un errore nel calcolo statico del solaio di {Opera}: a pochi mesi dalla consegna il solaio presenta frecce eccessive e fessurazioni, e ne chiede il rifacimento.",
                "Il consulente tecnico ha confermato il sottodimensionamento delle armature del solaio rispetto ai carichi di progetto. Richiesta fondata ai sensi degli Art. 2.1 e 2.6; risarcimento al netto della franchigia (Art. 4.1).",
                "Accertato l'errore di calcolo nella verifica a flessione delle travi del solaio; riconosciuti i costi di consolidamento con rinforzi in acciaio (Art. 2.6)."),
            Demo(ErroreProgettuale, TemaDemo.SolaioStrutture,
                "Un ingegnere dello studio ha sbagliato il calcolo di un solaio di {Opera} e il committente chiede i danni per il rifacimento della struttura.",
                "Errore nel calcolo dei carichi del solaio confermato dal consulente; costi di demolizione e rifacimento risarciti ai sensi degli Art. 2.1 e 2.6.",
                "La richiesta di risarcimento è pervenuta nel periodo di efficacia (Art. 1.4); errore di calcolo accertato, risarcimento al netto della franchigia (Art. 4.1)."),
            Demo(ErroreProgettuale, TemaDemo.SolaioStrutture,
                "Durante il collaudo statico di {Opera} sono emerse carenze nel progetto strutturale: le travi del primo impalcato non sono verificate per i carichi previsti. {Committente} chiede il rimborso degli interventi di rinforzo.",
                "Il collaudatore ha documentato l'errore nel progetto delle travi; interventi di rinforzo risarciti ai sensi dell'Art. 2.6.",
                "Accertata la responsabilità del progettista delle strutture; riconosciuti i maggiori costi di rinforzo al netto della franchigia (Art. 4.1)."),
            Demo(ErroreProgettuale, TemaDemo.SolaioStrutture,
                "Nel progetto delle fondazioni di {Opera} non è stata considerata la relazione geologica: si sono verificati cedimenti differenziali e lesioni nelle strutture portanti.",
                "Il consulente conferma che il progetto delle fondazioni ignorava le indicazioni della relazione geologica; danno risarcito ai sensi dell'Art. 2.1.",
                "Riconosciuti i costi di consolidamento delle fondazioni e di ripristino delle strutture lesionate (Art. 2.6)."),
            Standard(ErroreProgettuale, "{Committente} lamenta che l'impianto di riscaldamento progettato dallo studio è sottodimensionato e chiede i costi per sostituire la caldaia e i terminali."),
            Standard(ErroreProgettuale, "{Circostanza} è emerso che le quote del progetto architettonico di {Opera} erano errate: le scale non rispettano le altezze minime e vanno rifatte."),
            Standard(ErroreProgettuale, "Nel progetto di {Opera} è stato omesso il dimensionamento dei giunti di dilatazione; sono comparse fessure estese sulle facciate e il committente chiede il ripristino."),
            Standard(ErroreProgettuale, "{Committente} contesta l'errata progettazione dell'impermeabilizzazione dell'autorimessa interrata, con infiltrazioni diffuse e danni alle auto parcheggiate."),
            Standard(ErroreProgettuale, "Nel progetto acustico di {Opera} non sono stati rispettati i requisiti passivi; gli acquirenti chiedono una riduzione del prezzo al costruttore, che si rivale sullo studio."),
            Standard(ErroreProgettuale, "Un errore nel calcolo dei carichi della copertura in legno di {Opera} ha causato l'inflessione delle travi dopo la prima nevicata abbondante."),
            Respinto(ErroreProgettuale,
                "{Committente} ha formalizzato la richiesta di risarcimento per le lesioni al solaio di {Opera}, già contestate per lettera allo studio due anni fa, prima della stipula della polizza attuale.",
                [
                    "Le lesioni erano state contestate per iscritto prima della decorrenza della polizza: circostanza nota, esclusa ai sensi dell'Art. 3.4.",
                    "Dalla documentazione emerge una contestazione del committente anteriore alla decorrenza; si applica l'esclusione dell'Art. 3.4.",
                ],
                "La contestazione precedente riguardava un difetto diverso da quello oggetto della richiesta: sinistro in garanzia ai sensi dell'Art. 2.1."),
            Respinto(ErroreProgettuale,
                "Il cliente chiede i danni per un errore nei calcoli delle travi di {Opera}; lo studio aveva ricevuto una segnalazione dei difetti dal direttore dei lavori già prima della decorrenza della polizza.",
                [
                    "La segnalazione dei difetti era nota all'Assicurato prima della decorrenza: esclusione dell'Art. 3.4.",
                    "Circostanza nota prima della stipula e non dichiarata: richiesta esclusa ai sensi dell'Art. 3.4.",
                ],
                "La segnalazione del direttore dei lavori non riguardava le travi contestate: errore di calcolo in garanzia (Art. 2.1)."),
        ],
        [
            "Il consulente tecnico incaricato dalla Società ha confermato l'errore di progettazione e il nesso con i danni lamentati. Risarcimento ai sensi dell'Art. 2.1 al netto della franchigia (Art. 4.1).",
            "Accertata la responsabilità professionale dell'Assicurato; riconosciuti i costi di rifacimento dell'opera (Art. 2.6).",
        ],
        [
            "L'errore contestato risale a prima del periodo di retroattività previsto dall'Art. 1.5.",
            "Il danno deriva da un'esecuzione difforme dal progetto imputabile all'impresa, non da un errore dell'Assicurato.",
        ]),

        new(ErroreDirezioneLavori, 0.08, 5_000m, 100_000m, 0.20,
        [
            Standard(ErroreDirezioneLavori, "{Committente} contesta allo studio l'omessa vigilanza in cantiere: l'impresa ha posato un massetto più sottile di quanto previsto dal progetto e i pavimenti di {Opera} si sono fessurati."),
            Standard(ErroreDirezioneLavori, "Durante i lavori di {Opera} il direttore dei lavori ha accettato calcestruzzo non conforme alle prescrizioni; le prove sui provini hanno dato resistenze insufficienti e alcune strutture vanno demolite."),
            Standard(ErroreDirezioneLavori, "{Committente} chiede i danni per le infiltrazioni dalla copertura di {Opera}: l'impermeabilizzazione non è stata eseguita come da capitolato e la direzione lavori non l'ha rilevato."),
            Standard(ErroreDirezioneLavori, "{Circostanza} sono emerse difformità tra le opere eseguite e il progetto approvato di {Opera}; il Comune ha negato l'agibilità e il committente chiede i costi di regolarizzazione."),
            Standard(ErroreDirezioneLavori, "La contabilità dei lavori di {Opera} tenuta dal direttore dei lavori riportava quantità superiori a quelle eseguite, e il committente chiede la restituzione delle somme pagate in più all'impresa."),
            Standard(ErroreDirezioneLavori, "Il direttore dei lavori non ha verificato la posa degli ancoraggi della facciata ventilata di {Opera}: alcune lastre si sono staccate danneggiando le auto nel parcheggio."),
            Standard(ErroreDirezioneLavori, "{Committente} lamenta che i serramenti installati hanno prestazioni termiche inferiori a quelle di progetto e che la direzione lavori li ha accettati senza verifiche."),
            Standard(ErroreDirezioneLavori, "Per il mancato controllo delle pendenze degli scarichi, nei bagni di {Opera} si verificano ristagni e rigurgiti; il committente chiede il rifacimento degli impianti."),
            Respinto(ErroreDirezioneLavori,
                "Lo studio ha eseguito il collaudo statico di {Opera}, di cui aveva curato anche la progettazione; dopo la consegna sono comparse lesioni e il committente chiede i danni al collaudatore.",
                [
                    "L'attività di collaudo riguardava un'opera progettata dallo stesso Assicurato: esclusione dell'Art. 3.5.",
                    "Il danno deriva dal collaudo di un'opera propria, escluso ai sensi dell'Art. 3.5.",
                ],
                "Accertato che il collaudo riguardava strutture progettate da altro professionista; responsabilità in garanzia ai sensi dell'Art. 2.2."),
            Respinto(ErroreDirezioneLavori,
                "{Committente} chiede i danni allo studio per il collaudo tecnico-amministrativo di {Opera}, opera di cui lo studio era anche direttore dei lavori.",
                [
                    "Collaudo di un'opera diretta dall'Assicurato: la richiesta rientra nell'esclusione dell'Art. 3.5.",
                    "L'Assicurato ha collaudato un'opera di cui era direttore dei lavori (Art. 3.5): sinistro respinto.",
                ],
                "La richiesta riguarda in realtà l'omessa vigilanza come direttore dei lavori, in garanzia ai sensi dell'Art. 2.2."),
        ],
        [
            "Accertata l'omessa vigilanza del direttore dei lavori sulle lavorazioni contestate; danno indennizzabile ai sensi dell'Art. 2.2 al netto della franchigia.",
            "Il consulente tecnico conferma la responsabilità concorrente della direzione lavori; risarcita la quota a carico dell'Assicurato (Art. 2.2).",
        ],
        [
            "Le difformità sono imputabili esclusivamente all'impresa esecutrice, che non ha seguito le disposizioni scritte della direzione lavori.",
            "La contestazione era già nota all'Assicurato prima della decorrenza (Art. 3.4).",
        ]),

        new(SicurezzaCantiere, 0.05, 10_000m, 200_000m, 0.25,
        [
            Standard(SicurezzaCantiere, "Durante i lavori di {Opera} un operaio è caduto da un ponteggio privo di parapetti; l'INAIL agisce in rivalsa anche nei confronti del coordinatore per la sicurezza."),
            Standard(SicurezzaCantiere, "Nel cantiere di {Opera} il crollo di uno scavo non armato ha ferito due operai; il piano di sicurezza non prevedeva le opere di sostegno."),
            Standard(SicurezzaCantiere, "{Committente} chiede i danni al coordinatore per la sicurezza per l'infortunio di un dipendente dell'impresa, colpito da materiale caduto dall'alto."),
            Standard(SicurezzaCantiere, "Un passante è stato colpito da un pannello caduto dalla recinzione del cantiere di {Opera} durante una giornata di vento."),
            Standard(SicurezzaCantiere, "Durante lo smontaggio della gru nel cantiere di {Opera} il braccio ha colpito il tetto di un edificio confinante."),
            Standard(SicurezzaCantiere, "L'impresa subappaltatrice ha lavorato senza linee vita sul tetto di {Opera}; un operaio è caduto e il coordinatore è chiamato a rispondere per omessa vigilanza."),
            Respinto(SicurezzaCantiere,
                "L'ispettorato del lavoro ha sanzionato il coordinatore per la sicurezza per carenze nel piano di sicurezza del cantiere di {Opera}; l'Assicurato chiede il rimborso della sanzione pagata.",
                [
                    "La richiesta riguarda una sanzione amministrativa, esclusa ai sensi dell'Art. 3.3.",
                    "Multe e ammende inflitte all'Assicurato non sono risarcibili (Art. 3.3).",
                ],
                "Oltre alla sanzione, esclusa (Art. 3.3), è pervenuta la richiesta di un terzo infortunato: risarcita ai sensi dell'Art. 2.3."),
            Respinto(SicurezzaCantiere,
                "{Committente} chiede allo studio il rimborso della sanzione ricevuta dall'ASL per irregolarità di sicurezza nel cantiere di {Opera}, attribuendone la responsabilità al coordinatore.",
                [
                    "Sanzioni amministrative inflitte al committente: escluse ai sensi dell'Art. 3.3.",
                    "La richiesta ha per oggetto il rimborso di una sanzione, non un danno risarcibile (Art. 3.3).",
                ],
                "Accertato anche un danno a terzi causato dalle carenze del piano di sicurezza: risarcito ai sensi dell'Art. 2.3, esclusa la sanzione."),
        ],
        [
            "Accertate carenze nel piano di sicurezza e coordinamento redatto dall'Assicurato; risarcimento ai sensi dell'Art. 2.3 entro il sottolimite dell'Art. 4.4.",
            "Il consulente tecnico riconosce l'omessa vigilanza del coordinatore sulle opere provvisionali; danno risarcito al netto della franchigia.",
        ],
        [
            "L'infortunio è imputabile esclusivamente all'impresa, che non ha applicato le misure previste dal piano di sicurezza.",
            "La richiesta riguarda sanzioni amministrative, escluse ai sensi dell'Art. 3.3.",
        ]),

        new(PerditaPatrimoniale, 0.05, 3_000m, 60_000m, 0.30,
        [
            Standard(PerditaPatrimoniale, "Per un errore nella pratica edilizia presentata dallo studio, il permesso di costruire di {Opera} è stato rilasciato con otto mesi di ritardo e il committente chiede i maggiori costi di finanziamento."),
            Standard(PerditaPatrimoniale, "Una variazione catastale errata predisposta dallo studio ha bloccato il rogito di vendita di un appartamento; l'acquirente ha rinunciato e il cliente chiede il mancato guadagno."),
            Standard(PerditaPatrimoniale, "{Committente} chiede il risarcimento per la perdita di un contributo pubblico: la domanda preparata dallo studio è stata presentata con documentazione incompleta."),
            Standard(PerditaPatrimoniale, "Un errore nel computo metrico ha portato a sottostimare i costi di {Opera}, e il committente ha dovuto sostenere maggiori oneri non previsti nel finanziamento."),
            Standard(PerditaPatrimoniale, "Per errori nella SCIA di {Opera} il Comune ha sospeso i lavori per due mesi e l'impresa ha chiesto al committente i costi di fermo cantiere."),
            Standard(PerditaPatrimoniale, "Lo studio ha indicato una destinazione d'uso errata nella pratica urbanistica: l'attività commerciale del cliente non ha potuto aprire per diversi mesi."),
            Respinto(PerditaPatrimoniale,
                "{Committente} chiede allo studio il pagamento delle penali contrattuali per il ritardo nella consegna degli elaborati esecutivi di {Opera}.",
                [
                    "La richiesta riguarda penali contrattuali per ritardata consegna, escluse ai sensi dell'Art. 3.6.",
                    "Il ritardo non deriva da un errore professionale ma dall'organizzazione dello studio: esclusione dell'Art. 3.6.",
                ],
                "Il ritardo è conseguenza diretta di un errore di progetto, da rifare: perdita patrimoniale in garanzia (Art. 2.4) con lo scoperto dell'Art. 4.2."),
            Respinto(PerditaPatrimoniale,
                "Il cliente addebita allo studio i danni per il ritardo nella consegna del progetto di {Opera}, che ha fatto slittare l'avvio dei lavori e l'apertura dell'attività.",
                [
                    "Danni da ritardo nella consegna degli elaborati, esclusi ai sensi dell'Art. 3.6.",
                    "Nessun errore professionale accertato: il pregiudizio deriva dal solo ritardo (Art. 3.6).",
                ],
                "Il ritardo è stato causato da un errore nella pratica urbanistica: perdita patrimoniale risarcita ai sensi dell'Art. 2.4."),
        ],
        [
            "Accertato l'errore nella pratica e il pregiudizio economico subito dal cliente; perdita patrimoniale risarcita ai sensi dell'Art. 2.4 con lo scoperto dell'Art. 4.2.",
            "Il consulente conferma il nesso tra l'errore dello studio e i maggiori oneri sostenuti dal committente; risarcimento entro i limiti dell'Art. 4.2.",
        ],
        [
            "Il pregiudizio lamentato deriva da ritardi e penali contrattuali (Art. 3.6).",
            "Il cliente non ha documentato la perdita economica lamentata; richiesta respinta.",
        ]),
    ];
}
