// HR Residence Unified Map - Executive Leaflet Engine
(function () {
    window.HrResidenceMap = {
        map: null,
        markersLayer: null,
        userLayer: null,
        markerRegistry: {},
        dotNetHelper: null,
        currentTileLayer: null,
        activeLayerKey: 'streets',

        tileLayers: {
            streets: {
                name: 'شوارع',
                url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
                options: {
                    maxZoom: 19,
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a>'
                }
            },
            satellite: {
                name: 'أقمار صناعية',
                url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
                options: {
                    maxZoom: 19,
                    attribution: 'Tiles &copy; Esri &mdash; Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community'
                }
            },
            dark: {
                name: 'ليلي',
                url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
                options: {
                    maxZoom: 19,
                    attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
                }
            }
        },

        init: function (containerId, dotNetRef, locations, preferredLayer) {
            const container = document.getElementById(containerId);
            if (!container) return;

            // Cleanup previous map instance if any
            if (this.map) {
                try {
                    this.map.remove();
                } catch (e) {
                    console.warn("Map remove error:", e);
                }
                this.map = null;
                this.markerRegistry = {};
            }

            this.dotNetHelper = dotNetRef;

            // Auto-detect dark mode if preferredLayer is not specified
            if (!preferredLayer) {
                const isDark = document.documentElement.getAttribute('data-theme') === 'dark' ||
                               (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
                this.activeLayerKey = isDark ? 'dark' : 'streets';
            } else {
                this.activeLayerKey = preferredLayer;
            }

            // Center of Iraq
            const defaultCenter = [33.3152, 44.3661];
            const defaultZoom = 6;

            this.map = L.map(containerId, {
                center: defaultCenter,
                zoom: defaultZoom,
                zoomControl: false,
                attributionControl: true
            });

            // Zoom control on top-left
            L.control.zoom({ position: 'topleft' }).addTo(this.map);

            // Set Initial Tile Layer
            this.setTileLayer(this.activeLayerKey);

            this.markersLayer = L.layerGroup().addTo(this.map);
            this.userLayer = L.layerGroup().addTo(this.map);

            if (locations && locations.length > 0) {
                this.renderMarkers(locations);
            }

            // Invalidate size multiple times to ensure perfect canvas rendering
            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 80);
            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 250);
            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 600);
            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 1200);
        },

        setTileLayer: function (layerKey) {
            if (!this.map || !this.tileLayers[layerKey]) return;

            if (this.currentTileLayer) {
                this.map.removeLayer(this.currentTileLayer);
            }

            const config = this.tileLayers[layerKey];
            this.currentTileLayer = L.tileLayer(config.url, config.options).addTo(this.map);
            this.activeLayerKey = layerKey;

            // If switching to satellite, add label layer for street/city names
            if (layerKey === 'satellite') {
                if (!this.satelliteLabelsLayer) {
                    this.satelliteLabelsLayer = L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager_only_labels/{z}/{x}/{y}{r}.png', {
                        maxZoom: 19,
                        pane: 'shadowPane'
                    });
                }
                this.satelliteLabelsLayer.addTo(this.map);
            } else if (this.satelliteLabelsLayer) {
                this.map.removeLayer(this.satelliteLabelsLayer);
            }
        },

        createMarkerIcon: function (loc) {
            const isHouse = loc.housingType === "دار";
            const isCurrent = loc.isCurrent;

            // Border colour from the categorical series tokens, so pins follow the theme and match the legend:
            // emerald (series-3) house, violet (series-1) apartment, slate (series-6) previous
            const borderColor = isCurrent 
                ? (isHouse ? "var(--series-3)" : "var(--series-1)")
                : "var(--series-6)";

            const typeBadgeBg = isHouse ? "var(--series-3)" : "var(--series-1)";
            const typeBadgeIcon = isHouse
                ? `<svg viewBox="0 0 24 24" width="10" height="10" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>`
                : `<svg viewBox="0 0 24 24" width="10" height="10" fill="none" stroke="currentColor" stroke-width="2.5"><rect x="4" y="2" width="16" height="20" rx="2" ry="2"/><line x1="8" y1="6" x2="10" y2="6"/><line x1="14" y1="6" x2="16" y2="6"/><line x1="8" y1="10" x2="10" y2="10"/><line x1="14" y1="10" x2="16" y2="10"/></svg>`;

            const pulseRing = isCurrent ? `<div class="hr-map-avatar-pulse" style="border-color:${borderColor};"></div>` : "";

            let avatarContent = "";
            if (loc.photoUrl && loc.photoUrl.trim() !== "") {
                avatarContent = `<img src="${loc.photoUrl}" alt="${loc.employeeName || ''}" class="hr-map-pin-img" onerror="this.style.display='none';this.nextElementSibling.style.display='flex';" /><div class="hr-map-pin-fallback" style="display:none;">${(loc.employeeName || 'م')[0]}</div>`;
            } else {
                const initial = (loc.employeeName && loc.employeeName.trim().length > 0) ? loc.employeeName.trim()[0] : 'م';
                avatarContent = `<div class="hr-map-pin-fallback">${initial}</div>`;
            }

            const html = `
                <div class="hr-map-avatar-pin ${isCurrent ? 'is-current' : 'is-previous'}" style="--pin-border:${borderColor};">
                    ${pulseRing}
                    <div class="hr-map-pin-circle">
                        ${avatarContent}
                        <div class="hr-map-pin-type-badge" style="background:${typeBadgeBg};" title="${loc.housingType}">
                            ${typeBadgeIcon}
                        </div>
                    </div>
                    <div class="hr-map-pin-tip" style="border-top-color:${borderColor};"></div>
                </div>
            `;

            return L.divIcon({
                className: 'hr-map-avatar-marker',
                html: html,
                iconSize: [44, 52],
                iconAnchor: [22, 52],
                popupAnchor: [0, -50]
            });
        },

        buildPopupHtml: function (loc) {
            const isHouse = loc.housingType === "دار";
            const statusBadge = loc.isCurrent
                ? `<span class="hr-badge hr-badge-ok">السكن الحالي</span>`
                : `<span class="hr-badge">سكن سابق</span>`;

            const typeBadge = isHouse
                ? `<span class="hr-badge hr-badge-accent">دار</span>`
                : `<span class="hr-badge hr-badge-accent">شقة</span>`;

            let addressDetails = "";
            if (isHouse) {
                const parts = [];
                if (loc.district) parts.push(`حي: ${loc.district}`);
                if (loc.mahalla) parts.push(`م: ${loc.mahalla}`);
                if (loc.zuqaq) parts.push(`ز: ${loc.zuqaq}`);
                if (loc.dar) parts.push(`د: ${loc.dar}`);
                addressDetails = parts.join(' • ');
            } else {
                const parts = [];
                if (loc.complexName) parts.push(loc.complexName);
                if (loc.buildingNumber) parts.push(`عمارة: ${loc.buildingNumber}`);
                if (loc.floorNumber) parts.push(`طابق: ${loc.floorNumber}`);
                if (loc.apartmentNumber) parts.push(`شقة: ${loc.apartmentNumber}`);
                addressDetails = parts.join(' • ');
            }

            const avatarHtml = (loc.photoUrl && loc.photoUrl.trim() !== "")
                ? `<img src="${loc.photoUrl}" alt="${loc.employeeName}" class="hr-map-popup-avatar" />`
                : `<div class="hr-map-popup-avatar-placeholder">${(loc.employeeName || "م")[0]}</div>`;

            let gmapsBtn = "";
            if (loc.locationUrl && (loc.locationUrl.startsWith("http://") || loc.locationUrl.startsWith("https://"))) {
                gmapsBtn = `<a href="${loc.locationUrl}" target="_blank" rel="noopener noreferrer" class="hr-btn hr-btn-secondary hr-btn-sm" style="flex:1;justify-content:center;gap:4px;">
                    <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>
                    Google Maps
                </a>`;
            }

            return `
                <div class="hr-map-popup-content" dir="rtl">
                    <div class="hr-map-popup-header">
                        ${avatarHtml}
                        <div class="hr-map-popup-title">
                            <a href="/employees/${loc.employeeId}" class="hr-map-popup-name">${loc.employeeName || 'موظف'}</a>
                            <div class="hr-map-popup-sub">
                                <span class="hr-code">${loc.employeeIdNumber || ''}</span>
                                ${loc.jobTitle ? `<span>• ${loc.jobTitle}</span>` : ''}
                            </div>
                        </div>
                    </div>
                    <div class="hr-map-popup-badges" style="display:flex;gap:6px;margin:8px 0;flex-wrap:wrap;">
                        ${statusBadge}
                        ${typeBadge}
                        ${loc.governorateName ? `<span class="hr-badge">${loc.governorateName}</span>` : ''}
                        ${loc.isAccurate ? `<span class="hr-badge hr-badge-ok">GPS دقيق</span>` : `<span class="hr-badge hr-badge-warn">مركز المحافظة</span>`}
                    </div>
                    ${!loc.isAccurate ? `<div class="hr-alert hr-alert-warn" role="note" style="padding:var(--s-2);margin-block:var(--s-2);font-size:var(--fs-caption);line-height:1.5;">الموقع تقريبي في مركز المحافظة لعدم توفر رابط GPS دقيق. يمكن تعديله من ملف الموظف.</div>` : ''}
                    <div class="hr-map-popup-info">
                        ${addressDetails ? `<div class="hr-map-info-row"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg><span>${addressDetails}</span></div>` : ''}
                        ${loc.familyHeadName ? `<div class="hr-map-info-row"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg><span>رب الأسرة: ${loc.familyHeadName}</span></div>` : ''}
                        ${loc.formNumber ? `<div class="hr-map-info-row"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg><span>رقم البطاقة: ${loc.formNumber}</span></div>` : ''}
                    </div>
                    <div class="hr-map-popup-actions" style="display:flex;gap:6px;margin-top:10px;">
                        <button type="button" onclick="window.HrResidenceMap.selectFromPopup(${loc.id})" class="hr-btn hr-btn-primary hr-btn-sm" style="flex:1;justify-content:center;">لوحة التفاصيل</button>
                        ${gmapsBtn}
                    </div>
                </div>
            `;
        },

        selectFromPopup: function (id) {
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnMarkerSelected', id);
            }
        },

        renderMarkers: function (locations) {
            if (!this.map || !this.markersLayer) return;

            this.markersLayer.clearLayers();
            this.markerRegistry = {};

            const bounds = [];

            locations.forEach(loc => {
                if (loc.latitude && loc.longitude) {
                    const latLng = [loc.latitude, loc.longitude];
                    bounds.push(latLng);

                    const icon = this.createMarkerIcon(loc);
                    const marker = L.marker(latLng, { icon: icon });

                    // Tooltip with employee name on hover
                    marker.bindTooltip(`<b>${loc.employeeName || 'موظف'}</b> (${loc.housingType})`, {
                        direction: 'top',
                        offset: [0, -48],
                        className: 'hr-map-avatar-tooltip'
                    });

                    const popupHtml = this.buildPopupHtml(loc);
                    marker.bindPopup(popupHtml, {
                        maxWidth: 320,
                        className: 'hr-map-custom-popup'
                    });

                    marker.on('click', () => {
                        if (this.dotNetHelper) {
                            this.dotNetHelper.invokeMethodAsync('OnMarkerSelected', loc.id);
                        }
                    });

                    this.markersLayer.addLayer(marker);
                    this.markerRegistry[loc.id] = marker;
                }
            });

            if (bounds.length > 0) {
                try {
                    this.map.fitBounds(bounds, { padding: [60, 60], maxZoom: 14 });
                } catch (e) {
                    console.warn("fitBounds error:", e);
                }
            }
        },

        updateMarkers: function (locations) {
            this.renderMarkers(locations);
            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 80);
        },

        focusLocation: function (id, lat, lng) {
            if (!this.map) return;

            this.map.flyTo([lat, lng], 16, { duration: 1.2 });

            const marker = this.markerRegistry[id];
            if (marker) {
                setTimeout(() => {
                    marker.openPopup();
                }, 1200);
            }
        },

        fitAll: function () {
            if (!this.map) return;
            const markers = Object.values(this.markerRegistry);
            if (markers.length === 0) {
                this.map.flyTo([33.3152, 44.3661], 6);
                return;
            }
            const bounds = L.featureGroup(markers).getBounds();
            this.map.fitBounds(bounds, { padding: [60, 60], maxZoom: 14 });
        },

        locateUser: function () {
            if (!this.map || !navigator.geolocation) {
                alert("خاصية تحديد الموقع غير مدعومة في المتصفح.");
                return;
            }

            navigator.geolocation.getCurrentPosition(
                (pos) => {
                    const lat = pos.coords.latitude;
                    const lng = pos.coords.longitude;

                    if (this.userLayer) {
                        this.userLayer.clearLayers();
                        const userIcon = L.divIcon({
                            className: 'hr-map-user-beacon',
                            html: '<div class="hr-map-user-dot"><div class="hr-map-user-ring"></div></div>',
                            iconSize: [24, 24],
                            iconAnchor: [12, 12]
                        });
                        const m = L.marker([lat, lng], { icon: userIcon });
                        m.bindPopup('<b>موقعك الجغرافي الحالي</b>').openPopup();
                        this.userLayer.addLayer(m);
                    }

                    this.map.flyTo([lat, lng], 15, { duration: 1.5 });
                },
                (err) => {
                    alert("تعذر الوصول لموقعك الجغرافي: " + err.message);
                },
                { enableHighAccuracy: true, timeout: 10000 }
            );
        },

        toggleFullscreen: function (containerId) {
            const el = document.getElementById(containerId) || (this.map ? this.map.getContainer() : null);
            if (!el) return;

            if (!document.fullscreenElement) {
                el.requestFullscreen().catch(err => {
                    console.warn(`Error attempting to enable fullscreen: ${err.message}`);
                });
            } else {
                document.exitFullscreen();
            }

            setTimeout(() => { if (this.map) this.map.invalidateSize(); }, 200);
        },

        invalidateSize: function () {
            if (this.map) {
                this.map.invalidateSize();
            }
        }
    };
})();
