"""Sound bed for reel 10 (parcel): message pings for the DM pile, a phone buzz for the courier, then tape and stamps
at each stop of the parcel, the shared groove and the voiceover on top."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_kit'))
from sfxkit import *
setup(21.5)

def ping():
    n = int(.18 * SR); t = np.arange(n) / SR
    return (np.sin(2 * np.pi * 1320 * t) + .5 * np.sin(2 * np.pi * 1980 * t)) * env(n, .002, .05)

def buzz(d=.6):
    n = int(d * SR); t = np.arange(n) / SR
    return np.sign(np.sin(2 * np.pi * 150 * t)) * .4 * (np.sin(2 * np.pi * 8 * t) > 0) * np.minimum(1, (d - t) / .05)

DM, CALL, STORE, TRACK, S1, S2, S3, MONEY, END = 1.9, 4.35, 6.2, 10.6, 10.8, 11.9, 13.0, 13.9, 16.95
intro(DM)
for i in range(8): add(ping(), DM + i * .28, .3, (-.3, .3)[i % 2])          # "بكام؟" x8
add(buzz(), CALL, .4); add(buzz(), CALL + .9, .35)                          # courier calling
add(whoosh(.45, up=True), STORE - .25, .35); add(paper_slap(), STORE + .2, .5)
groove(STORE, END)
add(whoosh(.35, up=False), TRACK, .3)
for at in (S1, S2, S3): add(tape_rip(), at - .05, .45); add(stamp(), at + .1, .4)   # a sticker at each stop
for at in (S1 + .4, S2 + .4): add(whoosh(.5, up=True), at, .2)                      # the parcel slides on
add(stamp(), MONEY + .05, .55); add(ding(), MONEY + .25, .25)
end_card(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15)
write(os.path.join(HERE, 'sfx.wav'))
