# Week 1 posts — 3 posts (18 Oct 2026)

4:5, 1080×1350, same collage system as `../2026-10-17-teasers/`. Real app screens (served locally), drawn graphics, no internet or AI photos.

| File | Audience | Idea | CTA | Publish |
|---|---|---|---|---|
| `post-04-captain-gw7` | Fantasy players | كابتن الجولة 7 (الدوري المصري): Zizo, Nasser Maher, and the surprise Mahmoud Mamdouh | Comment your captain | **Before Mon 19 Oct** |
| `post-05-who-collects` | Team captains / organisers | مين هيلم الفلوس؟ A match receipt (14 × 95 EGP, 3 still owe) + the real match page where everyone pays their share | تابعنا | Any weekday evening |
| `post-06-empty-hours` | Pitch owners (launch phase 0) | الساعة الفاضية بتخسرك كام؟ A week grid with empty afternoons, example math, the real forecast dashboard | ابعتلنا رسالة | Weekday morning (Facebook groups + LinkedIn) |

**Facts checked 17 Oct 2026:** round 7 fixtures and times from youm7, sinai.news and alouroba (ZED × Modern Sport Tue 20 Oct 5 pm; Pyramids × Talaea El Gaish Tue 20 Oct 8 pm; Al Ahly × El Qanah Wed 21 Oct 8 pm). Goal tallies differ between sources (youm7 has Mahmoud Mamdouh on 6, others list several players on 3), so the post prints no goal numbers. Re-check times on the morning of posting.

## Captions

**Post 04**
```
كابتن الجولة 7 في فانتازي الدوري المصري 👀
زيزو قدام القناة؟ ناصر ماهر قدام طلائع الجيش؟ ولا المفاجأة محمود ممدوح قدام زد؟
كابتنك مين؟ اكتبه في الكومنتات، وبعد الجولة نشوف مين صدق.

#فانتازي_الدوري_المصري #فانتازي #الدوري_المصري #سوبر_اب
```

**Post 05**
```
مين هيلم الفلوس النهارده؟
14 لاعب × 95 جنيه… وكل أسبوع 3 لسه "هحوّلك بالليل".
قريب على Super App: كل واحد بيدفع نصيبه وهو بينضم للماتش، والكابتن يرتاح.
منشن الكابتن بتاعكم 👇

#ماتش_خماسي #كورة #كابتن_الماتش #سوبر_اب
```

**Post 06**
```
الساعة الفاضية في ملعبك بتخسرك كام؟
مثال: 3 ساعات فاضية في اليوم × 400 جنيه × 30 يوم = 36,000 جنيه في الشهر.
داشبورد Super App بيوريك الساعات الهادية، ويقترح عليها عرض، وبيملاها بحجوزات أونلاين.
لو عندك ملعب، ابعتلنا رسالة.

#ملاعب_مصر #حجز_ملاعب #ملاعب_خماسي #سوبر_اب
```

## Carousels (6 slides each, 4:5)

Rebuild: `node carousel.js` (both) — slides land in `carousel-*/slide-1..6.png/.jpg`. Upload the slides in order.

| Folder | Audience | Slides | CTA |
|---|---|---|---|
| `carousel-fantasy-rules/` | Fantasy players | 1 hook · 2 fifteen players for 100m (2/5/5/3) · 3 max 3 per club, 11 + 4 bench, formations · 4 captain ×2, vice steps in · 5 scoring table · 6 one free transfer, extra −4, Egyptian + English in one app | Save + send to a friend |
| `carousel-khomasi-rules/` | Footballers | 1 hook · 2 keeper by turns · 3 the ball owner never comes off · 4 "last goal wins" · 5 "I'll transfer it tonight" · 6 which rule do you break + Super App split payment | Tag your friend |

Fantasy rules are the app's own (`js/data/fantasy.js` rules: budget 100, squad 2/5/5/3, max 3 per club, 1 free transfer, −4 each extra, scoring table; captain ×2 with vice fallback in `fantasyService.js`).

**Caption — fantasy rules**
```
قواعد الفانتازي في 6 شرايح 📌
15 لاعب، 100 مليون، 3 بس من كل نادي، والكابتن نقطه دبل.
احفظ البوست وابعته لصاحبك اللي لسه بيسأل "النقط بتتحسب إزاي؟"

#فانتازي #فانتازي_الدوري_المصري #FPL #سوبر_اب
```

**Caption — 5-a-side rules**
```
قواعد الخماسي اللي محدش كتبها… بس الكل ماشي عليها 😂
الحارس بالدور، صاحب الكورة مبيطلعش، وآخر جول بيكسب.
انت بتكسر أنهي قاعدة؟ منشن صاحبك 👇

#ماتش_خماسي #كورة #خماسي #سوبر_اب
```
