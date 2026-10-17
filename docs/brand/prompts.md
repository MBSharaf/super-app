# Prompts & Video Stack — مكتبة البرومبتات والأدوات

البرومبتات اللي اخترناها من المكتبات الخارجية، متعدلة لـ Super App وجاهزة تتشغل. كل برومبت هنا بيمشي على `CLAUDE.md` وعلى أقسام `audiences.md` و`content-strategy.md` و`social-video.md`.

## الفكرة الأساسية: فيديو معمول بالكود
أغلب الأمثلة اللي جمعناها بتشتغل بنفس الطريقة. Claude بيكتب الفيديو **كود** (HTML/CSS/JS أو React)، وبعدين أداة بتحوّله لـ MP4. الطريقة دي مناسبة لينا جدًا:
- **مظبوط على البراند:** بيقرا ألوانا وخطوطنا وكومبوننتس الموقع الحقيقية من الريبو.
- **ثابت:** كل مرة نفس النتيجة، والتعديل سهل ("غيّر السعر لـ 500"، "ابطّأ المشهد التالت").
- **رخيص:** مفيش تكلفة توليد فيديو بالـ AI في الأجزاء الجرافيك.
- **أحسن حاجة للـ explainers والـ UI والأرقام والإنفوجرافيك.**

### الأدوات (Stack)
| الدور | الأداة | ملاحظات |
|---|---|---|
| العقل | Claude Opus 5.5 داخل Claude Code أو Cursor، **وجوه الريبو ده** | لازم يشتغل والريبو مفتوح عشان يشوف `CLAUDE.md` والتوكنز |
| فيديو بالكود (الأساسي) | **HyperFrames** (HeyGen): `npx skills add heygen-com/hyperframes` ثم `/hyperframes` | HTML + GSAP → MP4. بيعمل موشن جرافيك وكلام متحرك ولقطات UI |
| فيديو بالكود (بديل) | **Remotion** (React → MP4) | ينفع ناخد كومبوننتس الموقع زي ما هي. **الترخيص:** مجاني للأفراد والشركات الصغيرة لحد 3 موظفين، وبعد كده محتاج company license |
| الصوت | صوت إنسان مصري حقيقي (الأفضل)، أو ElevenLabs أو OpenAI TTS | الصوت الصناعي يبقى طبيعي جدًا وبالعامية، وإلا نستخدم صوت حقيقي |
| لقطات حقيقية (b-roll) | موبايل في الملعب + Veo / Runway / Seedance للخلفيات بس | **ممنوع** وشوش أو لاعيبة مولدة بالـ AI (القاعدة في content-strategy) |
| 3D | three.js + موديلات `models/*.glb` الموجودة | التيشيرت 3D بيلف بالتصميم. Blender للحاجات الكبيرة بس |

## الستايلات اللي هنستخدمها
| الستايل | يعني إيه | نستخدمه في |
|---|---|---|
| **HyperFrames kinetic UI** | كلام بيتحرك + كروت UI حقيقية + أرقام بتعد، بحركة البراند (spring، pitch-line wipe) | إطلاق ميزة، explainers، أرقام لأصحاب الملاعب |
| **One take** | لقطة واحدة متصلة من غير قطع: الكاميرا بتعدّي من مشهد للتاني | "من الحجز للتذكرة في لقطة واحدة"، جولة في التطبيق، جولة في المصنع |
| **Collage b-roll** | قص ولزق: صور حقيقية مقصوصة زي الاستيكرز + لقطات موبايل + ورق وشريط لاصق وخط باليد | ثقافة الخماسي، الميمز، الديربي. **أقرب ستايل للإحساس الإنساني** |
| **Character animation** (زي أسلوب Clawd) | شخصية كرتونية بسيطة بتحكي | لو عملنا ماسكوت خاص بينا (كورة أو صفارة). **منقلدش** Clawd ولا أي شخصية تانية |

## البرومبتات الجاهزة

### P1 — Launch / Hype (من أمثلة الموشن 2 و3 و7)
**الجمهور:** لاعيبة الكورة. **المقاس:** 9:16 و16:9. **المدة:** 15–20 ثانية.
```
/hyperframes Make a punchy 20-second launch video for Super App, vertical 1080×1920 (also export 1920×1080).
Read CLAUDE.md, docs/brand/*.md and css/tokens.css first and follow them exactly.
Take real components and screens from this repo (index.html, js/components, css/components.css) instead of inventing UI.
Audience: Egyptian amateur footballers. Message: "كل الكورة في مكان واحد" — book a pitch, fill your match, play fantasy, shop.
Hook in the first 1.5s with a real pain ("الماتش ناقصه 2؟"), never open on the logo.
Four fast beats, one per product (booking → find players → fantasy → shop), each showing the real UI, then an end card with the brand mark and one CTA pill.
Arabic on screen (Cairo 800), burned-in captions, safe zones from social-video.md, blaze only once, spring motion.
Sound: synthesized whistle, ball hitting the net and a UI "ding"; no generic stock music.
Clean and confident, made by a human designer: no gradients, glitches, lens flares or fake 3D players.
```

### P2 — Explainer (من التعليمي 2 و5)
**الجمهور:** أصحاب الملاعب. **المقاس:** 16:9 للـ LinkedIn واليوتيوب، و4:5 للفيسبوك. **المدة:** 45–60 ثانية.
```
/hyperframes Make a 50-second explainer for pitch owners, 1920×1080 plus a 1080×1350 cut.
Read CLAUDE.md and docs/brand/*.md; use the partner dashboard screens from this repo (js/pages/dash/*, css/dash.css) as the visuals.
Explain simply: empty hours cost money → Super App fills them (online booking, QR check-in, weekly memberships, discount codes for quiet hours, 30-day revenue forecast, Monday payouts).
Structure: problem (a paper booking notebook and missed WhatsApp messages) → 4 features with one real number each → result → CTA "سجّل ملعبك".
All graphics in HTML/CSS with the brand tokens; numbers count up; charts use turf and ink only.
Narration script in Egyptian Arabic, warm and practical, 130 words max; mark it for a human voice-over.
No claims we can't prove; keep sample numbers clearly illustrative.
```

### P3 — LinkedIn بمرجع screenshots (من التعليمي 3)
**الجمهور:** المصانع والشركات والرعاة. **المقاس:** 1080×1350. **المدة:** 10–15 ثانية.
```
Make a 12-second 1080×1350 video for a LinkedIn post using Remotion (or HyperFrames).
Reference: the 2 screenshots I attach — match their layout rhythm, not their colours.
Brand: read CLAUDE.md and css/tokens.css. Audience: print factory owners.
Message: Super App sends ready print jobs with deadlines, batches and automatic courier booking; you print and get paid per piece.
Show the real factory screens (js/pages/factory/*) animating: a job card moving New → Printing → QC → Packing → Ready.
English + Arabic headline, one CTA, no music, subtle UI sounds.
```

### P4 — One-take walkthrough (ستايل One take)
**الجمهور:** لاعيبة الكورة. **المقاس:** 9:16. **المدة:** 20 ثانية.
```
/hyperframes Make a 20-second 1080×1920 "one take" video: one continuous camera move through the Super App booking flow with no cuts.
Read CLAUDE.md and docs/brand/*.md first. Use the real screens: search area → pitch card → date and time chips → checkout → ticket with QR.
The camera glides and zooms between UI elements; each element pops in with the spring ease as the camera reaches it.
On-screen keyword: "حجز ملعب في 30 ثانية"; a timer in the corner counts 0→30.
End on the ticket, then the CTA pill "احجز دلوقتي".
```

### P5 — Collage b-roll (ستايل Collage)
**الجمهور:** لاعيبة الكورة والكباتن. **المقاس:** 9:16. **المدة:** 15 ثانية.
```
Make a 15-second 1080×1920 collage-style reel: "أنواع اللاعيبة في ماتش الخماسي".
Read CLAUDE.md and docs/brand/*.md. Use the real phone photos and clips I provide as cut-out stickers with a white border, torn paper, tape and hand-drawn arrows in blaze.
5 player types, 3 seconds each, each with a short Egyptian Arabic label (e.g. "اللي مبيباصيش", "الحارس بالدور").
Stop-motion feel at 12fps for the cut-outs; captions at 30fps. Paper texture from the brand paper colour.
End card: "تاجّ صاحبك ده 👇" + Super App mark (no CTA pill; this one is for shares).
```

### P6 — Data reel (من الموشن 6 و7 + الإنفوجرافيك)
**الجمهور:** أصحاب الملاعب أو الرعاة. **المقاس:** 9:16 و4:5.
```
/hyperframes Make a 15-second data reel: "الساعة الفاضية في ملعبك بتخسرك كام في الشهر؟"
Read CLAUDE.md and the forecast feature (js/pages/dash/*). Illustrative inputs: 3 empty hours a day × 400 EGP × 30 days = 36,000 EGP.
Numbers count up, a heatmap of the week fills hour by hour (turf = booked, surface-3 = empty), then empty cells flip to booked.
Label all numbers as an example. CTA "سجّل ملعبك".
```

## اللي مش هنستخدمه دلوقتي (ومليش)
- **Session-story** (`/session-story`): بيحوّل تاريخ جلساتك مع Claude Code لفيديو. ده بيحكي إزاي المبرمج بيشتغل، مش عن المنتج. ممكن نستخدمه مرة لمحتوى "بنبني المنصة قدامكم"، بس **بعد ما نراجعه** عشان ميطلعش أي حاجة داخلية أو بيانات سرية.
- **وكيل بيتحكم في Blender + Runway + Veo + Remotion لوحده:** تقيل ومكلف، والنتيجة شكلها AI. ينفع بس للـ 10% السينمائي (هايب الموسم) وبإشراف.
- **8 ساعات شغل متواصل (DesignLoop + ElevenLabs):** مبالغة لريل 15 ثانية. إحنا عايزين قطع كتير بسرعة، مش قطعة واحدة مثالية.
- **"Go all out" من غير brief:** بيطلع استعراض مهارة مش رسالة. دايمًا نحدد الجمهور والرسالة والـ CTA.

## أدوات المواقع (Web tools)
| الأداة | الحكم | ليه |
|---|---|---|
| **Motion** (Framer Motion) | ✅ | حركة الموقع والتطبيق لما ننقل لـ React. نفس قيم الـ easing بتاعتنا |
| **Textures** (grain/paper) | ✅ | طبقة خفيفة فوق الجرافيك بتبعده عن شكل الـ AI اللامع |
| **Particles** | ⚠️ قليل | كونفيتي لحظة الهدف أو اكتمال الماتش بس، مش في الخلفية |
| **Shaders** | ⚠️ قليل | خلفية نجيلة أو كشافات خفيفة. ممنوع الجريدينت الملونة اللي شكلها AI |
| **21st.dev** | ⚠️ | كومبوننتس React جاهزة للـ landing pages. نعيد تلوينها بالتوكنز بتاعتنا دايمًا |
| **daisyUI** | ❌ | عندنا design system خاص بينا، وهيبوّظ الهوية |
| **60fps** | ✅ للإلهام | مراجع موشن للتطبيقات |
| **Google Maps scraper** | ⚠️ قانونيًا | لقوايم الملاعب (B2B). سحب البيانات من خرايط جوجل ضد شروطها. الأسلم Google Places API أو جمع يدوي، والتواصل يكون بموافقة |
| is.graphics, contentcore.xyz, scrolltide, horizonx, kitchen, originKit | ؟ | مش متأكد إيه هما بالظبط. محتاجين اللينك قبل ما نحكم |
| "lemo opuscar" (ستايل) | ؟ | معرفتوش. محتاجين مثال |
