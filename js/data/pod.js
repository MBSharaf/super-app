/**
 * Seed data for the seller layer: the print-on-demand catalogue (made and shipped by the print partner),
 * design categories, seller levels, and demo sellers with original artwork.
 * Prices are in EGP. `base` already includes Super App's margin; `partnerCost` is what the print partner charges us
 * and is never shown to sellers. All values are copied into the platform DB once and the admin can edit them.
 */
(function (SA) {
  const C = (name, hex) => ({ name, hex });
  const SIZES = ['S', 'M', 'L', 'XL', '2XL', '3XL'];

  const catalog = [
    { id: 'pod-tee', name: 'Classic T-shirt', kind: 'tee', desc: '180 g combed cotton, regular fit, DTF print that survives the wash.', base: 300, partnerCost: 210, backExtra: 60, bigSizeExtra: 30, bigSizes: ['2XL', '3XL'],
      minPrice: 330, maxPrice: 900, sizes: SIZES, areas: ['front', 'back'], status: 'active',
      colors: [C('White', '#F7F7F5'), C('Black', '#141414'), C('Navy', '#1C2541'), C('Heather Grey', '#9A9DA2'), C('Bottle Green', '#1F4D3A'), C('Sand', '#D8C7A6')] },
    { id: 'pod-oversized', name: 'Oversized T-shirt', kind: 'tee', desc: '240 g heavyweight cotton, dropped shoulders, boxy streetwear fit.', base: 380, partnerCost: 270, backExtra: 60, bigSizeExtra: 30, bigSizes: ['2XL', '3XL'],
      minPrice: 420, maxPrice: 1000, sizes: SIZES, areas: ['front', 'back'], status: 'active',
      colors: [C('Black', '#141414'), C('Off-white', '#EFEBE1'), C('Sage', '#8FA58A'), C('Brown', '#5A4032'), C('Washed Blue', '#5E7E9E')] },
    { id: 'pod-hoodie', name: 'Hoodie', kind: 'hoodie', desc: '320 g brushed fleece, double-lined hood, kangaroo pocket.', base: 600, partnerCost: 430, backExtra: 70, bigSizeExtra: 40, bigSizes: ['2XL', '3XL'],
      minPrice: 650, maxPrice: 1600, sizes: SIZES, areas: ['front', 'back'], status: 'active',
      colors: [C('Black', '#141414'), C('Heather Grey', '#9A9DA2'), C('Navy', '#1C2541'), C('Maroon', '#5B1A24'), C('Off-white', '#EFEBE1')] },
    { id: 'pod-cap', name: 'Baseball Cap', kind: 'cap', desc: 'Six-panel cotton twill, adjustable strap, embroidered-look print on the front.', base: 220, partnerCost: 150, backExtra: 0, bigSizeExtra: 0, bigSizes: [],
      minPrice: 250, maxPrice: 600, sizes: ['One size'], areas: ['front'], status: 'active',
      colors: [C('Black', '#141414'), C('White', '#F7F7F5'), C('Navy', '#1C2541'), C('Khaki', '#B9A77F')] },
  ];

  const categories = [
    { id: 'football', name: 'Football fans' }, { id: 'sports', name: 'Sports & gym' }, { id: 'egyptian', name: 'Egyptian phrases' },
    { id: 'anime', name: 'Anime & manga' }, { id: 'gaming', name: 'Gaming' }, { id: 'music', name: 'Music' },
    { id: 'retro', name: 'Retro' }, { id: 'minimal', name: 'Minimal' }, { id: 'motivation', name: 'Motivation' }, { id: 'funny', name: 'Funny' },
  ];

  /** Level rules: lifetime delivered items sold + minimum rating. null = unlimited. */
  const levels = [
    { id: 'new', name: 'Starter', sales: 0, rating: 0, designs: 25, codes: 2, baseDiscount: 0, commission: 0.12, review: 'normal', boost: 0, featured: 'none', calendar: 'basic', analytics: 'basic', bulk: false, sampleDiscount: 0, freeSamples: 0, teamKits: false, earlyAccess: false, fantasyDiscount: 0, support: 'normal' },
    { id: 'rising', name: 'Rising', sales: 10, rating: 4.0, designs: 100, codes: 5, baseDiscount: 10, commission: 0.11, review: 'normal', boost: 1, featured: 'none', calendar: 'weather', analytics: 'basic', bulk: false, sampleDiscount: 0, freeSamples: 0, teamKits: false, earlyAccess: false, fantasyDiscount: 0, support: 'normal' },
    { id: 'pro', name: 'Pro', sales: 50, rating: 4.3, designs: 300, codes: 15, baseDiscount: 20, commission: 0.10, review: 'priority', boost: 2, featured: 'eligible', calendar: 'trends', analytics: 'advanced', bulk: true, sampleDiscount: 0.3, freeSamples: 0, teamKits: true, earlyAccess: false, fantasyDiscount: 0.5, support: 'priority' },
    { id: 'elite', name: 'Elite', sales: 200, rating: 4.6, designs: null, codes: null, baseDiscount: 35, commission: 0.08, review: 'instant', boost: 3, featured: 'featured', calendar: 'early', analytics: 'export', bulk: true, sampleDiscount: 1, freeSamples: 1, teamKits: true, earlyAccess: true, fantasyDiscount: 1, support: 'manager' },
  ];

  /* ---------- Original artwork, drawn as SVG (no logos, crests or real people) ---------- */
  const svg = (inner, w = 600, h = 600) => `data:image/svg+xml;charset=utf-8,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}">${inner}</svg>`)}`;
  const T = (x, y, size, fill, text, extra = '') => `<text x="${x}" y="${y}" ${/font-family/.test(extra) ? '' : `font-family="Archivo, 'Arial Black', Arial, sans-serif"`} font-weight="900" font-size="${size}" fill="${fill}" text-anchor="middle" ${extra}>${text}</text>`;
  const art = {
    nights: svg(`<circle cx="300" cy="250" r="170" fill="none" stroke="#F2C14E" stroke-width="14"/><path d="M180 250h240M300 80v340" stroke="#F2C14E" stroke-width="6" opacity=".5"/>${T(300, 270, 92, '#F2C14E', 'CAIRO')}${T(300, 360, 54, '#FFFFFF', 'NIGHTS FC')}${T(300, 470, 30, '#F2C14E', 'EST. 2026 · 5-A-SIDE', 'letter-spacing="6"')}`),
    press: svg(`<rect x="70" y="90" width="460" height="420" rx="28" fill="none" stroke="#E4572E" stroke-width="12"/>${T(300, 210, 64, '#E4572E', 'KEEP CALM')}${T(300, 300, 46, '#FFFFFF', '&amp;')}${T(300, 400, 64, '#E4572E', 'PRESS HIGH')}`),
    yalla: svg(`<circle cx="300" cy="300" r="230" fill="#17795A"/><circle cx="300" cy="300" r="190" fill="none" stroke="#FFFFFF" stroke-width="6" stroke-dasharray="18 14"/>${T(300, 290, 120, '#FFFFFF', 'يلا نلعب', 'font-family="Cairo, Tahoma, Arial"')}${T(300, 390, 34, '#D6F5E8', 'YALLA NEL3AB', 'letter-spacing="5"')}`),
    keeper: svg(`<path d="M300 70l190 70v150c0 130-90 210-190 250-100-40-190-120-190-250V140z" fill="#1C2541" stroke="#7CC6FE" stroke-width="10"/>${T(300, 280, 70, '#7CC6FE', 'No.1')}${T(300, 370, 44, '#FFFFFF', 'SAFE HANDS')}`),
    pyramids: svg(`<path d="M90 470L240 200l150 270z" fill="#E0B354"/><path d="M290 470l130-230 130 230z" fill="#C9963A"/><circle cx="430" cy="140" r="54" fill="#E4572E"/>${T(300, 545, 46, '#E0B354', 'GIZA RUN CLUB', 'letter-spacing="3"')}`),
    otaku: svg(`<circle cx="300" cy="290" r="200" fill="#FF5E7E"/><path d="M160 300c60-90 220-90 280 0" stroke="#1B1B2F" stroke-width="18" fill="none" stroke-linecap="round"/><circle cx="235" cy="270" r="20" fill="#1B1B2F"/><circle cx="365" cy="270" r="20" fill="#1B1B2F"/>${T(300, 560, 52, '#FF5E7E', 'CAIRO OTAKU', 'letter-spacing="4"')}`),
    pixel: svg(`${[...Array(8)].map((_, i) => `<rect x="${120 + i * 45}" y="${160 + (i % 3) * 45}" width="40" height="40" fill="${['#7CFC00', '#00E5FF', '#FF00E5'][i % 3]}"/>`).join('')}${T(300, 420, 64, '#FFFFFF', 'GG NO RE')}${T(300, 480, 28, '#7CFC00', 'PRESS START', 'letter-spacing="8"')}`),
    retro: svg(`${['#E4572E', '#F2C14E', '#17795A', '#1C2541'].map((c, i) => `<rect x="80" y="${150 + i * 50}" width="440" height="36" rx="18" fill="${c}"/>`).join('')}${T(300, 470, 60, '#F2C14E', 'RETRO 90')}`),
    habibi: svg(`<path d="M300 520C140 410 90 330 90 250c0-70 55-120 115-120 45 0 75 25 95 60 20-35 50-60 95-60 60 0 115 50 115 120 0 80-50 160-210 270z" fill="#E4572E"/>${T(300, 300, 74, '#FFFFFF', 'يا حبيبي', 'font-family="Cairo, Tahoma, Arial"')}`),
    lift: svg(`<rect x="120" y="270" width="360" height="22" rx="11" fill="#FFFFFF"/><rect x="90" y="215" width="40" height="130" rx="8" fill="#F2C14E"/><rect x="470" y="215" width="40" height="130" rx="8" fill="#F2C14E"/>${T(300, 180, 62, '#FFFFFF', 'NO DAYS OFF')}${T(300, 440, 40, '#F2C14E', 'HELIOPOLIS IRON', 'letter-spacing="3"')}`),
  };

  const sellers = [
    { id: 'sl-koora', accountId: 'acc-s1', handle: 'koora-ink', name: 'Koora Ink', owner: 'Omar Hany', email: 'omar@kooraink.example', phone: '+20 101 220 7781', avatar: 2, mode: 'pod',
      bio: 'Football streetwear drawn in Cairo — five-a-side culture, terrace chants and late-night games.', cover: 'photo-1489944440615-453fc2b6a9a9', history: { sales: 236, revenue: 141600, profit: 47200, rating: 4.8, reviews: 118 }, joinedDays: 110 },
    { id: 'sl-nile', accountId: 'acc-s2', handle: 'nile-lines', name: 'Nile Lines', owner: 'Salma Nabil', email: 'salma@nilelines.example', phone: '+20 112 903 4410', avatar: 11, mode: 'both',
      bio: 'Egyptian phrases, retro posters and gym tees. We also sell our own printed tote bags.', cover: 'photo-1719659018185-8a239c35fb4a', history: { sales: 61, revenue: 30500, profit: 9800, rating: 4.5, reviews: 33 }, joinedDays: 75 },
    { id: 'sl-otaku', accountId: 'acc-s3', handle: 'cairo-otaku', name: 'Cairo Otaku', owner: 'Nour Khaled', email: 'nour@cairootaku.example', phone: '+20 128 330 1902', avatar: 8, mode: 'pod',
      bio: 'Original anime-style and gaming designs. No copied characters — everything drawn by us.', cover: 'photo-1782974396379-c86aa9cf3c1c', history: { sales: 12, revenue: 6600, profit: 1900, rating: 4.3, reviews: 7 }, joinedDays: 30 },
  ];

  /** Demo designs: [sellerId, art, title, category, catalogId, colours, price, tags, description, status = live] */
  const designs = [
    ['sl-koora', 'nights', 'Cairo Nights FC', 'football', 'pod-oversized', ['Black', 'Washed Blue'], 520, ['five-a-side', 'cairo', 'night games'], 'For the crews that only play after 11 PM.'],
    ['sl-koora', 'press', 'Keep Calm & Press High', 'football', 'pod-tee', ['Black', 'Navy', 'White'], 420, ['tactics', 'pressing', 'funny'], 'A tee for the midfielder who never stops running.'],
    ['sl-koora', 'keeper', 'Safe Hands No.1', 'football', 'pod-hoodie', ['Navy', 'Black'], 890, ['goalkeeper', 'keeper'], 'Every team needs one. Gift it to yours.'],
    ['sl-koora', 'yalla', 'Yalla Nel3ab', 'egyptian', 'pod-tee', ['Bottle Green', 'White', 'Black'], 400, ['arabic', 'football', 'yalla'], 'The two words that start every game in Egypt.'],
    ['sl-koora', 'nights', 'Cairo Nights FC Cap', 'football', 'pod-cap', ['Black', 'Navy'], 320, ['cap', 'cairo'], 'Match the crew — the Cairo Nights badge on a cap.'],
    ['sl-nile', 'habibi', 'Ya Habibi', 'egyptian', 'pod-oversized', ['Off-white', 'Black'], 480, ['arabic', 'love', 'gift'], 'Big heart, bigger fit.'],
    ['sl-nile', 'pyramids', 'Giza Run Club', 'sports', 'pod-tee', ['Sand', 'White', 'Black'], 390, ['running', 'giza', 'club'], 'Sunrise runs with a view nobody else has.'],
    ['sl-nile', 'retro', 'Retro 90', 'retro', 'pod-hoodie', ['Off-white', 'Black'], 820, ['90s', 'vintage', 'stripes'], 'Four stripes, one decade.'],
    ['sl-nile', 'lift', 'No Days Off', 'motivation', 'pod-tee', ['Black', 'Heather Grey'], 380, ['gym', 'lifting'], 'For the 6 AM gym crowd.'],
    ['sl-otaku', 'otaku', 'Cairo Otaku', 'anime', 'pod-oversized', ['Black', 'Off-white'], 500, ['anime', 'kawaii'], 'An original character drawn in Cairo.'],
    ['sl-otaku', 'pixel', 'GG No Re', 'gaming', 'pod-tee', ['Black', 'Navy'], 380, ['gaming', 'pixel', 'retro'], 'Good game. No rematch.'],
    ['sl-otaku', 'pixel', 'Press Start Hoodie', 'gaming', 'pod-hoodie', ['Black', 'Navy'], 780, ['gaming', 'pixel'], 'Pixel art for the late-night gaming crew.', 'review'],
    ['sl-nile', 'habibi', 'Ya Habibi Cap', 'egyptian', 'pod-cap', ['White', 'Black'], 300, ['arabic', 'cap'], 'The heart, now on a cap.', 'review'],
  ];

  SA.data.pod = { catalog, categories, levels, sellers, designs, art, svg };
})(window.SA);
