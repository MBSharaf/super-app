"""Shared synth kit for reel sound beds: a 104 BPM A-minor groove and paper/tape/stamp/whoosh foley, all generated here
(no licensed audio). A reel's sfx.py does `from sfxkit import *`, places sounds with add(), then calls write()."""

import numpy as np, wave

SR, DUR, BPM = 48000, 15.0, 104
BEAT = 60 / BPM
N = int(SR * DUR)
L = np.zeros(N); R = np.zeros(N)
rng = np.random.default_rng(11)

def add(sig, at, gain=1.0, pan=0.0):
    i = int(at * SR)
    if i >= N: return
    j = min(N, i + len(sig)); s = sig[: j - i] * gain
    L[i:j] += s * (1 - max(0, pan)); R[i:j] += s * (1 + min(0, pan))

def env(n, a=.002, d=.2):
    t = np.arange(n) / SR
    return np.minimum(1, t / a) * np.exp(-t / d)

def lowpass(x, k):
    return np.convolve(x, np.ones(k) / k, 'same')

def kick():
    n = int(.35 * SR); t = np.arange(n) / SR
    f = 50 + 110 * np.exp(-t * 28)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7) + .3 * rng.standard_normal(n) * np.exp(-t * 300)

def clap():
    n = int(.25 * SR); x = rng.standard_normal(n)
    x = x - lowpass(x, 12)                                # high-pass
    e = np.zeros(n)
    for k, d in ((0, .012), (.011, .012), (.022, .012), (.03, .09)):  # three quick hits and a tail
        s = int(k * SR); e[s:] += env(n - s, .001, d)
    return x * e

def hat(open_=False):
    n = int((.22 if open_ else .05) * SR); x = rng.standard_normal(n)
    x = x - lowpass(x, 4)
    return x * env(n, .001, .09 if open_ else .015)

def bass(freq, d):
    n = int(d * SR); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * freq * t) + .5 * np.sign(np.sin(2 * np.pi * freq * t)) * .3
    return lowpass(s, 20) * np.minimum(1, t / .005) * np.exp(-t * 3.5)

def stab(freqs, d=.18, bright=1.0):
    n = int(d * SR); t = np.arange(n) / SR
    s = sum(np.sign(np.sin(2 * np.pi * f * t)) * .5 + np.sin(2 * np.pi * f * 2 * t) * .3 for f in freqs)
    s = lowpass(s, max(2, int(14 / bright)))
    return s * env(n, .003, d * .45) / len(freqs)

def whoosh(d=.35, up=True):
    n = int(d * SR); t = np.arange(n) / SR; x = rng.standard_normal(n)
    out = np.zeros(n); seg = n // 16
    for k in range(16):                                    # sweeping band of noise
        w = 2 + int((16 - k if up else k + 1) * 1.6)
        a, b = k * seg, (k + 1) * seg
        out[a:b] = (x - lowpass(x, w))[a:b] if up else lowpass(x, w)[a:b]
    return out * np.sin(np.pi * t / d) ** 2

def paper_slap():
    n = int(.14 * SR); t = np.arange(n) / SR; x = lowpass(rng.standard_normal(n), 3)
    return (x * np.exp(-t * 50) * .8 + np.sin(2 * np.pi * 140 * t) * np.exp(-t * 45))

def tape_rip():
    n = int(.22 * SR); t = np.arange(n) / SR
    crackle = (rng.random(n) < .18) * rng.standard_normal(n)
    return lowpass(crackle, 2) * np.sin(np.pi * t / .22) * 1.4

def typewriter(k=6, gap=.07):
    out = np.zeros(int((k * gap + .05) * SR))
    for i in range(k):
        n = int(.03 * SR); t = np.arange(n) / SR
        c = (rng.standard_normal(n) - lowpass(rng.standard_normal(n), 3)) * np.exp(-t * 180) + np.sin(2 * np.pi * 1800 * t) * np.exp(-t * 220) * .4
        i0 = int(i * gap * SR * (1 + .15 * rng.standard_normal())); i0 = max(0, i0)
        out[i0:i0 + n] += c[: len(out) - i0]
    return out

def stamp():
    n = int(.25 * SR); t = np.arange(n) / SR
    return np.sin(2 * np.pi * 90 * t) * np.exp(-t * 22) + lowpass(rng.standard_normal(n), 5) * np.exp(-t * 60) * .6

def ding():
    n = int(.7 * SR); t = np.arange(n) / SR
    return (np.sin(2 * np.pi * 1568 * t) + .45 * np.sin(2 * np.pi * 3136 * t)) * env(n, .002, .22)

def whistle(d=.42):
    n = int(SR * d); t = np.arange(n) / SR
    trill = 1 + .04 * np.sin(2 * np.pi * 38 * t)
    s = np.sin(2 * np.pi * 2950 * t * trill) + .3 * np.sin(2 * np.pi * 5900 * t)
    return s * np.minimum(1, t / .015) * np.minimum(1, (d - t) / .06)

def riser(d=.6):
    n = int(d * SR); t = np.arange(n) / SR; x = rng.standard_normal(n)
    return (x - lowpass(x, 3)) * (t / d) ** 2

# Chords: A minor progression, one per bar
A, F, C, G = 110.0, 87.31, 130.81, 98.0
PROG = [(A, [220, 261.63, 329.63]), (F, [174.61, 220, 261.63]), (C, [196, 261.63, 329.63]), (G, [196, 246.94, 293.66])]

def intro(hook_end, whistle_at=None):
    """Filtered stabs under the hook and a riser into the drop."""
    for b in range(4):
        add(stab(PROG[0][1], .16, .35), b * BEAT * .75 + .05, .35, -.2)
    if whistle_at is not None: add(whistle(.42), whistle_at, .22, .2)
    add(riser(.55), hook_end - .55, .25)

def groove(t0, end):
    """Kick, clap, swung hats, bass and chord stabs from t0 to end."""
    bar = 4 * BEAT
    for b in range(int((end - t0) / bar) + 1):
        root, chord = PROG[b % 4]; bt = t0 + b * bar
        for k in (0, 1.5, 2):
            if bt + k * BEAT < end: add(kick(), bt + k * BEAT, .9)
        for k in (1, 3):
            if bt + k * BEAT < end: add(clap(), bt + k * BEAT, .45)
        for k in range(8):
            at = bt + k * BEAT / 2 + (.035 if k % 2 else 0)
            if at < end: add(hat(open_=(k == 7)), at, .16 if k % 2 else .1, .3)
        for k in range(8):
            at = bt + k * BEAT / 2
            if at < end: add(bass(root * (2 if k in (3, 6) else 1), BEAT / 2 * .9), at, .5)
        for k in (0, 1.5, 2.75):
            at = bt + k * BEAT
            if at < end: add(stab(chord, .18), at, .28, -.25)

def steps(times):
    """Whoosh into each step and the caption typing in."""
    for a in times:
        add(whoosh(.32, up=True), a - .18, .32)
        add(typewriter(7), a + .25, .2, .2)

def end_card(at):
    add(whoosh(.4, up=False), at - .2, .35)
    add(stab(PROG[0][1], .5, .8), at + .02, .4)
    add(paper_slap(), at + .05, .6); add(stamp(), at + .55, .4)

def write(path='sfx.wav'):
    """Fade the last 120 ms, normalise and write 16-bit stereo."""
    fade = np.ones(N); fade[-int(.12 * SR):] = np.linspace(1, 0, int(.12 * SR))
    st = np.stack([L * fade, R * fade], 1)
    st /= max(1e-9, np.abs(st).max() / .95)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((st * 32767).astype('<i2').tobytes())
    print('wrote', path)
