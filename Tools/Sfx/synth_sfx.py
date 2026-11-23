"""Synthesizes the small UI / feedback sounds the game was missing, so no outside sound files are needed.

  python synth_sfx.py              every sound
  python synth_sfx.py Gulp Purr    only the named ones (the rest stay as they are)

Writes 44.1 kHz mono WAVs to Assets/Resources/Sfx and copies the two unused project sounds (splash, paper) there.
Every sound is short, soft and warm (wood, brass, glass), to sit with the cozy shop.
After this (or eleven_sfx.py) run Tools/Sfx/level_sfx.cs in the editor to bring every sound to its loudness target.
"""
import shutil
import sys
from pathlib import Path
import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Resources" / "Sfx"
SR = 44100
rng = np.random.default_rng(11)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def env(n, attack=0.004, decay=0.2):
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def lowpass(x, cutoff):
    # one-pole low-pass, enough for softening noise
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


# Sounds that now come from ElevenLabs recordings: never overwritten here.
ELEVEN = {"PageTurn", "CardDeal", "Paper", "Card", "StampThud", "SlimePop", "Drawer", "Splash", "Flop", "Serve",
          "FoundBell", "BrewDone", "DoorBell", "Fanfare"}


ONLY = set(sys.argv[1:])


def save(name, x, gain=0.8):
    if ONLY and name not in ONLY:
        return
    if name in ELEVEN:  # replaced by an ElevenLabs recording (Tools/Sfx/eleven_sfx.py)
        print(name, "kept (ElevenLabs)")
        return
    x = x / (np.max(np.abs(x)) + 1e-9) * gain
    fade = min(len(x), int(SR * 0.01))
    x[-fade:] *= np.linspace(1, 0, fade)
    wavfile.write(OUT / f"{name}.wav", SR, (x * 32767).astype(np.int16))
    print(name, f"{len(x) / SR:.2f}s")


OUT.mkdir(parents=True, exist_ok=True)

# Shop door bell: a small brass bell, inharmonic partials, struck twice (the bell swings back).
def bell(f0, dur):
    x = t(dur)
    partials = [(1.0, 1.0, 0.9), (2.76, 0.55, 0.5), (5.40, 0.3, 0.25), (8.93, 0.15, 0.12)]
    return sum(a * np.sin(2 * np.pi * f0 * r * x) * np.exp(-x / d) for r, a, d in partials)
b = bell(1480, 1.6)
second = np.zeros_like(b)
lag = int(SR * 0.18)
second[lag:] = 0.55 * bell(1520, 1.6)[:len(b) - lag]
save("DoorBell", (b + second) * env(len(b), 0.002, 1.2), 0.7)

# Dialogue blip: a soft, short rounded tone; the game varies its pitch per character.
x = t(0.07)
blip = np.sin(2 * np.pi * 520 * x) + 0.3 * np.sin(2 * np.pi * 1040 * x)
save("Blip", blip * env(len(x), 0.003, 0.025), 0.5)

# Stamp thud: a low wooden knock with a short papery slap on top.
x = t(0.35)
body = np.sin(2 * np.pi * np.cumsum(np.interp(x, [0, 0.05, 0.35], [170, 85, 70])) / SR)
slap = lowpass(rng.standard_normal(len(x)), 3000) * np.exp(-x / 0.012)
save("StampThud", body * env(len(x), 0.001, 0.09) + 0.9 * slap, 0.95)

# Score tick: a tiny wooden click (counting up a row).
x = t(0.05)
click = np.sin(2 * np.pi * 1800 * x) * np.exp(-x / 0.006) + 0.5 * lowpass(rng.standard_normal(len(x)), 4000) * np.exp(-x / 0.003)
save("Tick", click, 0.45)

# Choice click: a soft glass/wood pop for picking a bottle or ink.
x = t(0.12)
pop = np.sin(2 * np.pi * np.cumsum(np.interp(x, [0, 0.03, 0.12], [900, 620, 600])) / SR)
save("Select", pop * env(len(x), 0.002, 0.04), 0.55)

# Brew done: a little two-note glass chime with sparkle.
def chime(f, dur):
    x = t(dur)
    return (np.sin(2 * np.pi * f * x) + 0.4 * np.sin(2 * np.pi * f * 2.01 * x) + 0.15 * np.sin(2 * np.pi * f * 3.9 * x)) * np.exp(-x / 0.45)
c1, c2 = chime(1046.5, 1.2), chime(1568.0, 1.2)
done = np.zeros(int(SR * 1.35))
done[:len(c1)] += c1
done[int(SR * 0.12):int(SR * 0.12) + len(c2)] += c2[:len(done) - int(SR * 0.12)]
save("BrewDone", done * env(len(done), 0.003, 0.8), 0.6)

# Serve: a short rising "ta-da" pluck pair.
x = t(0.5)
pluck = lambda f: np.sin(2 * np.pi * f * x) * np.exp(-x / 0.14) + 0.3 * np.sin(2 * np.pi * 2 * f * x) * np.exp(-x / 0.08)
serve = pluck(659.3)
serve[int(SR * 0.09):] += pluck(987.8)[:len(x) - int(SR * 0.09)]
save("Serve", serve, 0.55)

# Slime pop: a wet bubble, pitch shooting up.
x = t(0.25)
bub = np.sin(2 * np.pi * np.cumsum(np.interp(x, [0, 0.08, 0.25], [180, 620, 700])) / SR)
save("SlimePop", bub * env(len(x), 0.002, 0.07), 0.7)

# Low-time pulse: a soft heartbeat-like double thump for the last seconds.
x = t(0.5)
thump = lambda: np.sin(2 * np.pi * 60 * t(0.14)) * np.exp(-t(0.14) / 0.04)
beat = np.zeros(len(x))
beat[:len(thump())] += thump()
off = int(SR * 0.2)
beat[off:off + len(thump())] += 0.7 * thump()
save("Heartbeat", beat, 0.7)

# Drawer: a wooden drawer sliding out, ending in a soft knock.
x = t(0.45)
slide = lowpass(rng.standard_normal(len(x)), 900) * np.interp(x, [0, 0.05, 0.3, 0.36, 0.45], [0, 0.6, 0.5, 0.1, 0])
knock = np.zeros(len(x))
k = t(0.1)
start = int(SR * 0.33)
knock[start:start + len(k)] = np.sin(2 * np.pi * 140 * k) * np.exp(-k / 0.025)
save("Drawer", slide + 0.9 * knock, 0.65)

# Hover: the faintest glassy tap, for pointing at a bottle or ink.
x = t(0.06)
save("Hover", np.sin(2 * np.pi * 2300 * x) * np.exp(-x / 0.01), 0.25)

# Card: a sheet of parchment dropped on the counter.
x = t(0.3)
swish = lowpass(rng.standard_normal(len(x)), 2500) * np.interp(x, [0, 0.1, 0.22, 0.3], [0, 0.35, 0.9, 0])
pat = np.zeros(len(x))
k = t(0.06)
pat[int(SR * 0.22):int(SR * 0.22) + len(k)] = lowpass(rng.standard_normal(len(k)), 1200) * np.exp(-k / 0.012) * 1.6
save("Card", swish + pat, 0.6)

# Page turn: a quick papery swish with a soft flap at the end.
x = t(0.28)
sw = lowpass(rng.standard_normal(len(x)), 3200) * np.interp(x, [0, 0.04, 0.16, 0.28], [0, 0.8, 0.5, 0])
flap = np.zeros(len(x)); k = t(0.05)
flap[int(SR * 0.17):int(SR * 0.17) + len(k)] = lowpass(rng.standard_normal(len(k)), 900) * np.exp(-k / 0.01)
save("PageTurn", sw + 0.8 * flap, 0.55)

# Card deal: a tiny flick of paper landing.
x = t(0.12)
flick = lowpass(rng.standard_normal(len(x)), 4000) * np.interp(x, [0, 0.02, 0.06, 0.12], [0, 1, 0.3, 0])
save("CardDeal", flick, 0.4)

# Found: a small hand bell, rung twice quickly.
fb = bell(2093, 1.1)
fb2 = np.zeros_like(fb); lag = int(SR * 0.11)
fb2[lag:] = 0.8 * bell(2093, 1.1)[:len(fb) - lag]
save("FoundBell", (fb + fb2) * env(len(fb), 0.002, 0.7), 0.6)

# Step chime: one soft glass note, for news from downstairs.
save("StepChime", chime(1318.5, 0.9) * env(int(SR * 0.9), 0.003, 0.5), 0.45)

# Click: a short wooden tick for any button.
x = t(0.04)
save("Click", (np.sin(2 * np.pi * 1300 * x) * np.exp(-x / 0.006) +
               0.4 * lowpass(rng.standard_normal(len(x)), 5000) * np.exp(-x / 0.002)), 0.4)

# Fanfare: a bright rising arpeggio for a fine brew.
notes = [1046.5, 1318.5, 1568.0, 2093.0]
fan = np.zeros(int(SR * 1.4))
for i, f0 in enumerate(notes):
    c = chime(f0, 1.0)
    o = int(SR * 0.09 * i)
    fan[o:o + len(c)] += c[:len(fan) - o] * (0.8 + 0.1 * i)
save("Fanfare", fan * env(len(fan), 0.003, 0.9), 0.65)

# Flop: a dull, deflating "bwomp" for a failed brew.
x = t(0.6)
fr = np.interp(x, [0, 0.6], [220, 90])
flop = np.sin(2 * np.pi * np.cumsum(fr) / SR) + 0.3 * np.sin(4 * np.pi * np.cumsum(fr) / SR)
save("Flop", lowpass(flop, 1200) * env(len(x), 0.01, 0.3), 0.6)

# A customer's gulp: a short wet "glug", a falling throat tone over a soft noise burst.
x = t(0.2)
fr = np.interp(x, [0, 0.05, 0.2], [210, 150, 85])
glug = np.sin(2 * np.pi * np.cumsum(fr) / SR) * env(len(x), 0.008, 0.06)
wet = lowpass(rng.normal(0, 1, len(x)), 700) * env(len(x), 0.002, 0.03)
save("Gulp", glug + 0.6 * wet, 0.7)

# A thrown ingredient: air rushing past, swelling and fading, its brightness rising and falling with it.
x = t(0.32)
noise = rng.normal(0, 1, len(x))
swell = np.sin(np.pi * np.clip(x / 0.32, 0, 1)) ** 2
whoosh = lowpass(noise, 1400) - lowpass(noise, 300)
save("Whoosh", whoosh * swell, 0.55)

# The cauldron fire flaring up: a low "fwoomp" with a few crackles riding on it.
x = t(0.7)
body = lowpass(rng.normal(0, 1, len(x)), 260) * np.interp(x, [0, 0.08, 0.7], [0, 1, 0])
crack = np.zeros(len(x))
for at in rng.uniform(0.03, 0.6, 9):
    i = int(at * SR)
    n = int(SR * 0.006)
    crack[i:i + n] += rng.normal(0, 1, n) * np.exp(-np.arange(n) / (SR * 0.0015)) * rng.uniform(0.4, 1)
save("FireFlare", body * 1.4 + lowpass(crack, 5000) * 0.8, 0.6)

# A sleeping cat's purr: low rumble pulsing about 25 times a second, in and out on two breaths.
x = t(1.8)
rumble = lowpass(rng.normal(0, 1, len(x)), 140)
pulse = 0.55 + 0.45 * np.sin(2 * np.pi * 26 * x)
breath = np.where(x < 0.9, np.sin(np.pi * x / 0.9), 0.75 * np.sin(np.pi * (x - 0.9) / 0.9)) ** 0.7
save("Purr", rumble * pulse * breath, 0.6)

# Reused project sounds that were not wired to anything.
if ONLY:
    sys.exit(0)
if "Splash" not in ELEVEN:
    shutil.copy(ROOT / "Assets/Arts/Models/gg/Soundeffect/splash.wav", OUT / "Splash.wav")
if "Paper" not in ELEVEN:
    shutil.copy(ROOT / "Assets/Arts/Models/gg/Soundeffect/paper.mp3", OUT / "Paper.mp3")
import glob
tone = glob.glob(str(ROOT / "Assets/Arts/Audio/Workshop/Workshop_Room_Tone.*"))
tone = [p for p in tone if not p.endswith(".meta")]
if tone:
    shutil.copy(tone[0], OUT / Path(tone[0]).name)
print("copied Splash, Paper, room tone")
