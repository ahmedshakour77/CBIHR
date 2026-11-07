(function () {
    "use strict";

    let mapInstance = null;
    let geoJsonLayer = null;
    let markersLayer = null;
    let cachedGeoJson = null;

    const isoToCode = {
        "IQ-BG": "BGD",
        "IQ-NI": "NIN",
        "IQ-AR": "EBL",
        "IQ-BA": "BSR",
        "IQ-DA": "DHK",
        "IQ-SU": "SUL",
        "IQ-KI": "KIR",
        "IQ-SD": "SAL",
        "IQ-DI": "DIY",
        "IQ-AN": "ANB",
        "IQ-BB": "BBL",
        "IQ-KA": "KRB",
        "IQ-WA": "WAS",
        "IQ-NA": "NJF",
        "IQ-QA": "QAD",
        "IQ-MA": "MYS",
        "IQ-DQ": "DHI",
        "IQ-MU": "MUT"
    };

    function ensureLeafletLoaded() {
        return new Promise((resolve, reject) => {
            if (window.L) {
                resolve(window.L);
                return;
            }
            if (typeof window.hrLoadScript === "function") {
                window.hrLoadScript("js/leaflet.js").then(() => resolve(window.L)).catch(reject);
                return;
            }
            const script = document.createElement("script");
            script.src = "js/leaflet.js";
            script.onload = () => resolve(window.L);
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    async function loadIraqGeoJson() {
        if (cachedGeoJson) return cachedGeoJson;
        try {
            const res = await fetch("data/iraq-governorates.geojson");
            if (res.ok) {
                cachedGeoJson = await res.json();
            }
        } catch (e) {
            console.warn("Could not load iraq-governorates.geojson:", e);
        }
        return cachedGeoJson;
    }

    window.hrGeneralMap = {
        initOrUpdate: async function (containerId, governorateNodes, selectedGovCode, dotNetRef) {
            try {
                const L = await ensureLeafletLoaded();
                const container = document.getElementById(containerId);
                if (!container || !L) return;

                if (mapInstance && mapInstance._container !== container) {
                    mapInstance.remove();
                    mapInstance = null;
                }

                if (!mapInstance) {
                    mapInstance = L.map(container, {
                        center: [33.3152, 44.1],
                        zoom: 6,
                        minZoom: 5,
                        maxZoom: 13,
                        zoomControl: true
                    });

                    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                        attribution: "&copy; OpenStreetMap | FAO Administrative Boundaries",
                        maxZoom: 18
                    }).addTo(mapInstance);

                    geoJsonLayer = L.layerGroup().addTo(mapInstance);
                    markersLayer = L.layerGroup().addTo(mapInstance);
                }

                geoJsonLayer.clearLayers();
                markersLayer.clearLayers();

                if (!Array.isArray(governorateNodes)) return;
                const nodeByCode = {};
                governorateNodes.forEach(n => { nodeByCode[n.code] = n; });

                const geoData = await loadIraqGeoJson();
                if (geoData) {
                    const layer = L.geoJSON(geoData, {
                        style: function (feature) {
                            const iso = feature.properties && feature.properties.shapeISO;
                            const code = isoToCode[iso] || "";
                            const node = nodeByCode[code];
                            const isSelected = selectedGovCode && selectedGovCode === code;
                            const isHub = node && (node.employeeCount > 0 || node.isMainBranchHub);

                            return {
                                fillColor: isHub ? (node.color || "#1a669d") : "#cbd5e1",
                                fillOpacity: isSelected ? 0.82 : (isHub ? 0.68 : 0.25),
                                color: isSelected ? "#0f172a" : (isHub ? "#ffffff" : "#64748b"),
                                weight: isSelected ? 3 : (isHub ? 2 : 1)
                            };
                        },
                        onEachFeature: function (feature, fLayer) {
                            const iso = feature.properties && feature.properties.shapeISO;
                            const code = isoToCode[iso] || "";
                            const node = nodeByCode[code];
                            if (node) {
                                fLayer.on("click", function () {
                                    if (dotNetRef) {
                                        dotNetRef.invokeMethodAsync("SelectGovernorateFromJs", node.code);
                                    }
                                });
                            }
                        }
                    });
                    geoJsonLayer.addLayer(layer);
                }

                governorateNodes.forEach(node => {
                    if (!node.lat || !node.lng) return;
                    const hasStaff = node.employeeCount > 0 || node.isMainBranchHub;
                    if (!hasStaff && selectedGovCode !== node.code) return;

                    const isSelected = selectedGovCode && selectedGovCode === node.code;
                    const bgColor = node.color || "#1a669d";

                    const html = `
                        <div style="
                            display:inline-flex;
                            align-items:center;
                            gap:6px;
                            padding:4px 10px;
                            border-radius:10px;
                            background:#ffffff;
                            color:#0f172a;
                            border:${isSelected ? '2px solid ' + bgColor : '1px solid #cbd5e1'};
                            box-shadow:0 4px 12px rgba(15,23,42,0.16);
                            font-family:inherit;
                            white-space:nowrap;
                            cursor:pointer;">
                            <span style="width:9px;height:9px;border-radius:999px;background:${bgColor};display:inline-block;"></span>
                            <strong style="font-size:12px;">${node.name}</strong>
                            <span style="background:${bgColor};color:#fff;padding:1px 6px;border-radius:6px;font-size:11px;font-weight:800;">${node.arabicCount || node.employeeCount}</span>
                        </div>
                    `;

                    const icon = L.divIcon({
                        className: "hr-general-map-divicon",
                        html: html,
                        iconSize: [120, 32],
                        iconAnchor: [60, 16]
                    });

                    const marker = L.marker([node.lat, node.lng], { icon: icon });
                    marker.on("click", function () {
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync("SelectGovernorateFromJs", node.code);
                        }
                    });

                    markersLayer.addLayer(marker);
                });

                setTimeout(() => {
                    if (mapInstance) {
                        mapInstance.invalidateSize();
                        if (selectedGovCode && nodeByCode[selectedGovCode]) {
                            const target = nodeByCode[selectedGovCode];
                            mapInstance.flyTo([target.lat, target.lng], 7, { duration: 0.6 });
                        } else {
                            mapInstance.flyTo([33.3152, 44.1], 6, { duration: 0.5 });
                        }
                    }
                }, 80);
            } catch (err) {
                console.warn("hrGeneralMap error:", err);
            }
        },

        dispose: function () {
            if (mapInstance) {
                mapInstance.remove();
                mapInstance = null;
            }
        }
    };
})();
