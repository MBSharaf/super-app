"""Sound for reel 14: key clicks while the link is typed, an enter hit, the app bed, whooshes for laptop/phone moves."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_app'))
from sfxapp import *
setup(13.6)
END = 10.55
add(keys(15, .08), .8, .5)
add(tap(), 2.15, .8); add(riser(.4), 2.05, .25)
bed(2.45, END)
scene_cuts([2.45, 3.9, 5.05, 8.3])
for at in (9.05, 9.65): add(swipe(.2), at - .08, .2)
end_hit(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15, duck=.35)
write(os.path.join(HERE, 'sfx.wav'))
