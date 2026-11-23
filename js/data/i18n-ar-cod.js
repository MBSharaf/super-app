/** Arabic dictionary — store paid by cash on delivery, 50% deposit for pitches, padel courts in the partner application (2026-11-23). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'Pay the courier when your order arrives': 'ادفع للمندوب لما طلبك يوصل',
    '20% deposit in the app, the rest at the pitch': 'عربون 20% في الأبلكيشن، والباقي في الملعب',
    '50% deposit in the app, the rest at the pitch': 'عربون 50% في الأبلكيشن، والباقي في الملعب',
    'Players pay half online to hold the pitch (at least 100 EGP). You collect the other half in cash when you scan their QR code.': 'اللاعيبة بيدفعوا نص المبلغ أونلاين عشان يحجزوا الملعب (100 جنيه على الأقل). وإنت بتحصّل النص التاني كاش لما تمسح الـ QR بتاعهم.',
    '50% deposit in the app, the rest at the pitch.': 'عربون 50% في الأبلكيشن، والباقي في الملعب.',
    'You pay half online to hold the pitch, and the other half to the venue when they scan your QR code.': 'بتدفع نص المبلغ أونلاين عشان تحجز الملعب، والنص التاني للملعب لما يمسحوا الـ QR بتاعك.',
    '50% deposit + rest at the pitch': 'عربون 50% + الباقي في الملعب',
    'Pitch types (football and padel)': 'نوع الملاعب (كورة وبادل)',
    'Padel court': 'ملعب بادل',
    'padel court': 'ملعب بادل',
  });
})(window.SA);
