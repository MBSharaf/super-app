/**
 * My games (#/my-games): the games I posted — with their join requests, so I can manage them in one tap — and the
 * games I asked to join or play in.
 */
(function (SA) {
  const { esc, money, fmtDate, fmtHour } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const RS = { pending: ['badge-gold', 'Waiting for the host'], accepted: ['badge-turf', 'You’re in'], waitlist: ['badge-info', 'Waitlist'], declined: ['', 'Not accepted'], withdrawn: ['', 'You left'], removed: ['badge-danger', 'Removed'] };

  const row = (m, side) => {
    const reqs = S.games.forMatch(m.id);
    const pending = reqs.filter((r) => r.status === 'pending').length;
    const inGame = reqs.filter((r) => r.status === 'accepted').length;
    const mine = side === 'join' ? S.games.myRequest(m.id) || reqs.find((r) => r.player.id === S.user.me().id) : null;
    const past = new Date(`${m.date}T00:00`).getTime() + m.hour * 36e5 < Date.now();
    return `<a class="card card-link mg-row ${past ? 'is-past' : ''}" href="#/players/${m.id}">
      <span class="mg-when"><b class="num">${fmtDate(m.date)}</b><span class="xs">${fmtHour(m.hour)}</span></span>
      <span class="grow"><b>${esc(m.pitch?.name || m.pitchName || '')}</b><span class="small muted" style="display:block">${esc(m.type || '')} · ${m.filled ?? ''}/${m.total ?? ''} players · ${money(m.pricePerPlayer)} each</span></span>
      ${side === 'host' ? `<span class="row wrap" style="--gap:6px">${pending ? `<span class="badge badge-blaze">${pending} request${pending > 1 ? 's' : ''} to answer</span>` : ''}<span class="badge">${inGame} joined</span>${m.autoAccept ? '<span class="badge badge-turf">Auto-accept</span>' : ''}${m.closed ? '<span class="badge">Closed</span>' : ''}</span>`
        : mine ? `<span class="badge ${RS[mine.status]?.[0] || ''}">${RS[mine.status]?.[1] || mine.status}</span>` : '<span class="badge badge-turf">Joined</span>'}
      ${icon('chevronRight', 18)}</a>`;
  };

  SA.pages.myGames = {
    title: 'My games — Super App',
    async render() {
      const { joined, hosting } = await S.matches.mine();
      const asked = S.db.all('gameRequests').filter((r) => r.player.id === S.user.me().id).map((r) => r.matchId);
      const joinList = [...new Map([...joined, ...(await S.matches.list({})).filter((m) => asked.includes(m.id))].map((m) => [m.id, m])).values()];
      const toAnswer = hosting.reduce((t, m) => t + S.games.forMatch(m.id).filter((r) => r.status === 'pending').length, 0);
      return `<div class="container section-tight mg-page">
        <div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">My games</h1><p class="lead">The games you posted and the ones you joined — answer requests and manage players from here.</p></div>
        <div class="row wrap" style="--gap:8px;margin-bottom:16px"><a class="btn btn-blaze" href="#/players/new">${icon('plus', 16)} Post a game</a><a class="btn btn-outline" href="#/players">${icon('users', 16)} Find a game</a></div>
        <section class="section-tight"><div class="sec-head"><h2 class="h3">Games you posted ${toAnswer ? `<span class="badge badge-blaze">${toAnswer} to answer</span>` : ''}</h2></div>
          ${hosting.length ? `<div class="stack" style="--gap:10px">${hosting.map((m) => row(m, 'host')).join('')}</div>` : `<div class="panel">${c.empty({ icon: 'users', title: 'You haven’t posted a game', text: 'Booked a pitch and short of players? Post it — players near you ask to join and pay their share.', action: '<a class="btn btn-primary" href="#/players/new">Post a game</a>' })}</div>`}</section>
        <section class="section-tight"><div class="sec-head"><h2 class="h3">Games you joined</h2></div>
          ${joinList.length ? `<div class="stack" style="--gap:10px">${joinList.map((m) => row(m, 'join')).join('')}</div>` : `<div class="panel">${c.empty({ icon: 'ball', title: 'No games yet', text: 'Find a game near you and ask to join.', action: '<a class="btn btn-primary" href="#/players">Find a game</a>' })}</div>`}</section>
      </div>`;
    },
  };
})(window.SA);
