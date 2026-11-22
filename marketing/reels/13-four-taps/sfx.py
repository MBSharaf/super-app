"""Sound for reel 13: app bed, whooshes on colour changes, a tap + tick for each of the 4 taps, the success chime on the booking."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_app'))
from sfxapp import *
setup(14.4)
END = 11.4
bed(0, END, soft_until=2.45)
add(riser(.6), 1.85, .25)
scene_cuts([2.45, 3.85, 5.6, 7.55, 9.4])
taps_at([3.35, 4.95, 7.0])
for at in (3.35, 4.95, 7.0): add(ding(), at + .05, .1)
add(success(), 7.6, .5)
end_hit(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15, duck=.35)
write(os.path.join(HERE, 'sfx.wav'))
