# Il progetto in breve

> Per chi non sviluppa software: che cosa fa l'assistente, come lo fa e perché ci si può fidare del risultato. I dettagli tecnici sono
> in [`architettura.md`](architettura.md).

## In una frase

Un liquidatore incolla il testo di una denuncia di sinistro e il numero di polizza; in una ventina di secondi riceve una **scheda di
pre-istruttoria** con le garanzie che potrebbero operare, le esclusioni da verificare, la franchigia, le domande da fare al cliente, i
casi simili già liquidati e gli eventuali sospetti di duplicato. Tutto gira su un portatile, senza servizi esterni.

## Il problema che risolve

Chi apre una pratica deve:

- rileggere le condizioni di polizza per capire quali articoli c'entrano;
- ricordarsi come sono finiti casi simili, e con quali importi;
- accorgersi se lo stesso danno è già stato denunciato.

Sono tre lavori di ricerca, prima ancora della valutazione vera e propria. L'assistente li prepara; la valutazione resta del liquidatore.

## Come funziona

1. **Trova gli articoli giusti.** Le clausole di polizza sono state trasformate in "impronte numeriche" del loro significato
   (vettori). Anche la denuncia diventa un'impronta, e il sistema cerca le clausole con l'impronta più vicina: trova "Acqua condotta"
   anche se il cliente ha scritto "si è staccato il tubo della lavastoviglie". Aggiunge sempre l'esclusione e la franchigia più
   vicine, perché conta anche ciò che *limita* la copertura.
2. **Trova i casi simili.** Nello storico cerca i sinistri più simili, con i filtri che servono (prodotto, provincia, importo), e
   calcola quanti sono stati respinti e quanto si è liquidato: minimo, mediana, massimo.
3. **Scrive la scheda.** Un modello linguistico, che gira sul portatile, riceve la denuncia, le clausole trovate e i casi simili, e
   compila la scheda in un formato fisso.
4. **Controlla la scheda.** Il programma verifica che ogni articolo citato sia davvero tra quelli trovati e che gli importi scritti
   esistano: ciò che non torna viene tolto o segnalato.
5. **Cerca i duplicati.** Confronta la denuncia con i sinistri degli ultimi due anni e segnala quelli quasi uguali, dicendo se hanno
   lo stesso contraente o lo stesso riparatore.

## Perché non ci si fida ciecamente dell'intelligenza artificiale

- **I numeri non li calcola il modello.** Statistiche e importi arrivano dal database; al modello si chiede espressamente di non fare
  conti, e un controllo segnala gli importi che non trova nei dati.
- **Le citazioni si verificano.** Ogni articolo nella scheda si apre con un clic e mostra il testo integrale della clausola. Gli
  articoli inventati vengono tolti prima che il liquidatore li veda.
- **I sospetti di frode non passano dal modello.** Il controllo dei duplicati è una regola fissa, con una soglia misurata: il modello
  non "ragiona" su un indizio statistico.
- **La qualità si misura.** Quindici denunce di prova, scritte a mano, dicono quanto spesso il sistema trova gli articoli giusti: oggi
  il 76% degli articoli attesi è tra i primi cinque trovati, e quasi sempre il primo è quello giusto.

## Un esempio

> *Dopo un temporale si è bruciata la caldaia e il televisore.*

La scheda indica la garanzia per fenomeno elettrico, chiede di verificare l'età degli apparecchi (oltre dieci anni sono esclusi),
applica il limite annuo e la franchigia dell'articolo dedicato, mostra i dieci casi più simili con la mediana del liquidato e
segnala che la polizza è in vigore. Ogni articolo si apre con un clic.

## Che cosa dimostra

- Che un assistente utile si può far girare **in locale**, su un portatile con una scheda grafica da 8 GB, con dati che non escono
  dall'azienda.
- Che la ricerca "per significato" e la ricerca classica con i filtri convivono **nella stessa interrogazione** del database.
- Che il modello si può tenere al suo posto: scrive, mentre numeri, citazioni e sospetti li controlla il programma.

## Che cosa non è

- Non decide se un sinistro va pagato: prepara la pratica.
- Non usa dati reali: polizze, persone e sinistri sono inventati da un generatore.
- Non è un prodotto: è una prova di fattibilità, senza utenti, permessi né installazione su server.
