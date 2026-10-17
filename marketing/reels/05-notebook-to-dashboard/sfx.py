"""Sound bed for reel 05 (pitch owners: notebook to dashboard): the shared groove and foley on this reel's timeline
(hook 0-1.6, steps at 1.6 / 4.8 / 8.0 / 11.0, end 13.4)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '_kit'))
from sfxkit import *

S1, S2, S3, S4, END = 1.6, 4.8, 8.0, 11.0, 13.4
intro(S1)
groove(S1, END)
steps((S1, S2, S3, S4))
add(paper_slap(), S1 + .1, .6)                                              # notebook lands
add(typewriter(12, .1), S1 + .3, .14, -.2)                                  # rows being written
add(tape_rip(), S1 + 1.6, .35); add(stamp(), S1 + 2.0, .55)                # circled, "two at 10!"
for i in range(4): add(ding(), S2 + .2 + i * .6, .16, (-.3, .3)[i % 2])    # messages keep coming
add(whoosh(.4, up=True), S3, .3); add(paper_slap(), S3 + .3, .55); add(tape_rip(), S3 + .5, .3, -.3)  # laptop lands, taped
add(paper_slap(), S3 + .85, .4)                                             # "book and pay online"
add(whoosh(.3, up=False), S4 + .05, .3); add(ding(), S4 + .5, .28)        # finance screen, "every pound recorded"
end_card(END)
write(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sfx.wav'))
