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
  const askAgent = async (state, message, topic) => {
    const url = SA.config.chatbot?.url;
    if (!url) return null;
    const u = SA.services.user?.me?.() || null;
    const res = await fetch(url, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId: state.id, message, topic: topic || null, lang: ar() ? 'ar' : 'en', page: location.hash || '#/', user: u ? { id: u.id, name: u.name, email: u.email || null } : null }),
    });
    if (!res.ok) throw new Error(`chatbot ${res.status}`);
    const data = await res.json().catch(() => ({}));
    const body = Array.isArray(data) ? data[0] : data;
    const reply = body?.reply ?? body?.output ?? body?.text;
    return reply ? esc(String(reply)).replace(/\n/g, '<br>') : null;
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

    const bubble = (m) => `<div class="cb-msg ${m.from === 'me' ? 'is-me' : 'is-bot'}">${m.html}</div>`;
    const paint = () => { log.innerHTML = state.msgs.map(bubble).join(''); body.scrollTop = body.scrollHeight; };
    const push = (from, html) => { state.msgs = [...state.msgs, { from, html, at: Date.now() }].slice(-60); save(state); paint(); };

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
      if (!reply) { await new Promise((r) => setTimeout(r, 450)); reply = demoReply(t, topic, fromTopic); }
      busy = false;
      push('bot', reply);
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
  };

  SA.ui.chatbot = { init };
})(window.SA);
