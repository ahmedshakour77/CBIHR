/**
 * Portal Dossier PDF Export Helper
 * Converts institutional employee dossier to high-quality A4 PDF using html2pdf.js
 */
/* The html2pdf bundle is ~900 KB. It is fetched the first time an export
   runs, not on every page of the application. */
var html2pdfLoading = null;
function loadHtml2Pdf() {
    if (typeof html2pdf !== "undefined") return Promise.resolve();
    if (!html2pdfLoading) {
        html2pdfLoading = new Promise(function (resolve, reject) {
            var s = document.createElement("script");
            s.src = "js/html2pdf.bundle.min.js";
            s.onload = function () { resolve(); };
            s.onerror = function () { html2pdfLoading = null; reject("html2pdf library failed to load"); };
            document.head.appendChild(s);
        });
    }
    return html2pdfLoading;
}

window.downloadDossierPdf = function (elementId, filename) {
    return loadHtml2Pdf().then(function () { return exportDossierPdf(elementId, filename); });
};

function exportDossierPdf(elementId, filename) {
    return new Promise(function (resolve, reject) {
        try {
            var element = document.getElementById(elementId);
            if (!element) {
                console.error('PDF element not found:', elementId);
                reject('Element not found');
                return;
            }

            if (typeof html2pdf === 'undefined') {
                console.error('html2pdf library is not loaded');
                reject('html2pdf library not loaded');
                return;
            }

            var cleanFilename = (filename || 'employee-dossier.pdf')
                .replace(/[/\\?%*:|"<>]/g, '-');

            var opt = {
                margin:       [8, 8, 8, 8], // 8mm margin
                filename:     cleanFilename,
                image:        { type: 'jpeg', quality: 0.98 },
                html2canvas:  { 
                    scale: 2, 
                    useCORS: true, 
                    letterRendering: true,
                    logging: false,
                    scrollX: 0,
                    scrollY: 0
                },
                jsPDF: { 
                    unit: 'mm', 
                    format: 'a4', 
                    orientation: 'portrait' 
                },
                pagebreak: { mode: ['avoid-all', 'css', 'legacy'] }
            };

            html2pdf()
                .set(opt)
                .from(element)
                .save()
                .then(function () {
                    resolve(true);
                })
                .catch(function (err) {
                    console.error('PDF generation error:', err);
                    reject(err);
                });
        } catch (e) {
            console.error('PDF export unexpected exception:', e);
            reject(e);
        }
    });
}

/**
 * Copy text to clipboard helper
 */
window.copyTextToClipboard = function (text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text);
    }
    var textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.left = "-9999px";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    try {
        document.execCommand('copy');
        document.body.removeChild(textArea);
        return Promise.resolve(true);
    } catch (err) {
        document.body.removeChild(textArea);
        return Promise.reject(err);
    }
};

/**
 * Downloads a binary file provided as base64 string
 */
window.downloadBase64File = function (contentType, base64Data, filename) {
    try {
        var byteCharacters = atob(base64Data);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: contentType });
        var url = URL.createObjectURL(blob);
        var downloadLink = document.createElement('a');
        downloadLink.href = url;
        downloadLink.download = filename || 'document.pdf';
        document.body.appendChild(downloadLink);
        downloadLink.click();
        setTimeout(function () {
            document.body.removeChild(downloadLink);
            URL.revokeObjectURL(url);
        }, 200);
        return true;
    } catch (err) {
        console.error('Failed to download base64 file:', err);
        return false;
    }
};
