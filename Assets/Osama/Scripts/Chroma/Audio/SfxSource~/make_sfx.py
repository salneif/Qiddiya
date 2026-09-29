"""
Render the Chroma SFX set (44.1 kHz mono 16-bit WAV) - needs numpy only.

    python make_sfx.py <output folder> [preview.wav]

Point it at Assets/Osama/Resources/Chroma/Sfx to overwrite the clips in place (their .meta
files keep the GUIDs). The noise is seeded, so the same script always renders the same set.
Each clip is scaled to its momentary-loudness target (K-weighted, 400 ms) and never above
-3 dBFS; the table printed at the end re-reads the written files.
"""
import os
import sys
import wave

import numpy as np

import sfx_synth as S
from sfx_synth import SR, note, bell, place, room, finish, air, bump_env, glitter, shimmer, sine_blip, \
    lowpass, highpass, svf, noise, cos_ramp, silence
from loud import momentary_max

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

PENTA_HI = ['C7', 'D7', 'E7', 'G7', 'A7', 'C8', 'D8', 'E8']


# ------------------------------------------------------------------ designs

def drop_note():
    """C6 bell: bar partials, soft chorus, short and dark enough to survive +2 octaves."""
    f = note('C6')
    x = bell(f, 0.62, tau=0.12, bright=0.85, chorus_amp=0.24, attack=0.0025, top=6000)
    # felt-mallet tick: a few ms of band-passed noise gives the onset a soft 'tink'
    tick = svf(noise(int(0.012 * SR)), 2600.0, 1.4, 'band') * np.exp(-np.arange(int(0.012 * SR)) / (0.0025 * SR))
    tick[:int(0.002 * SR)] *= cos_ramp(int(0.002 * SR))
    x = place(x, tick * 0.05, 0.0)
    x = room(x, wet=0.10, fb=0.55, damp=0.45, tail=0.2)
    # 4th-order low-pass keeps everything under ~6 kHz: pitched x4 it still stays below 24 kHz
    return finish(x, 0.5, fade_out=0.09, fade_in=0.0025, lp=5200.0, lp_order=4)


def drop_gold():
    """Golden drop: a quick C major arpeggio up to a sparkling C7+E7, with glitter and a warm root."""
    x = S.silence(1.2)
    steps = [('C6', 0.00, 0.10), ('E6', 0.055, 0.10), ('G6', 0.11, 0.12), ('C7', 0.165, 0.34)]
    for i, (nm, at, tau) in enumerate(steps):
        amp = 0.62 + 0.1 * i
        x = place(x, bell(note(nm), 1.0, tau=tau, amp=amp, bright=1.0, attack=0.0025), at)
    x = place(x, bell(note('E7'), 0.9, tau=0.30, amp=0.42, bright=0.7, attack=0.003), 0.172)
    # warm body under the last chord (sine octave below, gentle attack)
    x = place(x, bell(note('C5'), 0.9, tau=0.28, amp=0.22, bright=0.0, chorus_amp=0.0, attack=0.02,
                      partials=[(1.0, 1.0, 1.0)]), 0.165)
    x = place(x, glitter(1.0, 0.2, 0.75, 14, PENTA_HI, 0.16, tau=0.03, rng_seed=7), 0.0)
    sh = shimmer(0.8, [note('C8'), note('G8'), note('E8')], 0.05, bump_env(0.12, 0.8, shape=1.5))
    x = place(x, sh, 0.15)
    x = room(x, wet=0.20, fb=0.62, damp=0.35, tail=0.4)
    return finish(x, 0.9, fade_out=0.22, lp=11000.0)


def drop_burst():
    """Soft pop (a falling sine bubble) + a short rising air puff + two tiny pings."""
    x = S.silence(0.6)
    pop = sine_blip(820.0, 240.0, 0.14, tau=0.035, glide=0.018, amp=0.9, attack=0.002)
    x = place(x, pop, 0.0)
    x = place(x, sine_blip(1640.0, 480.0, 0.08, tau=0.015, glide=0.012, amp=0.18, attack=0.002), 0.0)
    wh = air(0.36, 450.0, 2800.0, 1.3, bump_env(0.05, 0.36, shape=2.2), curve=0.8, amp=0.7)
    x = place(x, wh, 0.01)
    x = place(x, bell(note('G7'), 0.2, tau=0.03, amp=0.12, bright=0.4, chorus_amp=0.0), 0.07)
    x = place(x, bell(note('D8'), 0.2, tau=0.025, amp=0.08, bright=0.4, chorus_amp=0.0), 0.14)
    x = room(x, wet=0.12, fb=0.5, damp=0.4, tail=0.2)
    return finish(x, 0.4, fade_out=0.12, lp=9000.0)


def combo_end():
    """Csus4 -> C major: the F falls to E. Rolled like a harp, with a soft root underneath."""
    x = S.silence(0.9)
    # tension (quiet, short)
    for i, nm in enumerate(['C6', 'F6', 'G6']):
        x = place(x, bell(note(nm), 0.3, tau=0.055, amp=0.34, bright=0.8, attack=0.003), 0.008 * i)
    # resolution (rolled 12 ms apart, longer ring)
    for i, nm in enumerate(['C6', 'E6', 'G6', 'C7']):
        amp = [0.55, 0.62, 0.5, 0.38][i]
        x = place(x, bell(note(nm), 0.8, tau=0.26, amp=amp, bright=0.85, attack=0.003), 0.095 + 0.012 * i)
    x = place(x, bell(note('C5'), 0.8, tau=0.25, amp=0.20, bright=0.0, chorus_amp=0.0, attack=0.025,
                      partials=[(1.0, 1.0, 1.0)]), 0.095)
    x = place(x, bell(note('E8'), 0.2, tau=0.04, amp=0.06, bright=0.3, chorus_amp=0.0), 0.16)
    x = room(x, wet=0.18, fb=0.6, damp=0.38, tail=0.3)
    return finish(x, 0.6, fade_out=0.16, lp=10000.0)


def paper_grain(n, density, amp):
    """Sparse crackle: tiny Hann-windowed noise grains - the 'paper' in a paper whoosh."""
    y = np.zeros(n)
    g = int(0.0025 * SR)
    win = np.hanning(g)
    count = int(density * n / SR)
    for s in S.RNG.integers(0, max(1, n - g), count):
        y[s:s + g] += S.RNG.standard_normal(g) * win * S.RNG.uniform(0.3, 1.0)
    return amp * svf(y, 2800.0, 0.9, 'band')


def skin_open():
    """Rising paper/air whoosh with a faint chime as the page settles open."""
    n = int(0.36 * SR)
    env = bump_env(0.14, 0.30, shape=1.6)
    x = air(0.36, 360.0, 3000.0, 1.5, env, curve=0.8, amp=1.0)
    x = place(x, paper_grain(n, 300, 0.25) * env(n), 0.0)
    x = place(x, bell(note('E7'), 0.2, tau=0.05, amp=0.05, bright=0.3, chorus_amp=0.0), 0.12)
    x = room(x, wet=0.10, fb=0.5, damp=0.45, tail=0.15)
    return finish(x, 0.3, fade_out=0.07, lp=9000.0)


def skin_close():
    """The same page folding shut: falling whoosh, then a very soft low settle."""
    n = int(0.36 * SR)
    env = bump_env(0.06, 0.26, shape=1.6)
    x = air(0.36, 2300.0, 380.0, 1.5, env, curve=1.1, amp=1.0)
    x = place(x, paper_grain(n, 240, 0.22) * env(n), 0.0)
    x = place(x, sine_blip(150.0, 105.0, 0.12, tau=0.028, glide=0.02, amp=0.30, attack=0.003), 0.17)
    x = room(x, wet=0.09, fb=0.5, damp=0.45, tail=0.15)
    return finish(x, 0.3, fade_out=0.07, lp=8000.0)


def skin_move():
    """A soft glass-bead tick for moving between items. On C6 like Drop_Note: the wardrobe steps it
    0 2 4 7 9 12 semitones up, so from C the ticks stay in C major pentatonic with the rest."""
    x = bell(note('C6'), 0.1, tau=0.016, amp=1.0, bright=1.0, chorus_amp=0.0, attack=0.002,
             partials=[(1.0, 1.0, 1.0), (2.756, 0.16, 0.4), (3.9, 0.06, 0.25)])
    x = room(x, wet=0.08, fb=0.45, damp=0.5, tail=0.05)
    return finish(x, 0.08, fade_out=0.03, fade_in=0.002, lp=7000.0)


def skin_equip():
    """Magic: a rushing arpeggio up the scale into a fluttering shimmer, over an airy lift."""
    x = S.silence(1.0)
    seq = ['G5', 'C6', 'E6', 'G6', 'C7', 'E7']
    for i, nm in enumerate(seq):
        amp = 0.42 + 0.05 * i
        tau = 0.08 + 0.05 * i
        x = place(x, bell(note(nm), 0.8, tau=tau, amp=amp, bright=0.8, attack=0.003), 0.032 * i)
    sh = shimmer(0.62, [note('G7'), note('C8'), note('E8'), note('A8')], 0.12, bump_env(0.15, 0.62, shape=1.4))
    x = place(x, sh, 0.1)
    lift = air(0.6, 900.0, 5500.0, 1.6, bump_env(0.25, 0.58, shape=2.0), curve=0.7, amp=0.22)
    x = place(x, lift, 0.0)
    x = room(x, wet=0.20, fb=0.62, damp=0.33, tail=0.35)
    return finish(x, 0.7, fade_out=0.2, lp=12000.0)


def skin_locked():
    """Muted wooden thunk: a short falling tone and a dull knock, no ring. Its weight sits at
    400-600 Hz, not under 300 Hz, so laptop and TV speakers still play it."""
    x = S.silence(0.3)
    x = place(x, sine_blip(220.0, 165.0, 0.25, tau=0.045, glide=0.03, amp=0.35, attack=0.003), 0.0)
    # 3 ms late so the two tones don't peak together: under the -3 dBFS ceiling that costs ~1.5 dB
    x = place(x, sine_blip(560.0, 420.0, 0.12, tau=0.03, glide=0.02, amp=0.8, attack=0.003), 0.003)
    knock = svf(noise(int(0.05 * SR)), 1150.0, 1.1, 'band') * np.exp(-np.arange(int(0.05 * SR)) / (0.009 * SR))
    knock[:int(0.003 * SR)] *= cos_ramp(int(0.003 * SR))
    x = place(x, knock * 1.1, 0.0)
    x = room(x, wet=0.06, fb=0.45, damp=0.6, tail=0.08)
    return finish(x, 0.2, fade_out=0.06, lp=3500.0, hp=45.0)


def skin_unlock():
    """Celebration: a bright four-note run, a big sparkling chord, glitter falling after it."""
    x = S.silence(1.8)
    run = [('G5', 0.00), ('C6', 0.085), ('E6', 0.17), ('G6', 0.255)]
    for i, (nm, at) in enumerate(run):
        x = place(x, bell(note(nm), 0.6, tau=0.11, amp=0.5 + 0.04 * i, bright=0.9, attack=0.003), at)
    chord_at = 0.36
    for i, (nm, amp) in enumerate([('C7', 0.55), ('E7', 0.40), ('G7', 0.30)]):
        x = place(x, bell(note(nm), 1.2, tau=0.42, amp=amp, bright=0.75, attack=0.003), chord_at + 0.01 * i)
    for nm, amp in (('C5', 0.22), ('G5', 0.12)):
        x = place(x, bell(note(nm), 1.1, tau=0.40, amp=amp, bright=0.0, chorus_amp=0.0, attack=0.03,
                          partials=[(1.0, 1.0, 1.0)]), chord_at)
    x = place(x, glitter(1.3, 0.42, 1.12, 22, PENTA_HI, 0.14, tau=0.035, rng_seed=11), 0.0)
    sh = shimmer(0.95, [note('C8'), note('E8'), note('G8')], 0.06, bump_env(0.2, 0.95, shape=1.3))
    x = place(x, sh, chord_at)
    x = room(x, wet=0.22, fb=0.64, damp=0.33, tail=0.45)
    return finish(x, 1.3, fade_out=0.35, lp=12000.0)


def pulse_whoosh():
    """Colour shockwave: a broad airy swell that rises and blooms, with a gentle shimmer riding it."""
    x = S.silence(1.0)
    env = bump_env(0.16, 0.78, shape=2.4)
    wave_ = air(0.8, 240.0, 2200.0, 1.1, env, curve=0.55, amp=1.0, track=2.6)
    x = place(x, wave_, 0.0)
    # a second, thinner band sweeping up into the 3-7 kHz air - without it the swell is a dull 'whump'
    wave2 = air(0.8, 1400.0, 7500.0, 1.6, bump_env(0.22, 0.74, shape=2.6), curve=0.6, amp=0.5)
    x = place(x, wave2, 0.0)
    sh = shimmer(0.66, [note('C7'), note('G7'), note('E8')], 0.16, bump_env(0.2, 0.66, shape=1.8))
    x = place(x, sh, 0.1)
    body = sine_blip(95.0, 72.0, 0.5, tau=0.16, glide=0.15, amp=0.16, attack=0.06)
    x = place(x, body, 0.02)
    x = room(x, wet=0.14, fb=0.58, damp=0.4, tail=0.2)
    return finish(x, 0.8, fade_out=0.12, lp=12000.0)


def land_puff():
    """Very soft dust 'fff' with a faint low touch of weight."""
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    env = np.exp(-t / 0.055)
    env[:int(0.004 * SR)] *= cos_ramp(int(0.004 * SR))
    dust = highpass(lowpass(noise(n), 1700.0, 4), 220.0) * env
    x = dust * 0.9
    x = place(x, sine_blip(85.0, 62.0, 0.2, tau=0.035, glide=0.03, amp=0.35, attack=0.004), 0.0)
    return finish(x, 0.25, fade_out=0.08, fade_in=0.004, lp=2500.0)


# name, design, momentary-loudness target (K-weighted LUFS, 400 ms), intended role
SET = [
    ('Drop_Note', drop_note, -19.0),
    ('Drop_Gold', drop_gold, -15.5),
    ('Drop_Burst', drop_burst, -18.5),
    ('Combo_End', combo_end, -16.5),
    ('Skin_Open', skin_open, -20.5),
    ('Skin_Close', skin_close, -21.0),
    ('Skin_Move', skin_move, -23.5),
    ('Skin_Equip', skin_equip, -16.5),
    ('Skin_Locked', skin_locked, -20.5),
    ('Skin_Unlock', skin_unlock, -15.5),
    ('Pulse_Whoosh', pulse_whoosh, -17.5),
    ('Land_Puff', land_puff, -25.5),
]

CEILING = 10 ** (-3.0 / 20)   # -3 dBFS


def write_wav(path, x):
    pcm = np.clip(np.round(x * 32767.0), -32768, 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    return pcm


def read_wav(path):
    with wave.open(path, 'rb') as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2 and w.getframerate() == SR, path
        data = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2')
    return data


def main():
    os.makedirs(OUT, exist_ok=True)
    rendered = []
    for name, fn, target in SET:
        x = fn()
        m = momentary_max(x[:, None], SR)
        gain = 10 ** ((target - m) / 20)
        peak = np.abs(x).max() * gain
        if peak > CEILING:
            gain *= CEILING / peak
        x = x * gain
        rendered.append((name, x))
        write_wav(os.path.join(OUT, name + '.wav'), x)

    print(f"{'clip':14s} {'dur':>6s} {'peak':>7s} {'rms':>7s} {'M(LUFS)':>8s} first last  maxstep")
    preview = []
    for name, _ in rendered:
        pcm = read_wav(os.path.join(OUT, name + '.wav'))
        f = pcm.astype(np.float64) / 32768.0
        peak = 20 * np.log10(np.abs(f).max())
        rms = 20 * np.log10(np.sqrt(np.mean(f ** 2)))
        m = momentary_max(f[:, None], SR)
        step = np.abs(np.diff(f)).max()
        print(f"{name:14s} {len(f) / SR:6.3f} {peak:7.2f} {rms:7.2f} {m:8.2f} {int(pcm[0]):5d} {int(pcm[-1]):4d}  {step:.4f}")
        preview.append(f)
        preview.append(np.zeros(int(0.4 * SR)))
    if PREVIEW:
        write_wav(PREVIEW, np.concatenate(preview))
        print('preview ->', PREVIEW)


if __name__ == '__main__':
    main()
