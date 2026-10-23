// hr-orgchart.js - Interactive Pan and Zoom for Organizational Hierarchy Chart
window.hrOrgChart = {
    init: function (viewportId, canvasId, dotNetHelper) {
        const viewport = document.getElementById(viewportId);
        const canvas = document.getElementById(canvasId);
        if (!viewport) return;

        // Cleanup any existing listeners
        if (viewport._hrCleanup) {
            try { viewport._hrCleanup(); } catch (e) { }
        }

        let isDown = false;
        let startX = 0;
        let startY = 0;
        let scrollLeft = 0;
        let scrollTop = 0;
        let isDragging = false;

        const onMouseDown = function (e) {
            // Ignore if clicking interactive controls, buttons, links, inputs
            if (e.target.closest('button, a, input, select, textarea, [data-no-drag]')) {
                return;
            }
            if (e.button !== 0) return; // Only main button

            isDown = true;
            isDragging = false;
            startX = e.clientX;
            startY = e.clientY;
            scrollLeft = viewport.scrollLeft;
            scrollTop = viewport.scrollTop;

            viewport.style.cursor = 'grabbing';
            viewport.style.userSelect = 'none';
        };

        const onMouseMove = function (e) {
            if (!isDown) return;
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;

            if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
                isDragging = true;
            }

            viewport.scrollLeft = scrollLeft - dx;
            viewport.scrollTop = scrollTop - dy;
        };

        const onMouseUp = function (e) {
            if (!isDown) return;
            isDown = false;
            viewport.style.cursor = 'grab';
            viewport.style.userSelect = '';
        };

        const onWheel = function (e) {
            // Zoom with wheel
            e.preventDefault();
            if (!dotNetHelper) return;
            const delta = e.deltaY < 0 ? 0.08 : -0.08;
            dotNetHelper.invokeMethodAsync('OnJsWheelZoom', delta);
        };

        const onDblClick = function (e) {
            if (e.target.closest('.hr-org-card, button, a')) return;
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('ResetZoom');
            }
        };

        viewport.style.cursor = 'grab';
        viewport.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
        viewport.addEventListener('wheel', onWheel, { passive: false });
        viewport.addEventListener('dblclick', onDblClick);

        viewport._hrCleanup = function () {
            viewport.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
            viewport.removeEventListener('wheel', onWheel);
            viewport.removeEventListener('dblclick', onDblClick);
        };
    },

    destroy: function (viewportId) {
        const viewport = document.getElementById(viewportId);
        if (viewport && viewport._hrCleanup) {
            try { viewport._hrCleanup(); } catch (e) { }
            delete viewport._hrCleanup;
        }
    }
};
