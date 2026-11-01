// The page-level busy signal for Bruinet: <body data-busy>. The layout calls set(false) on its first
// interactive render, so the attribute also says "the app is interactive".
window.bruinetBusy = {
    set: function (busy) {
        document.body.dataset.busy = busy ? "true" : "false";
    }
};
