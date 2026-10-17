# Reel 01 — أنواع اللاعيبة في ماتش الخماسي

**Audience:** footballers (5-a-side regulars) · **Pillar:** ثقافة ماتش الخماسي · **Goal:** shares and follows before launch
**Format:** 9:16, 1080×1920, 30 fps, 15 s · **Style:** collage (docs/brand/prompts.md P5) · made in code

| File | What |
|---|---|
| `reel-01-khomasi-types-v2.mp4` | **Current version (v2):** collage style from `docs/brand/references.md`, full 104 BPM beat + paper/tape/stamp/whoosh foley, -14.5 LUFS |
| `reel-01-khomasi-types-v2-silent.mp4` | v2 without sound, for adding trending audio inside Instagram/TikTok |
| `cover-v2.png` | v2 cover frame (hook) |
| `reel-01-khomasi-types.mp4`, `-silent.mp4`, `cover.png` | v1 (flat style, effects only) — kept for comparison |
| `index.html` | The composition: `window.render(t)` draws any frame; open it in a browser to scrub |
| `render.js` | `node render.js` → MP4 (Playwright frames + ffmpeg); `--frames=1.3,5` saves stills |
| `sfx.py` | Generates `sfx.wav`: the beat and foley (no licensed audio used) |

## Beats
| Time | On screen |
|---|---|
| 0.0–1.6 | Hook on torn paper: "في كل ماتش خماسي / فيه الخمسة دول" (keyword "ماتش خماسي" also on the tag) |
| 1.6–4.0 | #10 · اللي مبيباصيش — "الكورة بتاعته من البيت" |
| 4.0–6.4 | #1 · الحارس بالدور — "خمس دقايق بس وراجع قدام" |
| 6.4–8.8 | #7 · اللي جاي متأخر — "أنا تحت البيت… من ساعة" |
| 8.8–11.2 | #8 · المحلل — "لو كنت باصيت شمال كان جول" |
| 11.2–13.6 | #5 · اللي بيلم الفلوس — "يا رجالة اللي مدفعش الـ 45 جنيه" + "على Super App كل واحد بيدفع نصيبه" |
| 13.6–15.0 | "منشن صاحبك ده — وانت… رقم كام فيهم؟" + mark + "كل الكورة في مكان واحد" |

Shirts are the app's own original kit drawing (`SA.ui.c.kit`), in generic colours — no club colours or crests.

## Caption (copy as is)
```
أنواع اللاعيبة في ماتش الخماسي 😂
منشن صاحبك اللي مبيباصيش… وقولنا انت رقم كام؟

#ماتش_خماسي #كورة #خماسي #سوبر_اب
```
- Post on TikTok + Instagram Reels (+ Facebook Reels, YouTube Shorts with the same caption).
- Pin a first comment: "رقم 5 ده انا كل أسبوع 😅" to start replies.
- Best window: weekday evenings 8–11 pm Cairo, when the 5-a-side crowd is on their phones before or after games.

## Next version
Swap the drawn shirts for real cut-out photos of real players (with their permission) — the collage style is strongest with real faces.
