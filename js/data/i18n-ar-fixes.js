/** Arabic dictionary — part 8: the site-wide fixes round (docs/PLAN-fixes.md). */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- M1 weekly package ---------- */
    'Package paid': 'مدفوع للباقة', 'One QR for the whole package': 'QR واحد للباقة كلها', 'Package used up': 'الباقة خلصت', 'Your weekly package': 'باقتك الأسبوعية',
    'Played': 'اتلعب', 'Missed': 'غاب', 'Left': 'فاضل', 'In the package': 'في الباقة', 'Missed — counted': 'غاب — اتحسب', 'Excused': 'متسامح فيه', 'Upcoming': 'جاي', 'Make-up': 'تعويضي',
    'A game you don’t come to counts as used. Can’t make it? Cancel it before kick-off from its page.': 'الماتش اللي متجيش له بيتحسب من الباقة. مش هتقدر تيجي؟ الغيه قبل ميعاده من صفحته.',
    'One missed game was excused by Super App — a make-up game was added.': 'Super App سامحت في ماتش فاتك — واتضاف ماتش تعويضي.',
    'Package invoice (PDF)': 'فاتورة الباقة (PDF)', 'Weekly packages': 'الباقات الأسبوعية', 'Scanned': 'اتمسح', 'Exception': 'الاستثناء', 'Available': 'متاح', 'Used': 'اتستخدم', 'Used up': 'خلصت',
    'Excuse a missed game': 'سامح في ماتش فات', 'Missed game': 'الماتش اللي فات', 'What did you agree with the pitch manager?': 'اتفقت على إيه مع مدير الملعب؟', 'Excuse the game': 'سامح في الماتش',
    'Only once per package. The game won’t count, and the customer gets a make-up game at the same time after the package ends. The customer and the pitch manager are told.': 'مرة واحدة بس في كل باقة. الماتش مش هيتحسب، والعميل هياخد ماتش تعويضي في نفس الميعاد بعد ما الباقة تخلص. العميل ومدير الملعب هيعرفوا.',
    'One QR code per package: it works once per game, then it expires. A game the customer doesn’t come to counts as used. One missed game per package can be excused after you agree it with the pitch manager — the customer gets a make-up game.': 'QR واحد لكل باقة: بيشتغل مرة لكل ماتش، وبعدها بيخلص. الماتش اللي العميل ميجيش له بيتحسب. ماتش واحد بس في كل باقة ممكن يتسامح فيه بعد الاتفاق مع مدير الملعب — والعميل بياخد ماتش تعويضي.',
    'Package used up': 'الباقة خلصت', 'This package already used its one exception.': 'الباقة دي استخدمت الاستثناء بتاعها خلاص.', 'Choose a missed game.': 'اختار ماتش فات.', 'Write what you agreed with the pitch manager.': 'اكتب اتفقت على إيه مع مدير الملعب.',
    'A missed game was excused': 'اتسامح في ماتش فاتك',
    /* ---------- M2 / M10 receipts ---------- */
    'Tip: arrive 10 minutes early, and tell the host if you can’t make it.': 'نصيحة: تعالى قبلها بـ 10 دقايق، وبلّغ صاحب الماتش لو مش هتقدر تيجي.',
    'Payments & receipts': 'المدفوعات والإيصالات', 'Every payment, refund and top-up — download a PDF for any of them': 'كل دفعة واسترجاع وشحن — نزّل PDF لأي واحدة فيهم',
    'No payments yet': 'لسه مفيش مدفوعات', 'Your receipts appear here after your first payment.': 'إيصالاتك هتظهر هنا بعد أول دفعة.', 'Receipt (PDF)': 'الإيصال (PDF)', 'Download the receipt as PDF': 'نزّل الإيصال PDF',
    /* ---------- M8 points → vouchers ---------- */
    'in vouchers': 'كقسايم', 'Get a voucher': 'اعمل قسيمة', 'Turn points into a voucher': 'حوّل النقاط لقسيمة', 'Points to use': 'النقاط اللي هتستخدمها', 'Create voucher': 'اعمل القسيمة', 'Your vouchers': 'قسايمك',
    'Ready': 'جاهزة', 'Super Points voucher': 'قسيمة نقاط سوبر', 'Don’t use a voucher': 'من غير قسيمة', 'It pays first; the rest is paid with the method above.': 'القسيمة بتدفع الأول، والباقي بطريقة الدفع اللي فوق.', 'Smallest voucher (points)': 'أقل قسيمة (نقاط)',
    'This voucher has no value left.': 'القسيمة دي خلص رصيدها.',
  });
  D.patterns.push(
    ['^(Payment|Refund|Top-up|Voucher) of (.+) recorded$', '{t1} {2} اتسجلت'], ['^Top-up$', 'شحن'], ['^Payment$', 'دفعة'], ['^Refund$', 'استرجاع'], ['^Voucher$', 'قسيمة'],
    ['^as a voucher, from ([\\d,]+) points \\((.+)\\)\\. Use it on bookings, games, the shop, printed designs, tournaments and the Fantasy Pass — points can’t be turned into cash\\.$', 'كقسيمة، من {1} نقطة ({2}). استخدمها في الحجوزات والماتشات والمتجر ومنتجات الطباعة والبطولات و Fantasy Pass — النقاط مبتتحولش لفلوس.'],
    ['^(.+) left of (.+)$', 'فاضل {1} من {2}'], ['^Used — (.+)$', 'اتستخدمت — {1}'], ['^(SPV-\\S+) — (.+) left$', '{1} — فاضل {2}'], ['^Voucher (SPV-\\S+) — (\\d+) EGP$', 'قسيمة {1} — {2} جنيه'],
    ['^([\\d,]+) points available · (\\d+) points = 1 EGP · vouchers only, no cash$', '{1} نقطة متاحة · {2} نقطة = 1 جنيه · قسايم بس، مفيش كاش'], ['^= (.+) voucher$', '= قسيمة {1}'],
    ['^Voucher (SPV-\\S+) — (.+) ready to use at checkout$', 'القسيمة {1} — {2} جاهزة تستخدمها وانت بتدفع'], ['^The smallest voucher is (\\d+) EGP \\(([\\d,]+) points\\)\\.$', 'أقل قسيمة {1} جنيه ({2} نقطة).'],
    ['^Super Points voucher \\(([\\d,]+) points\\)$', 'قسيمة نقاط سوبر ({1} نقطة)'], ['^(\\d+) points = 1 EGP$', '{1} نقطة = 1 جنيه'],
    ['^Show it at reception every week\\. It works for (\\d+) games, then it expires\\. If the camera can’t read it, staff can type$', 'وريه في الاستقبال كل أسبوع. بيشتغل لـ {1} ماتش وبعدها بيخلص. لو الكاميرا مقرتهوش، الموظف يقدر يكتب'],
    ['^All (\\d+) games are used\\. The code has expired\\.$', 'كل الـ {1} ماتش اتستخدموا. الكود خلص.'], ['^(\\d+) games$', '{1} ماتش'], ['^(\\d+) games · (.+) weekly discount$', '{1} ماتش · خصم أسبوعي {2}'],
    ['^Weekly package (WK-\\S+)$', 'باقة أسبوعية {1}'], ['^(\\d+) played · (\\d+) missed · (\\d+) left of (\\d+) · scanned (\\d+)×$', '{1} اتلعب · {2} غاب · {3} فاضل من {4} · اتمسح {5} مرة'],
    ['^(\\d+) games · (Active|Used up)$', '{1} ماتش · {t2}'], ['^Excuse a missed game · (WK-\\S+)$', 'سامح في ماتش فات · {1}'], ['^Game (\\d+) · (.+)$', 'ماتش {1} · {2}'], ['^Excused — make-up game on (.+)$', 'اتسامح — الماتش التعويضي يوم {1}'],
    ['^This package is used up: (\\d+) of (\\d+) games \\((\\d+) played, (\\d+) missed\\)\\. The QR code has expired\\.$', 'الباقة دي خلصت: {1} من {2} ماتش ({3} اتلعب، {4} غاب). الـ QR خلص.'],
    ['^No game in this package today\\. Next: game (\\d+) of (\\d+) on (.+) at (.+)\\.$', 'مفيش ماتش في الباقة دي النهارده. الجاي: ماتش {1} من {2} يوم {3} الساعة {4}.'],
    ['^(.+) Game (\\d+) of (\\d+) · (\\d+) used so far\\.$', '{t1} ماتش {2} من {3} · اتستخدم {4} لحد دلوقتي.'],
    ['^Your game on (.+) isn’t counted\\. Make-up game: (.+) at (.+) at (.+)\\.$', 'ماتشك يوم {1} مش هيتحسب. الماتش التعويضي: {2} الساعة {3} في {4}.'],
  );
})(window.SA);
