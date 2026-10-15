# Pitch community and player requests

Agreed with the user on 2026-10-15.

**Decisions:**
- Player shares are paid **online** after the host accepts.
- The host chooses: **auto-accept** or **decide manually**.
- **Player record and ratings** are built now; the data is used later.
- **Alerts** and **rich cards for every internal link** are built now.
- **One community**, "Pitches & free slots", with area and time filters.

**Resume rule:** if the session stops, continue from the first unticked item and don't redo finished items.

## Checklist
- [ ] 1. **Internal link cards.** Any Super App link in a post or comment becomes a rich card: pitch, free slot, game, product, design, seller store, tournament, community post. Outside links stay blocked.
- [ ] 2. **Free slots from the partner dashboard.**
  - The owner picks a pitch and a free hour, with an optional discount (uses the offers system).
  - This posts a live **slot card** in "Pitches & free slots", with a "Book now" button that opens checkout with the time pre-selected.
  - The card shows "Booked" and expires on its own.
  - Limit per owner per day (an admin setting).
  - When a booking is cancelled, the owner is prompted to post the freed hour.
  - Owner stats: posted vs booked.
- [ ] 3. **Join requests for games** (built into the existing Find players).
  - The host sets the players needed and positions, and chooses auto-accept or manual.
  - A player requests with a position and a message.
  - The host sees the player's card (level, position, games played, attendance record, rating) and accepts or rejects.
  - The share is paid online on acceptance (held by Super App). Declined requests cost nothing; a removed player is refunded.
  - Waitlist; the host can close requests.
  - Share the game to the community as a card.
- [ ] 4. **Player record and ratings.** After the game the host rates players (stars, showed up or no-show). The reliability score is shown on player cards; admins can block players with many no-shows.
- [ ] 5. **Alerts.** A player follows a pitch or an area, optionally with a time window, and is notified when a matching free slot is posted. Managed on `#/alerts`.
- [ ] 6. **Admin.**
  - Moderate slot cards and game posts; stop an owner from posting.
  - Daily limit setting.
  - Intervene in a game: remove a player, refund.
  - Block unreliable players.
  - Stats: slots posted vs booked, games filled.
- [ ] 7. Sharaf test links, Arabic strings, check, README, push.
