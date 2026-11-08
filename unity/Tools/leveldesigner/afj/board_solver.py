"""Tahta çözücü (sürükleme modeli): oyunda blok parmağı izler, fizikle engellerin kenarı boyunca kayar; yani bir blok
diğerlerine dokunmadan bağlı boş hücrelerden geçerek ulaşabildiği her konuma gidebilir. Blok dönmez; tek eksenli bloklar
yalnız kendi ekseninde ilerler. Kapı: blok kapı kenarındaki satır/sütunda ve kapı açıklığının içindeyken çıkar.

Ölçümler (referans analiziyle aynı tanımlar):
- serbest blok: başka bloğa dokunmadan kapısına ulaşabilen blok,
- katmanlar: serbest blokların hepsi çıkınca yeniler serbest kalır; tahtanın kaç dalgada boşaldığı,
- yer değiştirme: hiç serbest blok yokken çıkarmadan kenara çekilmesi gereken blok sayısı (en kısa arama).
Donuk bloklar hiç oynamaz: buzlu blok (buz sayacı > çıkmış aşama sayısı) ve anahtarı henüz çıkmamış kilitli blok. Durum
yalnız tahtadaki aşamaları tutar (Board.block, Board.exit_state): katmanlı bloğun dış katmanı çıkınca blok aynı yerde iç
renkte kalır; her aşama çıkışı buz ve asteroit sayaçlarını azaltır, anahtar blok tamamen çıkınca açar. Asteroitler ve
delikler Board.blocked ile dolu hücredir.
"""
from collections import deque
from typing import Dict, List, Optional, Tuple

from .model import Board, Block, Gate

DIRS = ((0, 1), (0, -1), (1, 0), (-1, 0))


def can_exit(b: Block, pos, g: Gate, W: int, H: int) -> bool:
    if b.color != g.color: return False
    if b.way == 'H' and g.edge in ('top', 'bottom'): return False      # tek eksenli blok yalnız kendi ekseninden çıkar
    if b.way == 'V' and g.edge in ('left', 'right'): return False
    cs = b.cells(pos)
    xs = [c[0] for c in cs]; ys = [c[1] for c in cs]
    def span(v): return g.lo <= min(v) and max(v) <= g.lo + g.w - 1
    if g.edge == 'top': return max(ys) == H - 1 and span(xs)
    if g.edge == 'bottom': return min(ys) == 0 and span(xs)
    if g.edge == 'left': return min(xs) == 0 and span(ys)
    return max(xs) == W - 1 and span(ys)


def occupancy(board: Board, state: Dict[int, tuple], skip: Optional[int] = None) -> set:
    occ = set()
    for j, p in state.items():
        if j != skip: occ.update(board.block(j).cells(p))
    return occ


def frozen(board: Board, state: Dict[int, tuple], i: int) -> bool:
    """Donuk blok: buz sayacı çıkmış aşama sayısından büyük ya da kilitli ve anahtarlı blok tamamen çıkmamış."""
    b = board.block(i)
    if b.ice and b.ice > board.exits_done(state): return True
    if b.lock and not any(board.gone(state, j) and board.blocks[j].key == b.lock for j in range(len(board.blocks))): return True
    return False


def _info(board: Board, key: int) -> tuple:
    """Aşamanın değişmez bilgileri (tahta başına önbellekli): şekil bit maskesi, boyut, gidebildiği yönler, çıkış konumları.
    Hücre (x, y) = bit x + y * W; (x, y) konumundaki blok = maske << (x + y * W)."""
    cache = board.__dict__.setdefault('_info_cache', {})
    info = cache.get(key)
    if info is None:
        b = board.block(key)
        mask = 0
        for dx, dy in b.rel: mask |= 1 << (dx + dy * board.W)
        dirs = tuple(d for d in DIRS if not (b.way == 'H' and d[0] == 0) and not (b.way == 'V' and d[1] == 0))
        info = cache[key] = (mask, *b.size, dirs, exit_positions(board, key))
    return info


def _occ_mask(board: Board, state: Dict[int, tuple]) -> int:
    W = board.W
    occ = 0
    for j, (x, y) in state.items(): occ |= _info(board, j)[0] << (x + y * W)
    return occ


def _blocked_mask(board: Board, state: Dict[int, tuple]) -> int:
    """Board.blocked'ın bit maskesi (sağlam asteroitler yalnız çıkmış aşama sayısına bağlı; sayı başına önbellekli)."""
    done = board.exits_done(state) if board.asteroids else 0
    cache = board.__dict__.setdefault('_blocked_cache', {})
    mask = cache.get(done)
    if mask is None:
        W, H = board.W, board.H
        mask = cache[done] = sum(1 << (x + y * W) for x, y in board.blocked(state) if 0 <= x < W and 0 <= y < H)
    return mask


def _bfs(board: Board, info: tuple, start: tuple, others: int, exits=None):
    """Blok şeklinin `others` hücrelerine değmeden gezdiği konumlar → yol uzunluğu. `exits` verilirse ilk çıkış konumunun
    uzaklığı döner (yayılma sırası uzaklığa göre olduğundan en kısası), yoksa None."""
    mask, bw, bh, dirs, _ = info
    W, H = board.W, board.H
    if exits is not None and start in exits: return 0
    dist = {start: 0}
    q = deque([start])
    while q:
        p = q.popleft()
        d = dist[p] + 1
        for dx, dy in dirs:
            nx, ny = p[0] + dx, p[1] + dy
            if nx < 0 or ny < 0 or nx + bw > W or ny + bh > H or (nx, ny) in dist: continue
            if mask << (nx + ny * W) & others: continue
            if exits is not None and (nx, ny) in exits: return d
            dist[(nx, ny)] = d
            q.append((nx, ny))
    return None if exits is not None else dist


def reach(board: Board, state: Dict[int, tuple], i: int, occ: Optional[int] = None, blk: Optional[int] = None) -> Dict[tuple, int]:
    """Bloğun diğerlerine dokunmadan gidebildiği konumlar → hücre cinsinden yol uzunluğu (donuk blok yerinde kalır).
    `occ` ve `blk` durumun maskeleri; aynı durumda çok blok soran çağıran bir kez hesaplayıp geçer."""
    if frozen(board, state, i): return {state[i]: 0}
    if occ is None: occ = _occ_mask(board, state)
    if blk is None: blk = _blocked_mask(board, state)
    info = _info(board, i)
    x, y = state[i]
    return _bfs(board, info, state[i], (occ ^ info[0] << (x + y * board.W)) | blk)


def exit_positions(board: Board, i: int) -> set:
    """Aşamanın kapıdan çıkabildiği konumlar (yalnız şekle, renge, eksene ve kapılara bağlı; tahta başına önbellekli)."""
    cache = board.__dict__.setdefault('_exit_cache', {})
    b = board.block(i)
    key = (i, b.color, tuple(b.rel), b.way)
    found = cache.get(key)
    if found is None:
        found = cache[key] = {(x, y) for x in range(board.W) for y in range(board.H)
                              if any(can_exit(b, (x, y), g, board.W, board.H) for g in board.gates)}
    return found


def free_blocks(board: Board, state: Dict[int, tuple], occ: Optional[int] = None, blk: Optional[int] = None) -> Dict[int, int]:
    """Serbest bloklar → kapıya en kısa yol."""
    if occ is None: occ = _occ_mask(board, state)
    if blk is None: blk = _blocked_mask(board, state)
    W = board.W
    out = {}
    for i, pos in state.items():
        info = _info(board, i)
        exits = info[4]
        if not exits: continue
        if frozen(board, state, i):
            if pos in exits: out[i] = 0
            continue
        best = _bfs(board, info, pos, (occ ^ info[0] << (pos[0] + pos[1] * W)) | blk, exits)
        if best is not None:
            out[i] = best
    return out


def stuck_phases(board: Board) -> int:
    """Diğer bloklar ve kırılabilir asteroitler kalksa bile kapısına ulaşamayan aşama sayısı. Sıfır değilse tahta kesin
    çözülemez; pahalı aramaya girmeden anlaşılır."""
    W, H = board.W, board.H
    perm = set(board.holes)
    for a in board.asteroids:
        if a.count == 0: perm.update(a.cells())
    mask = sum(1 << (x + y * W) for x, y in perm if 0 <= x < W and 0 <= y < H)
    stuck = 0
    for i, b in enumerate(board.blocks):
        for key in ((i, -i - 1) if b.inner else (i,)):
            info = _info(board, key)
            if _bfs(board, info, b.pos, mask, info[4]) is None: stuck += 1
    return stuck


def unlock_search(board: Board, state: Dict[int, tuple], max_depth: int = 4, cap: int = 20000, goal=None):
    """Blokları kenara çekerek (en az hamle) yeni serbest blok açan durumu bul. `goal(i)` verilirse yalnız o koşulu
    sağlayan bir blok serbest kalınca durur (ör. rengi şu an resimde açık olan blok)."""
    seen = {tuple(sorted(state.items()))}
    q = deque([(state, 0)])
    W = board.W
    blk = _blocked_mask(board, state)           # kenara çekmede blok çıkmaz: sağlam asteroitler değişmez
    while q and len(seen) < cap:
        st, d = q.popleft()
        if d >= max_depth: continue
        occ = _occ_mask(board, st)
        for i in list(st):
            mask, cur = _info(board, i)[0], st[i]
            without = occ ^ mask << (cur[0] + cur[1] * W)
            for p in reach(board, st, i, occ, blk):
                if p == cur: continue
                trial = dict(st); trial[i] = p
                key = tuple(sorted(trial.items()))
                if key in seen: continue
                seen.add(key)
                f = free_blocks(board, trial, without | mask << (p[0] + p[1] * W), blk)
                if f and (goal is None or any(goal(j) for j in f)):
                    return trial, d + 1
                q.append((trial, d + 1))
    return None


def peel(board: Board, cap: int = 20000) -> dict:
    """Katman katman boşaltma: serbest blokların hepsi çıkar; yoksa yer değiştirme aranır."""
    state = board.start_state()
    layers: List[List[int]] = []; dists: List[int] = []; shuffles = 0; first_free = None
    while state:
        f = free_blocks(board, state)
        if first_free is None: first_free = len(f)
        if f:
            layers.append(sorted(f)); dists += f.values()
            for i in f: state = board.exit_state(state, i)
            continue
        found = unlock_search(board, state, cap=cap)
        if not found:
            return dict(solved=False, first_free=first_free or 0, layers=layers, dists=dists, shuffles=shuffles)
        state, extra = found
        shuffles += extra
    return dict(solved=True, first_free=first_free or 0, layers=layers, dists=dists, shuffles=shuffles)
