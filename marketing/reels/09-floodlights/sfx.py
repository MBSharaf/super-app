"""Sound bed for reel 09 (floodlight board): mains hum and relay clunks for each row that lights, the shared groove,
foley and the voiceover on top."""
import sys, os, glob; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, '..', '_kit'))
from sfxkit import *
setup(22.0)

def hum(d):
    n = int(d * SR); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * 50 * t) + .5 * np.sin(2 * np.pi * 100 * t) + .25 * np.sin(2 * np.pi * 150 * t)
    return s * np.minimum(1, t / .4) * np.minimum(1, (d - t) / .4)

def clunk():
    n = int(.3 * SR); t = np.arange(n) / SR
    return np.sin(2 * np.pi * 70 * t) * np.exp(-t * 30) + lowpass(rng.standard_normal(n), 4) * np.exp(-t * 80) * .7 + hum(.3) * .25

BOARD, CX, WL, RE, SCAN, END = 4.2, 10.25, 11.15, 13.2, 14.25, 17.15
add(hum(4.0), .0, .12)                                              # the board hums in the dark
intro(BOARD)
groove(BOARD, END)
for at in (5.0, 5.8, 6.6, 7.4): add(clunk(), at, .55); add(stamp(), at + .15, .25)   # deposits light rows
add(clunk(), CX, .45); add(whoosh(.3, up=False), CX, .25)           # cancelled: lights out
add(whoosh(.35, up=True), WL - .2, .3); add(ding(), WL + .1, .3)   # waitlist notification
add(clunk(), RE, .55)                                               # booked again
add(whoosh(.32, up=True), SCAN - .18, .3); add(ding(), SCAN + .3, .2); add(typewriter(9, .06), SCAN + .6, .16)
end_card(END)
vo = sorted(glob.glob(os.path.join(HERE, 'voiceover.*')))
if vo: voice(vo[0], at=.15)
write(os.path.join(HERE, 'sfx.wav'))
