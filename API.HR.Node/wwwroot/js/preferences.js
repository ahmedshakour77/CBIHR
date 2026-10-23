/* ==========================================================================
   API.HR.Node — User Preferences Script (Offline & Instant DOM Sync)
   Controls: Font family, Text scale, Density, Radius, High Contrast, Motion
   ========================================================================== */
(function () {
    'use strict';

    var root = document.documentElement;

    function safeGet(key, def) {
        try {
            return localStorage.getItem(key) || def;
        } catch (e) {
            return def;
        }
    }

    function safeSet(key, val) {
        try {
            localStorage.setItem(key, String(val));
        } catch (e) { }
    }

    function isSystemDark() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    window.hrPreferences = {
        get: function () {
            return {
                theme: safeGet('hr-theme', 'light'),
                font: safeGet('hr-font', 'IBM Plex Sans Arabic'),
                scale: safeGet('hr-scale', 'normal'),
                density: safeGet('hr-density', 'comfortable'),
                radius: safeGet('hr-radius', 'rounded'),
                contrast: safeGet('hr-contrast', 'false') === 'true',
                reducedMotion: safeGet('hr-reduced-motion', 'false') === 'true'
            };
        },
        apply: function (prefs) {
            if (!prefs) return;

            if (prefs.theme) {
                var isDark = prefs.theme === 'dark' || (prefs.theme === 'system' && isSystemDark());
                root.setAttribute('data-theme', isDark ? 'dark' : 'light');
                root.classList.toggle('dark', isDark);
                safeSet('hr-theme', prefs.theme);
                safeSet('cbi.hr.theme', isDark ? 'dark' : 'light');

                var meta = document.querySelector('meta[name="theme-color"]');
                if (meta) {
                    meta.setAttribute('content', isDark ? '#160921' : '#3B1E54');
                }
            }

            if (prefs.font) {
                var fontLower = prefs.font.toLowerCase();
                var f = 'ibm';
                if (fontLower.includes('cairo')) {
                    f = 'cairo';
                } else if (fontLower.includes('system')) {
                    f = 'system';
                }
                root.setAttribute('data-font', f);
                safeSet('hr-font', prefs.font);
            }

            if (prefs.scale) {
                root.setAttribute('data-scale', prefs.scale);
                safeSet('hr-scale', prefs.scale);
            }

            if (prefs.density) {
                root.setAttribute('data-density', prefs.density);
                safeSet('hr-density', prefs.density);
            }

            if (prefs.radius) {
                root.setAttribute('data-radius', prefs.radius);
                safeSet('hr-radius', prefs.radius);
            }

            if (typeof prefs.contrast === 'boolean') {
                if (prefs.contrast) {
                    root.setAttribute('data-contrast', 'high');
                } else {
                    root.removeAttribute('data-contrast');
                }
                safeSet('hr-contrast', prefs.contrast);
            }

            if (typeof prefs.reducedMotion === 'boolean') {
                if (prefs.reducedMotion) {
                    root.setAttribute('data-reduced-motion', 'true');
                } else {
                    root.removeAttribute('data-reduced-motion');
                }
                safeSet('hr-reduced-motion', prefs.reducedMotion);
            }
        }
    };

    // Listen for OS dark mode changes when theme is set to 'system'
    if (window.matchMedia) {
        var mq = window.matchMedia('(prefers-color-scheme: dark)');
        var onOsThemeChange = function () {
            var currentTheme = safeGet('hr-theme', 'light');
            if (currentTheme === 'system') {
                window.hrPreferences.apply({ theme: 'system' });
            }
        };
        if (mq.addEventListener) {
            mq.addEventListener('change', onOsThemeChange);
        } else if (mq.addListener) {
            mq.addListener(onOsThemeChange);
        }
    }

    // Apply saved preferences on script load
    window.hrPreferences.apply(window.hrPreferences.get());
})();
