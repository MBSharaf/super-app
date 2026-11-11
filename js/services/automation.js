/**
 * The owner's automation (n8n) on the demo site (GitHub Pages, no server): every new store order, pitch booking, weekly
 * package, published game and request to join a game is sent with all its details to the links in
 * `SA.config.automation` (orders → `orders`; bookings, packages and games → `bookings`). Fantasy: a new private league
 * (`league.created`), someone joining a league (`league.member.joined`) and a finished gameweek (`gameweek.finished`, with
 * `superAppLeague.weeklyWinners`: the gameweek's top 3 in the Super App League and the Super Points each won; one
 * per league with every member's rank and points and the gameweek's top three) → `fantasy` (the orders link). In API mode the server sends
 * these events itself (its webhooks), so nothing is sent from the browser there.
 * Cancellation requests (`cancellation.requested`, then `.approved` / `.rejected`) → the chatbot's AI agent
 * (`SA.config.chatbot.url`, with the chat's sessionId; its reply shows in the chat), never the orders link (owner, 2026-11-11).
 * A dedicated `cancellations` link, if set, wins. The request number is the trigger of the AI agent's cancellation workflow.
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
    // Deposit rule: only the deposit is paid online, the rest at the pitch (QR check-in)
    depositPaid: b.deposit ?? null, payAtPitch: b.payAtPitch ?? null, perPlayer: b.perPlayer ?? null, cancelPolicy: b.policy || null,
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
      leagueId: g.id, leagueName: g.name, type: g.members ? 'private' : g.kind === 'official' ? 'superApp' : 'free', isFree: true, code: g.code || null,
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

    /** A league's table after the gameweek: every member with rank and points, and the gameweek's top three */
    const leagueTable = (g, gw) => {
      const rows = g.standings || [];
      const byGw = rows.slice().sort((a, b) => b.gw - a.gw);
      const gwRank = new Map(byGw.map((r, i) => [r, i + 1]));
      const lg = { ...leagueOf(g), membersCount: g.members ? rows.length : g.participants || rows.length };
      const participants = rows.map((r) => ({
        ...personOf(r), leagueId: lg.leagueId, leagueName: lg.leagueName, gameweekId: gw,
        rank: r.rank, points: r.total, totalPoints: r.total, gameweekRank: gwRank.get(r), gameweekPoints: r.gw, teamName: r.team || null,
      }));
      const winners = byGw.slice(0, 3).map((r, i) => ({ place: i + 1, ...personOf(r), gameweekPoints: r.gw, totalPoints: r.total, teamName: r.team || null }));
      return { league: lg, participantsCount: participants.length, partialTable: !g.members, winners, participants };
    };
    /**
     * ONE event for the whole gameweek with every league the player is in (`leagues[]`). An n8n test link takes a single
     * request per "Listen" click, so one event per league lost all but the first. `league` / `winners` / `participants`
     * at the top level are the first league's, as before.
     */
    /** The Super App League's weekly prize: the gameweek's top 3 and the Super Points each one won (1,500 / 1,000 / 500 by default) */
    const superAppLeagueOf = (weekly, gw) => {
      const sl = F.superLeague?.();
      if (!sl) return null;
      const amounts = F.weeklyPoints();
      const rows = weekly?.winners || [];
      return {
        leagueId: sl.id, leagueName: sl.name, type: 'superApp', isFree: true, managedBy: 'admin', gameweekId: gw, competition: competition(),
        prizes: amounts.map((points, i) => ({ place: i + 1, superPoints: points })),
        weeklyWinners: rows.map((w) => ({ place: w.place, ...personOf(w), teamName: w.team || null, gameweekPoints: w.gw, totalPoints: w.total, superPointsWon: w.points })),
        link: link(`/fantasy/league/${sl.id}`),
      };
    };
    const gameweekFinished = (gw, weekly) => {
      const leagues = myLeagues().map((l) => F.group(l.id)).filter(Boolean).map((g) => leagueTable(g, gw));
      const me = customer(), t = F.totals();
      const sal = superAppLeagueOf(weekly, gw);
      const mine = sal?.weeklyWinners.find((w) => w.userId === me.id) || null;
      send(fantasyUrl(), 'gameweek.finished', {
        gameweek: gw, gameweekId: gw, competition: competition(), finishedAt: new Date().toISOString(), allLeaguesFree: true,
        player: { userId: me.id, name: me.name, email: me.email, teamName: F.team()?.name || null, gameweekPoints: F.lastResult()?.pts ?? null, totalPoints: t.total || 0,
          superPointsWon: mine?.superPointsWon || 0, superAppLeaguePlace: mine?.place || null },
        superAppLeague: sal,
        leaguesCount: leagues.length, ...(leagues[0] || {}), leagues,
      });
    };

    /** A new fantasy team (the player is entered into the Super App League with it) */
    const saveSquad = F.saveSquad;
    F.saveSquad = async (ids, opts) => {
      const had = !!F.team();
      const out = await saveSquad(ids, opts);
      const team = F.team();
      if (!had && team) {
        const me = customer();
        send(fantasyUrl(), 'team.created', {
          team: { teamId: team.id, teamName: team.name, captain: F.byId?.[team.captain]?.name || null, startGameweek: team.createdGW || F.meta().gw, players: (team.squad || []).length },
          player: { userId: me.id, name: me.name, email: me.email },
          competition: competition(), leagues: myLeagues().map((l) => leagueOf(l)), createdAt: new Date().toISOString(),
        });
      }
      return out;
    };
    const simulateGameweek = F.simulateGameweek;
    F.simulateGameweek = async (...args) => {
      const out = await simulateGameweek(...args);
      gameweekFinished(out.gw, out.weekly);
      return out;
    };
  }

  /* ---------- Cancellation requests: the trigger of the AI agent's cancellation workflow ---------- */
  const CX = S.cancellations;
  if (CX) {
    const cxSend = (event, data) => {
      if (S.api?.isLive()) return;
      if (cfg().cancellations) { send(cfg().cancellations, event, data); return; }
      SA.ui.chatbot?.notifyAgent?.(event, data);
    };
    const cxOf = (r) => ({
      requestId: r.id, status: r.status, type: r.kind, typeLabel: r.kindLabel, referenceId: r.refId, item: r.label, link: link(r.link),
      paidOnline: r.paid || 0, refundIfApproved: r.refund || 0, refunded: r.refunded ?? null, currency: 'EGP', cancelPolicy: r.policy || null, games: r.games || null,
      reason: r.reason || null, createdAt: iso(r.createdAt), replyBy: iso(r.dueAt), replyWithinHours: CX.REPLY_HOURS, channel: r.channel,
      decision: r.decidedAt ? { status: r.status, note: r.note || null, by: r.decidedBy || null, at: iso(r.decidedAt) } : null,
      adminLink: link(`/admin/cancellations?id=${r.id}`),
    });
    const extra = (r) => (r.pitchId ? pitchOf(r.pitchId) : {});
    const request = CX.request;
    CX.request = (d) => {
      const r = request(d);
      cxSend('cancellation.requested', { cancellation: cxOf(r), customer: { ...customer(), ...r.customer }, ...extra(r) });
      return r;
    };
    const approve = CX.approve;
    CX.approve = async (...a) => { const r = await approve(...a); cxSend('cancellation.approved', { cancellation: cxOf(r), customer: r.customer, ...extra(r) }); return r; };
    const reject = CX.reject;
    CX.reject = (...a) => { const r = reject(...a); cxSend('cancellation.rejected', { cancellation: cxOf(r), customer: r.customer, ...extra(r) }); return r; };
  }

  /* ---------- Pitch partner dashboard → `dashboard` link (owner, 2026-11-11) ---------- */
  // pitch.added / pitch.removed and review.created: n8n sends them to the pitch owner on Telegram (`owner` has the
  // contact details and `telegramChatId` when the account has one). offer.created: n8n emails the players in `audience`.
  // slot.posted (free hours): n8n tells the players who follow that area or pitch (`audience`, plus area / city / format).
  const D = S.dash;
  if (D) {
    const dashUrl = () => cfg().dashboard;
    const ownerOf = (id) => {
      const a = db().find('accounts', id);
      return a ? { id: a.id, name: a.name || null, company: a.company || null, email: a.email || null, phone: a.phone || null, telegram: a.telegram || null, telegramChatId: a.telegramChatId || null } : null;
    };
    const pitchInfo = (p) => ({ id: p.id, name: p.name, format: p.format, players: p.players, surface: p.surface, indoor: !!p.indoor, area: p.area, city: p.city, address: p.address,
      pricePerHour: p.price, peakPricePerHour: p.peakPrice ?? null, openFrom: hh(p.open?.[0] ?? 0), openTo: hh(p.open?.[1] ?? 0), status: p.status, approval: p.review || null, link: link(`/pitches/${p.id}`) });
    /** The players this demo knows (only this device's player is real; the server would list every player) */
    const players = () => { const c = customer(); return c.email ? [{ id: c.id, name: c.name, email: c.email, phone: c.phone, city: c.city }] : []; };
    const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

    const createPitch = D.createPitch;
    D.createPitch = (...a) => { const p = createPitch(...a); send(dashUrl(), 'pitch.added', { pitch: pitchInfo(p), owner: ownerOf(p.ownerId), at: iso(Date.now()) }); return p; };
    const deletePitch = D.deletePitch;
    D.deletePitch = async (id, ...a) => {
      const p = db().find('pitches', id);
      const r = await deletePitch(id, ...a);
      if (p) send(dashUrl(), 'pitch.removed', { pitch: pitchInfo({ ...p, status: 'removed' }), owner: ownerOf(p.ownerId), cancelledBookings: r?.cancelled || 0, at: iso(Date.now()) });
      return r;
    };

    // Offers from "Forecast & offers" (the free-hours page sends its own slot.posted with the discount)
    const createPromo = D.createPromo;
    D.createPromo = (d, ...a) => {
      const x = createPromo(d, ...a);
      const p = db().find('pitches', x.pitchId);
      if (p && x.label !== 'Last-minute slot') {
        send(dashUrl(), 'offer.created', {
          offer: { id: x.id, title: x.label, discountPercent: Math.round(x.pct * 100), days: x.days.map((w) => DAYS[w]), hourFrom: hh(x.hourFrom), hourTo: hh(x.hourTo), from: x.from, to: x.to,
            priceBefore: p.price, priceAfter: Math.round(p.price * (1 - x.pct)), currency: 'EGP', link: link(`/pitches/${p.id}`) },
          pitch: pitchInfo(p), owner: ownerOf(p.ownerId), audience: { channel: 'email', who: 'all players', recipients: players() },
        });
      }
      return x;
    };

    const G = S.games;
    if (G?.postSlot) {
      const postSlot = G.postSlot;
      G.postSlot = (d, ...a) => {
        const post = postSlot(d, ...a);
        const p = db().find('pitches', d.pitchId);
        if (p) {
          const followers = (G.alerts?.() || []).filter((al) => (al.pitchId ? al.pitchId === p.id : al.area === p.area || al.area === p.city) && d.hour >= al.from && d.hour < (al.to >= 24 ? 99 : al.to));
          const price = S.pitches.basePrice ? S.pitches.basePrice(d.date, d.hour, p) : p.price;
          send(dashUrl(), 'slot.posted', {
            slot: { id: post.id, date: d.date, startTime: hh(d.hour), endTime: hh(d.hour + 1), duration: 1, discountPercent: Math.round((d.pct || 0) * 100), price, priceAfter: Math.round(price * (1 - (d.pct || 0))),
              currency: 'EGP', note: post.text || null, bookLink: link(`/book/${p.id}?date=${d.date}&hour=${d.hour}&dur=1`) },
            pitch: pitchInfo(p), owner: ownerOf(p.ownerId),
            audience: { channel: 'notification', who: 'players following this area or pitch', area: p.area, city: p.city, format: p.format,
              recipients: followers.length ? players() : [], alerts: followers.map((al) => ({ id: al.id, pitchId: al.pitchId || null, area: al.area || null, from: hh(al.from), to: hh(al.to) })) },
          });
        }
        return post;
      };
    }

    // Reviews players leave on a pitch → its owner (good or bad)
    const B = S.bookings;
    if (B?.review) {
      const review = B.review;
      B.review = async (id, d, ...a) => {
        const rv = await review(id, d, ...a);
        const p = rv && db().find('pitches', rv.pitchId);
        if (p) {
          send(dashUrl(), 'review.created', {
            review: { id: rv.id, stars: rv.rating, sentiment: rv.rating >= 4 ? 'good' : rv.rating <= 2 ? 'bad' : 'neutral', text: rv.text, bookingCode: rv.bookingId, by: rv.customer?.name || null,
              at: iso(rv.createdAt || Date.now()), replyLink: link(`/dash/reviews?id=${rv.id}`) },
            pitch: { ...pitchInfo(p), rating: p.rating, reviews: p.reviews }, owner: ownerOf(p.ownerId),
          });
        }
        return rv;
      };
    }
  }

  S.automation = { send };
})(window.SA);
