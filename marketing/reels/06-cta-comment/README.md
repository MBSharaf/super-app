# Reel 06 — CTA reel: قولنا رأيك واستنى الإطلاق

**Audience:** footballers who juggle several apps · **Goal:** comments (the algorithm signal) + people waiting for launch · **One CTA:** write your pick in the comments
**Format:** 9:16, 1080×1920, 30 fps, 18.5 s · **Style:** collage on cream grid · voiceover in Egyptian Arabic + burned-in subtitles · built on `../_kit`

**Cover:** `cover.png`, the designed cover (series look from `../_kit/covers.html`).

## Voiceover script (Egyptian Arabic, young energetic male voice)
| Time | Line (also the on-screen subtitle) | On screen |
|---|---|---|
| 0.0–1.4 | بص… | Hook: "4 أبلكيشنات / عشان ماتش واحد؟" |
| 1.4–5.2 | الملعب بتحجزه من مكان، والفانتازي من أبلكيشن تاني، والجداول من تالت… | Three app tiles land: حجز الملعب، الفانتازي، الجداول |
| 5.2–7.6 | والفلوس؟ بتلمّها على الواتساب. | 4th tile "فلوس الماتش" + "يا رجالة اللي مدفعش يدفع 😤" |
| 7.6–10.4 | إحنا بنعمل Super App… كل الكورة في مكان واحد. | The tiles are pulled into one phone (real home screen) |
| 10.4–13.6 | بس عايزين رأيك إنت: إيه أول حاجة نفسك تلاقيها فيه؟ | Poll card: 1 احجز ملعب · 2 ماتش ناقصه لاعيبة · 3 فانتازي مصري وإنجليزي · 4 متجر وبراندات |
| 13.6–14.9 | اكتبها في الكومنتات… | "اكتب رقمك 👇" + a comment box |
| 14.9–18.5 | وخليك معانا، الإطلاق قرّب. | "الإطلاق / قرّب" + mark |

Voice: ElevenLabs **Ziad – Dynamic and Vibrant** (Egyptian, young, energetic), model eleven_v4 → `voiceover.mp3`, mixed at 0.15 s with the beat ducked under it (-14.7 LUFS). Subtitle times in `LINES` follow the take's pauses.

Files: `reel-06-cta-comment.mp4` (voice + beat + foley), `reel-06-cta-comment-voice-only.mp4` (voice, no music: add a trending track in the app), `reel-06-cta-comment-silent.mp4`, `cover.png`.

### Re-recording the voice
1. Put the recording in this folder as `voiceover.mp3` (ElevenLabs download, or a phone recording of the script above).
2. Retime `LINES` in `index.html` to the take (pauses: `ffmpeg -i voiceover.mp3 -af silencedetect=n=-35dB:d=0.25 -f null -`).
3. `python3 sfx.py && node ../_kit/render.js .` — the music ducks under the voice automatically.

## Caption (copy as is)
```
تطبيق واحد للكورة كلها: حجز ملاعب، فانتازي، ماتشات، ومتجر ⚽
قولنا في الكومنتات: إيه أول حاجة نفسك فيها؟ 1، 2، 3 ولا 4؟
والإطلاق قرّب… خليك معانا.

#حجز_ملاعب #فانتازي #ماتش_خماسي #سوبر_اب
```
- Reply to every comment in the first hour (that is what keeps the reel pushed). Pin the first good answer.
- Count the answers: they tell us which feature to lead the launch with.
- Every feature named exists in the app today (booking, open games, fantasy for both leagues, shop).
