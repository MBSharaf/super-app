/** Find players / open matches. Future API: GET /matches, POST /matches, POST /matches/:id/join */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { isoDate, addDays, today, haversineKm, parseISO, seeded } = SA.core.utils;

  const people = SA.data.matches.people;
  const me = () => store.get().user;

  /** Turn a seed or user-published record into the public match shape. */
  const hydrate = (m) => {
    const p = SA.services.pitches.getSync(m.pitchId);
    const date = m.date || isoDate(addDays(today(), m.dayOffset));
    const joined = store.get().joinedMatchIds.includes(m.id);
    // Seeded participants (stable per match), then the user if they joined
    const participants = m.participants || Array.from({ length: m.filled }, (_, i) => {
      const idx = i === 0 ? m.host : Math.floor(seeded(m.id + i) * people.length);
      return { name: people[idx], avatar: idx % 15, role: i === 0 ? 'Host' : (m.needsGK && i === 1 ? 'Defender' : ['Midfielder', 'Forward', 'Defender', 'Winger'][i % 4]), photo: (idx % 3) !== 1 };
    });
    if (joined && !participants.some((x) => x.isMe)) participants.push({ name: me().name, avatar: me().avatar, role: me().position, photo: true, isMe: true });
    const filled = participants.length;
    const start = parseISO(date); start.setHours(0, 0, 0, 0);
    const startsAt = start.getTime() + m.hour * 36e5;
    const loc = store.get().location || SA.config.fallbackLocation;
    return {
      ...m, date, pitch: { id: p.id, name: p.name, area: p.area, city: p.city, image: p.images[0], lat: p.lat, lng: p.lng, format: p.format, surface: p.surface, address: p.address },
      hostName: m.hostName || participants[0].name, participants, filled, spotsLeft: Math.max(0, m.total - filled), joined,
      isMine: !!m.isMine, startsAt, distanceKm: haversineKm(loc, p),
    };
  };

  // Matches at a pitch that was removed from the platform are no longer listed
  const exists = (m) => !!SA.services.pitches.getSync(m.pitchId) && !SA.services.content?.matchHidden(m.id);
  const all = () => [...store.get().publishedMatches, ...SA.data.matches.list].filter(exists).map(hydrate).filter((m) => m.startsAt + m.duration * 36e5 > Date.now() - 36e5);

  const matchService = {
    list: (f = {}) => request(() => {
      let out = all();
      if (f.level) out = out.filter((m) => m.level === f.level);
      if (f.gender) out = out.filter((m) => m.gender === f.gender);
      if (f.when === 'today') out = out.filter((m) => m.date === isoDate(today()));
      if (f.when === 'tomorrow') out = out.filter((m) => m.date === isoDate(addDays(today(), 1)));
      if (f.when === 'week') out = out.filter((m) => parseISO(m.date) <= addDays(today(), 7));
      if (f.gk) out = out.filter((m) => m.needsGK);
      if (f.q) { const q = f.q.toLowerCase(); out = out.filter((m) => `${m.pitch.name} ${m.pitch.area} ${m.hostName}`.toLowerCase().includes(q)); }
      if (f.openOnly !== false) out = out.filter((m) => m.spotsLeft > 0 || m.joined || m.isMine);
      const sort = { soon: (a, b) => a.startsAt - b.startsAt, distance: (a, b) => a.distanceKm - b.distanceKm, spots: (a, b) => a.spotsLeft - b.spotsLeft };
      return out.sort(sort[f.sort] || sort.soon);
    }),
    get: (id) => request(() => {
      const m = all().find((x) => x.id === id) || [...store.get().publishedMatches, ...SA.data.matches.list].filter(exists).map(hydrate).find((x) => x.id === id);
      if (!m) throw new ApiError('This match is no longer listed. It may have filled up or been cancelled.', 'not_found');
      return m;
    }),
    allSync: all,

    /** Join a match: pays the per-player share, then adds the user to the participant list. */
    async join(id, payment, { position = 'Any position', message = '' } = {}) {
      if (SA.services.games) return SA.services.games.request(id, { position, message, payment });
      SA.services.user.assertActive();
      const m = all().find((x) => x.id === id);
      if (!m) throw new ApiError('This match is no longer listed.', 'not_found');
      if (m.joined) throw new ApiError('You’re already in this match.', 'already_joined');
      if (m.spotsLeft <= 0) throw new ApiError('The last spot was just taken. Try another match.', 'full');
      let paid = null;
      if (payment.method !== 'cash') paid = await SA.services.payment.charge({ ...payment, amount: m.pricePerPlayer, label: `Joined match at ${m.pitch.name}` });
      return request(() => {
        store.set((s) => ({ joinedMatchIds: [...s.joinedMatchIds, id], matchRequests: { ...s.matchRequests, [id]: { status: 'joined', at: Date.now(), payment: paid ? paid.label : 'Pay at the pitch' } } }), 'match:join');
        SA.services.user.notify({ kind: 'match', title: 'You’re in!', body: `${m.pitch.name}, ${SA.core.utils.relDay(m.date)} at ${SA.core.utils.fmtHour(m.hour)}. The host has been notified.`, link: `/players/${id}` });
        return { ok: true };
      });
    },
    async leave(id) {
      const req = store.get().matchRequests[id];
      const m = all().find((x) => x.id === id);
      store.set((s) => { const r = { ...s.matchRequests }; delete r[id]; return { joinedMatchIds: s.joinedMatchIds.filter((x) => x !== id), matchRequests: r }; });
      if (req && req.payment !== 'Pay at the pitch' && m) await SA.services.payment.refund(m.pricePerPlayer, `Refund — left match at ${m.pitch.name}`);
      return { ok: true };
    },

    /** Publish a booking as "Looking for players". */
    publish: (data) => request(() => {
      SA.services.user.assertActive();
      const b = SA.services.bookings.getSync(data.bookingId);
      if (!b) throw new ApiError('Pick one of your upcoming bookings first.', 'no_booking');
      const total = +data.total, have = +data.have;
      if (!(have >= 1 && have < total)) throw new ApiError('Players you already have must be fewer than the total.', 'invalid_counts');
      const id = 'm-' + Math.floor(2000 + Math.random() * 7000);
      const user = me();
      const participants = [{ name: user.name, avatar: user.avatar, role: 'Host', photo: true, isMe: true },
        ...Array.from({ length: have - 1 }, (_, i) => ({ name: data.friends?.[i] || `Friend ${i + 1}`, avatar: (i + 4) % 15, role: 'Player', photo: false }))];
      const match = {
        id, pitchId: b.pitchId, date: b.date, hour: b.hour, duration: b.duration, total, filled: have, participants,
        level: data.level, gender: data.gender, type: data.type, pricePerPlayer: Math.ceil(b.total / total), notes: data.notes?.trim() || '',
        needsGK: data.needsGK === 'on' || data.needsGK === true, isMine: true, hostName: user.name, bookingId: b.id, postedAt: Date.now(), postedHoursAgo: 0,
        autoAccept: !!data.autoAccept, closed: false,
      };
      store.set((s) => ({ publishedMatches: [match, ...s.publishedMatches], bookings: s.bookings.map((x) => (x.id === b.id ? { ...x, publishedMatchId: id } : x)) }), 'match:publish');
      SA.services.user.notify({ kind: 'match', title: 'Your match is live', body: `Players nearby can now join your game at ${b.pitchName}.`, link: `/players/${id}` });
      return { id };
    }),
    unpublish: (id) => request(() => {
      store.set((s) => ({ publishedMatches: s.publishedMatches.filter((m) => m.id !== id), bookings: s.bookings.map((b) => (b.publishedMatchId === id ? { ...b, publishedMatchId: null } : b)) }));
      return { ok: true };
    }),
    mine: () => request(() => {
      const list = all();
      return { joined: list.filter((m) => m.joined), hosting: list.filter((m) => m.isMine) };
    }),
  };

  SA.services.matches = matchService;
})(window.SA);
