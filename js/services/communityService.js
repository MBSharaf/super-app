/**
 * Communities (version 1): forum-style groups created by the Super App team — posts (text, photo, poll), comments, reactions,
 * pinned posts and announcements. No live chat yet (that comes with the backend).
 *
 * Who can join depends on the community's audience: everyone, sellers, or Pro/Elite sellers. Badges are real: seller level,
 * store name, Super App team. Safety: reports, a banned-word filter (plus phone numbers and outside links), slow mode and
 * muted members — all managed in the admin console (#/admin/community).
 *
 * Data: db.communities, db.communityPosts (comments inside), settings.community (filter); the shopper's memberships in the
 * device store (state.communities). Future API: /communities, /communities/:id/posts, /posts/:id/comments, /admin/community/*
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const store = SA.core.store;
  const DAY = 864e5, H = 36e5;

  const AUDIENCE = { everyone: 'Everyone', sellers: 'Sellers', 'sellers-pro': 'Pro & Elite sellers' };
  const REACTIONS = ['👍', '🔥', '😂', '❤️'];
  const me = () => store.get().user || SA.data.user.profile;
  const cfg = () => db().settings().community || { bannedWords: [], blockContacts: true };
  const seller = () => { try { return SA.services.sellers?.mine() || null; } catch { return null; } };

  /** Badge shown next to the shopper's name */
  const myBadge = () => {
    const s = seller();
    if (s && s.status === 'active') { const l = SA.services.sellers.levelOf(s); return { label: `${l.name} seller · ${s.name}`, tone: l.id }; }
    return null;
  };
  const author = () => ({ kind: 'user', id: me().id, name: me().name, avatar: me().avatar ?? 0, badge: myBadge() });

  /** Can the shopper on this device read and post in this community? */
  const access = (c) => {
    if (c.audience === 'everyone') return { ok: true };
    const s = seller();
    if (!s) return { ok: false, reason: 'For sellers on Super App', action: ['#/sell', 'Start selling'] };
    if (s.status !== 'active') return { ok: false, reason: 'Your seller account isn’t active yet' };
    if (c.audience === 'sellers-pro' && SA.services.sellers.levelOf(s).index < 2) return { ok: false, reason: 'For Pro and Elite sellers', action: ['#/dash/seller', 'See your level'] };
    return { ok: true };
  };
  const joined = () => store.get().communities || [];
  const muted = (communityId, userId = me().id) => {
    const m = (db().settings().community?.muted || []).find((x) => x.userId === userId && (!x.communityId || x.communityId === communityId) && x.until > Date.now());
    return m || null;
  };

  /** Reject banned words, phone numbers and outside links (keeps deals on Super App and stops spam) */
  const clean = (text) => {
    const t = String(text || '');
    const low = t.toLowerCase();
    const bad = (cfg().bannedWords || []).find((w) => w && low.includes(w.toLowerCase()));
    if (bad) throw new ApiError('Your post has words that aren’t allowed in Super App communities. Please edit it.', 'filtered');
    if (cfg().blockContacts !== false) {
      if (/(\+?20|0)1[0125][\s-]?\d{3,4}[\s-]?\d{3,4}/.test(t.replace(/[٠-٩]/g, (d) => '٠١٢٣٤٥٦٧٨٩'.indexOf(d)))) throw new ApiError('Phone numbers can’t be shared in communities — keep deals and messages on Super App.', 'filtered');
      const outside = (t.match(/(?:https?:\/\/|www\.)[^\s]+/gi) || []).filter((u) => !community.isInternal(u));
      if (outside.length) throw new ApiError('Outside links can’t be shared in communities — links to Super App pages are fine.', 'filtered');
    }
    return t.trim();
  };

  const view = (c) => {
    const posts = db().all('communityPosts').filter((p) => p.communityId === c.id && !p.hidden && !p.deleted);
    const isIn = joined().includes(c.id);
    return { ...c, posts: posts.length, members: (c.baseMembers || 0) + (isIn ? 1 : 0), joined: isIn, last: posts.reduce((m, p) => Math.max(m, p.at, ...(p.comments || []).map((x) => x.at)), c.createdAt || 0), access: access(c), audienceLabel: AUDIENCE[c.audience] };
  };
  const postView = (p) => {
    const uid = me().id;
    const comments = (p.comments || []).filter((x) => !x.hidden && !x.deleted);
    return { ...p, mine: p.author?.kind === 'user' && p.author.id === uid, comments, myReaction: Object.keys(p.reactions || {}).find((k) => (p.reactions[k] || []).includes(uid)) || null,
      reactionCount: Object.values(p.reactions || {}).reduce((t, l) => t + l.length + 0, 0) + (p.seedReactions || 0),
      myVote: p.poll ? p.poll.options.findIndex((o) => (o.votes || []).includes(uid)) : -1,
      pollTotal: p.poll ? p.poll.options.reduce((t, o) => t + (o.votes || []).length + (o.seed || 0), 0) : 0 };
  };
  const findPost = (id) => { const p = db().find('communityPosts', id); if (!p || p.deleted) throw new ApiError('This post is no longer available.', 'not_found'); return p; };
  const mustAccess = (c) => { const a = access(c); if (!a.ok) throw new ApiError(a.reason, 'forbidden'); };

  /** The Super App team writes as "Super App Admin" */
  const ADMIN = { kind: 'admin', id: 'acc-admin', name: 'Super App Admin', avatar: 7, badge: { label: 'Official', tone: 'team' } };

  /** Links to Super App pages (shown as cards); anything else counts as an outside link */
  const ROUTES = [['slot', /^#\/book\/([\w-]+)\?(.*)$/], ['pitch', /^#\/pitches\/([\w-]+)/], ['game', /^#\/players\/(m-[\w-]+)/], ['product', /^#\/shop\/([\w:-]+)/], ['store', /^#\/s\/([\w-]+)/],
    ['tournament', /^#\/tournaments\/([\w-]+)/], ['post', /^#\/community\/([\w-]+)\/([\w-]+)/], ['community', /^#\/community\/([\w-]+)$/]];
  const hashOf = (u) => {
    const i = u.indexOf('#/'); if (i >= 0) return u.slice(i).replace(/[.,!?)]+$/, '');
    try { const x = new URL(u.startsWith('www.') ? `https://${u}` : u); return `#${(x.pathname.replace(/^\/super-app(?=\/|$)/, '') || '/') + x.search}`.replace(/[.,!?)]+$/, ''); } catch { return ''; }
  };
  const isInternal = (u) => { if (/^#\//.test(u)) return true; try { const x = new URL(u.startsWith('www.') ? `https://${u}` : u); return (x.host === location.host || /superapp/i.test(x.host) || /mbsharaf\.github\.io$/i.test(x.host)) && (x.hash.startsWith('#/') || x.pathname.replace(/^\/super-app(?=\/|$)/, '').length > 1); } catch { return false; } };
  /** Super App links found in a text → [{ type, id, hash }] (max 3) */
  const linksIn = (text) => {
    const found = String(text || '').match(/(?:https?:\/\/[^\s]+|www\.[^\s]+|#\/[^\s]+)/gi) || [];
    const out = [];
    found.filter(isInternal).map(hashOf).forEach((h) => { const r = ROUTES.find(([, re]) => re.test(h)); if (r && !out.some((x) => x.hash === h)) { const m = h.match(r[1]); out.push({ type: r[0], id: m[1], extra: m[2] || '', hash: h }); } });
    return out.slice(0, 3);
  };

  const community = {
    AUDIENCE, REACTIONS, ADMIN, access, myBadge, clean, postView, view, isInternal, linksIn, hashOf,
    list: () => db().all('communities').filter((c) => c.status !== 'archived').map(view).sort((a, b) => (b.joined - a.joined) || (a.order || 0) - (b.order || 0)),
    get(slug) { const c = db().all('communities').find((x) => x.slug === slug || x.id === slug); if (!c || c.status === 'archived') throw new ApiError('This community doesn’t exist.', 'not_found'); return view(c); },
    join(id) {
      const c = db().find('communities', id); mustAccess(c);
      if (!joined().includes(id)) store.set({ communities: [...joined(), id] }, 'community');
    },
    leave(id) { store.set({ communities: joined().filter((x) => x !== id) }, 'community'); },
    /** Posts of one community: pinned first, then newest or most active */
    feed(communityId, { sort = 'new' } = {}) {
      const list = db().all('communityPosts').filter((p) => p.communityId === communityId && !p.hidden && !p.deleted).map(postView);
      const score = (p) => p.reactionCount + p.comments.length * 2 + (p.pollTotal || 0) / 5 - (Date.now() - p.at) / (6 * H);
      return list.sort((a, b) => (b.pinned - a.pinned) || (sort === 'top' ? score(b) - score(a) : b.at - a.at));
    },
    /** Latest posts from every community the shopper joined */
    myFeed() {
      const ids = new Set(joined());
      return db().all('communityPosts').filter((p) => ids.has(p.communityId) && !p.hidden && !p.deleted).map(postView).sort((a, b) => b.at - a.at).slice(0, 30)
        .map((p) => ({ ...p, community: db().find('communities', p.communityId) }));
    },
    post(id) { const p = findPost(id); if (p.hidden) throw new ApiError('This post was removed by the Super App team.', 'not_found'); return postView(p); },

    createPost(communityId, { text, image = null, poll = [], card = null }) {
      const c = db().find('communities', communityId); if (!c) throw new ApiError('Community not found.', 'not_found');
      mustAccess(c);
      SA.services.user.assertActive();
      const m = muted(communityId); if (m) throw new ApiError(`You can’t post here until ${new Date(m.until).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })}.`, 'muted');
      const t = clean(text);
      const opts = (poll || []).map((o) => clean(o)).filter(Boolean);
      if (t.length < 3) throw new ApiError('Write something first (at least 3 characters).', 'invalid', { text: 'Too short' });
      if (t.length > 2000) throw new ApiError('Keep posts under 2,000 characters.', 'invalid', { text: 'Too long' });
      if (opts.length === 1 || opts.length > 6) throw new ApiError('A poll needs 2 to 6 options.', 'invalid', { poll: 'Add options' });
      if (c.slowMode) {
        const last = db().all('communityPosts').filter((p) => p.communityId === c.id && p.author?.id === me().id).reduce((x, p) => Math.max(x, p.at), 0);
        const wait = last + c.slowMode * 6e4 - Date.now();
        if (wait > 0) throw new ApiError(`Slow mode is on — you can post again in ${Math.ceil(wait / 6e4)} min.`, 'slow');
      }
      if (!joined().includes(c.id)) community.join(c.id);
      return db().insert('communityPosts', { communityId: c.id, author: author(), text: t, image, card, poll: opts.length ? { options: opts.map((o) => ({ text: o, votes: [] })) } : null, reactions: {}, comments: [], pinned: false, announcement: false, at: Date.now() }, 'cp');
    },
    comment(postId, text, replyTo = null) {
      const p = findPost(postId); const c = db().find('communities', p.communityId);
      mustAccess(c);
      SA.services.user.assertActive();
      if (p.locked) throw new ApiError('Comments are closed on this post.', 'locked');
      const m = muted(c.id); if (m) throw new ApiError(`You can’t comment here until ${new Date(m.until).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })}.`, 'muted');
      const t = clean(text);
      if (t.length < 1) throw new ApiError('Write a comment first.', 'invalid');
      if (t.length > 800) throw new ApiError('Keep comments under 800 characters.', 'invalid');
      const cm = { id: `cm-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`, author: author(), text: t, at: Date.now(), replyTo: replyTo || null };
      db().update('communityPosts', p.id, { comments: [...(p.comments || []), cm] });
      return cm;
    },
    react(postId, emoji) {
      const p = findPost(postId); if (!REACTIONS.includes(emoji)) return;
      const uid = me().id, r = { ...(p.reactions || {}) };
      const had = (r[emoji] || []).includes(uid);
      REACTIONS.forEach((k) => { r[k] = (r[k] || []).filter((x) => x !== uid); });
      if (!had) r[emoji] = [...(r[emoji] || []), uid];
      db().update('communityPosts', p.id, { reactions: r });
    },
    vote(postId, i) {
      const p = findPost(postId); if (!p.poll) return;
      mustAccess(db().find('communities', p.communityId));
      const uid = me().id;
      const options = p.poll.options.map((o, k) => ({ ...o, votes: (o.votes || []).filter((x) => x !== uid).concat(k === i ? [uid] : []) }));
      db().update('communityPosts', p.id, { poll: { ...p.poll, options } });
    },
    report(postId, { commentId = null, reason }) {
      const p = findPost(postId);
      if (!['spam', 'abuse', 'scam', 'offtopic', 'other'].includes(reason)) throw new ApiError('Choose a reason.', 'invalid');
      const rep = { id: `rr-${Date.now().toString(36)}`, by: me().id, byName: me().name, reason, commentId, at: Date.now(), status: 'open' };
      db().update('communityPosts', p.id, { reports: [...(p.reports || []), rep] });
      SA.services.notify?.toAdmins({ cat: 'account', tone: 'danger', title: `Community report: ${reason}`, body: `${db().find('communities', p.communityId)?.name || ''} · “${(commentId ? p.comments.find((x) => x.id === commentId)?.text : p.text || '').slice(0, 70)}”`, link: '#/admin/community' });
    },
    deleteOwn(postId, commentId = null) {
      const p = findPost(postId); const uid = me().id;
      if (commentId) {
        const cm = p.comments.find((x) => x.id === commentId);
        if (!cm || cm.author?.id !== uid) throw new ApiError('You can only delete your own comments.', 'forbidden');
        db().update('communityPosts', p.id, { comments: p.comments.map((x) => (x.id === commentId ? { ...x, deleted: true } : x)) });
      } else {
        if (p.author?.id !== uid) throw new ApiError('You can only delete your own posts.', 'forbidden');
        db().update('communityPosts', p.id, { deleted: true });
      }
    },
    muted,
    PITCH_COMMUNITY: 'cm-pitches',
    /** Share one of the shopper's games (looking for players) as a card in "Pitches & free slots" */
    shareGame(matchId, text) {
      const m = SA.services.matches.allSync().find((x) => x.id === matchId);
      if (!m || !m.isMine) throw new ApiError('You can only share your own games.', 'forbidden');
      if (db().all('communityPosts').some((p) => !p.deleted && p.card?.type === 'game' && p.card.matchId === matchId)) throw new ApiError('This game is already shared in the community.', 'already');
      return community.createPost('cm-pitches', { text: text || `Looking for ${m.spotsLeft} player${m.spotsLeft > 1 ? 's' : ''} — ${m.level}, ${m.type}. Ask to join from the card.`, card: { type: 'game', matchId } });
    },
    /** Free slot cards posted by pitch owners (from the partner dashboard) */
    postSlot({ ownerId, pitchId, date, hour, pct = 0, text = '', promoId = null }) {
      const p = db().find('pitches', pitchId);
      const a = db().find('accounts', ownerId);
      const card = { type: 'slot', pitchId, date, hour, duration: 1, pct, promoId };
      const post = db().insert('communityPosts', { communityId: 'cm-pitches', author: { kind: 'partner', id: ownerId, name: p.name, avatar: a?.avatar ?? 0, badge: { label: `Verified venue · ${a?.company || ''}`, tone: 'venue' } },
        text: clean(text || `Free slot ${pct ? `with ${Math.round(pct * 100)}% off ` : ''}at ${p.name} — tap the card to book it.`), image: null, card, reactions: {}, comments: [], pinned: false, announcement: false, at: Date.now() }, 'cp');
      SA.services.games?.matchAlerts(card);
      return post;
    },
    /** Filters for "Pitches & free slots": area and time of day */
    slotFilter(posts, { area = '', when = '' } = {}) {
      if (!area && !when) return posts;
      return posts.filter((p) => {
        const pitchId = p.card?.type === 'slot' ? p.card.pitchId : p.card?.type === 'game' ? SA.services.matches.allSync().find((x) => x.id === p.card.matchId)?.pitchId : null;
        if (!pitchId) return !area && !when;
        const pt = db().find('pitches', pitchId);
        const hour = p.card.type === 'slot' ? p.card.hour : SA.services.matches.allSync().find((x) => x.id === p.card.matchId)?.hour;
        return (!area || pt?.area === area || pt?.city === area) && (!when || (when === 'day' ? hour < 17 : when === 'evening' ? hour >= 17 && hour < 21 : hour >= 21));
      });
    },

    /** Seed data (fictional people and stores; Koora Ink, Nile Lines and Cairo Otaku are the demo sellers) */
    seed(now = Date.now()) {
      const C = (id, slug, name, about, audience, icon, color, cover, baseMembers, order, rules, slowMode = 0) => ({ id, slug, name, about, audience, icon, color, cover, baseMembers, order, rules, slowMode, status: 'active', createdAt: now - 40 * DAY, createdBy: 'acc-admin' });
      const RULES = ['Be respectful — no insults, no fan fights.', 'No phone numbers or outside links: keep deals on Super App.', 'Only share work you made or have the rights to.', 'Report anything that breaks the rules — the Super App team checks every report.'];
      const communities = [
        C('cm-creators', 'creators', 'Super App Creators', 'Print-on-demand sellers helping each other: design feedback, what sells, printing tips and news from the Super App team.', 'sellers', 'shirt', '#FF5B14', 'stadiumNight', 214, 1, RULES),
        C('cm-lounge', 'pro-lounge', 'Pro & Elite Lounge', 'For Pro and Elite sellers: early news on new products, bigger campaigns and talking shop with the top sellers.', 'sellers-pro', 'crown', '#C9920F', 'litField', 38, 2, RULES),
        C('cm-players', 'players', 'Super App Players', 'Players across Egypt: find teammates, share pitch tips, ask anything about booking and playing.', 'everyone', 'users', '#0B6E4F', 'nightMatch', 1840, 3, RULES, 2),
        C('cm-fantasy', 'fantasy', 'Fantasy Talk', 'Captain picks, transfers and gameweek banter for the Egyptian league and the Premier League.', 'everyone', 'trophy', '#2459D6', 'stadium', 960, 4, RULES, 1),
      ];
      const A = (name, avatar, badge = null, id = `seed-${name.split(' ')[0].toLowerCase()}`) => ({ kind: 'user', id, name, avatar, badge });
      const SAT = { kind: 'admin', id: 'acc-admin', name: 'Super App Admin', avatar: 7, badge: { label: 'Official', tone: 'team' } };
      const koora = A('Omar Hany', 2, { label: 'Elite seller · Koora Ink', tone: 'elite' }), nile = A('Salma Nabil', 11, { label: 'Pro seller · Nile Lines', tone: 'pro' }), otaku = A('Nour Khaled', 9, { label: 'Rising seller · Cairo Otaku', tone: 'rising' });
      const p = (communityId, au, text, ago, extra = {}) => ({ id: `cp-seed-${communityId}-${ago}`, communityId, author: au, text, image: null, poll: null, reactions: {}, seedReactions: Math.round(4 + (ago % 7) * 3), comments: [], pinned: false, announcement: false, at: now - ago * H, ...extra });
      const cmt = (au, text, ago) => ({ id: `cm-seed-${ago}-${au.name.length}`, author: au, text, at: now - ago * H });
      const posts = [
        p('cm-creators', SAT, 'Welcome to Super App Creators! 👋 Introduce yourself: what do you design and which product sells best for you? Read the rules in “About” — no phone numbers or outside links, so deals stay safe on Super App.', 24 * 6, { pinned: true, announcement: true, seedReactions: 41 }),
        p('cm-creators', SAT, 'Derby week is in 9 days. Designs with Cairo pride (not club logos!) sold 3× more last derby. Upload early: Pro sellers are reviewed first.', 30, { announcement: true, seedReactions: 26 }),
        p('cm-creators', otaku, 'Rate my next design honestly 🙏 Retro controller + “Press Start” on a hoodie. Should the text go on the back or the front?', 8, {
          poll: { options: [{ text: 'Front, big', votes: [], seed: 12 }, { text: 'Back, with a small logo on the front', votes: [], seed: 27 }, { text: 'Only the controller, no text', votes: [], seed: 6 }] },
          comments: [cmt(koora, 'Back + small front logo. The back sells better on hoodies, people see it in the gym.', 6), cmt(nile, 'Agree with Omar. Also try it on Sand — dark prints look premium on it.', 5)] }),
        p('cm-creators', koora, 'Tip that doubled my sales: put the same design on the tee AND the cap, then share the store link, not the product link. People buy 2 items.', 52, {
          comments: [cmt(otaku, 'Trying this today, thanks!', 50), cmt(nile, 'Same for me — the cap is the impulse buy.', 49)] }),
        p('cm-creators', nile, 'Does anyone know how dark the “Bottle Green” hoodie prints? My white ink looked a bit grey on the sample.', 75, {
          comments: [cmt(SAT, 'Thanks Salma — the print partner switched to a double white underbase this week. Order another sample, it’s 30% off for Pro.', 70)] }),
        p('cm-lounge', SAT, 'Early access: the oversized tee in “Washed Black” goes live for Pro & Elite next Monday, a week before everyone else. Get your designs ready.', 20, { pinned: true, announcement: true, seedReactions: 18 }),
        p('cm-lounge', koora, 'Anyone running a joint drop for the Super Cup? Thinking 3 sellers, one theme, one weekend.', 33, { comments: [cmt(nile, 'I’m in. Let’s pick the theme here first.', 31)] }),
        p('cm-players', SAT, 'New here? Book a pitch, post your game in Find players and fill the squad in minutes. Pin your area in a comment so players near you find you.', 24 * 8, { pinned: true, announcement: true, seedReactions: 112 }),
        p('cm-players', A('Ahmed Samir', 4), 'Looking for a goalkeeper for Thursday 9 PM in Nasr City, 7-a-side. I posted it in Find players too.', 4, { comments: [cmt(A('Ziad Khaled', 6), 'I can play, sending a request now.', 3)] }),
        p('cm-players', A('Farida Ayman', 12), 'Which pitches in New Cairo have good lights for late games? Last week we couldn’t see the ball after 11 😅', 26, {
          poll: { options: [{ text: 'Fifth Settlement Arena', votes: [], seed: 31 }, { text: 'Rehab Club', votes: [], seed: 18 }, { text: 'Madinaty Park', votes: [], seed: 9 }] } }),
        p('cm-fantasy', A('Mostafa Gamal', 3), 'Captain this week? I’m torn between the two Ahly forwards 🤔', 6, {
          poll: { options: [{ text: 'Wessam Abou Ali', votes: [], seed: 64 }, { text: 'Emam Ashour', votes: [], seed: 41 }, { text: 'Someone else', votes: [], seed: 12 }] } }),
        p('cm-fantasy', A('Islam Magdy', 8), 'Took a -8 hit and it worked 😂 87 points this gameweek.', 30, { comments: [cmt(A('Belal Ehab', 1), 'Respect. I benched 14 points again.', 28)] }),
        p('cm-players', A('Spam Seller', 5), 'Cheap boots!! 50% off original, DM me for the price', 2, { reports: [{ id: 'rr-seed-1', by: 'seed-farida', byName: 'Farida Ayman', reason: 'spam', at: now - H, status: 'open' }] }),
      ];
      return { communities, posts };
    },
  };

  SA.services.community = community;
  // First run on this device: create the communities and demo posts
  if (!db().all('communities')) {
    const s = community.seed();
    db().raw().communities = s.communities; db().raw().communityPosts = s.posts;
    if (!db().settings().community) db().raw().settings.community = { bannedWords: ['scam', 'fake', 'نصاب', 'حرامي'], blockContacts: true, muted: [] };
    try { db().touch('communities'); } catch { /* storage full */ }
  }
  // "Pitches & free slots" (added later): one community for free-slot cards from venues and games looking for players
  if (!db().find('communities', 'cm-pitches')) {
    const now = Date.now();
    db().raw().communities.unshift({ id: 'cm-pitches', slug: 'pitches', name: 'Pitches & free slots', about: 'Free hours posted by verified venues — book them in one tap — and games looking for players. Filter by area and time.', audience: 'everyone',
      icon: 'pitch', color: '#0A3D2D', cover: 'aerialPitch', baseMembers: 2630, order: 0, slowMode: 0, status: 'active', createdAt: now, createdBy: 'acc-admin',
      rules: ['Slot cards come only from verified venues on Super App.', 'Share your game as a card so players can ask to join and pay safely.', 'No phone numbers or outside links — book and pay on Super App.', 'Be on time: hosts rate players after every game.'] });
    db().raw().communityPosts.push({ id: 'cp-seed-pitches-welcome', communityId: 'cm-pitches', author: { ...community.ADMIN }, text: 'Welcome! Venues post free hours here (tap “Book now” on a card), and players share games that need people. Turn on alerts to hear about free slots near you first: #/alerts', image: null, poll: null, reactions: {}, seedReactions: 64, comments: [], pinned: true, announcement: true, at: now - 3 * 864e5 });
    try { db().touch('communities'); } catch { /* storage full */ }
    // A few real free slots from demo venues (posted after the pitch data is ready)
    setTimeout(() => {
      try {
        const { isoDate, addDays, today } = SA.core.utils;
        const picks = [['p-maadi-cage', 1], ['p-zayed-22', 1], ['p-gezira-fives', 2]];
        picks.forEach(([pid, d], i) => {
          const date = isoDate(addDays(today(), d));
          const slot = (SA.services.pitches.slotsSync(pid, date) || []).find((s) => s.status === 'available' && s.hour >= 17);
          const p = db().find('pitches', pid);
          if (!slot || !p) return;
          const post = community.postSlot({ ownerId: p.ownerId, pitchId: pid, date, hour: slot.hour, pct: 0, text: i === 0 ? `Last-minute free hour at ${p.name}. First to book gets it!` : `A team cancelled — this hour at ${p.name} is free again.` });
          db().update('communityPosts', post.id, { at: now - (i + 1) * 2 * 36e5, seedReactions: 6 + i * 4 });
        });
        const g = SA.services.matches.allSync().find((m) => m.id === 'm-1001');
        if (g) db().insert('communityPosts', { communityId: 'cm-pitches', author: { kind: 'user', id: 'seed-karim', name: g.hostName, avatar: 0, badge: null }, text: 'We need a keeper and one more player for tonight — friendly but competitive. Ask to join from the card 👇', image: null, card: { type: 'game', matchId: g.id }, reactions: {}, seedReactions: 9, comments: [], pinned: false, announcement: false, at: now - 50 * 6e4 }, 'cp');
      } catch (e) { console.warn('Slot seed skipped', e); }
    }, 0);
  }
})(window.SA);
