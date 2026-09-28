// Pre-istruttoria con i passi in streaming (fase-8.md §3.1): il form va a /pre-istruttoria/stream della Web, che inoltra gli eventi
// dell'API. Eventi: "passo" {passo, secondi}, "fine" {url}, "errore" {titolo, dettaglio}. A fine stream la pagina carica la scheda
// dall'url ricevuto, disegnata da Razor. Senza questo script il form fa il POST classico.
(() => {
    const form = document.getElementById("form-pre-istruttoria");

    if (!form || !window.fetch || !window.TextDecoderStream) {
        return;
    }

    const pulsante = document.getElementById("genera");
    const pannello = document.getElementById("avanzamento");
    const elencoPassi = document.getElementById("avanzamento-passi");
    const tempo = document.getElementById("avanzamento-tempo");
    const errore = document.getElementById("errore-stream");

    const valore = (nome) => {
        const campo = form.elements[nome];
        const testo = campo ? campo.value.trim() : "";
        return testo === "" ? null : testo;
    };

    const richiesta = () => ({
        numeroPolizza: valore("Modulo.NumeroPolizza"),
        denuncia: valore("Modulo.Denuncia"),
        dataEvento: valore("Modulo.DataEvento"),
        causaIndicata: valore("Modulo.Causa"),
        riparatoreId: valore("Modulo.RiparatoreId") === null ? null : Number(valore("Modulo.RiparatoreId"))
    });

    // Una riga per passo: "…" quando parte, "✓ 0,4 s" quando finisce.
    const passo = ({ passo, secondi }) => {
        let riga = elencoPassi.querySelector(`[data-passo="${passo}"]`);

        if (!riga) {
            riga = document.createElement("li");
            riga.dataset.passo = passo;
            elencoPassi.appendChild(riga);
        }

        riga.textContent = secondi === null
            ? `… ${passo}`
            : `✓ ${passo} ${secondi.toLocaleString("it-IT", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} s`;
        riga.className = secondi === null ? "text-muted" : "text-success";
    };

    const mostraErrore = ({ titolo, dettaglio }) => {
        errore.textContent = `${titolo}: ${dettaglio}`;
        errore.hidden = false;
    };

    // Parser minimo di text/event-stream: blocchi separati da riga vuota, righe "event:" e "data:".
    async function* eventi(corpo) {
        const lettore = corpo.pipeThrough(new TextDecoderStream()).getReader();
        let buffer = "";

        while (true) {
            const { value, done } = await lettore.read();

            if (done) {
                return;
            }

            // Normalizzato sul buffer intero: un "\r\n" può arrivare spezzato tra due pezzi.
            buffer = (buffer + value).replace(/\r\n/g, "\n");
            let fine;

            while ((fine = buffer.indexOf("\n\n")) >= 0) {
                const blocco = buffer.slice(0, fine);
                buffer = buffer.slice(fine + 2);

                let tipo = "message";
                const dati = [];

                for (const riga of blocco.split("\n")) {
                    if (riga.startsWith("event:")) {
                        tipo = riga.slice(6).trim();
                    } else if (riga.startsWith("data:")) {
                        dati.push(riga.slice(5).trimStart());
                    }
                }

                if (dati.length > 0) {
                    yield { tipo, dato: JSON.parse(dati.join("\n")) };
                }
            }
        }
    }

    form.addEventListener("submit", async (evento) => {
        if (!form.checkValidity()) {
            return;
        }

        evento.preventDefault();

        pulsante.disabled = true;
        errore.hidden = true;
        elencoPassi.replaceChildren();
        pannello.hidden = false;
        document.getElementById("esito")?.remove();
        document.getElementById("errore")?.remove();

        const inizio = performance.now();
        const timer = setInterval(() => {
            tempo.textContent = `${Math.round((performance.now() - inizio) / 1000)} s`;
        }, 500);

        let concluso = false;

        try {
            const risposta = await fetch(form.dataset.stream, {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "text/event-stream" },
                body: JSON.stringify(richiesta())
            });

            if (!risposta.ok) {
                throw new Error(`${risposta.status} ${risposta.statusText}`);
            }

            for await (const { tipo, dato } of eventi(risposta.body)) {
                if (tipo === "passo") {
                    passo(dato);
                } else if (tipo === "fine") {
                    concluso = true;
                    window.location.assign(dato.url);
                    return;
                } else if (tipo === "errore") {
                    concluso = true;
                    mostraErrore(dato);
                    break;
                }
            }

            if (!concluso) {
                mostraErrore({ titolo: "Risposta incompleta", dettaglio: "la connessione si è chiusa prima della fine." });
            }
        } catch (eccezione) {
            mostraErrore({ titolo: "Richiesta non riuscita", dettaglio: eccezione.message });
        } finally {
            clearInterval(timer);

            if (!concluso || !errore.hidden) {
                pannello.hidden = true;
                pulsante.disabled = false;
            }
        }
    });
})();
