"""Approximate ITU-R BS.1770 loudness (K-weighted, gated) via per-block FFT weighting."""
import numpy as np

# K-weighting biquads (48 kHz reference coefficients) evaluated at physical frequencies
_B1 = [1.53512485958697, -2.69169618940638, 1.19839281085285]
_A1 = [1.0, -1.69065929318241, 0.73248077421585]
_B2 = [1.0, -2.0, 1.0]
_A2 = [1.0, -1.99004745483398, 0.99007225036621]


def _resp(b, a, f, fs=48000.0):
    z = np.exp(-1j * 2 * np.pi * f / fs)
    num = b[0] + b[1] * z + b[2] * z * z
    den = a[0] + a[1] * z + a[2] * z * z
    return np.abs(num / den) ** 2


def kweight_power(n, sr):
    f = np.fft.rfftfreq(n, 1.0 / sr)
    return _resp(_B1, _A1, f) * _resp(_B2, _A2, f)


def block_loudness(x, sr, block=0.4, hop=0.1):
    """x: (n, ch) float. Returns array of block loudness (LUFS) values."""
    n = x.shape[0]
    bl = int(block * sr)
    hp = int(hop * sr)
    if n < bl:
        pad = np.zeros((bl - n, x.shape[1]), dtype=x.dtype)
        x = np.concatenate([x, pad])
        n = bl
    w = kweight_power(bl, sr)
    out = []
    for s in range(0, n - bl + 1, hp):
        seg = x[s:s + bl]
        p = 0.0
        for c in range(seg.shape[1]):
            X = np.fft.rfft(seg[:, c])
            # Parseval: mean square = sum |X|^2 * weights / n^2 (rfft double counts except DC/Nyquist)
            P = np.abs(X) ** 2 * w
            P[1:-1] *= 2
            p += P.sum() / (bl * bl)
        out.append(-0.691 + 10 * np.log10(max(p, 1e-12)))
    return np.array(out)


def integrated(x, sr):
    L = block_loudness(x, sr)
    L = L[L > -70]
    if len(L) == 0:
        return -70.0
    rel = 10 * np.log10(np.mean(10 ** (L / 10))) - 10
    L2 = L[L > rel]
    return float(10 * np.log10(np.mean(10 ** (L2 / 10))))


def momentary_max(x, sr):
    L = block_loudness(x, sr, 0.4, 0.05)
    return float(L.max())


def short_term_max(x, sr):
    L = block_loudness(x, sr, 3.0, 0.5)
    return float(L.max())
