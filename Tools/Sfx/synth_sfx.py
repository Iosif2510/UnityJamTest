"""Synthesizes the small UI / feedback sounds the game was missing, so no outside sound files are needed.

  python synth_sfx.py

Writes 44.1 kHz mono WAVs to Assets/Resources/Sfx and copies the two unused project sounds (splash, paper) there.
Every sound is short, soft and warm (wood, brass, glass), to sit with the cozy shop.
"""
import shutil
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


def save(name, x, gain=0.8):
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

# Reused project sounds that were not wired to anything.
shutil.copy(ROOT / "Assets/Arts/Models/gg/Soundeffect/splash.wav", OUT / "Splash.wav")
shutil.copy(ROOT / "Assets/Arts/Models/gg/Soundeffect/paper.mp3", OUT / "Paper.mp3")
import glob
tone = glob.glob(str(ROOT / "Assets/Arts/Audio/Workshop/Workshop_Room_Tone.*"))
tone = [p for p in tone if not p.endswith(".meta")]
if tone:
    shutil.copy(tone[0], OUT / Path(tone[0]).name)
print("copied Splash, Paper, room tone")
