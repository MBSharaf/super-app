/** Real photography (Unsplash). Each entry was checked to load and to match its subject. */
(function (SA) {
  // Partner uploads (data: / blob: / full URLs) are used as-is; stock photo ids go through the image CDN
  // "pod:<designId>:<colour>[:back]" = a print-on-demand design drawn on its garment (SA.services.pod.mockUrl)
  const u = (id, w = 900, h) => (String(id || '').startsWith('pod:') ? SA.services.pod?.mockUrl(id) || ''
    : /^(data:|blob:|https?:)/.test(id || '') ? id : `https://images.unsplash.com/${id}?auto=format&fit=crop&w=${w}${h ? `&h=${h}` : ''}&q=72`);

  const photos = {
    // Pitches & venues
    nightMatch: 'photo-1431324155629-1a6deb1dec8d',
    litField: 'photo-1487466365202-1afdb86c764e',
    aerialPitch: 'photo-1556056504-5c7696c4c28d',
    birdsEyeLights: 'photo-1517747614396-d21a78b850e8',
    goalTrees: 'photo-1543351611-58f69d7c1781',
    turfLine: 'photo-1459865264687-595d652de67e',
    turfKick: 'photo-1785528469373-1103f69c2943',
    stringLights: 'photo-1782974396379-c86aa9cf3c1c',
    turfGoal: 'photo-1779288440792-f47b71179e21',
    indoorTurf: 'photo-1789060121008-a310f690c742',
    aerialField: 'photo-1521217078329-f8fc1becab68',
    aerialPlayers: 'photo-1509077613385-f89402467146',
    futsalGoal: 'photo-1553627220-92f0446b6a5f',
    futsalArena: 'photo-1763775594018-4a84eeadd83d',
    groupPlaying: 'photo-1630420598913-44208d36f9af',
    goalNet: 'photo-1574676581439-89ee9f0a7bf1',
    fieldGoal: 'photo-1632684363781-c82fe0e357c8',
    nightPlayers: 'photo-1711447395363-5595964fd7ab',
    nightGrass: 'photo-1621058567697-7673eef9ce28',
    fieldLights: 'photo-1493038950213-29266628eb06',
    ballsInGoal: 'photo-1551958219-acbc608c6377',
    stadium: 'photo-1522778119026-d647f0596c20',
    stadiumNight: 'photo-1489944440615-453fc2b6a9a9',
    // Action
    kick16: 'photo-1517466787929-bc90951d0974',
    kickDay: 'photo-1560272564-c83b66b1ad12',
    duel: 'photo-1543326727-cf6c39e8f84c',
    menPlaying: 'photo-1494177310973-4841f7d5a882',
    menField: 'photo-1551385093-ad3fb362dc43',
    greenTeam: 'photo-1598881034666-6d3443d4b1bc',
    womenPlaying: 'photo-1624880357913-a8539238245b',
    kidsTraining: 'photo-1526232761682-d26e03ac148e',
    teamLineup: 'photo-1628891890467-b79f2c8ba9dc',
    trophy: 'photo-1518091043644-c1d4457512c6',
    footBall: 'photo-1606925797300-0b35e9d1794e',
    goalkeeperDive: 'photo-1774201427012-fd1eb39deddb',
    cairoNile: 'photo-1719659018185-8a239c35fb4a',
  };

  const avatars = [
    'photo-1568602471122-7832951cc4c5', 'photo-1522529599102-193c0d76b5b6', 'photo-1492562080023-ab3db95bfbce',
    'photo-1527980965255-d3b416303d12', 'photo-1592234789031-94bf65f630ed', 'photo-1583692331507-fc0bd348695d',
    'photo-1602896441343-ed0dc62ed65e', 'photo-1557862921-37829c790f19', 'photo-1508341591423-4347099e1f19',
    'photo-1580489944761-15a19d654956', 'photo-1662850886700-4ec19bd30d11', 'photo-1489278353717-f64c6ee8a4d2',
    'photo-1564564295391-7f24f26f568b', 'photo-1625241152315-4a698f74ceb7', 'photo-1600603406200-5b2a104684ac',
  ];

  /**
   * Padel courts (2026-11-21): a drawn court seen from above — glass walls all round (Panoramic) or glass at the back and mesh
   * on the sides (Classic) — so a court always shows a court. Used as the first photo of the demo padel clubs.
   */
  const court = (panoramic, turf, light) => {
    const glass = panoramic ? '<rect x="40" y="40" width="720" height="420" rx="6" fill="none" stroke="#d9f2ff" stroke-opacity=".85" stroke-width="10"/>'
      : '<path d="M40 40h720M40 460h720" stroke="#d9f2ff" stroke-opacity=".85" stroke-width="10"/><path d="M40 40v420M760 40v420" stroke="#ffffff" stroke-opacity=".35" stroke-width="6" stroke-dasharray="4 7"/>';
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 500"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${light}"/><stop offset="1" stop-color="${turf}"/></linearGradient>`
      + `<radialGradient id="l" cx=".5" cy=".1" r=".9"><stop offset="0" stop-color="#fff" stop-opacity=".28"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient></defs>`
      + `<rect width="800" height="500" fill="#0b1f17"/><rect x="40" y="40" width="720" height="420" fill="url(#g)"/>`
      + `<g stroke="#fff" stroke-width="5" fill="none"><path d="M400 40v420"/><path d="M190 40v420M610 40v420"/><path d="M190 250h420"/></g>`
      + `<path d="M400 34v432" stroke="#0b1f17" stroke-width="9"/><path d="M400 34v432" stroke="#fff" stroke-width="3" stroke-dasharray="2 6"/>${glass}`
      + `<rect width="800" height="500" fill="url(#l)"/><circle cx="520" cy="170" r="9" fill="#e6ff3a" stroke="#0b1f17" stroke-width="2"/></svg>`;
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  };
  Object.assign(photos, {
    courtPanoramicBlue: court(true, '#1f5fbf', '#3f86e8'), courtPanoramicGreen: court(true, '#1f7a4c', '#2fa36a'),
    courtClassicBlue: court(false, '#24508f', '#3a73c4'), courtClassicTerracotta: court(false, '#b5532f', '#d97447'),
    padelRacket: 'photo-1612534847738-b3af9bc31f0c', padelRackets: 'photo-1657704358775-ed705c7388d2', padelSet: 'photo-1658723826297-fe4d1b1e6600',
  });

  SA.data.media = { u, photos, avatars, photo: (key, w, h) => u(photos[key] || key, w, h), avatar: (i, w = 120) => u(avatars[i % avatars.length], w, w) };
})(window.SA);
