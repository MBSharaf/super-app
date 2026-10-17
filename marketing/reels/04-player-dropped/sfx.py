"""Sound bed for reel 04 (a player drops out an hour before): the shared groove and foley on this reel's timeline
(hook 0-1.6, steps at 1.6 / 5.2 / 8.8 / 11.6, end 13.6)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '_kit'))
from sfxkit import *

S1, S2, S3, S4, END = 1.6, 5.2, 8.8, 11.6, 13.6
intro(S1)
add(stamp(), .37, .35); add(stamp(), .53, .3)                              # the bad news lands
groove(S1, END)
steps((S1, S2, S3, S4))
add(paper_slap(), S1 + .1, .5)                                             # chat lands
for i in range(4): add(ding(), S1 + .35 + i * .6, .14, (-.3, .3)[i % 2])  # messages coming in
add(paper_slap(), S2 + .2, .5); add(tape_rip(), S2 + .45, .3, -.3)       # phone lands, taped
add(paper_slap(), S2 + .85, .4)                                            # "publish" pill
add(ding(), S3 + .35, .22); add(ding(), S3 + 1.25, .3)                    # requests, accepted
add(whoosh(.3, up=True), S4 + .05, .3); add(stamp(), S4 + .4, .7)        # 10/10 stamp
end_card(END)
write(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sfx.wav'))
