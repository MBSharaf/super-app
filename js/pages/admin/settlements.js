/**
 * Admin console: pitch accounts (#/admin/settlements). Each pitch's owner chooses how players pay (owner's rule,
 * 2026-11-21): a deposit in the app and the rest at the pitch, or the full amount in the app. This page shows, per
 * pitch and per partner, where the money is, what Super Malaab earned and what it still owes each venue.
 * API mode: GET /admin/settlements (permission payouts.read). The Excel export has a dashboard sheet first.
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;

  const MODE = {
    FullOnline: ['badge-turf', 'card', 'Full payment in the app'],
    Deposit: ['badge-info', 'wallet', 'Deposit + rest at the pitch'],
    Deposit50: ['badge-info', 'wallet', '50% deposit + rest at the pitch'],
  };
  const modeBadge = (m) => { const [cls, ic, label] = MODE[m] || MODE.Deposit; return `<span class="badge ${cls}">${icon(ic, 12)} ${label}</span>`; };
  const modesOf = (s) => s.split(' + ').map(modeBadge).join(' ');
  const empty = (cols, text) => `<tr><td colspan="${cols}" class="small muted" style="padding:18px">${text}</td></tr>`;

  SA.pages.adminSettlements = A.guard({
    title: 'Pitch accounts — Super Malaab Admin',
    render(ctx) {
      const r = C.range(ctx.query, '30');
      const q = (ctx.query.q || '').trim();
      const data = AD().settlements({ ...r, q });
      const t = data.totals;
      const full = data.pitches.filter((p) => p.paymentMode === 'FullOnline').length;
      const body = `${C.rangePicker(r, '/admin/settlements', q ? { q } : {})}
        <div class="kpi-grid">
          ${C.kpi({ label: 'Booked value', value: money(t.gross), count: t.gross, suffix: ' EGP', ic: 'ticket', sub: `${t.bookings.toLocaleString()} bookings · ${C.rangeLabel(r)}` })}
          ${C.kpi({ label: 'Paid in the app', value: money(t.paidInApp), count: t.paidInApp, suffix: ' EGP', ic: 'card', tone: 'k-turf', sub: 'Reached Super Malaab' })}
          ${C.kpi({ label: 'Cash at the pitch', value: money(t.cashAtPitch), count: t.cashAtPitch, suffix: ' EGP', ic: 'wallet', sub: `${money(t.stillAtPitch)} still to collect` })}
          ${C.kpi({ label: 'Owed to venues now', value: money(t.owed), count: t.owed, suffix: ' EGP', ic: 'swap', tone: 'k-blaze', sub: `Our income ${money(t.ourIncome)}` })}
        </div>
        <section class="dash-card">
          <div class="row-between wrap" style="--gap:10px"><h2 class="h4">How it works</h2>
            <form class="row" data-q style="--gap:8px"><input class="input" name="q" value="${esc(q)}" placeholder="Search a partner or pitch" aria-label="Search a partner or pitch" style="min-width:220px" /><button class="btn btn-outline btn-sm" type="submit">${icon('search', 14)} Search</button></form></div>
          <div class="dash-grid g-1-1" style="margin-top:12px">
            <p class="small soft">${modeBadge('Deposit')} <span>The player pays a deposit in the app and the rest in cash at the pitch. The venue keeps the cash; Super Malaab pays the venue the deposit’s pitch part, less the commission.</span></p>
            <p class="small soft">${modeBadge('FullOnline')} <span>The player pays everything in the app. Super Malaab holds it until the match is played, then moves the venue’s share, less the commission, to its balance for withdrawal.</span></p>
          </div>
          <p class="xs muted" style="margin-top:8px">${data.pitches.length} pitches with bookings in this period · ${full} take the full payment in the app.</p>
        </section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">By pitch</h2><div class="table-wrap"><table class="table dash-table"><thead><tr>
          <th>Pitch</th><th>How players pay</th><th class="r">Bookings</th><th class="r">Booked value</th><th class="r">Paid in the app</th><th class="r">Cash at the pitch</th><th class="r">Super Malaab income</th><th class="r">Venue share (app)</th></tr></thead><tbody>
          ${data.pitches.map((p) => `<tr><td class="small"><b>${esc(p.pitchName)}</b><span class="xs muted" style="display:block">${esc(p.partnerName)}${p.venueName ? ` · ${esc(p.venueName)}` : ''}</span></td>
            <td>${modeBadge(p.paymentMode)}</td><td class="r num">${p.bookings.toLocaleString()}</td><td class="r num">${money(p.gross)}</td><td class="r num strong">${money(p.paidInApp)}</td>
            <td class="r num">${money(p.cashAtPitch)}${p.stillAtPitch ? `<span class="xs muted" style="display:block">${money(p.stillAtPitch)} to collect</span>` : ''}</td><td class="r num">${money(p.ourIncome)}</td><td class="r num strong">${money(p.venueShareInApp)}</td></tr>`).join('') || empty(8, data.loading ? 'Loading…' : 'No bookings in this period.')}
          </tbody>${data.pitches.length ? `<tfoot><tr><th colspan="2">Total</th><th class="r num">${t.bookings.toLocaleString()}</th><th class="r num">${money(t.gross)}</th><th class="r num">${money(t.paidInApp)}</th><th class="r num">${money(t.cashAtPitch)}</th><th class="r num">${money(t.ourIncome)}</th><th class="r num">${money(t.venueShareInApp)}</th></tr></tfoot>` : ''}</table></div></section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">By partner — what we owe today</h2><div class="table-wrap"><table class="table dash-table"><thead><tr>
          <th>Partner</th><th>How players pay</th><th class="r">Held until the match</th><th class="r">Available</th><th class="r">In payout</th><th class="r">Paid out</th><th class="r">Owed now</th></tr></thead><tbody>
          ${data.partners.map((p) => `<tr><td class="small"><b>${esc(p.partnerName)}</b><span class="xs muted" style="display:block">${p.pitches} ${p.pitches === 1 ? 'pitch' : 'pitches'}</span></td><td>${modesOf(p.paymentModes)}</td>
            <td class="r num">${money(p.held)}</td><td class="r num">${money(p.available)}</td><td class="r num">${money(p.inPayout)}</td><td class="r num">${money(p.paidOut)}</td><td class="r num strong">${money(p.owed)}</td></tr>`).join('') || empty(7, data.loading ? 'Loading…' : 'No partners in this period.')}
          </tbody></table></div>
          <p class="xs muted" style="padding:0 18px 18px">Held money moves to Available after the match. Partners withdraw Available money from their dashboard; approve it in <a class="link" href="#/admin/payouts">Withdrawals</a>.</p></section>`;
      return C.layout('settlements', { title: 'Pitch accounts', subtitle: 'How each pitch is paid, and what Super Malaab owes each venue', actions: `<button class="btn btn-outline btn-sm" data-export>${icon('file', 15)} Export Excel</button>`, body });
    },
    mount(el, ctx) {
      C.bindRange(el, '/admin/settlements');
      el.querySelector('[data-q]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const q = new FormData(e.target).get('q')?.toString().trim();
        SA.core.router.go(SA.core.router.href('/admin/settlements', { ...ctx.query, q: q || undefined }));
      });
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const r = C.range(ctx.query, '30');
        const data = AD().settlements({ ...r, q: (ctx.query.q || '').trim() });
        const label = (m) => (MODE[m] || MODE.Deposit)[2];
        const dashSheets = SA.core.xlsx.dashboard({
          title: 'Super Malaab — Pitch accounts', subtitle: `${r.from} to ${r.to}`,
          data: { name: 'Pitches', columns: [{ header: 'Pitch', width: 26 }, { header: 'Partner', width: 24 }, { header: 'How players pay', width: 28 }, { header: 'Bookings', width: 10 },
            { header: 'Booked value', width: 14, type: 'money' }, { header: 'Paid in the app', width: 15, type: 'money' }, { header: 'Cash at the pitch', width: 15, type: 'money' },
            { header: 'Still to collect', width: 14, type: 'money' }, { header: 'Super Malaab income', width: 16, type: 'money' }, { header: 'Venue share (app)', width: 16, type: 'money' }],
          rows: data.pitches.map((p) => [p.pitchName, p.partnerName, label(p.paymentMode), p.bookings, p.gross, p.paidInApp, p.cashAtPitch, p.stillAtPitch, p.ourIncome, p.venueShareInApp]) },
          kpis: [{ label: 'Booked value', col: 'Booked value', type: 'money' }, { label: 'Paid in the app', col: 'Paid in the app', type: 'money' }, { label: 'Cash at the pitch', col: 'Cash at the pitch', type: 'money' },
            { label: 'Super Malaab income', col: 'Super Malaab income', type: 'money' }, { label: 'Owed to venues now', formula: `${data.totals.owed}`, type: 'money' }],
          groups: [{ title: 'Paid in the app by payment mode', by: 'How players pay', sum: 'Paid in the app', chart: 'bar', type: 'money' }, { title: 'Venue share by partner', by: 'Partner', sum: 'Venue share (app)', chart: 'bar', type: 'money' }],
        });
        C.xlsx(`supermalaab-pitch-accounts-${r.from}-to-${r.to}.xlsx`, [...dashSheets,
          { name: 'Partners', columns: [{ header: 'Partner', width: 26 }, { header: 'Pitches', width: 9 }, { header: 'How players pay', width: 40 }, { header: 'Venue share (app)', width: 16, type: 'money' },
            { header: 'Held until the match', width: 18, type: 'money' }, { header: 'Available', width: 14, type: 'money' }, { header: 'In payout', width: 14, type: 'money' }, { header: 'Paid out', width: 14, type: 'money' }, { header: 'Owed now', width: 14, type: 'money' }],
            rows: data.partners.map((p) => [p.partnerName, p.pitches, p.paymentModes.split(' + ').map(label).join(' + '), p.venueShareInApp, p.held, p.available, p.inPayout, p.paidOut, p.owed]),
            totals: ['Total', '', '', data.partners.reduce((s, p) => s + p.venueShareInApp, 0), data.partners.reduce((s, p) => s + p.held, 0), data.partners.reduce((s, p) => s + p.available, 0),
              data.partners.reduce((s, p) => s + p.inPayout, 0), data.partners.reduce((s, p) => s + p.paidOut, 0), data.totals.owed] }]);
      });
    },
  });
})(window.SA);
