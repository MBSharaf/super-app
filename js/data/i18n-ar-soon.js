/** Arabic dictionary — tournaments back as "coming soon" (Ramadan tournament) and communities back (2026-11-15). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'Coming soon': 'قريباً',
    'Tournaments are coming soon.': 'البطولات جاية قريب.',
    'The Ramadan tournament is on its way: venues across Egypt are preparing cups and leagues with prizes and offers. Registration opens soon — keep an eye on this page.': 'بطولة رمضان جاية: الملاعب في كل مصر بتجهّز كاسات ودوريات بجوايز وعروض. التسجيل هيفتح قريب — خليك متابع الصفحة دي.',
    'Book a pitch meanwhile': 'احجز ملعب لحد ما تبدأ',
    'Ramadan nights': 'ليالي رمضان',
    'Matches after iftar at venues near you.': 'ماتشات بعد الفطار في ملاعب قريبة منك.',
    'Cups & leagues': 'كاسات ودوريات',
    'Knockout cups and weekly leagues for 5-a-side and 7-a-side.': 'كاسات خروج المغلوب ودوريات أسبوعية للخماسي والسباعي.',
    'Prizes & offers': 'جوايز وعروض',
    'Trophies, prizes and launch offers for the first teams.': 'كاسات وجوايز وعروض افتتاح لأول الفرق.',
    'Coming up': 'جاي قريب',
    'Registration opens soon.': 'التسجيل هيفتح قريب.',
    'Coming soon · registration opens soon': 'قريباً · التسجيل هيفتح قريب',
    'The Ramadan tournament is coming soon. Registration opens soon.': 'بطولة رمضان جاية قريب. التسجيل هيفتح قريب.',
    'Tournaments are coming soon — registration isn’t open yet.': 'البطولات جاية قريب — التسجيل لسه مفتحش.',
    'Tournaments are coming soon — registration is not open yet.': 'البطولات جاية قريب — التسجيل لسه مفتحش.',
    "Tournaments are coming soon — registration isn't open yet.": 'البطولات جاية قريب — التسجيل لسه مفتحش.',
    'How do I enter a tournament?': 'أدخل بطولة إزاي؟',
    '5-a-side tournaments and leagues are coming soon to Super App — starting with the Ramadan tournament.': 'بطولات ودوريات الخماسي جاية قريب على Super App — وأولها بطولة رمضان.',
    'The rules for using Super App: accounts, bookings, the shop, fantasy and communities.': 'قواعد استخدام Super App: الحسابات والحجوزات والمتجر والفانتازي والمجتمعات.',
  });
})(window.SA);
