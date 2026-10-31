/**
 * The owner's automation (n8n) on the demo site (GitHub Pages, no server): every new store order, pitch booking, weekly
 * package, published game and request to join a game is sent with all its details to the links in
 * `SA.config.automation` (orders → `orders`; bookings, packages and games → `bookings`). Fantasy: a new private league
 * (`league.created`), someone joining a league (`league.member.joined`) and a finished gameweek (`gameweek.finished`, one
 * per league with every member's rank and points and the gameweek's top three) → `fantasy` (the orders link). In API mode the server sends
 * these events itself (its webhooks), so nothing is sent from the browser there.
 *
 * Sent as JSON. The links are ngrok tunnels: a POST from a browser goes through (ngrok's warning page is only shown for
 * page visits). If the browser refuses the cross-site call, the same JSON is sent again as plain text, so n8n still gets it.
 */
(function (SA) {
  const S = SA.services;
  const cfg = () => SA.config.automation || {};
  const db = () => S.db;
  const iso = (t) => (t ? new Date(t).toISOString() : null);
  /** A page address on this site (clean URLs: /super-app/order/ORD-1) */
  const link = (path) => new URL(SA.core.router.url(path), location.href).href;
  const hh = (h) => `${String(Math.floor(h) % 24).padStart(2, '0')}:${h % 1 ? '30' : '00'}`;
  const STORE_COMMISSION = 0.05;   // owner's decision (2026-10-27): 5% on every purchase; Super App's own store pays none

  const send = async (url, event, data) => {
    if (!url || S.api?.isLive()) return;
    const body = JSON.stringify({ event, source: 'demo-site', site: link('/'), sentAt: new Date().toISOString(), ...data });
    try {
      await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body });
    } catch {
      try { await fetch(url, { method: 'POST', mode: 'no-cors', headers: { 'Content-Type': 'text/plain' }, body }); } catch { /* the automation is down: the site carries on */ }
    }
  };

  /** The signed-in customer on this device */
  const customer = () => {
    const u = SA.core.store.get().user || {};
    return { id: u.id || null, name: u.name || [u.firstName, u.lastName].filter(Boolean).join(' ') || 'Customer', email: u.email || null, phone: u.phone || null, city: u.city || null };
  };
  const account = (id) => {
    const a = db().find('accounts', id);
    return a ? { id: a.id, name: a.name || null, company: a.company || null, email: a.email || null, phone: a.phone || null } : null;
  };
  /** A pitch with its partner (the pitch owner) */
  const pitchOf = (id) => {
    const p = S.pitches.getSync(id) || {};
    return {
      pitch: { id: p.id, name: p.name, format: p.format, players: p.players, surface: p.surface, indoor: !!p.indoor, area: p.area, city: p.city, address: p.address,
        lat: p.lat, lng: p.lng, pricePerHour: p.price, peakPricePerHour: p.peakPrice, cancelPolicy: p.cancelPolicy || 'standard', link: link(`/pitches/${p.id}`) },
      partner: account(p.ownerId),
    };
  };
  const bookingOf = (b) => ({
    code: b.id, status: b.status, date: b.date, startTime: hh(b.hour), duration: b.duration, endTime: hh(b.hour + b.duration), players: b.players,
    listPrice: b.listPrice, discountPercent: b.discount || 0, discountAmount: b.discountAmount || 0, promoCode: b.promo?.code || null, promoAmount: b.promo?.amount || 0,
    pitchTotal: b.pitchTotal, serviceFee: b.serviceFee, total: b.total, currency: 'EGP', payment: b.payment, createdAt: iso(b.createdAt),
    packageId: b.seriesId || null, sessionNo: b.sessionNo || null, sessions: b.seriesSize || null,
  });

  /* ---------- Store orders: every store in the order, its items and its share ---------- */

  const orderDetails = (o) => {
    const byStore = new Map();
    o.lines.forEach((l) => {
      const key = l.pod ? 'printed' : l.storeId || 'official';
      if (!byStore.has(key)) byStore.set(key, []);
      byStore.get(key).push(l);
    });
    const stores = [...byStore.entries()].map(([key, lines]) => {
      const st = key === 'printed' ? null : db().find('stores', key);
      const official = key === 'printed' || !!st?.official;
      const itemsTotal = lines.reduce((t, l) => t + l.price * l.qty, 0);
      const commission = official ? 0 : Math.round(itemsTotal * STORE_COMMISSION * 100) / 100;
      return {
        storeId: key, name: key === 'printed' ? 'Printed for you by Super App' : st?.name || 'Super App Store', handle: st?.slug || null, official,
        tagline: st?.tagline || null, phone: st?.phone || null, owner: st ? account(st.ownerId) : null, commissionRate: official ? 0 : STORE_COMMISSION,
        items: lines.map((l) => ({ productId: l.productId, name: l.name, brand: l.brand || null, size: l.variant?.size || null, color: l.variant?.color || null, qty: l.qty,
          unitPrice: l.price, lineTotal: l.price * l.qty, link: link(`/shop/${l.productId}`) })),
        itemsCount: lines.reduce((t, l) => t + l.qty, 0), itemsTotal, commission, storeEarning: itemsTotal - commission,
      };
    });
    return {
      order: {
        code: o.id, status: o.status, placedAt: iso(o.createdAt), estimatedDelivery: o.eta, deliverySpeed: o.delivery, itemsCount: o.lines.reduce((t, l) => t + l.qty, 0),
        subtotal: o.subtotal, discount: o.couponOff || 0, promoCode: o.coupon || null, deliveryFee: o.shipping, total: o.total, currency: 'EGP',
        payment: o.payment, link: link(`/order/${o.id}`),
      },
      customer: customer(), address: o.address || null, stores,
      items: stores.flatMap((st) => st.items.map((i) => ({ ...i, store: st.name, storeId: st.storeId }))),
    };
  };

  const shop = S.shop;
  const placeOrder = shop.placeOrder;
  shop.placeOrder = async (args) => {
    const o = await placeOrder(args);
    send(cfg().orders, 'order.created', orderDetails(o));
    return o;
  };

  /* ---------- Pitch bookings and weekly packages ---------- */

  const B = S.bookings;
  const create = B.create;
  B.create = async (args) => {
    const b = await create(args);
    send(cfg().bookings, 'booking.created', { booking: bookingOf(b), ...pitchOf(b.pitchId), customer: customer() });
    return b;
  };
  const createSeries = B.createSeries;
  B.createSeries = async (args) => {
    const out = await createSeries(args);
    const pack = db().find('series', out.seriesId) || {};
    const first = out.bookings[0] || {};
    send(cfg().bookings, 'package.created', {
      package: {
        id: out.seriesId, kind: 'weekly', weeks: pack.weeks || +args.weeks, sessionsBooked: out.bookings.length, weekday: SA.core.utils.DAY?.[pack.weekday] || pack.weekday,
        startTime: hh(first.hour), duration: first.duration, startDate: pack.startDate || first.date, discountPercent: pack.discount || 0, skippedWeeks: pack.skipped || [],
        total: out.bookings.reduce((t, b) => t + (b.total || 0), 0), currency: 'EGP', payment: first.payment, createdAt: iso(first.createdAt),
      },
      sessions: out.bookings.map(bookingOf), ...pitchOf(first.pitchId), customer: customer(),
    });
    return out;
  };

  /* ---------- Find players: a host publishes a game; a player asks to join one ---------- */

  const M = S.matches;
  const publish = M.publish;
  M.publish = async (data) => {
    const out = await publish(data);
    const m = SA.core.store.get().publishedMatches.find((x) => x.id === out.id) || {};
    const b = S.bookings.getSync(m.bookingId) || {};
    send(cfg().bookings, 'game.published', {
      game: { id: m.id, status: m.closed ? 'closed' : 'open', date: m.date, startTime: hh(m.hour), duration: m.duration, endTime: hh(m.hour + m.duration), playersNeeded: m.total, playersAlready: m.filled, spotsLeft: m.total - m.filled,
        level: m.level, gender: m.gender, type: m.type, pricePerPlayer: m.pricePerPlayer, needsGoalkeeper: !!m.needsGK, notes: m.notes, autoAccept: m.autoAccept,
        postedAt: iso(m.postedAt), link: link(`/players/${m.id}`),
        totalCost: b.total || null, currency: 'EGP', players: (m.participants || []).map((x) => ({ name: x.name, role: x.role })) },
      booking: b.id ? bookingOf(b) : null, ...pitchOf(m.pitchId), host: customer(),
    });
    return out;
  };
  const G = S.games;
  if (G) {
    const ask = G.request;
    G.request = async (matchId, args) => {
      const r = await ask(matchId, args);
      const m = M.allSync?.().find((x) => x.id === matchId) || {};
      send(cfg().bookings, 'game.join_requested', {
        request: { id: r.id, status: r.status, position: r.position, message: r.message || '', amountHeld: r.amount, paid: !!r.paid, at: iso(r.at) },
        game: { id: matchId, host: r.hostName, date: r.date, startTime: hh(r.hour), duration: m.duration || null, pricePerPlayer: m.pricePerPlayer || r.amount, spotsLeft: m.spotsLeft ?? null,
          level: m.level || null, link: link(`/players/${matchId}`) },
        ...(m.pitch?.id || m.pitchId ? pitchOf(m.pitch?.id || m.pitchId) : { pitch: { name: r.pitchName }, partner: null }), player: customer(),
      });
      return r;
    };
  }

  /* ---------- Fantasy: a new league, someone joins a league, a gameweek is finished (→ the orders link) ---------- */

  const F = S.fantasy;
  if (F) {
    const fantasyUrl = () => cfg().fantasy || cfg().orders;
    /** Demo rivals have only a name: a stable fictional id and an example.com address (every person in demo data is fictional) */
    const slug = (name) => String(name || 'player').toLowerCase().normalize('NFKD').replace(/[^a-z0-9]+/g, '.').replace(/^\.|\.$/g, '');
    const personOf = (row) => {
      if (row.isMe) { const c = customer(); return { userId: c.id, name: c.name, email: c.email }; }
      return { userId: `demo-${slug(row.name)}`, name: row.name, email: `${slug(row.name)}@example.com` };
    };
    const competition = () => { const i = F.leagueInfo(); return { key: i.key, name: i.label }; };
    const leagueOf = (g) => ({
      leagueId: g.id, leagueName: g.name, type: g.members ? 'private' : g.kind === 'pass' ? 'prize' : 'official', code: g.code || null,
      owner: g.owner || null, startGameweek: g.startGW || g.from || 1, membersCount: g.members ? g.members.length : g.participants || null,
      competition: competition(), link: link(`/fantasy/league/${g.id}`),
    });
    /** The leagues this player is in: private groups, the season league (with a team) and the prize leagues entered */
    const myLeagues = () => {
      const joined = F.officialLeagues().filter((l) => l.joined);
      return [...F.privateGroups(), ...joined];
    };

    const createGroup = F.createGroup;
    F.createGroup = async (name) => {
      const g = await createGroup(name);
      const me = customer();
      send(fantasyUrl(), 'league.created', { league: leagueOf(g), createdBy: { userId: me.id, name: me.name, email: me.email }, createdAt: new Date().toISOString() });
      return g;
    };

    const memberJoined = (g) => {
      const me = customer();
      const t = F.totals();
      const gw = F.meta().gw;
      send(fantasyUrl(), 'league.member.joined', {
        league: leagueOf(g),
        member: { userId: me.id, name: me.name, email: me.email, leagueId: g.id, leagueName: g.name, gameweekId: gw, gameweekPoints: t.gwPts || 0, totalPoints: t.total || 0,
          teamName: F.team()?.name || null },
        joinedAt: new Date().toISOString(),
      });
    };
    const joinGroupByCode = F.joinGroupByCode;
    F.joinGroupByCode = async (code) => {
      const g = await joinGroupByCode(code);
      memberJoined(F.group(g.id) || g);
      return g;
    };
    const joinOfficial = F.joinOfficial;
    F.joinOfficial = async (id) => {
      const out = await joinOfficial(id);
      const g = F.officialLeagues().find((l) => l.id === id);
      if (g) memberJoined(g);
      return out;
    };

    /** One event per league the player is in: every member with rank and points, and the gameweek's top three */
    const gameweekFinished = (gw) => myLeagues().forEach((l) => {
      const g = F.group(l.id);
      if (!g) return;
      const rows = g.standings || [];
      const byGw = rows.slice().sort((a, b) => b.gw - a.gw);
      const gwRank = new Map(byGw.map((r, i) => [r, i + 1]));
      const lg = { ...leagueOf(g), membersCount: g.members ? rows.length : g.participants || rows.length };
      const participants = rows.map((r) => ({
        ...personOf(r), leagueId: lg.leagueId, leagueName: lg.leagueName, gameweekId: gw,
        rank: r.rank, points: r.total, totalPoints: r.total, gameweekRank: gwRank.get(r), gameweekPoints: r.gw, teamName: r.team || null,
      }));
      const winners = byGw.slice(0, 3).map((r, i) => ({ place: i + 1, ...personOf(r), gameweekPoints: r.gw, totalPoints: r.total, teamName: r.team || null }));
      send(fantasyUrl(), 'gameweek.finished', {
        gameweek: gw, gameweekId: gw, competition: competition(), league: lg, finishedAt: new Date().toISOString(),
        participantsCount: participants.length, partialTable: !g.members, winners, participants,
      });
    });
    const simulateGameweek = F.simulateGameweek;
    F.simulateGameweek = async (...args) => {
      const out = await simulateGameweek(...args);
      gameweekFinished(out.gw);
      return out;
    };
  }

  S.automation = { send };
})(window.SA);
