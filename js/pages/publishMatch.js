(function (SA) {
  const { esc, money, fmtHour, relDay, fmtRange, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  SA.pages.publishMatch = {
    title: 'Post your match — Super App',
    render(ctx) {
      const bookings = S.bookings.upcomingSync().filter((b) => !b.publishedMatchId);
      const head = c.pageHead({ crumbs: [['Find players', '#/players'], ['Post your match']], title: 'Post your match', text: 'Tell players what kind of game it is and how many you need. We’ll show it to players near the pitch.' });
      if (!bookings.length) {
        return `${head}<div class="container"><div class="panel">${c.empty({ icon: 'pitch', title: 'Book a pitch first', text: 'You post a match from one of your upcoming bookings, so players know exactly where and when to show up.', action: `<div class="row wrap" style="justify-content:center">${c.cta('Book a pitch', { href: '#/pitches' })}<a class="btn btn-outline" href="#/players">Join someone else’s game</a></div>` })}</div></div>`;
      }
      const selected = bookings.find((b) => b.id === ctx.query.booking) || bookings[0];
      return `${head}
      <div class="container">
        <div class="publish-layout">
          <form id="publish-form" class="stack" style="--gap:20px" novalidate>
            <section class="panel panel-pad">
              <h2 class="h4">1. Which booking?</h2>
              <div class="stack" style="--gap:10px;margin-top:14px">${bookings.map((b) => `<label class="choice"><input type="radio" name="bookingId" value="${b.id}" ${b.id === selected.id ? 'checked' : ''} data-players="${b.players}" /><span class="radio-dot"></span>
                <span class="media" style="width:56px;height:56px;border-radius:12px;flex:none"><img src="${media.photo(b.image, 120, 120)}" alt="" /></span>
                <span class="grow"><b>${esc(b.pitchName)}</b><span class="xs muted" style="display:block">${fmtDate(b.date)} · ${fmtRange(b.hour, b.duration)} · ${esc(b.format)}</span></span><span class="small num">${money(b.total)}</span></label>`).join('')}</div>
            </section>
            <section class="panel panel-pad">
              <h2 class="h4">2. Players</h2>
              <div class="form-grid" style="margin-top:14px">
                <div class="field"><span class="field-label">Total players for the game</span><div class="qty" data-qty="total"><button type="button" data-step="-1" aria-label="Fewer">${icon('minus', 16)}</button><output name="total">${selected.players}</output><button type="button" data-step="1" aria-label="More">${icon('plus', 16)}</button></div></div>
                <div class="field"><span class="field-label">Players you already have (incl. you)</span><div class="qty" data-qty="have"><button type="button" data-step="-1" aria-label="Fewer">${icon('minus', 16)}</button><output name="have">${Math.max(1, selected.players - 3)}</output><button type="button" data-step="1" aria-label="More">${icon('plus', 16)}</button></div></div>
              </div>
              <div class="need-callout" data-need></div>
            </section>
            <section class="panel panel-pad">
              <h2 class="h4">3. What kind of game?</h2>
              <div class="form-grid" style="margin-top:14px">
                <div class="field"><label for="pm-level">Skill level</label><select class="select" id="pm-level" name="level">${SA.data.matches.levels.map((l) => `<option ${l === 'Intermediate' ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
                <div class="field"><label for="pm-type">Match type</label><select class="select" id="pm-type" name="type">${['5 v 5', '6 v 6', '7 v 7', '8 v 8', '11 v 11', 'Training session'].map((t) => `<option ${t === (selected.format.startsWith('7') ? '7 v 7' : selected.format.startsWith('11') ? '11 v 11' : '5 v 5') ? 'selected' : ''}>${t}</option>`).join('')}</select></div>
                <div class="field span-2"><span class="field-label">Who can join?</span><div class="tabs" role="radiogroup" data-gender>${SA.data.matches.genders.map((g, i) => `<button type="button" class="tab" role="radio" data-v="${g}" aria-selected="${i === 1}" aria-checked="${i === 1}">${g === 'Men' ? 'Men only' : g === 'Women' ? 'Women only' : 'Mixed'}</button>`).join('')}</div></div>
                <label class="choice span-2"><input type="checkbox" name="needsGK" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>We need a goalkeeper</b><span class="xs muted" style="display:block">Keepers get a highlighted badge and are notified first</span></span>${icon('glove', 20)}</label>
                <div class="field span-2"><label for="pm-notes">Notes for players <span class="muted">(optional)</span></label><textarea class="textarea" id="pm-notes" name="notes" maxlength="280" placeholder="E.g. Friendly game, bring a dark and a white shirt. Rolling subs."></textarea><span class="field-hint" data-count>0 / 280</span></div>
              </div>
            </section>
          </form>
          <aside class="publish-preview">
            <p class="small muted" style="margin-bottom:10px">${icon('eye', 14)} Preview — how players will see it</p>
            <div data-preview></div>
            <div class="panel panel-pad" style="margin-top:16px">
              <dl class="kv"><div><dt>Each new player pays</dt><dd class="num" data-share></dd></div><div><dt>You’ll get back up to</dt><dd class="num" data-back></dd></div></dl>
              <button class="btn btn-blaze btn-lg btn-block" style="margin-top:16px" form="publish-form" type="submit">${icon('users', 18)} Publish match</button>
              <p class="xs muted" style="margin-top:10px;text-align:center">Players’ payments go to your wallet after the game.</p>
            </div>
          </aside>
        </div>
      </div>`;
    },
    mount(el) {
      const form = el.querySelector('#publish-form');
      if (!form) return;
      let gender = 'Men';
      const out = (n) => form.querySelector(`output[name="${n}"]`);
      const booking = () => S.bookings.getSync(form.querySelector('[name="bookingId"]:checked').value);
      const update = () => {
        const b = booking();
        let total = +out('total').value, have = +out('have').value;
        have = Math.min(have, total - 1); out('have').value = have;
        const need = total - have;
        const share = Math.ceil(b.total / total);
        el.querySelector('[data-need]').innerHTML = `<b class="num">${need}</b><span>player${need > 1 ? 's' : ''} needed to complete your ${total}-player game</span>`;
        el.querySelector('[data-share]').textContent = money(share);
        el.querySelector('[data-back]').textContent = money(share * need);
        const fake = { id: 'preview', date: b.date, hour: b.hour, duration: b.duration, level: form.level.value, type: form.type.value, gender, needsGK: form.needsGK.checked,
          pitch: { name: b.pitchName, area: b.area }, distanceKm: 0, hostName: S.user.me().name, filled: have, total, spotsLeft: need, pricePerPlayer: share, isMine: false, joined: false };
        el.querySelector('[data-preview]').innerHTML = SA.ui.cards.match(fake).replace(/href="#\/players\/preview"/, 'href="#" tabindex="-1"');
        el.querySelector('[data-preview] [data-action]')?.setAttribute('tabindex', '-1');
      };
      form.addEventListener('click', (e) => {
        const step = e.target.closest('[data-step]');
        if (step) {
          const key = step.closest('[data-qty]').dataset.qty, o = out(key);
          const max = key === 'total' ? 22 : +out('total').value - 1;
          o.value = Math.max(key === 'total' ? 4 : 1, Math.min(max, +o.value + +step.dataset.step));
          update();
        }
        const g = e.target.closest('[data-gender] [data-v]');
        if (g) { gender = g.dataset.v; el.querySelectorAll('[data-gender] [data-v]').forEach((b) => { b.setAttribute('aria-selected', b === g); b.setAttribute('aria-checked', b === g); }); update(); }
      });
      form.addEventListener('change', (e) => {
        if (e.target.name === 'bookingId') { out('total').value = e.target.dataset.players; out('have').value = Math.max(1, +e.target.dataset.players - 3); }
        update();
      });
      const notes = form.notes;
      notes.addEventListener('input', () => (el.querySelector('[data-count]').textContent = `${notes.value.length} / 280`));
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const btn = el.querySelector('button[form="publish-form"]');
        btn.disabled = true; btn.innerHTML = '<span class="spinner"></span> Publishing…';
        try {
          const { id } = await S.matches.publish({ bookingId: booking().id, total: out('total').value, have: out('have').value, level: form.level.value, type: form.type.value, gender, needsGK: form.needsGK.checked, notes: notes.value });
          SA.core.router.go(`/players/${id}?published=1`);
        } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); btn.disabled = false; btn.innerHTML = 'Publish match'; }
      });
      update();
    },
  };
})(window.SA);
