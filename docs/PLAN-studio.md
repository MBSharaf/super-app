# Design studio fixes (seller "New design") — plan & progress

The user's list (2026-10-16), plus real 3D models of the blank garments.

## 3D models
- The user sent photos of a blank oversized tee and a blank hoodie (front and back).
- Converted to 3D with Microsoft TRELLIS. There is no library called "trellis.js"; we used the free TRELLIS space on Hugging Face.
- The output is a GLB in `models/`.
- The site shows it with three.js, vendored in `vendor/three/` and loaded only when a 3D view opens.
- **Colour:** a material of the chosen colour.
- **Print:** the artwork is projected onto the fabric as a decal, front and back.
- Hoodie: the free daily GPU quota ran out. Generate it again (`hoodie-front.png` + `hoodie-back.png`, multi-image) and drop it in `models/hoodie.glb`.
- Until a model exists, the old drawn/photo flip view is used.

## Checklist
- [x] 1 — 3D inside the studio view (Front / Back tabs turn the model), no separate "3D preview" button
- [x] 2 — Size chart per product (cm) in the studio and on the product page
- [x] 3 — The uploaded artwork shows as a thumbnail in the Artwork section
- [x] 4 — Artwork per side: front design and back design (different files allowed)
- [x] 5 — Price follows the sides: adding the back raises the price by the back-print cost, so profit stays the same
- [x] 6 — Product details: fabric, weight, fit, print method, care — in the studio and on the product page
- [x] 7 — Cap removed from the catalogue (old cap designs hidden)
- [x] 8 — Price can equal the base price (0 profit is allowed, e.g. a seller buying for themselves)
- [x] 9 — Test design not in the store: it's waiting for review. The owner now sees "In review" / draft designs on their own store page, with a note
- [x] 10 — "Back to dashboard" button on the store page for the owner
- [x] Arabic, QA sweep, README, commit and push
- [ ] Hoodie 3D model (TRELLIS quota) — then add it to `MODELS` in `js/components/model3d.js` and tune its print area

## Also fixed on the way
- **Studio saved designs to the wrong store.** If the same device had a pitch-partner session, the studio (which is outside the seller dashboard) used the partner's store. That can be why a test design "didn't show". It now follows the seller dashboard sign-in.
- **Size "L" showed as "خسارة" (loss) in Arabic.** The league-table "L" was in the dictionary. The tables now print it themselves, and sizes stay as they are.

## Pop-up ads with 3D (user request, 2026-10-16)
- [x] Rotating pop-ups for our services and offers (seller, fantasy, discount codes, shop, pitches, weekly packages, tournaments, players, points, partners)
- [x] 3D elements: spinning icon cube, discount coin with thickness, the real 3D T-shirt, orbiting sparks; 3D flip in/out and pointer tilt
- [x] Every 10 s by default (admin can change), limits so it isn't annoying, close / mute for 7 days, reduced motion
- [x] Admin tab with stats; Arabic; CEO Every page link
