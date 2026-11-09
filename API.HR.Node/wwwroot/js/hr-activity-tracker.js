(function () {
    if (window.__hrActivityTrackerInstalled) return;
    window.__hrActivityTrackerInstalled = true;

    const SESSION_KEY = "hr_activity_session_id";
    let sessionId = sessionStorage.getItem(SESSION_KEY);
    if (!sessionId) {
        sessionId = "sess_" + Date.now().toString(36) + "_" + Math.random().toString(36).substring(2, 9);
        sessionStorage.setItem(SESSION_KEY, sessionId);
    }

    const queue = [];
    const inputTimers = new WeakMap();
    const lastInputValues = new WeakMap();

    function getCleanPageTitle() {
        const raw = document.title || "";
        return raw.split("|")[0].trim() || location.pathname;
    }

    function resolveElementLabel(el) {
        if (!el) return "";
        if (el.getAttribute("aria-label")) return el.getAttribute("aria-label").trim();
        if (el.getAttribute("title")) return el.getAttribute("title").trim();
        if (el.id) {
            try {
                const lbl = document.querySelector(`label[for="${CSS.escape(el.id)}"]`);
                if (lbl && lbl.innerText) return lbl.innerText.replace(/\*/g, "").trim();
            } catch (e) { }
        }
        const fieldWrap = el.closest(".hr-field, .hr-toolbar-field, .hr-toolbar-search, label");
        if (fieldWrap) {
            const lbl = fieldWrap.querySelector(".hr-field-label, .hr-label, label");
            if (lbl && lbl.innerText) return lbl.innerText.replace(/\*/g, "").trim();
        }
        const prev = el.previousElementSibling;
        if (prev && prev.tagName === "LABEL" && prev.innerText) {
            return prev.innerText.replace(/\*/g, "").trim();
        }
        if (el.getAttribute("placeholder")) return el.getAttribute("placeholder").trim();
        return el.name || el.id || "عنصر واجهة";
    }

    function pushEvent(item) {
        queue.push({
            category: item.category || "Click",
            actionCode: item.actionCode || "UI_EVENT",
            actionTitle: (item.actionTitle || "").substring(0, 220),
            pagePath: location.pathname + location.search,
            pageTitle: getCleanPageTitle(),
            elementLabel: (item.elementLabel || "").substring(0, 180),
            valueText: (item.valueText || "").substring(0, 350),
            durationSeconds: item.durationSeconds || null,
            sessionId: sessionId
        });

        if (queue.length >= 15) {
            flushQueue(false);
        }
    }

    function flushQueue(useKeepalive) {
        if (queue.length === 0) return;
        const batch = queue.splice(0, 35);
        try {
            fetch("/api/activity/batch", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(batch),
                credentials: "same-origin",
                keepalive: !!useKeepalive
            }).catch(function () { });
        } catch (e) { }
    }

    // 1. Dropdown (<select>) change tracking
    document.addEventListener("change", function (ev) {
        const target = ev.target;
        if (!target || !target.tagName) return;

        if (target.tagName === "SELECT") {
            const label = resolveElementLabel(target);
            const selectedOpt = target.options && target.selectedIndex >= 0
                ? target.options[target.selectedIndex]
                : null;
            const selectedText = selectedOpt ? (selectedOpt.text || selectedOpt.value || "").trim() : (target.value || "");
            pushEvent({
                category: "Dropdown",
                actionCode: "DROPDOWN_CHANGE",
                actionTitle: "تغيير قائمة منسدلة: " + label,
                elementLabel: label,
                valueText: selectedText
            });
            return;
        }

        if (target.tagName === "INPUT" && (target.type === "checkbox" || target.type === "radio")) {
            const label = resolveElementLabel(target);
            const stateText = target.checked ? "تفعيل / اختيار" : "إلغاء تحديد";
            pushEvent({
                category: "Click",
                actionCode: "CHECKBOX_TOGGLE",
                actionTitle: stateText + ": " + label,
                elementLabel: label,
                valueText: target.checked ? "مفعّل" : "غير مفعّل"
            });
        }
    }, true);

    // 2. Debounced Search & Text Input tracking (excludes passwords)
    document.addEventListener("input", function (ev) {
        const target = ev.target;
        if (!target || (target.tagName !== "INPUT" && target.tagName !== "TEXTAREA")) return;

        const type = (target.type || "text").toLowerCase();
        if (type === "password" || type === "hidden" || type === "file" || type === "checkbox" || type === "radio") return;
        const auto = (target.getAttribute("autocomplete") || "").toLowerCase();
        if (auto.includes("password")) return;

        const prevTimer = inputTimers.get(target);
        if (prevTimer) clearTimeout(prevTimer);

        const timer = setTimeout(function () {
            const val = (target.value || "").trim();
            if (!val || lastInputValues.get(target) === val) return;
            lastInputValues.set(target, val);

            const label = resolveElementLabel(target);
            const isSearch = type === "search" ||
                (label && (label.includes("بحث") || label.includes("ابحث") || label.includes("فلتر")));

            pushEvent({
                category: "Search",
                actionCode: isSearch ? "SEARCH_QUERY" : "TEXT_INPUT",
                actionTitle: (isSearch ? "بحث في «" : "إدخال نص في «") + label + "»",
                elementLabel: label,
                valueText: val
            });
        }, 750);

        inputTimers.set(target, timer);
    }, true);

    // 3. Button, Tab & Interactive Card Clicks
    document.addEventListener("click", function (ev) {
        const target = ev.target;
        if (!target || !target.closest) return;

        const clickable = target.closest("button, [role='tab'], a.hr-btn, .gm-kpi-card, .bd-month-btn, .bd-kpi-card");
        if (!clickable) return;

        const role = clickable.getAttribute("role");
        const isTab = role === "tab" || clickable.classList.contains("hr-tab");
        let text = (clickable.innerText || clickable.getAttribute("aria-label") || clickable.getAttribute("title") || "").replace(/\s+/g, " ").trim();
        if (!text) return;
        if (text.length > 90) text = text.substring(0, 90) + "…";

        pushEvent({
            category: "Click",
            actionCode: isTab ? "TAB_SWITCH" : "BUTTON_CLICK",
            actionTitle: (isTab ? "الانتقال لتبويب: " : "نقر زر: ") + text,
            elementLabel: text,
            valueText: ""
        });
    }, true);

    setInterval(function () {
        flushQueue(false);
    }, 2500);

    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "hidden") {
            flushQueue(true);
        }
    });

    window.addEventListener("pagehide", function () {
        flushQueue(true);
    });
})();

