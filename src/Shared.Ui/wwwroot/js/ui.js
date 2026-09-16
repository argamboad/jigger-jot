// Small DOM helpers the RCL's components call through IJSRuntime. Loaded by BOTH hosts' index.html
// (src/Web + src/Maui — the R68 parity gate holds the script sets identical).
(function () {
    'use strict';

    window.appUi = window.appUi || {};

    // Brings a chip in a sideways-scrolling row into view — horizontally, and only as far as needed, so
    // the page itself never jumps (Cocktails' scope chips on a phone, 2026-09-16).
    window.appUi.revealChip = function (el) {
        if (el && typeof el.scrollIntoView === 'function') {
            el.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
    };
})();
