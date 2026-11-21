/** Arabic dictionary — each pitch's payment mode (deposit or full payment in the app) and the admin's pitch accounts (2026-11-21). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    /* ---------- Partner: pitch form ---------- */
    'How players pay': 'اللاعيبة بيدفعوا إزاي',
    'Choose how players pay for this pitch. Every booking keeps the way it was paid; a change applies to new bookings only.': 'اختار اللاعيبة يدفعوا إزاي في الملعب ده. كل حجز بيفضل على الطريقة اللي اتدفع بيها؛ والتغيير بيتطبق على الحجوزات الجديدة بس.',
    'Deposit in the app, the rest at the pitch': 'عربون في التطبيق، والباقي في الملعب',
    'Players pay a deposit online to hold the pitch (20% of the total, at least 100 EGP). You collect the rest in cash when you scan their QR code.': 'اللاعيبة بيدفعوا عربون أونلاين علشان يحجزوا الملعب (20% من الإجمالي، وبحد أدنى 100 ج.م). وإنت بتحصّل الباقي كاش لما تعمل scan للـ QR بتاعهم.',
    'Full payment in the app': 'الدفع كامل في التطبيق',
    'Players pay everything online. Nothing to collect at the pitch. Super Malaab pays your share, less the commission, to your balance after the match.': 'اللاعيبة بيدفعوا كل حاجة أونلاين. مفيش حاجة تحصّلها في الملعب. وسوبر ملعب بينزّل نصيبك، بعد خصم العمولة، في رصيدك بعد الماتش.',
    /* ---------- Pitch page ---------- */
    'How you pay': 'هتدفع إزاي',
    'Full payment in the app.': 'الدفع كامل في التطبيق.',
    'You pay the whole amount online by card when you book. Nothing to pay at the pitch — just show your QR code.': 'بتدفع المبلغ كله أونلاين بالكارت وإنت بتحجز. مفيش حاجة تدفعها في الملعب — بس ورّي الـ QR بتاعك.',
    'Deposit in the app, the rest at the pitch.': 'عربون في التطبيق، والباقي في الملعب.',
    'You pay a deposit online to hold the pitch, and the rest to the venue when they scan your QR code.': 'بتدفع عربون أونلاين علشان تحجز الملعب، والباقي لصاحب الملعب لما يعمل scan للـ QR بتاعك.',
    /* ---------- Checkout and booking page ---------- */
    'Review and pay': 'راجع وادفع', 'Pay now:': 'ادفع دلوقتي:', 'Nothing to pay at the pitch.': 'مفيش حاجة تدفعها في الملعب.',
    'You pay the full amount online by card. At the pitch, just show your QR code.': 'بتدفع المبلغ كامل أونلاين بالكارت. وفي الملعب، بس ورّي الـ QR بتاعك.',
    'Pay now — full amount': 'ادفع دلوقتي — المبلغ كامل', 'Nothing to pay at the pitch': 'مفيش حاجة تدفعها في الملعب',
    'Paid in full in the app — nothing to pay at the pitch.': 'اتدفع كامل في التطبيق — مفيش حاجة تدفعها في الملعب.',
    'now.': 'دلوقتي.', 'Refunds go straight to your wallet.': 'أي استرداد بيرجع على محفظتك على طول.', 'Paid in full': 'اتدفع كامل',
    'Pay': 'ادفع', 'Booking': 'حجز', 'Weekly booking × {0}': 'حجز أسبوعي × {0}',
    /* ---------- Notifications ---------- */
    '{0} {1} paid in full': 'اتدفع {0} {1} كامل', 'nothing to pay at the pitch': 'مفيش حاجة تدفعها في الملعب',
    'paid in full in the app ({0} {1})': 'اتدفع كامل في التطبيق ({0} {1})', 'nothing to collect': 'مفيش حاجة تحصّلها',
    'Super Malaab pays your share after the match': 'سوبر ملعب بيدفعلك نصيبك بعد الماتش',
    'New bookings follow it. Bookings already made keep the way they were paid.': 'الحجوزات الجديدة بتمشي عليها. والحجوزات القديمة بتفضل على الطريقة اللي اتدفعت بيها.',
    /* ---------- Admin: pitch accounts ---------- */
    'Pitch accounts': 'حسابات الملاعب', 'Pitch accounts — Super Malaab Admin': 'حسابات الملاعب — إدارة سوبر ملعب',
    'How each pitch is paid, and what Super Malaab owes each venue': 'كل ملعب بيتدفع إزاي، وسوبر ملعب عليه كام لكل ملعب',
    'Booked value': 'قيمة الحجوزات', 'Paid in the app': 'اتدفع في التطبيق', 'Reached Super Malaab': 'وصل لسوبر ملعب', 'Cash at the pitch': 'كاش في الملعب',
    '{0} {1} still to collect': 'لسه {0} {1} هيتحصّلوا', 'Owed to venues now': 'المستحق للملاعب دلوقتي', 'Our income {0} {1}': 'دخلنا {0} {1}',
    '{0} bookings': '{0} حجز', 'How it works': 'بتشتغل إزاي', 'Search a partner or pitch': 'دوّر على شريك أو ملعب', 'Search': 'بحث',
    'Deposit + rest at the pitch': 'عربون + الباقي في الملعب',
    'The player pays a deposit in the app and the rest in cash at the pitch. The venue keeps the cash; Super Malaab pays the venue the deposit’s pitch part, less the commission.': 'اللاعب بيدفع عربون في التطبيق والباقي كاش في الملعب. الملعب بياخد الكاش؛ وسوبر ملعب بيدفع للملعب نصيبه من العربون بعد خصم العمولة.',
    'The player pays everything in the app. Super Malaab holds it until the match is played, then moves the venue’s share, less the commission, to its balance for withdrawal.': 'اللاعب بيدفع كل حاجة في التطبيق. سوبر ملعب بيمسك الفلوس لحد ما الماتش يتلعب، وبعدين بينزّل نصيب الملعب، بعد خصم العمولة، في رصيده علشان يسحبه.',
    '{0} pitches with bookings in this period · {1} take the full payment in the app.': '{0} ملعب عندهم حجوزات في الفترة دي · {1} منهم بياخدوا الدفع كامل في التطبيق.',
    'By pitch': 'حسب الملعب', 'Super Malaab income': 'دخل سوبر ملعب', 'Venue share (app)': 'نصيب الملعب (من التطبيق)', '{0} {1} to collect': '{0} {1} هيتحصّلوا',
    'No bookings in this period.': 'مفيش حجوزات في الفترة دي.', 'Loading…': 'جاري التحميل…', 'Total': 'الإجمالي',
    'By partner — what we owe today': 'حسب الشريك — علينا كام النهارده', 'Partner': 'الشريك', 'Held until the match': 'محجوز لحد الماتش', 'Available': 'متاح',
    'In payout': 'في السحب', 'Paid out': 'اتدفع', 'Owed now': 'المستحق دلوقتي', 'pitch': 'ملعب', 'pitches': 'ملاعب', 'No partners in this period.': 'مفيش شركاء في الفترة دي.',
    'Held money moves to Available after the match. Partners withdraw Available money from their dashboard; approve it in': 'الفلوس المحجوزة بتتحول لمتاحة بعد الماتش. الشركاء بيسحبوا الفلوس المتاحة من الداشبورد بتاعهم؛ ووافق عليها من',
    'Withdrawals': 'السحب', 'Export Excel': 'تصدير Excel',
  });
  D.patterns.push(
    ['^(.+), (.+) at (\\d+(?::\\d+)? [AP]M)\\. Paid in full: ([\\d,]+) EGP\\. Nothing to pay at the pitch\\.$', '{1}، {t2} الساعة {t3}. اتدفع كامل: {4} ج.م. مفيش حاجة تدفعها في الملعب.'],
    ['^Booking — (.+)$', 'حجز — {1}'],
    ['^(.+) now takes full payment in the app$', '{1} بقى بياخد الدفع كامل في التطبيق'],
    ['^(.+) now takes deposit in the app, the rest at the pitch$', '{1} بقى بياخد عربون في التطبيق والباقي في الملعب'],
    ['^(.+): players now pay by full payment in the app$', '{1}: اللاعيبة بقوا بيدفعوا كامل في التطبيق'],
    ['^(.+): players now pay by deposit in the app, the rest at the pitch$', '{1}: اللاعيبة بقوا بيدفعوا عربون في التطبيق والباقي في الملعب'],
    ['^(\\d+) pitch(?:es)?$', '{1} ملعب'],
  );
})(window.SA);
