/**
 * Becoming a pitch partner: a customer applies from their profile (#/partner/apply) with their details, ID and the
 * pitch; the Super App team approves or rejects it (Admin → Approvals → Pitch partners). On approval the pitch partner
 * account and the pitch are created, and the customer opens the pitch partner dashboard from their profile.
 * Future API: POST /partner-applications, GET /me/partner-application, /admin/partner-applications/*
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const N = () => SA.services.notify;
  const me = () => SA.core.store.get().user || SA.data.user.profile;
  const FORMATS = ['5-a-side', '7-a-side', '11-a-side'];
  const PAYOUTS = ['Bank transfer', 'InstaPay'];

  const ensure = () => { const d = db().raw(); if (!Array.isArray(d.partnerApps)) { d.partnerApps = []; db().touch('partnerApps'); } };

  const partners = {
    FORMATS, PAYOUTS,
    /** This customer's latest application (pending, approved or rejected) */
    mine: () => { ensure(); return db().all('partnerApps').filter((a) => a.userId === me().id).sort((a, b) => b.createdAt - a.createdAt)[0] || null; },
    list: (status) => { ensure(); return db().all('partnerApps').filter((a) => !status || a.status === status).sort((a, b) => b.createdAt - a.createdAt); },
    apply(d) {
      ensure();
      SA.services.ceo?.assertOn('applications');
      const prev = partners.mine();
      if (prev?.status === 'pending') throw new ApiError('Your application is already being reviewed.', 'exists');
      if (prev?.status === 'approved') throw new ApiError('You are already a pitch partner.', 'exists');
      const e = {};
      const t = (k) => String(d[k] || '').trim();
      if (t('ownerName').split(/\s+/).length < 2) e.ownerName = 'Your full name, as on your ID.';
      if (!/^\+?[\d\s]{10,16}$/.test(t('phone'))) e.phone = 'A mobile number, e.g. 010 1234 5678.';
      if (!/^\S+@\S+\.\S+$/.test(t('email'))) e.email = 'A valid email.';
      if (!/^\d{14}$/.test(t('nationalId'))) e.nationalId = 'The national ID number has 14 digits.';
      if (!d.idPhoto) e.idPhoto = 'Add a photo of your ID.';
      if (t('pitchName').length < 3) e.pitchName = 'The name customers will see.';
      if (!t('area')) e.area = 'Choose the area.';
      if (t('address').length < 6) e.address = 'Street and landmark.';
      if (!(+d.lat && +d.lng)) e.lat = 'Tap the map to pin the pitch.';
      const count = Math.round(+d.count);
      if (!(count >= 1 && count <= 20)) e.count = 'From 1 to 20.';
      const formats = [].concat(d.formats || []).filter((f) => FORMATS.includes(f));
      if (!formats.length) e.formats = 'Choose at least one.';
      if (!(d.photos || []).length) e.photos = 'Add at least one photo of the pitch.';
      const price = Math.round(+d.price);
      if (!(price >= 100 && price <= 5000)) e.price = 'Between 100 and 5,000 EGP an hour.';
      if (!PAYOUTS.includes(d.payoutMethod)) e.payoutMethod = 'Choose how we pay you.';
      if (t('payoutDetail').length < 4) e.payoutDetail = d.payoutMethod === 'InstaPay' ? 'Your InstaPay address or number.' : 'Your bank account or IBAN.';
      if (!d.agree) e.agree = 'Please accept the partner terms.';
      if (Object.keys(e).length) throw new ApiError('Check the highlighted fields.', 'invalid', e);
      const u = me();
      const app = db().insert('partnerApps', {
        userId: u.id, status: 'pending', ownerName: t('ownerName'), phone: t('phone'), email: t('email'), idLast4: t('nationalId').slice(-4), idPhoto: d.idPhoto,
        pitchName: t('pitchName'), area: t('area'), city: t('city') || 'Cairo', address: t('address'), lat: +d.lat, lng: +d.lng, count, formats, photos: d.photos.slice(0, 4), price,
        payout: { method: d.payoutMethod, detail: t('payoutDetail') }, note: t('note').slice(0, 400),
      }, 'pa');
      SA.services.legal?.accept(['partner', 'terms', 'privacy'], { where: 'pitch partner application', person: { id: u.id, name: app.ownerName, kind: 'partner' } });
      db().log('customer', 'partner.applied', `${app.ownerName} applied to list “${app.pitchName}” (${app.area})`, app.id);
      N()?.toAdmins({ cat: 'store', tone: 'gold', title: `New pitch partner application: ${app.pitchName}`, body: `${app.ownerName} · ${app.area} · ${app.count} pitch${app.count > 1 ? 'es' : ''}`, link: '#/admin/approvals?tab=partners' });
      SA.services.user.notify({ kind: 'account', title: 'We got your application', body: `“${app.pitchName}” — the Super App team reviews it within 2 working days.`, link: '/partner/apply' });
      return app;
    },

    /* ---------- Admin ---------- */
    approve(id) {
      const ad = SA.services.admin.me();
      const app = db().find('partnerApps', id); if (!app || app.status !== 'pending') throw new ApiError('Application not found.', 'not_found');
      const now = Date.now();
      const accountId = `acc-p-${Math.random().toString(36).slice(2, 7)}`;
      db().insert('accounts', { id: accountId, role: 'owner', name: app.ownerName, company: app.pitchName, email: app.email, phone: app.phone, avatar: 0, joinedAt: now, payout: { ...app.payout }, userId: app.userId, kyc: { idLast4: app.idLast4, verifiedAt: now } });
      const format = app.formats[0];
      const pitch = db().insert('pitches', {
        id: `p-${app.pitchName.toLowerCase().replace(/[^a-z0-9]+/g, '-').slice(0, 24)}-${Math.random().toString(36).slice(2, 5)}`,
        name: app.pitchName, area: app.area, city: app.city, address: app.address, lat: app.lat, lng: app.lng, format, players: { '5-a-side': 10, '7-a-side': 14, '11-a-side': 22 }[format],
        surface: 'Artificial turf', indoor: false, price: app.price, peakPrice: Math.round(app.price * 1.25), open: [9, 25], images: app.photos, facilities: ['lights', 'water', 'wc'], about: app.note || '', tags: [],
        ownerId: accountId, status: 'active', closedReason: '', rating: 0, reviews: 0, seed: false, review: 'approved', reviewedAt: now, cancelPolicy: 'standard', weekly: { enabled: true, discount: 0.1 },
      });
      db().update('partnerApps', id, { status: 'approved', accountId, pitchId: pitch.id, decidedAt: now, decidedBy: ad.id });
      SA.services.admin.log('partner.approved', `Approved pitch partner ${app.ownerName} — “${app.pitchName}”`, id);
      N()?.toAccount(accountId, { cat: 'account', tone: 'turf', title: `Welcome to Super App, ${app.ownerName.split(' ')[0]}!`, body: `“${app.pitchName}” is live. Check the price, hours and photos, and add your other pitches.`, link: `#/dash/pitches/${pitch.id}` });
      if (app.userId === me().id) SA.services.user.notify({ kind: 'account', title: 'You’re a Super App pitch partner!', body: `“${app.pitchName}” is approved. Open your pitch partner dashboard from your profile.`, link: '/profile' });
      return { accountId, pitchId: pitch.id };
    },
    reject(id, reason) {
      const ad = SA.services.admin.me();
      const app = db().find('partnerApps', id); if (!app || app.status !== 'pending') throw new ApiError('Application not found.', 'not_found');
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason the applicant sees.', 'invalid');
      db().update('partnerApps', id, { status: 'rejected', reason: reason.trim(), decidedAt: Date.now(), decidedBy: ad.id });
      SA.services.admin.log('partner.rejected', `Didn’t approve “${app.pitchName}”: ${reason.trim()}`, id);
      if (app.userId === me().id) SA.services.user.notify({ kind: 'account', title: 'Your pitch application wasn’t approved', body: `${reason.trim()} You can fix it and apply again.`, link: '/partner/apply' });
    },
    /** Open the pitch partner dashboard for this customer's approved account */
    openDashboard() { const a = partners.mine(); if (a?.status === 'approved') SA.services.dash.auth.signIn(a.accountId); return a; },
  };

  SA.services.partners = partners;
})(window.SA);
