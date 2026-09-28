# Valutazione del retrieval delle clausole — 2026-09-28 16:09

| Parametro | Valore |
|---|---|
| Modello di embedding | `embeddinggemma` (ollama, 768 dimensioni) |
| Database | Sinistri |
| Modello di chat (informativo) | `qwen3.5:9b` |
| Casi del golden set | 15 |
| Ricerca pura | prime 5 per recall, prime 10 per il rank; nessuna clausola integrativa |
| Ricerca completa | top 5, integrative entro 0,700, franchigia di base Art. 4.1 |
| Durata | 0,7 s |

## Metriche

| Metrica | Valore | Obiettivo |
|---|---|---|
| recall@5 | **0,76** | ≥ 0,70 ✓ |
| MRR | 0,92 | informativa |
| hit@1 | 0,87 | informativa |
| recall esclusioni (ricerca completa) | 1,00 | informativa |

## Per caso

| Caso | Prodotto | recall@5 | Rank 1° rilevante | Prime 5 (✓ rilevante) | Mancanti | Esclusioni |
|---|---|---|---|---|---|---|
| G01 | CasaFabbricati | 1,00 | 1 | Art. 2.4 ✓, Art. 2.5 ✓, Art. 3.4 ✓, Art. 4.3 ✓, Art. 2.6 | — | 1,00 |
| G02 | CasaFabbricati | 0,60 | 1 | Art. 2.4 ✓, Art. 3.4 ✓, Art. 3.3, Art. 2.5 ✓, Art. 3.2 | Art. 2.9, Art. 4.3 | 1,00 |
| G03 | CasaFabbricati | 0,75 | 1 | Art. 3.1 ✓, Art. 3.2 ✓, Art. 2.2 ✓, Art. 2.5, Art. 2.4 | Art. 4.2 | 1,00 |
| G04 | CasaFabbricati | 0,75 | 1 | Art. 3.3 ✓, Art. 1.3 ✓, Art. 2.4 ✓, Art. 3.4, Art. 2.5 | Art. 4.3 | 1,00 |
| G05 | CasaFabbricati | 0,75 | 1 | Art. 3.6 ✓, Art. 2.3 ✓, Art. 2.2 ✓, Art. 2.10, Art. 2.8 | Art. 4.2 | 1,00 |
| G06 | CasaFabbricati | 1,00 | 1 | Art. 2.2 ✓, Art. 1.6 ✓, Art. 3.1 ✓, Art. 3.5, Art. 4.2 ✓ | — | 1,00 |
| G07 | CasaFabbricati | 1,00 | 1 | Art. 1.7 ✓, Art. 2.6 ✓, Art. 2.1, Art. 4.4 ✓, Art. 3.7 ✓ | — | 1,00 |
| G08 | CasaFabbricati | 0,33 | 1 | Art. 2.1 ✓, Art. 1.3, Art. 2.7, Art. 2.4, Art. 1.4 | Art. 2.11, Art. 4.1 | — |
| G09 | CasaFabbricati | 1,00 | 2 | Art. 3.8, Art. 1.8 ✓, Art. 2.8 ✓, Art. 2.7 ✓, Art. 4.5 ✓ | — | — |
| G10 | CasaFabbricati | 1,00 | 1 | Art. 3.8 ✓, Art. 2.7 ✓, Art. 1.8 ✓, Art. 4.5 ✓, Art. 2.6 | — | 1,00 |
| G11 | CasaFabbricati | 0,50 | 1 | Art. 2.10 ✓, Art. 2.2, Art. 2.8, Art. 3.6, Art. 2.3 | Art. 4.1 | — |
| G12 | RcProfTecnici | 0,75 | 1 | Art. 2.1 ✓, Art. 2.6 ✓, Art. 2.2, Art. 2.4, Art. 1.1 ✓ | Art. 4.1 | — |
| G13 | RcProfTecnici | 0,75 | 1 | Art. 2.2 ✓, Art. 2.6 ✓, Art. 2.1, Art. 1.1 ✓, Art. 2.3 | Art. 4.1 | — |
| G14 | RcProfTecnici | 0,75 | 1 | Art. 2.3 ✓, Art. 3.3 ✓, Art. 4.4 ✓, Art. 2.2, Art. 1.1 | Art. 4.1 | 1,00 |
| G15 | RcProfTecnici | 0,50 | 3 | Art. 1.1, Art. 2.1, Art. 3.4 ✓, Art. 1.3 ✓, Art. 2.6 | Art. 1.5, Art. 1.4 | 1,00 |

## Casi peggiori (3 recall più basse)

### G08 — recall@5 0,33

> La canna fumaria della stufa a legna ha preso fuoco: il fumo ha annerito il soggiorno e i vigili del fuoco hanno dovuto aprire il controsoffitto.

Attesi: Art. 2.1, Art. 2.11, Art. 4.1 (incendio da canna fumaria, con spese di demolizione e sgombero).

| # | Articolo | Tipo | Titolo | Distanza | Rilevante |
|---|---|---|---|---|---|
| 1 | Art. 2.1 | Garanzia | Incendio, fulmine, esplosione e scoppio | 0,666 | ✓ |
| 2 | Art. 1.3 | Definizione | Scoppio | 0,727 |  |
| 3 | Art. 2.7 | Garanzia | Furto e rapina del contenuto | 0,728 |  |
| 4 | Art. 2.4 | Garanzia | Acqua condotta | 0,737 |  |
| 5 | Art. 1.4 | Definizione | Esplosione | 0,738 |  |

Mancanti: Art. 2.11 al 6° posto (0,744); Art. 4.1 oltre il 10° posto.

### G15 — recall@5 0,50

> È arrivata la lettera dell'avvocato di un cliente per una ristrutturazione che l'architetto aveva progettato nel 2012, molto prima di assicurarsi con noi.

Attesi: Art. 1.5, Art. 1.4, Art. 3.4, Art. 1.3 (errore commesso prima del periodo di retroattività (claims made)).

| # | Articolo | Tipo | Titolo | Distanza | Rilevante |
|---|---|---|---|---|---|
| 1 | Art. 1.1 | Garanzia | Oggetto dell'assicurazione (RC professionale di ingegneri e architetti) | 0,657 |  |
| 2 | Art. 2.1 | Garanzia | Errori di progettazione e di calcolo | 0,664 |  |
| 3 | Art. 3.4 | Esclusione | Circostanze note prima della decorrenza | 0,679 | ✓ |
| 4 | Art. 1.3 | Definizione | Richiesta di risarcimento | 0,681 | ✓ |
| 5 | Art. 2.6 | Garanzia | Costi di rifacimento dell'opera | 0,682 |  |

Mancanti: Art. 1.5 oltre il 10° posto; Art. 1.4 al 9° posto (0,719).

### G11 — recall@5 0,50

> Mio figlio giocando a pallone in casa ha rotto il vetro della porta finestra del soggiorno e lo specchio dell'ingresso.

Attesi: Art. 2.10, Art. 4.1 (rottura accidentale di cristalli).

| # | Articolo | Tipo | Titolo | Distanza | Rilevante |
|---|---|---|---|---|---|
| 1 | Art. 2.10 | Garanzia | Cristalli | 0,584 | ✓ |
| 2 | Art. 2.2 | Garanzia | Eventi atmosferici | 0,649 |  |
| 3 | Art. 2.8 | Garanzia | Guasti cagionati dai ladri | 0,651 |  |
| 4 | Art. 3.6 | Esclusione | Grandine su serramenti, pannelli solari e fotovoltaici (salvo Art. 2.3) | 0,654 |  |
| 5 | Art. 2.3 | Garanzia | Grandine su serramenti, lastre e pannelli solari (estensione, operante solo se richiamata in polizza) | 0,663 |  |

Mancanti: Art. 4.1 oltre il 10° posto.
