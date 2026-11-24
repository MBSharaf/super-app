/**
 * "Pick your favourite clubs" (up to 3, in order): asked once after the first sign-in on the live site, and from the
 * profile / notification settings. Clubs come from the server (current seasons of the leagues on the Matches page),
 * grouped by league, with a search. Saving sends PUT /me/favorite-clubs; the server then tells this person about
 * their clubs' line-ups, kick-off, goals and results.
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const MAX = 3;
  const F = () => SA.services.follow;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  /** The crest renderer wants club colours and a short name; server clubs have a badge and a short name */
  const crest = (c, size = 40) => SA.ui.sports.crest({ name: c.name, short: (c.shortName || c.name).slice(0, 4).toUpperCase(), a: '#1E7A4C', b: '#0D1411', logo: c.logo }, size);
  const name = (c) => F().clubName(c);
  const norm = (s) => String(s || '').toLowerCase().normalize('NFKD').replace(/[ً-ٟ]/g, '').replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي');

  const open = ({ first = false, onSaved } = {}) => {
    let comps = [], picked = [], tab = '', q = '';
    const m = SA.ui.overlay.open({
      wide: true,
      title: first ? 'Pick your favourite clubs' : 'My clubs',
      subtitle: `Choose up to ${MAX}. We’ll tell you about their line-ups, kick-off, goals and results.`,
      body: `<div class="fp-wrap"><div class="fp-picked" data-picked></div>
        <div class="fp-tools"><input class="input" type="search" data-q placeholder="Search clubs" aria-label="Search clubs" /><div class="chips" data-tabs></div></div>
        <div class="fp-grid" data-grid><p class="small muted">Loading clubs…</p></div></div>`,
      footer: `<button class="btn btn-ghost" data-skip>${first ? 'Not now' : 'Cancel'}</button><button class="btn btn-primary" data-save disabled>Save</button>`,
      onMount(el, close) {
        const $ = (s) => el.querySelector(s);
        const all = () => comps.flatMap((c) => c.clubs);
        const isOn = (c) => picked.some((p) => p.id === c.id);

        const drawPicked = () => {
          $('[data-picked]').innerHTML = Array.from({ length: MAX }, (_, i) => {
            const c = picked[i];
            return c ? `<button class="fp-slot is-on" data-drop="${c.id}" aria-label="Remove ${esc(c.name)}"><span class="fp-n">${i + 1}</span>${crest(c, 26)}<span class="grow" data-i18n-skip>${esc(name(c))}</span>${icon('x', 14)}</button>`
              : `<span class="fp-slot"><span class="fp-n">${i + 1}</span><span class="muted small">Empty</span></span>`;
          }).join('');
          const btn = $('[data-save]');
          btn.disabled = !picked.length && first;
          btn.textContent = picked.length ? `Save ${picked.length} of ${MAX}` : 'Save';
        };
        const drawGrid = () => {
          const words = norm(q).trim();
          const list = (words ? all() : (comps.find((c) => c.key === tab) || comps[0])?.clubs || [])
            .filter((c, i, arr) => arr.findIndex((x) => x.id === c.id) === i)
            .filter((c) => !words || norm(`${c.name} ${c.nameAr} ${c.shortName}`).includes(words));
          $('[data-grid]').innerHTML = list.length ? list.map((c) => { const n = picked.findIndex((p) => p.id === c.id); return `<button class="fp-club ${n >= 0 ? 'is-on' : ''}" data-club="${c.id}" aria-pressed="${n >= 0}">
              ${n >= 0 ? `<span class="fp-rank">${n + 1}</span>` : ''}${crest(c, 40)}<span class="fp-name" data-i18n-skip>${esc(name(c))}</span></button>`; }).join('')
            : `<p class="small muted">No club matches “${esc(q)}”.</p>`;
        };
        const drawTabs = () => {
          $('[data-tabs]').innerHTML = comps.map((c) => `<button class="chip ${c.key === tab && !q ? 'is-active' : ''}" data-tab="${esc(c.key)}" data-i18n-skip>${esc(SA.i18n?.isAr ? c.nameAr || c.name : c.name)}</button>`).join('');
        };
        const draw = () => { drawPicked(); drawTabs(); drawGrid(); };

        el.addEventListener('click', (e) => {
          const club = e.target.closest('[data-club]'), drop = e.target.closest('[data-drop]'), t = e.target.closest('[data-tab]');
          if (t) { tab = t.dataset.tab; q = ''; $('[data-q]').value = ''; draw(); return; }
          if (drop) { picked = picked.filter((p) => String(p.id) !== drop.dataset.drop); draw(); return; }
          if (!club) return;
          const c = all().find((x) => String(x.id) === club.dataset.club);
          if (!c) return;
          if (isOn(c)) picked = picked.filter((p) => p.id !== c.id);
          else if (picked.length >= MAX) { toast(`You can pick up to ${MAX} clubs. Remove one first.`, 'info'); return; }
          else picked = [...picked, c];
          draw();
        });
        $('[data-q]').addEventListener('input', (e) => { q = e.target.value; drawTabs(); drawGrid(); });
        $('[data-skip]').addEventListener('click', () => { close(); if (first) toast('You can pick your clubs any time from your profile.', 'info'); });
        $('[data-save]').addEventListener('click', async (e) => {
          e.currentTarget.disabled = true;
          try {
            const saved = await F().setMine(picked.map((p) => p.id));
            close(); toast(saved.length ? 'Your clubs are saved. We’ll keep you posted.' : 'Your clubs are cleared.');
            onSaved?.(saved); SA.app?.rerender?.();
          } catch (err) { toast(err.message, 'error'); e.currentTarget.disabled = false; }
        });

        Promise.all([F().competitions(), F().mine().catch(() => [])]).then(([cs, mine]) => {
          comps = cs.filter((c) => c.clubs.length);
          tab = comps[0]?.key || '';
          const byId = new Map(comps.flatMap((c) => c.clubs).map((c) => [c.id, c]));
          picked = mine.map((c) => byId.get(c.id) || c).slice(0, MAX);
          draw();
        }).catch((err) => { $('[data-grid]').innerHTML = `<p class="small text-danger">${esc(err.message)}</p>`; });
      },
    });
    return m;
  };

  SA.ui.clubPicker = { open, crest };
})(window.SA);
