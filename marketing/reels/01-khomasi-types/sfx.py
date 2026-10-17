"""Synthesizes sfx.wav for reel 01: a referee whistle on the hook, a paper 'slap' as each
sticker lands, a soft 'ding' on the Super App line, and a double whistle on the end card.
Everything is generated here, so there is no licensing question."""
import numpy as np, wave
SR, DUR = 48000, 15.0
mix = np.zeros(int(SR * DUR))
rng = np.random.default_rng(7)

def add(sig, at, gain):
    i = int(at * SR); j = min(len(mix), i + len(sig)); mix[i:j] += sig[: j - i] * gain

def whistle(d=.42):
    t = np.arange(int(SR * d)) / SR
    trill = 1 + .04 * np.sin(2 * np.pi * 38 * t)            # the pea rattling
    tone = np.sin(2 * np.pi * 2950 * t * trill) + .35 * np.sin(2 * np.pi * 5900 * t)
    env = np.minimum(1, t / .015) * np.minimum(1, (d - t) / .06)
    return tone * env * (.85 + .15 * rng.standard_normal(len(t)).clip(-1, 1))

def slap(d=.16):
    n = int(SR * d); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    noise = np.convolve(noise, np.ones(6) / 6, 'same')        # soften the hiss
    thump = np.sin(2 * np.pi * 120 * t) * np.exp(-t * 40)
    return (noise * np.exp(-t * 45) * .6 + thump) * np.minimum(1, t / .002)

def ding(d=.6):
    t = np.arange(int(SR * d)) / SR
    return (np.sin(2 * np.pi * 1318.5 * t) + .4 * np.sin(2 * np.pi * 2637 * t)) * np.exp(-t * 7) * np.minimum(1, t / .003)

add(whistle(), .02, .22)
for i in range(5):
    add(slap(), 1.6 + i * 2.4 + .08, .5)
add(ding(), 1.6 + 4 * 2.4 + 1.12, .25)
add(slap(), 13.62, .5)
add(whistle(.16), 14.05, .18); add(whistle(.3), 14.3, .2)

mix /= max(1, np.abs(mix).max() / .9)
pcm = (mix * 32767).astype('<i2')
with wave.open('sfx.wav', 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
print('wrote sfx.wav')
