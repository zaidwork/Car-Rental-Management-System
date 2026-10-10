/**
 * TAJ / SAMARA SYSTEM - Isolated Direct Print Helper
 * Ensures 100% faithful printouts without background page leakage,
 * with exact RTL direction, fonts, and custom paper sizes (A4 / 80mm).
 * Properly handles 2-sided duplex A4 printing (Front & Back on 1 physical paper).
 */
window.printDocumentArea = function (elementId, format) {
    format = format || 'A4';
    var sourceEl = document.getElementById(elementId);
    if (!sourceEl) {
        console.error('Print element not found:', elementId);
        window.print();
        return;
    }

    // Remove any existing print frame
    var existingFrame = document.getElementById('taj-print-frame');
    if (existingFrame) {
        existingFrame.remove();
    }

    // Create a new hidden iframe
    var iframe = document.createElement('iframe');
    iframe.id = 'taj-print-frame';
    iframe.style.position = 'fixed';
    iframe.style.right = '0';
    iframe.style.bottom = '0';
    iframe.style.width = '0';
    iframe.style.height = '0';
    iframe.style.border = '0';
    iframe.style.visibility = 'hidden';
    document.body.appendChild(iframe);

    var doc = iframe.contentWindow.document;
    doc.open();

    var is80mm = (format === '80mm');
    var pageRule = is80mm
        ? '@page { size: 80mm auto; margin: 0; } body { width: 76mm; margin: 0 auto; padding: 2mm 1mm; }'
        : '@page { size: A4 portrait; margin: 5mm 6mm; } body { width: 100%; margin: 0 auto; padding: 0; }';

    // Clone element and strictly remove any screen-only .no-print elements
    var clone = sourceEl.cloneNode(true);
    var noPrintEls = clone.querySelectorAll('.no-print');
    for (var n = 0; n < noPrintEls.length; n++) {
        noPrintEls[n].remove();
    }

    var htmlContent = '<!DOCTYPE html>' +
        '<html lang="ar-EG" dir="rtl">' +
        '<head>' +
        '<meta charset="utf-8" />' +
        '<title>طباعة وثيقة رسمية</title>' +
        '<link rel="preconnect" href="https://fonts.googleapis.com">' +
        '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>' +
        '<link href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;500;600;700;800;900&family=Tajawal:wght@400;500;700;800&display=swap" rel="stylesheet">' +
        '<link rel="stylesheet" href="css/app.css" />' +
        '<style>' +
        pageRule +
        ' * { box-sizing: border-box !important; -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }' +
        ' html, body { background: #ffffff !important; color: #000000 !important; direction: rtl !important; text-align: right !important; margin: 0 !important; padding: 0 !important; font-family: "Cairo", "Tajawal", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important; }' +
        ' .no-print { display: none !important; }' +
        ' .printable-document { margin: 0 auto !important; color: #000000 !important; background: #ffffff !important; box-shadow: none !important; }' +
        
        ' .contract-a4-sheet {' +
        '   width: 100% !important;' +
        '   max-width: 198mm !important;' +
        '   height: 278mm !important;' +
        '   max-height: 278mm !important;' +
        '   min-height: 270mm !important;' +
        '   box-sizing: border-box !important;' +
        '   margin: 0 auto !important;' +
        '   padding: 3mm 5mm !important;' +
        '   border: 2px solid #000000 !important;' +
        '   border-radius: 6px !important;' +
        '   background: #ffffff !important;' +
        '   color: #000000 !important;' +
        '   page-break-inside: avoid !important;' +
        '   break-inside: avoid !important;' +
        '   overflow: hidden !important;' +
        '   display: flex !important;' +
        '   flex-direction: column !important;' +
        '   justify-content: space-between !important;' +
        ' }' +

        ' .contract-both-front {' +
        '   page-break-after: page !important;' +
        '   break-after: page !important;' +
        '   page-break-inside: avoid !important;' +
        '   break-inside: avoid !important;' +
        ' }' +

        ' .contract-both-back {' +
        '   page-break-before: auto !important;' +
        '   break-before: auto !important;' +
        '   page-break-after: avoid !important;' +
        '   break-after: avoid !important;' +
        '   page-break-inside: avoid !important;' +
        '   break-inside: avoid !important;' +
        ' }' +

        ' .official-promissory-sheet, .official-inspection-sheet, .official-voucher-sheet { border: 2px solid #000000 !important; page-break-inside: avoid !important; break-inside: avoid !important; }' +
        ' .thermal-receipt-sheet { border: none !important; box-shadow: none !important; width: 76mm !important; max-width: 76mm !important; margin: 0 auto !important; }' +
        ' table, th, td { border-color: #000000 !important; }' +
        '</style>' +
        '</head>' +
        '<body dir="rtl">' +
        clone.innerHTML +
        '</body>' +
        '</html>';

    doc.write(htmlContent);
    doc.close();

    function triggerPrint() {
        try {
            iframe.contentWindow.focus();
            iframe.contentWindow.print();
        } catch (e) {
            console.error('Print trigger error:', e);
        }
        setTimeout(function () {
            iframe.remove();
        }, 5000);
    }

    // Wait for images to load before printing
    var images = doc.images;
    var totalImages = images ? images.length : 0;
    var loadedCount = 0;

    if (totalImages === 0) {
        setTimeout(triggerPrint, 300);
    } else {
        var fallbackTimeout = setTimeout(triggerPrint, 1200);
        for (var i = 0; i < totalImages; i++) {
            if (images[i].complete) {
                loadedCount++;
                if (loadedCount === totalImages) {
                    clearTimeout(fallbackTimeout);
                    setTimeout(triggerPrint, 250);
                }
            } else {
                images[i].addEventListener('load', function () {
                    loadedCount++;
                    if (loadedCount === totalImages) {
                        clearTimeout(fallbackTimeout);
                        setTimeout(triggerPrint, 250);
                    }
                });
                images[i].addEventListener('error', function () {
                    loadedCount++;
                    if (loadedCount === totalImages) {
                        clearTimeout(fallbackTimeout);
                        setTimeout(triggerPrint, 250);
                    }
                });
            }
        }
    }
};
