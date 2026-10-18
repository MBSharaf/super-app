# Window 3 — Marketing and social media

**Job:** turn what Super App offers into a marketing plan and social media content. Work in
`docs/marketing/` (plans, calendars, copy) and produce visuals with the design system
([design-system.md](design-system.md)). Never change the website code. Ask window 1 through its Inbox.

## Content already made (start here)
Made in the design-system chat (branch `claude/design-system-extraction-9z03sr`) and copied here on 2026-10-18:
- [`marketing/HANDOFF.md`](../../marketing/HANDOFF.md): what exists, how it is made, open items.
- [`marketing/CLAUDE.md`](../../marketing/CLAUDE.md): brand, content and design rules for every post, reel and ad.
- `docs/brand/`: audiences, content strategy, social and video specs, prompts, moodboard, competitor Yalla Hagz, idea bank, YouTube voice.
- `.agents/product-marketing.md`: product, positioning and stage.
- `marketing/CAPTIONS.md`: every caption, hooks and a two-week posting order.
- `marketing/posts/` (posts 01–07 and 2 carousels) and `marketing/reels/` (reels 01–07 + `_kit`).

## What to read first
1. [project-brief.md](project-brief.md): the product, users and money.
2. [decisions.md](decisions.md): prices, points, fantasy rules and what must not be promised.
3. The design system artifact (`project/README.md`): colours, fonts, voice.
4. The site itself (`https://localhost:7254/super-app/` or the demo on GitHub Pages) and the pop-up ads in `js/services/contentService.js` (`PROMO_DEFAULTS`). They are the current campaign messages.

## Audiences and what each gets
| Audience | Main promise |
|---|---|
| Players (5-a-side crews, office teams) | Find and book a pitch in seconds, fill the squad, pay online, weekly packages are cheaper |
| Fantasy fans | Egyptian + English Premier League fantasy, sponsor prizes, play with friends |
| Pitch owners | Online bookings and payments, fewer empty hours, a free dashboard for their team |
| Designers / sellers | Sell designs on tees and hoodies with no stock and no upfront cost; levels with perks |
| Shoppers | Boots, kits, balls and original designs delivered across Egypt |

## Constraints
- **Language:** Egyptian Arabic first, English second.
- **Links:** no WhatsApp or social media links in the product until the owner sends them. Marketing plans can list the channels: Facebook, Instagram, TikTok and WhatsApp.
- **Fantasy:** no cash prizes; never call it betting or gambling (betting is illegal in Egypt).
- **Club logos:** not used without a licence.
- **Prices:** only promise prices and discounts that exist in the product (see decisions.md). Examples:
  - 10% off the first order with code KICKOFF10
  - weekly packages cheaper per match
  - pitches from 500 EGP an hour before 4 PM
- **People in posts:** use fictional names, or real people with their permission.

## First tasks (suggested)
1. A positioning one-pager and messaging per audience.
2. A 90-day launch plan: the pre-launch waitlist, then launch week, then the first month.
3. A social calendar around the football calendar (big matches, derby days, Ramadan). The holidays come from the backend.
4. Post templates using the design system, plus content pillars.

## Inbox (requests from other windows)
- *(empty)*

## Done
- 2026-10-17/18 (design-system chat): brand docs, posts 01–07, 2 carousels, reels 01–07 with covers, sound and voiceover, captions. See `marketing/HANDOFF.md`.
