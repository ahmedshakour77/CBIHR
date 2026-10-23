/* API.HR.Node appearance preferences.
   One source of truth for data-theme, data-brand, data-font, data-font-size, data-density, and data-nav-mode on <html>.
   Replaced the old theme.js (deleted) and preferences.js; keeps their window.hrTheme and hrPreferences APIs. */
(function () {
  'use strict';
  var KEY = 'hr-prefs';
  var DEFAULTS = { mode: 'system', brand: 'purple', font: 'plex', fontSize: 'md', density: 'comfortable', navMode: 'side', contrast: false, reducedMotion: false };
  var ALLOWED = {
    mode: ['light', 'dark', 'system'],
    brand: ['purple', 'indigo', 'blue', 'teal', 'green', 'olive', 'gold', 'copper', 'crimson', 'rose', 'slate'],
    font: ['plex', 'cairo', 'tajawal', 'noto', 'alexandria'],
    fontSize: ['sm', 'md', 'lg', 'xl'],
    density: ['compact', 'comfortable', 'spacious'],
    navMode: ['side', 'top', 'floating']
  };
  var FONT_URL = {
    plex: 'IBM+Plex+Sans+Arabic:wght@400;500;600;700',
    cairo: 'Cairo:wght@400;500;600;700',
    tajawal: 'Tajawal:wght@400;500;700',
    noto: 'Noto+Sans+Arabic:wght@400;500;600;700',
    alexandria: 'Alexandria:wght@400;500;600;700'
  };
  var root = document.documentElement;
  var mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  var listeners = [];

  function getCookie(name) {
    try {
      var match = document.cookie.match(new RegExp('(^|;\\s*)' + name + '=([^;]*)'));
      return match ? decodeURIComponent(match[2]) : null;
    } catch (e) {
      return null;
    }
  }

  function read() {
    try {
      var item = localStorage.getItem(KEY);
      if (item) return JSON.parse(item);
    } catch (e) { }
    // Fallback: check hr-prefs cookie
    try {
      var cookieJson = getCookie('hr-prefs');
      if (cookieJson) return JSON.parse(cookieJson);
    } catch (e) { }
    // Fallback: check individual cookies & legacy keys
    try {
      var legacyTheme = localStorage.getItem('hr-theme') || getCookie('hr-theme');
      var legacyNav = localStorage.getItem('hr-nav-mode') || getCookie('hr-nav-mode');
      var legacyBrand = getCookie('hr-brand');
      var legacyFont = getCookie('hr-font');
      var legacyScale = getCookie('hr-font-size');
      var legacyDensity = getCookie('hr-density');
      var legacyContrast = localStorage.getItem('hr-contrast') === 'true' || getCookie('hr-contrast') === 'true';
      var legacyMotion = localStorage.getItem('hr-reduced-motion') === 'true' || getCookie('hr-reduced-motion') === 'true';
      var out = {};
      if (legacyTheme) out.mode = legacyTheme;
      if (legacyNav) out.navMode = legacyNav;
      if (legacyBrand) out.brand = legacyBrand;
      if (legacyFont) out.font = legacyFont;
      if (legacyScale) out.fontSize = legacyScale;
      if (legacyDensity) out.density = legacyDensity;
      if (legacyContrast) out.contrast = true;
      if (legacyMotion) out.reducedMotion = true;
      return out;
    } catch (e) {
      return {};
    }
  }

  function write(p) {
    try {
      localStorage.setItem(KEY, JSON.stringify(p));
      localStorage.setItem('hr-theme', p.mode);
      localStorage.setItem('hr-nav-mode', p.navMode);
      localStorage.setItem('hr-contrast', String(!!p.contrast));
      localStorage.setItem('hr-reduced-motion', String(!!p.reducedMotion));

      var maxAge = '; path=/; max-age=31536000; SameSite=Lax';
      document.cookie = 'hr-nav-mode=' + encodeURIComponent(p.navMode || 'side') + maxAge;
      document.cookie = 'hr-theme=' + encodeURIComponent(p.mode || 'system') + maxAge;
      document.cookie = 'hr-brand=' + encodeURIComponent(p.brand || 'purple') + maxAge;
      document.cookie = 'hr-font=' + encodeURIComponent(p.font || 'plex') + maxAge;
      document.cookie = 'hr-font-size=' + encodeURIComponent(p.fontSize || 'md') + maxAge;
      document.cookie = 'hr-density=' + encodeURIComponent(p.density || 'comfortable') + maxAge;
      document.cookie = 'hr-contrast=' + encodeURIComponent(String(!!p.contrast)) + maxAge;
      document.cookie = 'hr-reduced-motion=' + encodeURIComponent(String(!!p.reducedMotion)) + maxAge;
      document.cookie = 'hr-prefs=' + encodeURIComponent(JSON.stringify(p)) + maxAge;
    } catch (e) { /* private mode */ }
  }

  function clean(p) {
    var out = {};
    Object.keys(DEFAULTS).forEach(function (k) {
      if (k === 'contrast' || k === 'reducedMotion') {
        out[k] = !!(p && p[k]);
      } else {
        out[k] = p && ALLOWED[k] && ALLOWED[k].indexOf(p[k]) > -1 ? p[k] : DEFAULTS[k];
      }
    });
    return out;
  }

  function loadFont(font) {
    var id = 'hr-font-link', link = document.getElementById(id);
    var href = 'https://fonts.googleapis.com/css2?family=' + FONT_URL[font] + '&family=Inter:wght@400;500;600;700&display=swap';
    if (!link) {
      link = document.createElement('link');
      link.id = id;
      link.rel = 'stylesheet';
      document.head.appendChild(link);
    }
    if (link.href !== href) link.href = href;
  }

  /* A quick theme switch names the action it will take, so its label has to
     follow the theme: in dark mode it offers light, not dark again. Runs
     before the body exists on first call, and again on DOMContentLoaded. */
  function labelThemeToggles(theme) {
    var ku = root.getAttribute('data-lang') === 'ku';
    var label = theme === 'dark'
      ? (ku ? 'گۆڕین بۆ شێوازی ڕووناک' : 'التحويل إلى النمط الفاتح')
      : (ku ? 'گۆڕین بۆ شێوازی تاریک' : 'التحويل إلى النمط الداكن');
    document.querySelectorAll('[data-hr-theme-toggle]').forEach(function (el) {
      el.setAttribute('aria-label', label);
      el.setAttribute('title', label);
    });
  }

  function apply(p) {
    if (!p) p = current;
    var theme = p.mode === 'system' ? (mq && mq.matches ? 'dark' : 'light') : p.mode;
    root.setAttribute('data-theme', theme);
    root.classList.toggle('dark', theme === 'dark');
    labelThemeToggles(theme);

    if (p.brand === 'purple') {
      root.removeAttribute('data-brand');
    } else {
      root.setAttribute('data-brand', p.brand);
    }

    root.setAttribute('data-font', p.font);
    root.setAttribute('data-font-size', p.fontSize);
    root.setAttribute('data-density', p.density);
    root.setAttribute('data-nav-mode', p.navMode || 'side');

    if (p.contrast) {
      root.setAttribute('data-contrast', 'high');
    } else {
      root.removeAttribute('data-contrast');
    }

    if (p.reducedMotion) {
      root.setAttribute('data-reduced-motion', 'true');
    } else {
      root.removeAttribute('data-reduced-motion');
    }

    // Read the tint back out of the design tokens rather than hardcoding it,
    // so the browser chrome follows the chosen brand and not just the theme.
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      var tint = (getComputedStyle(root)
        .getPropertyValue(theme === 'dark' ? '--b-950' : '--accent') || '').trim();
      if (!/^#[0-9a-f]{3,8}$/i.test(tint)) tint = theme === 'dark' ? '#160921' : '#3B1E54';
      meta.setAttribute('content', tint);
    }

    loadFont(p.font);
  }

  var current = clean(read());

  /* The server renders only the navigation surface for the mode in the
     hr-nav-mode cookie and writes that mode on <html>. If this browser's
     stored preference disagrees (cookie expired or cleared, localStorage
     kept), sync the cookie and reload once so the server renders the
     surface the user chose. If cookies cannot be written, the guard stops
     a loop and the page keeps the mode the server rendered, so there is
     always a visible navigation. */
  (function reconcileNavMode() {
    var serverMode = root.getAttribute('data-nav-mode');
    if (!serverMode || serverMode === current.navMode) return;
    var guard = 'hr-nav-sync';
    var tried = false;
    try { tried = sessionStorage.getItem(guard) === current.navMode; } catch (e) { tried = true; }
    if (!tried) {
      try { sessionStorage.setItem(guard, current.navMode); } catch (e) { }
      write(current);
      window.location.reload();
      return;
    }
    current.navMode = clean({ navMode: serverMode }).navMode;
  })();

  apply(current);

  if (mq) {
    var onScheme = function () { if (current.mode === 'system') apply(current); };
    mq.addEventListener ? mq.addEventListener('change', onScheme) : mq.addListener(onScheme);
  }

  // Ensure settings persist across Blazor enhanced page navigation & DOM swaps
  document.addEventListener('blazor:enhancedload', function () {
    apply(current);
  });
  document.addEventListener('DOMContentLoaded', function () {
    apply(current);
  });
  window.addEventListener('popstate', function () {
    apply(current);
  });

  // Delegated click listener for quick theme toggles
  document.addEventListener('click', function (e) {
    var btn = e.target.closest && e.target.closest('[data-hr-theme-toggle]');
    if (!btn) return;
    e.preventDefault();
    var cur = window.hrPrefs.get();
    var isDark = cur.mode === 'dark' || (cur.mode === 'system' && mq && mq.matches);
    var next = isDark ? 'light' : 'dark';
    window.hrPrefs.set({ mode: next });
  });

  window.hrPrefs = {
    get: function () { return Object.assign({}, current); },
    set: function (partial) {
      if (!partial) return Object.assign({}, current);
      var norm = {};
      for (var k in partial) {
        if (Object.prototype.hasOwnProperty.call(partial, k)) {
          var lk = k.charAt(0).toLowerCase() + k.slice(1);
          norm[lk] = partial[k];
        }
      }
      current = clean(Object.assign({}, current, norm));
      apply(current);
      write(current);
      try {
        window.dispatchEvent(new CustomEvent('hr:prefs-changed', { detail: current }));
      } catch (e) { }
      listeners.forEach(function (fn) { try { fn(current); } catch (e) { } });
      return Object.assign({}, current);
    },
    reset: function () { return this.set(DEFAULTS); },
    load: function (serverPrefs) {
      current = clean(serverPrefs);
      apply(current);
      write(current);
    },
    reapply: function () {
      apply(current);
    },
    onChange: function (fn) { listeners.push(fn); },
    bindDotNet: function (ref) {
      listeners.push(function (p) { ref.invokeMethodAsync('Save', p); });
    }
  };

  /* window.hrTheme, the API the old theme.js exposed; the command palette calls hrTheme.toggle(). */
  window.hrTheme = {
    toggle: function () {
      var isDark = root.getAttribute('data-theme') === 'dark';
      var next = isDark ? 'light' : 'dark';
      window.hrPrefs.set({ mode: next });
      return next;
    },
    get: function () {
      return root.getAttribute('data-theme') || 'light';
    },
    set: function (m) {
      return window.hrPrefs.set({ mode: m });
    }
  };

  /* Compatibility with legacy preferences.js callers */
  window.hrPreferences = {
    get: function () { return window.hrPrefs.get(); },
    apply: function (p) {
      if (!p) return;
      var patch = {};
      if (p.theme) patch.mode = p.theme;
      if (p.brand) patch.brand = p.brand;
      if (p.navMode) patch.navMode = p.navMode;
      if (p.font) {
        var fl = p.font.toLowerCase();
        if (fl.indexOf('cairo') > -1) patch.font = 'cairo';
        else if (fl.indexOf('tajawal') > -1) patch.font = 'tajawal';
        else if (fl.indexOf('noto') > -1) patch.font = 'noto';
        else if (fl.indexOf('alexandria') > -1) patch.font = 'alexandria';
        else patch.font = 'plex';
      }
      if (p.scale) {
        if (p.scale === 'compact') patch.fontSize = 'sm';
        else if (p.scale === 'large') patch.fontSize = 'lg';
        else patch.fontSize = 'md';
      }
      if (p.density) patch.density = p.density;
      return window.hrPrefs.set(patch);
    }
  };
})();

/* Loads a page-specific script once, on first use, instead of on every page.
   Returns the same promise to concurrent callers; Blazor awaits it through
   JS.InvokeVoidAsync("hrLoadScript", src). */
(function () {
  var pending = {};
  window.hrLoadScript = function (src) {
    if (!pending[src]) {
      pending[src] = new Promise(function (resolve, reject) {
        var s = document.createElement('script');
        s.src = src;
        s.onload = function () { resolve(); };
        s.onerror = function () { delete pending[src]; reject(new Error('Failed to load ' + src)); };
        document.head.appendChild(s);
      });
    }
    return pending[src];
  };
})();
