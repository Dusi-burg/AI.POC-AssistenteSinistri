// Pulsanti con data-dialog: aprono il <dialog> con quell'id (testo integrale di una clausola citata, fase-8.md §3.1).
// Il testo è già nella pagina: nessuna chiamata all'API.
document.addEventListener("click", (evento) => {
    const pulsante = evento.target.closest("[data-dialog]");
    const dialog = pulsante && document.getElementById(pulsante.dataset.dialog);

    if (dialog) {
        dialog.showModal();
    }
});

// Click sullo sfondo del dialog: chiude.
document.addEventListener("click", (evento) => {
    if (evento.target instanceof HTMLDialogElement) {
        evento.target.close();
    }
});
