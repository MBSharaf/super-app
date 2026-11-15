/**
 * Waitlist for booked hours. A player joins on a booked/blocked slot; whenever bookings,
 * closures or pitches change (any cancellation path, here or in another tab), waiting entries are
 * re-checked and the player is notified the moment the hour is free again.
 * Future API: POST /waitlist, DELETE /waitlist/:id — the check runs server-side on cancellation.
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const { isoDate, today, fmtDate, fmtHour, debounce } = SA.core.utils;
  const db = SA.services.db;
  const PS = () => SA.services.pitches;
  const me = () => SA.core.store.get().user || {};
  const startOf = (e) => SA.core.utils.parseISO(e.date).getTime() + e.hour * 36e5;

  const waitlist = {
    mine: () => db.all('waitlist').filter((e) => e.userId === (me().id || 'u-001') && ['waiting', 'notified'].includes(e.status) && startOf(e) > Date.now()).sort((a, b) => startOf(a) - startOf(b)),
    entryFor: (pitchId, date, hour) => db.all('waitlist').find((e) => e.pitchId === pitchId && e.date === date && e.hour === hour && e.userId === (me().id || 'u-001') && e.status === 'waiting'),
    countFor: (pitchId, date, hour) => db.all('waitlist').filter((e) => e.pitchId === pitchId && e.date === date && e.hour === hour && e.status === 'waiting').length,
    join({ pitchId, date, hour }) {
      const p = PS().getSync(pitchId);
      const s = PS().slotsSync(pitchId, date).find((x) => x.hour === hour);
      if (!p || !s) throw new ApiError('That time isn’t on this pitch’s schedule.', 'invalid');
      if (s.status === 'available') throw new ApiError('Good news — this time is free. Book it now.', 'available');
      if (!['booked', 'blocked'].includes(s.status)) throw new ApiError('You can only join the waitlist for a booked time.', 'invalid');
      if (waitlist.entryFor(pitchId, date, hour)) throw new ApiError('You’re already on the waitlist for this time.', 'duplicate');
      const u = me();
      const e = db.insert('waitlist', { pitchId, ownerId: p.ownerId, date, hour, userId: u.id || 'u-001', name: u.name || 'Player', phone: u.phone || '', status: 'waiting' }, 'wl');
      db.log('customer', 'waitlist.joined', `${u.name || 'A player'} joined the waitlist for ${p.name}, ${date} ${fmtHour(hour)}`, e.id);
      return e;
    },
    leave(id) { const e = db.find('waitlist', id); if (e) db.update('waitlist', id, { status: 'left' }); },
    /** Re-check every waiting entry; notify when the hour is free */
    check() {
      const t = Date.now();
      db.all('waitlist').forEach((e) => {
        if (e.status !== 'waiting' && e.status !== 'notified') return;
        if (startOf(e) <= t) { db.update('waitlist', e.id, { status: 'expired' }); return; }
        if (e.status !== 'waiting') return;
        const s = PS().slotsSync(e.pitchId, e.date).find((x) => x.hour === e.hour);
        if (s?.status === 'yours') { db.update('waitlist', e.id, { status: 'booked' }); return; }
        if (s?.status !== 'available') return;
        const p = PS().getSync(e.pitchId);
        db.update('waitlist', e.id, { status: 'notified', notifiedAt: t });
        if (e.userId === (me().id || 'u-001')) {
          SA.services.user.notify({ kind: 'booking', title: 'The time you wanted is free', body: `${p?.name}, ${fmtDate(e.date)} at ${fmtHour(e.hour)} just opened up. Book it before someone else does.`, link: `/pitches/${e.pitchId}?date=${e.date}&hour=${e.hour}` });
          SA.ui?.overlay?.toast(`${p?.name} at ${fmtHour(e.hour)} is free now`, { action: 'Book', onAction: () => SA.core.router.go(`/pitches/${e.pitchId}?date=${e.date}&hour=${e.hour}`) });
        }
      });
    },
  };

  // Any change that can free an hour triggers a re-check
  const run = debounce(() => { try { waitlist.check(); } catch (err) { console.warn('waitlist check', err); } }, 150);
  db.subscribe((col) => { if (['bookings', 'closures', 'pitches', 'series', 'tournaments', '*'].includes(col)) run(); });

  SA.services.waitlist = waitlist;
})(window.SA);
