/** Seed: open "Looking for players" matches. dayOffset is resolved to a real date by matchService. */
(function (SA) {
  const people = [
    'Karim Adel', 'Omar Hassan', 'Youssef Mahmoud', 'Ahmed Samir', 'Mostafa Gamal', 'Hazem Tarek', 'Seif Ashraf', 'Ali Reda',
    'Mohamed Fawzy', 'Ziad Khaled', 'Tamer Nabil', 'Hassan Wael', 'Amr Sherif', 'Islam Magdy', 'Khaled Ezzat', 'Marwan Hany',
    'Nour El Din', 'Mariam Fathy', 'Salma Yasser', 'Farida Ayman', 'Habiba Osama', 'Yassin Fouad', 'Adham Lotfy', 'Belal Ehab',
  ];

  const matches = [
    { id: 'm-1001', pitchId: 'p-fifth-arena', dayOffset: 0, hour: 21, duration: 1.5, total: 14, filled: 11, level: 'Intermediate', gender: 'Men', type: '7 v 7',
      host: 0, pricePerPlayer: 95, notes: 'Weekly Thursday game, friendly but competitive. Bring a dark and a white shirt. We need a keeper most of all.', needsGK: true, postedHoursAgo: 3 },
    { id: 'm-1002', pitchId: 'p-zayed-22', dayOffset: 1, hour: 22, duration: 1, total: 10, filled: 7, level: 'Advanced', gender: 'Men', type: '5 v 5',
      host: 5, pricePerPlayer: 80, notes: 'Fast-paced 5-a-side, ex-academy players. Rolling subs every 7 minutes.', needsGK: false, postedHoursAgo: 6 },
    { id: 'm-1003', pitchId: 'p-maadi-cage', dayOffset: 0, hour: 19, duration: 1, total: 10, filled: 8, level: 'Beginner', gender: 'Mixed', type: '5 v 5',
      host: 17, pricePerPlayer: 60, notes: 'Relaxed mixed game for people getting back into football. No slide tackles, lots of laughs.', needsGK: false, postedHoursAgo: 1 },
    { id: 'm-1004', pitchId: 'p-gezira-fives', dayOffset: 2, hour: 20, duration: 1, total: 10, filled: 6, level: 'Intermediate', gender: 'Men', type: '5 v 5',
      host: 2, pricePerPlayer: 85, notes: 'Office crew from Smart Village — two of us dropped out. Good level, good vibes.', needsGK: false, postedHoursAgo: 12 },
    { id: 'm-1005', pitchId: 'p-nasr-hub', dayOffset: 1, hour: 18, duration: 1, total: 10, filled: 5, level: 'Beginner', gender: 'Women', type: '5 v 5',
      host: 18, pricePerPlayer: 70, notes: 'Women-only futsal session, indoor and air-conditioned. All levels welcome — we rotate teams.', needsGK: false, postedHoursAgo: 20 },
    { id: 'm-1006', pitchId: 'p-helio-night', dayOffset: 3, hour: 23, duration: 1.5, total: 14, filled: 12, level: 'Competitive', gender: 'Men', type: '7 v 7',
      host: 9, pricePerPlayer: 120, notes: 'League-prep scrimmage on natural grass. Referee booked. Studs allowed.', needsGK: false, postedHoursAgo: 30 },
    { id: 'm-1007', pitchId: 'p-madinaty-park', dayOffset: 4, hour: 17, duration: 1.5, total: 14, filled: 9, level: 'Intermediate', gender: 'Mixed', type: '7 v 7',
      host: 13, pricePerPlayer: 75, notes: 'Saturday afternoon mixed game, families welcome to watch from the café.', needsGK: true, postedHoursAgo: 8 },
    { id: 'm-1008', pitchId: 'p-dokki-river', dayOffset: 2, hour: 22, duration: 1, total: 14, filled: 10, level: 'Advanced', gender: 'Men', type: '7 v 7',
      host: 11, pricePerPlayer: 70, notes: 'Late game after work. We keep score and play to win, but respect first.', needsGK: false, postedHoursAgo: 4 },
    { id: 'm-1009', pitchId: 'p-smouha-seven', dayOffset: 1, hour: 21, duration: 1, total: 14, filled: 11, level: 'Intermediate', gender: 'Men', type: '7 v 7',
      host: 14, pricePerPlayer: 60, notes: 'Alexandria regulars, need three more. Meet 15 minutes early to warm up.', needsGK: false, postedHoursAgo: 2 },
    // Padel Open Matches: always 4 players, level on the 1–7 scale, the court cost split by 4
    { id: 'm-1101', sport: 'padel', pitchId: 'p-padel-smash-1', dayOffset: 0, hour: 20.5, duration: 1.5, total: 4, filled: 3, level: 'Level 4', gender: 'Mixed', type: 'Open Match',
      host: 1, pricePerPlayer: 375, notes: 'Friendly Open Match on the panoramic court. We need one more around level 4, any side.', needsGK: false, postedHoursAgo: 2 },
    { id: 'm-1102', sport: 'padel', pitchId: 'p-padel-zayed-hub', dayOffset: 1, hour: 19, duration: 1.5, total: 4, filled: 2, level: 'Level 3', gender: 'Mixed', type: 'Open Match',
      host: 19, pricePerPlayer: 338, notes: 'Relaxed doubles after work. Rackets can be rented at the club.', needsGK: false, postedHoursAgo: 5 },
    { id: 'm-1103', sport: 'padel', pitchId: 'p-padel-maadi-box', dayOffset: 2, hour: 21, duration: 1, total: 4, filled: 3, level: 'Level 6', gender: 'Men', type: 'Open Match',
      host: 6, pricePerPlayer: 213, notes: 'Fast game, good lobs and smashes expected. Looking for a drive-side player.', needsGK: false, postedHoursAgo: 1 },
  ];

  SA.data.matches = { list: matches, people, levels: ['Beginner', 'Intermediate', 'Advanced', 'Competitive'], padelLevels: ['Level 1', 'Level 2', 'Level 3', 'Level 4', 'Level 5', 'Level 6', 'Level 7'], padelSides: [['Either', 'Either side'], ['Drive', 'Drive side (right)'], ['Backhand', 'Backhand side (left)']], genders: ['Mixed', 'Men', 'Women'] };
})(window.SA);
