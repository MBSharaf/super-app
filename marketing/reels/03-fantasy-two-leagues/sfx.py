"""Sound bed for reel 03 (fantasy, two leagues): the shared groove and foley on this reel's timeline
(hook 0-1.6, steps at 1.6 / 5.0 / 8.6, end 12.6)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '_kit'))
from sfxkit import *

S1, S2, S3, END = 1.6, 5.0, 8.6, 12.6
intro(S1, whistle_at=.08)                      # a referee whistle: this one is about matchday
groove(S1, END)
steps((S1, S2, S3))
add(paper_slap(), S1 + .25, .5); add(tape_rip(), S1 + .45, .3, -.3)       # phone lands, taped
add(paper_slap(), S1 + .55, .35, .3)                                       # "الدوري المصري" label
add(whoosh(.4, up=False), S2 + .25, .4); add(paper_slap(), S2 + .7, .45)   # the flip to the EPL screen
add(ding(), S2 + .3, .18)                                                  # "one tap"
add(whoosh(.35, up=True), S3, .25); add(paper_slap(), S3 + .5, .55); add(stamp(), S3 + .7, .35)  # invite code ticket
for i in range(3): add(ding(), S3 + 1.1 + i * .55, .2)                     # the crew replying
end_card(END)
write(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sfx.wav'))
