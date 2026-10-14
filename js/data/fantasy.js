/**
 * Seed: fantasy datasets (mock) — Egyptian Premier League and English Premier League.
 * Clubs, a squad per club, fixtures for the season and per-gameweek points for completed rounds.
 * Generated deterministically so every reload shows the same "database".
 * Prizes are sponsor prizes, vouchers and discount codes — never cash. Prize leagues need a Fantasy Pass
 * (monthly subscription, see fantasyService).
 */
(function (SA) {
  const { rng, seeded } = SA.core.utils;

  const build = (cfg) => {
    const { key, season, currentGW, totalGW, teams, stars, firsts, lasts } = cfg;
    const shape = { GK: 2, DEF: 5, MID: 5, FWD: 3 };
    const basePrice = { GK: 4.0, DEF: 4.0, MID: 4.5, FWD: 4.5 };

    const players = [];
    teams.forEach((t) => {
      const r = rng(Math.floor(seeded(t.id) * 1e9));
      const own = (stars[t.id] || []).map(([name, pos, price]) => ({ name, pos, price, star: true }));
      Object.entries(shape).forEach(([pos, n]) => {
        const have = own.filter((p) => p.pos === pos).length;
        for (let i = have; i < n; i++) {
          const lift = (t.strength - 60) / 32; // 0..1
          const price = Math.round((basePrice[pos] + r() * 1.6 + lift * 1.2) * 2) / 2;
          own.push({ name: `${firsts[Math.floor(r() * firsts.length)]} ${lasts[Math.floor(r() * lasts.length)]}`, pos, price: Math.min(price, pos === 'GK' ? 5.0 : 6.5), star: false });
        }
      });
      own.forEach((p, i) => players.push({ id: `${t.id}-${i + 1}`, teamId: t.id, ...p }));
    });

    // Season round-robin (circle method) → fixtures for every gameweek
    const ids = teams.map((t) => t.id);
    const rounds = [];
    const arr = ids.slice();
    for (let r = 0; r < ids.length - 1; r++) {
      const pairs = [];
      for (let i = 0; i < arr.length / 2; i++) {
        const home = r % 2 === 0 ? arr[i] : arr[arr.length - 1 - i];
        const away = r % 2 === 0 ? arr[arr.length - 1 - i] : arr[i];
        pairs.push([home, away]);
      }
      rounds.push(pairs);
      arr.splice(1, 0, arr.pop());
    }
    const second = rounds.map((rd) => rd.map(([h, a]) => [a, h]));
    const allRounds = [...rounds, ...second].slice(0, totalGW);

    const strengthOf = Object.fromEntries(teams.map((t) => [t.id, t.strength]));
    const fixtures = [];
    const fxPrefix = key === 'egy' ? 'fx' : `fx-${key}`;
    allRounds.forEach((pairs, gi) => {
      const gw = gi + 1;
      pairs.forEach(([h, a], k) => {
        const fx = { id: `${fxPrefix}-${gw}-${k}`, gw, home: h, away: a, slot: k };
        if (gw < currentGW) {
          const r = rng(Math.floor(seeded(fx.id) * 1e9));
          const ph = (strengthOf[h] + 4) / 100, pa = strengthOf[a] / 100;
          fx.hs = Math.max(0, Math.round(r() * 3.2 * ph + (r() < 0.25 ? 1 : 0) - 0.4));
          fx.as = Math.max(0, Math.round(r() * 3.0 * pa - 0.5));
          fx.played = true;
        }
        fixtures.push(fx);
      });
    });

    // Fixture difficulty (1 easy – 5 hard) from opponent strength
    const fdr = (opp) => { const s = strengthOf[opp]; return s >= 88 ? 5 : s >= 78 ? 4 : s >= 70 ? 3 : s >= 65 ? 2 : 1; };

    /** Deterministic match performance for one player in one gameweek (used for seeded and simulated rounds). */
    const performance = (p, gw) => {
      const r = rng(Math.floor(seeded(`${p.id}|gw${gw}`) * 1e9));
      const quality = (p.price - 4) / 6; // 0..1
      const e = { minutes: 0, goals: 0, assists: 0, cs: 0, saves: 0, bonus: 0, yellow: 0, pts: 0 };
      if (r() >= 0.55 + quality * 0.4) return e;
      e.minutes = r() < 0.8 ? 90 : 30 + Math.floor(r() * 50);
      let pts = e.minutes >= 60 ? 2 : 1;
      const gRate = { GK: 0, DEF: 0.06, MID: 0.16, FWD: 0.3 }[p.pos] * (0.6 + quality * 1.4);
      const aRate = { GK: 0.01, DEF: 0.07, MID: 0.17, FWD: 0.12 }[p.pos] * (0.6 + quality * 1.2);
      e.goals = r() < gRate ? (r() < 0.18 ? 2 : 1) : 0;
      e.assists = r() < aRate ? 1 : 0;
      pts += e.goals * ({ GK: 6, DEF: 6, MID: 5, FWD: 4 }[p.pos]) + e.assists * 3;
      if ((p.pos === 'GK' || p.pos === 'DEF') && e.minutes >= 60 && r() < 0.3 + quality * 0.25) { pts += 4; e.cs = 1; }
      if (p.pos === 'MID' && e.minutes >= 60 && r() < 0.3) pts += 1;
      if (p.pos === 'GK') { e.saves = Math.floor(r() * 6); pts += Math.floor(e.saves / 3); }
      if (r() < 0.12) { pts -= 1; e.yellow = 1; }
      if (r() < 0.1 + quality * 0.15) { e.bonus = 1 + Math.floor(r() * 3); pts += e.bonus; }
      e.pts = pts;
      return e;
    };

    /** Recompute season aggregates for every player up to (but not including) `liveGW`. */
    const applyRounds = (liveGW) => {
      players.forEach((p) => {
        p.gw = {}; p.log = {};
        const st = { goals: 0, assists: 0, cleanSheets: 0, minutes: 0, bonus: 0, yellow: 0, saves: 0 };
        for (let gw = 1; gw < liveGW; gw++) {
          const e = performance(p, gw);
          p.gw[gw] = e.pts; p.log[gw] = e;
          st.goals += e.goals; st.assists += e.assists; st.cleanSheets += e.cs; st.minutes += e.minutes; st.bonus += e.bonus; st.yellow += e.yellow; st.saves += e.saves;
        }
        p.total = Object.values(p.gw).reduce((a, b) => a + b, 0);
        p.form = +(((p.gw[liveGW - 1] || 0) + (p.gw[liveGW - 2] || 0) + (p.gw[liveGW - 3] || 0)) / 3).toFixed(1);
        p.stats = st;
      });
    };

    players.forEach((p) => {
      const r = rng(Math.floor(seeded(p.id + 'meta') * 1e9));
      const quality = (p.price - 4) / 6;
      p.selectedBy = +(Math.min(68, 0.4 + quality * 35 * (p.star ? 1.6 : 0.6) + r() * 6)).toFixed(1);
      const s = r();
      p.status = s < 0.05 ? 'injured' : s < 0.11 ? 'doubt' : 'fit';
      p.news = p.status === 'injured' ? 'Hamstring injury — expected back in 2–3 weeks.' : p.status === 'doubt' ? '75% chance of playing — knock picked up in training.' : '';
    });
    applyRounds(currentGW);

    /** Results for fixtures of simulated rounds */
    const playFixture = (fx) => {
      if (fx.played) return fx;
      const r = rng(Math.floor(seeded(fx.id) * 1e9));
      const ph = (strengthOf[fx.home] + 4) / 100, pa = strengthOf[fx.away] / 100;
      return Object.assign(fx, { hs: Math.max(0, Math.round(r() * 3.2 * ph + (r() < 0.25 ? 1 : 0) - 0.4)), as: Math.max(0, Math.round(r() * 3.0 * pa - 0.5)), played: true });
    };

    const rules = {
      budget: 100.0, squad: shape, maxPerTeam: 3, starting: 11,
      formations: ['3-4-3', '3-5-2', '4-4-2', '4-3-3', '4-5-1', '5-3-2', '5-4-1'],
      freeTransfersPerGW: 1, transferCost: 4,
      scoring: [
        ['Playing up to 60 minutes', 1], ['Playing 60 minutes or more', 2], ['Goal by a goalkeeper or defender', 6], ['Goal by a midfielder', 5],
        ['Goal by a forward', 4], ['Assist', 3], ['Clean sheet — goalkeeper or defender', 4], ['Clean sheet — midfielder', 1],
        ['Every 3 saves by a goalkeeper', 1], ['Penalty save', 5], ['Bonus points for the best players in a match', '1–3'],
        ['Yellow card', -1], ['Red card', -3], ['Own goal', -2],
      ],
    };

    return { key, label: cfg.label, short: cfg.short, managers: cfg.managers, season, currentGW, totalGW, teams, players, fixtures, fdr, rules,
      officialLeagues: cfg.officialLeagues, privateGroups: cfg.privateGroups, joinableGroups: cfg.joinableGroups, performance, applyRounds, playFixture };
  };

  /* ================= Egyptian Premier League ================= */
  // kit: pattern drawn by the shirt SVG — solid | stripes | hoops | bands | sash | half
  const EGY = {
    key: 'egy', label: 'Egyptian Premier League', short: 'Egypt', managers: 184320, season: '2026/27', currentGW: 9, totalGW: 34,
    teams: [
      { id: 'AHL', name: 'Al Ahly', city: 'Cairo', strength: 92, kit: { a: '#C8102E', b: '#FFFFFF', p: 'solid' } },
      { id: 'ZAM', name: 'Zamalek', city: 'Giza', strength: 86, kit: { a: '#FFFFFF', b: '#D7182A', p: 'bands' } },
      { id: 'PYR', name: 'Pyramids', city: 'Cairo', strength: 88, kit: { a: '#0A2240', b: '#6CACE4', p: 'solid' } },
      { id: 'MAS', name: 'Al Masry', city: 'Port Said', strength: 78, kit: { a: '#00843D', b: '#FFFFFF', p: 'solid' } },
      { id: 'MOD', name: 'Modern Sport', city: 'Cairo', strength: 74, kit: { a: '#00A3AD', b: '#0B2545', p: 'sash' } },
      { id: 'CER', name: 'Ceramica Cleopatra', city: 'Cairo', strength: 76, kit: { a: '#232323', b: '#F58220', p: 'stripes' } },
      { id: 'SMO', name: 'Smouha', city: 'Alexandria', strength: 70, kit: { a: '#5BC2E7', b: '#FFFFFF', p: 'solid' } },
      { id: 'ENP', name: 'ENPPI', city: 'Cairo', strength: 71, kit: { a: '#E31B23', b: '#FFFFFF', p: 'half' } },
      { id: 'ISM', name: 'Ismaily', city: 'Ismailia', strength: 66, kit: { a: '#FFD200', b: '#0055A4', p: 'solid' } },
      { id: 'ITT', name: 'Al Ittihad', city: 'Alexandria', strength: 69, kit: { a: '#1B5E20', b: '#F2C230', p: 'stripes' } },
      { id: 'GAI', name: 'Talaea El Gaish', city: 'Cairo', strength: 67, kit: { a: '#7A1F2B', b: '#FFFFFF', p: 'solid' } },
      { id: 'NBE', name: 'National Bank', city: 'Cairo', strength: 72, kit: { a: '#002F6C', b: '#C9A227', p: 'sash' } },
      { id: 'MAH', name: 'Ghazl El Mahalla', city: 'El Mahalla', strength: 64, kit: { a: '#2D5DA8', b: '#FFFFFF', p: 'hoops' } },
      { id: 'GOU', name: 'El Gouna', city: 'Hurghada', strength: 65, kit: { a: '#F47B20', b: '#0B2545', p: 'solid' } },
      { id: 'PET', name: 'Petrojet', city: 'Suez', strength: 66, kit: { a: '#005BAC', b: '#FDB913', p: 'half' } },
      { id: 'HAR', name: 'Haras El Hodoud', city: 'Alexandria', strength: 62, kit: { a: '#3B6E3B', b: '#FFFFFF', p: 'hoops' } },
      { id: 'ZED', name: 'ZED FC', city: 'Cairo', strength: 73, kit: { a: '#111111', b: '#C6FF00', p: 'stripes' } },
      { id: 'PHA', name: 'Pharco', city: 'Alexandria', strength: 63, kit: { a: '#0098DB', b: '#FFFFFF', p: 'bands' } },
    ],
    // Headline players per club. Remaining squad slots are generated.
    stars: {
      AHL: [['Mohamed El Shenawy', 'GK', 5.5], ['Yasser Ibrahim', 'DEF', 5.5], ['Mohamed Hany', 'DEF', 5.0], ['Ahmed Ramadan', 'DEF', 5.0], ['Emam Ashour', 'MID', 9.0], ['Trezeguet', 'MID', 9.5], ['Ahmed Sayed Zizo', 'MID', 10.0], ['Hussein El Shahat', 'MID', 7.5], ['Marwan Attia', 'MID', 6.0], ['Mohamed Sherif', 'FWD', 8.5]],
      ZAM: [['Mohamed Awad', 'GK', 5.0], ['Hossam Abdelmaguid', 'DEF', 5.0], ['Mahmoud Bentayg', 'DEF', 5.0], ['Abdallah El Saied', 'MID', 7.5], ['Nabil Emad Dunga', 'MID', 6.0], ['Ahmed Hamdi', 'MID', 6.5], ['Nasser Mansi', 'FWD', 8.0], ['Seif El Din Jaziri', 'FWD', 7.5]],
      PYR: [['Ahmed El Shenawy', 'GK', 5.0], ['Ahmed Samy', 'DEF', 5.0], ['Osama Galal', 'DEF', 5.0], ['Mohanad Lasheen', 'MID', 6.5], ['Ramadan Sobhi', 'MID', 8.0], ['Walid El Karti', 'MID', 7.5], ['Mostafa Fathi', 'MID', 8.5], ['Fiston Mayele', 'FWD', 9.0]],
      MAS: [['Salah Mohsen', 'FWD', 7.0], ['Mohamed Hamdy Zaki', 'DEF', 4.5], ['Omar El Wahsh', 'MID', 6.0]],
      CER: [['Marwan Hamdi', 'FWD', 7.0], ['Mohamed Reda Bobo', 'MID', 6.5]],
      ZED: [['Hossam Ashraf', 'FWD', 6.5], ['Mohamed Hesham', 'MID', 6.0]],
      SMO: [['Mohamed Adel', 'FWD', 6.0]],
      ENP: [['Ahmed Kamal', 'MID', 6.0]],
      NBE: [['Ahmed Refaat', 'MID', 6.5]],
      MOD: [['Mohamed Magdy Afsha', 'MID', 6.5]],
      ISM: [['Ahmed Yasser', 'FWD', 5.5]],
      ITT: [['Mabululu', 'FWD', 6.5]],
    },
    firsts: ['Mohamed', 'Ahmed', 'Mahmoud', 'Mostafa', 'Omar', 'Karim', 'Hossam', 'Youssef', 'Ali', 'Islam', 'Amr', 'Tarek', 'Hesham', 'Ibrahim', 'Khaled', 'Ziad', 'Hamza', 'Marwan', 'Seif', 'Hazem', 'Abdelrahman', 'Mazen', 'Ayman', 'Sherif', 'Kareem', 'Fady', 'Belal', 'Adham'],
    lasts: ['Gaber', 'Fathy', 'El Sayed', 'Hamdy', 'Abdelaziz', 'Mansour', 'Salah', 'Kamal', 'Shawky', 'Eid', 'Ragab', 'Soliman', 'Nasr', 'El Gendy', 'Fouad', 'Hegazy', 'Morsy', 'Wahba', 'Zaki', 'Atef', 'Galal', 'Shehata', 'Rizk', 'Barakat', 'El Masry', 'Tawfik', 'Farag', 'Lotfy'],
    // Leagues run by Super App. "pass" leagues need a Fantasy Pass; prizes come from sponsors and partners.
    officialLeagues: [
      { id: 'ol-grand', name: 'Super App Grand League', kind: 'official', participants: 184320, prizeValue: 25000,
        scope: 'Season', from: 1, blurb: 'Everyone who builds a team is entered automatically.', color: 'ink',
        prizes: [['1st', 'Two season tickets + signed kit'], ['2nd', 'Signed kit + 2,000 EGP shop voucher'], ['3rd', '1,000 EGP shop voucher'], ['Top 100', '15% shop discount code']] },
      { id: 'ol-derby', name: 'Cairo Derby Cup', kind: 'pass', participants: 3846, cap: 5000, prizeValue: 60000,
        scope: 'GW 9 – GW 17', from: 9, to: 17, blurb: 'Nine gameweeks — the winner watches the derby from the stands.', color: 'blaze',
        prizes: [['1st', 'Two derby tickets + signed kit'], ['2nd', '10 free pitch hours at partner venues'], ['3rd', '1,500 EGP shop voucher'], ['4th – 10th', '500 EGP shop voucher each'], ['11th – 100th', '25% off one booking at partner venues']] },
      { id: 'ol-weekly', name: 'Gameweek Sprint', kind: 'pass', participants: 9712, cap: 20000, prizeValue: 15000,
        scope: 'GW 9 only', from: 9, to: 9, blurb: 'One gameweek, instant results. Highest score on Monday night wins.', color: 'turf',
        prizes: [['1st', 'Signed shirt + 1,000 EGP shop voucher'], ['2nd', '5 free pitch hours'], ['3rd', '500 EGP shop voucher'], ['Top 500', '10% shop discount code']] },
      { id: 'ol-elite', name: 'Champions Circle', kind: 'pass', participants: 412, cap: 1000, prizeValue: 40000,
        scope: 'Rest of season', from: 9, to: 34, blurb: 'Capped at 1,000 managers. For the serious ones.', color: 'gold',
        prizes: [['1st', 'Away-match trip for two'], ['2nd', 'A season of free Thursday games at a partner venue'], ['3rd', '3,000 EGP shop voucher'], ['4th – 10th', '1,000 EGP shop voucher each']] },
    ],
    // Private groups the user already belongs to (members are other managers)
    privateGroups: [
      { id: 'pg-office', name: 'Smart Village Office League', code: 'SA-OFF7Q2', owner: 'Karim Adel', createdAt: '2026-08-14',
        members: ['Karim Adel', 'Omar Hassan', 'Seif Ashraf', 'Mostafa Gamal', 'Ziad Khaled', 'Hassan Wael', 'Amr Sherif', 'Islam Magdy'] },
      { id: 'pg-school', name: 'El Orman Class of 2015', code: 'SA-ORM15K', owner: 'Youssef Mahmoud', createdAt: '2026-08-20',
        members: ['Youssef Mahmoud', 'Ali Reda', 'Tamer Nabil', 'Marwan Hany', 'Khaled Ezzat', 'Belal Ehab'] },
    ],
    // A group the user can join with an invite code (try SA-GEZ5A1)
    joinableGroups: [
      { id: 'pg-gezira', name: 'Gezira Thursday Ballers', code: 'SA-GEZ5A1', owner: 'Ali Reda', createdAt: '2026-08-02',
        members: ['Ali Reda', 'Hazem Tarek', 'Ziad Khaled', 'Adham Lotfy', 'Yassin Fouad', 'Mostafa Gamal', 'Seif Ashraf', 'Tamer Nabil', 'Marwan Hany', 'Islam Magdy', 'Belal Ehab'] },
    ],
  };

  /* ================= English Premier League (mock squads; kits are colour patterns, not crests) ================= */
  const EPL = {
    key: 'epl', label: 'English Premier League', short: 'England', managers: 96410, season: '2026/27', currentGW: 9, totalGW: 38,
    teams: [
      { id: 'ARS', name: 'Arsenal', city: 'London', strength: 92, kit: { a: '#EF0107', b: '#FFFFFF', p: 'half' } },
      { id: 'LIV', name: 'Liverpool', city: 'Liverpool', strength: 92, kit: { a: '#C8102E', b: '#F6EB61', p: 'solid' } },
      { id: 'MCI', name: 'Manchester City', city: 'Manchester', strength: 91, kit: { a: '#6CABDD', b: '#FFFFFF', p: 'solid' } },
      { id: 'CHE', name: 'Chelsea', city: 'London', strength: 86, kit: { a: '#034694', b: '#FFFFFF', p: 'solid' } },
      { id: 'MUN', name: 'Manchester United', city: 'Manchester', strength: 82, kit: { a: '#DA291C', b: '#000000', p: 'solid' } },
      { id: 'NEW', name: 'Newcastle United', city: 'Newcastle', strength: 83, kit: { a: '#111111', b: '#FFFFFF', p: 'stripes' } },
      { id: 'TOT', name: 'Tottenham Hotspur', city: 'London', strength: 80, kit: { a: '#FFFFFF', b: '#132257', p: 'solid' } },
      { id: 'AVL', name: 'Aston Villa', city: 'Birmingham', strength: 81, kit: { a: '#670E36', b: '#95BFE5', p: 'half' } },
      { id: 'BHA', name: 'Brighton', city: 'Brighton', strength: 77, kit: { a: '#0057B8', b: '#FFFFFF', p: 'stripes' } },
      { id: 'CRY', name: 'Crystal Palace', city: 'London', strength: 76, kit: { a: '#1B458F', b: '#C4122E', p: 'stripes' } },
      { id: 'BOU', name: 'Bournemouth', city: 'Bournemouth', strength: 74, kit: { a: '#DA291C', b: '#000000', p: 'stripes' } },
      { id: 'BRE', name: 'Brentford', city: 'London', strength: 73, kit: { a: '#E30613', b: '#FFFFFF', p: 'stripes' } },
      { id: 'FUL', name: 'Fulham', city: 'London', strength: 72, kit: { a: '#FFFFFF', b: '#000000', p: 'solid' } },
      { id: 'NFO', name: 'Nottingham Forest', city: 'Nottingham', strength: 75, kit: { a: '#DD0000', b: '#FFFFFF', p: 'solid' } },
      { id: 'EVE', name: 'Everton', city: 'Liverpool', strength: 71, kit: { a: '#003399', b: '#FFFFFF', p: 'solid' } },
      { id: 'WHU', name: 'West Ham United', city: 'London', strength: 72, kit: { a: '#7A263A', b: '#1BB1E7', p: 'half' } },
      { id: 'WOL', name: 'Wolverhampton', city: 'Wolverhampton', strength: 68, kit: { a: '#FDB913', b: '#231F20', p: 'solid' } },
      { id: 'LEE', name: 'Leeds United', city: 'Leeds', strength: 67, kit: { a: '#FFFFFF', b: '#1D428A', p: 'solid' } },
      { id: 'SUN', name: 'Sunderland', city: 'Sunderland', strength: 65, kit: { a: '#EB172B', b: '#FFFFFF', p: 'stripes' } },
      { id: 'BUR', name: 'Burnley', city: 'Burnley', strength: 64, kit: { a: '#6C1D45', b: '#99D6EA', p: 'half' } },
    ],
    stars: {
      LIV: [['Alisson', 'GK', 5.5], ['Virgil van Dijk', 'DEF', 6.0], ['Mohamed Salah', 'MID', 13.0], ['Florian Wirtz', 'MID', 8.5], ['Alexander Isak', 'FWD', 10.5]],
      ARS: [['David Raya', 'GK', 5.5], ['William Saliba', 'DEF', 6.0], ['Gabriel', 'DEF', 6.0], ['Bukayo Saka', 'MID', 10.0], ['Martin Ødegaard', 'MID', 8.5], ['Viktor Gyökeres', 'FWD', 9.0]],
      MCI: [['Gianluigi Donnarumma', 'GK', 5.5], ['Joško Gvardiol', 'DEF', 6.0], ['Rodri', 'MID', 6.5], ['Phil Foden', 'MID', 8.0], ['Omar Marmoush', 'MID', 8.0], ['Erling Haaland', 'FWD', 14.0]],
      CHE: [['Robert Sánchez', 'GK', 5.0], ['Reece James', 'DEF', 5.5], ['Cole Palmer', 'MID', 10.5], ['Enzo Fernández', 'MID', 7.0], ['João Pedro', 'FWD', 7.5]],
      MUN: [['Bruno Fernandes', 'MID', 9.0], ['Bryan Mbeumo', 'MID', 8.0], ['Benjamin Šeško', 'FWD', 7.5]],
      NEW: [['Bruno Guimarães', 'MID', 7.0], ['Anthony Gordon', 'MID', 7.5], ['Nick Woltemade', 'FWD', 7.0]],
      TOT: [['Guglielmo Vicario', 'GK', 5.0], ['Mohammed Kudus', 'MID', 7.0], ['Richarlison', 'FWD', 6.5]],
      AVL: [['Morgan Rogers', 'MID', 7.0], ['Ollie Watkins', 'FWD', 8.5]],
      BHA: [['Kaoru Mitoma', 'MID', 6.5], ['Danny Welbeck', 'FWD', 6.5]],
      CRY: [['Adam Wharton', 'MID', 5.5], ['Jean-Philippe Mateta', 'FWD', 7.5]],
      NFO: [['Morgan Gibbs-White', 'MID', 7.5], ['Chris Wood', 'FWD', 7.5]],
      EVE: [['Jordan Pickford', 'GK', 5.0], ['Iliman Ndiaye', 'MID', 6.5]],
      WHU: [['Jarrod Bowen', 'MID', 7.5]],
      BOU: [['Antoine Semenyo', 'MID', 7.5]],
      WOL: [['Jørgen Strand Larsen', 'FWD', 6.5]],
    },
    firsts: ['James', 'Jack', 'Harry', 'Oliver', 'George', 'Charlie', 'Tom', 'Josh', 'Ben', 'Lewis', 'Ryan', 'Callum', 'Luke', 'Jamie', 'Kyle', 'Aaron', 'Liam', 'Owen', 'Connor', 'Dan', 'Mason', 'Ethan', 'Sam', 'Joe'],
    lasts: ['Walker', 'Cooper', 'Hughes', 'Clarke', 'Wright', 'Turner', 'Mitchell', 'Phillips', 'Evans', 'Morris', 'Bennett', 'Barnes', 'Fletcher', 'Webb', 'Carter', 'Lowe', 'Shaw', 'Holmes', 'Dawson', 'Reid', 'Palmer', 'Knight', 'Ward', 'Burke'],
    officialLeagues: [
      { id: 'ol-epl-grand', name: 'Super App Premier League Cup', kind: 'official', participants: 96410, prizeValue: 20000,
        scope: 'Season', from: 1, blurb: 'Everyone who builds an English Premier League team is entered automatically.', color: 'ink',
        prizes: [['1st', 'Signed Premier League shirt + 2,000 EGP shop voucher'], ['2nd', '1,500 EGP shop voucher'], ['3rd', '1,000 EGP shop voucher'], ['Top 100', '15% shop discount code']] },
      { id: 'ol-epl-kings', name: 'Egyptian Kings League', kind: 'pass', participants: 5120, cap: 10000, prizeValue: 35000,
        scope: 'GW 9 – GW 20', from: 9, to: 20, blurb: 'Back the Egyptians abroad — the top managers win watch-party nights and kits.', color: 'blaze',
        prizes: [['1st', 'Watch-party for 10 at a partner venue + two shirts'], ['2nd', '10 free pitch hours'], ['3rd', '1,500 EGP shop voucher'], ['4th – 50th', '20% off one booking']] },
      { id: 'ol-epl-sprint', name: 'Super Sunday Sprint', kind: 'pass', participants: 7304, cap: 20000, prizeValue: 12000,
        scope: 'GW 9 only', from: 9, to: 9, blurb: 'One gameweek of English football. Best score wins.', color: 'turf',
        prizes: [['1st', 'Signed shirt'], ['2nd', '5 free pitch hours'], ['3rd', '500 EGP shop voucher'], ['Top 500', '10% shop discount code']] },
    ],
    privateGroups: [
      { id: 'pg-epl-office', name: 'Office Premier League Mini-League', code: 'SA-EPL7W3', owner: 'Omar Hassan', createdAt: '2026-08-16',
        members: ['Omar Hassan', 'Karim Adel', 'Ziad Khaled', 'Amr Sherif', 'Hassan Wael', 'Seif Ashraf'] },
    ],
    joinableGroups: [
      { id: 'pg-epl-uni', name: 'AUC Premier League Fans', code: 'SA-EPL4U2', owner: 'Tamer Nabil', createdAt: '2026-08-10',
        members: ['Tamer Nabil', 'Marwan Hany', 'Adham Lotfy', 'Yassin Fouad', 'Belal Ehab', 'Islam Magdy', 'Mostafa Gamal'] },
    ],
  };

  SA.data.fantasy = build(EGY);
  SA.data.fantasyEPL = build(EPL);
  SA.data.fantasyLeagues = { egy: SA.data.fantasy, epl: SA.data.fantasyEPL };
})(window.SA);
