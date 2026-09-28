-- Clausole di polizza del POC (fase-3.md §3a): testi FITTIZI, nessuna compagnia o condizione di polizza reale.
-- Rieseguibile a mano: sqlcmd -S "(localdb)\localdev" -E -d Sinistri -f 65001 -i db\003_seed_clausole.sql
-- (-f 65001 è obbligatorio: il file è UTF-8 e senza, sqlcmd lo legge con la code page di Windows e corrompe le lettere accentate)
-- MERGE su (ProdottoId, Articolo): se titolo, tipo o testo cambiano la riga si aggiorna e il suo Embedding torna NULL,
-- così basta "embed --solo-mancanti" senza ricreare il DB. Id delle lookup risolti per nome (righe inserite da DbInit).

SET NOCOUNT ON;

DECLARE @Casa TINYINT = (SELECT Id FROM dbo.Prodotto WHERE Name = 'CasaFabbricati');
DECLARE @Rc TINYINT = (SELECT Id FROM dbo.Prodotto WHERE Name = 'RcProfTecnici');
DECLARE @Definizione TINYINT = (SELECT Id FROM dbo.TipoClausola WHERE Name = 'Definizione');
DECLARE @Garanzia TINYINT = (SELECT Id FROM dbo.TipoClausola WHERE Name = 'Garanzia');
DECLARE @Esclusione TINYINT = (SELECT Id FROM dbo.TipoClausola WHERE Name = 'Esclusione');
DECLARE @Franchigia TINYINT = (SELECT Id FROM dbo.TipoClausola WHERE Name = 'Franchigia');

IF @Casa IS NULL OR @Rc IS NULL OR @Definizione IS NULL OR @Garanzia IS NULL OR @Esclusione IS NULL OR @Franchigia IS NULL
    THROW 50001, 'Lookup Prodotto o TipoClausola vuote: eseguire prima DbInit.', 1;

MERGE dbo.Clausola AS t
USING (VALUES
    -- ===================== CASA E FABBRICATI =====================
    (@Casa, N'Art. 1.1', @Definizione, N'Fabbricato',
     N'Per fabbricato si intende l''intera costruzione edile, o la porzione di essa indicata in polizza, comprese le opere murarie e di finitura, gli impianti fissi idrici, elettrici, termici e di condizionamento, i serramenti, le recinzioni e le pertinenze. Sono compresi i pannelli solari termici e fotovoltaici installati sul fabbricato. Sono esclusi il terreno, i giardini e il contenuto.'),
    (@Casa, N'Art. 1.2', @Definizione, N'Contenuto',
     N'Per contenuto si intendono i mobili, l''arredamento, gli elettrodomestici, gli apparecchi audiovisivi ed elettronici, gli indumenti e gli oggetti di uso domestico e personale che si trovano nell''abitazione assicurata. Sono compresi i preziosi e i valori entro i limiti indicati in polizza. Non sono considerati contenuto i veicoli a motore e le merci destinate alla vendita.'),
    (@Casa, N'Art. 1.3', @Definizione, N'Scoppio',
     N'Per scoppio si intende il repentino dirompersi di contenitori o tubazioni per eccesso di pressione interna di fluidi, non dovuto a esplosione. Gli effetti del gelo e del colpo d''ariete non sono considerati scoppio.'),
    (@Casa, N'Art. 1.4', @Definizione, N'Esplosione',
     N'Per esplosione si intende lo sviluppo di gas o vapori ad alta temperatura e pressione, dovuto a una reazione chimica che si autopropaga con elevata velocità. Rientra nella definizione l''esplosione di impianti a gas domestici.'),
    (@Casa, N'Art. 1.5', @Definizione, N'Acqua condotta',
     N'Per acqua condotta si intende l''acqua contenuta negli impianti idrici, igienico-sanitari, di riscaldamento e di condizionamento al servizio del fabbricato. Non è acqua condotta quella piovana, quella proveniente da falde o da corsi d''acqua e quella delle reti fognarie pubbliche.'),
    (@Casa, N'Art. 1.6', @Definizione, N'Eventi atmosferici',
     N'Per eventi atmosferici si intendono uragano, bufera, tempesta, grandine, vento e cose da esso trascinate, trombe d''aria e nubifragi, quando la violenza dell''evento sia riscontrabile su una pluralità di enti, assicurati o non, posti nella stessa zona. Non rientrano nella definizione le semplici piogge, le infiltrazioni e il gelo.'),
    (@Casa, N'Art. 1.7', @Definizione, N'Fenomeno elettrico',
     N'Per fenomeno elettrico si intendono le correnti, le scariche e gli altri fenomeni elettrici, comprese le sovratensioni dovute a fulmine o a disturbi della rete di distribuzione, che danneggiano macchine, apparecchi e impianti elettrici ed elettronici. Il danno deve essere provocato dal fenomeno elettrico e non da un guasto interno dell''apparecchio.'),
    (@Casa, N'Art. 1.8', @Definizione, N'Scasso ed effrazione',
     N'Per scasso si intende la forzatura, la rottura o la manomissione dei mezzi di chiusura dei locali, tale da lasciare tracce visibili e accertabili. È equiparato allo scasso l''uso di chiavi false o di grimaldelli, purché risulti dalla denuncia all''Autorità. L''introduzione attraverso porte o finestre lasciate aperte non costituisce scasso.'),
    (@Casa, N'Art. 1.9', @Definizione, N'Franchigia e scoperto',
     N'La franchigia è l''importo fisso, indicato in polizza, che per ogni sinistro rimane a carico dell''Assicurato. Lo scoperto è la percentuale del danno indennizzabile che rimane a carico dell''Assicurato, con il minimo eventualmente indicato. Quando sono previsti entrambi si applica l''importo più elevato.'),

    (@Casa, N'Art. 2.1', @Garanzia, N'Incendio, fulmine, esplosione e scoppio',
     N'La Società indennizza i danni materiali e diretti causati al fabbricato e al contenuto da incendio, fulmine, esplosione e scoppio, anche se avvenuti in fabbricati vicini. Sono compresi i danni da fumo, gas e vapori sprigionati a seguito di tali eventi. Sono inoltre indennizzati i guasti causati per ordine dell''Autorità allo scopo di impedire o arrestare l''incendio.'),
    (@Casa, N'Art. 2.2', @Garanzia, N'Eventi atmosferici',
     N'La Società indennizza i danni materiali e diretti al fabbricato causati da eventi atmosferici come definiti all''Art. 1.6. Sono compresi i danni da bagnamento verificatisi all''interno dei locali, purché l''acqua sia penetrata attraverso rotture o lesioni provocate al tetto, alle pareti o ai serramenti dalla violenza dell''evento. Per i danni causati dalla grandine a serramenti, lastre e pannelli valgono gli Art. 2.3 e 3.6. Il pagamento dell''indennizzo è effettuato previa detrazione dello scoperto previsto all''Art. 4.2.'),
    (@Casa, N'Art. 2.3', @Garanzia, N'Grandine su serramenti, lastre e pannelli solari (estensione, operante solo se richiamata in polizza)',
     N'L''estensione è operante solo se espressamente richiamata in polizza e pagato il relativo premio. La Società indennizza i danni materiali e diretti causati dalla grandine a serramenti, vetrate, lastre di fibrocemento o di materiale plastico e pannelli solari termici e fotovoltaici installati sul fabbricato. L''indennizzo è corrisposto entro il 20% della somma assicurata per il fabbricato, con lo scoperto previsto all''Art. 4.2.'),
    (@Casa, N'Art. 2.4', @Garanzia, N'Acqua condotta',
     N'La Società indennizza i danni materiali e diretti causati al fabbricato e al contenuto da fuoriuscita di acqua condotta, a seguito di rottura accidentale di tubazioni e impianti idrici, igienico-sanitari e di riscaldamento posti al servizio del fabbricato. Sono compresi i danni ai pavimenti, compresi i parquet, alle pareti, ai soffitti e ai controsoffitti dei locali interessati dall''acqua fuoriuscita, anche se situati a un piano diverso da quello della rottura. Sono compresi i danni causati dal gelo agli impianti, purché i locali siano riscaldati e abitati, fermo quanto previsto all''Art. 3.3. Il pagamento dell''indennizzo è effettuato previa detrazione della franchigia prevista all''Art. 4.3.'),
    (@Casa, N'Art. 2.5', @Garanzia, N'Ricerca e riparazione del guasto',
     N'In caso di danno indennizzabile ai sensi dell''Art. 2.4, la Società rimborsa le spese sostenute per ricercare e riparare la rottura che ha dato origine alla fuoriuscita di acqua, comprese quelle per demolire e ripristinare pavimenti, pareti e rivestimenti. Il rimborso avviene entro il limite previsto all''Art. 4.3. Non sono rimborsate le spese per la sostituzione dell''intero impianto o per interventi di adeguamento.'),
    (@Casa, N'Art. 2.6', @Garanzia, N'Fenomeno elettrico',
     N'La Società indennizza i danni materiali e diretti causati da fenomeno elettrico, come definito all''Art. 1.7, agli impianti elettrici ed elettronici del fabbricato e agli apparecchi elettrodomestici ed elettronici che fanno parte del contenuto. Sono compresi la caldaia, il quadro elettrico, gli impianti di allarme, l''inverter e gli altri componenti dell''impianto fotovoltaico. L''indennizzo è corrisposto con la franchigia ed entro il limite previsti all''Art. 4.4.'),
    (@Casa, N'Art. 2.7', @Garanzia, N'Furto e rapina del contenuto',
     N'La Società indennizza i danni materiali e diretti derivanti dal furto del contenuto, a condizione che l''autore si sia introdotto nei locali mediante scasso o effrazione dei mezzi di chiusura, come definiti all''Art. 1.8, oppure per una via diversa da quella ordinaria che richieda il superamento di ostacoli con mezzi artificiosi o agilità personale. È compresa la rapina avvenuta nei locali dell''abitazione. L''Assicurato deve presentare denuncia all''Autorità e trasmetterne copia alla Società. Si applica lo scoperto previsto all''Art. 4.5.'),
    (@Casa, N'Art. 2.8', @Garanzia, N'Guasti cagionati dai ladri',
     N'La Società indennizza i guasti causati dai ladri alle parti del fabbricato e ai mezzi di chiusura dei locali in occasione di furto o rapina, consumati o tentati. Sono comprese le spese per la sostituzione delle serrature e il ripristino di porte e finestre danneggiate.'),
    (@Casa, N'Art. 2.9', @Garanzia, N'Responsabilità civile della proprietà del fabbricato',
     N'La Società tiene indenne l''Assicurato di quanto sia tenuto a pagare, quale civilmente responsabile ai sensi di legge, a titolo di risarcimento per danni involontariamente cagionati a terzi, per morte, lesioni personali e danneggiamenti a cose, in conseguenza di un fatto accidentale verificatosi in relazione alla proprietà del fabbricato. Sono compresi i danni cagionati a terzi da spargimento d''acqua conseguente a rottura accidentale degli impianti. La garanzia opera entro il massimale indicato in polizza.'),
    (@Casa, N'Art. 2.10', @Garanzia, N'Cristalli',
     N'La Società indennizza le spese sostenute per sostituire lastre di cristallo, mezzo cristallo, specchi e vetri stabilmente collocati su porte, finestre, pareti e mobili, a seguito di rottura accidentale, compresa quella causata da terzi. Sono escluse le rigature, le scheggiature e le rotture dovute a lavori di manutenzione o a vizi di installazione.'),
    (@Casa, N'Art. 2.11', @Garanzia, N'Spese di demolizione e sgombero',
     N'In caso di sinistro indennizzabile, la Società rimborsa le spese necessarie per demolire, sgomberare e trasportare alla più vicina discarica autorizzata i residui del sinistro, entro il 10% dell''indennizzo liquidato. Sono escluse le spese relative a residui classificati come rifiuti tossici o nocivi.'),

    (@Casa, N'Art. 3.1', @Esclusione, N'Infiltrazioni dovute a mancata manutenzione',
     N'Sono esclusi i danni causati da infiltrazioni di acqua piovana o di acqua condotta dovute a mancata o insufficiente manutenzione del fabbricato, delle coperture, delle guaine, delle grondaie e dei pluviali. Sono altresì esclusi i danni conseguenti a infiltrazioni già note all''Assicurato prima del sinistro e non riparate. L''esclusione opera anche se il danno si manifesta a seguito di un evento atmosferico.'),
    (@Casa, N'Art. 3.2', @Esclusione, N'Umidità, stillicidio e condensa',
     N'Sono esclusi i danni causati da umidità, stillicidio, trasudamento e condensa, nonché le muffe e il deterioramento lento e graduale di intonaci, pavimenti e rivestimenti. L''esclusione si applica anche quando tali fenomeni sono conseguenza di una perdita occulta protratta nel tempo.'),
    (@Casa, N'Art. 3.3', @Esclusione, N'Gelo su impianti di locali non riscaldati o non in uso',
     N'Sono esclusi i danni da gelo agli impianti idrici e di riscaldamento posti in locali non riscaldati o non abitati per oltre 72 ore consecutive, salvo che gli impianti siano stati svuotati. L''esclusione si applica in particolare alle abitazioni secondarie o di villeggiatura lasciate senza riscaldamento nel periodo invernale.'),
    (@Casa, N'Art. 3.4', @Esclusione, N'Usura, corrosione e vetustà degli impianti',
     N'Sono esclusi i danni dovuti a usura, corrosione, incrostazione, logorio e vetustà degli impianti e delle tubazioni, nonché le spese per riparare o sostituire le parti deteriorate. Resta indennizzabile il danno causato dall''acqua fuoriuscita, se la rottura è accidentale e improvvisa, fermo quanto previsto all''Art. 3.1.'),
    (@Casa, N'Art. 3.5', @Esclusione, N'Fabbricati in costruzione o in ristrutturazione',
     N'Sono esclusi i danni ai fabbricati in corso di costruzione o di ristrutturazione che interessi le strutture portanti o il tetto, nonché ai fabbricati non ultimati o privi di serramenti. Sono esclusi i danni a cose che si trovano all''aperto o sotto tettoie.'),
    (@Casa, N'Art. 3.6', @Esclusione, N'Grandine su serramenti, pannelli solari e fotovoltaici (salvo Art. 2.3)',
     N'Salvo quanto previsto dall''estensione dell''Art. 2.3, se richiamata in polizza, sono esclusi i danni causati dalla grandine a serramenti, vetrate, lucernari, lastre di fibrocemento o di materiale plastico e pannelli solari termici e fotovoltaici. L''esclusione opera anche quando la grandinata costituisce evento atmosferico ai sensi dell''Art. 1.6.'),
    (@Casa, N'Art. 3.7', @Esclusione, N'Fenomeno elettrico: guasti meccanici, usura e apparecchi con oltre 10 anni',
     N'Relativamente alla garanzia fenomeno elettrico sono esclusi i danni dovuti a usura, difetti di fabbricazione, guasti meccanici o carenza di manutenzione, e quelli verificatisi durante operazioni di riparazione o montaggio. Sono esclusi i danni ad apparecchi con oltre 10 anni di vita dalla data di acquisto o di installazione, come caldaie, televisori ed elettrodomestici, anche quando il guasto si manifesta durante un temporale. Sono inoltre escluse lampade, fusibili e componenti soggetti a normale sostituzione.'),
    (@Casa, N'Art. 3.8', @Esclusione, N'Furto senza segni di scasso',
     N'Sono esclusi i furti avvenuti senza scasso o effrazione dei mezzi di chiusura, compresi quelli commessi attraverso porte o finestre lasciate aperte o non chiuse a chiave. Sono esclusi i furti commessi o agevolati con dolo o colpa grave da familiari conviventi, persone di servizio o altre persone che abbiano accesso ai locali.'),
    (@Casa, N'Art. 3.9', @Esclusione, N'Dolo del contraente o dell''assicurato',
     N'Sono esclusi i danni causati con dolo del Contraente, dell''Assicurato, dei loro familiari conviventi o dei rappresentanti legali. In caso di dichiarazioni false o reticenti sulle circostanze del sinistro l''Assicurato perde il diritto all''indennizzo.'),
    (@Casa, N'Art. 3.10', @Esclusione, N'Rigurgito di fognature e allagamenti da falda',
     N'Sono esclusi i danni causati da rigurgito o traboccamento della rete fognaria pubblica, da allagamenti, inondazioni e alluvioni e dall''innalzamento della falda freatica. Sono altresì esclusi i danni da acqua piovana penetrata da aperture lasciate aperte.'),

    (@Casa, N'Art. 4.1', @Franchigia, N'Franchigia frontale per sinistro',
     N'Salvo quanto diversamente previsto dagli articoli seguenti, il pagamento dell''indennizzo per ciascun sinistro è effettuato previa detrazione della franchigia indicata in polizza. La franchigia non si applica alle spese di demolizione e sgombero dell''Art. 2.11.'),
    (@Casa, N'Art. 4.2', @Franchigia, N'Scoperto eventi atmosferici (10%, minimo 500 €)',
     N'Per i danni da eventi atmosferici, compresi quelli rientranti nell''estensione dell''Art. 2.3, il pagamento dell''indennizzo è effettuato previa detrazione di uno scoperto del 10%, con il minimo di 500 euro per sinistro. In nessun caso la Società paga, per sinistro e per anno assicurativo, un importo superiore al 70% della somma assicurata per il fabbricato.'),
    (@Casa, N'Art. 4.3', @Franchigia, N'Franchigia acqua condotta e limite ricerca guasto (5.000 € per anno)',
     N'Per i danni da acqua condotta il pagamento dell''indennizzo è effettuato previa detrazione della franchigia indicata in polizza. Le spese di ricerca e riparazione del guasto dell''Art. 2.5 sono rimborsate fino a 5.000 euro per sinistro e per anno assicurativo, con una franchigia di 150 euro.'),
    (@Casa, N'Art. 4.4', @Franchigia, N'Limite fenomeno elettrico (6.000 € per anno, franchigia 150 €)',
     N'Per i danni da fenomeno elettrico la Società non paga, per sinistro e per anno assicurativo, un importo superiore a 6.000 euro. Il pagamento dell''indennizzo è effettuato previa detrazione di una franchigia di 150 euro per sinistro, o di quella indicata in polizza se più elevata.'),
    (@Casa, N'Art. 4.5', @Franchigia, N'Scoperto furto (20% in assenza di impianto antifurto)',
     N'Per i danni da furto e rapina il pagamento dell''indennizzo è effettuato previa detrazione di uno scoperto del 20% quando i locali non sono protetti da un impianto antifurto funzionante al momento del sinistro. Lo scoperto è ridotto al 10% se l''impianto è presente e attivo. Per preziosi e valori l''indennizzo non può superare il 30% della somma assicurata per il contenuto.'),

    -- ===================== RC PROFESSIONALE TECNICI =====================
    (@Rc, N'Art. 1.1', @Garanzia, N'Oggetto dell''assicurazione (RC professionale di ingegneri e architetti)',
     N'La Società tiene indenne l''Assicurato, ingegnere o architetto iscritto al relativo albo professionale, di quanto sia tenuto a pagare quale civilmente responsabile ai sensi di legge per danni involontariamente cagionati a terzi, compresi i committenti, nell''esercizio dell''attività professionale descritta in polizza. La garanzia comprende le perdite patrimoniali, i danni a persone e i danni a cose, entro il massimale indicato. L''assicurazione è prestata nella forma claims made dell''Art. 1.4.'),
    (@Rc, N'Art. 1.2', @Definizione, N'Sinistro',
     N'Per sinistro si intende la richiesta di risarcimento di danni per i quali è prestata l''assicurazione, pervenuta all''Assicurato per la prima volta nel periodo di efficacia della polizza. Più richieste originate dal medesimo errore professionale costituiscono un unico sinistro.'),
    (@Rc, N'Art. 1.3', @Definizione, N'Richiesta di risarcimento',
     N'Per richiesta di risarcimento si intende la comunicazione scritta con cui il terzo manifesta all''Assicurato l''intenzione di ritenerlo responsabile per danni causati da un errore professionale, oppure la citazione o la chiamata in causa dell''Assicurato. È equiparata la notifica di un accertamento tecnico preventivo.'),
    (@Rc, N'Art. 1.4', @Definizione, N'Claims made',
     N'L''assicurazione vale per le richieste di risarcimento presentate per la prima volta all''Assicurato durante il periodo di efficacia della polizza e da questi denunciate alla Società nello stesso periodo, a condizione che derivino da errori commessi non prima della data di retroattività dell''Art. 1.5. Ai fini della garanzia rileva la data della richiesta di risarcimento e non quella dell''errore o del danno.'),
    (@Rc, N'Art. 1.5', @Definizione, N'Retroattività',
     N'La garanzia opera per gli errori professionali commessi nei cinque anni precedenti la data di decorrenza della polizza, purché l''Assicurato non ne fosse a conoscenza alla stipula del contratto. Gli errori commessi prima del periodo di retroattività sono esclusi, anche se la richiesta di risarcimento perviene durante il periodo di efficacia.'),
    (@Rc, N'Art. 1.6', @Definizione, N'Perdite patrimoniali',
     N'Per perdite patrimoniali si intendono i pregiudizi economici subiti da terzi che non siano conseguenza di morte, lesioni personali o danneggiamenti a cose, quali i maggiori costi di costruzione, i mancati redditi e il fermo di attività.'),
    (@Rc, N'Art. 1.7', @Definizione, N'Garanzia postuma',
     N'In caso di cessazione definitiva dell''attività professionale, l''assicurazione vale per le richieste di risarcimento presentate entro dieci anni dalla cessazione, purché relative a errori commessi durante il periodo di efficacia della polizza o di retroattività. La garanzia postuma non opera in caso di sospensione o radiazione dall''albo.'),

    (@Rc, N'Art. 2.1', @Garanzia, N'Errori di progettazione e di calcolo',
     N'La garanzia comprende la responsabilità dell''Assicurato per errori od omissioni nella progettazione architettonica, strutturale e impiantistica, compresi gli errori nei calcoli statici e nel dimensionamento di solai, travi, pilastri e fondazioni. Sono compresi i danni che si manifestano dopo l''ultimazione dei lavori, nei termini dell''Art. 1.4.'),
    (@Rc, N'Art. 2.2', @Garanzia, N'Direzione lavori',
     N'La garanzia comprende la responsabilità dell''Assicurato nella funzione di direttore dei lavori, per omessa o insufficiente vigilanza sull''esecuzione delle opere in conformità al progetto, al capitolato e alle regole dell''arte. È compresa la responsabilità per la contabilità dei lavori e per l''accettazione dei materiali.'),
    (@Rc, N'Art. 2.3', @Garanzia, N'Coordinatore per la sicurezza (D.Lgs. 81/08)',
     N'La garanzia comprende la responsabilità dell''Assicurato nelle funzioni di coordinatore per la progettazione e per l''esecuzione dei lavori ai sensi del D.Lgs. 81/2008, per danni a persone e a cose derivanti da carenze del piano di sicurezza e coordinamento o dalla mancata vigilanza sulla sua applicazione. Sono comprese le azioni di rivalsa dell''INAIL. La garanzia opera entro il sottolimite dell''Art. 4.4.'),
    (@Rc, N'Art. 2.4', @Garanzia, N'Perdite patrimoniali cagionate a terzi',
     N'La garanzia comprende le perdite patrimoniali, come definite all''Art. 1.6, cagionate a terzi in conseguenza di errori professionali, quali ritardi nel rilascio di titoli abilitativi, errori nelle pratiche catastali o urbanistiche e maggiori oneri sostenuti dal committente. Si applica lo scoperto previsto all''Art. 4.2.'),
    (@Rc, N'Art. 2.5', @Garanzia, N'Spese legali e di resistenza',
     N'La Società assume, fino a quando ne ha interesse, la gestione delle vertenze in sede stragiudiziale e giudiziale a nome dell''Assicurato, designando legali e tecnici. Sono a carico della Società le spese di resistenza entro il limite di un quarto del massimale. Non sono rimborsate le spese per legali o tecnici non designati dalla Società.'),
    (@Rc, N'Art. 2.6', @Garanzia, N'Costi di rifacimento dell''opera',
     N'La garanzia comprende i costi sostenuti dal committente per demolire, rifare o consolidare parti dell''opera, quando si rende necessario a causa di un errore di progettazione o di direzione lavori imputabile all''Assicurato. Sono esclusi i costi che il committente avrebbe comunque sostenuto per una corretta esecuzione dell''opera.'),

    (@Rc, N'Art. 3.1', @Esclusione, N'Dolo dell''assicurato',
     N'Sono esclusi i danni derivanti da atti dolosi dell''Assicurato o da violazioni consapevoli di leggi, regolamenti e norme tecniche. È altresì esclusa la responsabilità per incarichi assunti in conflitto di interessi.'),
    (@Rc, N'Art. 3.2', @Esclusione, N'Attività per cui l''assicurato non è abilitato',
     N'Sono esclusi i danni derivanti da attività per le quali l''Assicurato non è abilitato ai sensi di legge o dell''ordinamento professionale, o svolte durante periodi di sospensione dall''albo.'),
    (@Rc, N'Art. 3.3', @Esclusione, N'Multe, ammende e sanzioni',
     N'Sono escluse le multe, le ammende e le sanzioni amministrative e pecuniarie inflitte all''Assicurato o ai committenti, anche se conseguenti a violazioni in materia di sicurezza nei cantieri. Sono esclusi i danni di natura punitiva o esemplare.'),
    (@Rc, N'Art. 3.4', @Esclusione, N'Circostanze note prima della decorrenza',
     N'Sono escluse le richieste di risarcimento derivanti da fatti o circostanze di cui l''Assicurato era a conoscenza prima della decorrenza della polizza e che potevano ragionevolmente dar luogo a una richiesta, anche se non ancora formalizzata. Rientrano tra queste le contestazioni scritte del committente, le riserve iscritte in contabilità e le segnalazioni di difetti già pervenute.'),
    (@Rc, N'Art. 3.5', @Esclusione, N'Collaudo di opere progettate o dirette dall''assicurato',
     N'Sono esclusi i danni derivanti dall''attività di collaudo statico o tecnico-amministrativo di opere progettate o dirette dall''Assicurato stesso o da professionisti a lui associati. Sono esclusi i danni derivanti da attività di collaudo svolte senza incarico formale.'),
    (@Rc, N'Art. 3.6', @Esclusione, N'Penali contrattuali e ritardi nella consegna',
     N'Sono esclusi gli importi dovuti a titolo di penali contrattuali, nonché i danni derivanti da ritardi nella consegna degli elaborati o nel rispetto dei termini contrattuali, salvo che il ritardo sia conseguenza diretta di un errore professionale indennizzabile.'),
    (@Rc, N'Art. 3.7', @Esclusione, N'Danni da inquinamento',
     N'Sono esclusi i danni derivanti da inquinamento dell''aria, dell''acqua o del suolo, da interruzione o alterazione di falde acquifere e da contaminazione da amianto o da altre sostanze nocive.'),
    (@Rc, N'Art. 3.8', @Esclusione, N'Attività svolte all''estero',
     N'Sono esclusi i danni derivanti da prestazioni professionali relative a opere situate fuori dal territorio della Repubblica Italiana, della Repubblica di San Marino e dello Stato della Città del Vaticano.'),

    (@Rc, N'Art. 4.1', @Franchigia, N'Franchigia fissa per sinistro',
     N'Il pagamento del risarcimento per ciascun sinistro è effettuato previa detrazione della franchigia fissa indicata in polizza. La franchigia si applica anche alle spese di resistenza quando la vertenza si conclude con un risarcimento.'),
    (@Rc, N'Art. 4.2', @Franchigia, N'Scoperto sulle perdite patrimoniali',
     N'Per le perdite patrimoniali dell''Art. 2.4 il pagamento è effettuato previa detrazione di uno scoperto del 10%, con il minimo pari alla franchigia fissa indicata in polizza. Il risarcimento per perdite patrimoniali non può superare il 50% del massimale.'),
    (@Rc, N'Art. 4.3', @Franchigia, N'Massimale per sinistro e per anno assicurativo',
     N'Il massimale indicato in polizza è il limite massimo di risarcimento per ciascun sinistro e per l''insieme dei sinistri denunciati nel medesimo anno assicurativo, qualunque sia il numero dei terzi danneggiati. I sottolimiti previsti dagli altri articoli fanno parte del massimale e non si aggiungono a esso.'),
    (@Rc, N'Art. 4.4', @Franchigia, N'Sottolimite per l''attività di coordinatore della sicurezza',
     N'Per la responsabilità derivante dall''attività di coordinatore per la sicurezza dell''Art. 2.3 il risarcimento non può superare 250.000 euro per sinistro e per anno assicurativo. Resta ferma la franchigia fissa prevista all''Art. 4.1.')
) AS s (ProdottoId, Articolo, TipoClausolaId, Titolo, Testo)
ON t.ProdottoId = s.ProdottoId AND t.Articolo = s.Articolo
WHEN MATCHED AND (t.Testo <> s.Testo OR t.Titolo <> s.Titolo OR t.TipoClausolaId <> s.TipoClausolaId)
    THEN UPDATE SET Titolo = s.Titolo, Testo = s.Testo, TipoClausolaId = s.TipoClausolaId, Embedding = NULL
WHEN NOT MATCHED THEN
    INSERT (ProdottoId, Articolo, TipoClausolaId, Titolo, Testo) VALUES (s.ProdottoId, s.Articolo, s.TipoClausolaId, s.Titolo, s.Testo);
GO
