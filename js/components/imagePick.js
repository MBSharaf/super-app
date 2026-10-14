/**
 * Pick an image from the device, crop it to an aspect ratio (centre) and shrink it for storage.
 * SA.ui.pickImage({ aspect: 1, max: 400, type: 'logo' }) → Promise<dataURL | null>
 * Logos keep transparency (WebP/PNG); covers are saved as JPEG. Files go to the CDN when the backend exists.
 */
(function (SA) {
  const MAX_FILE = 10 * 1024 * 1024;
  const read = (file, { aspect = 0, max = 1600, alpha = false } = {}) => new Promise((resolve, reject) => {
    if (!/^image\/(png|jpeg|webp|gif|svg\+xml)$/.test(file.type)) return reject(new Error('Choose a PNG, JPG or WebP image.'));
    if (file.size > MAX_FILE) return reject(new Error('That image is over 10 MB. Choose a smaller one.'));
    const fr = new FileReader();
    fr.onerror = () => reject(new Error('We couldn’t read that file.'));
    fr.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error('That image looks damaged.'));
      img.onload = () => {
        let sw = img.naturalWidth || 800, sh = img.naturalHeight || 800, sx = 0, sy = 0;
        if (aspect) { if (sw / sh > aspect) { const w = sh * aspect; sx = (sw - w) / 2; sw = w; } else { const h = sw / aspect; sy = (sh - h) / 2; sh = h; } }
        const k = Math.min(1, max / Math.max(sw, sh));
        const c = document.createElement('canvas'); c.width = Math.round(sw * k); c.height = Math.round(sh * k);
        c.getContext('2d').drawImage(img, sx, sy, sw, sh, 0, 0, c.width, c.height);
        let url = alpha ? c.toDataURL('image/webp', 0.9) : c.toDataURL('image/jpeg', 0.84);
        if (alpha && !url.startsWith('data:image/webp')) url = c.toDataURL('image/png');
        resolve(url);
      };
      img.src = fr.result;
    };
    fr.readAsDataURL(file);
  });

  /** Opens the file chooser; resolves null if cancelled */
  SA.ui.pickImage = (opts = {}) => new Promise((resolve, reject) => {
    const inp = document.createElement('input');
    inp.type = 'file'; inp.accept = 'image/png,image/jpeg,image/webp';
    inp.addEventListener('change', () => { const f = inp.files[0]; if (!f) return resolve(null); read(f, opts).then(resolve, reject); });
    inp.click();
  });
  SA.ui.readImage = read;
})(window.SA);
