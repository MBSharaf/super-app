"""Sound for reel 12: app bed, colour-change whooshes, the join tap, key clicks for the split, a success chime when the match is full."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_app'))
from sfxapp import *
setup(16.5)
END = 12.9
bed(0, END, soft_until=1.75)
add(riser(.6), 1.15, .25)
scene_cuts([1.75, 3.7, 7.85, 9.75])
taps_at([3.4])
for k in range(7): add(tap(), 5.0 + k * .08, .35)          # the level row pops in
add(keys(3), 8.0, .3); add(keys(6), 8.6, .3)               # ÷ 4, then 375
add(success(), 9.85, .45)
end_hit(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15, duck=.35)
write(os.path.join(HERE, 'sfx.wav'))
