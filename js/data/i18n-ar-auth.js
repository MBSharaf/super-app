/** Arabic dictionary — part 14: real accounts (API mode): sign in, sign up, two-step, applications sent to the server. */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- Account pages ---------- */
    'Sign in': 'تسجيل الدخول', 'Use your email or mobile number.': 'استخدم الإيميل أو رقم الموبايل.', 'Email or mobile': 'الإيميل أو الموبايل', 'Password': 'الباسورد',
    'Forgot your password?': 'نسيت الباسورد؟', 'New here?': 'جديد هنا؟', 'Create an account': 'اعمل حساب', 'Create account': 'اعمل حساب',
    'Enter your code': 'اكتب الكود', 'Open your authenticator app and type the 6-digit code for Super App.': 'افتح تطبيق التحقق واكتب الكود اللي فيه 6 أرقام بتاع Super App.',
    'Code': 'الكود', 'I’m using one of my recovery codes': 'هستخدم كود من أكواد الاسترجاع', 'Continue': 'كمّل', 'Back to sign in': 'رجوع لتسجيل الدخول',
    'Create your account': 'اعمل حسابك', 'Book pitches, join games, shop and play fantasy with one account.': 'احجز ملاعب، انضم لماتشات، اشتري، والعب فانتازي بحساب واحد.',
    'Full name': 'الاسم بالكامل', 'Email': 'الإيميل', 'Mobile': 'الموبايل', 'At least 8 characters, with a number.': '8 حروف على الأقل وفيها رقم.',
    'I agree to the': 'أنا موافق على', 'terms of use': 'شروط الاستخدام', 'and the': 'و', 'privacy policy': 'سياسة الخصوصية',
    'Create my account': 'اعمل حسابي', 'Already have an account?': 'عندك حساب؟', 'Your account is ready — check your email to confirm it': 'حسابك جاهز — شوف الإيميل عشان تأكده',
    'Enter your email and we’ll send you a link to choose a new one.': 'اكتب إيميلك وهنبعتلك لينك تختار منه باسورد جديد.', 'Send the link': 'ابعت اللينك',
    'Choose a new password': 'اختار باسورد جديد', 'New password': 'الباسورد الجديد', 'Save my password': 'احفظ الباسورد', 'Password changed — sign in with the new one': 'الباسورد اتغير — ادخل بالجديد',
    'Welcome to the Super App team': 'أهلاً بيك في فريق Super App', 'Choose your password. Next you’ll sign in and set up your authenticator app.': 'اختار الباسورد بتاعك. بعدها هتدخل وتفعّل تطبيق التحقق.',
    'Accept the invitation': 'قبول الدعوة', 'Password set — now sign in': 'الباسورد اتحفظ — ادخل دلوقتي',
    'Confirming your email…': 'بنأكد إيميلك…', 'Your email is confirmed. Thank you!': 'إيميلك اتأكد. شكراً!', 'Go to my profile': 'روح للبروفايل', 'My profile': 'البروفايل بتاعي',
    'Set up two-step sign-in': 'فعّل التحقق بخطوتين', 'Team accounts use an authenticator app (Google or Microsoft Authenticator) for every sign-in.': 'حسابات الفريق بتستخدم تطبيق تحقق (Google أو Microsoft Authenticator) في كل دخول.',
    'Open the app and add an account.': 'افتح التطبيق وضيف حساب.', 'Scan this code, or type the key.': 'امسح الكود ده، أو اكتب المفتاح.', 'Enter the 6-digit code it shows.': 'اكتب الكود اللي فيه 6 أرقام.',
    'Key:': 'المفتاح:', '6-digit code': 'كود من 6 أرقام', 'Turn it on': 'فعّله', 'Two-step sign-in is on.': 'التحقق بخطوتين اتفعّل.',
    'Keep these recovery codes somewhere safe — each one works once if you lose your phone.': 'احفظ أكواد الاسترجاع دي في مكان آمن — كل كود بيشتغل مرة واحدة لو موبايلك ضاع.',
    'Real accounts': 'الحسابات الحقيقية', 'Signing in with your own email and password works when Super App runs on its server. This demo site keeps a demo account on your device.': 'الدخول بإيميلك وباسوردك بيشتغل لما Super App يشتغل على السيرفر بتاعه. نسخة التجربة دي بتحتفظ بحساب تجريبي على جهازك.',
    'Open my demo profile': 'افتح البروفايل التجريبي',

    /* ---------- Profile ---------- */
    'Sign in to your Super App account': 'ادخل على حسابك في Super App', 'Your bookings, orders and applications follow you on every device.': 'حجوزاتك وطلباتك وطلبات الانضمام معاك على أي جهاز.',
    'Email confirmed': 'الإيميل متأكد', 'Email not confirmed': 'الإيميل مش متأكد', 'Send the link again': 'ابعت اللينك تاني', 'Two-step on': 'التحقق بخطوتين شغال',
    'Pitch dashboard': 'لوحة الملاعب', 'Seller dashboard': 'لوحة البائع', 'Factory system': 'نظام المصنع', 'Admin console': 'لوحة الأدمن', 'Command Center': 'مركز القيادة',
    'Sign out on all devices': 'اخرج من كل الأجهزة', 'Sign out': 'خروج', 'Signed out': 'خرجت', 'Signed out on every device': 'خرجت من كل الأجهزة', 'We sent you a new link': 'بعتنالك لينك جديد',

    /* ---------- Team sign-in, applications, approvals (API mode) ---------- */
    'Sign in with your team account': 'ادخل بحساب الفريق', 'Your email, password and the code from your authenticator app.': 'الإيميل والباسورد والكود من تطبيق التحقق.',
    'Photo of your ID (back)': 'صورة البطاقة (ضهر)', 'Choose the area from the list.': 'اختار المنطقة من القايمة.', 'Live applications': 'الطلبات الحقيقية',
    'Applications from the server': 'طلبات من السيرفر', 'ID photos': 'صور البطاقة',
    'Real accounts (when the site runs on the Super App server)': 'الحسابات الحقيقية (لما الموقع يشتغل على سيرفر Super App)', 'Two-step code': 'كود التحقق بخطوتين',
    'Forgot password': 'نسيت الباسورد', 'Choose a new password (from the email)': 'اختيار باسورد جديد (من الإيميل)', 'Confirm email (from the email)': 'تأكيد الإيميل (من الإيميل)', 'Team invitation (from the email)': 'دعوة الفريق (من الإيميل)',
    'Not part of your permissions': 'مش من صلاحياتك', 'Ask the CEO for the approvals permission for this type.': 'اطلب من المدير التنفيذي صلاحية الموافقة على النوع ده.', 'New applications wait here.': 'الطلبات الجديدة بتستنى هنا.',
    'Sign in to apply': 'ادخل عشان تقدّم', 'Applications are linked to your Super App account.': 'الطلبات بتتربط بحسابك في Super App.',
    'You’re a Super App seller': 'إنت بقيت بائع على Super App', 'Open my seller dashboard': 'افتح لوحة البائع',
    'This account can’t open this system.': 'الحساب ده مينفعش يفتح النظام ده.', 'Enter the 6-digit code from your authenticator app.': 'اكتب الكود اللي فيه 6 أرقام من تطبيق التحقق.', 'Welcome back': 'أهلاً بيك تاني',
    /* ---------- Pitches and bookings from the server ---------- */
    'Sign in to book': 'ادخل عشان تحجز', 'Bookings are linked to your Super App account.': 'الحجوزات بتتربط بحسابك في Super App.',
    'This time isn’t available': 'الميعاد ده مش متاح', 'Promo codes aren’t available yet. They arrive with online payments.': 'أكواد الخصم لسه مش متاحة. هتيجي مع الدفع أونلاين.',
    'Reviews open soon on the live site.': 'التقييمات هتفتح قريب على الموقع.', 'Booking not found.': 'الحجز مش موجود.',
    'Pay at the venue': 'الدفع في الملعب', 'Paid in the app': 'مدفوع في التطبيق',
    'We couldn’t find that pitch. It may have been removed.': 'ما لقيناش الملعب ده. ممكن يكون اتشال.',
    /* ---------- Partner dashboard from the server ---------- */
    'Sign in to your partner account': 'ادخل بحساب الشريك بتاعك', 'Use the account you applied with. Staff use their own account.': 'استخدم الحساب اللي قدّمت بيه. الموظفين يدخلوا بحساباتهم.',
    'This account has no pitch business yet': 'الحساب ده لسه مالوش ملاعب', 'Apply to list your pitch, or ask the owner to add you as staff.': 'قدّم عشان تضيف ملعبك، أو اطلب من صاحب الملعب يضيفك كموظف.',
    'QR check-in on the live site arrives in the next step.': 'تسجيل الحضور بالـ QR على الموقع هييجي في الخطوة الجاية.',
    'Choose an area from the list.': 'اختار المنطقة من القايمة.', 'A photo could not be loaded. Try another one.': 'صورة ماتحملتش. جرّب صورة تانية.',
    /* ---------- QR check-in from the server ---------- */
    'Checked in. Have a great game!': 'اتسجل الحضور. ماتش موفق!', 'This code isn’t a Super App booking. Ask for the QR in the app or the booking ID.': 'الكود ده مش حجز على Super App. اطلب الـ QR من التطبيق أو رقم الحجز.',
    'This booking is for another pitch.': 'الحجز ده لملعب تاني.', 'This booking is for another day.': 'الحجز ده ليوم تاني.',
    'Too early or too late: check-in opens 60 minutes before kick-off and closes when the booking ends.': 'بدري أو متأخر: تسجيل الحضور بيفتح قبل الماتش بساعة وبيقفل لما الحجز يخلص.',
    'This booking was cancelled.': 'الحجز ده اتلغى.', 'Already checked in.': 'الحضور متسجل قبل كده.', 'Every match of this package was used.': 'كل ماتشات الباقة دي اتلعبت.',
    'This player’s account is blocked. Contact Super App support.': 'حساب اللاعب ده موقوف. كلّم دعم Super App.',
  });
  D.patterns.push(
    ['^Back to (.+)$', 'رجوع لـ {1}'],
    ['^(.+) was sent for Super App review$', '{1} اتبعت لمراجعة Super App'],
    ['^If (.+) has an account, a link is on its way\\. It works for 30 minutes\\.$', 'لو {1} ليه حساب، اللينك في الطريق. شغال نص ساعة.'],
  );
})(window.SA);
