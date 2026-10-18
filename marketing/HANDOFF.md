# Super App — handoff for a new conversation

Paste this file (or the ZIP that contains it) into the new conversation first. It says what exists, where, and what is still open.

**Repo:** https://github.com/MBSharaf/super-app · made on branch `claude/design-system-extraction-9z03sr`, copied to the marketing window's branch on 2026-10-18.
**Rendered media** (`reel-*.mp4`, `-silent.mp4`, `-voice-only.mp4`, `sfx.wav`, post/slide `.png` masters, ~140 MB) stay only on `claude/design-system-extraction-9z03sr`; everything else (sources, JPGs, covers, voiceovers, docs) is here. Re-render with the kit or check them out from that branch.
**Design system artifact:** https://claude.ai/artifact/65e1JyQRGpGN3JMgobxF2f

## Read these first
1. `marketing/CLAUDE.md` (the root `CLAUDE.md` of that branch) — brand, content and design rules (audience, hook, Egyptian Arabic, human-not-AI, SEO/GEO, one CTA, red lines, competitor Yalla Hagz, fantasy positioning).
2. `docs/brand/` — `audiences.md`, `content-strategy.md`, `social-video.md`, `prompts.md`, `references.md` (+ `references/` moodboard), `competitors/yalla-hagz.md`, `content-ideas.md` (idea bank + what is done), `youtube-voice.md`.
3. `.agents/product-marketing.md` — product, positioning, stage (pre-launch).
4. `marketing/CAPTIONS.md` — every caption with its viral hook, A/B hook, pinned comment, timing and pre-post notes, plus a two-week posting order.

## What is made (all final files in `marketing/export/SuperApp-content-<date>.zip`, rebuilt by `python3 marketing/export.py`)
| Piece | Folder | Audience |
|---|---|---|
| Posts 01–03 (players / fantasy / sellers) | `marketing/posts/2026-10-17-teasers/` | — |
| Posts 04–07 (captain GW7, who collects, empty hours, crew league) | `marketing/posts/2026-10-18-week1/` | fantasy, captains, pitch owners, fantasy crews |
| Carousels: fantasy rules, 5-a-side rules (6 slides each) | same folder, `carousel-*/` | fantasy, footballers |
| Reel 01 types of players · 02 3D tee · 03 two fantasy leagues · 04 player dropped · 05 notebook→dashboard · 06 CTA (voiceover) · 07 collab (voiceover) | `marketing/reels/0X-*/` | see each README |

Each reel folder: `index.html` (composition), `sfx.py` (sound bed), `README.md` (beats, caption), `reel-*.mp4`, `-silent.mp4`, `cover.png` (designed). 06 and 07 also have `voiceover.mp3` and `-voice-only.mp4`.

## How things are made (code, no stock or AI images)
- Posts: HTML + `collage.css` → `node render.js` (Playwright) → PNG/JPG 1080×1350.
- Reels: `marketing/reels/_kit` — `reel.css`, `reel.js` (timing engine, subtitles), `sfxkit.py` (synth beat + foley + `voice()` ducking), `render.js` (30 fps → MP4, −14 LUFS), `covers.html/.js` (designed covers).
- Real app screenshots come from the app in this repo served locally (Playwright, Arabic, light theme, consent set, promo popup muted). Demo data is labelled as such.
- Voiceover: ElevenLabs connector, voice **Ziad – Dynamic and Vibrant** (`5REPlS2Ja1VZ7zNA0ykn`), model eleven_v4. The connector must be linked to the account that has credits.

## Open items
- **Reel 07 (collab):** partner handles are placeholders (`@اسم_الملعب`, `@اسم_الكابتن`, `@اسم_البراند`). Set `PARTNERS` in `marketing/reels/07-collab-three/index.html` after the partners agree, re-render, then post as an Instagram Collab.
- **Post 04:** publish before Mon 19 Oct (GW7 deadline); re-check fixtures on the day.
- **App i18n:** the fantasy private-group standings screen still shows English ("Compare", "Private group…") — needs Arabic before launch.
- Still unknown and never invented: launch date, first launch areas, founder-offer approvals.
