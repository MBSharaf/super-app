/**
 * Floating chatbot support button (all pages), styled in Super App brand colours. An AI agent answers customers.
 * - SA.config.chatbot.url set (the AI agent's webhook, e.g. an n8n "AI Agent" workflow): every message is sent there as
 *   POST JSON { sessionId, message, topic, lang, page, user } and the reply is read from { reply } or { output }.
 * - Empty, or the agent doesn't answer: a built-in demo assistant answers from the Help center questions and points to
 *   Contact support when it can't help.
 * The conversation is kept for the browser tab (sessionStorage).
 */
(function (SA) {
  const { icon, esc } = { icon: SA.ui.icon, esc: SA.core.utils.esc };
  const KEY = 'superapp.chatbot';
  const ar = () => !!SA.i18n?.isAr;
  const L = (en, a) => (ar() ? a : en);
  // A friendly bot head with a speech tail
  const GLYPH = '<svg viewBox="0 0 24 24" width="26" height="26" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M12 3v2.5"/><circle cx="12" cy="2.6" r=".9" fill="currentColor" stroke="none"/><rect x="4" y="5.5" width="16" height="12" rx="4"/><path d="M8.5 17.5 7 21l4.2-3.5"/><circle cx="9.2" cy="11.2" r="1.2" fill="currentColor" stroke="none"/><circle cx="14.8" cy="11.2" r="1.2" fill="currentColor" stroke="none"/><path d="M9.5 14.3c1.4 1 3.6 1 5 0"/><path d="M2 10.5v3M22 10.5v3"/></svg>';

  const TOPICS = [
    ['pitch', 'Help with a booking', 'bookings'],
    ['users', 'Finding players', 'games'],
    ['trophy', 'Fantasy & leagues', 'fantasy'],
    ['package', 'Order or delivery', 'shop'],
  ];

  const load = () => { try { return JSON.parse(sessionStorage.getItem(KEY)) || null; } catch { return null; } };
  const save = (s) => { try { sessionStorage.setItem(KEY, JSON.stringify(s)); } catch { /* private mode */ } };
  const sessionId = () => `cb-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 7)}`;

  /* ---------- Demo assistant: best Help center answer for the question ---------- */
  const STOP = new Set('the and for you your can how what when where who why with this that are was from have has does did not but get got any about into out our its his her they them there here which will would should could please need want help just also some more than then'.split(' ')
    .concat('إزاي ازاي ايه إيه ليه فين امتى إمتى عايز عاوز ممكن اللي على عشان علشان لما بتاع بتاعي كده هو هي انا أنا انت إنت فيه في من مع ولا'.split(' ')));
  const words = (t) => String(t || '').toLowerCase().replace(/<[^>]+>/g, ' ').split(/[^\p{L}\p{N}]+/u).filter((w) => w.length > 2 && !STOP.has(w));
  const demoReply = (text, topic, fromTopic = false) => {
    const H = SA.data.help;
    const lang = ar() ? 'ar' : 'en';
    const asked = new Set(fromTopic ? [] : words(text));
    const pool = H.QA.filter((x) => !topic || x.topic === topic);
    const scored = H.QA.map((x) => ({ x, n: words(`${x.q.en} ${x.q.ar}`).filter((w) => asked.has(w)).length + (x.topic === topic ? 0.5 : 0) }))
      .sort((a, b) => b.n - a.n);
    if (!asked.size && pool.length) {
      return `${L('Here’s what people ask most about this:', 'دي أكتر حاجة الناس بتسأل عنها هنا:')}<br><b>${esc(pool[0].q[lang])}</b><br>${pool[0].a[lang]}`;
    }
    if (scored[0] && scored[0].n >= (asked.size > 2 ? 2 : 1)) return `<b>${esc(scored[0].x.q[lang])}</b><br>${scored[0].x.a[lang]}`;
    return L('I’m not sure about that one yet. Our team can help: open <a href="#/support">Contact support</a> and we’ll reply soon.',
      'لسه مش متأكد من الإجابة دي. فريقنا يقدر يساعدك: افتح <a href="#/support">كلّم الدعم</a> وهنرد عليك قريب.');
  };

  /* ---------- The AI agent (when its link is set) ---------- */

  /**
   * Everything the agent needs about this customer, from this device (the demo site has no database yet): profile, wallet,
   * Super Points, bookings and packages, games published / joined / asked to join, shop orders, fantasy team and leagues,
   * cancellation requests and the last payments. Each list keeps the newest 20.
   */
  const customerContext = () => {
    const S = SA.services, st = SA.core.store.get();
    const top = (a, n = 20) => (Array.isArray(a) ? a.slice(-n).reverse() : []);
    const iso = (t) => (t ? new Date(t).toISOString() : null);
    const hh = (h) => (h == null ? null : `${String(Math.floor(h) % 24).padStart(2, '0')}:${h % 1 ? '30' : '00'}`);
    const safe = (fn, d = null) => { try { return fn(); } catch { return d; } };
    const u = st.user || {};
    const pts = safe(() => S.loyalty.summary());
    const bookings = top(st.bookings).map((b) => ({
      code: b.id, status: b.status, pitch: b.pitchName, pitchId: b.pitchId, area: b.area, city: b.city, date: b.date, startTime: hh(b.hour), duration: b.duration,
      players: b.players, total: b.total, depositPaid: b.deposit ?? null, payAtPitch: b.payAtPitch ?? null, cancelPolicy: b.policy || null,
      packageId: b.seriesId || null, sessionNo: b.sessionNo || null, publishedGameId: b.publishedMatchId || null, link: `/booking/${b.id}`,
      cancellationPolicy: safe(() => { const p = S.bookings.cancellationPolicy(b); return { refundNow: p.refund ?? null, policy: p.policy?.name || null }; }),
    }));
    const games = top(st.publishedMatches).map((m) => ({ id: m.id, mine: !!m.isMine, date: m.date, startTime: hh(m.hour), playersNeeded: m.total, playersAlready: m.filled,
      level: m.level, type: m.type, gender: m.gender, pricePerPlayer: m.pricePerPlayer, status: m.closed ? 'closed' : 'open', bookingCode: m.bookingId || null, link: `/players/${m.id}` }));
    const requests = Object.entries(st.matchRequests || {}).map(([id, r]) => {
      const m = safe(() => S.matches.allSync().find((x) => x.id === id)) || {};
      return { gameId: id, status: r.status, at: iso(r.at), payment: r.payment || null, date: m.date || null, startTime: hh(m.hour), host: m.hostName || null, pitch: m.pitch?.name || m.pitchName || null, link: `/players/${id}` };
    });
    const orders = top(safe(() => S.shop.orders(), st.orders) || st.orders).map((o) => ({
      code: o.id, status: o.status, placedAt: iso(o.createdAt || o.placedAt), total: o.total, payment: o.payment?.method || null, delivery: o.delivery || o.speed || null,
      items: (o.lines || []).map((l) => ({ name: l.name, size: l.variant?.size || null, color: l.variant?.color || null, qty: l.qty, price: l.price })), link: `/order/${o.id}`,
    }));
    const f = st.fantasy || {};
    return {
      customer: { id: u.id || null, name: u.name || null, email: u.email || null, phone: u.phone || null, city: u.city || null, area: u.area || null },
      context: {
        wallet: { balance: st.wallet?.balance ?? 0, currency: 'EGP', lastTransactions: top(st.wallet?.transactions, 10).map((t) => ({ at: iso(t.at), type: t.type, label: t.label, amount: t.amount })) },
        superPoints: pts ? { points: pts.balance ?? pts.points ?? null, tier: pts.tier?.name || pts.tier || null } : null,
        bookings, games, gameRequests: requests, orders,
        fantasy: { team: f.team ? { name: f.team.name, players: (f.team.squad || []).length, gameweeksPlayed: (f.team.history || []).length, totalPoints: safe(() => S.fantasy.totals().total, null) } : null,
          leaguesCreated: (f.createdGroups || []).map((g) => ({ id: g.id, name: g.name, code: g.code })), leaguesJoined: f.joinedLeagues || [] },
        cancellationRequests: top(safe(() => S.cancellations.mine(), [])).map((c) => ({ code: c.id, kind: c.kind, about: c.label, status: c.status, createdAt: iso(c.createdAt), note: c.note || null })),
        lastPayments: top(st.payments, 10).map((p) => ({ id: p.id, at: iso(p.at), kind: p.kind, label: p.label, amount: p.amount, method: p.method })),
      },
    };
  };

  const askAgent = async (state, message, topic) => {
    const url = SA.config.chatbot?.url;
    if (!url) return null;
    const site = new URL(SA.core.router.url('/'), location.href).href;
    const res = await fetch(url, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        event: 'support.message', source: 'demo-site', site, sentAt: new Date().toISOString(),
        sessionId: state.id, message, topic: topic || null, lang: ar() ? 'ar' : 'en', page: location.pathname + location.search,
        // The conversation so far (text only), so the agent keeps the thread even without its own memory
        history: state.msgs.slice(-12).map((m) => ({ from: m.from === 'me' ? 'customer' : 'assistant', text: String(m.html).replace(/<br\s*\/?>/g, '\n').replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'") })),
        ...customerContext(),
        knowledgeUrl: new URL('chatbot/knowledge.md', site).href,
      }),
    });
    if (!res.ok) throw new Error(`chatbot ${res.status}`);
    const data = await res.json().catch(() => ({}));
    const body = Array.isArray(data) ? data[0] : data;
    const reply = body?.reply ?? body?.output ?? body?.text;
    if (!reply) return null;
    const actions = [].concat(body.actions || [], body.action ? [body.action] : []).filter((a) => a && typeof a === 'object' && ['cancel', 'open'].includes(a.type));
    return { html: esc(String(reply)).replace(/\n/g, '<br>'), actions };
  };

  /** Buttons for what the agent proposes; nothing happens until the customer presses one */
  const KIND_LABEL = { booking: ['booking', 'الحجز'], package: ['package', 'الباكدج'], order: ['order', 'الأوردر'], 'game-request': ['request to join', 'طلب الانضمام'], game: ['game', 'الماتش'] };
  const actionButtons = (actions) => actions.map((a, i) => {
    if (a.type === 'open') return `<button type="button" class="btn btn-outline btn-sm" data-cb-act="${i}">${esc(a.label || L('Open', 'افتح'))}</button>`;
    const k = KIND_LABEL[a.kind] || [a.kind, a.kind];
    return `<button type="button" class="btn btn-primary btn-sm" data-cb-act="${i}">${esc(L(`Send cancellation request (${k[0]} ${a.id})`, `ابعت طلب إلغاء (${k[1]} ${a.id})`))}</button>`;
  }).join('');

  /* ---------- Cancellation requests (js/services/cancellationService.js) speak through the chat ---------- */
  let ui = null;
  const cxText = (rec, phase) => {
    const code = `<b class="num">${esc(rec.id)}</b>`;
    const what = `${esc(rec.label)}`;
    const link = `<a href="#${esc(rec.link)}">${L('Open it', 'افتحه')}</a>`;
    const money = (n) => `${Math.round(n || 0).toLocaleString()} ${L('EGP', 'جنيه')}`;
    if (phase === 'sent') {
      return L(`Your cancellation request ${code} was sent to the Super App team for: ${what}. We’ll reply within 2 hours, here and in your notifications. Until then it stays active. ${link}`,
        `طلب الإلغاء رقم ${code} اتبعت لفريق Super App عشان: ${what}. هنرد عليك في خلال ساعتين، هنا وفي الإشعارات. لحد ما نرد هيفضل شغال زي ما هو. ${link}`);
    }
    if (phase === 'approved') {
      return L(`Good news: cancellation ${code} was approved. ${what} is cancelled.${rec.refunded ? ` ${money(rec.refunded)} is back in your wallet.` : ''}${rec.note ? ` ${esc(rec.note)}` : ''} ${link}`,
        `طلب الإلغاء ${code} اتوافق عليه. ${what} اتلغى.${rec.refunded ? ` رجعلك ${money(rec.refunded)} في المحفظة.` : ''}${rec.note ? ` ${esc(rec.note)}` : ''} ${link}`);
    }
    return L(`Cancellation ${code} wasn’t approved: ${esc(rec.note || '')} ${what} stays active. ${link}`,
      `طلب الإلغاء ${code} متوافقش عليه: ${esc(rec.note || '')} ${what} هيفضل شغال. ${link}`);
  };
  const cancellation = (rec, phase) => {
    const html = cxText(rec, phase);
    if (ui) { ui.push('bot', html); if (phase === 'sent') setTimeout(() => ui.open(true), 80); return; }
    const st = load() || { id: sessionId(), msgs: [] };
    st.msgs = [...st.msgs, { from: 'bot', html, at: Date.now() }].slice(-60);
    save(st);
  };

  const init = () => {
    const cfg = SA.config.chatbot || {};
    const state = load() || { id: sessionId(), msgs: [] };
    const el = document.createElement('div');
    el.className = 'wa cb';
    el.innerHTML = `
      <div class="wa-card" id="wa-card" role="dialog" aria-label="Chat with Super App assistant" hidden>
        <div class="wa-head">
          <span class="wa-avatar">${GLYPH}<i class="wa-online"></i></span>
          <div class="grow"><b>Super App assistant</b><span class="xs">AI assistant · replies straight away${cfg.hours ? ` · ${cfg.hours}` : ''}</span></div>
          <button class="wa-close" type="button" data-wa-close aria-label="Close">${icon('x', 18)}</button>
        </div>
        <div class="wa-body cb-body" data-cb-body>
          <div class="wa-bubble">Hi there! I’m the Super App assistant. Ask me about a booking, a match, your fantasy team or an order — or pick a topic.</div>
          <div class="wa-topics">${TOPICS.map(([ic, t, k]) => `<button type="button" class="wa-topic" data-topic="${k}" aria-pressed="false">${icon(ic, 16)}<span>${t}</span></button>`).join('')}</div>
          <div class="cb-log" data-cb-log data-i18n-skip aria-live="polite"></div>
        </div>
        <form class="wa-foot cb-foot" data-cb-form novalidate>
          <label class="sr-only" for="cb-in">Type your message</label>
          <input class="input cb-input" id="cb-in" name="msg" maxlength="500" autocomplete="off" placeholder="Type your message…" />
          <button class="btn wa-cta cb-send" type="submit" aria-label="Send">${icon('send', 18)}</button>
        </form>
      </div>
      <button class="wa-fab" type="button" aria-label="Chat with the Super App assistant" aria-expanded="false" aria-controls="wa-card">
        <span class="wa-fab-ico">${GLYPH}</span>
        <span class="wa-fab-label">Chat with us</span>
        <i class="wa-dot" aria-hidden="true"></i>
      </button>`;
    document.body.appendChild(el);

    const fab = el.querySelector('.wa-fab');
    const card = el.querySelector('.wa-card');
    const log = el.querySelector('[data-cb-log]');
    const body = el.querySelector('[data-cb-body]');
    const form = el.querySelector('[data-cb-form]');
    const input = form.msg;
    let busy = false;

    const bubble = (m, i) => `<div class="cb-msg ${m.from === 'me' ? 'is-me' : 'is-bot'}">${m.html}${m.acts?.length && !m.done ? `<div class="row" style="--gap:6px;margin-top:8px;flex-wrap:wrap" data-cb-msg="${i}">${actionButtons(m.acts)}</div>` : ''}</div>`;
    const paint = () => { log.innerHTML = state.msgs.map(bubble).join(''); body.scrollTop = body.scrollHeight; };
    const push = (from, html, acts) => { state.msgs = [...state.msgs, { from, html, at: Date.now(), ...(acts?.length ? { acts } : {}) }].slice(-60); save(state); paint(); };

    /** The customer pressed an agent's button: open a page, or (after a confirm) send the cancellation request */
    const runAction = (msgIdx, actIdx) => {
      const m = state.msgs[msgIdx];
      const a = m?.acts?.[actIdx];
      if (!a || m.done) return;
      if (a.type === 'open') { open(false); SA.core.router.go(String(a.path || '/').replace(/^#/, '')); return; }
      if (!window.confirm(L(`Send a cancellation request for ${a.id}? The Super App team replies within 2 hours.`, `تبعت طلب إلغاء لـ ${a.id}؟ فريق Super App هيرد في خلال ساعتين.`))) return;
      m.done = true; save(state);
      try { SA.services.cancellations.request({ kind: a.kind, refId: a.id, reason: a.reason || '' }); paint(); }
      catch (err) { m.done = false; save(state); push('bot', esc(err.message || L('That couldn’t be sent.', 'مقدرتش أبعت الطلب.'))); }
    };
    log.addEventListener('click', (e) => {
      const b = e.target.closest('[data-cb-act]');
      if (b) runAction(+b.closest('[data-cb-msg]').dataset.cbMsg, +b.dataset.cbAct);
    });

    const ask = async (text, topic = '', fromTopic = false) => {
      if (busy) return;
      const t = String(text || '').trim().slice(0, 500);
      if (!t) return;
      busy = true;
      push('me', esc(t));
      log.insertAdjacentHTML('beforeend', '<div class="cb-msg is-bot cb-typing" aria-label="typing"><i></i><i></i><i></i></div>');
      body.scrollTop = body.scrollHeight;
      let reply = null;
      try { reply = await askAgent(state, t, topic); } catch (e) { console.warn('[chatbot]', e?.message || e); }
      if (!reply) { await new Promise((r) => setTimeout(r, 450)); reply = { html: demoReply(t, topic, fromTopic), actions: [] }; }
      busy = false;
      push('bot', reply.html, reply.actions);
    };

    const open = (on) => {
      card.hidden = !on;
      el.classList.toggle('is-open', on);
      fab.setAttribute('aria-expanded', String(on));
      if (on) { paint(); input.focus({ preventScroll: true }); }
    };
    fab.addEventListener('click', () => open(card.hidden));
    el.querySelector('[data-wa-close]').addEventListener('click', () => { open(false); fab.focus(); });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !card.hidden) { open(false); fab.focus(); } });
    document.addEventListener('click', (e) => { if (!card.hidden && !el.contains(e.target)) open(false); });

    el.querySelectorAll('.wa-topic').forEach((b) => b.addEventListener('click', () => {
      el.querySelectorAll('.wa-topic').forEach((x) => x.setAttribute('aria-pressed', String(x === b)));
      ask(b.querySelector('span').textContent, b.dataset.topic, true);
    }));
    form.addEventListener('submit', (e) => { e.preventDefault(); const v = input.value; input.value = ''; ask(v); });
    // Links in answers open pages on the site: close the chat on the way
    log.addEventListener('click', (e) => { if (e.target.closest('a[href^="#/"]')) open(false); });

    SA.core.router.onChange(() => open(false));
    ui = { push, open };
  };

  SA.ui.chatbot = { init, cancellation, open: () => ui?.open(true) };
})(window.SA);
