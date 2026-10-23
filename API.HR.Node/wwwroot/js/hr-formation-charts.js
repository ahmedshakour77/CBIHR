// CBI HR - Formation Dashboard ApexCharts Integration
// Draws the executive health gauges; the detailed breakdowns live in the page's tabs.

(function () {
    window.HrFormationCharts = window.HrFormationCharts || {};
    const chartInstances = {};

    function isDarkMode() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ||
            (!document.documentElement.getAttribute('data-theme') && window.matchMedia('(prefers-color-scheme: dark)').matches);
    }

    // Colours come from the design tokens in hr-tokens.css, so the charts
    // follow the light/dark theme like the rest of the page. A probe element
    // resolves var() and color-mix() to plain rgb(), which ApexCharts can shade.
    function resolveColor(token, fallback) {
        const probe = document.createElement('span');
        probe.style.display = 'none';
        probe.style.color = 'var(' + token + ', ' + fallback + ')';
        document.body.appendChild(probe);
        const value = getComputedStyle(probe).color;
        probe.remove();
        return value || fallback;
    }

    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function getPalette() {
        const dark = isDarkMode();
        const s = n => resolveColor('--series-' + n, '#475569');
        const ok = resolveColor('--ok', '#1d6b53');
        const warn = resolveColor('--warn', '#8a5a11');
        const danger = resolveColor('--danger', '#a8203c');
        return {
            mode: dark ? 'dark' : 'light',
            bg: 'transparent',
            text: resolveColor('--text', dark ? '#e0d5ec' : '#2a143e'),
            grid: resolveColor('--line-soft', dark ? 'rgba(255, 255, 255, 0.07)' : 'rgba(112, 85, 146, 0.12)'),
            ok: ok,
            warn: warn,
            danger: danger,
            series: [s(1), s(2), s(3), s(4), s(5), s(6)]
        };
    }

    function destroyChart(id) {
        if (chartInstances[id]) {
            try {
                chartInstances[id].destroy();
            } catch (e) {
                console.warn('Error destroying chart', id, e);
            }
            delete chartInstances[id];
        }
    }

    window.HrFormationCharts.destroyAll = function () {
        Object.keys(chartInstances).forEach(id => destroyChart(id));
    };

    let lastData = null;

    window.HrFormationCharts.renderCharts = function (data) {
        lastData = data;
        if (!window.ApexCharts) {
            console.error('ApexCharts is not loaded.');
            return;
        }

        const p = getPalette();
        const baseChart = {
            fontFamily: 'inherit',
            background: p.bg,
            toolbar: {
                show: true,
                tools: {
                    download: true,
                    selection: false,
                    zoom: false,
                    zoomin: false,
                    zoomout: false,
                    pan: false,
                    reset: false
                }
            },
            animations: {
                enabled: !prefersReducedMotion(),
                easing: 'easeinout',
                speed: 700
            }
        };

        function renderSemiGauge(id, value, colorFrom, colorTo, formatSuffix = '%') {
            const el = document.getElementById(id);
            if (!el) return;
            destroyChart(id);
            const val = Math.min(100, Math.max(0, Math.round(value || 0)));

            const options = {
                chart: {
                    ...baseChart,
                    type: 'radialBar',
                    height: 180,
                    sparkline: { enabled: true }
                },
                series: [val],
                colors: [colorFrom],
                plotOptions: {
                    radialBar: {
                        startAngle: -90,
                        endAngle: 90,
                        hollow: {
                            margin: 0,
                            size: '68%',
                            background: 'transparent'
                        },
                        track: {
                            background: p.grid,
                            strokeWidth: '95%',
                            margin: 4
                        },
                        dataLabels: {
                            name: { show: false },
                            value: {
                                offsetY: -4,
                                fontSize: '20px',
                                fontWeight: 800,
                                color: p.text,
                                formatter: v => v + formatSuffix
                            }
                        }
                    }
                },
                fill: {
                    type: 'gradient',
                    gradient: {
                        shade: p.mode,
                        type: 'horizontal',
                        shadeIntensity: 0.5,
                        gradientToColors: [colorTo],
                        inverseColors: true,
                        opacityFrom: 1,
                        opacityTo: 1,
                        stops: [0, 100]
                    }
                },
                stroke: {
                    lineCap: 'round'
                },
                tooltip: {
                    enabled: true,
                    theme: p.mode,
                    y: {
                        formatter: v => v + formatSuffix
                    }
                }
            };

            chartInstances[id] = new ApexCharts(el, options);
            chartInstances[id].render();
        }

        // 0. Render Executive Health Gauges (Cockpit)
        if (data) {
            renderSemiGauge('gauge-discipline', data.cleanDisciplinePercentage, p.ok, p.ok);
            renderSemiGauge('gauge-readiness', data.activeStaffPercentage, p.series[1], p.series[1]);
            renderSemiGauge('gauge-higher-edu', data.higherEducationPercentage, p.series[0], p.series[0]);
            renderSemiGauge('gauge-promotion', data.promotionReadinessPercentage, p.warn, p.warn);
            renderSemiGauge('gauge-digital', data.digitalPhotoPercentage, p.series[2], p.series[2]);
            if (document.getElementById('gauge-contracts')) {
                renderSemiGauge('gauge-contracts', data.contractStaffPercentage, p.danger, p.danger);
            }
            if (document.getElementById('gauge-vehicles')) {
                renderSemiGauge('gauge-vehicles', data.vehicleCompliancePercentage, p.series[3], p.series[3]);
            }
        }
    };

    // The theme switches live (hrTheme.toggle sets data-theme on <html>), so
    // charts already on screen are redrawn with the new token colours.
    function redrawForTheme() {
        if (!lastData || Object.keys(chartInstances).length === 0) return;
        if (!document.getElementById('gauge-discipline')) return;
        window.HrFormationCharts.renderCharts(lastData);
    }

    new MutationObserver(redrawForTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', redrawForTheme);
})();
