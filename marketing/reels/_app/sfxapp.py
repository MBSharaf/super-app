"""Sound kit for the app-promo reels: a clean 118 BPM four-on-the-floor bed with bright plucks (no licensed audio),
UI foley (tap, swipe, success chime, key clicks) and the voiceover ducking from the shared kit.
A reel's sfx.py does `from sfxapp import *`, calls setup(dur), places sounds, then voice() and write()."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '_kit'))
import sfxkit as K
from sfxkit import add, env, lowpass, kick, clap, hat, whoosh, ding, riser, voice, write, SR, rng  # noqa: F401
import numpy as np

BPM = 118
BEAT = 60 / BPM

def setup(dur):
    K.setup(dur)

def pluck(freq, d=.22, bright=1.0):
    n = int(d * SR); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * freq * t) + .35 * np.sin(2 * np.pi * freq * 2 * t) * bright + .15 * np.sin(2 * np.pi * freq * 3 * t) * bright
    return s * env(n, .002, d * .35)

def sub(freq, d):
    n = int(d * SR); t = np.arange(n) / SR
    return np.sin(2 * np.pi * freq * t) * np.minimum(1, t / .01) * np.exp(-t * 2.5)

# F major-ish lift: F, Am, C, G (one chord per bar)
CH = [(87.31, [349.23, 440.0, 523.25]), (110.0, [329.63, 440.0, 523.25]), (130.81, [392.0, 523.25, 659.25]), (98.0, [392.0, 493.88, 587.33])]

def bed(t0, end, soft_until=None):
    """Kick on every beat, off-beat hats, a clap on 2 and 4, sub bass and a pluck arpeggio. Before soft_until only plucks."""
    bar = 4 * BEAT
    for b in range(int((end - t0) / bar) + 1):
        root, ch = CH[b % 4]; bt = t0 + b * bar
        for k in range(4):
            at = bt + k * BEAT
            if at >= end: break
            full = soft_until is None or at >= soft_until
            if full:
                add(kick(), at, .75)
                if k in (1, 3): add(clap(), at, .3)
                add(hat(), at + BEAT / 2, .14, .25)
                add(sub(root, BEAT * .95), at, .45)
            for j, f in enumerate(ch + [ch[1] * 2]):
                a2 = at + j * BEAT / 4
                if a2 < end: add(pluck(f, .2, .8 if full else .5), a2, .16 if full else .1, (-.3, .3)[j % 2])

def tap():
    n = int(.06 * SR); t = np.arange(n) / SR
    return (np.sin(2 * np.pi * 1900 * t) * .6 + (rng.standard_normal(n) - lowpass(rng.standard_normal(n), 3)) * .5) * np.exp(-t * 120)

def swipe(d=.28):
    return whoosh(d, up=True)

def success():
    out = np.zeros(int(.8 * SR))
    for k, f in enumerate((783.99, 987.77, 1318.51)):
        s = pluck(f, .5, 1.2); i = int(k * .09 * SR); out[i:i + len(s)] += s[: len(out) - i]
    return out

def keys(n_keys, gap=.075):
    out = np.zeros(int((n_keys * gap + .05) * SR))
    for k in range(n_keys):
        n = int(.035 * SR); t = np.arange(n) / SR
        c = (rng.standard_normal(n) - lowpass(rng.standard_normal(n), 3)) * np.exp(-t * 160) + np.sin(2 * np.pi * 2400 * t) * np.exp(-t * 260) * .3
        i = int(k * gap * SR); out[i:i + n] += c[: len(out) - i]
    return out

def scene_cuts(times):
    for at in times: add(swipe(), at - .12, .32)

def taps_at(times):
    for at in times: add(tap(), at, .7)

def end_hit(at):
    add(riser(.5), at - .5, .2); add(kick(), at, .9); add(success(), at + .05, .35)
