/** Seed: the signed-in demo user (no auth server — this is the local profile). */
(function (SA) {
  SA.data.user = {
    profile: {
      id: 'u-001', name: 'Youssef Adel', firstName: 'Youssef', phone: '+20 100 482 7713', email: 'youssef.adel@example.com',
      avatar: 0, city: 'Cairo', area: 'New Cairo', position: 'Midfielder', level: 'Intermediate', foot: 'Right',
      memberSince: '2025-03-12', matchesPlayed: 47, goals: 23, rating: 4.7,
      addresses: [
        { id: 'ad-1', label: 'Home', line1: 'Building 14, Street 90', line2: 'South Investors, Fifth Settlement', city: 'New Cairo', phone: '+20 100 482 7713', default: true },
        { id: 'ad-2', label: 'Office', line1: 'B2104, Smart Village', line2: 'KM 28 Cairo–Alex Desert Rd', city: 'Giza', phone: '+20 100 482 7713' },
      ],
    },
    wallet: {
      balance: 1250,
      transactions: [
        { id: 'tx-seed-1', type: 'credit', label: 'Wallet top-up — Visa •••• 4417', amount: 1000, at: Date.now() - 6 * 864e5 },
        { id: 'tx-seed-2', type: 'credit', label: 'Refund — cancelled booking at Maadi Cage', amount: 550, at: Date.now() - 12 * 864e5 },
        { id: 'tx-seed-3', type: 'debit', label: 'Joined match at Gezira Fives', amount: 85, at: Date.now() - 14 * 864e5 },
      ],
    },
    // Super Points history before this device (earned on earlier games / orders)
    loyalty: {
      entries: [
        { id: 'pt-s1', key: 'welcome', at: Date.now() - 210 * 864e5, points: 250, label: 'Welcome to Super App', kind: 'bonus' },
        { id: 'pt-s2', at: Date.now() - 150 * 864e5, points: 132, label: 'Played at Maadi Cage (5 games)', kind: 'earn' },
        { id: 'pt-s3', at: Date.now() - 96 * 864e5, points: 260, label: 'Played at Gezira Fives (4 games)', kind: 'earn' },
        { id: 'pt-s4', at: Date.now() - 70 * 864e5, points: 189, label: 'Order delivered — Predator boots', kind: 'earn' },
        { id: 'pt-s5', at: Date.now() - 41 * 864e5, points: -500, label: 'Voucher SPV-WELC0M — 25 EGP', kind: 'redeem' },
        { id: 'pt-s6', at: Date.now() - 30 * 864e5, points: 214, label: 'Played at Fifth Settlement Arena (3 games)', kind: 'earn' },
        { id: 'pt-s7', at: Date.now() - 18 * 864e5, points: 20, label: 'Review bonus — Maadi Cage', kind: 'bonus' },
        { id: 'pt-s8', at: Date.now() - 9 * 864e5, points: 148, label: 'Played at Smouha Seven (2 games)', kind: 'earn' },
      ],
    },
    savedCards: [{ id: 'card-1', brand: 'Visa', last4: '4417', exp: '09/28', holder: 'YOUSSEF ADEL' }],
    notifications: [
      { id: 'n-1', kind: 'fantasy', title: 'Gameweek 9 deadline is tomorrow', body: 'Make your transfers and pick a captain before Friday 5:30 PM.', at: Date.now() - 2 * 36e5, read: false, link: '/fantasy' },
      { id: 'n-2', kind: 'match', title: 'A game near you needs a keeper', body: 'Fifth Settlement Arena tonight at 9 PM — 3 spots left.', at: Date.now() - 5 * 36e5, read: false, link: '/players/m-1001' },
      { id: 'n-3', kind: 'shop', title: 'Boots week: up to 20% off', body: 'Predator, Tiempo and Phantom on sale until Sunday.', at: Date.now() - 26 * 36e5, read: true, link: '/shop?cat=boots' },
    ],
  };
})(window.SA);
