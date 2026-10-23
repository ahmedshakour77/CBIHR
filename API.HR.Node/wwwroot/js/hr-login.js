/* Sign-in page behaviour. The Identity pages render statically (cookie sign-in has to
   write response headers, which a live Blazor circuit has already flushed), so these
   view-only touches run here instead of in @onclick handlers. Kept in a file, not
   inline, so the page needs no inline script. Labels come from data attributes on the
   page, so they follow its language. */
(function () {
    'use strict';

    var root = document.getElementById('loginRoot');
    if (!root) { return; }

    /* Password reveal. Focus returns to the field so the caret does not jump to the
       end of what the user was mid-way through typing. */
    root.addEventListener('click', function (e) {
        var btn = e.target.closest ? e.target.closest('[data-toggle-password]') : null;
        if (!btn) { return; }

        var field = document.getElementById(btn.getAttribute('aria-controls'));
        if (!field) { return; }

        var reveal = field.getAttribute('type') === 'password';
        field.setAttribute('type', reveal ? 'text' : 'password');
        btn.setAttribute('aria-pressed', String(reveal));

        var label = btn.getAttribute(reveal ? 'data-label-hide' : 'data-label-show');
        if (label) {
            btn.setAttribute('aria-label', label);
            btn.setAttribute('title', label);
        }

        field.focus();
    });

    /* An OAuth hop or a directory round trip should never look like a dead click. The
       spinner swaps in for the idle glyph rather than being added beside it, so the
       button keeps its width. */
    root.addEventListener('submit', function (e) {
        var action = e.target.querySelector('[data-submit]');
        if (!action || action.getAttribute('data-busy') === '1') { return; }
        action.setAttribute('data-busy', '1');
        action.setAttribute('aria-busy', 'true');
    });

    /* Coming back through the bfcache must not leave a button spinning. */
    window.addEventListener('pageshow', function () {
        var busy = root.querySelectorAll('[data-busy="1"]');
        for (var i = 0; i < busy.length; i++) {
            busy[i].removeAttribute('data-busy');
            busy[i].removeAttribute('aria-busy');
        }
    });
})();
