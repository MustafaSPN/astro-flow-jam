"""Kural tabanlı desen üretici (künyede resim tipi 'Desen' olan bölümler).

Her aile bir bölge haritası ve her hücre için bir "sınıf" üretir (halka numarası, şerit numarası, damadaki konum...).
Renkler sınıflara atanır; böylece desen her zaman düzenli ve simetrik kalır. Sınıf → renk eşlemesi aramayla seçilir:
hedef kilitli renk, başta açık renk, renk dengesi, en büyük renk payı ve bitişik farklı sınıfların farklı renk olması.
"""
import itertools
import math
import random
from typing import List, Tuple

from .model import EMPTY, Picture
from . import picture as pic

C_EMPTY = -1


def _grid(n, f):
    return [[f(x, y) for x in range(n)] for y in range(n)]


def rings(n, rng, k):
    """İç içe halkalar (kare/elmas/daire); kalınlıklar değişken; çift halkalar isteğe bağlı iki renge bölünür."""
    shape = rng.choice(['square', 'square', 'diamond', 'circle'])
    widths = []
    while sum(widths) < n // 2 + 1:
        widths.append(rng.choice([1, 2, 2, 2, 3]))
    split = rng.random() < 0.4
    split_mode = rng.choice(['lr', 'tb', 'quad'])
    c = (n - 1) / 2

    def cls(x, y):
        if shape == 'square': d = min(x, y, n - 1 - x, n - 1 - y)
        elif shape == 'diamond': d = max(0, int((n - 1 - abs(x - c) - abs(y - c)) / 2 + 0.25))
        else: d = max(0, int(c - math.hypot(x - c, y - c) * 0.72 + 0.5))
        r, acc = 0, 0
        while r < len(widths) - 1 and d >= acc + widths[r]:
            acc += widths[r]; r += 1
        if split and r % 2 == 0:
            part = {'lr': int(x >= n / 2), 'tb': int(y >= n / 2), 'quad': int(x >= n / 2) ^ int(y >= n / 2)}[split_mode]
            return r * 2 + part
        return r * 2
    return _grid(n, cls)


def stripes(n, rng, k):
    mode = rng.choice(['h', 'v', 'd'])
    width = rng.choice([1, 2, 2, 3])
    frame = rng.random() < 0.45
    mirror = rng.random() < 0.5                       # şeritler ortadan aynalı (simetrik)
    c = n - 1

    def cls(x, y):
        if frame and min(x, y, c - x, c - y) == 0: return 99
        t = {'h': y, 'v': x, 'd': (x + y) // 1}[mode]
        if mirror and mode != 'd': t = min(t, c - t)
        return t // width
    return _grid(n, cls)


def checker(n, rng, k):
    s = rng.choice([2, 3, 3, 4, 5])
    period = rng.randint(2, max(2, k))
    step = 1 if period == 2 else rng.choice([1, 2])
    off = (n % s) // 2
    return _grid(n, lambda x, y: (((x + off) // s) + ((y + off) // s) * step) % period)


def window(n, rng, k):
    """Çerçeve + pencere (referans 1. bölüm): kenar halkası yarılara ya da dörde bölünür, ortası boş ya da dolu."""
    th = rng.choice([3, 4]) if n >= 15 else 3
    parts = 2 if k <= 2 else rng.choice([2, 4])
    hollow = k <= 2 or rng.random() < 0.5

    def cls(x, y):
        d = min(x, y, n - 1 - x, n - 1 - y)
        if d < th:
            return int(x >= n / 2) if parts == 2 else int(x >= n / 2) + 2 * int(y >= n / 2)
        return C_EMPTY if hollow else 10 + (d - th) // 2
    return _grid(n, cls)


def spiral(n, rng, k):
    w = rng.choice([1, 2])
    lab = [[None] * n for _ in range(n)]
    x0, y0, x1, y1, seg = 0, 0, n - 1, n - 1, 0
    while x0 <= x1 and y0 <= y1:
        for x in range(x0, x1 + 1):
            for t in range(w):
                if y0 + t <= y1 and lab[y0 + t][x] is None: lab[y0 + t][x] = seg
        for y in range(y0, y1 + 1):
            for t in range(w):
                if x1 - t >= x0 and lab[y][x1 - t] is None: lab[y][x1 - t] = seg
        for x in range(x0, x1 + 1):
            for t in range(w):
                if y1 - t >= y0 and lab[y1 - t][x] is None: lab[y1 - t][x] = seg + 1
        for y in range(y0, y1 + 1):
            for t in range(w):
                if x0 + t <= x1 and lab[y][x0 + t] is None: lab[y][x0 + t] = seg + 1
        x0 += 2 * w; y0 += 2 * w; x1 -= 2 * w; y1 -= 2 * w; seg += 2
    return [[seg if v is None else v for v in row] for row in lab]


def diamonds(n, rng, k):
    s = rng.choice([3, 4, 5])
    c = (n - 1) / 2
    p, q = rng.choice([(2, 2), (2, 3), (3, 2), (3, 3), (2, 4)])
    def cls(x, y):
        u, v = int((x - c + y - c + 4 * n) // s), int((x - c - (y - c) + 4 * n) // s)
        return (u % p) + p * (v % q)
    return _grid(n, cls)


def bricks(n, rng, k):
    bw, bh = rng.choice([(4, 2), (3, 2), (5, 2), (4, 3)])
    period = max(1, k - 1)
    def cls(x, y):
        row = y // (bh + 1)
        if y % (bh + 1) == bh: return 0
        off = (bw + 1) // 2 if row % 2 else 0
        if (x + off) % (bw + 1) == bw: return 0
        return 1 + (row + (x + off) // (bw + 1)) % period
    return _grid(n, cls)


def stamps(n, rng, k):
    s = rng.choice([4, 5, 6])
    kind = rng.choice(['plus', 'dot', 'x', 'heart'])
    period = max(1, k - 1)
    off = (n % s) // 2
    heart = ['.X.X.', 'XXXXX', 'XXXXX', '.XXX.', '..X..']
    def cls(x, y):
        cx, cy = ((x + off) % s) - s // 2, ((y + off) % s) - s // 2
        r = s // 2
        if kind == 'plus': inside = (cx == 0 or cy == 0) and abs(cx) + abs(cy) <= r - (s % 2 == 0)
        elif kind == 'dot': inside = cx * cx + cy * cy <= max(1, (r - 1) ** 2)
        elif kind == 'x': inside = abs(cx) == abs(cy) and abs(cx) <= r - 1
        else:
            hx, hy = cx + 2, cy + 2
            inside = 0 <= hx < 5 and 0 <= hy < 5 and s >= 5 and heart[hy][hx] == 'X'
        if not inside: return 0
        return 1 + ((((x + off) // s) + 2 * ((y + off) // s)) % period)
    return _grid(n, cls)


FAMILIES = {'rings': rings, 'stripes': stripes, 'checker': checker, 'window': window, 'spiral': spiral,
            'diamonds': diamonds, 'bricks': bricks, 'stamps': stamps}
SUBJECT_FAMILY = {
    'iki renkli çerçeve, ortası boş': ['window'], 'iç içe kareler': ['rings'], 'iç içe dikdörtgenler': ['rings'],
    'sarmal': ['spiral'], 'dama': ['checker'], 'şeritler': ['stripes'], 'elmas deseni': ['diamonds'],
    'mandala': ['rings', 'stamps'], 'labirent': ['spiral'], 'tuğla duvar': ['bricks'],
    'kalp deseni': ['stamps'], 'yıldız deseni': ['stamps', 'diamonds'], 'zikzak': ['stripes'],
}


def _apply_silhouette(cls, n, rng):
    """Silüet: köşelerden yuvarlak bir maskeyle dışı boşaltılır."""
    c = (n - 1) / 2
    r = n * rng.uniform(0.47, 0.53)
    return [[C_EMPTY if math.hypot(x - c, y - c) > r else v for x, v in enumerate(row)] for y, row in enumerate(cls)]


def colorize(cls, colors: List[str], spec, rng, samples=400) -> Tuple[Picture, float]:
    classes = sorted({v for row in cls for v in row if v != C_EMPTY})
    m = len(colors)
    # bitişik sınıf çiftleri (farklı renk olmalı)
    n = len(cls)
    adj = set()
    for y in range(n):
        for x in range(n):
            a = cls[y][x]
            if a == C_EMPTY: continue
            for xx, yy in ((x + 1, y), (x, y + 1)):
                if xx < n and yy < n and cls[yy][xx] not in (C_EMPTY, a):
                    adj.add((min(a, cls[yy][xx]), max(a, cls[yy][xx])))

    def paint(mapping):
        return Picture([''.join(EMPTY if v == C_EMPTY else colors[mapping[v]] for v in row) for row in cls])

    def cost(mapping):
        p = paint(mapping)
        penalty, _, _ = pic.validate(p, spec)
        clash = sum(1 for a, b in adj if mapping[a] == mapping[b])
        missing = m - len(set(mapping.values()))
        return penalty + 2.0 * clash + 12 * missing, p

    best = None
    # aday eşlemeler: sınıf sırasına göre döngüsel renk dizileri + rastgele
    seqs = []
    for perm in itertools.islice(itertools.permutations(range(m)), 0, 120):
        seqs.append({c: perm[i % m] for i, c in enumerate(classes)})
    for _ in range(samples):
        seqs.append({c: rng.randrange(m) for c in classes})
    for mp in seqs:
        c, p = cost(mp)
        if best is None or c < best[0]: best = (c, p, mp)
    # yerel iyileştirme
    c0, p0, mp = best
    for _ in range(150):
        cand = dict(mp); cand[rng.choice(classes)] = rng.randrange(m)
        c, p = cost(cand)
        if c < c0: c0, p0, mp = c, p, cand
    return p0, c0


def generate(spec, tries=60, seed=None) -> Tuple[Picture, float, List[str]]:
    rng = random.Random(seed if seed is not None else spec.seed)
    n = spec.picture_grid
    k = len(spec.colors)
    fams = SUBJECT_FAMILY.get(spec.picture['subject'], list(FAMILIES))
    best = None
    for _ in range(tries):
        cls = FAMILIES[rng.choice(fams)](n, rng, k)
        if len({v for row in cls for v in row if v != C_EMPTY}) < k:   # tüm renkleri taşıyacak kadar sınıf yok
            continue
        if spec.picture['silhouette']: cls = _apply_silhouette(cls, n, rng)
        p, c = colorize(cls, spec.colors, spec, rng, samples=150)
        sym = pic.metrics(p)['symmetry']
        score = c - 1.0 * sym
        if best is None or score < best[0]: best = (score, p)
    if best is None:
        return None, 99.0, ['bu desen ailesi bu renk sayısını taşıyamıyor']
    penalty, issues, _ = pic.validate(best[1], spec)
    return best[1], penalty, issues
