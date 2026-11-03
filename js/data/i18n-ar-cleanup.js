/** Arabic dictionary — free slots after tournaments, news and communities were removed from the site (2026-11-03). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'It shows as a live card on the free-slot alerts page with a Book now button, and players with alerts for this pitch or area are notified.': 'بتظهر ككارت حي في صفحة تنبيهات الساعات الفاضية فيه زرار احجز دلوقتي، واللاعيبة اللي عاملين تنبيه للملعب ده أو المنطقة بيوصلهم إشعار.',
    'Fill empty hours: post them to players as free slots': 'املا الساعات الفاضية: انزلها للاعيبة كساعات فاضية',
    'Booked from free slots': 'اتحجزت من الساعات الفاضية',
    'Post free slot': 'انزل الساعة الفاضية',
    'Book a pitch': 'احجز ملعب',
    'Stops free slots turning into ads. Only real free hours can be posted — the site checks the pitch’s calendar.': 'بيمنع الساعات الفاضية إنها تتحول لإعلانات. ينفع ينزل بس ساعات فاضية بجد — الموقع بيراجع جدول الملعب.',
    'Watch free slots: pitch partners post free hours as cards (set the daily limit, stop a venue, hide a card); players ask to join games (remove a player with a full refund); and each player’s record (block players who keep not showing up).': 'تابع الساعات الفاضية: شركاء الملاعب بينزلوا الساعات الفاضية ككروت (حدد الحد اليومي، وقّف ملعب، اخفي كارت)؛ واللاعيبة بيطلبوا يدخلوا ماتشات (شيل لاعب مع استرجاع كامل)؛ وسجل كل لاعب (احظر اللي بيغيب كتير).',
    'Seller pages need a seller account:': 'صفحات البائع محتاجة حساب بائع:',
    'Free slots & games': 'الساعات الفاضية والماتشات',
  });
  D.patterns.push(['^(\\d+) slots? posted$', 'اتنزل {1} ساعة فاضية']);
})(window.SA);
