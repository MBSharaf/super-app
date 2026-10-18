# Reel 07 — Collab reel: ماتش واحد، 3 حكايات

**What it is:** one Instagram **Collab** post published by Super App together with three partners — a pitch, a captain/creator and an Egyptian tee brand — so it lands in all four audiences at once.
**Audience:** footballers (each partner brings theirs) · **One CTA:** تابعونا… الإطلاق قرّب
**Format:** 9:16, 1080×1920, 30 fps, 17.5 s · **Style:** collage on ink · voiceover in Egyptian Arabic + burned-in subtitles · built on `../_kit`

**Cover:** `cover.png`, the designed cover (series look from `../_kit/covers.html`).

## Voiceover script
| Time | Line (also the subtitle) | On screen |
|---|---|---|
| 0.0–2.75 | ماتش خماسي واحد… وتلات حكايات. | "ماتش واحد / 3 حكايات" + line-up: Super App × @الملعب × @الكابتن × @البراند |
| 2.75–4.8 | الكابتن حجز الملعب من Super App. | Drawn pitch + "اتأكد الحجز · الخميس 9 م" + the pitch's handle |
| 4.8–7.1 | الشلة كلها دفعت نصيبها من الأبلكيشن. | Real match page + "10/10 دفعوا نصيبهم" + the captain's handle |
| 7.1–11.0 | والتيشيرتات؟ براند مصري، اتطبعت واتشحنت من غير مخزن. | Our 3D tee + "اتطبعت ✓ اتشحنت ✓ من غير مخزن" + the brand's handle |
| 11.0–14.7 | ملعب، ولاعيبة، وبراند… كلهم في مكان واحد. | The three handles gather around the Super App mark |
| 14.7–17.5 | تابعونا… الإطلاق قرّب. | "تابعونا / الإطلاق قرّب" + mark |

Voice: ElevenLabs **Ziad – Dynamic and Vibrant** → `voiceover.mp3`, mixed at 0.15 s with the beat ducked under it (-14.5 LUFS). Files: `reel-07-collab-three.mp4`, `reel-07-collab-three-voice-only.mp4` (voice, no music), `reel-07-collab-three-silent.mp4`, `cover.png`. 
## Before posting (required)
1. **Get the three partners' agreement** and their real handles, then set `PARTNERS` in `index.html` and re-render. The current render uses placeholders (`@اسم_الملعب`, `@اسم_الكابتن`, `@اسم_البراند`) and must not be posted as is.
2. If the brand wants its own design on the tee, render it with `../02-tee-3d/tee-turn.html` and replace `assets/tee-3d.png`.
3. Publish from the Super App account and invite the three as collaborators (Instagram → New reel → Tag people → Invite collaborator). Each one accepts from their account.

## Caption (copy as is, after the handles are set)
```
ماتش خماسي واحد… وتلات حكايات ⚽👕
ملعب، ولاعيبة، وبراند مصري، كلهم في مكان واحد.
تابعونا… الإطلاق قرّب.

#ماتش_خماسي #براند_مصري #حجز_ملاعب #سوبر_اب
```
