/**
 * PDF documents: booking invoice, shop order invoice, store order invoice / packing slip and the
 * partner's monthly statement. Prices on Super App include VAT, so every document shows the VAT
 * that is inside each amount (rate from Platform settings, 14% by default).
 */
(function (SA) {
  const { isoDate, fmtDate, fmtHour, fmtRange, seeded, MON } = SA.core.utils;
  const db = () => SA.services.db;
  const vatRate = () => db().settings().vat || 0;
  const vatIn = (gross) => gross - gross / (1 + vatRate());
  const m2 = (n) => `${(Math.round(n * 100) / 100).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} EGP`;
  const m0 = (n) => `${Math.round(n).toLocaleString('en-US')} EGP`;
  const invNo = (prefix, id, t) => { const d = new Date(t || Date.now()); return `${prefix}-${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}-${Math.floor(seeded(id) * 9e5 + 1e5)}`; };
  const HQ = () => db().find('accounts', 'acc-admin');

  const INK = '#0D1411', SOFT = '#46514B', TURF = '#0B6E4F', LINE = '#DCE0D9', TINT = '#EEF5F0', BLAZE = '#FF5B14';

  /** Header band + title block, returns y to continue from */
  const header = (p, { title, subtitle, number, date }) => {
    p.rect(0, 0, p.W, 92, { fill: INK });
    p.logo(40, 30, 30);
    p.text('SUPER APP', 80, 46, { size: 15, weight: 800, color: '#fff', spacing: 1 });
    p.text('Book · Play · Shop — Egypt', 80, 62, { size: 8.5, color: '#9FB3A9' });
    p.text(title, p.W - 40, 46, { size: 17, weight: 800, color: '#fff', align: 'right' });
    if (subtitle) p.text(subtitle, p.W - 40, 63, { size: 9, color: '#9FB3A9', align: 'right' });
    let y = 122;
    if (number) { p.text('Number', 40, y, { size: 8, color: SOFT }); p.text(number, 40, y + 14, { size: 10.5, weight: 700 }); }
    if (date) { p.text('Issue date', 200, y, { size: 8, color: SOFT }); p.text(date, 200, y + 14, { size: 10.5, weight: 700 }); }
    return y + 34;
  };
  const party = (p, x, y, label, lines) => {
    p.text(label.toUpperCase(), x, y, { size: 7.5, weight: 700, color: TURF, spacing: 0.8 });
    lines.filter(Boolean).forEach((l, i) => p.text(l, x, y + 15 + i * 13, { size: i === 0 ? 10.5 : 9, weight: i === 0 ? 700 : 400, color: i === 0 ? INK : SOFT, maxWidth: 240 }));
    return y + 15 + lines.filter(Boolean).length * 13;
  };
  const totalsBox = (p, y, rows, { x = p.W - 40 - 250, w = 250 } = {}) => {
    const h = rows.length * 18 + 14;
    p.rect(x, y, w, h, { fill: TINT, r: 6 });
    rows.forEach(([label, val, strong], i) => {
      const yy = y + 20 + i * 18;
      if (strong) p.line(x + 10, yy - 13, x + w - 10, yy - 13, { color: INK, lw: 0.8 });
      p.text(label, x + 12, yy, { size: strong ? 10 : 9, weight: strong ? 800 : 400, color: strong ? INK : SOFT });
      p.text(val, x + w - 12, yy, { size: strong ? 10.5 : 9, weight: strong ? 800 : 600, align: 'right' });
    });
    return y + h;
  };
  const footer = (p, n, total, note) => {
    p.line(40, p.H - 46, p.W - 40, p.H - 46);
    p.text(note || `Super App Egypt · ${HQ()?.email || ''} · Tax reg. no. ${HQ()?.taxId || '—'} (demo)`, 40, p.H - 30, { size: 7.5, color: SOFT, maxWidth: p.W - 140 });
    p.text(`Page ${n} of ${total}`, p.W - 40, p.H - 30, { size: 7.5, color: SOFT, align: 'right' });
  };
  const stamp = (p, text, color, x, y) => {
    p.ctx.save(); p.ctx.translate(x, y); p.ctx.rotate(-0.18);
    p.rect(-62, -18, 124, 34, { stroke: color, lw: 2, r: 6 });
    p.text(text, 0, 6, { size: 14, weight: 800, color, align: 'center', spacing: 2 });
    p.ctx.restore();
  };
  const finish = async (doc, pages, name, note) => {
    pages.forEach((p, i) => footer(p, i + 1, pages.length, note));
    await doc.save(name);
    SA.ui.overlay.toast(`Downloaded ${name}`, { type: 'success' });
  };

  /* ---------------- Booking invoice ---------------- */
  /**
   * @param b normalised booking: { id, pitchId, date, hour, duration, listPrice?, discountAmount?, amount, fee, total?, customer:{name,phone}, payment, status, refund, createdAt, source }
   */
  const bookingInvoice = async (b) => {
    const pitch = db().find('pitches', b.pitchId) || {};
    const owner = db().find('accounts', pitch.ownerId) || HQ();
    const doc = SA.core.pdf.create({ title: `Invoice ${b.id}` });
    const p = doc.page();
    const rate = vatRate();
    let y = header(p, { title: 'TAX INVOICE', subtitle: 'Pitch booking', number: invNo('INV', b.id, b.createdAt), date: fmtDate(new Date(b.createdAt || Date.now()), 'long') });
    p.text('Booking ID', 360, 122, { size: 8, color: SOFT }); p.text(b.id, 360, 136, { size: 10.5, weight: 700 });
    const yy = Math.max(
      party(p, 40, y + 6, 'Billed to', [b.customer?.name || 'Customer', b.customer?.phone || '', `Paid by ${b.payment || '—'}`]),
      party(p, 300, y + 6, 'Pitch supplied by', [owner.company, `${pitch.name || b.pitchName || ''}`, `${pitch.address || ''}${pitch.area ? `, ${pitch.area}` : ''}`, `Tax reg. no. ${owner.taxId || '—'} (demo)`]),
    );
    y = yy + 22;
    const list = b.listPrice || b.amount + (b.promo?.amount || 0);
    const rows = [[`Pitch rental — ${pitch.name || b.pitchName}`, `${fmtDate(b.date, 'long')} · ${fmtRange(b.hour, b.duration)}`, m2(list / (1 + rate)), m2(vatIn(list)), m2(list)]];
    if (b.discountAmount) rows.push([`Weekly booking discount (${Math.round((b.discount || 0) * 100)}%)`, 'Applied to the pitch price', `−${m2(b.discountAmount / (1 + rate))}`, `−${m2(vatIn(b.discountAmount))}`, `−${m2(b.discountAmount)}`]);
    if (b.promo?.amount) rows.push([`Venue promo code ${b.promo.code}`, 'Discount from the venue', `−${m2(b.promo.amount / (1 + rate))}`, `−${m2(vatIn(b.promo.amount))}`, `−${m2(b.promo.amount)}`]);
    if (b.fee) rows.push(['Super App service fee', 'Booking & secure payment', m2(b.fee / (1 + rate)), m2(vatIn(b.fee)), m2(b.fee)]);
    const total = (b.total != null ? b.total : b.amount + (b.fee || 0));
    y = p.table(40, y, [{ label: 'Description', w: 170 }, { label: 'Details', w: 130 }, { label: 'Excl. VAT', w: 75, align: 'right' }, { label: `VAT ${Math.round(rate * 100)}%`, w: 68, align: 'right' }, { label: 'Amount', w: 72, align: 'right' }], rows, { rowH: 22 });
    y += 16;
    const yt = totalsBox(p, y, [
      ['Subtotal (excl. VAT)', m2(total / (1 + rate))],
      [`VAT ${Math.round(rate * 100)}%`, m2(vatIn(total))],
      ['Total paid', m2(total), true],
      ...(b.status === 'cancelled' && b.refund ? [['Refunded', `−${m2(b.refund)}`]] : []),
    ]);
    // Check-in QR for games that haven't happened yet
    const start = SA.core.utils.parseISO(b.date).getTime() + b.hour * 36e5;
    if (b.status !== 'cancelled' && start + b.duration * 36e5 > Date.now() && b.source === 'app') {
      p.rect(40, y, 230, 112, { stroke: LINE, r: 8 });
      p.qr(SA.services.bookings.checkInCode(b.id), 52, y + 12, 88);
      p.text('Check-in code', 150, y + 30, { size: 10, weight: 700 });
      p.wrap('Show this at reception. Staff scan it in the Super App partner dashboard.', 150, y + 46, 110, { size: 8, color: SOFT, lineHeight: 11 });
    }
    if (b.status === 'cancelled') stamp(p, 'CANCELLED', '#C62828', 160, y + 50);
    else if (start + b.duration * 36e5 < Date.now()) stamp(p, 'PLAYED', TURF, 160, y + 50);
    y = Math.max(yt, y + 112) + 30;
    const pol = SA.services.pitches.policyOf(b.policy || pitch.cancelPolicy);
    p.text('Cancellation policy', 40, y, { size: 9, weight: 700 });
    y = p.wrap(`${pol.name}: ${pol.summary}`, 40, y + 14, p.W - 80, { size: 8.5, color: SOFT });
    p.wrap(`All prices include VAT at ${Math.round(rate * 100)}%. The pitch rental is supplied by ${owner.company}; the service fee is supplied by Super App Egypt. This is a demo document — tax registration numbers are placeholders.`, 40, y + 8, p.W - 80, { size: 8, color: SOFT });
    await finish(doc, [p], `super-app-invoice-${b.id}.pdf`);
  };

  /* ---------------- Shop order invoice (customer) ---------------- */
  const orderInvoice = async (o) => {
    const doc = SA.core.pdf.create({ title: `Invoice ${o.id}` });
    const p = doc.page();
    const rate = vatRate();
    const u = SA.core.store.get().user || {};
    let y = header(p, { title: 'TAX INVOICE', subtitle: 'Super App Shop order', number: invNo('INV', o.id, o.createdAt), date: fmtDate(new Date(o.createdAt), 'long') });
    p.text('Order', 360, 122, { size: 8, color: SOFT }); p.text(o.id, 360, 136, { size: 10.5, weight: 700 });
    y = Math.max(
      party(p, 40, y + 6, 'Billed to', [u.name || 'Customer', o.address?.line1 || '', `${o.address?.line2 ? `${o.address.line2}, ` : ''}${o.address?.city || ''}`, o.address?.phone || u.phone || '']),
      party(p, 300, y + 6, 'Sold through', ['Super App Egypt', `Payment: ${o.payment?.label || '—'}`, `Delivery: ${o.delivery === 'express' ? 'Express' : 'Standard'}`, `Tax reg. no. ${HQ()?.taxId || '—'} (demo)`]),
    ) + 22;
    const sellers = new Map((o.sellers || []).flatMap((g) => g.lines.map((l) => [l.index, g])));
    const rows = o.lines.map((l, i) => {
      const g = sellers.get(i);
      const gross = l.price * l.qty;
      return [`${l.name}${g?.status === 'cancelled' ? ' (cancelled)' : ''}`, `${g?.storeName || 'Super App Store'} · ${l.variant?.size || ''}`, String(l.qty), m2(gross / (1 + rate)), m2(vatIn(gross)), m2(gross)];
    });
    if (o.shipping) rows.push(['Delivery', o.delivery === 'express' ? 'Express' : 'Standard', '1', m2(o.shipping / (1 + rate)), m2(vatIn(o.shipping)), m2(o.shipping)]);
    if (o.couponOff) rows.push([`Discount code ${o.coupon}`, '', '', `−${m2(o.couponOff / (1 + rate))}`, `−${m2(vatIn(o.couponOff))}`, `−${m2(o.couponOff)}`]);
    y = p.table(40, y, [{ label: 'Item', w: 150 }, { label: 'Seller · size', w: 110 }, { label: 'Qty', w: 28, align: 'right' }, { label: 'Excl. VAT', w: 78, align: 'right' }, { label: `VAT ${Math.round(rate * 100)}%`, w: 72, align: 'right' }, { label: 'Amount', w: 77, align: 'right' }], rows, { rowH: 21 });
    y += 16;
    totalsBox(p, y, [['Subtotal (excl. VAT)', m2(o.total / (1 + rate))], [`VAT ${Math.round(rate * 100)}%`, m2(vatIn(o.total))], ['Total', m2(o.total), true], ...(o.cancelledValue ? [['Cancelled by seller (refunded)', `−${m2(o.cancelledValue)}`]] : [])]);
    p.wrap(`All prices include VAT at ${Math.round(rate * 100)}%. Items are sold by the store shown next to each line through Super App Egypt. Demo document — tax registration numbers are placeholders.`, 40, y + 120, 250, { size: 8, color: SOFT });
    await finish(doc, [p], `super-app-invoice-${o.id}.pdf`);
  };

  /* ---------------- Store order (partner): invoice + packing slip ---------------- */
  const storeOrderDoc = async (o) => {
    const st = db().find('stores', o.storeId);
    const owner = db().find('accounts', st?.ownerId) || HQ();
    const doc = SA.core.pdf.create({ title: `${o.orderId} — ${st?.name}` });
    const rate = vatRate();
    const p1 = doc.page();
    let y = header(p1, { title: 'TAX INVOICE', subtitle: st?.name, number: invNo('SINV', `${o.orderId}|${o.storeId}`, o.createdAt), date: fmtDate(new Date(o.createdAt), 'long') });
    p1.text('Order', 360, 122, { size: 8, color: SOFT }); p1.text(o.orderId, 360, 136, { size: 10.5, weight: 700 });
    y = Math.max(
      party(p1, 40, y + 6, 'Customer', [o.customer?.name || 'Customer', o.address?.line1 || '', `${o.address?.line2 ? `${o.address.line2}, ` : ''}${o.address?.city || o.customer?.city || ''}`, o.address?.phone || o.customer?.phone || '']),
      party(p1, 300, y + 6, 'Seller', [st?.name || 'Store', owner.company, st?.phone || owner.phone, `Tax reg. no. ${owner.taxId || '—'} (demo)`]),
    ) + 22;
    const rows = o.lines.map((l) => [l.name, `${l.variant?.size || '—'} · ${l.variant?.color || '—'}`, String(l.qty), m2(l.price), m2(vatIn(l.total)), m2(l.total)]);
    y = p1.table(40, y, [{ label: 'Item', w: 165 }, { label: 'Size · colour', w: 100 }, { label: 'Qty', w: 30, align: 'right' }, { label: 'Unit price', w: 70, align: 'right' }, { label: `VAT ${Math.round(rate * 100)}%`, w: 70, align: 'right' }, { label: 'Amount', w: 80, align: 'right' }], rows, { rowH: 21 });
    y += 16;
    totalsBox(p1, y, [['Subtotal (excl. VAT)', m2(o.gross / (1 + rate))], [`VAT ${Math.round(rate * 100)}%`, m2(vatIn(o.gross))], ['Total', m2(o.gross), true], ['Super App commission', `−${m2(o.commission)}`], ['Your net', m2(o.net)]]);
    p1.wrap(`Payment: ${o.payment}. Status: ${o.status}${o.courier ? ` · ${o.courier}${o.tracking ? ` ${o.tracking}` : ''}` : ''}.`, 40, y + 14, 240, { size: 8.5, color: SOFT });
    // Packing slip — goes in the box
    const p2 = doc.page();
    y = header(p2, { title: 'PACKING SLIP', subtitle: st?.name, number: o.orderId, date: fmtDate(new Date(), 'long') });
    p2.rect(40, y, p2.W - 80, 96, { stroke: INK, lw: 1.5, r: 8 });
    p2.text('SHIP TO', 56, y + 22, { size: 8, weight: 700, color: TURF, spacing: 1 });
    p2.text(o.customer?.name || 'Customer', 56, y + 44, { size: 16, weight: 800 });
    p2.text(`${o.address?.line1 || ''}${o.address?.line2 ? `, ${o.address.line2}` : ''}`, 56, y + 62, { size: 10, color: SOFT, maxWidth: 360 });
    p2.text(`${o.address?.city || o.customer?.city || ''} · ${o.address?.phone || o.customer?.phone || ''}`, 56, y + 78, { size: 10, color: SOFT });
    p2.qr(`SUPERAPP|ORD|${o.orderId}`, p2.W - 40 - 92, y + 10, 76);
    y += 120;
    y = p2.table(40, y, [{ label: '✓', w: 30 }, { label: 'Item', w: 260 }, { label: 'Size', w: 70 }, { label: 'Colour', w: 85 }, { label: 'Qty', w: 70, align: 'right' }], o.lines.map((l) => ['☐', l.name, l.variant?.size || '—', l.variant?.color || '—', String(l.qty)]), { rowH: 24, size: 10 });
    p2.wrap(`${o.units} item${o.units === 1 ? '' : 's'} · ${o.delivery === 'express' ? 'EXPRESS — next day' : 'Standard delivery'} · ${o.payment === 'Cash on delivery' ? `COLLECT ${m0(o.gross)} ON DELIVERY` : 'Already paid online'}`, 40, y + 24, p2.W - 80, { size: 11, weight: 800 });
    await finish(doc, [p1, p2], `super-app-${o.orderId}-${st?.slug || 'store'}.pdf`);
  };

  /* ---------------- Partner monthly statement ---------------- */
  const statement = async (st) => {
    const { account: a, s, daily, payouts } = st;
    const [y0, m0n] = st.month.split('-').map(Number);
    const label = `${['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'][m0n - 1]} ${y0}`;
    const rate = vatRate();
    const doc = SA.core.pdf.create({ title: `Statement ${label} — ${a.company}` });
    const p = doc.page();
    let y = header(p, { title: 'MONTHLY STATEMENT', subtitle: `${label}${st.partial ? ' (month to date)' : ''}`, number: invNo('ST', `${a.id}|${st.month}`, Date.now()), date: fmtDate(new Date(), 'long') });
    p.text('Period', 360, 122, { size: 8, color: SOFT }); p.text(`${fmtDate(st.from)} – ${fmtDate(st.to)}`, 360, 136, { size: 10.5, weight: 700 });
    y = Math.max(
      party(p, 40, y + 6, 'Partner', [a.company, a.name, a.email, `Tax reg. no. ${a.taxId || '—'} (demo)`]),
      party(p, 300, y + 6, 'Payout account', [a.payout?.method || 'Not set', a.payout?.detail || '', `Settlement every Monday`]),
    ) + 18;
    // KPI tiles
    const paid = payouts.filter((x) => x.status !== 'rejected').reduce((t, x) => t + x.amount, 0);
    const tiles = [['Net earnings', m0(s.net + s.storeNet), TURF], ['Bookings', `${s.bookings} · ${s.hours} h`, INK], ['Store orders', `${s.storeOrders} · ${s.storeUnits} units`, BLAZE], ['Paid out', m0(paid), INK]];
    const tw = (p.W - 80 - 24) / 4;
    tiles.forEach(([l, v, c], i) => { const x = 40 + i * (tw + 8); p.rect(x, y, tw, 52, { fill: TINT, r: 6 }); p.rect(x, y, 4, 52, { fill: c }); p.text(l, x + 12, y + 19, { size: 8, color: SOFT }); p.text(v, x + 12, y + 38, { size: 12, weight: 800, maxWidth: tw - 18 }); });
    y += 74;
    const sec = (t) => { p.text(t, 40, y, { size: 11, weight: 800 }); y += 10; };
    const commission = s.commission + s.storeCommission;
    sec('Pitch bookings');
    y = p.table(40, y, [{ label: 'Item', w: 380 }, { label: 'Amount', w: 135, align: 'right' }], [
      ['Gross pitch revenue (incl. VAT)', m2(s.gross)],
      [`   of which VAT ${Math.round(rate * 100)}% you declare`, m2(vatIn(s.gross))],
      ['Collected online by Super App', m2(s.online)],
      ['Collected by you at the venue (cash / card)', m2(s.cash)],
      ['Kept from late cancellations (policy)', m2(s.retained || 0)],
      ['Refunded to customers', m2(s.refunds)],
      [`Super App commission (${Math.round(SA.services.dash.commission().bookings * 100)}% of app bookings)`, `−${m2(s.commission)}`],
    ], { totals: ['Net from bookings', m2(s.net)] }) + 14;
    sec('Store sales');
    y = p.table(40, y, [{ label: 'Item', w: 380 }, { label: 'Amount', w: 135, align: 'right' }], [
      ['Gross sales (incl. VAT)', m2(s.storeGross)],
      [`   of which VAT ${Math.round(rate * 100)}% you declare`, m2(vatIn(s.storeGross))],
      [`Super App commission (${Math.round(SA.services.dash.commission().store * 100)}%)`, `−${m2(s.storeCommission)}`],
    ], { totals: ['Net from store', m2(s.storeNet)] }) + 14;
    sec('Super App commission — tax invoice to you');
    y = p.table(40, y, [{ label: 'Item', w: 380 }, { label: 'Amount', w: 135, align: 'right' }], [
      ['Commission (excl. VAT)', m2(commission / (1 + rate))],
      [`VAT ${Math.round(rate * 100)}% on commission (input VAT you can reclaim)`, m2(vatIn(commission))],
    ], { totals: ['Total commission', m2(commission)] }) + 14;
    totalsBox(p, y, [['Net from bookings', m2(s.net)], ['Net from store', m2(s.storeNet)], ['Net earnings', m2(s.net + s.storeNet), true]], { x: 40, w: 515 });
    // Page 2 — daily and payouts
    const pages = [p];
    let q = doc.page(); pages.push(q);
    let y2 = header(q, { title: 'MONTHLY STATEMENT', subtitle: `${label} · details` }) - 20;
    q.text('Daily earnings', 40, y2, { size: 11, weight: 800 }); y2 += 10;
    const sum = (k) => daily.reduce((t, d) => t + d[k], 0);
    y2 = q.table(40, y2, [{ label: 'Date', w: 120 }, { label: 'Bookings', w: 70, align: 'right' }, { label: 'Hours', w: 60, align: 'right' }, { label: 'Bookings net', w: 90, align: 'right' }, { label: 'Store net', w: 85, align: 'right' }, { label: 'Total net', w: 90, align: 'right' }],
      daily.map((d) => [fmtDate(d.date), String(d.bookings), String(d.hours), m0(d.bookingsNet), m0(d.storeNet), m0(d.bookingsNet + d.storeNet)]),
      { rowH: 15.5, size: 8, onPageBreak: () => { q = doc.page(); pages.push(q); return q; }, totals: ['Total', String(sum('bookings')), String(sum('hours')), m0(sum('bookingsNet')), m0(sum('storeNet')), m0(sum('bookingsNet') + sum('storeNet'))] });
    q = pages[pages.length - 1];
    if (y2 + 60 + (payouts.length + 1) * 17 > q.H - 60) { q = doc.page(); pages.push(q); y2 = 60; }
    y2 += 18;
    q.text('Payouts in this period', 40, y2, { size: 11, weight: 800 }); y2 += 10;
    if (payouts.length) q.table(40, y2, [{ label: 'Date', w: 95 }, { label: 'Type', w: 120 }, { label: 'Sent to', w: 170 }, { label: 'Status', w: 60 }, { label: 'Amount', w: 70, align: 'right' }], payouts.map((x) => [fmtDate(new Date(x.createdAt)), x.auto ? 'Weekly settlement' : 'Withdrawal', `${x.method} ${x.detail}`, x.status, m0(x.amount)]), { rowH: 17, totals: ['Total', '', '', '', m0(paid)] });
    else q.text('No payouts in this period.', 40, y2 + 16, { size: 9, color: SOFT });
    await finish(doc, pages, `super-app-statement-${st.month}-${(a.company || 'partner').toLowerCase().replace(/[^a-z0-9]+/g, '-')}.pdf`,
      `All amounts include VAT at ${Math.round(rate * 100)}% where applicable. Booking line details are in the Excel finance report. Demo document.`);
  };

  SA.ui.docs = { bookingInvoice, orderInvoice, storeOrderDoc, statement, vatIn };
})(window.SA);
