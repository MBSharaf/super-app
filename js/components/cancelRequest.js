/**
 * "Cancel" on anything the customer paid for opens this dialog: it explains that a cancellation request is sent to the
 * Super App team (reply within 2 hours, in the chatbot), takes an optional reason and creates the request.
 *   SA.ui.cancelRequest.ask({ kind, refId, title, text }) → the request, or null if the customer kept it
 *   SA.ui.cancelRequest.notice(kind, refId) → a notice for the page while a request is open (or '' when none)
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const CX = () => SA.services.cancellations;

  const ask = ({ kind, refId, title = 'Ask to cancel?', text = '' }) => new Promise((resolve) => {
    let done = false;
    SA.ui.overlay.open({
      title,
      body: `<form class="stack" style="--gap:12px" data-cx novalidate>
          ${text ? `<p class="soft">${text}</p>` : ''}
          <div class="notice">${icon('message', 18)}<span>Your request goes to the Super App team with its own request number. We reply in the chat within <b>${CX().REPLY_HOURS} hours</b>. Until then nothing is cancelled.</span></div>
          <div class="field"><label for="cx-r">Why are you cancelling? (optional)</label><textarea class="input" id="cx-r" name="reason" rows="3" maxlength="300"></textarea></div>
          <p class="field-error" data-err="form"></p>
        </form>`,
      footer: '<button class="btn btn-outline" data-close>Keep it</button><button class="btn btn-blaze" data-cx-send>Send cancellation request</button>',
      onMount(m, close) {
        const f = m.querySelector('[data-cx]');
        const btn = m.querySelector('[data-cx-send]');
        const send = async (e) => {
          e?.preventDefault();
          if (btn.disabled) return;
          btn.disabled = true;
          try { const r = await CX().request({ kind, refId, reason: f.reason.value }); done = true; resolve(r); close(); SA.ui.overlay.toast(`Cancellation request ${r.id} sent`); SA.app.rerender(); }
          catch (err) { f.querySelector('[data-err="form"]').textContent = err.message; btn.disabled = false; }
        };
        f.addEventListener('submit', send);
        btn.addEventListener('click', send);
      },
      onClose: () => { if (!done) resolve(null); },
    });
  });

  const notice = (kind, refId) => {
    const r = CX()?.openFor(kind, refId);
    if (!r) return '';
    return `<div class="notice gold" style="margin-top:14px">${icon('clock', 18)}<span><b>Cancellation request <span class="num">${esc(r.id)}</span> sent.</b> The Super App team replies in the chat within ${CX().REPLY_HOURS} hours. Until then it stays active. <button class="link" type="button" data-cx-chat>Open the chat</button></span></div>`;
  };
  document.addEventListener('click', (e) => { if (e.target.closest('[data-cx-chat]')) SA.ui.chatbot?.open(); });

  SA.ui.cancelRequest = { ask, notice };
})(window.SA);
