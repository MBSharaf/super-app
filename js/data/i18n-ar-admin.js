/** Arabic dictionary — part 6: the admin console (#/admin). */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- Shell & sign-in ---------- */
    'Admin console': 'لوحة الإدارة', 'Admin console — Super App': 'لوحة الإدارة — Super App', 'Super App team only': 'لفريق Super App بس',
    'Run the whole platform from one place.': 'أدر المنصة كلها من مكان واحد.',
    'Money and withdrawals, customers, partners and sellers, every order and booking, support, content, fantasy and promotions — with a full audit log.': 'الفلوس والسحوبات، العملاء والشركاء والبايعين، كل الطلبات والحجوزات، الدعم، المحتوى، الفانتازي والعروض — مع سجل كامل لكل عملية.',
    'Demo account — admin sign-in with email, password and two-step verification arrives with the backend.': 'حساب تجريبي — دخول الأدمن بالإيميل والباسورد والتحقق بخطوتين هييجي مع الباك إند.',
    'Pitch owner, seller or print partner?': 'صاحب ملعب أو بايع أو شريك طباعة؟', 'Go to the partner dashboard': 'روح للوحة الشركاء',
    'Platform revenue · 30 days': 'إيراد المنصة · 30 يوم', 'Withdrawals to approve': 'سحوبات مستنية موافقة', 'Audit log': 'سجل العمليات', 'Every change, signed': 'كل تعديل متسجل باسم صاحبه',
    'Signed out of the admin console': 'خرجت من لوحة الإدارة', 'Admin': 'أدمن',
    /* Navigation */
    'Withdrawals': 'السحوبات', 'Revenue': 'الإيرادات', 'People': 'الناس', 'Customers': 'العملاء', 'Partners': 'الشركاء', 'Sellers': 'البايعين', 'Operations': 'التشغيل',
    'Orders & bookings': 'الطلبات والحجوزات', 'Support & disputes': 'الدعم والنزاعات', 'Reviews': 'التقييمات', 'Print on demand': 'الطباعة عند الطلب', 'Design review': 'مراجعة التصميمات',
    'Level review': 'مراجعة المستويات', 'Samples': 'العينات', 'Print orders': 'طلبات الطباعة', 'Print catalogue': 'كتالوج الطباعة', 'Marketing & content': 'التسويق والمحتوى',
    'Broadcasts': 'الرسائل الجماعية', 'Promo codes': 'أكواد الخصم', 'Site content': 'محتوى الموقع', 'Fantasy': 'الفانتازي', 'Insights': 'التحليلات', 'Analytics': 'التحليلات المتقدمة', 'Platform settings': 'إعدادات المنصة',
    /* ---------- Overview ---------- */
    'Money through Super App (30 days)': 'الفلوس اللي عدّت على Super App (30 يوم)', 'Platform revenue (30 days)': 'إيراد المنصة (30 يوم)', 'Open to-dos': 'مهام مفتوحة', 'Nothing waiting': 'مفيش حاجة مستنية', 'All clear': 'كله تمام',
    'Revenue by day': 'الإيراد باليوم', 'Full report': 'التقرير الكامل', 'Needs your attention': 'محتاج انتباهك', 'No withdrawals, reviews or reports waiting.': 'مفيش سحوبات أو تقييمات أو بلاغات مستنية.',
    'Where revenue comes from': 'الإيراد جاي منين', 'On the platform now': 'على المنصة دلوقتي', 'Live pitches': 'ملاعب شغالة', 'Pitch owners': 'أصحاب الملاعب', 'Active sellers': 'بايعين نشطين', 'Live designs': 'تصميمات منشورة',
    'Live stores': 'متاجر شغالة', 'Products for sale': 'منتجات للبيع', 'Latest activity': 'آخر النشاطات', 'Overview': 'نظرة عامة',
    'Booking commission': 'عمولة الحجوزات', 'Booking service fees': 'رسوم خدمة الحجز', 'Store commission': 'عمولة المتاجر', 'Official store sales': 'مبيعات المتجر الرسمي', 'Print-on-demand margin': 'هامش الطباعة عند الطلب', 'Fantasy Pass': 'Fantasy Pass',
    'Refused print deliveries': 'شحنات طباعة اترفضت', 'Refunds paid by Super App': 'مرتجعات دفعتها Super App', 'Super App promo codes': 'أكواد خصم Super App',
    /* ---------- Audit log ---------- */
    'Who': 'مين', 'Everyone': 'الكل', 'Admins': 'الأدمنز', 'Print partner': 'شريك الطباعة', 'System': 'النظام', 'Search': 'بحث', 'Name, order ID, design, code…': 'اسم، رقم طلب، تصميم، كود…',
    'Filter': 'فلترة', 'Excel': 'إكسيل', 'When': 'إمتى', 'What happened': 'إيه اللي حصل', 'Reference': 'المرجع', 'Nothing found': 'مفيش نتايج', 'Try another filter.': 'جرّب فلتر تاني.',
    'The demo keeps the latest 1,000 on this device.': 'النسخة التجريبية بتحتفظ بآخر 1,000 على الجهاز ده.',
    'Who changed what, and when — admins, partners, sellers, the print partner and the system': 'مين غيّر إيه وإمتى — الأدمنز والشركاء والبايعين وشريك الطباعة والنظام',
    'Admin': 'أدمن', 'Pitch owner': 'صاحب ملعب', 'Seller': 'بايع', 'Customer': 'عميل',
    /* Shared */
    'Reason': 'السبب', 'Write a short reason (it’s saved in the audit log).': 'اكتب سبب قصير (بيتسجل في سجل العمليات).', 'Open their dashboard': 'افتح لوحتهم',
  });
  D.patterns.push(
    ['^(\\d+) withdrawals? to approve$', '{1} سحب مستني موافقة'], ['^(.+) EGP waiting to be sent\\.$', '{1} جنيه مستنيين يتحولوا.'],
    ['^(\\d+) designs? to review$', '{1} تصميم محتاج مراجعة'], ['^(\\d+) new or changed · (\\d+) copyright reports?\\.$', '{1} جديد أو متعدل · {2} بلاغ حقوق ملكية.'],
    ['^(\\d+) seller ID checks?$', '{1} تأكيد هوية بايع'], ['^(\\d+) reported reviews?$', '{1} تقييم متبلغ عنه'], ['^(\\d+) support tickets? waiting$', '{1} تذكرة دعم مستنية'],
    ['^(\\d+) sample requests?$', '{1} طلب عينة'], ['^(\\d+) queues?$', '{1} قايمة'],
    ['^Super App platform · (.+)$', 'منصة Super App · {1}'], ['^(\\d+) entr(?:y|ies)\\. The demo keeps the latest 1,000 on this device\\.$', '{1} عملية. النسخة التجريبية بتحتفظ بآخر 1,000 على الجهاز ده.'],
    ['^Show more \\((\\d+) older\\)$', 'اعرض أكتر ({1} أقدم)'], ['^Last 30 days · costs: (.+) \\(refused print deliveries, refunds and promo codes paid by Super App\\)$', 'آخر 30 يوم · التكاليف: {1} (شحنات طباعة اترفضت ومرتجعات وأكواد خصم دفعتها Super App)'],
    ['^(.) (\\d+)% vs the 30 days before$', '{1} {2}% مقارنة بالـ30 يوم اللي قبلها'],
  );
})(window.SA);
