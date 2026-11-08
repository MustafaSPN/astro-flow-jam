"""Veri modeli: renkler, resim, tahta (bloklar, kapılar, delikler) ve bölüm.

Koordinatlar: resim ızgarası satır satır (y=0 üst); tahta x sağa, y yukarı (y=0 alt) — Unity BoardModel ile aynı.
"""
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple

# Palet: 34 piksel rengi (sRGB) ve gölge tonları.
# Kod: palet numarasının tek karakteri (0–9, a–x); bölüm düzeni bu karakterlerle yazılır.
_PF = [
    (0, 'Mavi', '#0073FF', '#001556'), (1, 'Koyu mavi', '#0E39CC', '#2C4388'), (2, 'Siyah', '#2F2F2F', '#111111'), (3, 'Yeşil', '#3FAC1D', '#0C7F06'),
    (4, 'Turuncu', '#FF6200', '#813100'), (5, 'Pembe', '#C04983', '#922D71'), (6, 'Mor', '#7829E1', '#8229A6'),
    (7, 'Kırmızı', '#A00D0D', '#551212'), (8, 'Turkuaz', '#2FAACF', '#007CA0'), (9, 'Sarı', '#D2A626', '#B25900'),
    (10, 'Beyaz', '#CCCCCC', '#333333'), (11, 'Kahverengi', '#613E23', '#3B0E00'), (12, 'Koyu yeşil', '#046000', '#062A00'),
    (13, 'Zümrüt', '#1D6553', '#004C39'), (14, 'Leylak', '#BF83E2', '#3A004E'), (15, 'Adaçayı', '#959D67', '#0C7F06'),
    (16, 'Somon', '#C37B7C', '#7F5051'), (17, 'Lavanta', '#878EC0', '#2C2F3F'), (18, 'Kiremit', '#8D404D', '#4A2229'),
    (19, 'Krem', '#C4AA70', '#5B524A'), (20, 'Tuğla', '#B2544A', '#551212'), (21, 'Gri', '#888795', '#333333'),
    (22, 'Koyu gri', '#474652', '#333333'), (23, 'Koyu mor', '#39205D', '#8229A6'), (24, 'Koyu pembe', '#AA1D62', '#922D71'),
    (25, 'Pudra pembe', '#CF8AB2', '#922D71'), (26, 'Pastel turuncu', '#D2914F', '#813100'), (27, 'Bordo', '#6F0F0F', '#551212'),
    (28, 'Zeytin', '#7B730A', '#4E4906'), (29, 'Soluk camgöbeği', '#5C8782', '#354E4B'), (30, 'Sütlü kahve', '#9F6A44', '#5F3420'),
    (31, 'Karamel', '#7B4827', '#352014'), (32, 'Neon mor', '#9328A7', '#6E177F'), (33, 'Köpekbalığı grisi', '#666476', '#333333'),
]
CODES = '0123456789abcdefghijklmnopqrstuvwx'


def _rgb(h): return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))


# kod -> (ad, rgb 0..1, palet numarası, gölge hex)
PALETTE: Dict[str, Tuple[str, Tuple[float, float, float]]] = {CODES[i]: (name, _rgb(hx)) for i, name, hx, _ in _PF}
PALETTE_ID = {CODES[i]: i for i, *_ in _PF}
SHADE = {CODES[i]: sh for i, _, _, sh in _PF}
CODE_OF_ID = {i: CODES[i] for i, *_ in _PF}
COLOR_NAMES = {k: v[0] for k, v in PALETTE.items()}
EMPTY = '.'


def lab_of(code: str):
    r, g, b = PALETTE[code][1]
    def lin(c): return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    R, G, B = lin(r), lin(g), lin(b)
    X = (0.4124 * R + 0.3576 * G + 0.1805 * B) / 0.95047
    Y = 0.2126 * R + 0.7152 * G + 0.0722 * B
    Z = (0.0193 * R + 0.1192 * G + 0.9505 * B) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return 116 * f(Y) - 16, 500 * (f(X) - f(Y)), 200 * (f(Y) - f(Z))


def delta_e(a: str, b: str) -> float:
    la, lb = lab_of(a), lab_of(b)
    return sum((x - y) ** 2 for x, y in zip(la, lb)) ** 0.5


def hex_of(code: str) -> str:
    r, g, b = PALETTE[code][1]
    return '#%02X%02X%02X' % (round(r * 255), round(g * 255), round(b * 255))


@dataclass
class Picture:
    """Piksel resim: rows[y][x] renk harfi ya da '.' (boş)."""
    rows: List[str]

    @property
    def W(self) -> int: return len(self.rows[0])

    @property
    def H(self) -> int: return len(self.rows)

    def at(self, x: int, y: int) -> str: return self.rows[y][x]

    def counts(self) -> Dict[str, int]:
        out: Dict[str, int] = {}
        for row in self.rows:
            for c in row:
                if c != EMPTY: out[c] = out.get(c, 0) + 1
        return out

    def layout(self) -> str: return ''.join(self.rows)


@dataclass
class Block:
    color: str
    rel: List[Tuple[int, int]]            # hücreler, sol alt köşeye göre
    pos: Tuple[int, int]
    way: Optional[str] = None             # None, 'H' (yalnız yatay), 'V' (yalnız dikey)
    ammo: List[int] = field(default_factory=list)   # hücre başına roket mermisi
    ice: int = 0                          # buz: tahtadan bu kadar blok çıkana kadar donuk
    key: int = 0                          # anahtar grubu: bu blok çıkınca aynı gruptaki kilitler açılır
    lock: int = 0                         # kilit grubu: anahtarı çıkana kadar donuk
    inner: str = ''                       # katmanlı blok: iç katmanın rengi ('' katmansız)
    inner_ammo: List[int] = field(default_factory=list)   # iç katmanın hücre başına mermisi

    def cells(self, pos=None) -> List[Tuple[int, int]]:
        x0, y0 = pos or self.pos
        return [(x0 + dx, y0 + dy) for dx, dy in self.rel]

    @property
    def size(self) -> Tuple[int, int]:
        return max(x for x, _ in self.rel) + 1, max(y for _, y in self.rel) + 1


    def copy(self) -> 'Block':
        return Block(self.color, list(self.rel), self.pos, self.way, list(self.ammo), self.ice, self.key, self.lock,
                     self.inner, list(self.inner_ammo))

    def inner_phase(self) -> 'Block':
        """Dış katman çıktıktan sonra tahtada kalan blok: iç renk ve mermi, katmansız (anahtar ve kilit korunur)."""
        return Block(self.inner, list(self.rel), self.pos, self.way, list(self.inner_ammo), 0, self.key, self.lock)


# Asteroit şekilleri (Unity AsteroidSpec, pivot (0,0) hücresi); dönüş saat yönünün tersine 90° adım.
ASTEROID_SHAPES = {'Single': [(0, 0)], 'Double': [(0, 0), (1, 0)], 'L': [(0, 0), (0, 1), (0, 2), (1, 0)],
                   'T': [(-1, 0), (0, 0), (1, 0), (0, -1)]}


def rotate_cell(c, rot):
    x, y = c
    for _ in range(rot % 4): x, y = -y, x
    return x, y


@dataclass
class Asteroid:
    shape: str
    pos: Tuple[int, int]                  # pivot hücresi
    rot: int = 0
    count: int = 0                        # 0 kalıcı; >0 tahtadan bu kadar blok (katman) çıkınca kırılır

    def cells(self, pos=None) -> List[Tuple[int, int]]:
        x0, y0 = pos or self.pos
        return [(x0 + dx, y0 + dy) for dx, dy in (rotate_cell(c, self.rot) for c in ASTEROID_SHAPES[self.shape])]


@dataclass
class Gate:
    color: str
    edge: str                             # 'top' | 'bottom' | 'left' | 'right'
    lo: int                               # kenar boyunca ilk hücre
    w: int = 2


@dataclass
class Board:
    W: int
    H: int
    blocks: List[Block]
    gates: List[Gate]
    holes: set = field(default_factory=set)
    asteroids: List[Asteroid] = field(default_factory=list)

    def copy(self) -> 'Board':
        return Board(self.W, self.H, [b.copy() for b in self.blocks], [Gate(g.color, g.edge, g.lo, g.w) for g in self.gates],
                     set(self.holes), [Asteroid(a.shape, a.pos, a.rot, a.count) for a in self.asteroids])

    # Çözücü durumu: {anahtar: konum}. Anahtar i bloğun ilk aşaması (katmanlıda dış katman), −(i+1) katmanlı bloğun iç
    # aşaması (dış katman çıktı, blok iç renkte tahtada).
    def block(self, key: int) -> Block:
        if key >= 0: return self.blocks[key]
        cache = self.__dict__.setdefault('_inner_cache', {})
        i = -key - 1
        b = cache.get(i)
        if b is None or b.rel != self.blocks[i].rel or b.color != self.blocks[i].inner:
            b = cache[i] = self.blocks[i].inner_phase()
        return b

    def start_state(self) -> dict:
        return {i: b.pos for i, b in enumerate(self.blocks)}

    def exit_state(self, state: dict, key: int) -> dict:
        """Bir aşama kapıdan çıktı: katmanlı bloğun dış katmanıysa blok aynı yerde iç aşamaya geçer."""
        s = dict(state); pos = s.pop(key)
        if key >= 0 and self.blocks[key].inner: s[-key - 1] = pos
        return s

    @property
    def phases(self) -> int:
        """Toplam çıkış (aşama) sayısı: katmanlı blok iki kez çıkar."""
        return sum(2 if b.inner else 1 for b in self.blocks)

    def exits_done(self, state: dict) -> int:
        return self.phases - sum(2 if k >= 0 and self.blocks[k].inner else 1 for k in state)

    def gone(self, state: dict, i: int) -> bool:
        """Blok tamamen çıktı mı (iki aşaması da)?"""
        return i not in state and -i - 1 not in state

    def blocked(self, state: dict) -> set:
        """Kalıcı dolu hücreler: delikler ve henüz kırılmamış asteroitler."""
        if not self.asteroids: return self.holes
        done = self.exits_done(state)
        out = set(self.holes)
        for a in self.asteroids:
            if a.count == 0 or a.count > done: out.update(a.cells())
        return out


# Blok şekilleri (sol alt köşeye göre hücreler). Bloklar oyunda dönmez; her yön ayrı şekildir.
SHAPES: Dict[str, List[Tuple[int, int]]] = {
    '1': [(0, 0)],
    '2H': [(0, 0), (1, 0)], '2V': [(0, 0), (0, 1)],
    'L3a': [(0, 0), (1, 0), (0, 1)], 'L3b': [(0, 0), (1, 0), (1, 1)],
    'L3c': [(0, 1), (1, 1), (0, 0)], 'L3d': [(1, 0), (1, 1), (0, 1)],
    'T4u': [(0, 0), (1, 0), (2, 0), (1, 1)], 'T4d': [(1, 0), (0, 1), (1, 1), (2, 1)],
    'T4l': [(1, 0), (1, 1), (1, 2), (0, 1)], 'T4r': [(0, 0), (0, 1), (0, 2), (1, 1)],
}
SHAPE_FAMILY = {k: ('1' if k == '1' else '2' if k.startswith('2') else 'L' if k.startswith('L') else 'T') for k in SHAPES}
