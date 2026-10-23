/*
   Command palette helpers.
   - Ctrl+K / Cmd+K opens it from anywhere by clicking its trigger, so the
     same Blazor handler runs as for a mouse click.
   - hrCmd.reveal keeps the highlighted option in view while arrowing.
   - hrCmd.logout submits the shell's own logout form: logout is a POST
     with an antiforgery token, so a plain navigation cannot do it.
*/
(function () {
    'use strict';

    document.addEventListener('keydown', function (e) {
        if (e.repeat) return;
        if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 'k' || e.key === 'K')) {
            var btn = document.querySelector('[data-cmd-palette-trigger]');
            if (!btn) return;
            e.preventDefault();
            btn.click();
        }
    });

    window.hrCmd = {
        reveal: function (id) {
            var el = document.getElementById(id);
            if (el) el.scrollIntoView({ block: 'nearest' });
        },
        logout: function () {
            var form = document.getElementById('hr-logout-form') ||
                document.querySelector('form[action="Account/Logout"]');
            if (!form) return;
            if (form.requestSubmit) form.requestSubmit();
            else form.submit();
        }
    };
})();
