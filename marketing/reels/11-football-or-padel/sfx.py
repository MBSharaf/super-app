"""Sound for reel 11: the app bed, a whoosh on each colour change, the tap on the padel switch, a pop per minutes chip."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_app'))
from sfxapp import *
setup(17.4)
END = 12.3
bed(0, END, soft_until=2.0)
add(riser(.6), 1.4, .25)
scene_cuts([2.0, 3.4, 6.0, 8.3])
taps_at([3.15])
for at in (10.1, 10.55, 11.1): add(tap(), at, .5); add(ding(), at + .02, .08)
end_hit(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15, duck=.35)
write(os.path.join(HERE, 'sfx.wav'))
