# Reel kit — shared pieces for collage reels (03 and later)

| File | What |
|---|---|
| `reel.css` | Collage pieces (blocks, notes, tape, tag, step badge, captions, phone, laptop, toasts, chat bubbles) and the three grounds: `body.bg-turf`, `body.bg-cream`, ink (default) |
| `reel.js` | `Reel({ T: { steps, end }, caps, scene })`: runs the hook, step badge, captions, end card and brand line; the reel only writes `scene(t)` for its own visuals. Helpers for custom timelines: `popIn`, `show`, `jit`, `subtitles(el, lines, t)` for burned-in voiceover subtitles (`.sub`) |
| `sfxkit.py` | The 104 BPM groove and foley synth (no licensed audio): `setup(dur)`, `intro()`, `groove()`, `steps()`, `end_card()`, `voice(path)` (mixes a voiceover and ducks the music under it), `write()` |
| `render.js` | `node _kit/render.js <reel-dir>` → `<reel-dir>/reel-<name>.mp4` at 30 fps with `sfx.wav` normalised to -14 LUFS; `--frames=1,5` saves stills |

New reel: copy `03-fantasy-two-leagues/`, change the markup, `T`, `caps` and `scene`, then `python3 sfx.py && node ../_kit/render.js .`
