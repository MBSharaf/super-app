/**
 * Admin console: free slots & games (#/admin/games) — slot cards from venues (limit, stop a venue, hide a card),
 * games with join requests (remove a player with a refund) and player records (block players who don't show up).
 */
(function (SA) {
  const { esc, money, fmtDate, fmtHour, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const RS = { pending: ['badge-gold', 'Waiting'], accepted: ['badge-turf', 'In'], declined: ['', 'Declined'], withdrawn: ['', 'Withdrew'], removed: ['badge-danger', 'Removed'], waitlist: ['badge-info', 'Waitlist'] };

  SA.pages.adminGames = A.guard({
    title: 'Free slots & games — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const slots = db().all('communityPosts').filter((p) => !p.deleted && p.card?.type === 'slot').sort((a, b) => b.at - a.at);
      const states = slots.map((p) => SA.ui.linkCards.slotState(p.card).state);
      const booked = states.filter((s) => s === 'booked').length;
      const games = AD().gamesOverview();
      const players = AD().playerRecords();
      const limit = db().settings().slotPosting?.perDay ?? 5;
      let section = '';
      if (!tab) {
        const owners = db().all('accounts').filter((a) => a.role === 'owner');
        const byOwner = owners.map((a) => { const mine = slots.filter((p) => p.author?.id === a.id); const st = mine.map((p) => SA.ui.linkCards.slotState(p.card).state); return { a, n: mine.length, booked: st.filter((s) => s === 'booked').length }; });
        section = `<div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">Posting rules</h2><form class="row wrap" data-limit style="--gap:10px;margin-top:12px;align-items:flex-end"><label class="field" style="margin:0"><span class="field-label">Free slots each venue can post a day</span><input class="input num" name="perDay" type="number" min="0" max="50" value="${limit}" /></label><button class="btn btn-primary" type="submit">Save</button></form>
            <p class="xs muted" style="margin-top:8px">Stops the community turning into ads. Only real free hours can be posted — the site checks the pitch’s calendar.</p></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Venues</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Venue</th><th class="r">Posted</th><th class="r">Booked</th><th>Posting</th><th></th></tr></thead><tbody>
            ${byOwner.map((x) => `<tr><td class="small"><b>${esc(x.a.company)}</b></td><td class="r num">${x.n}</td><td class="r num">${x.booked}${x.n ? ` <span class="xs muted">(${Math.round((x.booked / x.n) * 100)}%)</span>` : ''}</td><td>${x.a.slotBlocked ? `<span class="badge badge-danger" title="${esc(x.a.slotBlocked)}">Stopped</span>` : '<span class="badge badge-turf">Allowed</span>'}</td>
              <td class="r"><button class="btn btn-ghost btn-sm" data-slot-block="${x.a.id}" data-on="${x.a.slotBlocked ? '' : '1'}">${x.a.slotBlocked ? 'Allow' : 'Stop posting'}</button></td></tr>`).join('')}
          </tbody></table></div></section></div>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Slot cards</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Pitch</th><th>When</th><th>Discount</th><th>Posted</th><th>Status</th><th></th></tr></thead><tbody>
            ${slots.slice(0, 40).map((p, i) => { const pt = db().find('pitches', p.card.pitchId); const s = states[i]; return `<tr class="${p.hidden ? 'is-dim' : ''}"><td class="small"><b>${esc(pt?.name || '')}</b><span class="xs muted" style="display:block">${esc(pt?.area || '')}</span></td><td class="small nowrap">${fmtDate(p.card.date)} · ${fmtHour(p.card.hour)}</td><td class="small">${p.card.pct ? `${Math.round(p.card.pct * 100)}%` : '—'}</td><td class="xs muted">${timeAgo(p.at)}</td>
              <td><span class="badge ${s === 'free' ? 'badge-info' : s === 'booked' ? 'badge-turf' : ''}">${s === 'free' ? 'Free' : s === 'booked' ? 'Booked' : 'Expired'}</span>${p.hidden ? ' <span class="badge badge-danger">Hidden</span>' : ''}</td>
              <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/community/pitches/${p.id}" target="_blank" rel="noopener">View</a><button class="btn btn-ghost btn-sm" data-hide-slot="${p.id}" data-on="${p.hidden ? '' : '1'}">${p.hidden ? 'Show' : 'Hide'}</button></div></td></tr>`; }).join('') || '<tr><td colspan="6" class="small muted" style="padding:18px">No slots posted yet.</td></tr>'}
          </tbody></table></div></section>`;
      } else if (tab === 'games') {
        section = games.length ? games.map((g) => `<section class="dash-card"><div class="row-between wrap" style="--gap:8px"><div><h2 class="h4">${esc(g.pitchName || '')} · ${g.date ? fmtDate(g.date) : ''} ${g.hour != null ? fmtHour(g.hour) : ''}</h2><p class="xs muted" style="margin-top:2px">Host: ${esc(g.hostName || '')} · ${g.m ? `${g.m.filled}/${g.m.total} players · ${money(g.m.pricePerPlayer)} each${g.m.isMine ? ' · this device' : ''}` : 'no longer listed'}</p></div>${g.m ? `<a class="btn btn-ghost btn-sm" href="#/players/${g.id}" target="_blank" rel="noopener">Open game</a>` : ''}</div>
            <ul class="pl-list">${g.list.map((r) => `<li class="pl-req">${SA.ui.c.avatar({ name: r.player.name, avatar: r.player.avatar })}<span class="grow"><span class="row wrap" style="--gap:6px"><b class="small">${esc(r.player.name)}</b><span class="badge">${esc(r.position)}</span><span class="badge ${RS[r.status][0]}">${RS[r.status][1]}</span>${r.paid ? `<span class="xs muted">${money(r.amount)} ${['declined', 'withdrawn', 'removed'].includes(r.status) ? 'refunded' : 'held'}</span>` : ''}</span>${SA.ui.playerRecord ? SA.ui.playerRecord(r.record) : ''}${r.reason ? `<span class="xs muted" style="display:block">${esc(r.reason)}</span>` : ''}</span>
              <span class="pl-act">${['accepted', 'pending', 'waitlist'].includes(r.status) ? `<button class="btn btn-ghost btn-sm" data-remove-req="${r.id}" style="color:var(--danger)">Remove & refund</button>` : ''}</span></li>`).join('')}</ul></section>`).join('')
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'users', title: 'No join requests yet', text: 'Games where players asked to join show up here.' })}</div>`;
      } else {
        section = `<section class="dash-card table-card"><p class="xs muted" style="padding:14px 18px 0">Sorted by attendance, lowest first. Hosts rate players after each game; this record is shown to hosts before they accept someone.</p><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Player</th><th class="r">Games</th><th class="r">Showed up</th><th class="r">Rating</th><th class="r">Asked / in</th><th>Status</th><th></th></tr></thead><tbody>
          ${players.map((x) => `<tr><td class="small"><span class="row" style="--gap:8px">${SA.ui.c.avatar({ name: x.player.name, avatar: x.player.avatar }, 'sm')}<span><b>${esc(x.player.name)}</b>${x.player.sim ? '' : ' <span class="badge badge-info">This device</span>'}</span></span></td><td class="r num">${x.record.games}</td>
            <td class="r num"><span class="pl-att is-${x.record.reliable}">${x.record.attendance != null ? `${Math.round(x.record.attendance * 100)}%` : 'New'}</span>${x.record.noShows ? `<span class="xs muted" style="display:block">${x.record.noShows} no-show${x.record.noShows > 1 ? 's' : ''}</span>` : ''}</td><td class="r num">${x.record.rating ? x.record.rating.toFixed(1) : '—'}</td><td class="r num">${x.asked} / ${x.accepted}</td>
            <td>${x.blocked ? `<span class="badge badge-danger" title="${esc(x.blocked.reason)}">Blocked${x.blocked.until ? ` until ${fmtDate(new Date(x.blocked.until))}` : ''}</span>` : '<span class="badge badge-turf">Can join</span>'}</td>
            <td class="r">${x.blocked ? `<button class="btn btn-ghost btn-sm" data-unblock="${esc(x.player.id)}">Unblock</button>` : `<button class="btn btn-ghost btn-sm" data-block="${esc(x.player.id)}" data-name="${esc(x.player.name)}">Block</button>`}</td></tr>`).join('') || '<tr><td colspan="7" class="small muted" style="padding:18px">No players yet.</td></tr>'}
        </tbody></table></div></section>`;
      }
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Slots posted', value: slots.length, ic: 'megaphone' })}
          ${C.kpi({ label: 'Booked from cards', value: booked, ic: 'check', tone: 'k-turf', sub: slots.length ? `${Math.round((booked / slots.length) * 100)}% of slots` : '' })}
          ${C.kpi({ label: 'Games with requests', value: games.length, ic: 'users', sub: `${games.filter((g) => g.m && g.m.spotsLeft === 0).length} full` })}
          ${C.kpi({ label: 'Blocked players', value: players.filter((x) => x.blocked).length, ic: 'ban' })}
        </div>
        ${A.tabs('/admin/games', [['', 'Free slots', slots.length], ['games', 'Games & requests', games.length], ['players', 'Player records', players.length]], tab)}${section}`;
      return C.layout('games', { title: 'Free slots & games', subtitle: 'Venues’ free-slot cards, games looking for players, and every player’s record', body });
    },
    mount(el) {
      el.querySelector('[data-limit]')?.addEventListener('submit', (e) => { e.preventDefault(); try { AD().setSlotLimit(e.target.perDay.value); toast('Limit saved'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-slot-block]')) { const b = t('[data-slot-block]'); if (b.dataset.on) { const r = await A.reason({ title: 'Stop this venue posting free slots?', label: 'Reason the venue sees', confirmLabel: 'Stop posting', danger: true }); if (!r) return; AD().setSlotBlock(b.dataset.slotBlock, r.reason); } else AD().setSlotBlock(b.dataset.slotBlock, ''); toast('Saved', 'info'); SA.app.rerender(); return; }
          if (t('[data-hide-slot]')) { const b = t('[data-hide-slot]'); AD().setPost(b.dataset.hideSlot, { hidden: !!b.dataset.on }, b.dataset.on ? 'Hid slot card' : 'Restored slot card'); toast(b.dataset.on ? 'Slot card hidden' : 'Slot card restored', 'info'); SA.app.rerender(); return; }
          if (t('[data-remove-req]')) { const r = await A.reason({ title: 'Remove this player from the game?', text: 'Their share is refunded in full. The first player on the waitlist moves up.', label: 'Reason (saved in the audit log)', confirmLabel: 'Remove & refund', danger: true }); if (!r) return; await AD().removeFromGame(t('[data-remove-req]').dataset.removeReq, r.reason); toast('Player removed and refunded', 'info'); SA.app.rerender(); return; }
          if (t('[data-block]')) { const b = t('[data-block]'); const r = await A.reason({ title: `Block ${esc(b.dataset.name)} from games?`, text: 'They can still book pitches, but can’t ask to join games.', label: 'Reason they’ll see', confirmLabel: 'Block', danger: true, extra: '<div class="field"><label for="bl-d">For</label><select class="select" id="bl-d" name="days"><option value="7">7 days</option><option value="30" selected>30 days</option><option value="0">Until unblocked</option></select></div>' }); if (!r) return; AD().blockPlayer({ playerId: b.dataset.block, name: b.dataset.name, days: +r.days, reason: r.reason }); toast('Player blocked from games', 'info'); SA.app.rerender(); return; }
          if (t('[data-unblock]')) { AD().unblockPlayer(t('[data-unblock]').dataset.unblock); toast('Player can join games again'); SA.app.rerender(); }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
