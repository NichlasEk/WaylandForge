#!/usr/bin/env python3
"""Build deterministic, local RTS combat effects for Stormakt 3020."""
from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path

RATE = 48_000
OUT = Path("assets/stormakt3020/sfx")


def env(t: float, length: float, attack: float = .005, decay: float = 3.0) -> float:
    return min(1.0, t / attack) * math.exp(-decay * t / length)


def write(name: str, length: float, synth) -> None:
    rng = random.Random(3020 + sum(map(ord, name)))
    samples = []
    for i in range(int(length * RATE)):
        t = i / RATE
        value = max(-.96, min(.96, synth(t, length, rng)))
        samples.append(int(value * 32767))
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / name), "wb") as wav:
        wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(RATE)
        wav.writeframes(b"".join(struct.pack("<hh", s, s) for s in samples))


def noise_burst(t, length, rng, bass=58, crack=1.0):
    e = env(t, length)
    return e * (.48 * math.sin(2 * math.pi * bass * t * (1 - .35 * t / length)) + crack * rng.uniform(-.25, .25))


def main() -> None:
    write("rts-build-place.wav", .72, lambda t,l,r: env(t,l,.01,2.5) * (.34*math.sin(2*math.pi*(86+260*t)*t)+.18*math.sin(2*math.pi*43*t)) + (r.uniform(-.08,.08) if t < .09 else 0))
    write("rts-carolean-volley.wav", .82, lambda t,l,r: sum(noise_burst(max(0,t-d), l-d, r, 72+d*40, 1.25) if t >= d else 0 for d in (0,.035,.071))*.62)
    write("rts-moose-charge.wav", 1.05, lambda t,l,r: env(t,l,.02,1.7)*(.22*math.sin(2*math.pi*(54-8*t)*t)+.15*math.sin(2*math.pi*91*t)+(r.uniform(-.18,.18) if int(t*12)%3==0 else 0)))
    write("rts-tower-fire.wav", .58, lambda t,l,r: noise_burst(t,l,r,48,1.05) + (.18*math.sin(2*math.pi*620*t)*env(t,l,.002,7)))
    write("rts-raid-horn.wav", 2.20, lambda t,l,r: env(t,l,.08,1.0)*(.32*math.sin(2*math.pi*(146+4*math.sin(t*5))*t)+.18*math.sin(2*math.pi*292*t)+.10*math.sin(2*math.pi*438*t)))
    write("rts-powder-fuse.wav", 1.25, lambda t,l,r: env(t,l,.01,.7)*(r.uniform(-.22,.22)*(0.4+0.6*math.sin(2*math.pi*17*t)**2)+.08*math.sin(2*math.pi*950*t)))
    write("rts-powder-explosion.wav", 1.45, lambda t,l,r: noise_burst(t,l,r,39,1.45) + .2*math.sin(2*math.pi*24*t)*env(t,l,.003,1.5))
    write("rts-organ-volley.wav", 1.18, lambda t,l,r: sum(noise_burst(max(0,t-d),l-d,r,52+d*20,.8) if t>=d else 0 for d in (0,.055,.11,.17,.23))*.42)
    write("rts-unit-ready.wav", .52, lambda t,l,r: env(t,l,.01,2.2)*(.22*math.sin(2*math.pi*220*t)+.16*math.sin(2*math.pi*330*t)+.08*math.sin(2*math.pi*440*t)))
    write("rts-engine-ignition.wav", 3.80, lambda t,l,r: min(1,t/.9)*(.28*math.sin(2*math.pi*(31+9*min(1,t/2))*t)+.14*math.sin(2*math.pi*(62+18*min(1,t/2))*t)+r.uniform(-.11,.11)*(1-.65*min(1,t/2))))


if __name__ == "__main__":
    main()
