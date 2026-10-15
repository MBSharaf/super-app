/** CEO system: every report, for any dates — on screen, as Excel (with a live Dashboard sheet) and as PDF. */
(function (SA) {
  const { esc, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const D = SA.pages.dashCommon;
  const CE = () => SA.services.ceo;

  const cell = (v, type) => (type === 'money' ? C.egp(v) : type === 'date' && v ? fmtDate(v) : type === 'n' ? (+v || 0).toLocaleString() : esc(String(v ?? '')));

  /** Excel: a Dashboard sheet (KPIs + grouped totals with native charts) over the data sheet */
  const excel = (kind, label, rep, r) => {
    const cols = rep.columns;
    const moneyCol = cols.find(([k]) => k === (rep.chart || 'amount')) || cols.find(([, , t]) => t === 'money');
    const sheets = SA.core.xlsx.dashboard({
      title: `Super App — ${label} report`, subtitle: `${r.from} to ${r.to} · Command Center`,
      data: { name: label.slice(0, 28), columns: cols.map(([, h, t]) => ({ header: h, width: t === 'money' ? 16 : Math.min(36, Math.max(12, h.length + 4)), type: t === 'money' ? 'money' : undefined })), rows: rep.rows.map((x) => cols.map(([k, , t]) => (t === 'money' || t === 'n' ? Math.round((+x[k] || 0) * 100) / 100 : x[k] ?? ''))) },
      kpis: [{ label: 'Lines', f: 'COUNT', col: cols[0][1] }, ...(moneyCol ? [{ label: `Total — ${moneyCol[1]}`, col: moneyCol[1], type: 'money' }] : []), ...(moneyCol ? [{ label: `Average — ${moneyCol[1]}`, f: 'AVERAGE', col: moneyCol[1], type: 'money' }] : [])],
      groups: [
        ...(rep.group && moneyCol ? [{ title: `${moneyCol[1]} by ${cols.find(([k]) => k === rep.group)?.[1]}`, by: cols.find(([k]) => k === rep.group)?.[1], sum: moneyCol[1], chart: 'pie', type: 'money' }] : []),
        ...(cols[0][2] === 'date' && moneyCol ? [{ title: `${moneyCol[1]} by day`, by: cols[0][1], sum: moneyCol[1], chart: 'line', type: 'money', limit: 92 }] : []),
      ],
    });
    D.xlsx(`super-app-${kind}-report-${r.from}-to-${r.to}.xlsx`, sheets);
  };

  /** PDF: header, the report's key numbers, then the table (the first columns that fit on A4) */
  const pdf = async (kind, label, rep, r) => {
    const doc = SA.core.pdf.create({ title: `Super App — ${label} report` });
    let p = doc.page();
    p.rect(0, 0, p.W, 92, { fill: '#0D1411' });
    p.logo(40, 30, 30);
    p.text('SUPER APP', 80, 46, { size: 15, weight: 800, color: '#fff', spacing: 1 });
    p.text('Command Center — CEO report', 80, 62, { size: 8.5, color: '#9FB3A9' });
    p.text(`${label} report`, p.W - 40, 46, { size: 17, weight: 800, color: '#fff', align: 'right' });
    p.text(`${fmtDate(r.from)} – ${fmtDate(r.to)}`, p.W - 40, 63, { size: 9, color: '#9FB3A9', align: 'right' });
    let y = 122;
    (rep.kpis || []).forEach(([l, v, t], i) => { const x = 40 + i * 130; p.rect(x, y, 122, 46, { fill: '#EEF5F0', r: 6 }); p.text(l, x + 10, y + 17, { size: 7.5, color: '#46514B', maxWidth: 104 }); p.text(t === 'money' ? C.egp(v) : (+v).toLocaleString(), x + 10, y + 35, { size: 12, weight: 800, maxWidth: 104 }); });
    y += 66;
    const cols = rep.columns.slice(0, 7);
    const w = 515 / cols.length;
    const rows = rep.rows.slice(0, 400).map((x) => cols.map(([k, , t]) => (t === 'money' ? C.egp(x[k]) : t === 'date' && x[k] ? fmtDate(x[k]) : String(x[k] ?? ''))));
    p.table(40, y, cols.map(([, h, t]) => ({ label: h, w, align: t === 'money' || t === 'n' ? 'right' : 'left' })), rows, { onPageBreak: () => (p = doc.page()) });
    const last = p._last || p;
    last.text(`Generated ${new Date().toLocaleString()} by the Command Center${rep.rows.length > 400 ? ` · first 400 of ${rep.rows.length} lines (the Excel file has all)` : ''}${rep.columns.length > 7 ? ' · more columns in the Excel file' : ''}`, 40, p.H - 30, { size: 7.5, color: '#46514B' });
    await doc.save(`super-app-${kind}-report-${r.from}-to-${r.to}.pdf`);
    SA.ui.overlay.toast('PDF downloaded');
  };

  SA.pages.ceoReports = C.guard({
    title: 'Reports — Super App',
    render(ctx) {
      const kind = ctx.query.kind || 'finance';
      const r = D.range(ctx.query, '30');
      const def = CE().REPORTS.find(([k]) => k === kind) || CE().REPORTS[0];
      const rep = CE().report(def[0], r);
      this._state = { kind: def[0], label: def[1], rep, r };
      const show = rep.rows.slice(0, 60);
      const body = `
        <div class="ceo-reps">${CE().REPORTS.map(([k, l, t, ic]) => `<a class="ceo-rep ${k === def[0] ? 'is-on' : ''}" href="${SA.core.router.href('/ceo/reports', { kind: k, range: ctx.query.range, from: ctx.query.from, to: ctx.query.to })}" ${k === def[0] ? 'aria-current="page"' : ''}>${icon(ic, 18)}<span><b class="small">${l}</b><span class="xs muted" style="display:block">${t}</span></span></a>`).join('')}</div>
        <section class="dash-card">
          <div class="row-between" style="flex-wrap:wrap;gap:10px"><h2 class="h4">${def[1]} · ${D.rangeLabel(r)}</h2>
            <div class="row ceo-rtools" style="--gap:8px">${D.rangePicker(r, '/ceo/reports', { kind: def[0] })}<button class="btn btn-primary btn-sm" data-xlsx>${icon('file', 14)} Excel</button><button class="btn btn-outline btn-sm" data-pdf>${icon('file', 14)} PDF</button></div></div>
          <div class="ceo-rkpis">${(rep.kpis || []).map(([l, v, t]) => `<div class="ceo-stat"><span class="grow xs muted">${l}</span><b class="num">${t === 'money' ? C.egp(v) : (+v).toLocaleString()}</b></div>`).join('')}</div>
          ${rep.chart ? C.columns(rep.rows.slice().reverse().length ? rep.rows : [], { key: rep.chart, label: def[1] }) : ''}
          <div class="table-wrap" style="margin-top:12px"><table class="table ceo-table"><thead><tr>${rep.columns.map(([, h, t]) => `<th ${t === 'money' || t === 'n' ? 'class="num"' : ''}>${h}</th>`).join('')}</tr></thead>
            <tbody>${show.map((x) => `<tr>${rep.columns.map(([k, , t]) => `<td ${t === 'money' || t === 'n' ? 'class="num"' : ''}>${cell(x[k], t)}</td>`).join('')}</tr>`).join('') || `<tr><td colspan="${rep.columns.length}" class="small muted">Nothing in these dates.</td></tr>`}</tbody></table></div>
          ${rep.rows.length > show.length ? `<p class="xs muted" style="margin-top:8px">Showing ${show.length} of ${rep.rows.length} lines — the Excel file has all of them.</p>` : ''}
          <p class="xs muted" style="margin-top:8px">${icon('info', 12)} The Excel file opens on a Dashboard sheet with live formulas and charts over the data.</p>
        </section>`;
      return C.layout('reports', { title: 'Reports', subtitle: 'Every part of the company, for any dates', body });
    },
    mount(el, ctx) {
      D.bindRange(el, '/ceo/reports', { kind: ctx.query.kind || 'finance' });
      const s = this._state;
      el.querySelector('[data-xlsx]')?.addEventListener('click', () => { try { excel(s.kind, s.label, s.rep, s.r); CE().log('report', `Downloaded the ${s.label} report (Excel), ${s.r.from} to ${s.r.to}`); } catch (e) { SA.ui.overlay.toast(e.message, { type: 'error' }); } });
      el.querySelector('[data-pdf]')?.addEventListener('click', async (e) => { e.currentTarget.disabled = true; try { await pdf(s.kind, s.label, s.rep, s.r); CE().log('report', `Downloaded the ${s.label} report (PDF), ${s.r.from} to ${s.r.to}`); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } e.currentTarget.disabled = false; });
    },
  });
})(window.SA);
