/** Modal, drawer, confirm and toast controllers with focus management and Esc-to-close. */
(function (SA) {
  const { icon } = SA.ui;
  const root = () => document.getElementById('overlay-root');
  const stack = [];

  const trapFocus = (el) => (e) => {
    if (e.key !== 'Tab') return;
    const f = [...el.querySelectorAll('a[href],button:not([disabled]),input:not([disabled]),select,textarea,[tabindex]:not([tabindex="-1"])')].filter((n) => n.offsetParent !== null);
    if (!f.length) return;
    const first = f[0], last = f[f.length - 1];
    if (e.shiftKey && document.activeElement === first) { last.focus(); e.preventDefault(); }
    else if (!e.shiftKey && document.activeElement === last) { first.focus(); e.preventDefault(); }
  };

  /**
   * open({ title, body, footer, wide, drawer, side, onMount(el, close) }) → { el, close }
   */
  const open = ({ title = '', subtitle = '', body = '', footer = '', wide = false, drawer = false, side = 'right', onMount, onClose, label } = {}) => {
    const prevFocus = document.activeElement;
    const wrap = document.createElement('div');
    wrap.className = `overlay ${drawer ? 'overlay-drawer' : ''}`;
    wrap.setAttribute('role', 'dialog');
    wrap.setAttribute('aria-modal', 'true');
    wrap.setAttribute('aria-label', label || title.replace(/<[^>]+>/g, ''));
    const head = title ? `<div class="modal-head"><div><h2 class="h3">${title}</h2>${subtitle ? `<p class="small muted" style="margin-top:6px">${subtitle}</p>` : ''}</div><button class="icon-btn" data-close aria-label="Close">${icon('x')}</button></div>` : '';
    wrap.innerHTML = drawer
      ? `<div class="overlay-scrim" data-close></div><aside class="drawer ${side}">${head}<div class="drawer-body">${body}</div>${footer ? `<div class="modal-foot">${footer}</div>` : ''}</aside>`
      : `<div class="overlay-scrim" data-close></div><div class="modal ${wide ? 'modal-wide' : ''}">${head}<div class="modal-body">${body}</div>${footer ? `<div class="modal-foot">${footer}</div>` : ''}</div>`;
    root().appendChild(wrap);
    document.body.style.overflow = 'hidden';
    const trap = trapFocus(wrap);
    let closed = false;
    const close = () => {
      if (closed) return; closed = true;
      wrap.classList.add('is-closing');
      document.removeEventListener('keydown', onKey);
      setTimeout(() => {
        wrap.remove(); stack.splice(stack.indexOf(api), 1);
        if (!stack.length) document.body.style.overflow = '';
        prevFocus?.focus?.();
        onClose?.();
      }, 180);
    };
    const onKey = (e) => { if (e.key === 'Escape' && stack[stack.length - 1] === api) close(); trap(e); };
    document.addEventListener('keydown', onKey);
    wrap.addEventListener('click', (e) => { if (e.target.closest('[data-close]')) close(); });
    const api = { el: wrap, close };
    stack.push(api);
    onMount?.(wrap, close);
    requestAnimationFrame(() => (wrap.querySelector('[autofocus]') || wrap.querySelector('.modal-body input, .drawer-body input, .modal-body button, .drawer-body button') || wrap.querySelector('[data-close]'))?.focus({ preventScroll: true }));
    return api;
  };

  const confirm = ({ title, text, confirmLabel = 'Confirm', cancelLabel = 'Keep it', danger = false }) => new Promise((resolve) => {
    let answered = false;
    const m = open({
      title, body: `<div class="soft">${text}</div>`,
      footer: `<button class="btn btn-outline" data-close>${cancelLabel}</button><button class="btn ${danger ? 'btn-blaze' : 'btn-primary'}" data-ok>${confirmLabel}</button>`,
      onMount: (el, close) => el.querySelector('[data-ok]').addEventListener('click', () => { answered = true; resolve(true); close(); }),
      onClose: () => { if (!answered) resolve(false); },
    });
    return m;
  });

  const toast = (message, { type = 'success', action, onAction, duration = 3800 } = {}) => {
    // A money action sent to the CEO for approval isn't an error
    if (type === 'error' && SA.services.ceo?.isQueued(message)) type = 'info';
    const host = document.getElementById('toast-root');
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    el.innerHTML = `<span class="toast-ico">${icon(type === 'error' ? 'alert' : type === 'info' ? 'info' : 'check', 16, 2.4)}</span><span>${message}</span>${action ? `<button class="toast-action">${action}</button>` : ''}`;
    host.appendChild(el);
    const remove = () => { el.classList.add('is-leaving'); setTimeout(() => el.remove(), 220); };
    el.querySelector('.toast-action')?.addEventListener('click', () => { onAction?.(); remove(); });
    setTimeout(remove, duration);
    while (host.children.length > 3) host.firstElementChild.remove();
  };

  SA.ui.overlay = { open, confirm, toast, closeAll: () => stack.slice().forEach((m) => m.close()) };
})(window.SA);
