# Demo — preparazione, scenari e sequenza

> I dati demo nascono da una tabella unica, `src/Dusiburg.AI.Sinistri.Core/Demo/DemoCatalog.cs`: da lì partono il seed (polizze demo),
> i pulsanti della Web e i test `DemoCatalogTests`. Tutti i dati sono sintetici e fittizi.

## Preparazione

```powershell
ollama list                                                   # qwen3.5:9b e embeddinggemma presenti
dotnet run --project tools/Dusiburg.AI.Sinistri.DbInit        # ricrea Sinistri su (localdb)\localdev con i dati demo (~5 s)
dotnet run --project src/Dusiburg.AI.Sinistri.Cli -- embed      # 880 vettori su CPU (~30 s)
dotnet run --project src/Dusiburg.AI.Sinistri.AppHost           # api + web + dashboard
```

- Dal dashboard di Aspire si apre la Web (`http://localhost:5202`). `api` e `web` devono essere sane; la pagina **Stato** della Web
  deve essere tutta verde, con l'embedding su CPU e la chat su GPU nel controllo "Posizionamento modelli".
- L'API fa un **warm-up** all'avvio (un embedding e una risposta minima): il primo utente non aspetta il caricamento del modello di
  chat. Conviene aspettare una decina di secondi dopo l'avvio.
- `DbInit` riscrive `data/duplicati_attesi.json`: la numerazione dei sinistri dipende dalla data del seed, quindi il file va sempre
  rigenerato insieme al DB.
- Dopo ogni modifica allo schema va rieseguito `DbInit` (poi `embed`): non ci sono migration.
- Le richieste HTTP pronte sono in `src/Dusiburg.AI.Sinistri.Api/Api.http`.

### Manopole della demo

Si impostano **sull'AppHost** e arrivano all'API (elenco completo e default in [`architettura.md`](architettura.md) §11):

```powershell
dotnet user-secrets set OLLAMA_CHAT_MODEL qwen3.5:9b --project src/Dusiburg.AI.Sinistri.AppHost
dotnet run --project src/Dusiburg.AI.Sinistri.AppHost -- SOGLIA_DUPLICATO_DENUNCIA=0.10
```

Utili in demo: `SOGLIA_DUPLICATO_DENUNCIA` e `SOGLIA_DUPLICATO_COSINE` (più o meno segnalazioni), `SINISTRI_PROMPT_CAPTURE_DIR`
(per mostrare il prompt reale inviato al modello), `OLLAMA_ENDPOINT` su una porta sbagliata (per mostrare cosa succede se Ollama è giù).

## Polizze demo

| Numero | Prodotto | Contraente | Validità |
|---|---|---|---|
| `CF-DEMO-000001` | Casa e fabbricati | Mario Bianchi (MI) | da 1 anno fa a fra 2 anni |
| `CF-DEMO-000002` | Casa e fabbricati | Laura Verdi (BO) | da 2 anni fa a fra 1 anno |
| `RP-DEMO-000001` | RC professionale tecnici | Studio Tecnico Ing. Neri (VR) | da 3 anni fa a fra 1 anno |

Le date sono relative al giorno del seed: le polizze demo sono sempre in vigore.

## Scenari

| # | Pagina | Testo | Cosa mostrare |
|---|---|---|---|
| 1 | Pre-istruttoria | *Rottura di un tubo nel bagno del piano superiore, danni a parquet e controsoffitto del soggiorno.* | garanzie 2.4 e 2.5, franchigia 4.3, esclusioni da verificare |
| 2 | Pre-istruttoria | *Il cliente dice che la grandine ha rotto i pannelli solari sul tetto.* | tensione fra eventi atmosferici (2.2), estensione 2.3 ed esclusione 3.6; punto da chiarire |
| 3 | Pre-istruttoria (`RP-DEMO-000001`) | *Un ingegnere ha sbagliato il calcolo di un solaio e il committente chiede i danni per il rifacimento.* | garanzie 2.1 e 2.6, franchigia 4.1, claims made |
| 4 | Pre-istruttoria | *Dopo un temporale si è bruciata la caldaia e il televisore.* | fenomeno elettrico 2.6, esclusione 3.7 (età degli apparecchi), limite 4.4 |
| 5 | Storico | *sovratensione ha danneggiato il quadro elettrico e l'inverter* — fenomeno elettrico, MI, liquidato ≥ 5.000 € | ricerca ibrida: filtri e distanza in una query, statistiche da SQL |
| 6 | Antifrode | ultimi 12 mesi | coppie con stesso contraente o riparatore, precision/recall contro le coppie attese |

Riformulazione per il riquadro antifrode, sulla polizza `CF-DEMO-000001`:
*Durante un forte temporale è andata via la corrente e quando è tornata il quadro elettrico era bruciato; si sono rovinati anche
l'inverter, la caldaia e il cancello automatico.* → segnala `SIN-2026-000024`, "stesso contraente" (il numero può cambiare con la
data del seed).

## Sequenza per una demo di 15 minuti

1. **Stato** (1 min): tutto verde; modelli locali, embedding su CPU e chat su GPU; conteggi dei dati sintetici.
2. **Clausole** (2 min): scenario 2 nella ricerca clausole. Le prime per distanza più l'esclusione 3.6 "integrativa": il modello vede
   anche ciò che limita la copertura. Un clic sull'articolo apre il testo.
3. **Pre-istruttoria, scenario 1** (4 min): i passi compaiono uno alla volta (embedding, SQL, modello, antifrode). Nella scheda:
   - le citazioni si verificano con un clic;
   - le statistiche sono "calcolate dal database, non dal modello";
   - gli avvisi mostrano cosa ha corretto la validazione;
   - **Apri la traccia nel dashboard**: la stessa richiesta come traccia unica, dal browser all'API, a SQL, al modello.
4. **Pre-istruttoria, riformulazione** (2 min): il riquadro "Possibili duplicati" segnala il sinistro originale dello stesso
   contraente; il controllo è fuori dal prompt.
5. **Storico, scenario 5** (2 min): filtri e distanza nella stessa query; la mediana del liquidato arriva da SQL.
6. **Antifrode, scenario 6** (2 min): coppie con legame e tabella precision/recall; spiegare perché le coppie "solo testo" sono tante
   (dati a template) e perché la soglia è 0,05.
7. **Chiusura** (2 min): scarica il Markdown della scheda; eventualmente `OLLAMA_ENDPOINT` sbagliato → indicatore rosso e messaggio chiaro.

## Dove guardare nel dashboard

- **Tracce**: una pre-istruttoria è una traccia unica con gli span `polizza`, `embedding`, `clausole`, `storico`, `statistiche`,
  `generazione scheda` (con lo span della chiamata al modello e i token), `validazione`, `antifrode`.
- **Log strutturati** dell'API: errori mappati in `ProblemDetails`, warm-up, eventuali retry del parsing.
- **Risorse**: `api` diventa rossa se SQL o Ollama non rispondono (health check con i controlli di `health`).
