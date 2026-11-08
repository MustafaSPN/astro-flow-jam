"""Resme uygun tahta üretici.

1. Roket envanteri: resmin renk sayılarından hücre sayısı; her renkte toplam mermi = piksel sayısı (referans kuralı).
2. Blok parçalama: hücreler zorluğa ve açılmış şekillere göre 1'li / 2'li / L / T bloklara bölünür.
3. Kapılar: her renge bir kapı (referans kuralı), kenarlara dağıtılır.
4. Yerleştirme: rastgele kurulum + tavlama (simulated annealing); hedef: çözülebilirlik, katman/serbest blok/yer değiştirme
   aralıkları, resimle sıra uyumu ve estetik (yaslanma, sıkılık, simetri, ortalama).
5. Seçim: en iyi adaylar oyun simülatöründe insana benzer botla oynatılır; kazanma oranı, slot baskısı ve süre künye
   hedeflerine en yakın olan seçilir.
"""
import itertools
import math
import random
from typing import Dict, List, Optional, Tuple

from . import board_solver as bs
from . import picture as pic
from .model import ASTEROID_SHAPES, EMPTY, SHAPES, SHAPE_FAMILY, Asteroid, Block, Board, Gate, Picture
from .sim import evaluate, human_eval
from .targets import HUMAN_WIN, NOVICE_TIME_FAIL_MAX

EDGES = ['top', 'bottom', 'left', 'right']


# ---------------------------------------------------------------- envanter
def inventory(spec, picture: Picture, rng, boost: int = 0) -> List[Tuple[str, List[str]]]:
    """[(renk, [şekil, şekil, ...])]: her rengin blokları. Hücre sayısı mermi/hücre hedefinden; tahta doluluğuna sığar."""
    counts = picture.counts()
    area = spec.board['W'] * spec.board['H'] - spec.board.get('holes', 0)
    fill_hi = spec.board['fill'][1]
    total_px = sum(counts.values())
    # Mermi/hücre tahta doluluğundan çıkar: hedef doluluğun ortası kadar hücre (küçük resimde daha az mermili, çok roket).
    # Zorlaştırma (boost): biraz daha dolu tahta.
    fill_lo, fill_hi = spec.board['fill']
    want_cells = max(len(counts), area * min(0.9, (fill_lo + fill_hi) / 2 * (1 + 0.04 * boost)))
    # Katmanlı blok iki birimi tek bloğa katar, tahtadaki hücre azalır: doluluk tutsun diye o kadar hücre eklenir.
    want_cells += 2.2 * max(spec.board.get('layer', 0), round(spec.board.get('layer_share', 0) * want_cells / 2.2))
    apc = max(4, round(total_px / want_cells))
    cells = {c: max(1, round(v / apc)) for c, v in counts.items()}
    while sum(cells.values()) > area * min(0.9, fill_hi + 0.04) and apc < 80:      # tahtaya sığmıyorsa mermi/hücre artar
        apc += 2
        cells = {c: max(1, round(v / apc)) for c, v in counts.items()}
    fams = spec.board['shapes']
    out = []
    for color, k in sorted(cells.items(), key=lambda kv: -kv[1]):
        shapes, left = [], k
        while left > 0:
            opts = [('1', 1)]
            if '2' in fams and left >= 2: opts += [('2', 2)] * 2
            if 'L' in fams and left >= 3: opts += [('L', 3)]
            if 'T' in fams and left >= 4: opts += [('T', 4)]
            fam, size = rng.choice(opts)
            shape = {'1': '1', '2': rng.choice(['2H', '2V']), 'L': rng.choice(['L3a', 'L3b', 'L3c', 'L3d']),
                     'T': rng.choice(['T4u', 'T4d', 'T4l', 'T4r'])}[fam]
            shapes.append(shape); left -= size
        out.append((color, shapes))
    return out


def split_ammo(total: int, cells: List[int]) -> List[List[int]]:
    """Rengin toplam mermisini bloklara ve hücrelerine böl (hücre başı eşit, artık ilk hücrelere)."""
    n = sum(cells)
    base, extra = divmod(total, n)
    flat = [base + (1 if i < extra else 0) for i in range(n)]
    out, i = [], 0
    for c in cells:
        out.append(flat[i:i + c]); i += c
    return out


# ---------------------------------------------------------------- kurulum
# referans: ayna-x %41, yok %50, 180 %6; küçük tahtamızda dağınık görünmesin diye simetri ağır basar, simetrisizde tek parça.
HOLE_SYMMETRY = [('ayna', 0.55), ('yok', 0.3), ('180', 0.15)]
HOLE_ZONE = [('kenar', 0.5), ('orta', 0.35), ('köşe', 0.15)]           # referans: delik hücrelerinin bölgesi


def _pick(rng, table):
    r = rng.random() * sum(w for _, w in table)
    for k, w in table:
        r -= w
        if r <= 0: return k
    return table[-1][0]


def _hole_piece(W: int, H: int, zone: str, rng) -> set:
    """Tek delik parçası: köşe kesiği (1–3 hücre), kenar çentiği (1–2 hücre, kenar boyunca), ortada 1x1–2x2 sütun."""
    if zone == 'köşe':
        x = rng.choice([0, W - 1]); y = rng.choice([0, H - 1])
        sx = 1 if x == 0 else -1; sy = 1 if y == 0 else -1
        cells = {(x, y)}
        r = rng.random()
        if r < 0.35: cells.add((x + sx, y))
        elif r < 0.7: cells.add((x, y + sy))
        elif r < 0.85: cells |= {(x + sx, y), (x, y + sy)}
        return cells
    if zone == 'kenar':
        edge = rng.choice(['left', 'right', 'bottom', 'top'])
        L = H if edge in ('left', 'right') else W
        n = rng.choice([1, 2, 2, 3]) if L >= 7 else rng.choice([1, 2]) if L >= 5 else 1
        t = rng.randrange(1, L - n)                          # köşeye değmesin (köşe ayrı bölge)
        line = [(0 if edge == 'left' else W - 1, t + i) if edge in ('left', 'right') else (t + i, 0 if edge == 'bottom' else H - 1)
                for i in range(n)]
        return set(line)
    w = rng.choice([1, 1, 2]) if W >= 6 else 1; h = rng.choice([1, 2]) if H >= 6 else 1
    x = rng.randrange(1, W - w); y = rng.randrange(1, H - h)
    return {(x + i, y + j) for i in range(w) for j in range(h)}


def holes_ok(W: int, H: int, holes: set) -> bool:
    """Kalan hücreler tek parça, her hücrenin en az iki açık komşusu var (çıkmaz tek hücreli cep yok), her kenarda kapı
    konabilecek en az iki bitişik açık hücre."""
    open_ = {(x, y) for x in range(W) for y in range(H)} - holes
    if not open_: return False
    start = next(iter(open_)); seen = {start}; stack = [start]
    while stack:
        x, y = stack.pop()
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + d[0], y + d[1])
            if q in open_ and q not in seen: seen.add(q); stack.append(q)
    if len(seen) != len(open_): return False
    if any(sum((x + dx, y + dy) in open_ for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) < 2 for x, y in open_): return False
    for line in ([(x, 0) for x in range(W)], [(x, H - 1) for x in range(W)], [(0, y) for y in range(H)], [(W - 1, y) for y in range(H)]):
        if not any(a not in holes and b not in holes for a, b in zip(line, line[1:])): return False
    return True


def hole_template(W: int, H: int, count: int, rng, intro: bool = False) -> set:
    """Delik şablonu (referanstaki tahta şekilleri): simetri ayna/yok/180, parça bölgesi kenar/orta/köşe,
    parça 1–4 hücre, en çok 2 simetrik parça çifti. Tanıtımda yalnız iki alt ya da üst köşe (referans 10. bölüm). Sayı hedefe en fazla bir hücre yaklaşır."""
    if count <= 0: return set()
    if intro:
        y = rng.choice([0, H - 1])
        return {(0, y), (W - 1, y)}
    for _ in range(60):
        sym = _pick(rng, HOLE_SYMMETRY)
        holes = set()
        pieces = 0
        for _ in range(20):
            if len(holes) >= count - (1 if count > 4 else 0) or pieces >= (1 if sym == 'yok' else 2): break
            piece = _hole_piece(W, H, _pick(rng, HOLE_ZONE), rng)
            if sym == 'ayna': piece |= {(W - 1 - x, y) for x, y in piece}
            elif sym == '180': piece |= {(W - 1 - x, H - 1 - y) for x, y in piece}
            if len(holes | piece) > count + 1 or not holes_ok(W, H, holes | piece): continue
            holes |= piece; pieces += 1
        if abs(len(holes) - count) <= (1 if count > 4 else 0) and holes_ok(W, H, holes): return holes
    return set()


def assign_mechanics(spec, blocks: List[Block], gates: List[Gate], rng) -> None:
    """Tek eksen, buz ve anahtar–kilit bloklara atanır (referans kullanımı):
    - Tek eksen: çoğunlukla 1–2 hücreli bloklar (referans %88); ekseni kendi renginin kapısının kenarına bakar (referans %87,
      yoksa blok hiç çıkamazdı). Tanıtım dalgasında blokların oneway_share kadarı.
    - Buz: çoğunlukla küçük bloklar; sayaç blok sayısının ice_ratio katı (referans: başta ~%30, sonra ~%50), birden çok buzlu
      blokta sayaçlar farklı (sırayla çözülür).
    - Anahtar–kilit: bölümde tek kilit (referansta hep tek); kilit 2+ hücreli blokta (referans %95), anahtar aynı renkte başka blok."""
    bt = spec.board
    edge_of = {g.color: g.edge for g in gates}
    order = list(range(len(blocks))); rng.shuffle(order)
    used = set()
    def take(pred=lambda i: True, prefer=lambda i: 0):
        for i in sorted(order, key=prefer):                 # sorted kararlı: eşit tercihlerde karışık sıra korunur
            if i not in used and not blocks[i].inner and pred(i):       # katmanlı bloğa başka mekanik konmaz (referans %1)
                used.add(i); return i
        return None
    small = lambda i: 0 if len(blocks[i].rel) <= 2 else 1 + (rng.random() < 0.6)
    n_way = max(bt.get('oneway', 0), round(bt.get('oneway_share', 0) * len(blocks)))
    for _ in range(n_way):
        i = take(prefer=small)
        if i is None: break
        blocks[i].way = 'V' if edge_of.get(blocks[i].color) in ('top', 'bottom') else 'H'
    lo, hi = bt.get('ice_ratio', (0.3, 0.45))
    top = max(1, len(blocks) - 2)
    values = sorted({min(top, max(2 if len(blocks) > 3 else 1, round(rng.uniform(lo, hi) * len(blocks)) + k))
                     for k in range(bt.get('ice', 0))}, reverse=True)
    for v in values:
        i = take(prefer=lambda i: 0 if len(blocks[i].rel) <= 2 else rng.random() < 0.7)
        if i is None: break
        blocks[i].ice = v
    # Kilit ve anahtar aynı renkte iki farklı blokta: kilitli bloğu yalnız kendi rengindeki anahtarlı blok açar (oyuncu blok
    # renginden hangi anahtarın hangi kilidi açtığını görür). O renkte ikinci blok yoksa kilit konmaz.
    for g in range(1, bt.get('keylock', 0) + 1):
        l = take(lambda i: sum(1 for j, b in enumerate(blocks) if j != i and j not in used and b.color == blocks[i].color) > 0,
                 prefer=lambda i: -min(len(blocks[i].rel), 4))
        if l is None: break
        k = take(lambda i: blocks[i].color == blocks[l].color)
        if k is None: used.discard(l); break
        blocks[k].key = g; blocks[l].lock = g


def lock_in_corner(board: Board) -> int:
    """Köşeye sıkışmış kilitli blok sayısı (referansta kilit %62 ortada, %32 kenarda, %5 köşede)."""
    n = 0
    for b in board.blocks:
        if not b.lock: continue
        cs = b.cells(); x = sum(c[0] for c in cs) / len(cs); y = sum(c[1] for c in cs) / len(cs)
        n += (x < 1 or x > board.W - 2) and (y < 1 or y > board.H - 2)
    return n


def keylock_ok(blocks: List[Block]) -> bool:
    """Her kilidin aynı renkte ve aynı grupta bir anahtarı var mı?"""
    return all(any(k.key == b.lock and k.color == b.color for k in blocks) for b in blocks if b.lock)


def start_colors(picture: Picture) -> set:
    """Resimde başta kenardan görünen renkler (önbellekli)."""
    cached = picture.__dict__.get('_start_colors')
    if cached is None:
        cached = picture.__dict__['_start_colors'] = set(pic.metrics(picture)['start_colors'])
    return cached


def color_rank(picture: Picture) -> dict:
    """Renklerin resimde açılma sırası: 0 başta kenardan görünenler; onların pikselleri silinince görünenler 1, ... (önbellekli)."""
    cached = picture.__dict__.get('_color_rank')
    if cached is not None: return cached
    rows = [list(r) for r in picture.rows]
    rank, r = {}, 0
    while True:
        cur = Picture([''.join(x) for x in rows])
        front = {cur.rows[y][x] for x, y in pic.front_cells(cur)} - set(rank)
        if not front: break
        for c in front: rank[c] = r
        rows = [[EMPTY if ch in rank else ch for ch in row] for row in rows]
        r += 1
    for c in picture.counts(): rank.setdefault(c, r)
    picture.__dict__['_color_rank'] = rank
    return rank


def order_inversions(board: Board, layers: List[List[int]], rank: dict) -> float:
    """Tahtanın boşalma sırası ile resmin açılma sırası ne kadar ters: daha önce serbest kalan blok resimde daha geç açılan
    renkteyse ters çift. Ters çiftlerin, katmanı ve rengi farklı tüm çiftlere oranı (0 tam uyumlu, 1 tam ters)."""
    items = [(k, rank.get(board.block(i).color, 0)) for k, layer in enumerate(layers) for i in layer]
    inv = tot = 0
    for a in range(len(items)):
        for b in range(a + 1, len(items)):
            (la, ra), (lb, rb) = items[a], items[b]
            if la == lb or ra == rb: continue
            tot += 1
            if (la - lb) * (ra - rb) < 0: inv += 1
    return inv / tot if tot else 0.0


def layer_units(units: List[List], count: int, exposed: set, rng) -> List[List]:
    """Katmanlı bloklar: aynı hücre sayısındaki iki farklı renkli bloktan biri diğerinin iç katmanı olur (iç blok envanterden
    düşer). Dış renk resimde başta açık olan, iç renk sonra açılan renk
    tercih edilir; iç rengin kendi ayrı bloğu da kalır (referans %94); bloklar çoğunlukla 1–2 hücreli (referans %68)."""
    for _ in range(count):
        outers = [u for u in units if not u[2]]
        rng.shuffle(outers)
        outers.sort(key=lambda u: (u[0] not in exposed, len(SHAPES[u[1]]) > 2))
        done = False
        for o in outers:
            n = len(SHAPES[o[1]])
            inners = [u for u in units if u is not o and not u[2] and u[0] != o[0] and len(SHAPES[u[1]]) == n
                      and sum(1 for v in units if v[0] == u[0]) >= 2]
            if not inners: continue
            inners.sort(key=lambda u: (u[0] in exposed, rng.random()))
            units.remove(inners[0])
            o[2] = inners[0][0]
            done = True
            break
        if not done: break
    return units


def interior_holes_to_rocks(W: int, H: int, holes: set):
    """Tahtanın ortasındaki delikler (kenara değmeyen 1x1, 1x2, 2x2 parçalar) kalıcı asteroide dönüşür: ortada boşluk
    yerine kaya daha iyi görünüyor (kullanıcı tercihi). Köşe kesikleri ve kenar çentikleri delik kalır (tahta şekli).
    Oynanışı aynıdır: ikisi de geçilmez hücre. Parça bağlı bileşenlere ayrılır; ikili parçalar Double, gerisi Single."""
    inner = {(x, y) for x, y in holes if 1 <= x < W - 1 and 1 <= y < H - 1}
    # 2026-10-28: yan yana çok sayıda kaya şık durmuyor: ortadaki 3+ hücrelik delik grupları delik kalır (delikli tahta),
    # yalnız 1–2 hücrelik küçük delikler kayaya döner.
    keep, seen = set(), set()
    for c in inner:
        if c in seen: continue
        group, stack = set(), [c]
        while stack:
            p = stack.pop()
            if p in group or p not in inner: continue
            group.add(p); stack += [(p[0] + 1, p[1]), (p[0] - 1, p[1]), (p[0], p[1] + 1), (p[0], p[1] - 1)]
        seen |= group
        if len(group) >= 3: keep |= group
    inner -= keep
    rocks: List[Asteroid] = []
    left = set(inner)
    while left:
        x, y = min(left, key=lambda c: (c[1], c[0]))
        if (x + 1, y) in left:
            rocks.append(Asteroid('Double', (x, y), 0, 0)); left -= {(x, y), (x + 1, y)}
        elif (x, y + 1) in left:
            rocks.append(Asteroid('Double', (x, y), 1, 0)); left -= {(x, y), (x, y + 1)}
        else:
            rocks.append(Asteroid('Single', (x, y), 0, 0)); left.discard((x, y))
    return holes - inner, rocks


def place_asteroids(spec, W: int, H: int, holes: set, rng) -> List[Asteroid]:
    """Asteroit engelleri tahtayı bölmeden (holes_ok). Kalıcılar kenara değmez (kapı yeri ve kenar yolu kalsın) ve
    çoğunlukla tek hücre (referans sabit engel: 1 hücre %84, bölümde 1–2); sayaçlılar 1–2 hücre, L/T yalnız büyük tahtada."""
    bt = spec.board
    rocks: List[Asteroid] = []
    want = [0] * bt.get('asteroid', 0) + [1] * bt.get('asteroid_count', 0)
    big = W * H >= 42
    occ = set(holes)
    for counted in want:
        shapes = ['Single'] * 6 + ['Double'] * 3 + (['L', 'T'] if big else [])
        for _ in range(80):
            a = Asteroid(rng.choice(shapes), (rng.randrange(0, W), rng.randrange(0, H)), rng.randrange(4), 1 if counted else 0)
            cs = a.cells()
            if not counted and any(not (1 <= x < W - 1 and 1 <= y < H - 1) for x, y in cs): continue
            if any(not (0 <= x < W and 0 <= y < H) or (x, y) in occ for x, y in cs): continue
            # Kaya kümesi olmasın: yeni asteroit başka kayaya ya da deliğe yan yana değmez.
            if any((x + dx, y + dy) in occ for x, y in cs for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): continue
            if not holes_ok(W, H, occ | set(cs)): continue
            occ.update(cs); rocks.append(a); break
    return rocks


def set_asteroid_counts(spec, board: Board, rng) -> None:
    """Sayaçlı asteroit: sayaç toplam çıkış (aşama) sayısının asteroid_ratio katı (referans sandık: blok sayısının ~%41'i);
    birden çok asteroitte sayaçlar farklı."""
    lo, hi = spec.board.get('asteroid_ratio', (0.3, 0.5))
    top = max(2, board.phases - 2)
    used = set()
    for a in board.asteroids:
        if not a.count: continue
        v = min(top, max(2, round(rng.uniform(lo, hi) * board.phases)))
        while v in used and v < top: v += 1
        used.add(v); a.count = v


def random_board(spec, picture: Picture, inv, rng) -> Optional[Board]:
    W, H = spec.board['W'], spec.board['H']
    counts = picture.counts()
    holes = hole_template(W, H, spec.board.get('holes', 0), rng, spec.board.get('holes_intro', False))
    holes, hole_rocks = interior_holes_to_rocks(W, H, holes)
    rocks = hole_rocks + place_asteroids(spec, W, H, holes | {c for a in hole_rocks for c in a.cells()}, rng)
    rock_cells = {c for a in rocks for c in a.cells()}
    # Birim: [renk, şekil, iç renk]; katmanlı bloklar birleşir, mermi her rengin tüm aşamalarına bölünür.
    units = [[color, shape, ''] for color, shapes in inv for shape in shapes]
    if spec.board.get('layer', 0) or spec.board.get('layer_share', 0):
        n = max(spec.board.get('layer', 0), round(spec.board.get('layer_share', 0) * len(units)))
        units = layer_units(units, n, start_colors(picture), rng)
    phases = {}
    for k, u in enumerate(units):
        phases.setdefault(u[0], []).append((k, 'outer'))
        if u[2]: phases.setdefault(u[2], []).append((k, 'inner'))
    ammo_of = {}
    for color, lst in phases.items():
        split = split_ammo(counts[color], [len(SHAPES[units[k][1]]) for k, _ in lst])
        for (k, part), am in zip(lst, split): ammo_of[(k, part)] = am
    blocks: List[Block] = []
    occ = set(holes) | rock_cells
    for k, (color, shape, inner) in enumerate(units):
        rel = SHAPES[shape]
        for _ in range(300):
            bw = max(x for x, _ in rel) + 1; bh = max(y for _, y in rel) + 1
            pos = (rng.randrange(0, W - bw + 1), rng.randrange(0, H - bh + 1))
            cs = [(pos[0] + dx, pos[1] + dy) for dx, dy in rel]
            if not any(c in occ for c in cs):
                occ.update(cs)
                blocks.append(Block(color, list(rel), pos, ammo=ammo_of[(k, 'outer')], inner=inner,
                                    inner_ammo=ammo_of.get((k, 'inner'), [])))
                break
        else:
            return None
    colors = list(dict.fromkeys([c for c, _ in inv] + [u[2] for u in units if u[2]]))
    gates = place_gates(spec, W, H, colors, blocks, rng, holes | {c for a in rocks if a.count == 0 for c in a.cells()})
    if not gates: return None
    assign_mechanics(spec, blocks, gates, rng)
    board = Board(W, H, blocks, gates, holes, rocks)
    set_asteroid_counts(spec, board, rng)
    return board


def gate_corners(edge, lo, w, W, H):
    """Kapının kapladığı köşe hücreleri (köşede iki kenarın kapısı iç içe görünür)."""
    L = W if edge in ('top', 'bottom') else H
    out = set()
    for c in (0, L - 1):
        if lo <= c < lo + w:
            out.add({'bottom': (c, 0), 'top': (c, H - 1), 'left': (0, c), 'right': (W - 1, c)}[edge])
    return out


def gate_cells(edge, lo, w, W, H):
    """Kapının önündeki (kenar sırasındaki) tahta hücreleri."""
    return [{'bottom': (c, 0), 'top': (c, H - 1), 'left': (0, c), 'right': (W - 1, c)}[edge] for c in range(lo, lo + w)]


def gate_fits(gates, g, edge, lo, W, H, holes=()) -> bool:
    """Aynı kenardaki kapılarla arada en az bir hücre boşluk var mı, başka kenardaki bir kapıyla aynı köşeyi paylaşıyor mu,
    önünde delik var mı?"""
    if any(c in holes for c in gate_cells(edge, lo, g.w, W, H)): return False
    mine = gate_corners(edge, lo, g.w, W, H)
    for o in gates:
        if o is g: continue
        if o.edge == edge:
            if not (lo + g.w + 1 <= o.lo or o.lo + o.w + 1 <= lo): return False
        elif mine & gate_corners(o.edge, o.lo, o.w, W, H):
            return False
    return True


def gate_span(blocks, color, edge) -> int:
    """O kenardan çıkabilmek için kapı en az kaç hücre olmalı: bloklar dönmez, her bloğun kenar boyundaki uzunluğu sığmalı
    (iç katmanlar dahil)."""
    axis = 0 if edge in ('top', 'bottom') else 1
    return max(b.size[axis] for b in blocks if b.color == color or b.inner == color)


def place_gates(spec, W, H, colors, blocks, rng, holes=()) -> Optional[List[Gate]]:
    edges = EDGES[:]; rng.shuffle(edges)
    gates = []
    for k, color in enumerate(colors):
        # Önce bütün blokların sığdığı dar kapılı kenarlar; rengin yatay ve dikey uzun blokları varsa kapı genişler.
        order = sorted(range(4), key=lambda e: gate_span(blocks, color, edges[(k + e) % 4]) > 2)
        narrow = 1 in spec.board['gate_widths'] and rng.random() < 0.3
        for attempt in range(40):
            edge = edges[(k + order[attempt % 4]) % 4]
            span = gate_span(blocks, color, edge)
            w = 1 if narrow and span == 1 else max(2, span)
            length = W if edge in ('top', 'bottom') else H
            if length < w: continue
            lo = rng.randrange(0, length - w + 1)
            g = Gate(color, edge, lo, w)
            if not gate_fits(gates, g, edge, lo, W, H, holes): continue
            gates.append(g); break
        else:
            return None
    return gates


# ---------------------------------------------------------------- değerlendirme
def aesthetics(board: Board) -> dict:
    W, H = board.W, board.H
    cmap = {c: (i, b.color) for i, b in enumerate(board.blocks) for c in b.cells()}
    touching = sum(1 for i, b in enumerate(board.blocks)
                   if any(cmap.get((x + dx, y + dy), (i,))[0] != i for x, y in b.cells()
                          for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (x + dx, y + dy) in cmap)) / len(board.blocks)
    xs = [c[0] for c in cmap]; ys = [c[1] for c in cmap]
    compact = len(cmap) / ((max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1))
    sym = sum(1 for (x, y), (_, col) in cmap.items() if cmap.get((W - 1 - x, y), (None, None))[1] == col) / len(cmap)
    mid = 1 - min(1, abs((min(xs) + max(xs)) / 2 - (W - 1) / 2) / (W / 2) + abs((min(ys) + max(ys)) / 2 - (H - 1) / 2) / (H / 2))
    same = tot = 0
    for (x, y), (_, col) in cmap.items():
        for dx, dy in ((1, 0), (0, 1)):
            o = cmap.get((x + dx, y + dy))
            if o: tot += 1; same += o[1] == col
    return dict(touching=touching, compact=compact, symmetry=sym, centered=mid, cluster=same / tot if tot else 0,
                fill=len(cmap) / (W * H - len(board.holes) - sum(len(a.cells()) for a in board.asteroids)))


def in_range(v, lo_hi):
    lo, hi = lo_hi
    return 0 if lo <= v <= hi else (lo - v if v < lo else v - hi)


def cheap_score(spec, picture_m, board: Board, boost: int = 0) -> Tuple[float, dict]:
    """Simülasyonsuz puan (düşük daha iyi): çözülebilirlik, tahta hedefleri, resimle sıra uyumu, estetik."""
    W, H = board.W, board.H
    if not keylock_ok(board.blocks):
        return 100.0, {}
    # Geçersiz tahtalar 50'nin üstünde ama kademeli puan alır: tavlama kapıdaki blok sayısını ve takılan bölümü adım adım
    # azaltabilsin (sabit ceza yön göstermiyordu, adayların çoğu geçerli tahtaya ulaşamıyordu). Kapıdaki blok, çözülemeyen
    # tahtadan (60–90) kötü sayılır: yoksa tavlama tek blok kapıda kalan tahtada takılır, çözülebilirliğe hiç bakılmaz.
    at_gate = sum(1 for b in board.blocks if any(bs.can_exit(b, b.pos, g, W, H) for g in board.gates))
    if at_gate:
        return 90.0 + 8 * at_gate, {}
    stuck = bs.stuck_phases(board)              # kapısına hiç ulaşamayan blok varsa aramaya gerek yok
    if stuck:
        return 60.0 + 30 * min(1.0, stuck / max(1, board.phases)) + 5, {}
    res = bs.peel(board, cap=3000)              # tavlamada kısa arama; seçilen tahta simülasyonda tam oynanır
    if not res['solved']:
        cleared = sum(len(layer) for layer in res['layers'])
        return 60.0 + 30 * (1 - cleared / max(1, board.phases)), {}
    bt = spec.board
    layers = len(res['layers'])
    shift = max(0, boost)
    lay_r = (bt['layers'][0] + shift, bt['layers'][1] + 2 * shift)
    shf_r = (bt['shuffles'][0] + (shift if 'reposition' in spec.mechanics else 0), bt['shuffles'][1] + shift)
    ff_r = (max(0, bt['first_free'][0] - shift), max(0, bt['first_free'][1] - shift)) if boost > 0 else \
        (bt['first_free'][0] - boost, bt['first_free'][1] - boost)
    score = 3 * in_range(res['first_free'], ff_r) + 1.5 * in_range(layers, lay_r) + 2 * in_range(res['shuffles'], shf_r)
    # Sıra uyumu: resimde açık renklerin blokları erken katmanlarda, kilitli renklerin blokları geç katmanlarda.
    layer_of = {i: k for k, layer in enumerate(res['layers']) for i in layer}
    exposed = set(picture_m['start_colors'])
    first_colors = {board.block(i).color for i in res['layers'][0]} if res['layers'] else set()
    conflict = sum(1 for i in res['layers'][0] if board.block(i).color not in exposed) / max(1, len(res['layers'][0])) \
        if res['layers'] else 1
    early_exposed = sum(1 for c in exposed if c in first_colors) / max(1, len(exposed))
    want_conflict = min(0.9, max(0.0, {'Kolay': 0.0, 'Orta': 0.15, 'Zor': 0.3, 'Çok Zor': 0.45}[spec.tier] + 0.15 * boost))
    score += 3 * abs(conflict - want_conflict) + (2 * (1 - early_exposed) if spec.tier == 'Kolay' else 0)
    # Sıra uyumu bütün tahtada: kolayda boşalma sırası resmin açılma sırasıyla uyumlu, zorlaştıkça kasıtlı ters düşmeler.
    inversions = order_inversions(board, res['layers'], picture_m['rank'])
    want_inv = min(0.6, max(0.0, {'Kolay': 0.0, 'Orta': 0.1, 'Zor': 0.2, 'Çok Zor': 0.3}[spec.tier] + 0.08 * boost))
    score += 4 * abs(inversions - want_inv)
    a = aesthetics(board)
    score += 1.5 * (1 - a['touching']) + 1.0 * (1 - a['compact']) + 1.2 * (1 - a['symmetry']) + 0.8 * (1 - a['centered']) \
        + 0.3 * (1 - a['cluster']) + 1.0 * lock_in_corner(board) + 2 * in_range(a['fill'], (bt['fill'][0] + 0.05 * shift, min(0.9, bt['fill'][1] + 0.08 * shift)))
    return score, dict(peel=res, layers=layers, conflict=conflict, inversions=inversions, **a)


def mutate(board: Board, rng) -> Board:
    b = board.copy()
    r = rng.random()
    W, H = b.W, b.H
    occ = {c: i for i, bl in enumerate(b.blocks) for c in bl.cells()}
    rocks = {c for a in b.asteroids for c in a.cells()}
    if b.asteroids and r < 0.08:                             # bir asteroidi taşı
        k = rng.randrange(len(b.asteroids)); a = b.asteroids[k]
        others = {c for j, o in enumerate(b.asteroids) if j != k for c in o.cells()}
        for _ in range(30):
            pos = (rng.randrange(0, W), rng.randrange(0, H))
            cs = a.cells(pos)
            if any(not (0 <= x < W and 0 <= y < H) or (x, y) in occ or (x, y) in b.holes or (x, y) in others for x, y in cs): continue
            if a.count == 0 and any(not (1 <= x < W - 1 and 1 <= y < H - 1) for x, y in cs): continue
            if not holes_ok(W, H, b.holes | others | set(cs)): continue
            a.pos = pos; break
        return b
    if r < 0.6:                                              # bir bloğu başka boş yere taşı
        i = rng.randrange(len(b.blocks)); bl = b.blocks[i]
        bw, bh = bl.size
        for _ in range(30):
            pos = (rng.randrange(0, W - bw + 1), rng.randrange(0, H - bh + 1))
            if all(occ.get(c, i) == i and c not in b.holes and c not in rocks for c in bl.cells(pos)):
                bl.pos = pos; break
    elif r < 0.85:                                           # iki bloğun yerini değiştir (aynı boyuttaysa)
        i, j = rng.sample(range(len(b.blocks)), 2)
        if b.blocks[i].rel == b.blocks[j].rel:
            b.blocks[i].pos, b.blocks[j].pos = b.blocks[j].pos, b.blocks[i].pos
    else:                                                    # bir kapıyı taşı
        g = rng.choice(b.gates)
        edge = rng.choice(EDGES) if rng.random() < 0.3 else g.edge
        length = W if edge in ('top', 'bottom') else H
        if length >= g.w and gate_span(b.blocks, g.color, edge) <= g.w:
            lo = rng.randrange(0, length - g.w + 1)
            if gate_fits(b.gates, g, edge, lo, W, H, b.holes | {c for a in b.asteroids if a.count == 0 for c in a.cells()}):
                g.edge, g.lo = edge, lo
                for bl in b.blocks:                                  # tek eksenli bloklar kapının kenarına bakar
                    if bl.color == g.color and bl.way: bl.way = 'V' if edge in ('top', 'bottom') else 'H'
    return b


def anneal(spec, picture_m, board: Board, rng, iters=1500, boost=0) -> Tuple[Board, float, dict]:
    cur, (cur_s, cur_m) = board, cheap_score(spec, picture_m, board, boost)
    best = (cur, cur_s, cur_m)
    temp = 2.0
    for it in range(iters):
        if best[1] >= 50 and it >= iters // 2: break        # yarı yolda hâlâ geçersiz: büyük olasılıkla umutsuz, yeni adaya geç
        cand = mutate(cur, rng)
        s, m = cheap_score(spec, picture_m, cand, boost)
        if s < cur_s or rng.random() < math.exp((cur_s - s) / max(0.02, temp)):
            cur, cur_s, cur_m = cand, s, m
            if s < best[1]: best = (cand, s, m)
        temp *= 0.996
    return best


def play_score(spec, ev) -> float:
    p = spec.play
    return 6 * in_range(ev['win_rate'], p['win_rate']) + 1.0 * in_range(ev['slots'], p['slot_peak']) + \
        0.03 * in_range(ev['seconds'], p['seconds']) + (5 if not ev['expert']['solved'] else 0)


def candidate(spec, picture: Picture, picture_m, rng, boost: int, iters: int):
    """Tek aday: envanter + rastgele kurulum + tavlama. Çözülemeyen ya da başta kapıda blok olan aday None."""
    inv = inventory(spec, picture, rng, boost)
    b0 = None
    for _ in range(50):
        b0 = random_board(spec, picture, inv, rng)
        if b0: break
    if not b0: return None
    b, s, m = anneal(spec, picture_m, b0, rng, iters=iters, boost=boost)
    return (s, b, m) if s < 50 else None


MIN_BOOST = -1      # zorluk ayarı (hücre sayısı) en çok bu kadar düşer
MAX_DROPS = 2       # hedef tutmayınca en çok kaç mekanik düşürülerek baştan denenir
ENOUGH = 3          # bu kadar uygun aday çıkınca o tur erken biter


def generate(spec, picture: Picture, restarts=6, iters=600, runs=10, final_runs=40, seed=None, log=print, max_rounds=6):
    """Üret ve sına: her turda birkaç zorluk ayarında (boost) adaylar üretilir, hepsi oyun simülatöründe oynatılır.
    Kazanma oranı hedefte ve usta bot çözüyorsa en iyi tahta puanlısı seçilir. Hedef tutmazsa ayarlar hedefi kuşatacak
    yöne kayar (hepsi zorsa kolaya, hepsi kolaysa zora; arada kaldıysa kesişimin iki yanı yoğunlaştırılır). Seçilen tahta
    `final_runs` denemeyle yeniden ölçülür. Usta botun çözemediği tahta seçilmez (adil değil)."""
    rng = random.Random(seed if seed is not None else spec.seed + 1)
    result = _generate(spec, picture, rng, restarts, iters, runs, final_runs, log, max_rounds)
    lo = spec.play['win_rate'][0]
    def too_hard(r): return r is None or r[3]['win_rate'] < lo - 0.2 or not r[3]['expert']['solved']
    drops = 0
    # Tahta çıkmadıysa ya da hedefin çok altında kaldıysa (çok zor) bölümün en ağır mekaniği düşürülerek yeniden denenir;
    # daha iyi olan tutulur (ör. kolay bölümde dört mekanik, ya da zor resimle kilit + buz).
    for drop in ('keylock', 'layer', 'ice', 'asteroid_count', 'asteroid', 'oneway'):
        if not too_hard(result): break
        if drops >= MAX_DROPS and result is not None and result[3]['expert']['solved']: break    # usta çözemiyorsa sınır işlemez
        if not spec.board.get(drop): continue
        from .spec import NOVELTY
        if any(lvl == spec.n and k == drop for lvl, k, _ in NOVELTY): continue      # tanıtılan mekanik düşmez
        import copy
        spec = copy.deepcopy(spec)
        spec.board[drop] = 0
        if drop == 'layer': spec.board['layer_share'] = 0
        if drop == 'oneway': spec.board['oneway_share'] = 0
        drops += 1
        log(f'  tahta çıkmadı ya da çok zor: {drop} düşürüldü')
        retry = _generate(spec, picture, rng, restarts, iters, runs, final_runs, log, max_rounds)
        if retry is not None and (result is None or abs(retry[3]['win_rate'] - lo) < abs(result[3]['win_rate'] - lo)
                                  or (retry[3]['expert']['solved'] and not result[3]['expert']['solved'])):
            result = retry
    return result


def _generate(spec, picture, rng, restarts, iters, runs, final_runs, log, max_rounds):
    lo, hi = spec.play['win_rate']
    mid = (lo + hi) / 2
    picture_m = dict(pic.metrics(picture), rank=color_rank(picture))
    pool = []                                   # (boost, tahta puanı, tahta, ölçüler, oynanış)
    # Uygun aday: usta çözüyor, gürültülü bot ve gerçekçi oyuncular hedef bandında. Gerçekçi oyuncu testi yalnız bot bandındakilere.
    def human_fit(ev): return 'human' in ev and in_range(ev['human']['win'], HUMAN_WIN[spec.tier]) == 0
    def suitable(ps): return sum(1 for p in ps if p[4]['expert']['solved'] and lo <= p[4]['win_rate'] <= hi and human_fit(p[4]))
    boosts = [-1, 0, 1]
    for rnd in range(max_rounds):
        # Ayarlar sırayla denenir (erken durunca aday havuzu tek ayara yığılmasın); yeterli uygun aday çıkınca tur biter.
        for _, boost in itertools.product(range(restarts), boosts):
            c = candidate(spec, picture, picture_m, rng, boost, iters)
            if not c: continue
            s, b, m = c
            ev = evaluate(picture, b, runs=runs)
            if ev['expert']['solved'] and lo <= ev['win_rate'] <= hi:
                ev['human'] = human_eval(picture, b, spec.n, runs=20, tier=spec.tier)
            pool.append((boost, s, b, m, ev))
            log(f"  aday (ayar {boost:+d}): tahta puanı {s:.2f}, kazanma %{ev['win_rate'] * 100:.0f}, usta "
                f"{'çözdü' if ev['expert']['solved'] else 'çözemedi'}, slot {ev['slots']:.1f}, süre {ev['seconds']:.0f} sn")
            if suitable(pool) >= ENOUGH: break
        if suitable(pool) >= 2: break                   # seçim için en az iki uygun aday
        # Ayar başına ortalama kazanma: hedefi kuşatan komşu ayarları bul.
        by = {}
        for p in pool: by.setdefault(p[0], []).append(p[4]['win_rate'] if p[4]['expert']['solved'] else 0.0)
        avg = {k: sum(v) / len(v) for k, v in by.items()}
        tried = sorted(avg)
        if not tried: boosts = [min(boosts) - 1, min(boosts) - 2]      # hiç geçerli aday yok: daha az blok, daha kolay
        elif all(avg[k] < lo for k in tried): boosts = [tried[0] - 1, tried[0] - 2]
        elif all(avg[k] > hi for k in tried): boosts = [tried[-1] + 1, tried[-1] + 2]
        else:
            near = min(tried, key=lambda k: abs(avg[k] - mid))
            boosts = [near] + [k for k in (near - 1, near + 1) if k in avg and (avg[k] - mid) * (avg[near] - mid) < 0]
        # Blok azaltma sınırlı: ölçümde tahtayı boşaltmak oyunu kolaylaştırmıyor, yalnız doluluğu düşürüyordu.
        boosts = [k for k in dict.fromkeys(boosts) if MIN_BOOST <= k <= 6] or [0]
    if not pool: return None
    def total(p):
        boost, s, b, m, ev = p
        return s + play_score(spec, ev) + (0 if ev['expert']['solved'] else 100)
    # En iyi birkaç aday daha çok denemeyle yeniden ölçülür (12 denemelik ölçüm ±%13 oynar); seçim yeniden ölçüme göre.
    finals = []
    ranked = sorted(pool, key=lambda p: (in_range(p[4]['win_rate'], (lo, hi)) > 0 or not p[4]['expert']['solved'],
                                         not human_fit(p[4]), abs(p[4]['win_rate'] - mid), total(p)))
    for boost, s, b, m, ev in ranked[:8]:
        ev = evaluate(picture, b, runs=final_runs)
        # Gerçekçi oyuncularla doğrulama: süre sınırı buradan çıkar; ağırlıklı kazanma zorluk bandında, acemi süreden az kaybeder.
        ev['human'] = human_eval(picture, b, spec.n, runs=20, tier=spec.tier)
        finals.append((boost, s, b, m, ev))
    # Önce hedefin içindekiler (tahta puanına göre), yoksa hedefe en yakın olan.
    def miss(p): return in_range(p[4]['win_rate'], (lo, hi)) + (1 if not p[4]['expert']['solved'] else 0)
    # Hedefin içindekilerde eğlence: usta botun karar anlarındaki yararlı seçenek sayısı (anlamlı seçim) yüksek, boşta
    # dönme düşük tahta tercih edilir.
    def fun(p): return -p[4]['expert']['choice'] + p[4]['expert']['idle']
    def human(p): return in_range(p[4]['human']['win'], HUMAN_WIN[spec.tier])
    def novice(p): return max(0.0, p[4]['human']['profiles']['acemi']['time_fail'] - NOVICE_TIME_FAIL_MAX[spec.tier])
    # Önce gürültülü bot bandı (referans etiketleriyle ölçüldü), sonra gerçekçi oyuncu bandı, sonra acemi süre kaybı, sonra
    # tahta puanı ve eğlence. Acemi kaybı banda eklenmez: yoksa süreyi rahat ama herkese çok zor tahta seçilir.
    best = min(finals, key=lambda p: (miss(p) > 0, human(p) > 0.02, miss(p) + human(p), novice(p), total(p) + 2 * fun(p)))
    boost, s, b, m, ev = best
    h = ev['human']
    log(f"  seçilen (ayar {boost:+d}): kazanma %{ev['win_rate'] * 100:.0f} ({final_runs} deneme), hedef %{lo * 100:.0f}–{hi * 100:.0f}; "
        f"gerçekçi oyuncular %{h['win'] * 100:.0f} (" + ', '.join(f"{k} %{v['win'] * 100:.0f}" for k, v in h['profiles'].items()) +
        f"), süre sınırı {h['time_limit']} sn")
    return total(best), b, m, ev
