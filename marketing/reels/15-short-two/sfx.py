"""Sound for reel 15: a tense filtered intro, the app bed, the publish tap, whooshes, two accept dings."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_app'))
from sfxapp import *
setup(12.8)
END = 9.66
bed(0, END, soft_until=2.45)
add(riser(.6), 1.85, .25)
scene_cuts([2.45, 4.6, 6.35, 8.45])
taps_at([3.3])
for at in (8.6, 9.0): add(ding(), at, .18); add(tap(), at, .4)
end_hit(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15, duck=.35)
write(os.path.join(HERE, 'sfx.wav'))
