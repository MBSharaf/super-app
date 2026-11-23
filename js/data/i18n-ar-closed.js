/** Arabic dictionary — store and fantasy closed on the demo site for the Paymob review (2026-11-23). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'Fantasy is coming soon': 'الفانتازي جاية قريب',
    'We’re getting Super Malaab fantasy ready. Meanwhile, book a pitch or join a game.': 'بنجهّز فانتازي سوبر ملعب. لحد ما تبدأ، احجز ملعب أو انضم لماتش.',
    'Pitches, padel courts and players for your game — in one app.': 'ملاعب كورة وبادل ولاعيبة لماتشك — في أبلكيشن واحد.',
    'Book pitches and padel courts, fill your squad and join games — all from one account and one wallet.': 'احجز ملاعب كورة وبادل، كمّل فريقك وانضم لماتشات — كله من حساب واحد ومحفظة واحدة.',
    'Frontend demo: payments and bookings are simulated on this device.': 'نسخة تجريبية: المدفوعات والحجوزات تجريبية على الجهاز ده.',
    'players across Egypt': 'لاعب في كل مصر',
    'Hi there! I’m the Super Malaab assistant. Ask me about a booking, a match or a padel court — or pick a topic.': 'أهلاً! أنا مساعد سوبر ملعب. اسألني عن حجز أو ماتش أو ملعب بادل — أو اختار موضوع.',
  });
})(window.SA);
