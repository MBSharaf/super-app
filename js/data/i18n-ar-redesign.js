/** Arabic dictionary — UI refresh: mobile menu, home, storefront and fantasy tabs (2026-11-21). */
(function (SA) {
  // Adds only new keys: strings that already have a translation elsewhere keep it, so the rest of the site does not change
  const all = SA.data.i18nAr.strings;
  Object.entries({
    /* ---------- Menu ---------- */
    'Open menu': 'افتح القائمة', 'Explore': 'استكشف', 'Pitches': 'الملاعب', 'Padel': 'بادل', 'Players': 'اللاعيبة', 'Fantasy': 'فانتازي', 'Matches': 'الماتشات', 'Shop': 'المتجر', 'Cups': 'البطولات', 'Community': 'المجتمع',
    'Padel courts': 'ملاعب البادل', 'Book a court or join an Open Match': 'احجز ملعب أو ادخل Open Match',
    'Welcome to Super Malaab': 'أهلاً بيك في سوبر ملعب', 'View profile': 'صفحتك', 'Wallet': 'المحفظة', 'My activity': 'نشاطي', 'More': 'كمان',
    'Wishlist': 'المفضلة', 'Post a match': 'انشر ماتش', 'Sell on Super Malaab': 'بيع على سوبر ملعب', 'Free-slot alerts': 'تنبيهات المواعيد الفاضية',
    'Help center': 'مركز المساعدة', 'Contact support': 'كلّم الدعم', 'Partner dashboard': 'لوحة الشريك', 'Pitch owners, sellers and print partners': 'أصحاب الملاعب والبياعين وشركاء الطباعة',
    'Light mode': 'الوضع الفاتح', 'Dark mode': 'الوضع الغامق', 'My bookings': 'حجوزاتي', 'My games': 'ماتشاتي', 'Orders': 'طلباتي',

    /* ---------- Home ---------- */
    '{0} spot open': '{0} مكان فاضي', '{0} slot today': '{0} معاد فاضي النهارده', 'Sport': 'الرياضة', 'Football': 'كورة', 'Book.': 'احجز.', 'Play.': 'العب.', 'Win.': 'اكسب.',
    'Pitches, padel courts, players for your game and Premier League fantasy — in one app.': 'ملاعب كورة وبادل، لاعيبة لماتشك وفانتازي الدوري الإنجليزي — في أبلكيشن واحد.',
    'Book a pitch': 'احجز ملعب', 'Book a padel court': 'احجز ملعب بادل', 'Find a game': 'لاقي ماتش', 'Find players': 'لاقي لاعيبة',
    'Everything in Super Malaab': 'كل حاجة في سوبر ملعب', 'Pitches near you': 'ملاعب جنبك', 'Closest to {0}': 'الأقرب لـ {0}', 'Near Downtown Cairo': 'جنب وسط البلد',
    'See all': 'شوف الكل', 'Use my location': 'استخدم مكاني', 'Book 60, 90 or 120 minutes': 'احجز ٦٠ أو ٩٠ أو ١٢٠ دقيقة', 'Book {0}, {1} or {2} minutes': 'احجز {0} أو {1} أو {2} دقيقة',
    'Games that need you': 'ماتشات محتاجاك', 'Join in two taps and pay your share': 'انضم في دوستين وادفع نصيبك',
    'Short on players?': 'ناقصك لاعيبة؟', 'Post your match and players nearby join': 'انشر ماتشك واللاعيبة اللي جنبك هينضموا',
    'Your next game': 'ماتشك الجاي', 'View listing': 'شوف الإعلان', 'Details': 'التفاصيل',
    'Gameweek {0} locks in': 'الجولة {0} بتقفل بعد', 'Manage my team': 'إدارة فريقي', 'Create my team': 'اعمل فريقي', 'Free leagues': 'دوريات مجانية', 'In form this week': 'الأحسن الأسبوع ده',
    'Live & next up': 'مباشر والجاي', 'Every kick-off in Cairo time': 'كل الماتشات بتوقيت القاهرة', 'Schedule': 'الجدول',
    'Cups & leagues': 'بطولات ودوريات', 'Tournaments at pitches near you': 'بطولات في ملاعب جنبك', 'Coming soon': 'قريباً',
    'Popular in the shop': 'الأكتر طلباً في المتجر', 'Original gear, delivered across Egypt': 'منتجات أصلية، بتوصل لكل مصر', 'Shop all': 'تسوّق الكل',

    /* ---------- Shop ---------- */
    'Free delivery over {0} {1}': 'توصيل مجاني فوق {0} {1}', '100% original brands': 'ماركات أصلية ١٠٠٪', '14-day easy returns': 'استرجاع سهل خلال ١٤ يوم', 'Pay by card, wallet or on delivery': 'ادفع بالكارت أو المحفظة أو عند الاستلام',
    'New season {0}': 'موسم جديد {0}', 'Gear for every game.': 'عدّة لكل ماتش.', 'Shop boots': 'تسوّق الجزم', 'Sale': 'تخفيضات', 'Best sellers': 'الأكتر مبيعاً', 'Deals': 'عروض', 'View all': 'شوف الكل',
    'Rackets & balls': 'مضارب وكور', 'Jerseys & kits': 'تيشيرتات وأطقم', 'Shop kits': 'تسوّق الأطقم', 'All products': 'كل المنتجات', 'Choose size': 'اختار المقاس', 'Add to cart': 'ضيف للسلة',
    'Football boot resting on a match ball': 'جزمة كورة على كورة ماتش', 'Filters': 'الفلاتر', 'Sort': 'الترتيب',

    /* ---------- Fantasy ---------- */
    'My team': 'فريقي', 'Pick squad': 'اختار التشكيلة', 'Transfers': 'الانتقالات', 'Leagues': 'الدوريات', 'Table': 'الجدول', 'Stats': 'الإحصائيات',
    'Build your dream team': 'كوّن فريق أحلامك', '15 players · 100M budget · free leagues with prizes': '١٥ لاعب · ميزانية ١٠٠ مليون · دوريات مجانية بجوايز',
    '{0} players · {1}M budget · free leagues with prizes': '{0} لاعب · ميزانية {1} مليون · دوريات مجانية بجوايز',
    'Pick 15 players': 'اختار ١٥ لاعب', 'Pick {0} players': 'اختار {0} لاعب', 'Choose a captain': 'اختار الكابتن', 'Win every gameweek': 'اكسب كل جولة', 'Step {0}': 'خطوة {0}',
    'Pick my squad': 'اختار تشكيلتي', 'Auto-pick': 'اختيار تلقائي', 'How it works': 'إزاي بتشتغل',
    '2 goalkeepers, 5 defenders, 5 midfielders, 3 forwards. Max 3 from one club.': '٢ حراس، ٥ مدافعين، ٥ وسط، ٣ مهاجمين. أقصى حاجة ٣ من نفس النادي.',
    '{0} goalkeepers, {1} defenders, {2} midfielders, {3} forwards. Max {4} from one club.': '{0} حراس، {1} مدافعين، {2} وسط، {3} مهاجمين. أقصى حاجة {4} من نفس النادي.',
    'Choose a formation and a captain — captains score double.': 'اختار الخطة والكابتن — الكابتن نقطه بتتضاعف.',
    'Players score from goals, assists and clean sheets.': 'اللاعيبة بتجمع نقط من الأهداف والأسيست والشباك النضيفة.',
    'Every league is free. The top 3 of every gameweek in the Super Malaab League win Super Points.': 'كل الدوريات مجانية. أول ٣ في كل جولة في دوري سوبر ملعب بيكسبوا سوبر بوينتس.',
    'Every league is free. The top {0} of every gameweek in the Super Malaab League win Super Points.': 'كل الدوريات مجانية. أول {0} في كل جولة في دوري سوبر ملعب بيكسبوا سوبر بوينتس.',
  }).forEach(([k, v]) => { if (!(k in all)) all[k] = v; });
})(window.SA);
