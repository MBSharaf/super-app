"""Sound bed for reel 08 (transfers gallery): the shared groove and foley, with the voiceover mixed on top."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_kit'))
from sfxkit import *
setup(20.0)

GAL, MSG, BOOK, POL, END = 3.4, 7.9, 10.6, 14.35, 16.75
intro(GAL)
add(whoosh(.5, up=True), .35, .3)                                     # the phone rises
for k in range(14):                                                    # thumb flicks through the gallery
    at = GAL + k * (MSG - GAL) / 14
    add(typewriter(1), at, .35, (-.2, .2)[k % 2]); add(paper_slap(), at + .02, .12)
add(ding(), 9.2, .3); add(stamp(), 9.4, .45)                           # "mش لاقي الحجز"
groove(BOOK, END)
add(whoosh(.4, up=True), BOOK - .2, .35); add(stamp(), BOOK + .9, .4)  # booking confirmed, QR lands
add(whoosh(.32, up=True), POL - .18, .3); add(typewriter(8, .07), POL + .5, .18)
end_card(END)
add(stamp(), END + .75, .45)                                           # "كاش في الملعب"
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15)
write(os.path.join(HERE, 'sfx.wav'))
