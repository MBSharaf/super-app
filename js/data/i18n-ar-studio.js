/** Arabic dictionary — part 12: design studio round (3D garments, front/back artwork, size charts, product details, 0-profit prices, store page for the owner). */
(function (SA) {
  const D = SA.data.i18nAr;
  Object.assign(D.strings, {
    /* ---------- Studio ---------- */
    'Dashboard': 'اللوحة', 'Back to dashboard': 'ارجع للوحة التحكم',
    'PNG with a transparent background works best · up to {0} MB · {1} px wide for sharp prints. The front and the back can have different designs.': 'أحسن حاجة PNG بخلفية شفافة · لحد {0} ميجا · عرض {1} بكسل علشان الطباعة تطلع حادة. ممكن يبقى في تصميم قدام وتصميم تاني ورا.',
    'The fabric, fit, print and size chart are added to the product page for you.': 'نوع القماش والمقاس ونوع الطباعة وجدول المقاسات بيتضافوا لصفحة المنتج لوحدهم.',
    'We review every design, usually within {0} hours. It shows in your store and in the shop once it’s approved. By sending it you confirm you own the rights to this artwork.': 'بنراجع كل تصميم، غالباً في خلال {0} ساعة. هيظهر في متجرك وفي المتجر العام أول ما يتوافق عليه. لما تبعته إنت بتأكد إن حقوق التصميم ده بتاعتك.',
    'Pro sellers are reviewed first — usually within a few hours. It shows in your store and in the shop once it’s approved. By sending it you confirm you own the rights to this artwork.': 'بائعين Pro بيتراجعوا الأول — غالباً في خلال ساعات. هيظهر في متجرك وفي المتجر العام أول ما يتوافق عليه. لما تبعته إنت بتأكد إن حقوق التصميم ده بتاعتك.',
    'Elite sellers publish straight away; we spot-check later. It shows in your store and in the shop once it’s approved. By sending it you confirm you own the rights to this artwork.': 'بائعين Elite بينشروا على طول؛ وبنراجع عينات بعدين. هيظهر في متجرك وفي المتجر العام أول ما يتوافق عليه. لما تبعته إنت بتأكد إن حقوق التصميم ده بتاعتك.',
    'Add a front design': 'ضيف تصميم قدام', 'Add a back design': 'ضيف تصميم ورا', 'Use the back design here': 'استخدم تصميم الضهر هنا', 'Use the front design here': 'استخدم تصميم القدام هنا',
    'Front design': 'تصميم القدام', 'Back design': 'تصميم الضهر', 'Optional · back print +{0} {1}': 'اختياري · طباعة الضهر +{0} {1}', 'Upload or drop an image here': 'ارفع أو اسحب صورة هنا',
    'Same as the front': 'نفس تصميم القدام', 'Same as the back': 'نفس تصميم الضهر',
    '{0}×{1} px': '{0}×{1} بكسل', '{0}×{1} px · same as the front': '{0}×{1} بكسل · نفس تصميم القدام',
    'Replace the front design': 'غيّر تصميم القدام', 'Replace the back design': 'غيّر تصميم الضهر', 'Upload the front design': 'ارفع تصميم القدام', 'Upload the back design': 'ارفع تصميم الضهر',
    'Front design added': 'تصميم القدام اتضاف', 'Back design added': 'تصميم الضهر اتضاف', 'Front design removed': 'تصميم القدام اتشال', 'Back design removed': 'تصميم الضهر اتشال',
    'At least {0} {1} — the base price of this product.': 'على الأقل {0} {1} — ده سعر الأساس للمنتج ده.', 'At most {0} {1}.': 'بحد أقصى {0} {1}.',
    'You earn nothing per item — that’s fine if it’s for you.': 'مش هتكسب حاجة في القطعة — وده عادي لو هتشتريه لنفسك.', 'From {0} {1} (no profit) to {2} {3}': 'من {0} {1} (من غير مكسب) لحد {2} {3}',
    'Back print': 'طباعة الضهر', 'Base (front + back)': 'الأساس (قدام + ورا)', 
    'The price can’t be below the base price ({0} {1}).': 'السعر ماينفعش يقل عن سعر الأساس ({0} {1}).', 'The price can’t be above {0} {1}.': 'السعر ماينفعش يزيد عن {0} {1}.',
    'Loading 3D…': 'بنحمّل الـ 3D…', 'Drag to turn · drag the print to move it': 'اسحب علشان تلف · اسحب الطباعة علشان تحركها',
    /* ---------- Product details & size chart ---------- */
    'Size chart': 'جدول المقاسات', 'Size chart (cm)': 'جدول المقاسات (سم)', 'Chest': 'الصدر', 'Length': 'الطول', 'Sleeve': 'الكم',
    'Measured flat in cm: chest is armpit to armpit, length is shoulder to hem. ±{0}–{1} cm.': 'القياس والقطعة مفرودة بالسنتي: الصدر من تحت الباط للباط، والطول من الكتف لآخر القطعة. ±{0}–{1} سم.',
    'Fit': 'المقاس', 'Neck': 'الرقبة', 'Care': 'العناية', 'Printed on': 'مطبوع على', 
    '100% combed cotton, 180 g/m² single jersey': 'قطن ممشط 100%، جيرسيه 180 جرام/م²', 'Regular fit, true to size': 'قصّة عادية، المقاس مظبوط',
    'DTF transfer — bright colours, soft feel, doesn’t crack': 'طباعة DTF — ألوان قوية وملمس ناعم ومابتتشققش', 'Ribbed crew neck, taped shoulder seams': 'رقبة مدوّرة ريب، وخياطة الكتف مقوّاة بشريط',
    'Wash inside out at 30°C, don’t iron on the print, no tumble dryer': 'اغسله مقلوب على 30 درجة، ماتكويش على الطباعة، ومن غير نشّافة',
    '100% heavyweight cotton, 240 g/m², pre-shrunk': 'قطن تقيل 100%، 240 جرام/م²، مش بينكمش', 'Oversized and boxy with dropped shoulders — take your usual size for the loose look': 'أوفر سايز واسع وكتف نازل — خد مقاسك العادي علشان يطلع واسع',
    'Thick 2.5 cm ribbed collar': 'ياقة ريب تخينة 2.5 سم', '80% cotton, 20% polyester brushed fleece, 320 g/m² — warm and soft inside': 'خليط 80% قطن و20% بوليستر مبطّن، 320 جرام/م² — دافي وناعم من جوه',
    'Relaxed fit with dropped shoulders': 'قصّة مريحة وكتف نازل', 'Double-lined hood, kangaroo pocket, ribbed cuffs and waistband': 'كابيشون دبل، جيب كانجارو، وأساور ووسط ريب',
    /* ---------- Store page for the owner ---------- */
    'Only you can see this': 'إنت بس اللي شايف ده', 'All my designs': 'كل تصميماتي', 'Untitled design': 'تصميم من غير اسم',
    'These designs aren’t in your store yet. A design shows here for customers once the Super App team approves it (usually within {0} hours).': 'التصميمات دي لسه مش في متجرك. التصميم بيظهر للعملاء هنا أول ما فريق Super App يوافق عليه (غالباً في خلال {0} ساعة).',
  });
  D.patterns.push(
    ['^(.+) \\+ front print$', '{t1} + طباعة قدام'],
    ['^Level discount \\((.+)\\)$', 'خصم المستوى ({t1})'],
    ['^“(.+)” is in review — it shows in your store once it’s approved$', '«{1}» تحت المراجعة — هيظهر في متجرك أول ما يتوافق عليه'],
    ['^(.+) · in centimetres\\. Lay a T-shirt you like flat and compare\\.$', '{t1} · بالسنتي. افرد تيشيرت بتحبه وقارن.'],
    ['^(.+) — 3D view\\. Drag the garment to turn it, or the print to move it\\.$', '{t1} — عرض 3D. اسحب القطعة علشان تلفها، أو اسحب الطباعة علشان تحركها.'],
    ['^(.+) — 3D view\\. Drag or use the arrow keys to turn it\\.$', '{t1} — عرض 3D. اسحب أو استخدم الأسهم علشان تلفها.'],
    ['^(.+) preview$', 'معاينة {t1}'],
  );
})(window.SA);
