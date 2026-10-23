/*
    Dialog focus management, for every [role="dialog"][aria-modal="true"]
    in the app: Modal.razor, ConfirmDialog.razor, DsDrawer, the command
    palette, and the dialogs pages draw by hand.

    aria-modal tells a screen reader that nothing outside the dialog exists.
    That promise has to hold for the keyboard too, so:

      1. Tab and Shift+Tab cycle inside the top-most open dialog and never
         reach the page underneath it.
      2. When a dialog closes, focus returns to the control that opened it,
         instead of falling to <body> and sending a keyboard user back to
         the top of the page.

    It works from the DOM rather than from each component, because Blazor
    adds and removes these dialogs as plain markup. Nothing to call.
*/
(function () {
    'use strict';

    var DIALOG = '[role="dialog"][aria-modal="true"], [role="alertdialog"][aria-modal="true"]';
    var FOCUSABLE = [
        'a[href]', 'area[href]', 'button:not([disabled])',
        'input:not([disabled]):not([type="hidden"])', 'select:not([disabled])',
        'textarea:not([disabled])', 'iframe', 'summary',
        '[contenteditable="true"]', '[tabindex]:not([tabindex="-1"])'
    ].join(',');

    /* Dialog element -> the element that had focus before it opened. */
    var openers = new Map();

    function isVisible(el) {
        return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length) &&
            getComputedStyle(el).visibility !== 'hidden';
    }

    function topDialog() {
        var all = document.querySelectorAll(DIALOG);
        for (var i = all.length - 1; i >= 0; i--) {
            if (isVisible(all[i])) return all[i];
        }
        return null;
    }

    function focusables(dialog) {
        return Array.prototype.filter.call(dialog.querySelectorAll(FOCUSABLE), function (el) {
            return isVisible(el) && !el.closest('[inert]');
        });
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Tab') return;
        var dialog = topDialog();
        if (!dialog) return;

        var items = focusables(dialog);
        if (items.length === 0) {
            e.preventDefault();
            dialog.focus();
            return;
        }

        var first = items[0];
        var last = items[items.length - 1];
        var active = document.activeElement;
        var inside = dialog.contains(active);

        if (e.shiftKey && (!inside || active === first || active === dialog)) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && (!inside || active === last)) {
            e.preventDefault();
            first.focus();
        }
    }, true);

    function collect(node, list) {
        if (node.nodeType !== 1) return;
        if (node.matches(DIALOG)) list.push(node);
        var nested = node.querySelectorAll(DIALOG);
        for (var i = 0; i < nested.length; i++) list.push(nested[i]);
    }

    /* MutationObserver callbacks run before Blazor's OnAfterRender interop
       moves focus into the panel, so activeElement is still the trigger. */
    new MutationObserver(function (records) {
        records.forEach(function (r) {
            var added = [];
            r.addedNodes.forEach(function (n) { collect(n, added); });
            added.forEach(function (d) {
                var a = document.activeElement;
                if (a && a !== document.body && !d.contains(a)) openers.set(d, a);
            });

            var removed = [];
            r.removedNodes.forEach(function (n) { collect(n, removed); });
            removed.forEach(function (d) {
                var opener = openers.get(d);
                openers.delete(d);
                if (!opener || !opener.isConnected) return;
                /* Only restore when focus was lost with the dialog: if the
                   close action navigated or focused something on purpose,
                   leave it alone. */
                requestAnimationFrame(function () {
                    var a = document.activeElement;
                    if (!a || a === document.body || !a.isConnected) {
                        opener.focus({ preventScroll: true });
                    }
                });
            });
        });
    }).observe(document.documentElement, { childList: true, subtree: true });
})();

/*
    The top bar's "more" menu is a native <details>: it opens and closes by
    itself but knows nothing of Escape or clicks elsewhere. This adds both
    ways out, and returns focus to the summary on Escape.
*/
(function () {
    'use strict';

    function openMenus() {
        return document.querySelectorAll('details.hr-top-more[open]');
    }

    document.addEventListener('click', function (e) {
        openMenus().forEach(function (d) {
            if (!d.contains(e.target)) d.removeAttribute('open');
        });
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        openMenus().forEach(function (d) {
            var hadFocus = d.contains(document.activeElement);
            d.removeAttribute('open');
            var summary = d.querySelector('summary');
            if (summary && hadFocus) summary.focus();
        });
    });

    document.addEventListener('blazor:enhancedload', function () {
        openMenus().forEach(function (d) { d.removeAttribute('open'); });
    });
})();

/*
    Tabs responsive smooth centering & touch/drag slider helper
*/
(function () {
    'use strict';

    window.hrScrollActiveTabIntoView = function () {
        requestAnimationFrame(function () {
            var active = document.querySelector('.hr-tabs-nav .hr-tab.is-active, .hr-tabs .hr-tab.is-active, .hr-tab[aria-selected="true"]');
            if (active) {
                active.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
            }
        });
    };

    document.addEventListener('click', function (e) {
        var tab = e.target.closest('.hr-tab');
        if (tab) {
            setTimeout(function () {
                tab.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
            }, 60);
        }
    });

    // Drag-to-scroll for horizontal tabs slider
    var isDown = false;
    var startX = 0;
    var scrollLeft = 0;
    var activeSlider = null;

    document.addEventListener('mousedown', function (e) {
        var slider = e.target.closest('.hr-tabs-nav');
        if (!slider) return;
        isDown = true;
        activeSlider = slider;
        startX = e.pageX - slider.offsetLeft;
        scrollLeft = slider.scrollLeft;
    });

    document.addEventListener('mouseleave', function () {
        isDown = false;
        activeSlider = null;
    });

    document.addEventListener('mouseup', function () {
        isDown = false;
        activeSlider = null;
    });

    document.addEventListener('mousemove', function (e) {
        if (!isDown || !activeSlider) return;
        var x = e.pageX - activeSlider.offsetLeft;
        var walk = (x - startX);
        if (Math.abs(walk) > 3) {
            e.preventDefault();
            activeSlider.scrollLeft = scrollLeft - walk;
        }
    });
})();


