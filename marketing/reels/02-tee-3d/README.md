# Reel 02 — من فكرة لبراند تيشيرتات في 4 خطوات

**Audience:** people who want to start a clothing brand / designers (sellers) · **Pillar:** اعمل براندك · **Goal:** seller sign-ups and saves before launch
**Format:** 9:16, 1080×1920, 30 fps, 15 s · **Style:** collage on ink (docs/brand/references.md) · made in code, the tee is our own 3D model (`models/tee.glb`)

| File | What |
|---|---|
| `reel-02-tee-3d.mp4` | The reel, with the 104 BPM beat + paper/tape/stamp/ding foley, about -14 LUFS |
| `reel-02-tee-3d-silent.mp4` | Same, no sound, for adding trending audio inside Instagram/TikTok |
| `cover.png` | Designed cover (series look from `../_kit/covers.html`, title inside the 4:5 grid crop) |
| `index.html` | The composition: `window.render(t)` draws any frame; open it in a browser to scrub |
| `tee-turn.html` | Renders `models/tee.glb` with three.js, prints the design as a decal: `?a=<turn 0..1>&print=0\|1&c=<hex>` |
| `render.js` | `node render.js` → MP4 (Playwright frames + ffmpeg); `--frames=1.3,5` saves stills |
| `sfx.py` | Generates `sfx.wav`: beat and foley (no licensed audio) |

`tee-frames/` (blank.png + turn-000…089.png) is not committed: 16 MB and regenerable. Serve the repo root on
`127.0.0.1:8765`, then screenshot `tee-turn.html?print=0` → `blank.png` and `?print=1&a=i/90` → `turn-NNN.png`
for i = 0…89 at 1080×1080 with a transparent background (Chromium flags `--use-gl=angle --use-angle=swiftshader --enable-unsafe-swiftshader --ignore-gpu-blocklist`).

## Beats
| Time | On screen |
|---|---|
| 0.0–1.6 | Hook: "عندك فكرة / تيشيرت؟ / خليها براند في 4 خطوات" (keyword on the tag too) |
| 1.6–4.0 | 01 ارفع تصميمك — the "خماسي" design drops in as a taped sticker |
| 4.0–8.0 | 02 شوفه 3D — the sticker flies onto the blank tee, the print appears, one full turn |
| 8.0–10.8 | 03 إحنا بنطبع ونشحن — box + "تم الشحن" stamp; "انت مش محتاج مخزن ولا مكنة" |
| 10.8–13.4 | 04 وإنت بتكسب — three order toasts + "ربحك وصل المحفظة" |
| 13.4–15.0 | "افتح براندك / بـ 0 جنيه / من غير مخزن ولا مصنع" + mark + "قريب · كل الكورة في مكان واحد" |

The "خماسي" print is our own drawing — no club crest, kit or player.

## Caption (copy as is)
```
براند تيشيرتات من غير مخزن ولا مصنع؟ 👕
ارفع تصميمك، شوفه 3D، واحنا نطبع ونشحن… وانت تكسب من كل أوردر.
اكتب "براند" في الكومنتات وهنبعتلك أول ما نفتح.

#براند_مصري #طباعة_تيشيرتات #بيزنس_اونلاين #سوبر_اب
```
- One CTA: comment "براند". Reply to each comment by DM (that list is the seller waitlist).
- Post on Instagram Reels + TikTok (+ Facebook Reels, YouTube Shorts with the same caption).
- Best window: Thu–Sat evenings 9–11 pm Cairo.
- Claims match the product: `js/pages/sell.js` ("no stock, no upfront cost", the print partner makes and ships), `js/data/i18n-ar-sellers.js` ("إحنا بنطبع ونشحن", "الربح في محفظتك", profit released after delivery). Re-check them before posting if the seller terms change.
