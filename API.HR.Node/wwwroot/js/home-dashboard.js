(function () {
    'use strict';

    window.hrDashboard = {
        reveal: function (heading) {
            if (!heading) return;
            heading.focus({ preventScroll: true });
            var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches ||
                document.documentElement.dataset.reducedMotion === 'true';
            heading.scrollIntoView({ block: 'start', behavior: reduceMotion ? 'auto' : 'smooth' });
        },
        // Chapter links move focus to the heading and scroll it clear of the sticky filter bar.
        // The bar's height changes with "all fields" and its chips, so the offset is measured
        // now rather than trusted to the stylesheet's estimate.
        jump: function (id) {
            var section = document.getElementById(id);
            if (!section) return;
            var heading = section.querySelector("h2");
            if (!heading) { section.scrollIntoView({ block: "start" }); return; }
            var bar = document.querySelector(".home-filter");
            if (bar) {
                var style = getComputedStyle(bar);
                if (style.position === "sticky") {
                    var top = parseFloat(style.top) || 0;
                    heading.style.scrollMarginTop = Math.ceil(top + bar.getBoundingClientRect().height + 16) + "px";
                }
            }
            heading.setAttribute("tabindex", "-1");
            window.hrDashboard.reveal(heading);
            history.replaceState(null, "", "#" + id);
        },
        downloadCsv: function (base64, filename) {
            var raw = atob(base64);
            var bytes = Uint8Array.from(raw, function (c) { return c.charCodeAt(0); });
            var url = URL.createObjectURL(new Blob([bytes], { type: 'text/csv;charset=utf-8' }));
            var link = document.createElement('a');
            link.href = url;
            link.download = filename;
            document.body.appendChild(link);
            try { link.click(); }
            finally {
                link.remove();
                setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
            }
        }
    };
})();
