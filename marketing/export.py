"""Builds the content pack: marketing/CAPTIONS.md (every caption in one place) and a ZIP with the final files
(posts, carousel slides, reels with and without sound, covers) plus one caption.txt per piece.

    python3 marketing/export.py            -> marketing/CAPTIONS.md + marketing/export/SuperApp-content-<date>.zip

The captions below are the source of truth; the READMEs next to each piece explain how it was made.
Rules they follow (CLAUDE.md): main keyword in the first line, Egyptian Arabic, one CTA, 3-5 hashtags,
no betting framing, no "first fantasy" claim, examples labelled as examples."""

import os, zipfile, datetime

HERE = os.path.dirname(os.path.abspath(__file__))
P1, P2 = 'posts/2026-10-17-teasers', 'posts/2026-10-18-week1'

ITEMS = [
    # ---- posts (4:5, 1080×1350) ----
    dict(id='post-01-players', kind='post', title='الماتش ناقصه 2', audience='لاعيبة الخماسي', when='أي يوم بالليل 8–11 م',
         files=[f'{P1}/post-01-players.jpg'],
         caption='''ماتش خماسي ناقصه 2 والساعة 11 بالليل؟ 😅
قريب على Super App: تنشر الماتش، والناس تنضم وتدفع نصيبها أونلاين. ومحدش يلم فلوس من حد تاني.
تابعنا عشان تبقى من أول اللي يلعبوا.

#ماتش_خماسي #كورة #خماسي #سوبر_اب''',
         pin='مين عنده ماتش الأسبوع ده وناقصه لاعيبة؟ 👇'),
    dict(id='post-02-fantasy', kind='post', title='فانتازي المصري والإنجليزي', audience='لاعيبة الفانتازي', when='قبل ديدلاين الجولة',
         files=[f'{P1}/post-02-fantasy.jpg'],
         caption='''فانتازي الدوري المصري والإنجليزي… في تطبيق واحد.
فريق لكل دوري، وجروبات مع صحابك، وجوايز من الرعاة.
كابتنك مين الجولة دي؟ قولنا في الكومنتات 👇

#فانتازي #فانتازي_الدوري_المصري #FPL #سوبر_اب''',
         pin='كابتني الجولة دي: … وانت؟',
         notes='الجوايز من الرعاة (قسائم وخصومات)، عمرها ما تكون فلوس.'),
    dict(id='post-03-sellers', kind='post', title='براند تيشيرتات بـ 0 جنيه', audience='اللي عايزين يعملوا براند / مصممين', when='الخميس–السبت بالليل',
         files=[f'{P1}/post-03-sellers.jpg'],
         caption='''افتح براند تيشيرتات بـ 0 جنيه.
ارفع تصميمك، وإحنا نطبع ونشحن، وإنت تكسب. من غير مخزن ولا مصنع ولا رأس مال.
تابعنا، قريب نفتح باب البائعين.

#براند_مصري #طباعة_تيشيرتات #بيزنس_اونلاين #سوبر_اب''',
         pin='عندك فكرة تيشيرت؟ اكتب الفكرة في كلمتين 👇'),
    dict(id='post-04-captain-gw7', kind='post', title='كابتن الجولة 7', audience='لاعيبة فانتازي الدوري المصري', when='قبل الاتنين 19 أكتوبر (ديدلاين الجولة 7)',
         files=[f'{P2}/post-04-captain-gw7.jpg'],
         caption='''كابتن الجولة 7 في فانتازي الدوري المصري 👀
زيزو قدام القناة؟ ناصر ماهر قدام طلائع الجيش؟ ولا المفاجأة محمود ممدوح قدام زد؟
كابتنك مين؟ اكتبه في الكومنتات، وبعد الجولة نشوف مين صدق.

#فانتازي_الدوري_المصري #فانتازي #الدوري_المصري #سوبر_اب''',
         pin='بعد الجولة هنرجع للكومنتات دي ونشوف مين صدق 😄',
         notes='راجع مواعيد الماتشات يوم النشر: الأهلي × القناة الأربع 21 أكتوبر 8 م، بيراميدز × طلائع الجيش والتلات 20 أكتوبر 8 م، زد × مودرن سبورت التلات 20 أكتوبر 5 م.'),
    dict(id='post-05-who-collects', kind='post', title='مين هيلم الفلوس؟', audience='كباتن ومنظمين الماتشات', when='أي يوم في الأسبوع بالليل',
         files=[f'{P2}/post-05-who-collects.jpg'],
         caption='''مين هيلم فلوس ماتش الخماسي النهارده؟
14 لاعب × 95 جنيه… وكل أسبوع 3 لسه "هحوّلك بالليل".
قريب على Super App: كل واحد بيدفع نصيبه وهو بينضم للماتش، والكابتن يرتاح.
منشن الكابتن بتاعكم 👇

#ماتش_خماسي #كورة #كابتن_الماتش #سوبر_اب''',
         pin='الكابتن اللي بيلم الفلوس كل أسبوع يستاهل تحية 🫡'),
    dict(id='post-06-empty-hours', kind='post', title='الساعة الفاضية بتخسرك كام؟', audience='أصحاب الملاعب', when='الصبح في أيام الأسبوع (جروبات فيسبوك + لينكدإن)',
         files=[f'{P2}/post-06-empty-hours.jpg'],
         caption='''الساعة الفاضية في ملعب الخماسي بتاعك بتخسرك كام؟
مثال: 3 ساعات فاضية في اليوم × 400 جنيه × 30 يوم = 36,000 جنيه في الشهر.
داشبورد Super App بيوريك الساعات الهادية، ويقترح عليها عرض، وبيملاها بحجوزات أونلاين.
لو عندك ملعب، ابعتلنا رسالة.

#ملاعب_مصر #حجز_ملاعب #ملاعب_خماسي #سوبر_اب''',
         pin='أصحاب الملاعب: الساعات الهادية عندكم إمتى؟ الضهر ولا بعد 12؟',
         notes='الأرقام مثال، ومكتوب على التصميم إنها مثال.'),
    dict(id='post-07-crew-league', kind='post', title='اعمل دوري لصحابك', audience='لاعيبة الفانتازي وشلتهم', when='الجمعة قبل جولة البريميرليج',
         files=[f'{P2}/post-07-crew-league.jpg'],
         caption='''اعمل دوري فانتازي لصحابك واللي يطلع آخر الجولة… يعزم 😂
مجموعة خاصة مجانًا، بكود تبعته على جروب الشلة.
منشن الشلة اللي هتلعب معاها 👇

#فانتازي #دوري_فانتازي #FPL #سوبر_اب''',
         pin='مين في الشلة دايمًا في الآخر؟ 😂',
         notes='الجدول اللي في التصميم مثال مرسوم ومكتوب عليه EXAMPLE.'),
    # ---- carousels (6 slides, 4:5) ----
    dict(id='carousel-fantasy-rules', kind='carousel', title='قواعد الفانتازي في 6 شرايح', audience='لاعيبة الفانتازي', when='أول الأسبوع قبل الديدلاين',
         files=[f'{P2}/carousel-fantasy-rules/slide-{i}.jpg' for i in range(1, 7)],
         caption='''قواعد الفانتازي في 6 شرايح 📌
15 لاعب، 100 مليون، 3 بس من كل نادي، والكابتن نقطه دبل.
احفظ البوست وابعته لصاحبك اللي لسه بيسأل "النقط بتتحسب إزاي؟"

#فانتازي #فانتازي_الدوري_المصري #FPL #سوبر_اب''',
         pin='أكتر قاعدة بتنساها: الكابتن ولا التبديلات؟'),
    dict(id='carousel-khomasi-rules', kind='carousel', title='قواعد الخماسي اللي محدش كتبها', audience='لاعيبة الخماسي', when='الخميس بالليل',
         files=[f'{P2}/carousel-khomasi-rules/slide-{i}.jpg' for i in range(1, 7)],
         caption='''قواعد الخماسي اللي محدش كتبها… بس الكل ماشي عليها 😂
الحارس بالدور، صاحب الكورة مبيطلعش، وآخر جول بيكسب.
انت بتكسر أنهي قاعدة؟ منشن صاحبك 👇

#ماتش_خماسي #كورة #خماسي #سوبر_اب''',
         pin='قاعدة 4 ("هحوّلك بالليل") دي بتاعة مين عندكم؟ 😅'),
    # ---- reels (9:16, 1080×1920) ----
    dict(id='reel-01-khomasi-types', kind='reel', title='أنواع اللاعيبة في ماتش الخماسي', audience='لاعيبة الخماسي', when='أيام الأسبوع 8–11 م',
         files=['reels/01-khomasi-types/reel-01-khomasi-types-v2.mp4', 'reels/01-khomasi-types/reel-01-khomasi-types-v2-silent.mp4', 'reels/01-khomasi-types/cover-v2.png'],
         caption='''أنواع اللاعيبة في ماتش الخماسي 😂
منشن صاحبك اللي مبيباصيش… وقولنا انت رقم كام؟

#ماتش_خماسي #كورة #خماسي #سوبر_اب''',
         pin='رقم 5 ده انا كل أسبوع 😅'),
    dict(id='reel-02-tee-3d', kind='reel', title='براند تيشيرتات في 4 خطوات', audience='اللي عايزين يعملوا براند / مصممين', when='الخميس–السبت 9–11 م',
         files=['reels/02-tee-3d/reel-02-tee-3d.mp4', 'reels/02-tee-3d/reel-02-tee-3d-silent.mp4', 'reels/02-tee-3d/cover.png'],
         caption='''براند تيشيرتات من غير مخزن ولا مصنع؟ 👕
ارفع تصميمك، شوفه 3D، واحنا نطبع ونشحن… وانت تكسب من كل أوردر.
اكتب "براند" في الكومنتات وهنبعتلك أول ما نفتح.

#براند_مصري #طباعة_تيشيرتات #بيزنس_اونلاين #سوبر_اب''',
         pin='اكتب "براند" وهنبعتلك رسالة أول ما باب البائعين يفتح 👕',
         notes='رد على كل كومنت "براند" برسالة: دي قائمة البائعين المستنيين.'),
    dict(id='reel-03-fantasy-two-leagues', kind='reel', title='فانتازي المصري والإنجليزي في حساب واحد', audience='لاعيبة الفانتازي', when='الجمعة–السبت قبل جولة البريميرليج، وستوري الاتنين قبل ديدلاين المصري',
         files=['reels/03-fantasy-two-leagues/reel-03-fantasy-two-leagues.mp4', 'reels/03-fantasy-two-leagues/reel-03-fantasy-two-leagues-silent.mp4', 'reels/03-fantasy-two-leagues/cover.png'],
         caption='''فانتازي الدوري المصري والإنجليزي… في حساب واحد ⚽
فريق للمصري، فريق للبريميرليج، ودوري خاص لشلتك بكود.
تابعنا عشان تلعب أول ما نفتح.

#فانتازي #فانتازي_الدوري_المصري #FPL #سوبر_اب''',
         pin='بتلعب الاتنين ولا مصري بس؟ 👇',
         notes='متقولش أبدًا "أول فانتازي للدوري المصري".'),
    dict(id='reel-04-player-dropped', kind='reel', title='واحد اعتذر قبل الماتش بساعة', audience='كباتن ماتشات الخماسي', when='الخميس 6–8 م',
         files=['reels/04-player-dropped/reel-04-player-dropped.mp4', 'reels/04-player-dropped/reel-04-player-dropped-silent.mp4', 'reels/04-player-dropped/cover.png'],
         caption='''ماتش خماسي ناقصه لاعب قبلها بساعة؟ 😩
قريب على Super App: تنشر المكان الفاضي، واللي جنبك ينضم ويدفع نصيبه.
منشن الكابتن اللي بيلف على الأرقام كل أسبوع 👇

#ماتش_خماسي #كورة #خماسي #سوبر_اب''',
         pin='"معلش يا رجالة مش هقدر آجي" 🙃 مين بيقولها عندكم؟'),
    dict(id='reel-05-notebook-to-dashboard', kind='reel', title='من الكراسة للداشبورد', audience='أصحاب الملاعب', when='الصبح في أيام الأسبوع + ابعته واتساب لأصحاب الملاعب',
         files=['reels/05-notebook-to-dashboard/reel-05-notebook-to-dashboard.mp4', 'reels/05-notebook-to-dashboard/reel-05-notebook-to-dashboard-silent.mp4', 'reels/05-notebook-to-dashboard/cover.png'],
         caption='''لسه بتحجز ملعب الخماسي في الكراسة وعلى الواتساب؟ 📒
المواعيد ساعة بساعة، واللاعيب بيحجز ويدفع لوحده، وكل جنيه متسجل.
ابعتلنا رسالة وخلي ملعبك جاهز على Super App قبل الإطلاق.

#ملاعب_خماسي #حجز_ملاعب #بيزنس_رياضي #سوبر_اب''',
         pin='أصحاب الملاعب: لسه بتستخدموا الكراسة؟ 📒',
         notes='الأسماء في الكراسة والشات مثال، وأرقام الداشبورد من بيانات تجريبية.'),
    dict(id='reel-06-cta-comment', kind='reel', title='CTA: قولنا رأيك واستنى الإطلاق', audience='لاعيبة الكورة اللي بيستخدموا كذا أبلكيشن', when='أي يوم 8–11 م، ورد على الكومنتات أول ساعة',
         files=['reels/06-cta-comment/reel-06-cta-comment.mp4', 'reels/06-cta-comment/reel-06-cta-comment-silent.mp4', 'reels/06-cta-comment/cover.png'],
         caption='''تطبيق واحد للكورة كلها: حجز ملاعب، فانتازي، ماتشات، ومتجر ⚽
قولنا في الكومنتات: إيه أول حاجة نفسك فيها؟ 1، 2، 3 ولا 4؟
والإطلاق قرّب… خليك معانا.

#حجز_ملاعب #فانتازي #ماتش_خماسي #سوبر_اب''',
         pin='إحنا هنقرا كل الكومنتات… واللي يكسب هنبدأ بيه 👀',
         notes='من غير فويس أوفر دلوقتي (الترجمة على الشاشة). عد الإجابات عشان تعرف تبدأ الإطلاق بأنهي ميزة.'),
    dict(id='reel-07-collab-three', kind='reel', title='Collab: ماتش واحد، 3 حكايات', audience='لاعيبة الكورة (جمهور الملعب + الكابتن + البراند)', when='بعد موافقة الشركاء التلاتة',
         files=['reels/07-collab-three/reel-07-collab-three.mp4', 'reels/07-collab-three/reel-07-collab-three-silent.mp4', 'reels/07-collab-three/cover.png'],
         caption='''ماتش خماسي واحد… وتلات حكايات ⚽👕
ملعب، ولاعيبة، وبراند مصري، كلهم في مكان واحد.
تابعونا… الإطلاق قرّب.

#ماتش_خماسي #براند_مصري #حجز_ملاعب #سوبر_اب''',
         pin='مين عايز يبقى الكولاب الجاي؟ 👇',
         notes='⚠️ ممنوع ينزل بالشكل ده: أسماء الحسابات في الفيديو مؤقتة (@اسم_الملعب، @اسم_الكابتن، @اسم_البراند). ابعتلنا الحسابات الحقيقية بعد موافقتهم ونرندره تاني، وبعدين انشره كـ Collab وادعي التلاتة.'),
]

# Viral hooks: the first caption line is what shows before "...more", so it has to stop the scroll on its own.
# Patterns used: a pain everyone lived, a question that splits people, "tag the one who...", a challenge.
# Each keeps the main keyword and makes no claim we can't back. `alt` is a second hook to A/B test (repost / TikTok).
HOOKS = {
    'post-01-players': ('ماتش الخماسي ناقصه 2 والساعة 11 بالليل… مين عاش اللحظة دي؟ 😅', 'أسوأ جملة ممكن تتقال في جروب ماتش الخماسي: «ناقصنا 2» 😩'),
    'post-02-fantasy': ('بتلعب فانتازي الدوري المصري ولا الإنجليزي؟ ليه تختار أصلًا؟ 👀', 'فانتازي الدوري المصري والإنجليزي في أبلكيشن واحد؟ أيوه.'),
    'post-03-sellers': ('معاك 0 جنيه وعايز تعمل براند تيشيرتات؟ ده ممكن فعلًا 👕', 'براند تيشيرتات من غير ما تدفع جنيه في مخزن ولا مكنة؟'),
    'post-04-captain-gw7': ('كابتن الجولة 7 في فانتازي الدوري المصري: الاختيار الآمن ولا المجازفة؟ 👀', 'لو كابتنك في الجولة 7 زيزو… استنى اقرا ده الأول.'),
    'post-05-who-collects': ('في كل ماتش خماسي فيه واحد بيقول «هحوّلك بالليل»… وعمره ما بيحوّل 😂', 'مين هيلم فلوس ماتش الخماسي النهارده؟ (مش انت تاني 😩)'),
    'post-06-empty-hours': ('صاحب ملعب خماسي؟ الساعة الفاضية ممكن تخسرك 36,000 جنيه في الشهر (مثال) 😳', 'صاحب ملعب خماسي؟ احسبها معانا قبل ما تقفل البوست.'),
    'post-07-crew-league': ('اعمل دوري فانتازي لصحابك… واللي يطلع آخر الجولة يعزم الكل 😂', 'صاحبك اللي فاكر نفسه فاهم في الفانتازي؟ حطه في دوري معاك وخلّي الجدول يتكلم.'),
    'carousel-fantasy-rules': ('لو لسه مش فاهم النقط بتتحسب إزاي في الفانتازي… احفظ البوست ده 📌', 'قاعدة واحدة في الفانتازي ممكن تخسرك الجولة كلها (الشريحة 4).'),
    'carousel-khomasi-rules': ('قواعد ماتش الخماسي اللي محدش كتبها… بس الكل ماشي عليها 😂', 'انت بتكسر أنهي قاعدة من قواعد ماتش الخماسي؟ اعترف 😂'),
    'reel-01-khomasi-types': ('في كل ماتش خماسي فيه الخمسة دول… وانت واحد منهم 😂', 'منشن صاحبك اللي مبيباصيش في ماتش الخماسي… من غير ما تقول اسمه.'),
    'reel-02-tee-3d': ('عندك فكرة تيشيرت؟ خليها براند في 4 خطوات… من غير مخزن ولا مصنع 👕', 'متشتريش مكنة طباعة قبل ما تشوف ريل براند التيشيرتات ده.'),
    'reel-03-fantasy-two-leagues': ('فانتازي الدوري المصري ولا الإنجليزي؟ ليه تختار… العب الاتنين من حساب واحد ⚽', 'فريقين فانتازي، مصري وإنجليزي… وحساب واحد.'),
    'reel-04-player-dropped': ('ماتش الخماسي الساعة 9… وواحد اعتذر الساعة 8 😩 حصلتلك؟', 'أسوأ رسالة في جروب ماتش الخماسي: «معلش يا رجالة مش هقدر آجي».'),
    'reel-05-notebook-to-dashboard': ('لسه بتحجز ملعب الخماسي في الكراسة؟ يبقى أكيد حصلك ده 📒', 'حجزين على نفس الساعة في ملعبك الخماسي… والخناقة على مين؟'),
    'reel-06-cta-comment': ('4 أبلكيشنات عشان ماتش كورة واحد؟ لا بقى 😤 حجز ملاعب وفانتازي ومتجر في تطبيق واحد', 'لو هتختار ميزة واحدة بس في تطبيق كورة… هتختار إيه؟'),
    'reel-07-collab-three': ('ماتش خماسي واحد… وتلات حكايات ⚽👕', 'ملعب، وكابتن، وبراند مصري… في ماتش خماسي واحد.'),
}
for _it in ITEMS:
    _h, _alt = HOOKS[_it['id']]
    _it['caption'] = _h + '\n' + _it['caption'].split('\n', 1)[1]
    _it['alt'] = _alt

KIND = {'post': 'بوست', 'carousel': 'كاروسيل', 'reel': 'ريل'}
ORDER = ['post-04-captain-gw7', 'reel-01-khomasi-types', 'post-05-who-collects', 'reel-03-fantasy-two-leagues', 'post-06-empty-hours',
         'carousel-fantasy-rules', 'reel-04-player-dropped', 'post-07-crew-league', 'carousel-khomasi-rules', 'reel-02-tee-3d',
         'post-01-players', 'reel-05-notebook-to-dashboard', 'post-02-fantasy', 'reel-06-cta-comment', 'post-03-sellers', 'reel-07-collab-three']

def caption_txt(it):
    out = [f'{KIND[it["kind"]]}: {it["title"]}', f'الجمهور: {it["audience"]}', f'أحسن وقت: {it["when"]}', '', 'الكابشن:', it['caption'], '', 'أول كومنت (ثبّته):', it['pin'], '', 'هوك بديل (جرّبه A/B أو على تيك توك):', it['alt']]
    if it.get('notes'): out += ['', 'قبل النشر:', it['notes']]
    return '\n'.join(out) + '\n'

def markdown():
    md = ['# كابشنات Super App — كل البوستات والكاروسيل والريلز', '',
          f'اتعمل {datetime.date.today():%d-%m-%Y}. المصدر: `marketing/export.py` (عدّل هناك وشغّل `python3 marketing/export.py`).', '',
          '**الهوك:** أول سطر هو اللي بيبان قبل «المزيد»، فلازم يوقف السكرول لوحده: وجع كلنا عشناه، سؤال يقسم الناس، «منشن اللي…»، أو تحدي. ولكل واحد هوك بديل تجربه.', '',
          '**القواعد:** الكلمة المفتاحية في أول سطر · عامية مصرية · CTA واحد · 3–5 هاشتاجات · الجوايز من الرعاة بس · مفيش "أول فانتازي".', '',
          '## ترتيب النشر المقترح (أسبوعين)', '']
    by = {i['id']: i for i in ITEMS}
    md += [f'{n}. **{KIND[by[k]["kind"]]}** — {by[k]["title"]} · {by[k]["when"]}' for n, k in enumerate(ORDER, 1)]
    for kind, head in (('post', 'البوستات'), ('carousel', 'الكاروسيل'), ('reel', 'الريلز')):
        md += ['', f'## {head}']
        for it in [i for i in ITEMS if i['kind'] == kind]:
            md += ['', f'### {it["title"]} (`{it["id"]}`)', f'**الجمهور:** {it["audience"]} · **أحسن وقت:** {it["when"]}', '', '```', it['caption'], '```',
                   f'**هوك بديل (A/B):** {it["alt"]}', f'**أول كومنت (ثبّته):** {it["pin"]}']
            if it.get('notes'): md.append(f'**قبل النشر:** {it["notes"]}')
    return '\n'.join(md) + '\n'

if __name__ == '__main__':
    missing = [f for it in ITEMS for f in it['files'] if not os.path.exists(os.path.join(HERE, f))]
    if missing: raise SystemExit('missing files:\n' + '\n'.join(missing))
    md = markdown()
    open(os.path.join(HERE, 'CAPTIONS.md'), 'w').write(md)
    os.makedirs(os.path.join(HERE, 'export'), exist_ok=True)
    root = f'SuperApp-content-{datetime.date.today():%Y-%m-%d}'
    zp = os.path.join(HERE, 'export', root + '.zip')
    folder = {'post': '1-posts', 'carousel': '2-carousels', 'reel': '3-reels'}
    with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr(f'{root}/00-CAPTIONS.md', md)
        z.writestr(f'{root}/00-CAPTIONS.txt', md.replace('```', ''))
        for it in ITEMS:
            base = f'{root}/{folder[it["kind"]]}/{it["id"]}'
            z.writestr(f'{base}/caption.txt', caption_txt(it))
            for f in it['files']:
                name = os.path.basename(f)
                if it['kind'] == 'reel':   # clean names inside the pack
                    name = 'cover.png' if name.startswith('cover') else ('reel-no-sound.mp4' if 'silent' in name else 'reel.mp4')
                z.write(os.path.join(HERE, f), f'{base}/{name}', compress_type=zipfile.ZIP_STORED if name.endswith('.mp4') else zipfile.ZIP_DEFLATED)
    print('wrote', os.path.relpath(zp, HERE), f'({os.path.getsize(zp) / 1e6:.1f} MB)', 'and CAPTIONS.md')
