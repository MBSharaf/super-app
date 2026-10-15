/**
 * Floating WhatsApp support button (all pages), styled in Super App brand colours.
 * The chat link comes from SA.config.whatsapp.url. While it is empty the button and card still
 * render, but "Open WhatsApp" doesn't navigate anywhere.
 */
(function (SA) {
  const { icon } = SA.ui;
  const GLYPH = '<svg viewBox="0 0 24 24" width="26" height="26" aria-hidden="true" focusable="false"><path fill="currentColor" d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413Z"/></svg>';

  const TOPICS = [
    ['pitch', 'Help with a booking'],
    ['users', 'Finding players'],
    ['trophy', 'Fantasy & leagues'],
    ['package', 'Order or delivery'],
  ];

  const init = () => {
    const cfg = SA.config.whatsapp || {};
    const el = document.createElement('div');
    el.className = 'wa';
    el.innerHTML = `
      <div class="wa-card" id="wa-card" role="dialog" aria-label="WhatsApp support" hidden>
        <div class="wa-head">
          <span class="wa-avatar">${GLYPH}<i class="wa-online"></i></span>
          <div class="grow"><b>Super App support</b><span class="xs">Usually replies in a few minutes · ${cfg.hours || ''}</span></div>
          <button class="wa-close" type="button" data-wa-close aria-label="Close">${icon('x', 18)}</button>
        </div>
        <div class="wa-body">
          <div class="wa-bubble">Hi there! Need a hand with a booking, a match, your fantasy team or an order? Pick a topic and chat with us on WhatsApp.</div>
          <div class="wa-topics">${TOPICS.map(([ic, t], i) => `<button type="button" class="wa-topic" data-topic="${t}" aria-pressed="${i === 0}">${icon(ic, 16)}<span>${t}</span></button>`).join('')}</div>
        </div>
        <div class="wa-foot">
          <a class="btn wa-cta btn-block" data-wa-open role="button" ${cfg.url ? `href="${cfg.url}" target="_blank" rel="noopener"` : 'aria-disabled="true" tabindex="0"'}>${GLYPH.replace('width="26" height="26"', 'width="18" height="18"')} Open WhatsApp</a>
        </div>
      </div>
      <button class="wa-fab" type="button" aria-label="Chat with us on WhatsApp" aria-expanded="false" aria-controls="wa-card">
        <span class="wa-fab-ico">${GLYPH}</span>
        <span class="wa-fab-label">Chat with us</span>
        <i class="wa-dot" aria-hidden="true"></i>
      </button>`;
    document.body.appendChild(el);

    const fab = el.querySelector('.wa-fab');
    const card = el.querySelector('.wa-card');
    const open = (on) => {
      card.hidden = !on;
      el.classList.toggle('is-open', on);
      fab.setAttribute('aria-expanded', String(on));
      if (on) card.querySelector('.wa-topic')?.focus({ preventScroll: true });
    };
    fab.addEventListener('click', () => open(card.hidden));
    el.querySelector('[data-wa-close]').addEventListener('click', () => { open(false); fab.focus(); });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !card.hidden) { open(false); fab.focus(); } });
    document.addEventListener('click', (e) => { if (!card.hidden && !el.contains(e.target)) open(false); });

    // Topic choice is prefilled into the chat message once a link exists
    el.querySelectorAll('.wa-topic').forEach((b) => b.addEventListener('click', () => {
      el.querySelectorAll('.wa-topic').forEach((x) => x.setAttribute('aria-pressed', String(x === b)));
      if (cfg.url) {
        const sep = cfg.url.includes('?') ? '&' : '?';
        el.querySelector('[data-wa-open]').href = `${cfg.url}${sep}text=${encodeURIComponent(`Hi Super App, I need help with: ${b.dataset.topic}`)}`;
      }
    }));
    // No link yet: keep the button inert
    el.querySelector('[data-wa-open]').addEventListener('click', (e) => { if (!cfg.url) e.preventDefault(); });

    // Close when navigating between pages
    SA.core.router.onChange(() => open(false));
  };

  SA.ui.whatsapp = { init };
})(window.SA);
