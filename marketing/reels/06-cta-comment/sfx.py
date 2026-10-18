"""Sound bed for reel 06 (CTA): the shared groove and foley on this reel's beats, with the voiceover mixed on top
(music ducked under the voice). Voiceover file: voiceover.mp3 (or .wav/.m4a) in this folder — see README."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_kit'))
from sfxkit import *
setup(18.5)

TILES, ONE, POLL, COMMENT, END = 1.4, 7.6, 10.4, 13.6, 14.9
intro(TILES)
groove(TILES, END)
for at in (1.5, 2.2, 3.8, 5.3): add(paper_slap(), at + .05, .45, (-.3, .3)[int(at) % 2])     # four apps land
add(ding(), 6.35, .2)                                                                          # the captain nags
add(whoosh(.5, up=False), ONE, .4); add(paper_slap(), ONE + .45, .55); add(tape_rip(), ONE + .7, .3)   # one phone
add(stamp(), ONE + 1.35, .45)
add(whoosh(.32, up=True), POLL - .18, .3); add(typewriter(10, .08), POLL + .6, .16)          # poll rows
add(whoosh(.32, up=True), COMMENT - .18, .3); add(stamp(), COMMENT + .4, .5)                 # "write your number"
end_card(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15)
write(os.path.join(HERE, 'sfx.wav'))
