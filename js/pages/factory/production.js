/**
 * Factory system — production: overview, the production board (stages, batches), one job, and the scan screen.
 */
(function (SA) {
  const { esc, fmtNum, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.factory;
  const FC = () => SA.pages.factoryCommon;
  const K = () => SA.pages.dashCommon;
  const H = 36e5;

  /* ---------- Overview ---------- */
  SA.pages.factoryOverview = { title: 'Factory — Super App', render: (ctx, s) => {
    const t = F().today(s.factoryId);
    const st = F().stats(s.factoryId, 7);
    const pct = Math.min(100, Math.round((t.inToday / Math.max(1, t.capacity)) * 100));
    const attention = F().jobs({ factoryId: s.factoryId, stage: F().ACTIVE }).filter((o) => o.late || (o.issue && !o.issue.resolved) || o.left < 6 * H).slice(0, 6);
    const mine = FC().MY_STAGES[s.role];
    const myJobs = mine ? F().jobs({ factoryId: s.factoryId, stage: mine }) : [];
    const pipe = ['new', 'printing', 'qc', 'packing', 'ready', 'shipped'];
    const notes = (SA.services.notify?.list(s.factory.accountId) || []).slice(0, 5);
    const body = `
      ${mine ? `<section class="dash-card fac-mywork"><div class="row-between wrap" style="--gap:12px"><div><span class="xs muted">Your part of the line · ${F().ROLES[s.role].label}</span><h2 class="h2">${myJobs.length} job${myJobs.length === 1 ? '' : 's'} waiting for you</h2><p class="small muted">${myJobs.filter((o) => o.late).length ? `${myJobs.filter((o) => o.late).length} late — start with those.` : 'Oldest deadline first.'}</p></div>
        <div class="row" style="--gap:8px"><a class="btn btn-primary" href="#/factory/jobs?tab=${mine[0] === 'new' ? 'new' : mine[0]}">${icon('package', 16)} Open my queue</a><a class="btn btn-outline" href="#/factory/scan">${icon('scan', 16)} Scan</a></div></div></section>` : ''}
      <div class="kpi-grid">
        ${K().kpi({ label: 'In production', value: fmtNum(t.open), sub: 'items not packed yet', ic: 'package', tone: 'k-blaze' })}
        ${K().kpi({ label: 'Came in today', value: `${fmtNum(t.inToday)} / ${fmtNum(t.capacity)}`, sub: `${pct}% of daily capacity`, ic: 'trend' })}
        ${K().kpi({ label: 'Packed today', value: fmtNum(t.readyToday), sub: 'handed to the courier flow', ic: 'check', tone: 'k-turf' })}
        ${K().kpi({ label: 'Late', value: fmtNum(t.late), sub: `${s.factory.slaHours} h to ship`, ic: 'alert', tone: t.late ? 'k-danger' : '' })}
      </div>
      <section class="dash-card"><div class="row-between"><h2 class="h4">The line right now</h2><span class="xs muted">tap a step to open it</span></div>
        <div class="fac-pipe">${pipe.map((p, i) => `<a class="fac-step" href="#/factory/jobs?tab=${p}" style="--i:${i}"><b class="num">${t.pipe[p] || 0}</b><span class="xs">${F().STAGE_LABEL[p]}</span></a>`).join(`<span class="fac-arrow" aria-hidden="true">${icon('chevronRight', 16)}</span>`)}</div>
        <div class="meter" style="margin-top:14px" aria-label="Today’s load ${pct}%"><span style="width:${pct}%;${pct > 90 ? 'background:var(--danger)' : ''}"></span></div>
      </section>
      <div class="dash-grid g-1-1">
        <section class="dash-card"><div class="row-between"><h2 class="h4">Needs attention</h2><a class="link xs" href="#/factory/jobs?tab=late">All late</a></div>
          ${attention.length ? `<ul class="fac-list">${attention.map((o) => `<li><a href="#/factory/job/${o.id}" class="row-between" style="--gap:10px"><span><b class="num small">${o.id}</b> <span class="xs muted">${o.qty} × ${esc(o.product?.name || '')} · ${esc(o.color)} · ${esc(o.size)}</span>${o.issue && !o.issue.resolved ? `<span class="xs fj-issue-inline">${icon('alert', 11)} ${esc(o.issue.type)}</span>` : ''}</span><span class="row" style="--gap:6px">${FC().dueChip(o)}${FC().stageBadge(o.stage)}</span></a></li>`).join('')}</ul>` : c.empty({ icon: 'check', title: 'All on time', text: 'No late jobs and no open problems.' })}
        </section>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Messages from Super App</h2></div>
          ${notes.length ? `<ul class="fac-list">${notes.map((n) => `<li><a href="${esc(n.link || '#/factory')}"><b class="small">${esc(n.title)}</b><span class="xs muted" style="display:block">${esc(n.body)} · ${timeAgo(n.at)}</span></a></li>`).join('')}</ul>` : c.empty({ icon: 'bell', title: 'No messages', text: 'New jobs, courier bookings and notes from Super App show up here.' })}
        </section>
      </div>
      ${F().can('reports', s) ? `<section class="dash-card"><div class="row-between"><h2 class="h4">Last 7 days</h2><a class="link xs" href="#/factory/reports">Reports</a></div>
        <div class="kpi-grid kpi-sm">${K().kpi({ label: 'Packed on time', value: `${st.onTimePct}%`, ic: 'clock', tone: st.onTimePct >= 95 ? 'k-turf' : 'k-danger' })}${K().kpi({ label: 'Defect rate', value: `${st.defectPct}%`, sub: `${st.qcFails} failed QC · ${st.complaints} complaints`, ic: 'shield' })}${K().kpi({ label: 'Average time to pack', value: `${st.avgHours} h`, ic: 'bolt' })}${K().kpi({ label: 'Items packed', value: fmtNum(st.items), ic: 'package' })}</div></section>` : ''}`;
    return FC().layout('overview', { title: `Good ${new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}, ${esc(s.name.split(' ')[0])}`, subtitle: `${esc(s.factory.name)} · DTF · ${esc(s.factory.city)}`, body });
  } };

  /* ---------- Production board ---------- */
  const TABS = [['open', 'All open'], ['new', 'New'], ['printing', 'Printing'], ['qc', 'Quality check'], ['packing', 'Packing'], ['ready', 'Ready for pickup'], ['late', 'Late'], ['issues', 'Problems'], ['done', 'Shipped & done']];
  const tabQuery = (tab) => ({ open: { stage: F().ACTIVE }, late: { stage: F().ACTIVE, late: true }, issues: { issue: true }, done: { stage: ['shipped', 'delivered', 'refused', 'cancelled'] } }[tab] || { stage: tab });
  SA.pages.factoryJobs = {
    title: 'Production — Factory', render: (ctx, s) => {
      const def = { printer: 'new', qc: 'qc', packer: 'packing' }[s.role] || 'open';
      const tab = TABS.some(([k]) => k === ctx.query.tab) ? ctx.query.tab : def;
      const mine = ctx.query.mine === '1', q = (ctx.query.q || '').trim();
      const list = F().jobs({ factoryId: s.factoryId, ...tabQuery(tab), mine, q });
      const count = (k) => F().jobs({ factoryId: s.factoryId, ...tabQuery(k) }).length;
      const batches = tab === 'new' && F().can('print', s) ? F().batches(s.factoryId).filter((b) => b.jobs.length > 1) : [];
      const href = (extra) => SA.core.router.href('/factory/jobs', { tab, mine: mine ? '1' : undefined, q: q || undefined, ...extra });
      const body = `
        <div class="row-between wrap" style="--gap:10px">
          <div class="chips fac-tabs" role="tablist" aria-label="Stages">${TABS.map(([k, l]) => { const n = count(k); return `<a class="chip" role="tab" href="${SA.core.router.href('/factory/jobs', { tab: k, mine: mine ? '1' : undefined })}" aria-pressed="${tab === k}" aria-selected="${tab === k}">${l}${n ? ` <span class="chip-count ${k === 'late' || k === 'issues' ? 'is-danger' : ''}">${n}</span>` : ''}</a>`; }).join('')}</div>
        </div>
        <div class="row wrap fac-tools" style="--gap:8px">
          <form class="search-mini" data-q><input class="input" name="q" value="${esc(q)}" placeholder="Job, order or customer" aria-label="Search jobs" /></form>
          <a class="chip" href="${href({ mine: mine ? undefined : '1' })}" aria-pressed="${mine}">${icon('user', 13)} Mine only</a>
          ${list.length ? `<button class="btn btn-ghost btn-sm" data-ticket="${list.slice(0, 40).map((o) => o.id).join(',')}">${icon('qr', 14)} Print ${Math.min(40, list.length)} ticket${list.length > 1 ? 's' : ''}</button>` : ''}
        </div>
        ${batches.length ? `<section class="dash-card fac-batches"><h2 class="h4">${icon('bolt', 16)} Print in batches</h2><p class="xs muted">Same product and colour — load the press once and run them together.</p>
          <div class="fac-batch-list">${batches.map((b) => `<div class="fac-batch"><span class="grow"><b class="small">${b.jobs.reduce((t, o) => t + o.qty, 0)} × ${esc(b.product?.name || '')}</b><span class="xs muted" style="display:block"><span>${esc(b.color)}</span> · <span>Sizes</span> ${[...new Set(b.jobs.map((o) => o.size))].join(', ')}</span></span><button class="btn btn-primary btn-sm" data-batch="${b.jobs.map((o) => o.id).join(',')}">${icon('bolt', 13)} Start batch (${b.jobs.length})</button></div>`).join('')}</div></section>` : ''}
        ${list.length ? `<div class="pr-list">${list.slice(0, 80).map((o) => FC().jobCard(o, s)).join('')}</div>${list.length > 80 ? `<p class="xs muted" style="text-align:center">Showing 80 of ${list.length}. Search to find others.</p>` : ''}`
          : `<div class="dash-card">${c.empty({ icon: 'check', title: tab === 'late' ? 'Nothing late' : tab === 'issues' ? 'No open problems' : 'Nothing here right now', text: 'New jobs arrive here as soon as a customer orders a printed item.' })}</div>`}`;
      return FC().layout('jobs', { title: 'Production', subtitle: `${F().jobs({ factoryId: s.factoryId, stage: F().ACTIVE }).length} jobs on the line · oldest deadline first`, body });
    },
    mount(el, ctx, s) {
      el.querySelector('[data-q]')?.addEventListener('submit', (e) => { e.preventDefault(); SA.core.router.go(SA.core.router.href('/factory/jobs', { ...ctx.query, q: e.target.q.value.trim() || undefined }).slice(1)); });
      el.addEventListener('click', (e) => {
        const b = e.target.closest('[data-batch]'); if (!b) return;
        try { const n = F().startBatch(b.dataset.batch.split(',')); SA.ui.overlay.toast(`${n} jobs moved to printing`); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
      return FC().bindJobs(el);
    },
  };

  /* ---------- One job ---------- */
  const STEP_LABEL = { new: 'Job received', printing: 'Printing started', qc: 'Printed — at quality check', packing: 'Passed quality check', ready: 'Packed', 'qc-fail': 'Failed quality check', issue: 'Problem reported', resolved: 'Problem resolved', complaint: 'Customer complaint — reprint', moved: 'Moved to this factory' };
  SA.pages.factoryJob = {
    title: 'Job — Factory', render: (ctx, s) => {
      const o = F().job(ctx.params.id);
      if (o.factoryId !== s.factoryId) throw Object.assign(new Error('This job belongs to another factory.'), { code: 'not_found' });
      const siblings = SA.services.db.all('podOrders').filter((x) => x.orderId === o.orderId && x.factoryId === o.factoryId && x.id !== o.id && x.status !== 'cancelled');
      const sh = o.shipmentId && SA.services.shipping.get(o.shipmentId);
      const staff = F().staff(s.factoryId).filter((x) => x.active && ['printer', 'supervisor', 'owner'].includes(x.role));
      const body = `<div class="fac-job">
        <section class="dash-card fac-job-main">
          <div class="fac-job-media">${FC().sides(o).map((x) => FC().mock(o, x, 'is-big')).join('')}</div>
          <div class="stack" style="--gap:10px">
            <div class="row wrap" style="--gap:6px">${FC().stageBadge(o.stage)}${o.sample ? '<span class="badge badge-gold">Sample → seller</span>' : ''}${FC().dueChip(o)}</div>
            <h2 class="h2">${o.qty} × ${esc(o.product?.name || '')}</h2>
            <p class="lead" style="margin:0"><span>${esc(o.color)}</span> · <span>Size</span> <b>${esc(o.size)}</b> · DTF <span>${{ front: 'Front', back: 'Back', 'front+back': 'Front + back' }[FC().sides(o).join('+')] || ''}</span></p>
            <p class="small muted">“${esc(o.title)}”</p>
            ${o.issue && !o.issue.resolved ? `<div class="fac-banner is-danger">${icon('alert', 16)} <span class="grow"><b>${esc(o.issue.type)}</b>${o.issue.note ? ` — ${esc(o.issue.note)}` : ''} <span class="xs">· ${esc(o.issue.by)}, ${timeAgo(o.issue.at)}</span></span>${F().can('assign', s) ? '<button class="btn btn-outline btn-sm" data-resolve>Resolved</button>' : ''}</div>` : ''}
            ${o.status === 'cancelled' ? `<div class="fac-banner is-danger">${icon('x', 16)} Cancelled by Super App${o.reason ? ` — ${esc(o.reason)}` : ''}. Stop work on it.</div>` : ''}
            <div class="row wrap fac-big-actions" style="--gap:8px">${FC().actions(o, s, { big: true })}</div>
            <div class="row wrap" style="--gap:8px">
              ${o.design ? `<button class="btn btn-ghost btn-sm" data-file="${o.id}">${icon('file', 14)} Print file</button>` : ''}
              <button class="btn btn-ghost btn-sm" data-ticket="${o.id}">${icon('qr', 14)} Job ticket</button>
              ${F().ACTIVE.includes(o.stage) && !(o.issue && !o.issue.resolved) ? `<button class="btn btn-ghost btn-sm" data-issue style="color:var(--danger)">${icon('alert', 14)} Report a problem</button>` : ''}
            </div>
            ${F().can('assign', s) && F().ACTIVE.includes(o.stage) ? `<label class="field" style="max-width:280px"><span class="xs muted">Assigned to</span><select class="select" data-assign><option value="">Nobody</option>${staff.map((x) => `<option value="${x.id}" ${o.assignedTo === x.id ? 'selected' : ''}>${esc(x.name)} · ${F().ROLES[x.role].label}</option>`).join('')}</select></label>` : o.assignee ? `<p class="small">${icon('user', 13)} ${esc(o.assignee.name)}</p>` : ''}
          </div>
        </section>
        <div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Details</h2>
            <dl class="fac-dl">
              <dt>Job</dt><dd class="num">${o.id}</dd>
              <dt>Order</dt><dd class="num">${esc(o.orderId)}${siblings.length ? ` · ${siblings.length + 1} items` : ''}</dd>
              <dt>Placed</dt><dd>${new Date(o.placedAt).toLocaleString()}</dd>
              <dt>Ship by</dt><dd>${new Date(o.dueAt).toLocaleString()}</dd>
              <dt>Customer</dt><dd>${esc(o.customer?.name || '')} · ${esc(o.customer?.city || '')}</dd>
              <dt>Payment</dt><dd>${o.payment === 'cod' ? 'Cash on delivery — the courier collects' : 'Paid online'}</dd>
              <dt>Your price</dt><dd class="num">${SA.core.utils.money(o.partnerCost * o.qty)}</dd>
              ${o.reprints ? `<dt>Reprints</dt><dd>${o.reprints}</dd>` : ''}
            </dl>
            ${siblings.length ? `<h3 class="h5" style="margin-top:14px">Same parcel</h3><ul class="fac-list">${siblings.map((x) => `<li><a href="#/factory/job/${x.id}" class="row-between"><span class="small"><b class="num">${x.id}</b> · ${x.qty} × ${esc(x.color)} ${esc(x.size)}</span>${FC().stageBadge(x.stage)}</a></li>`).join('')}</ul>` : ''}
            ${sh ? `<h3 class="h5" style="margin-top:14px">Courier</h3><p class="small">${esc(sh.providerName)} · <b class="num">${esc(sh.awb)}</b> · <span>${SA.services.shipping.STATUS_LABEL[sh.status]}</span></p><button class="btn btn-ghost btn-sm" data-label="${sh.id}">${icon('file', 13)} Shipping label</button>` : ''}
          </section>
          <section class="dash-card"><h2 class="h4">Timeline</h2>
            <ol class="fac-timeline">${(o.steps || []).slice().reverse().map((x) => `<li class="${['qc-fail', 'issue', 'complaint'].includes(x.stage) ? 'is-bad' : ''}"><b class="small">${STEP_LABEL[x.stage] || x.stage}</b><span class="xs muted">${x.name ? `${esc(x.name)} · ` : ''}${new Date(x.at).toLocaleString()}</span>${x.note ? `<span class="xs">${esc(x.note)}</span>` : ''}</li>`).join('')}
            ${sh ? sh.events.slice().reverse().map((ev) => `<li class="is-courier"><b class="small">${esc(sh.providerName)}: <span>${SA.services.shipping.STATUS_LABEL[ev.status]}</span></b><span class="xs muted">${new Date(ev.at).toLocaleString()}</span></li>`).join('') : ''}</ol>
          </section>
        </div></div>`;
      return FC().layout('jobs', { title: `Job ${o.id}`, subtitle: `<a class="link" href="#/factory/jobs">Production</a> · ${esc(o.orderId)}`, body });
    },
    mount(el, ctx, s) {
      const id = ctx.params.id.toUpperCase();
      el.querySelector('[data-assign]')?.addEventListener('change', (e) => { try { F().assign(id, e.target.value); SA.ui.overlay.toast('Assigned'); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } });
      el.querySelector('[data-resolve]')?.addEventListener('click', async () => {
        const r = await SA.pages.adminCommon.reason({ title: 'Problem resolved', label: 'What was done?', required: false, confirmLabel: 'Mark resolved' });
        if (r) { try { F().resolveIssue(id, r.reason); SA.ui.overlay.toast('Marked resolved'); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } }
      });
      el.querySelector('[data-issue]')?.addEventListener('click', async () => {
        const r = await SA.pages.adminCommon.reason({ title: `Report a problem · ${id}`, text: 'Your supervisor and Super App are told straight away.', label: 'More detail (optional)', required: false, confirmLabel: 'Report', danger: true,
          extra: `<div class="field"><label for="fi-type">What is the problem?</label><select class="select" id="fi-type" name="type">${F().ISSUES.map((x) => `<option>${x}</option>`).join('')}</select></div>` });
        if (r) { try { F().reportIssue(id, { type: r.type, note: r.reason }); SA.ui.overlay.toast('Problem reported'); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } }
      });
      return FC().bindJobs(el);
    },
  };

  /* ---------- Scan ---------- */
  // Works with a USB/Bluetooth barcode scanner (it types the code + Enter), typing by hand, or the phone camera
  SA.pages.factoryScan = {
    title: 'Scan — Factory', render: (ctx, s) => {
      const code = (ctx.query.code || '').trim().toUpperCase();
      let found = null, err = '';
      if (code) { try { found = F().job(code); if (found.factoryId !== s.factoryId) { found = null; err = 'This job belongs to another factory.'; } } catch (e) { err = e.message; } }
      const camera = 'BarcodeDetector' in window && navigator.mediaDevices?.getUserMedia;
      const body = `<div class="fac-scan">
        <section class="dash-card fac-scan-box">
          <form data-scan class="stack" style="--gap:10px">
            <label for="scan-code" class="h4">Scan the QR on the job ticket — or type the job code</label>
            <div class="row" style="--gap:8px"><input class="input fac-scan-input" id="scan-code" name="code" value="${esc(code)}" placeholder="PO-…" autocomplete="off" autocapitalize="characters" spellcheck="false" /><button class="btn btn-primary" type="submit">${icon('search', 16)} Open</button></div>
            ${camera ? `<button type="button" class="btn btn-outline" data-cam>${icon('camera', 16)} Use the camera</button><video class="fac-video" data-video playsinline muted hidden></video>` : '<p class="xs muted">A barcode scanner works here too: it types the code and presses Enter.</p>'}
          </form>
          ${err ? `<p class="field-error" role="alert">${esc(err)}</p>` : ''}
        </section>
        ${found ? `<div class="fac-scan-result">${FC().jobCard(found, s)}<div class="fac-big-actions row wrap" style="--gap:8px">${FC().actions(found, s, { big: true }) || `<p class="small muted">${F().NEXT[found.stage] ? `The next step (“${F().NEXT[found.stage].label}”) is for another role.` : `Current step: ${F().STAGE_LABEL[found.stage]}.`}</p>`}<a class="btn btn-ghost" href="#/factory/job/${found.id}">Open the job</a></div></div>` : ''}
      </div>`;
      return FC().layout('scan', { title: 'Scan a job', subtitle: 'Each step is saved with your name', body });
    },
    mount(el) {
      const inp = el.querySelector('#scan-code');
      if (inp && !inp.value) setTimeout(() => inp.focus(), 80);
      el.querySelector('[data-scan]')?.addEventListener('submit', (e) => { e.preventDefault(); const v = e.target.code.value.trim().toUpperCase(); SA.core.router.go(SA.core.router.href('/factory/scan', { code: v || undefined }).slice(1)); });
      let stream = null, raf = 0;
      el.querySelector('[data-cam]')?.addEventListener('click', async () => {
        const v = el.querySelector('[data-video]');
        try {
          stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
          v.srcObject = stream; v.hidden = false; await v.play();
          const det = new window.BarcodeDetector({ formats: ['qr_code', 'code_128'] });
          const loop = async () => { try { const r = await det.detect(v); if (r[0]?.rawValue) { stop(); SA.core.router.go(SA.core.router.href('/factory/scan', { code: r[0].rawValue.trim().toUpperCase() }).slice(1)); return; } } catch { /* keep trying */ } raf = requestAnimationFrame(loop); };
          loop();
        } catch { SA.ui.overlay.toast('The camera isn’t available — type the code instead.', { type: 'error' }); }
      });
      const stop = () => { cancelAnimationFrame(raf); stream?.getTracks().forEach((t) => t.stop()); stream = null; };
      const off = FC().bindJobs(el);
      return () => { stop(); off(); };
    },
  };

  // Wrap with the factory guard (sign-in + role)
  ['factoryOverview', 'factoryJobs', 'factoryJob', 'factoryScan'].forEach((k) => { SA.pages[k] = SA.pages.factoryCommon.guard(SA.pages[k]); });
})(window.SA);
