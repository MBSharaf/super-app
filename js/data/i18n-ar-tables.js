/** Arabic dictionary — league tables, match facts (stats, events, line-ups) and the competitions rail (2026-11-24). */
(function (SA) {
  const D = SA.data.i18nAr;
  const add = (o) => Object.entries(o).forEach(([k, v]) => { if (!(k in D.strings)) D.strings[k] = v; });
  add({
    // View tabs and tables
    Matches: 'الماتشات', Tables: 'الجداول', Table: 'الجدول', Season: 'موسم', Club: 'النادي',
    'League tables come from the live server.': 'جداول الدوري بتيجي من السيرفر اللايف.',
    'No table for this competition yet': 'مفيش جدول للبطولة دي لسه',
    'Knockout rounds have no table. Pick another competition.': 'أدوار خروج المغلوب مالهاش جدول. اختار بطولة تانية.',
    'The table couldn’t load. Try again in a minute.': 'الجدول ماحمّلش. جرّب كمان دقيقة.',
    // Competitions
    'English Premier League': 'الدوري الإنجليزي الممتاز', 'Egyptian Premier League': 'الدوري المصري الممتاز', 'UEFA Champions League': 'دوري أبطال أوروبا',
    'La Liga': 'الدوري الإسباني', 'Serie A': 'الدوري الإيطالي', Bundesliga: 'الدوري الألماني', 'Ligue 1': 'الدوري الفرنسي', 'Süper Lig': 'الدوري التركي',
    'UEFA Europa League': 'الدوري الأوروبي', 'UEFA Conference League': 'دوري المؤتمر الأوروبي', 'CAF Champions League': 'دوري أبطال أفريقيا',
    'CAF Confederation Cup': 'كأس الكونفدرالية الأفريقية', 'Africa Cup of Nations': 'كأس الأمم الأفريقية', 'UEFA European Championship': 'كأس الأمم الأوروبية', 'FIFA World Cup': 'كأس العالم',
    // Table legend (from the live feed)
    'Champions League': 'دوري الأبطال', 'Champions League qualification': 'تأهل لدوري الأبطال', 'Europa League': 'الدوري الأوروبي',
    'Europa League qualification': 'تأهل للدوري الأوروبي', 'Conference League': 'دوري المؤتمر', 'Conference League qualification': 'تأهل لدوري المؤتمر',
    'Conference League Qualification': 'تأهل لدوري المؤتمر', 'Relegation': 'هبوط', 'Relegation playoff': 'ملحق الهبوط', 'Promotion': 'صعود', 'Promotion playoff': 'ملحق الصعود',
    'Qualification': 'تأهل', 'Playoff': 'ملحق', 'Playoffs': 'الملحق', 'Knockout phase': 'الأدوار الإقصائية', 'Knockout phase playoffs': 'ملحق الأدوار الإقصائية',
    'Championship round': 'مجموعة البطولة', 'Relegation round': 'مجموعة الهبوط', 'CAF Confederation Cup qualification': 'تأهل لكأس الكونفدرالية',
    'Championship playoff': 'ملحق البطولة', 'Relegation qualification': 'ملحق الهبوط', 'Qualification to 1/8 finals': 'تأهل لدور الـ16',
    'Qualification to 1/16 finals': 'تأهل للملحق (دور الـ32)', 'Qualification next stage': 'تأهل للدور التالي', 'Possible qualification next stage': 'تأهل محتمل للدور التالي',
    'Group A': 'المجموعة A', 'Group B': 'المجموعة B', 'Group C': 'المجموعة C', 'Group D': 'المجموعة D', 'Group E': 'المجموعة E', 'Group F': 'المجموعة F', 'Group G': 'المجموعة G', 'Group H': 'المجموعة H', 'Group I': 'المجموعة I', 'Group J': 'المجموعة J', 'Group K': 'المجموعة K', 'Group L': 'المجموعة L',
    // Match centre
    Stats: 'الإحصائيات', Timeline: 'الأحداث', 'Line-ups': 'التشكيل', Substitutes: 'البدلاء', 'Coach:': 'المدرب:',
    'Match stats appear here once the match kicks off.': 'إحصائيات الماتش هتظهر هنا أول ما يبدأ.',
    'Goals, cards and substitutions appear here during the match.': 'الأهداف والكروت والتغييرات هتظهر هنا وقت الماتش.',
    'Line-ups are usually out about an hour before kick-off.': 'التشكيل بينزل عادةً قبل الماتش بساعة تقريباً.',
    'Match details couldn’t load. Try again in a minute.': 'تفاصيل الماتش ماحمّلتش. جرّب كمان دقيقة.',
    Penalty: 'ضربة جزاء', 'Own goal': 'هدف عكسي', Yellow: 'إنذار', Red: 'طرد',
    // Stat groups and titles (from the live feed)
    'Top stats': 'أهم الإحصائيات', Shots: 'التسديدات', 'Expected goals (xG)': 'الأهداف المتوقعة (xG)', Passes: 'التمريرات', Defence: 'الدفاع', Duels: 'الالتحامات', Discipline: 'الانضباط',
    'Ball possession': 'الاستحواذ', 'Total shots': 'إجمالي التسديدات', 'Shots on target': 'تسديدات على المرمى', 'Shots off target': 'تسديدات خارج المرمى',
    'Blocked shots': 'تسديدات اتصدت', 'Big chances': 'فرص محققة', 'Big chances missed': 'فرص محققة ضايعة', 'Accurate passes': 'تمريرات صحيحة',
    'Fouls committed': 'الأخطاء', Corners: 'الركنيات', Offsides: 'التسلل', 'Yellow cards': 'الكروت الصفرا', 'Red cards': 'الكروت الحمرا',
    'Hit woodwork': 'القائم والعارضة', 'Shots inside box': 'تسديدات من جوه الصندوق', 'Shots outside box': 'تسديدات من بره الصندوق',
    'xG open play': 'xG من اللعب المفتوح', 'xG set play': 'xG من الكرات الثابتة', 'Non-penalty xG': 'xG من غير ضربات جزاء', 'xG on target (xGOT)': 'xG على المرمى (xGOT)',
    'Own half': 'في نص ملعبه', "Opposition half": 'في نص ملعب المنافس', 'Opposition half passes': 'تمريرات في نص ملعب المنافس', 'Accurate long balls': 'كرات طويلة صحيحة',
    'Accurate crosses': 'عرضيات صحيحة', Throws: 'رميات التماس', 'Touches in opposition box': 'لمسات في صندوق المنافس', 'Tackles won': 'تدخلات ناجحة',
    Interceptions: 'قطع الكرة', Blocks: 'صد التسديدات', Clearances: 'إبعاد الكرة', 'Keeper saves': 'تصديات الحارس', 'Duels won': 'التحامات كسبها',
    'Ground duels won': 'التحامات أرضية كسبها', 'Aerial duels won': 'التحامات هوائية كسبها', 'Successful dribbles': 'مراوغات ناجحة', Tackles: 'التدخلات', Saves: 'التصديات',
  });
})(window.SA);
