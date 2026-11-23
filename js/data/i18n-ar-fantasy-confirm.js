/** Arabic dictionary — confirming a fantasy team and when it starts counting (2026-11-23). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'Confirm your team': 'أكّد فريقك',
    'Check your squad and give it a name.': 'راجع تشكيلتك واختار اسم لفريقك.',
    'Players': 'اللاعيبة',
    'Squad cost': 'تمن التشكيلة',
    'Money left': 'الفلوس الباقية',
    'First gameweek': 'أول جولة',
    'Gameweek {0}': 'الجولة {0}',
    'Deadline': 'آخر ميعاد',
    'Team name': 'اسم الفريق',
    'Your points start from Gameweek {0}. Your team is entered into the Super Malaab League automatically.': 'نقطك بتبدأ من الجولة {0}. فريقك بيدخل دوري سوبر ملعب لوحده.',
    'Confirm team': 'أكّد الفريق',
    'Your team is confirmed': 'فريقك اتأكد',
    'Your team plays from Gameweek {0}.': 'فريقك بيلعب من الجولة {0}.',
    'Until then you can change players for free, pick your captain and set your lineup.': 'لحد الميعاد ده تقدر تغيّر لاعيبة ببلاش، وتختار الكابتن وترتّب التشكيلة.',
    'After the deadline your team is locked for that gameweek. Your points come from the real Premier League matches and are added automatically after each game.': 'بعد آخر ميعاد فريقك بيتقفل للجولة دي. نقطك بتيجي من ماتشات الدوري الإنجليزي الحقيقية وبتتضاف لوحدها بعد كل ماتش.',
    'This is the demo site: use Simulate to play a gameweek and see your points.': 'ده الموقع التجريبي: دوس "شغّل الجولة" علشان تلعب جولة وتشوف نقطك.',
    'Pick my captain': 'اختار الكابتن',
    'Team saved for GW{0}': 'الفريق محفوظ للجولة {0}',
    'Transfers confirmed for Gameweek {0}': 'التغييرات اتأكدت للجولة {0}',
    'Your team is confirmed and plays from GW{0}. You can still change players, captain and lineup until the deadline. Points are added automatically after the real matches.': 'فريقك اتأكد وبيلعب من الجولة {0}. لسه تقدر تغيّر اللاعيبة والكابتن والتشكيلة لحد آخر ميعاد. النقط بتتضاف لوحدها بعد الماتشات الحقيقية.',
    'Continue — name your team': 'كمّل — سمّي فريقك',
    'Squad filled — swap anyone you don’t like': 'التشكيلة اكتملت — غيّر أي حد مش عاجبك',
    'GW{0} points': 'نقط الجولة {0}',
    'Plays every GW{0} fixture and scores your team': 'بيلعب كل ماتشات الجولة {0} ويحسب نقط فريقك',
    'Tap a player to view stats, change captain or substitute. Changes save automatically until the deadline.': 'دوس على أي لاعب تشوف أرقامه أو تخليه كابتن أو تبدّله. التغييرات بتتحفظ لوحدها لحد آخر ميعاد.',
    'Your gameweeks': 'جولاتك',
    'Scores double points': 'بياخد النقط دبل',
    'Sign in to save your team — your squad is kept': 'سجّل دخول علشان تحفظ فريقك — تشكيلتك محفوظة',
    'Simulate GW{0}': 'شغّل الجولة {0}',
    'Started GW{0}': 'بدأ من الجولة {0}',
    'Pick a highlighted player to swap with': 'اختار لاعب من المتعلّم عليهم علشان تبدّل مع',
  });
})(window.SA);
