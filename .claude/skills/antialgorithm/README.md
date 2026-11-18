# antialgorithm

![Six posters without the skill on top, six with it below](images/showcase.png)

Claude Code skill for creative work: names, copy, story, game ideas, posters, UI. It makes the model name its first, most average pick, then move away from it for a reason taken from the project.

Both rows came from the same prompt: `Design a poster for a music festival as a 400x600 SVG. Save the final SVG as final.svg in the current folder.` The top row is one batch of six runs without the skill, none left out. The bottom row is picked from about a dozen runs made while the skill was still being written.

![Chart of how often a choice gets picked: a tall peak around the average marked as the safe zone, flat on both sides](images/graph.png)

## Install

```
git clone https://github.com/mankidev/antialgorithm ~/.claude/skills/antialgorithm
```

The scripts need Python 3. `check.py` also needs numpy, Pillow and `rsvg-convert` (or the cairosvg package).

## Scripts

`seed.py` rolls a starting point when the request is thin. A visual seed gives a making method, colors, layout, type, a verb and a scale for the idea, a setting, a region and, half the time, an object. A text seed gives a voice, a way in and a register. An idea seed gives a verb, a constraint, a scale and a pressure. It draws from system randomness, so two people sending the same prompt start in different places. It also saves each seed to `history.jsonl` in the skill folder and skips values from the last few, so your own seeds don't repeat from one session to the next. `--keep "axis=value"` holds an axis fixed, which lets you keep a subject and roll only the look.

`moves.py` keeps a short log of the main move behind each off-default answer, such as "neon tube wall, almost no type". The model reads it before showing new work, so it doesn't reuse a move that worked last time just because it worked.

`rough.py` takes an SVG and shakes its straight lines, shapes and letters, and breaks the even spacing of repeated marks, so a hand-made look drawn in code stops looking plotted. It is only for hand-made methods such as print, paint, stamps and ink; screen, camera and render looks keep their own marks.

`check.py` renders an SVG at full size and at icon size and lists text that runs off an edge, sits under a shape, has low contrast or is too small. The size limit fits posters; for an app screen use `--min-text 0.6`.

Visual rules live in `visuals.md`, which the model reads only for visual work, so a name or a slogan doesn't load them.

## License

MIT

Source: https://github.com/mankidev/antialgorithm (commit ff7c3c4, MIT). Images left out to keep the repo small.
