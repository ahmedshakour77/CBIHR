/*
   Language control (Arabic <-> Kurdish Sorani). English is withdrawn for now.
   Persists in cookie 'hr-lang' (for server SSR) and localStorage (for client).
   Writes lang/dir on <html>; a language change reloads the page.
*/
(function () {
    'use strict';

    var KEY = 'hr-lang';
    var root = document.documentElement;

    function getCookie(name) {
        var match = document.cookie.match(new RegExp('(^|;\\s*)' + name + '=([^;]+)'));
        return match ? decodeURIComponent(match[2]) : null;
    }

    function setCookie(name, value, days) {
        var expires = '';
        if (days) {
            var date = new Date();
            date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
            expires = '; expires=' + date.toUTCString();
        }
        document.cookie = name + '=' + encodeURIComponent(value || '') + expires + '; path=/; SameSite=Lax';
    }

    function stored() {
        try {
            return getCookie(KEY) || localStorage.getItem(KEY);
        } catch (e) {
            return null;
        }
    }

    // English is withdrawn until the interface is translated (see
    // LanguageService.EnglishEnabled); a stored 'en' falls back to Arabic.
    function normalize(val) {
        return val === 'ku' ? 'ku' : 'ar';
    }

    // Sorani Kurdish is 'ckb' to screen readers; 'ku' means Kurmanji.
    function htmlLang(lang) {
        return lang === 'ku' ? 'ckb' : lang;
    }

    function persist(value) {
        var lang = normalize(value);
        try {
            localStorage.setItem(KEY, lang);
        } catch (e) { }
        setCookie(KEY, lang, 365);
    }

    function current() {
        var c = getCookie(KEY) || (root.getAttribute('data-lang')) || stored();
        return normalize(c);
    }

    function apply(value) {
        var lang = normalize(value);
        var dir = (lang === 'en') ? 'ltr' : 'rtl';

        root.setAttribute('lang', htmlLang(lang));
        root.setAttribute('data-lang', lang);
        root.setAttribute('dir', dir);

        document.querySelectorAll('[data-hr-lang-toggle]').forEach(function (el) {
            el.setAttribute('data-lang-state', lang);
            var title = lang === 'ku' ? 'گۆڕین بۆ زمانی عەرەبی' : 'التحويل إلى اللغة الكردية';
            el.setAttribute('aria-label', title);
            el.setAttribute('title', title);
            var label = el.querySelector('.hr-lang-label');
            if (label) {
                label.textContent = lang === 'ku' ? 'کوردی' : 'عربي';
            }
        });
    }

    function applyLive(value) {
        var lang = normalize(value);
        persist(lang);
        apply(lang);
    }

    window.hrLang = {
        get: current,
        applyLive: applyLive,
        set: function (value, reload) {
            persist(value);
            apply(value);
            if (reload) {
                window.location.reload();
            }
        },
        // Always reloads: the shell is server-rendered, so a live swap
        // would only change the attributes, not the words.
        toggle: function () {
            var next = current() === 'ar' ? 'ku' : 'ar';
            persist(next);
            apply(next);
            window.location.reload();
        }
    };

    window.hrOpenPreferences = function () {
        var trigger = document.getElementById('hr-global-pref-trigger') || document.querySelector('.hr-pref-toggle');
        if (trigger) {
            trigger.click();
        }
    };

    document.addEventListener('click', function (e) {
        var btn = e.target && e.target.closest ? e.target.closest('[data-hr-lang-toggle]') : null;
        if (btn) {
            e.preventDefault();
            e.stopPropagation();
            window.hrLang.toggle();
        }
    });

    // Ensure state is aligned on script load, and again after Blazor's
    // enhanced navigation patches the document.
    var initial = current();
    persist(initial);
    apply(initial);
    document.addEventListener('blazor:enhancedload', function () { apply(current()); });
})();
