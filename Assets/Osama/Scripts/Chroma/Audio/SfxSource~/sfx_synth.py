"""Chroma SFX set: one cohesive family of soft bell / air sounds, synthesized from scratch.

Everything is built from a few shared pieces so the set sounds like one instrument:
  * bell()      - celesta/glockenspiel-like tone from inharmonic bar partials with
                  exponential decays and a gentle two-voice chorus on the fundamental
  * air()       - filtered noise through a time-varying state-variable band-pass
  * room()      - the same tiny room for every clip (early taps + damped combs + allpasses)
  * finish()    - DC removal, safety low-pass, raised-cosine in/out to exact zero,
                  peak/loudness normalisation
All melodic material lives in C major pentatonic (C D E G A), the scale Drop_Note is
pitch-shifted along in code.
"""
import numpy as np

SR = 44100
RNG = np.random.default_rng(20260929)

A4 = 440.0


def note(name):
    """'C6' -> Hz (equal temperament, A4 = 440)."""
    steps = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1,
             'A': 0, 'A#': 1, 'B': 2}
    pitch, octave = name[:-1], int(name[-1])
    return A4 * 2 ** ((steps[pitch] + 12 * (octave - 4)) / 12)


def silence(dur):
    return np.zeros(int(round(dur * SR)))


def t_axis(n):
    return np.arange(n) / SR


def cos_ramp(n):
    """0 -> 1 raised cosine over n samples (first sample exactly 0)."""
    if n <= 1:
        return np.ones(max(n, 0))
    i = np.arange(n)
    return 0.5 - 0.5 * np.cos(np.pi * i / (n - 1))


def place(buf, sig, at):
    """Mix sig into buf starting at time `at` (seconds); grows buf if needed."""
    s = int(round(at * SR))
    need = s + len(sig)
    if need > len(buf):
        buf = np.concatenate([buf, np.zeros(need - len(buf))])
    buf[s:s + len(sig)] += sig
    return buf


# ---------------------------------------------------------------- filters

def svf(x, fc, q=0.7071, mode='low'):
    """Zavalishin/Simper TPT state-variable filter. fc may be a scalar or per-sample array."""
    x = np.asarray(x, dtype=np.float64)
    n = len(x)
    fc = np.broadcast_to(np.asarray(fc, dtype=np.float64), (n,))
    q = np.broadcast_to(np.asarray(q, dtype=np.float64), (n,))
    g = np.tan(np.pi * np.clip(fc, 5.0, SR * 0.49) / SR)
    k = 1.0 / q
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    out = np.empty(n)
    ic1 = ic2 = 0.0
    xs = x.tolist()
    a1l, a2l, a3l, kl = a1.tolist(), a2.tolist(), a3.tolist(), k.tolist()
    if mode == 'low':
        for i in range(n):
            v3 = xs[i] - ic2
            v1 = a1l[i] * ic1 + a2l[i] * v3
            v2 = ic2 + a2l[i] * ic1 + a3l[i] * v3
            ic1 = 2 * v1 - ic1
            ic2 = 2 * v2 - ic2
            out[i] = v2
    elif mode == 'band':
        for i in range(n):
            v3 = xs[i] - ic2
            v1 = a1l[i] * ic1 + a2l[i] * v3
            v2 = ic2 + a2l[i] * ic1 + a3l[i] * v3
            ic1 = 2 * v1 - ic1
            ic2 = 2 * v2 - ic2
            out[i] = v1 * kl[i]          # unity gain at the centre frequency
    elif mode == 'high':
        for i in range(n):
            v3 = xs[i] - ic2
            v1 = a1l[i] * ic1 + a2l[i] * v3
            v2 = ic2 + a2l[i] * ic1 + a3l[i] * v3
            ic1 = 2 * v1 - ic1
            ic2 = 2 * v2 - ic2
            out[i] = xs[i] - kl[i] * v1 - v2
    else:
        raise ValueError(mode)
    return out


def lowpass(x, fc, order=2):
    for _ in range(order // 2):
        x = svf(x, fc, 0.7071, 'low')
    return x


def highpass(x, fc, order=2):
    for _ in range(order // 2):
        x = svf(x, fc, 0.7071, 'high')
    return x


# ---------------------------------------------------------------- sources

# bar partials (free-free bar: 1, 2.756, 5.404, 8.933) plus a soft octave for warmth
BAR = [
    # ratio, amp, decay factor (x base tau)
    (1.0, 1.00, 1.00),
    (2.0, 0.10, 0.55),
    (2.756, 0.17, 0.32),
    (5.404, 0.045, 0.14),
]


def bell(freq, dur, tau, amp=1.0, bright=1.0, chorus_cents=3.5, chorus_amp=0.28,
         attack=0.003, partials=BAR, top=None):
    """One struck-bar note. `bright` scales the upper partials, `top` drops partials above it (Hz)."""
    n = int(round(dur * SR))
    t = t_axis(n)
    y = np.zeros(n)
    for ratio, a, dk in partials:
        f = freq * ratio
        if top is not None and f > top:
            continue
        a_eff = a * (1.0 if ratio == 1.0 else bright)
        ph = RNG.uniform(0, 2 * np.pi)
        y += a_eff * np.sin(2 * np.pi * f * t + ph) * np.exp(-t / (tau * dk))
    # gentle chorus: two detuned copies of the fundamental beat slowly against it
    for c in (-chorus_cents, chorus_cents):
        f = freq * 2 ** (c / 1200)
        ph = RNG.uniform(0, 2 * np.pi)
        y += chorus_amp * np.sin(2 * np.pi * f * t + ph) * np.exp(-t / (tau * 1.1))
    na = max(2, int(attack * SR))
    y[:na] *= cos_ramp(na)
    return amp * y


def sine_blip(f0, f1, dur, tau, glide, amp=1.0, attack=0.002):
    """Sine with an exponential pitch glide f0 -> f1 (time constant `glide`) - pops and thumps."""
    n = int(round(dur * SR))
    t = t_axis(n)
    f = f1 + (f0 - f1) * np.exp(-t / glide)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) * np.exp(-t / tau)
    na = max(2, int(attack * SR))
    y[:na] *= cos_ramp(na)
    return amp * y


def noise(n):
    return RNG.standard_normal(n)


def air(dur, f_from, f_to, q, env, curve=1.0, amp=1.0, track=2.2):
    """
    Moving air: noise through two cascaded band-passes whose centre sweeps f_from -> f_to
    (log sweep, `curve` shapes it), then a low-pass that follows the sweep at `track` x the
    centre - the resonance you hear move, without static hiss sitting on top.
    """
    n = int(round(dur * SR))
    u = np.linspace(0, 1, n) ** curve
    fc = f_from * (f_to / f_from) ** u
    y = svf(svf(noise(n), fc, q, 'band'), fc, q, 'band')
    y = svf(y, fc * track, 0.7071, 'low')
    y /= max(1e-9, np.sqrt(np.mean(y ** 2)))
    return amp * 0.25 * y * env(n)


def bump_env(rise, fall_to_zero_at, shape=2.0):
    """Raised-cosine rise to 1, then a smooth power-cosine fall reaching exactly 0."""
    def f(n):
        e = np.zeros(n)
        r = min(n, max(2, int(rise * SR)))
        e[:r] = cos_ramp(r)
        end = min(n, int(fall_to_zero_at * SR))
        m = end - r
        if m > 1:
            i = np.arange(m)
            e[r:end] = (0.5 + 0.5 * np.cos(np.pi * i / (m - 1))) ** shape
        return e
    return f


def glitter(dur, start, end, count, notes, amp, tau=0.035, rng_seed=None):
    """Tiny high pings scattered in time, thinning out - the sparkle that ties the set together."""
    rng = np.random.default_rng(rng_seed)
    buf = np.zeros(int(round(dur * SR)))
    times = np.sort(start + (end - start) * rng.random(count) ** 1.6)
    for i, at in enumerate(times):
        f = note(notes[rng.integers(len(notes))])
        fade = 1.0 - 0.6 * (at - start) / max(1e-6, end - start)
        a = amp * fade * rng.uniform(0.55, 1.0)
        ping = bell(f, 0.16, tau, a, bright=0.5, chorus_amp=0.0, attack=0.002,
                    partials=[(1.0, 1.0, 1.0), (2.756, 0.12, 0.3)])
        buf = place(buf, ping, at)
    return buf[:int(round(dur * SR))]


def shimmer(dur, freqs, amp, env, trem=(9.0, 13.0), detune=6.0):
    """A soft cluster of high sines, slightly detuned and fluttering - magic dust."""
    n = int(round(dur * SR))
    t = t_axis(n)
    y = np.zeros(n)
    for i, f in enumerate(freqs):
        for c in (-detune, detune):
            ff = f * 2 ** (c / 1200)
            ph = RNG.uniform(0, 2 * np.pi)
            rate = RNG.uniform(*trem)
            tr = 0.65 + 0.35 * np.sin(2 * np.pi * rate * t + RNG.uniform(0, 2 * np.pi))
            y += np.sin(2 * np.pi * ff * t + ph) * tr / (1 + 0.6 * i)
    y /= max(1e-9, np.abs(y).max())
    return amp * y * env(n)


# ---------------------------------------------------------------- the shared room

def _comb(x, d, fb, damp):
    out = np.zeros(len(x))
    buf = [0.0] * d
    idx = 0
    lp = 0.0
    xs = x.tolist()
    for i in range(len(xs)):
        y = buf[idx]
        lp = y * (1 - damp) + lp * damp
        buf[idx] = xs[i] + lp * fb
        out[i] = y
        idx += 1
        if idx == d:
            idx = 0
    return out


def _allpass(x, d, g):
    out = np.zeros(len(x))
    buf = [0.0] * d
    idx = 0
    xs = x.tolist()
    for i in range(len(xs)):
        b = buf[idx]
        y = -xs[i] * g + b
        buf[idx] = xs[i] + b * g
        out[i] = y
        idx += 1
        if idx == d:
            idx = 0
    return out


def room(x, wet=0.18, size=1.0, fb=0.62, damp=0.35, tail=0.5):
    """Tiny, soft room: three early taps + four damped combs + two allpasses (mono)."""
    x = np.concatenate([x, np.zeros(int(tail * SR))])
    pre = int(0.007 * SR)
    xin = np.concatenate([np.zeros(pre), x])[:len(x)]
    er = np.zeros(len(x))
    for ms, g in ((11.0, 0.30), (19.0, 0.22), (29.0, 0.15)):
        d = int(ms * 0.001 * SR * size)
        er[d:] += g * x[:len(x) - d]
    combs = sum(_comb(xin, int(d * size), fb, damp) for d in (1116, 1188, 1277, 1356)) * 0.25
    diff = _allpass(_allpass(combs, int(556 * size), 0.5), int(441 * size), 0.5)
    wetsig = lowpass(er * 0.6 + diff, 5500.0)
    return x * (1 - wet * 0.5) + wetsig * wet


# ---------------------------------------------------------------- finishing

def finish(x, dur, fade_out, fade_in=0.003, hp=35.0, lp=None, lp_order=2):
    """Clean up and cut to `dur` with raised-cosine edges that land exactly on zero."""
    n = int(round(dur * SR))
    if len(x) < n:
        x = np.concatenate([x, np.zeros(n - len(x))])
    x = highpass(x, hp)
    if lp:
        x = lowpass(x, lp, lp_order)
    x = x[:n].copy()
    ni = max(2, int(fade_in * SR))
    x[:ni] *= cos_ramp(ni)
    no = max(2, int(fade_out * SR))
    x[n - no:] *= cos_ramp(no)[::-1]
    x[0] = 0.0
    x[-1] = 0.0
    return x
