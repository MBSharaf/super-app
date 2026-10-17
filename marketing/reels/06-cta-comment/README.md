# Reel 06 — CTA reel: قولنا رأيك واستنى الإطلاق

**Audience:** footballers who juggle several apps · **Goal:** comments (the algorithm signal) + people waiting for launch · **One CTA:** write your pick in the comments
**Format:** 9:16, 1080×1920, 30 fps, 20 s · **Style:** collage on cream grid · voiceover in Egyptian Arabic + burned-in subtitles · built on `../_kit`

## Voiceover script (Egyptian Arabic, young energetic male voice)
| Time | Line (also the on-screen subtitle) | On screen |
|---|---|---|
| 0.2–1.4 | بص… | Hook: "4 أبلكيشنات / عشان ماتش واحد؟" |
| 1.4–4.8 | الملعب بتحجزه من مكان، والفانتازي من أبلكيشن تاني، والجداول من تالت… | Three app tiles land: حجز الملعب، الفانتازي، الجداول |
| 4.8–7.0 | والفلوس؟ بتلمّها على الواتساب. | 4th tile "فلوس الماتش" + "يا رجالة اللي مدفعش يدفع 😤" |
| 7.0–10.2 | إحنا بنعمل Super App… كل الكورة في مكان واحد. | The tiles are pulled into one phone (real home screen) |
| 10.2–14.2 | بس عايزين رأيك إنت: إيه أول حاجة نفسك تلاقيها فيه؟ | Poll card: 1 احجز ملعب · 2 ماتش ناقصه لاعيبة · 3 فانتازي مصري وإنجليزي · 4 متجر وبراندات |
| 14.2–16.8 | اكتبها في الكومنتات… | "اكتب رقمك 👇" + a comment box |
| 16.8–19.7 | وخليك معانا، الإطلاق قرّب. | "الإطلاق / قرّب" + mark |

Voice picked: ElevenLabs **Ziad – Dynamic and Vibrant** (Egyptian, young, energetic), model eleven_v4.
**Status:** the voice could not be generated yet: the ElevenLabs account had 0 of its 10,000 credits left, so nothing was charged. The current MP4 has the full beat, foley and subtitles, without the voice.

### Adding the voice
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
