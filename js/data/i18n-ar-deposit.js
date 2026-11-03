/** Arabic dictionary — pitch deposits, QR check-in time left, 1 Super Point per EGP and the points pop-up (2026-11-01). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    /* ---------- Checkout: deposit ---------- */
    'Review and pay the deposit': 'راجع وادفع العربون', 'Pay now (deposit):': 'ادفع دلوقتي (عربون):', 'Pay at the pitch:': 'الباقي في الملعب:',
    'You pay only a deposit in the app to hold the pitch. The rest is paid to the venue at the pitch when they scan your QR code.': 'إنت بتدفع عربون بس في التطبيق علشان تحجز الملعب. والباقي بتدفعه لصاحب الملعب في الملعب لما يعمل scan للـ QR بتاعك.',
    'You pay only a deposit online by card to hold the pitch. The rest is paid to the venue at the pitch when they scan your QR code.': 'إنت بتدفع عربون بس أونلاين بالكارت علشان تحجز الملعب. والباقي بتدفعه لصاحب الملعب في الملعب لما يعمل scan للـ QR بتاعك.',
    'Pay online by card': 'ادفع أونلاين بالكارت', 'Pitch bookings are paid online by card only.': 'حجز الملاعب بيتدفع أونلاين بالكارت بس.',
    'Pay now — deposit': 'ادفع دلوقتي — العربون', '{0} of the total, at least {1} {2}': '{0} من الإجمالي، وبحد أدنى {1} {2}', '{0} of each game, at least {1} {2}': '{0} من كل ماتش، وبحد أدنى {1} {2}',
    'Pay at the pitch': 'ادفع في الملعب', 'No online payment for the rest — you pay it at the pitch.': 'الباقي مش بيتدفع أونلاين — بتدفعه في الملعب.',
    'No online payment for the rest — you pay it at the pitch each week.': 'الباقي مش بيتدفع أونلاين — بتدفعه في الملعب كل أسبوع.',
    'You’ll earn': 'هتكسب', '({0} now for the deposit and {1} when you pay the rest at the pitch).': '({0} دلوقتي على العربون و{1} لما تدفع الباقي في الملعب).',
    '{0} point for every {1} {2} you pay.': '{0} نقطة على كل {1} {2} بتدفعه.', 'Refunds of your deposit go straight to your wallet.': 'لو العربون اترد، بيرجع على محفظتك على طول.',
    'Applies to each game separately.': 'بتتطبق على كل ماتش لوحده.', 'Pay deposit {0} {1}': 'ادفع العربون {0} {1}',
    /* ---------- Booking page ---------- */
    'Show the QR code to the pitch owner when you arrive — they scan it and your time starts.': 'ورّي الـ QR لصاحب الملعب أول ما توصل — هيعمله scan ووقتك يبدأ.',
    'Deposit paid': 'العربون المدفوع', 'Deposits paid': 'العرابين المدفوعة', 'of {0} {1}': 'من {0} {1}', 'of {0} {1} for the package': 'من {0} {1} للباقة',
    'Paid at the pitch': 'اتدفع في الملعب', 'Paid to the venue at check-in': 'اتدفع لصاحب الملعب وقت تسجيل الحضور', 'Not due — cancelled': 'مش مطلوب — الحجز اتلغى',
    'Cash to the venue when they scan your QR': 'كاش لصاحب الملعب لما يعمل scan للـ QR بتاعك',
    'Show this to the pitch owner when you arrive — they scan it and your time starts.': 'ورّي الكود ده لصاحب الملعب أول ما توصل — هيعمله scan ووقتك يبدأ.',
    'Your booking ends at {0} {1} even if you arrive late.': 'حجزك بيخلص الساعة {0} {1} حتى لو وصلت متأخر.', 'Pay the rest at the pitch:': 'ادفع الباقي في الملعب:',
    'If the camera can’t read it, staff can type your booking ID': 'لو الكاميرا مقرتهوش، صاحب الملعب يقدر يكتب رقم الحجز',
    'min left — your booking ends at {0} {1}.': 'دقيقة فاضلة — حجزك بيخلص الساعة {0} {1}.',
    'You’ll get your full deposit, {0} {1}, back in your wallet.': 'هيرجعلك العربون كله، {0} {1}، على محفظتك.', 'You’ll get your full {0} {1}, back in your wallet.': 'هيرجعلك المبلغ كله، {0} {1}، على محفظتك.',
    /* ---------- Partner check-in ---------- */
    'Deposit paid in the app:': 'العربون اللي اتدفع في التطبيق:', 'Collected at the pitch:': 'اتحصّل في الملعب:', 'To collect at the pitch:': 'هتحصّله في الملعب:', 'Nothing left to pay': 'مفيش حاجة باقية',
    'Deposit paid in the app': 'العربون اللي اتدفع في التطبيق', 'Collected at the pitch': 'اتحصّل في الملعب', 'To collect at the pitch': 'هتحصّله في الملعب',
    'Time left': 'الوقت الباقي', 'min left': 'دقيقة فاضلة', 'ends {0} {1}': 'بيخلص {0} {1}', '{0} min late': 'متأخر {0} دقيقة', 'Collect from the customer now:': 'حصّل من العميل دلوقتي:',
    'Check in & collect {0} {1}': 'سجّل الحضور وحصّل {0} {1}', 'Check in anyway & collect {0} {1}': 'سجّل الحضور برضه وحصّل {0} {1}',
    '{0} min late — {1} min left of the booking. It still ends at {2} {3}.': 'متأخر {0} دقيقة — فاضل {1} دقيقة من الحجز. وبرضه بيخلص الساعة {2} {3}.',
    '{0} min left — the booking ends at {1} {2}.': 'فاضل {0} دقيقة — الحجز بيخلص الساعة {1} {2}.',
    'Kick-off in {0} min. The booking runs {1} {2} – {3} {4}, {5} min.': 'الماتش يبدأ كمان {0} دقيقة. الحجز من {1} {2} لـ {3} {4}، {5} دقيقة.',
    'Already checked in at {0} {1}.': 'اتسجل حضوره الساعة {0} {1}.', '{0} min left, ends {1} {2}': 'فاضل {0} دقيقة، بيخلص {1} {2}', '{0} {1} collected': 'اتحصّل {0} {1}',
    'Deposit in the app (Card)': 'عربون في التطبيق (كارت)', 'Deposit in the app (Wallet)': 'عربون في التطبيق (المحفظة)',
    'The customer paid a deposit in the app; the deposit’s pitch part ({0} {1}) less the commission goes to your wallet. The rest ({2} {3}) is paid to you at the pitch. Collect it when you scan their QR at check-in.': 'العميل دفع عربون في التطبيق؛ نصيب الملعب من العربون ({0} {1}) بعد خصم العمولة بينزل في محفظتك. والباقي ({2} {3}) بيدفعهولك في الملعب. حصّله لما تعمل scan للـ QR وقت تسجيل الحضور.',
    /* ---------- Notifications ---------- */
    'deposit {0} {1} paid in the app': 'عربون {0} {1} اتدفع في التطبيق', 'collect {0} {1} at the pitch': 'حصّل {0} {1} في الملعب',
    'deposits {0} {1} paid in the app, the rest at the pitch each week': 'العرابين {0} {1} اتدفعت في التطبيق، والباقي في الملعب كل أسبوع',
    '{0} min left — your booking ends at {1} {2}. You paid {3} {4} at the pitch.': 'فاضل {0} دقيقة — حجزك بيخلص الساعة {1} {2}. دفعت {3} {4} في الملعب.',
    '{0} min left — your booking ends at {1} {2}.': 'فاضل {0} دقيقة — حجزك بيخلص الساعة {1} {2}.',
    /* ---------- Super Points ---------- */
    'Congratulations!': 'مبروك!', 'You earned': 'كسبت', 'Super Points earned': 'كسبت Super Points', 'Your balance:': 'رصيدك:', 'points': 'نقطة', 'See my points': 'شوف نقطك', 'Great!': 'تمام!',
    '{0} point for every {1} {2} you pay. They’re in your wallet now.': '{0} نقطة على كل {1} {2} بتدفعه. النقط نزلت في محفظتك.',
    'Booking deposit': 'عربون حجز', 'Weekly booking deposit × {0}': 'عربون حجز أسبوعي × {0}', 'Shop order': 'طلب من المتجر', '{0} item': '{0} قطعة', '{0} items': '{0} قطع',
    '{0} points to Gold': 'فاضل {0} نقطة على Gold', '{0} points to Silver': 'فاضل {0} نقطة على Silver', 'Top tier — Gold member': 'أعلى مستوى — عضو Gold',
    '{0} point for every {1} you pay': 'نقطة على كل جنيه بتدفعه', '{0} point for every {1} {2} you pay': '{0} نقطة على كل {1} {2} بتدفعه',
    '— booking deposits, the rest paid at the pitch, games, the shop, tournaments and the Fantasy Pass. The points land the moment you pay.': '— عرابين الحجز، والباقي اللي بتدفعه في الملعب، والماتشات، والمتجر، والبطولات، والـ Fantasy Pass. النقط بتنزل أول ما تدفع.',
    'Bronze · Silver · Gold': 'برونزي · فضي · ذهبي',
  });
  D.patterns.push(
    ['^(.+) checked in$', 'تم تسجيل حضور {1}'], ['^You’re checked in at (.+)$', 'اتسجل حضورك في {1}'],
    ['^(.+), (.+) at (\\d+(?::\\d+)? [AP]M)\\. Deposit ([\\d,]+) EGP paid; pay ([\\d,]+) EGP at the pitch\\.$', '{1}، {t2} الساعة {t3}. اتدفع عربون {4} ج.م؛ ادفع {5} ج.م في الملعب.'],
    ['^Paid at the pitch — (.+)$', 'اتدفع في الملعب — {1}'], ['^Booking deposit — (.+)$', 'عربون حجز — {1}'],
    // Server answers in API mode (check-in scan and the points history)
    ['^Checked in\\. Have a great game!(?: Match (\\d+) of (\\d+)\\.)?(?: Collect ([\\d,]+) EGP in cash \\(deposit ([\\d,]+) EGP paid in the app\\)\\.)?(?: (\\d+) min late: (\\d+) min left, ends at (\\d+:\\d+)\\.)?$', (m) => ['تم تسجيل الحضور. ماتش موفق!',
      m[1] ? `الماتش ${m[1]} من ${m[2]}.` : '', m[3] ? `حصّل ${m[3]} ج.م كاش (العربون ${m[4]} ج.م اتدفع في التطبيق).` : '', m[5] ? `متأخر ${m[5]} دقيقة: فاضل ${m[6]} دقيقة، والحجز بيخلص الساعة ${m[7]}.` : ''].filter(Boolean).join(' ')],
    ['^Booking deposit · (.+)$', 'عربون حجز · {1}'], ['^Package deposit · (.+)$', 'عربون باكدج · {1}'], ['^Paid at the pitch · (.+)$', 'اتدفع في الملعب · {1}'],
    ['^Refund · ([\\d.,]+) EGP$', 'استرداد · {1} ج.م'],
    ['^(\\d{4}-\\d{2}-\\d{2}) at (\\d+(?::\\d+)? [AP]M) — post it to players in the community in one tap\\.$', '{1} الساعة {t2} — انشرها للاعيبة في الكوميونيتي بضغطة واحدة.'],
  );
})(window.SA);
