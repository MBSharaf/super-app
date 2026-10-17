# Super App — brand, content and design rules

Read this before making any design, social post, reel, video, ad or marketing copy for Super App.

## Sources of truth
- **Design system (tokens, components, logos, icons, social templates):** https://claude.ai/artifact/65e1JyQRGpGN3JMgobxF2f — read its `project/README.md` and the sections below with the Artifact tool.
- **In this repo:** `css/tokens.css` + `css/dark.css` (tokens), `docs/brand/audiences.md`, `docs/brand/content-strategy.md`, `docs/brand/social-video.md`, `docs/brand/prompts.md` (ready video prompts, the code-video stack and approved styles).

## Always, for every piece of content
1. Write for **one** audience from `docs/brand/audiences.md`.
2. Hook in the first 1.5 seconds; never open on the logo.
3. **Human, not AI:** Egyptian Arabic as people talk on the pitch, real local details (areas, EGP prices, times), real phone footage and real app screenshots. None of the banned phrases or AI visuals listed in `content-strategy.md`.
4. **Search today's news and trends first** (matchday, fantasy deadline, derby, national team, Ramadan, weather) and tie the piece to what is happening now.
5. **SEO + GEO:** the main keyword on screen, spoken, and in the first caption line; 3–5 checked hashtags; burned-in captions.
6. Brand look from the design system: `paper`/`ink-bg` grounds, `turf` for pitch and success, `blaze` once per design, Archivo + Cairo, pills, spring motion. Sizes and safe zones in `social-video.md`.
7. One CTA.
8. Red lines: no club crests, official kits or player photos without rights; no betting framing (fantasy prizes are sponsor vouchers, never cash); no unverified claims.

## Fantasy positioning
Egyptian league fantasy is **not** a first in Egypt (FilGoal fantasy has run since about 2020/21; independent apps exist too). Never say "first Egyptian league fantasy". Angles: Egyptian + English fantasy in one app, connected to your real games, sponsor prizes. Re-check before any launch in case the league releases an official game.

## Installed skills (`.claude/skills/`)
- **/brag** and **/brag-slim** (latent-spaces/brag, MIT): turn this repo into a short launch video with HyperFrames. For Super App always pass `--format vertical` (or square for LinkedIn) and follow the rules above; Arabic on screen, our tokens, no bundled music (license unverified, MP3s removed — use `--no-music` or a licensed track).
- **/yt-script, /yt-package, /yt-seo, /yt-shorts, /yt-chapters, /yt-edit, /yt-retention, /yt-comment, /yt-plan, /yt-viral, /yt-audit** (Jakeschincariol/youtube-agent-skill, MIT): YouTube and Shorts workflow. They read `~/.claude/youtube/voice.md`; first run `mkdir -p ~/.claude/youtube && cp docs/brand/youtube-voice.md ~/.claude/youtube/voice.md`. Their hook formulas are English: write the final lines in Egyptian Arabic.
- **50 marketing skills** (coreyhaines31/marketingskills, MIT): copywriting, social, video, content-strategy, launch, seo-audit, ai-seo, programmatic-seo, schema, referrals, community-marketing, influencer-marketing, marketing-loops, cro, emails, and more. They all read **`.agents/product-marketing.md`** — keep it current with `/product-marketing` and never fill its TBD lines with invented facts. Their examples are US/English SaaS: adapt to Egypt, Arabic and these rules; where a skill conflicts with this file, this file wins.
