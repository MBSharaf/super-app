# Reels 11–15 + 8 covers — DONE 2026-11-22 (this file was the resume point)

All five reels and the eight covers are finished: `marketing/reels/1[1-5]-*/reel-*.mp4`, each reel folder has `cover.png`
(1080×1920) and `cover-grid.jpg` (centre 1080×1350). Covers: `node marketing/reels/_app/covers.js`. Site bugs went to the dev inbox.
Still open: store badges (owner to send Google Play / App Store links).


The owner's request (2026-11-22): five new reels in the style of their 5 reference videos, plus covers for the 5 new reels
and the 3 old ones (08–10). The owner judged reels 08–10 "not what I asked" but will use them, so they get covers only.

## What the references look like (analysed)
App-promo motion design: a solid colour background that changes each section (circular wipe), a short bold headline
at the top, one centred phone with the real app screens, a touch dot that taps, punch-in zooms, number typing, a full
"success" screen, an end card with the logo + URL + platforms. Ref 5 is flat 2D illustration (not used yet).
**This replaces the paper-collage look of `marketing/CLAUDE.md` for these reels (the owner asked for it).**

## Product facts to keep (from main, 2026-11-21/22)
- Name: Super Malaab / سوبر ملعب. Site: **supermalaab.com**. Say it is a **website and a mobile app** (Android app exists as
  an APK; no store links known — end card shows "موقع" + "تطبيق موبايل" chips, no store badges until the owner sends links).
- Padel is live: courts, half-hour booking for 60/90/120 min, rentals at the club, Open Match (4 players, level 1–7, cost split by 4,
  everyone told when full). Football + padel switch on the home page.
- Booking: deposit 20% (min 100) by card, rest at the pitch (or full payment if the pitch chose it); QR; Super Points 1/EGP.

## Done
- `marketing/reels/_app/` — new kit: `app.css`, `app.js` (AppReel engine: scenes with bg colour + screen, headline lines synced to the
  voiceover, phone keyframes, taps, tall-page scroll, pops, end card, status bar), `sfxapp.py` (118 BPM bed, tap, swipe, success, keys).
  Render with the old renderer: `node marketing/reels/_kit/render.js marketing/reels/<reel>` (after `python3 sfx.py`).
- `marketing/reels/_app/shots/*.jpg` — screens captured from supermalaab.com (mobile 390×844 @3x, Arabic, light) and desktop 1440×900.
  Payment/ticket/join screens were made on a **local copy of main in demo mode with the n8n webhooks blocked** (paying on the live
  site was refused by the permission check, correctly). Tools: `marketing/reels/_app/tools/steps.js`, `tall.js`, `explore.js`
  (need `npm root -g` playwright; the live site needs the "super-app" environment with images.unsplash.com and supermalaab.com allowed).
- Voiceovers (Ziad, ElevenLabs eleven_v4) downloaded: `marketing/reels/11…15/voiceover.mp3`.
- **Reel 11 «كورة ولا بادل؟» — DONE**: `marketing/reels/11-football-or-padel/` (index.html, sfx.py, reel-11-football-or-padel.mp4).

## To do (in this order)
1. **Reel 12 «ناقصك واحد في البادل؟»** (`12-padel-open-match/`, VO 15.07 s, end card at 12.9, dur 16.5).
   Lines (start,end s; add vo .15): A 0–1.11 ناقصك واحد في البادل؟ · B 1.73–3.25 اعمل Open Match على سوبر ملعب · C 3.72–4.59 أربع لاعيبة بس ·
   D 4.85–7.29 وكل واحد بيختار مستواه من 1 لـ 7 · E 7.73–9.29 حساب الملعب بيتقسم على 4 · F 9.62–10.80 وأول ما الماتش يكمل… ·
   G 11.07–12.36 الكل بيوصله إشعار · H 12.73 end card.
   Scenes: hook (ink, dots ●●●○ + text) until 1.75 → padel blue + `padel-match-2` → tap «اشترك في الماتش» (~95,790 css) at 3.45 →
   paper + `padel-join-sheet` zoomed on the top (level chips; hide the English lower half) + pop row 1–7 with 4 highlighted →
   turf + `padel-match-2` with overlay «÷ 4» then «375 ج.م للفرد» → green success screen drawn in the phone («الماتش اكتمل 4/4», dots, toast «اتبعت إشعار للأربعة»).
   Do NOT state the court total (375×4 not verified against the court price).
2. **Reel 13 «4 ضغطات… وملعبك اتحجز»** (`13-four-taps/`, VO 12.28, end 11.4, dur 14.4).
   Lines: A 0–2.33 hook · B 2.90–3.49 اختار الملعب · C 3.99–5.0 اختار اليوم والساعة · D 5.50–6.39 ادفع العربون بس · E 6.62–7.26 والباقي في الملعب ·
   F 7.80–8.38 وخلاص · G 8.61–10.91 QR في إيدك، ونقط على كل جنيه · H 11.24 end.
   Screens: `pitch` (tap «اختار الوقت» ~95,808 at 3.3) → `slots-before` (tap 8 م at 90,460 at 4.9) → `slots-picked` → `review-pay`
   (punch-in on the pay button ~195,423, tap at 7.0) → `paid` (مبروك + 165 Super Points + PDF receipt) → `ticket-qr` at 9.2.
   Counter chip «ضغطة 1 من 4 … 4 من 4 ✓» updates on each tap. Colours: ink hook, turf, paper, blaze, turf.
3. **Reel 14 «موقع وتطبيق»** (`14-web-and-app/`, VO 11.89, end 10.55, dur 13.6).
   Lines: A 0–0.39 اكتب · B 0.75–2.09 سوبر ملعب دوت كوم (hook = URL bar typing "supermalaab.com" with key clicks) ·
   C 2.63–3.51 واحجز من اللابتوب (laptop mockup with `desk-home`) · D 3.91–4.70 أو من الموبايل (phone `home-football`) ·
   E 5.17–5.83 نفس الحساب · F 6.05–8.05 ونفس حجوزاتك، في أي مكان (laptop `desk-padel` + phone side by side) ·
   G 8.39–10.15 كورة، وبادل، وفانتازي (phone swaps home-football → home-padel → fantasy) · H 10.38 end.
   Needs a laptop element (add to app.css) and a custom() for the typing.
4. **Reel 15 «الساعة 9 وناقصك 2؟»** (`15-short-two/`, VO 11.89, end 9.66, dur 12.8).
   Lines: A 0–1.13 الماتش الساعة تسعة… · B 1.44–2.30 وناقصك اتنين؟ · C 2.96–4.37 انشر ماتشك على سوبر ملعب · D 4.58–5.90 واللاعيبة اللي حواليك يشتركوا ·
   E 6.26–8.06 كل واحد بيدفع نصيبه وهو بينضم · F 8.37–9.09 وإنت بس اقبل · G 9.51 end.
   Screens: `players-view` (tap «انشر ماتشك» ~250,215 at 3.3) → `players-tall` (+top/bot; scroll only 0→420, the cards below
   overflow the screen width on the live site) with zoom on «باقي 2 أماكن» and «60 ج.م · للاعب» → chips «طلب انضمام · اقبل ✓».
5. **Covers (8)**: 1080×1920, everything important inside the centre 1080×1350: solid colour + phone (or a still) + big hook
   + «سوبر ملعب». Reels 08 (transfers gallery), 09 (floodlight board), 10 (parcel), 11–15. Save as `cover.png` in each reel folder.
6. Check every reel (stills, ffprobe 1080×1920 + audio, -14 LUFS), commit, push, send the files to the owner.
7. Tell the owner: site bugs found (dev inbox): Open Match title mixes English/Arabic in Arabic mode; padel join sheet strings in
   English ("Friendly Open Match…", "this host accepts automatically…", "Choose your position"); pitch name shows reversed
   ("Arena 22 Turf"); players page cards from the 2nd one are wider than the phone screen (prices cut); the players-count field on
   the review page shows the select arrow over the number. Ask for: Google Play / App Store links (store badges).

## Notes
- The branch `claude/laughing-bohr-58xk5p` is still on the old main history; a rebased copy is the local branch `rebased-on-main`
  (force push waits for the owner's OK). Keep committing on the branch with normal pushes.
- Old reel kit `_kit/` (collage) still works for 08–10; the new reels use `_app/`.
