/** Arabic dictionary — store and fantasy closed on the demo site for the Paymob review (2026-11-23). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    'Profile photo': 'صورة البروفايل',
    'Change photo': 'غيّر الصورة',
    'JPEG, PNG or WEBP, up to 5 MB.': 'JPEG أو PNG أو WEBP، لحد 5 ميجا.',
    'Photo saved': 'الصورة اتحفظت',
    'Photo removed': 'الصورة اتشالت',
    'The photo is larger than 5 MB.': 'الصورة أكبر من 5 ميجا.',
    'Add your position, level and area from Edit profile.': 'ضيف مركزك ومستواك ومنطقتك من تعديل البروفايل.',
    'Remove': 'شيل',
    'Add an admin': 'إضافة أدمن',
    'Already has a Super Malaab account (a player, partner or seller)? Write the email they signed up with: the same account joins the team, keeps its password and gets an email. Someone new gets an email to choose a password. Everyone sets up an authenticator app at the next sign-in. Money limits start at 0 — set them on their page.': 'عنده حساب على سوبر ملعب (لاعب أو شريك أو بائع)؟ اكتب الإيميل اللي سجّل بيه: نفس الحساب هيدخل الفريق بنفس الباسورد وهيوصله إيميل. الشخص الجديد هيوصله إيميل يختار فيه باسورد. والكل هيفعّل تطبيق التحقق في الدخول الجاي. حدود الفلوس بتبدأ من 0 — حددها من صفحته.',
    'Add to the team': 'ضيفه للفريق',
    'Added to the team': 'اتضاف للفريق',
    'No admins yet. Add the first one above.': 'لسه مفيش أدمنز. ضيف أول واحد من فوق.',
    'This person is already on the team. Open their page to change their roles.': 'الشخص ده في الفريق بالفعل. افتح صفحته علشان تغيّر أدواره.',
    'Live totals': 'أرقام حقيقية لحظة بلحظة',
    'In one queue': 'في طابور واحد',
    'Hour by hour': 'ساعة بساعة',
    'Apply to sell': 'قدّم كبائع',
    'No factory is set up yet. The Super Malaab admin adds factories and their team.': 'لسه مفيش مصنع متضاف. أدمن سوبر ملعب هو اللي بيضيف المصانع وفريقها.',
    'Fantasy is coming soon': 'الفانتازي جاية قريب',
    'Live scores are coming soon': 'نتايج الماتشات جاية قريب',
    'We’re getting the match schedule ready. Meanwhile, book a pitch or join a game.': 'بنجهّز جدول الماتشات. لحد ما يبدأ، احجز ملعب أو انضم لماتش.',
    'We’re getting Super Malaab fantasy ready. Meanwhile, book a pitch or join a game.': 'بنجهّز فانتازي سوبر ملعب. لحد ما تبدأ، احجز ملعب أو انضم لماتش.',
    'Pitches, padel courts and players for your game — in one app.': 'ملاعب كورة وبادل ولاعيبة لماتشك — في أبلكيشن واحد.',
    'Book pitches and padel courts, fill your squad and join games — all from one account and one wallet.': 'احجز ملاعب كورة وبادل، كمّل فريقك وانضم لماتشات — كله من حساب واحد ومحفظة واحدة.',
    'Frontend demo: payments and bookings are simulated on this device.': 'نسخة تجريبية: المدفوعات والحجوزات تجريبية على الجهاز ده.',
    'players across Egypt': 'لاعب في كل مصر',
    'Hi there! I’m the Super Malaab assistant. Ask me about a booking, a match or a padel court — or pick a topic.': 'أهلاً! أنا مساعد سوبر ملعب. اسألني عن حجز أو ماتش أو ملعب بادل — أو اختار موضوع.',
  });
})(window.SA);
