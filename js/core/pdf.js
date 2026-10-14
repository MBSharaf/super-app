/**
 * Small dependency-free PDF writer. Each A4 page is painted on a canvas (so Arabic and every
 * font the browser can render just work) and embedded as a high-resolution JPEG.
 *
 *   const doc = SA.core.pdf.create({ title });
 *   const p = doc.page();            // painter in PDF points (595 × 842), origin top-left
 *   p.text('Hello', 40, 60, { size: 18, weight: 700 });
 *   await doc.save('file.pdf');
 */
(function (SA) {
  const W = 595.28, H = 841.89, K = 2.5; // ~180 dpi
  const FONT = '"Archivo", "Segoe UI", Tahoma, Arial, sans-serif';
  const AR = /[؀-ۿ]/;

  const painter = (canvas) => {
    const ctx = canvas.getContext('2d');
    ctx.scale(K, K);
    ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, W, H);
    ctx.textBaseline = 'alphabetic';
    const font = ({ size = 10, weight = 400 }) => `${weight} ${size}px ${FONT}`;
    const p = {
      ctx, W, H,
      text(str, x, y, o = {}) {
        const s = String(str ?? '');
        ctx.save();
        ctx.font = font(o); ctx.fillStyle = o.color || '#0D1411';
        ctx.direction = AR.test(s) ? 'rtl' : 'ltr';
        ctx.textAlign = o.align || 'left';
        let out = s;
        if (o.maxWidth && ctx.measureText(out).width > o.maxWidth) {
          while (out.length > 1 && ctx.measureText(out + '…').width > o.maxWidth) out = out.slice(0, -1);
          out += '…';
        }
        if (o.spacing) ctx.letterSpacing = `${o.spacing}px`;
        ctx.fillText(out, x, y);
        ctx.restore();
      },
      measure(str, o = {}) { ctx.save(); ctx.font = font(o); const w = ctx.measureText(String(str)).width; ctx.restore(); return w; },
      /** Wrapped paragraph; returns the y after the last line */
      wrap(str, x, y, width, o = {}) {
        const words = String(str || '').split(/\s+/);
        const lh = o.lineHeight || (o.size || 10) * 1.45;
        let line = '';
        words.forEach((w) => {
          const t = line ? `${line} ${w}` : w;
          if (p.measure(t, o) > width && line) { p.text(line, x, y, o); y += lh; line = w; } else line = t;
        });
        if (line) { p.text(line, x, y, o); y += lh; }
        return y;
      },
      rect(x, y, w, h, { fill, stroke, lw = 1, r = 0 } = {}) {
        ctx.save(); ctx.beginPath();
        if (r) ctx.roundRect(x, y, w, h, r); else ctx.rect(x, y, w, h);
        if (fill) { ctx.fillStyle = fill; ctx.fill(); }
        if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lw; ctx.stroke(); }
        ctx.restore();
      },
      line(x1, y1, x2, y2, { color = '#DCE3DE', lw = 0.75, dash } = {}) {
        ctx.save(); ctx.strokeStyle = color; ctx.lineWidth = lw; if (dash) ctx.setLineDash(dash);
        ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke(); ctx.restore();
      },
      image(img, x, y, w, h) { try { ctx.drawImage(img, x, y, w, h); } catch { /* tainted or broken image: skip */ } },
      /** Super App brand mark */
      logo(x, y, s = 26) {
        ctx.save();
        const g = ctx.createLinearGradient(x, y, x + s, y + s); g.addColorStop(0, '#FF5B14'); g.addColorStop(1, '#E54A07');
        ctx.fillStyle = g; ctx.beginPath(); ctx.roundRect(x, y, s, s, s * 0.28); ctx.fill();
        ctx.translate(x + s * 0.12, y + s * 0.12); ctx.scale(s / 31, s / 31);
        ctx.strokeStyle = '#fff'; ctx.lineWidth = 2.6; ctx.lineCap = 'round';
        ctx.stroke(new Path2D('M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2'));
        ctx.restore();
      },
      qr(text, x, y, size) { SA.core.qr.draw(ctx, text, x, y, size); },
      /**
       * Simple table. cols: [{ label, w, align }]; rows: arrays of strings.
       * Returns the y after the table. Calls onPageBreak() → new painter when it runs out of room.
       */
      table(x, y, cols, rows, { size = 8.5, rowH = 17, head = '#0D1411', zebra = '#F4F7F5', bottom = H - 60, onPageBreak, totals } = {}) {
        let pg = p;
        const drawHead = (q, yy) => {
          q.rect(x, yy, cols.reduce((t, c) => t + c.w, 0), rowH + 2, { fill: head, r: 3 });
          let cx = x;
          cols.forEach((c) => { q.text(c.label, c.align === 'right' ? cx + c.w - 6 : cx + 6, yy + rowH - 4.5, { size: size - 0.5, weight: 700, color: '#fff', align: c.align === 'right' ? 'right' : 'left', maxWidth: c.w - 10 }); cx += c.w; });
          return yy + rowH + 2;
        };
        y = drawHead(pg, y);
        rows.forEach((r, i) => {
          if (y + rowH > bottom && onPageBreak) { pg = onPageBreak(); y = drawHead(pg, 60); }
          if (i % 2 === 1) pg.rect(x, y, cols.reduce((t, c) => t + c.w, 0), rowH, { fill: zebra });
          let cx = x;
          cols.forEach((c, j) => { pg.text(r[j], c.align === 'right' ? cx + c.w - 6 : cx + 6, y + rowH - 5.5, { size, color: '#26332D', align: c.align === 'right' ? 'right' : 'left', maxWidth: c.w - 10, weight: c.bold ? 600 : 400 }); cx += c.w; });
          y += rowH;
        });
        if (totals) {
          const tw = cols.reduce((t, c) => t + c.w, 0);
          pg.line(x, y, x + tw, y, { color: '#0D1411', lw: 1 });
          let cx = x;
          cols.forEach((c, j) => { if (totals[j] != null && totals[j] !== '') pg.text(totals[j], c.align === 'right' ? cx + c.w - 6 : cx + 6, y + rowH - 4, { size, weight: 700, align: c.align === 'right' ? 'right' : 'left', maxWidth: c.w - 10 }); cx += c.w; });
          y += rowH + 2;
        }
        p._last = pg;
        return y;
      },
    };
    return p;
  };

  const enc = new TextEncoder();
  const pdfString = (s) => `(${String(s).replace(/[^\x20-\x7E]/g, '').replace(/([\\()])/g, '\\$1')})`;

  const create = ({ title = 'Super App document', author = 'Super App' } = {}) => {
    const pages = [];
    const doc = {
      page() {
        const c = document.createElement('canvas');
        c.width = Math.round(W * K); c.height = Math.round(H * K);
        pages.push(c);
        return painter(c);
      },
      get count() { return pages.length; },
      async blob() {
        if (document.fonts?.load) { try { await Promise.all([document.fonts.load(`400 10px "Archivo"`), document.fonts.load(`700 10px "Archivo"`)]); } catch { /* fallback font */ } }
        const jpgs = await Promise.all(pages.map((c) => new Promise((res) => c.toBlob((b) => res(b), 'image/jpeg', 0.9)).then((b) => b.arrayBuffer()).then((a) => new Uint8Array(a))));
        const parts = []; const offsets = []; let len = 0;
        const push = (x) => { const u = typeof x === 'string' ? enc.encode(x) : x; parts.push(u); len += u.length; };
        const obj = (n, body) => { offsets[n] = len; push(`${n} 0 obj\n`); body(); push('\nendobj\n'); };
        push('%PDF-1.4\n%\xE2\xE3\xCF\xD3\n');
        const n = pages.length;
        // 1 catalog, 2 pages, 3 info, then per page: page, content, image
        const pageObj = (i) => 4 + i * 3;
        obj(1, () => push('<< /Type /Catalog /Pages 2 0 R >>'));
        obj(2, () => push(`<< /Type /Pages /Count ${n} /Kids [${pages.map((_, i) => `${pageObj(i)} 0 R`).join(' ')}] >>`));
        obj(3, () => push(`<< /Title ${pdfString(title)} /Author ${pdfString(author)} /Producer (Super App) /CreationDate (D:${new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14)}) >>`));
        pages.forEach((c, i) => {
          const po = pageObj(i);
          obj(po, () => push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${W} ${H}] /Resources << /XObject << /Im${i} ${po + 2} 0 R >> >> /Contents ${po + 1} 0 R >>`));
          const content = `q ${W} 0 0 ${H} 0 0 cm /Im${i} Do Q`;
          obj(po + 1, () => push(`<< /Length ${content.length} >>\nstream\n${content}\nendstream`));
          obj(po + 2, () => { push(`<< /Type /XObject /Subtype /Image /Width ${c.width} /Height ${c.height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpgs[i].length} >>\nstream\n`); push(jpgs[i]); push('\nendstream'); });
        });
        const total = 4 + n * 3;
        const xref = len;
        push(`xref\n0 ${total}\n0000000000 65535 f \n`);
        for (let i = 1; i < total; i++) push(`${String(offsets[i]).padStart(10, '0')} 00000 n \n`);
        push(`trailer\n<< /Size ${total} /Root 1 0 R /Info 3 0 R >>\nstartxref\n${xref}\n%%EOF`);
        return new Blob(parts, { type: 'application/pdf' });
      },
      async save(filename) {
        const b = await doc.blob();
        const a = document.createElement('a');
        a.href = URL.createObjectURL(b); a.download = filename;
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(a.href), 4000);
        return b;
      },
    };
    return doc;
  };

  SA.core.pdf = { create, W, H };
})(window.SA);
