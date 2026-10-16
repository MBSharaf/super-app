/** Arabic dictionary — part 10: gaps found by the front-end QA sweep (docs/PLAN-qa.md). */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    'Browse the shop': 'اتفرج على المتجر', 'Nothing saved yet': 'لسه مفيش حاجة محفوظة', 'Tap the heart on any product to save it for later.': 'دوس على القلب في أي منتج علشان تحفظه لبعدين.',
    'No upcoming bookings': 'مفيش حجوزات جاية', 'Match of the day': 'ماتش اليوم', 'Second preliminary round': 'الدور التمهيدي التاني',
    'Designs, products, orders and your seller wallet are in the seller dashboard.': 'التصميمات والمنتجات والطلبات ومحفظة البائع موجودين في لوحة البائع.',
    '(up to 3, separated by commas)': '(لحد 3، بينهم فواصل)', '(separated by commas — leave empty for one size)': '(بينهم فواصل — سيبها فاضية لو مقاس واحد)',
    'night games': 'ماتشات بالليل', 'the team': 'الفريق', '{0} members': '{0} عضو', '{0} posts': '{0} بوست', '{0} post': 'بوست واحد',
    'Nothing matches that': 'مفيش حاجة مطابقة', 'Try a broader search or clear your filters.': 'جرّب بحث أوسع أو امسح الفلاتر.', 'No sellers found.': 'مفيش بائعين.', 'No live matches right now': 'مفيش ماتشات شغالة دلوقتي', 'No matches on this day': 'مفيش ماتشات في اليوم ده',
    'Pick another day or competition — there’s football almost every day.': 'اختار يوم أو بطولة تانية — فيه كورة تقريباً كل يوم.',
    'Top 2 qualify for the CAF Champions League. Bottom 3 are relegated. Results are simulated for this demo.': 'أول اتنين يتأهلوا لدوري أبطال أفريقيا. وآخر 3 يهبطوا. النتايج تجريبية في النسخة دي.',
    'Pro sellers are reviewed first — usually within a few hours.': 'بائعين Pro بيتراجعوا الأول — عادة في خلال كام ساعة.',
    'Your own products.': 'منتجاتك إنت.', 'Your printed designs.': 'تصميماتك المطبوعة.', 'Printed designs below; your own products are in “Store money”.': 'التصميمات المطبوعة تحت؛ ومنتجاتك إنت في "فلوس المتجر".',
    'The PDF lists every order — printed or your own product — and every withdrawal.': 'الـ PDF فيه كل طلب — مطبوع أو منتج بتاعك — وكل سحب.',
  });
  D.patterns.push(
    ['^(.+) signed in to the seller dashboard$', '{1} دخل لوحة البائع'],
    ['^New booking at (.+)$', 'حجز جديد في {1}'], ['^New weekly package at (.+)$', 'باقة أسبوعية جديدة في {1}'],
    ['^(.+) · (.+) · paid in the app$', '{1} · {t2} · مدفوع في التطبيق'],
    ['^(.+) · (\\d+) games, every (\\w+) at (.+) · one QR for the whole package$', '{1} · {2} ماتشات، كل {t3} الساعة {t4} · QR واحد للباقة كلها'],
    ['^Your store “(.+)” is open$', 'متجرك “{1}” اتفتح'],
    ['^(\\d+)% of slots$', '{1}% من المواعيد'], ['^(\\d+)% of posted slots$', '{1}% من المواعيد اللي اتنشرت'],
    ['^([+−])([\\d,]+) EGP balance (credit|adjustment)$', '{1}{2} ج.م {t3} رصيد'],
    ['^([\\d,]+) EGP · (\\d+) refused \\(cost you nothing\\)$', '{1} ج.م · {2} مرفوض (مكلّفكش حاجة)'],
    ['^The factory prints and ships every order — nothing to do on your side\\. Profit is released (\\d+) days after delivery\\. Refused deliveries cost you nothing\\.$', 'المصنع بيطبع ويشحن كل طلب — مفيش عليك حاجة. الربح بينزل بعد {1} أيام من التوصيل. والطلبات المرفوضة مش بتكلفك حاجة.'],
    ['^(.+) is in review$', '{1} بيتراجع'], ['^“(.+)” is in review — we’ll notify you$', '“{1}” بيتراجع — هنبلّغك'],
    ['^Knockout cups need 4, 8 or 16 teams to draw \\(you have (\\d+)\\)\\. Drawing fixtures closes registration and tells every team when they play; unused reserved hours are released back to bookings\\.$', 'بطولات خروج المغلوب محتاجة 4 أو 8 أو 16 فريق علشان القرعة (عندك {1}). القرعة بتقفل التسجيل وبتبلّغ كل فريق بمواعيده؛ والساعات المحجوزة اللي مش هتتستخدم بترجع للحجز.'],
    ['^Leagues need at least 3 teams \\(you have (\\d+)\\)\\. Drawing fixtures closes registration and tells every team when they play; unused reserved hours are released back to bookings\\.$', 'الدوري محتاج 3 فرق على الأقل (عندك {1}). القرعة بتقفل التسجيل وبتبلّغ كل فريق بمواعيده؛ والساعات المحجوزة اللي مش هتتستخدم بترجع للحجز.'],
    ['^Fixtures are drawn when registration closes \\((.+?)\\)\\. Match days are already reserved: (.+)\\.$', 'القرعة بتتعمل لما التسجيل يقفل ({t1}). أيام الماتشات محجوزة خلاص: {t2}.'],
    ['^Fixtures are drawn when registration closes\\. Match days are already reserved: (.+)\\.$', 'القرعة بتتعمل لما التسجيل يقفل. أيام الماتشات محجوزة خلاص: {t1}.'],
    ['^(\\w{3} \\d{1,2} \\w{3}) from (\\d{1,2}(?::\\d\\d)? [AP]M)$', '{t1} من {t2}'],
  );
  D.strings.credit = D.strings.credit || 'إضافة'; D.strings.adjustment = D.strings.adjustment || 'تعديل';
})(window.SA);
