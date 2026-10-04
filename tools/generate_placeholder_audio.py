#!/usr/bin/env python3
"""Generates the placeholder game sounds into MonoTanx/Content/Audio/.

The sounds are synthetic and deterministic (fixed random seed), so re-running
this gives byte-identical files. They exist so every audio hook can be tested
before real assets are available. To use a real sound, replace the file with a
16-bit PCM WAV of the same name; see MonoTanx/Content/Audio/README.md.

Usage (from the repository root):  python3 tools/generate_placeholder_audio.py
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "MonoTanx", "Content", "Audio")


def write(name, samples):
    peak = max(abs(s) for s in samples) or 1.0
    scale = 0.85 / peak if peak > 0.85 else 1.0  # keep peaks below clipping
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32000)) for s in samples))
    print(f"wrote {name}: {len(samples) / RATE:.2f}s")


def lowpassed_noise(rng, n, smoothing):
    """White noise through a one-pole low-pass filter (higher smoothing = darker)."""
    out, y = [], 0.0
    for _ in range(n):
        y += (rng.random() * 2 - 1 - y) * (1.0 - smoothing)
        out.append(y)
    return out


def fire():  # classic 'boosh'
    rng = random.Random(1)
    n = int(RATE * 0.45)
    phase, out = 0.0, []
    for i in range(n):
        t = i / RATE
        phase += 2 * math.pi * (110.0 * math.exp(-3.0 * t) + 40.0) / RATE
        thump = math.sin(phase) * 0.8
        crack = (rng.random() * 2 - 1) * 0.9 * math.exp(-14.0 * t)
        out.append((thump + crack) * math.exp(-7.0 * t) * 1.4)
    return out


def reload():  # gun being cocked: two sharp clicks
    rng = random.Random(2)
    n = int(RATE * 0.30)
    out = [0.0] * n
    for start, strength in ((0.0, 1.0), (0.11, 0.8)):
        s0 = int(start * RATE)
        for i in range(int(RATE * 0.03)):
            if s0 + i < n:
                t = i / RATE
                out[s0 + i] += (rng.random() * 2 - 1) * strength * math.exp(-160.0 * t)
                out[s0 + i] += math.sin(2 * math.pi * 1800.0 * t) * 0.5 * strength * math.exp(-220.0 * t)
    return out


def explosion():  # loud: dark noise plus a low rumble, long decay
    rng = random.Random(3)
    n = int(RATE * 1.0)
    noise = lowpassed_noise(rng, n, 0.82)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        phase += 2 * math.pi * (70.0 * math.exp(-1.5 * t) + 25.0) / RATE
        out.append((noise[i] * 3.0 + math.sin(phase) * 0.9) * math.exp(-3.2 * t))
    return out


def ping():  # a ricochet: bright, short, decaying tone
    n = int(RATE * 0.35)
    out = []
    for i in range(n):
        t = i / RATE
        tone = math.sin(2 * math.pi * 1760.0 * t) + 0.4 * math.sin(2 * math.pi * 3520.0 * t)
        out.append(tone * math.exp(-11.0 * t))
    return out


def crump():  # dull thud on a solid surface
    rng = random.Random(4)
    n = int(RATE * 0.28)
    noise = lowpassed_noise(rng, n, 0.9)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        phase += 2 * math.pi * (90.0 * math.exp(-8.0 * t) + 45.0) / RATE
        out.append((noise[i] * 2.5 + math.sin(phase) * 0.8) * math.exp(-13.0 * t))
    return out


def pickup():  # classic 'ta-da': two rising notes
    out = []
    for freq, dur in ((660.0, 0.12), (990.0, 0.35)):
        for i in range(int(RATE * dur)):
            t = i / RATE
            tone = math.sin(2 * math.pi * freq * t) + 0.3 * math.sin(2 * math.pi * freq * 2 * t)
            out.append(tone * min(1.0, t * 400.0) * math.exp(-5.0 * t))
    return out


def engine():  # one second of drone; whole cycles so it loops without a click
    n = RATE
    out = []
    for i in range(n):
        t = i / RATE
        body = (math.sin(2 * math.pi * 55.0 * t) + 0.6 * math.sin(2 * math.pi * 110.0 * t)
                + 0.3 * math.sin(2 * math.pi * 165.0 * t))
        out.append(body * (0.8 + 0.2 * math.sin(2 * math.pi * 8.0 * t)))
    return out


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, make in (("fire.wav", fire), ("reload.wav", reload), ("explosion.wav", explosion),
                       ("ping.wav", ping), ("crump.wav", crump), ("pickup.wav", pickup), ("engine.wav", engine)):
        write(name, make())
