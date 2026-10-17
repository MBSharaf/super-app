"""Sound bed for reel 07 (collab): the shared groove and foley on this reel's beats, with the voiceover mixed on top
(music ducked under the voice). Voiceover file: voiceover.mp3 (or .wav/.m4a) in this folder — see README."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_kit'))
from sfxkit import *
setup(18.0)

S1, S2, S3, ALL, END = 2.8, 5.6, 8.6, 12.6, 15.4
intro(S1, whistle_at=.08)
for i in range(7): add(paper_slap(), .7 + i * .12, .18, (-.4, .4)[i % 2])            # the collab line-up pops in
groove(S1, END)
steps((S1, S2, S3, ALL))
add(paper_slap(), S1 + .2, .55); add(ding(), S1 + .95, .25)                           # pitch lands, booking confirmed
add(paper_slap(), S2 + .25, .5); add(tape_rip(), S2 + .5, .3, -.3); add(stamp(), S2 + 1.25, .6)   # phone, 10/10 paid
add(paper_slap(), S3 + .3, .5)                                                        # the tee
for k, at in enumerate((S3 + 1.4, S3 + 1.9, S3 + 2.5)): add(stamp(), at, .3, (-.3, .3, 0)[k])  # printed, shipped, no stock
add(whoosh(.5, up=False), ALL, .35); add(paper_slap(), ALL + .1, .5)
for i in range(3): add(paper_slap(), ALL + .35 + i * .2, .35, (-.3, .3, 0)[i])      # partners gather
end_card(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15)
write(os.path.join(HERE, 'sfx.wav'))
