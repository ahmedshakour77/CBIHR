/* passport-ocr.js: Client-side Passport MRZ OCR Reader for API.HR.Node
   Conforms to ICAO Doc 9303 TD3 (2 lines x 44 characters).
   Uses 100% locally-hosted Tesseract.js assets (no external CDN required). */
(function () {
  'use strict';

  var LOCAL_TESSERACT_SCRIPT = '/lib/tesseract/tesseract.min.js';
  var LOCAL_WORKER_PATH = '/lib/tesseract/worker.min.js';
  var LOCAL_CORE_SIMD_PATH = '/lib/tesseract/tesseract-core-simd-lstm.wasm.js';
  var LOCAL_CORE_LSTM_PATH = '/lib/tesseract/tesseract-core-lstm.wasm.js';
  var LOCAL_LANG_PATH = '/lib/tesseract/tessdata';

  function cleanMrzLine(raw) {
    if (!raw) return '';
    return raw
      .toUpperCase()
      .replace(/[«]/g, '<')
      .replace(/[>\(\)\[\]\{\}]/g, '<')
      .replace(/[\s]/g, '')
      .replace(/[^A-Z0-9<]/g, '');
  }

  function findMrzLines(lines) {
    var cleaned = lines.map(cleanMrzLine).filter(function (l) { return l.length >= 30; });

    // Look for Line 1 starting with P< or P
    for (var i = 0; i < cleaned.length; i++) {
      var l1 = cleaned[i];
      if ((l1.startsWith('P<') || l1.startsWith('P')) && i + 1 < cleaned.length) {
        var l2 = cleaned[i + 1];
        // Line 2 should have digits (dates, passport number)
        var digitCount = (l2.match(/[0-9]/g) || []).length;
        if (digitCount >= 8) {
          return { line1: l1, line2: l2 };
        }
      }
    }

    // Fallback: look for any two consecutive lines that are 38+ chars with digits in line 2
    for (var j = 0; j < cleaned.length - 1; j++) {
      var a = cleaned[j];
      var b = cleaned[j + 1];
      if (a.length >= 38 && b.length >= 38) {
        var digitsB = (b.match(/[0-9]/g) || []).length;
        if (digitsB >= 8) {
          return { line1: a, line2: b };
        }
      }
    }

    return null;
  }

  async function ensureTesseractLoaded() {
    if (window.Tesseract) return;

    if (window.hrLoadScript) {
      await window.hrLoadScript(LOCAL_TESSERACT_SCRIPT);
    } else {
      await new Promise(function (resolve, reject) {
        var s = document.createElement('script');
        s.src = LOCAL_TESSERACT_SCRIPT;
        s.onload = resolve;
        s.onerror = function () {
          reject(new Error('فشل تحميل مكتبة المسح الضوئي المحلية من ' + LOCAL_TESSERACT_SCRIPT));
        };
        document.head.appendChild(s);
      });
    }

    if (!window.Tesseract) {
      throw new Error('تعذر تهيئة مكتبة Tesseract في المتصفح.');
    }
  }

  async function createLocalWorker() {
    try {
      return await window.Tesseract.createWorker('eng', 1, {
        workerPath: LOCAL_WORKER_PATH,
        corePath: LOCAL_CORE_SIMD_PATH,
        langPath: LOCAL_LANG_PATH,
        gzip: true
      });
    } catch (simdErr) {
      console.warn('SIMD core failed, trying standard LSTM core fallback:', simdErr);
      return await window.Tesseract.createWorker('eng', 1, {
        workerPath: LOCAL_WORKER_PATH,
        corePath: LOCAL_CORE_LSTM_PATH,
        langPath: LOCAL_LANG_PATH,
        gzip: true
      });
    }
  }

  window.scanPassportImageMrz = async function (dataUrl) {
    var worker = null;
    try {
      await ensureTesseractLoaded();

      // Load image into HTML Image object
      var img = new Image();
      await new Promise(function (resolve, reject) {
        img.onload = resolve;
        img.onerror = function () {
          reject(new Error('تعذر قراءة ملف الصورة المحدد. يرجى اختيار ملف صورة صالح (JPG/PNG).'));
        };
        img.src = dataUrl;
      });

      // Passports have MRZ in the lower 32-35% of the page
      var canvas = document.createElement('canvas');
      var cropTop = Math.floor(img.height * 0.65);
      var cropHeight = img.height - cropTop;

      canvas.width = img.width;
      canvas.height = cropHeight;

      var ctx = canvas.getContext('2d');
      ctx.drawImage(img, 0, cropTop, img.width, cropHeight, 0, 0, canvas.width, canvas.height);

      // Contrast enhancement & binarization for OCR-B
      try {
        var imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        var d = imgData.data;
        for (var i = 0; i < d.length; i += 4) {
          var gray = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
          var val = gray > 140 ? 255 : 0;
          d[i] = val;
          d[i + 1] = val;
          d[i + 2] = val;
        }
        ctx.putImageData(imgData, 0, 0);
      } catch (canvasErr) {
        console.warn('Canvas filter failed, proceeding with raw crop:', canvasErr);
      }

      worker = await createLocalWorker();

      await worker.setParameters({
        tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<'
      });

      // 1. Recognize cropped MRZ first
      var res = await worker.recognize(canvas);
      var text = res && res.data && res.data.text ? res.data.text : '';
      var lines = text.split(/\r?\n/);
      var mrzMatch = findMrzLines(lines);

      // 2. If bottom crop didn't find MRZ, scan full image as fallback
      if (!mrzMatch) {
        var fullRes = await worker.recognize(img);
        var fullText = fullRes && fullRes.data && fullRes.data.text ? fullRes.data.text : '';
        var fullLines = fullText.split(/\r?\n/);
        mrzMatch = findMrzLines(fullLines);
      }

      if (mrzMatch) {
        return {
          success: true,
          line1: mrzMatch.line1,
          line2: mrzMatch.line2,
          mrz: mrzMatch.line1 + '\n' + mrzMatch.line2
        };
      }

      return {
        success: false,
        error: 'لم يتم العثور على شريط القراءة الآلية (MRZ) بوضوح في الصورة. يرجى التأكد من وضوح السطرين في أسفل صفحة الجواز، أو إدخالهما يدوياً.'
      };
    } catch (err) {
      console.error('Passport OCR error:', err);
      return {
        success: false,
        error: 'تعذر تشغيل المعالجة الضوئية للصورة: ' + (err.message || err)
      };
    } finally {
      if (worker) {
        try {
          await worker.terminate();
        } catch (termErr) {
          console.warn('Error terminating Tesseract worker:', termErr);
        }
      }
    }
  };
})();
