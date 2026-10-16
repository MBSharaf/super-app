/** Arabic dictionary — part 13: pop-up ads (site) and their admin page. */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- Default pop-ups ---------- */
    'No stock, no cost': 'من غير مخزون ولا تكلفة', 'Join as a seller and earn': 'انضم كبائع واكسب', 'Upload your designs — we print, ship and pay you for every sale.': 'ارفع تصميماتك — إحنا بنطبع وبنشحن وبندفعلك في كل بيعة.', 'Start selling': 'ابدأ البيع',
    'Free to play': 'ببلاش', 'Play Egyptian League fantasy': 'العب فانتازي الدوري المصري', 'Pick your 15, join a league with your friends and win prizes every gameweek.': 'اختار الـ 15 بتوعك، ادخل دوري مع صحابك واكسب جوايز كل جولة.', 'Pick my team': 'اختار فريقي',
    '10% off your first order': 'خصم 10% على أول طلب', 'Use code KICKOFF10 at checkout in the Super App shop.': 'استخدم كود KICKOFF10 وإنت بتدفع في متجر Super App.', 'Shop now': 'اشتري دلوقتي',
    'Before 4 PM': 'قبل 4 العصر', 'Pitches from 500 EGP an hour': 'ملاعب من 500 ج.م في الساعة', 'Weekday mornings and afternoons cost less. See the free hours near you.': 'الصبح والضهر في أيام الأسبوع أرخص. شوف المواعيد الفاضية جنبك.', 'Book a pitch': 'احجز ملعب',
    'Delivered across Egypt': 'توصيل لكل مصر', 'Boots, kits and printed designs': 'جزم وأطقم وتصميمات مطبوعة', 'Football gear and original designs from Egyptian creators in one shop.': 'لبس ومعدات كورة وتصميمات أصلية من مصممين مصريين في متجر واحد.', 'Open the shop': 'افتح المتجر',
    'Same pitch every week? Save 10%': 'نفس الملعب كل أسبوع؟ وفّر 10%', 'Book a weekly package and lock your hour for the whole month.': 'احجز باقة أسبوعية وثبّت ساعتك الشهر كله.', 'See pitches': 'شوف الملاعب',
    'Enter a 5-a-side tournament': 'اشترك في بطولة خماسي', 'Bring your team, play the group stage and go for the trophy.': 'هات فريقك، العب دور المجموعات وروح على الكاس.', 'See tournaments': 'شوف البطولات',
    'Short of players?': 'ناقصك لعيبة؟', 'Fill your squad in minutes': 'كمّل فريقك في دقايق', 'Post your game and players near you ask to join.': 'انشر ماتشك واللعيبة اللي جنبك هيطلبوا ينضموا.', 'Post a game': 'انشر ماتش',
    'Points on every booking and order': 'نقاط على كل حجز وطلب', 'Collect points and swap them for vouchers from 100 EGP.': 'جمّع نقاط وبدّلها بقسايم من 100 ج.م.', 'My points': 'نقاطي',
    'For pitch owners': 'لأصحاب الملاعب', 'List your pitch on Super App': 'ضيف ملعبك على Super App', 'Online bookings and payments, plus a free dashboard for your team.': 'حجز ودفع أونلاين، ولوحة تحكم ببلاش لفريقك.', 'Apply now': 'قدّم دلوقتي',
    /* ---------- Pop-up card ---------- */
    'Don’t show offers': 'ماتعرضش عروض', 'OK — no offers for 7 days.': 'تمام — مفيش عروض لمدة 7 أيام.', 'See more': 'شوف أكتر',
    /* ---------- Admin ---------- */
    'Pop-ups': 'البوب أب', 'Pop-up ads': 'إعلانات البوب أب', 'Show pop-ups': 'اعرض البوب أب', 'First one after (seconds)': 'أول واحد بعد (ثانية)', 'Then one every (seconds)': 'وبعدها واحد كل (ثانية)', 'Each stays (seconds)': 'كل واحد يفضل (ثانية)', 'At most per visit': 'بحد أقصى في الزيارة',
    'Small cards with a 3D object for Super App’s own services and offers. They take turns on the public site — never on checkout, dashboards or the studio, never over a dialog, and never for the page the visitor is already on. Visitors can close one or turn offers off for 7 days.': 'كروت صغيرة فيها مجسم 3D لخدمات وعروض Super App. بتتبادل على الموقع العام — عمرها ما بتظهر في الدفع أو لوحات التحكم أو الاستوديو، ولا فوق نافذة مفتوحة، ولا لنفس الصفحة اللي الزائر فيها. والزائر يقدر يقفل واحد أو يوقف العروض 7 أيام.',
    'They take turns in this order, one at a time.': 'بيظهروا بالترتيب ده، واحد ورا التاني.', 'New pop-up': 'بوب أب جديد', 'Edit pop-up': 'تعديل البوب أب', 'Preview': 'معاينة', 'Turn on': 'شغّل',
    'A small card with a 3D object that slides in on the public site. Visitors can close it or turn offers off.': 'كارت صغير فيه مجسم 3D بيظهر على الموقع العام. الزائر يقدر يقفله أو يوقف العروض.',
    'Text': 'النص', 'Button text': 'كلام الزرار', 'Opens': 'بيفتح', 'Badge (on the coin keep it short, e.g. 10%)': 'الشارة (على العملة خليها قصيرة، مثلاً 10%)', 'Colour': 'اللون', '3D object': 'المجسم الـ 3D',
    'Spinning cube with icons': 'مكعب بيلف عليه أيقونات', 'Coin with the badge': 'عملة عليها الشارة', '3D T-shirt': 'تيشيرت 3D', 'Cube icons (comma separated)': 'أيقونات المكعب (افصل بينهم بفاصلة)',
    'Don’t show to people who already sell': 'ماتعرضهوش للي بيبيعوا أصلاً', 'Save pop-up': 'احفظ البوب أب', 'Add pop-up': 'ضيف البوب أب', 'Pop-up saved': 'البوب أب اتحفظ', 'Pop-up deleted': 'البوب أب اتمسح', 'Pop-up settings saved': 'إعدادات البوب أب اتحفظت',
    'Delete this pop-up?': 'تمسح البوب أب ده؟', 'It stops showing on the site.': 'هيبطل يظهر على الموقع.', 'Write a title (4+ characters).': 'اكتب عنوان (4 حروف أو أكتر).', 'Write a line about the offer (10+ characters).': 'اكتب سطر عن العرض (10 حروف أو أكتر).', 'Pick a colour.': 'اختار لون.',
    'Use a link inside Super App, like #/fantasy': 'استخدم لينك جوه Super App، زي #/fantasy', 'Cube': 'مكعب', 'Coin': 'عملة', 'Pop-up ads — Super App': 'إعلانات البوب أب — Super App',
    'What visitors see: home banners, the announcement bar, pop-ups, news and pickup games': 'اللي الزوار بيشوفوه: بانرات الرئيسية وشريط الإعلانات والبوب أب والأخبار والماتشات',
  });
  D.patterns.push(
    ['^shown (\\d+) · clicked (\\d+)(.*)$', 'اتعرض {1} · اتداس {2}{3}'],
  );
})(window.SA);
