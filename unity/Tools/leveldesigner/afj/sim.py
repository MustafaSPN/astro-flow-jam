"""Astro Flow Jam oyun simülasyonu (Unity ConveyorRunner + LevelController + BoardInput kurallarının ayrık karşılığı).

Kurallar:
- Resmin çevresinde kapalı bir bant (yörünge); bantta en çok `belt_capacity` (5) roket birimi.
- Roket bant boyunca ilerler; hizalandığı satır/sütunda kenardan görünen ilk piksel kendi rengindeyse vurur.
  Bir geçişte her hatta en fazla bir atış (tek sıra kuralı); kenar değişince ya da yeni tur başlayınca sıfırlanır.
- Tahtadan çıkan blok kapıda hücrelerine bölünür, her hücre bir rokettir. Çıkış için bant + boş slot yeri blok hücre
  sayısına yetmeli; bantta yer kalmazsa fazla parçalar slotlara iner.
- Tur biten roket mermisi varsa boş slota iner (slot yoksa turlamaya devam eder); mermisi biten kaybolur.
- 2x (otomatik bitirici): tahtada blok kalmamış, kapıda roket yok ve kalan roketlerin hepsi banda sığıyorsa;
  bant 2 kat hızlı, atış hızı artar, roketler slota inmez.
- Kayıp: tur biten roket slota sığmıyor, banttaki hiçbir roket hiçbir pikseli vuramıyor, yola çıkmış roket yok,
  tahtada boş yere sığan blok yok ve slottaki hiçbir roket yörüngeye çıkıp vuramıyor.
- Katmanlı blokta dış katman çıkınca blok aynı yerde iç renkte kalır (Board.exit_state); asteroitler dolu hücredir.
Zamanlama Unity ile aynı: kapıdan geçiş 0,4 sn, dönüşüm 0,3 sn, yörüngeye atlama 0,15 sn, parçalar arası 0,3 sn.

Bot: her karar anında yararlı seçenekler (kendi rengi şu an kenardan görünen) arasından en çok hedefi olanı seçer;
`noise` olasılıkla rastgele bir seçenek seçer (insan hatası). Yararlı seçenek yoksa bantta bir şey dönerken bekler; bant
boşsa zorunlu hamle yapar. Hiç serbest blok yoksa blokları kenara çeker (yer değiştirme). Zorluk hedefleri bu bota göre
(referans etiketleriyle ölçüldü).

Gerçekçi insan botu (`profile`, HUMAN_PROFILES): düşünme süresi log-normal (acemi yavaş), az görünen rengi gözden kaçırır,
slottaki roketleri zaman zaman unutur, en iyi yerine ikinci iyi hamleyi seçer, ara sıra boş hamle yapar, sürüklemesi
yavaştır; `time_limit` verilirse süre dolunca kaybeder. Süre sınırı ve zorluk sırası bu botlarla doğrulanır.
"""
import random
from dataclasses import dataclass
from typing import Dict, List, Optional

from . import board_solver as bs
from .model import Board, Picture, EMPTY

PICTURE_WIDTH = 6.0
CONVEYOR_SPEED = 5.0 * (PICTURE_WIDTH / 6.2)      # OrbitTuning.ConveyorSpeed × ölçek
EXIT_SECONDS = 0.4 + 0.3                          # kapıdan geçiş + roket gövdesine dönüşüm (açılma dahil)
JUMP_SECONDS = 0.15
PIECE_INTERVAL = 0.3
SLOT_TAP_SECONDS = 0.25
DRAG_BASE, DRAG_PER_CELL = 0.35, 0.06             # sürükleme: tutma + hücre başı
DECISION_COOLDOWN = 0.35
REPOSITION_SECONDS = 0.6
CORNER_RATIO = 0.204                              # köşe yayı / resim genişliği (yörünge modeli: π/2·0,78 / 6)


@dataclass
class Rocket:
    color: str
    ammo: int
    state: str = 'enter'          # enter | ride | wait
    timer: float = 0.0
    step: int = 0
    used: set = None
    side: Optional[str] = None


# Gerçekçi oyuncu tipleri. think: karar başına ortalama düşünme (sn, log-normal, sigma), error: en iyi yerine başka bir
# yararlı hamle, blunder: herhangi bir hamle, forget: bu kararda slottaki roketleri görmez, miss: en çok 2 pikseli görünen
# rengi fark etmez, drag: sürükleme süresi çarpanı, look: bölüm başında tahtaya bakma süresi. blunder (dürtüsel hamle:
# rengine bakmadan bir blok ya da slot roketi) acemide referans etiketleriyle ölçülen gürültülü bota (%25) yakın.
# weight: genel kazanma oranındaki payı.
HUMAN_PROFILES = {
    'acemi':    dict(think=1.7, sigma=0.45, error=0.28, blunder=0.20, forget=0.25, miss=0.40, drag=1.4, look=4.0, weight=0.3),
    'ortalama': dict(think=1.15, sigma=0.40, error=0.16, blunder=0.08, forget=0.12, miss=0.20, drag=1.15, look=3.0, weight=0.5),
    'iyi':      dict(think=0.75, sigma=0.35, error=0.06, blunder=0.02, forget=0.04, miss=0.05, drag=1.0, look=2.0, weight=0.2),
}
WATCH_SECONDS = 0.3                               # insan bot: yapacak hamle yokken ekrana bakma aralığı


class Sim:
    def __init__(self, picture: Picture, board: Board, belt_capacity=5, slot_count=5, noise=0.0, seed=0,
                 profile: Optional[dict] = None, time_limit: Optional[float] = None):
        self.N, self.M = picture.W, picture.H
        self.g = [list(r) for r in picture.rows]
        self.remaining = sum(1 for r in self.g for c in r if c != EMPTY)
        self.board = board
        self.state = board.start_state()
        self.belt_capacity, self.slot_count = belt_capacity, slot_count
        self.rng = random.Random(seed)
        self.noise = noise
        self.profile = profile
        self.time_limit = time_limit
        if profile: self.decision_at = profile['look']
        self.rockets: List[Rocket] = []
        self.pending: List[Rocket] = []          # kapıdan geçen, banda girecek parçalar
        self.t = 0.0
        self.decision_at = 0.0
        self.autofinisher = False
        self.path = self._path()
        pitch = PICTURE_WIDTH / max(self.N, self.M)
        self.step_seconds = pitch / CONVEYOR_SPEED
        self.stats = dict(forced=0, max_slots=0, decisions=0, useful_choices=0, idle_steps=0, steps=0,
                          repositions=0, exits=0, locked_exits=0)
        self.fail: Optional[str] = None

    # ------------------------------------------------------------------ resim
    def _path(self):
        """Bant: sol kenarda katılma noktasından yukarı, üst, sağ, alt, soldan yukarı (kapalı tur). Köşelerde atış yok."""
        N, M = self.N, self.M
        corner = max(1, round(CORNER_RATIO * max(N, M)))
        merge = int(M * 0.67)                     # katılma noktası sol kenarın alt üçte biri civarında (yörünge modeli)
        p = [('left', y) for y in range(merge, -1, -1)]
        p += [(None, None)] * corner + [('top', x) for x in range(N)]
        p += [(None, None)] * corner + [('right', y) for y in range(M)]
        p += [(None, None)] * corner + [('bottom', x) for x in range(N - 1, -1, -1)]
        p += [(None, None)] * corner + [('left', y) for y in range(M - 1, merge, -1)]
        return p

    def front(self, side, line):
        g, N, M = self.g, self.N, self.M
        if side == 'top':
            for y in range(M):
                if g[y][line] != EMPTY: return y, line
        elif side == 'bottom':
            for y in range(M - 1, -1, -1):
                if g[y][line] != EMPTY: return y, line
        elif side == 'left':
            for x in range(N):
                if g[line][x] != EMPTY: return line, x
        else:
            for x in range(N - 1, -1, -1):
                if g[line][x] != EMPTY: return line, x
        return None

    def exposed(self) -> Dict[str, int]:
        out: Dict[str, int] = {}
        seen = set()
        for side, n in (('top', self.N), ('bottom', self.N), ('left', self.M), ('right', self.M)):
            for line in range(n):
                f = self.front(side, line)
                if f and f not in seen:
                    seen.add(f); c = self.g[f[0]][f[1]]; out[c] = out.get(c, 0) + 1
        return out

    def remaining_of(self, color):
        return sum(1 for r in self.g for c in r if c == color)

    # ------------------------------------------------------------------ yer hesabı
    def belt_used(self):
        return sum(1 for r in self.rockets if r.state in ('ride', 'enter')) + len(self.pending)

    def slots_used(self):
        return sum(1 for r in self.rockets if r.state == 'wait')

    def room(self):
        return (self.belt_capacity - self.belt_used()) + (self.slot_count - self.slots_used())

    # ------------------------------------------------------------------ hamleler
    def exit_block(self, i, dist):
        b = self.board.block(i)
        self.state = self.board.exit_state(self.state, i)      # katmanlı blokta iç aşama tahtada kalır
        free_belt = self.belt_capacity - self.belt_used()
        drag = (DRAG_BASE + DRAG_PER_CELL * dist) * (self.profile['drag'] if self.profile else 1.0)
        delay = drag + EXIT_SECONDS + JUMP_SECONDS
        exp = self.exposed()
        self.stats['exits'] += 1
        if exp.get(b.color, 0) == 0: self.stats['locked_exits'] += 1
        for k, ammo in enumerate(b.ammo):
            r = Rocket(b.color, ammo, used=set())
            if k < free_belt:
                r.state, r.timer = 'enter', delay + k * PIECE_INTERVAL
            else:
                r.state = 'wait'
            self.rockets.append(r)
        self.decision_at = self.t + delay * 0.6

    def tap_slot(self, r: Rocket):
        r.state, r.timer, r.used, r.side = 'enter', SLOT_TAP_SECONDS + JUMP_SECONDS, set(), None

    def board_left(self):
        return len(self.state)

    def think(self):
        """Bir sonraki karara kadar geçen süre: usta/gürültülü bot sabit, insan bot log-normal düşünme süresi."""
        p = self.profile
        if not p: return DECISION_COOLDOWN
        import math
        return max(0.25, self.rng.lognormvariate(math.log(p['think']) - p['sigma'] ** 2 / 2, p['sigma']))

    def decide(self):
        if self.profile: return self.decide_human()
        self.decision_at = self.t + DECISION_COOLDOWN
        free_belt = self.belt_capacity - self.belt_used()
        room = self.room()
        if room <= 0: return
        exp = self.exposed()
        free = bs.free_blocks(self.board, self.state) if self.state else {}
        options = []
        for i, d in free.items():
            b = self.board.block(i)
            if len(b.ammo) <= room: options.append(('exit', i, b.color, d))
        if free_belt > 0:
            for r in self.rockets:
                if r.state == 'wait': options.append(('slot', r, r.color, 0))
        useful = [o for o in options if exp.get(o[2], 0) > 0]
        if options:
            self.stats['decisions'] += 1
            self.stats['useful_choices'] += len({o[2] for o in useful})
        pick = None
        if options and self.noise and self.rng.random() < self.noise:
            pick = self.rng.choice(options)
        elif useful:
            pick = max(useful, key=lambda o: (exp.get(o[2], 0), o[0] == 'slot', -o[3]))
        else:
            if self.should_wait(): return
            # Yararlı hamle yok ve bant boş: önce rengi açık bir bloğun önünü açacak yer değiştirmeyi dene.
            if self.state and room > 0:
                found = bs.unlock_search(self.board, self.state, max_depth=3, cap=4000,
                                         goal=lambda j: exp.get(self.board.block(j).color, 0) > 0
                                         and len(self.board.block(j).ammo) <= room)
                if found:
                    self.state, moves = found
                    self.stats['repositions'] += moves
                    self.decision_at = self.t + REPOSITION_SECONDS * moves
                    return
            if options:
                self.stats['forced'] += 1
                pick = max(options, key=lambda o: self.remaining_of(o[2]))
            elif self.state and not free:
                found = bs.unlock_search(self.board, self.state)
                if found:
                    self.state, moves = found
                    self.stats['repositions'] += moves
                    self.decision_at = self.t + REPOSITION_SECONDS * moves
                    return
                self.fail = 'tahta kilitli'
                return
        if pick:
            if pick[0] == 'exit': self.exit_block(pick[1], pick[3])
            else: self.tap_slot(pick[1])

    def decide_human(self):
        """Gerçekçi insan: algı (az görünen rengi kaçırma, slotu unutma), seçim hatası, düşünme ve sürükleme süresi."""
        p, rng = self.profile, self.rng
        self.decision_at = self.t + WATCH_SECONDS
        free_belt = self.belt_capacity - self.belt_used()
        room = self.room()
        if room <= 0: return
        exp = self.exposed()
        seen = {c: k for c, k in exp.items() if k > 2 or rng.random() >= p['miss']}
        free = bs.free_blocks(self.board, self.state) if self.state else {}
        options = [('exit', i, self.board.block(i).color, d) for i, d in free.items() if len(self.board.block(i).ammo) <= room]
        if free_belt > 0 and rng.random() >= p['forget']:
            options += [('slot', r, r.color, 0) for r in self.rockets if r.state == 'wait']
        useful = sorted((o for o in options if seen.get(o[2], 0) > 0), key=lambda o: (-seen[o[2]], o[0] != 'slot', o[3]))
        if options:
            self.stats['decisions'] += 1
            self.stats['useful_choices'] += len({o[2] for o in useful})
        pick = None
        u = rng.random()
        if options and u < p['blunder']:
            pick = rng.choice(options)
        elif useful:
            pick = useful[0] if len(useful) == 1 or rng.random() >= p['error'] else rng.choice(useful[1:])
        else:
            if self.should_wait(): return                     # bant dönüyor: bekler, izler
            if self.state and room > 0:
                found = bs.unlock_search(self.board, self.state, max_depth=2, cap=1500,
                                         goal=lambda j: seen.get(self.board.block(j).color, 0) > 0
                                         and len(self.board.block(j).ammo) <= room)
                if found:
                    self.state, moves = found
                    self.stats['repositions'] += moves
                    self.decision_at = self.t + REPOSITION_SECONDS * moves * p['drag'] + self.think()
                    return
            if options:
                self.stats['forced'] += 1
                pick = max(options, key=lambda o: self.remaining_of(o[2])) if rng.random() >= p['error'] else rng.choice(options)
            elif self.state and not free:
                found = bs.unlock_search(self.board, self.state)
                if found:
                    self.state, moves = found
                    self.stats['repositions'] += moves
                    self.decision_at = self.t + REPOSITION_SECONDS * moves * p['drag'] + self.think()
                    return
                self.fail = 'tahta kilitli'
                return
        if pick:
            if pick[0] == 'exit': self.exit_block(pick[1], pick[3])
            else: self.tap_slot(pick[1])
            self.decision_at = self.t + self.think()

    # ------------------------------------------------------------------ kayıp kontrolü
    def should_wait(self):
        """Yararlı hamle yokken bandı beklemek işe yarar mı? Bantta vuran roket, yola çıkmış parça ya da slota inecek yer varsa
        evet. Slotlar dolu ve bantta vurabilen yoksa roketler sonsuza dek döner: oyuncu beklemez, blokları oynatır."""
        if self.pending or any(r.state == 'enter' for r in self.rockets) or self.any_rider_can_shoot(): return True
        return any(r.state == 'ride' for r in self.rockets) and self.slots_used() < self.slot_count

    def any_rider_can_shoot(self):
        exp = self.exposed()
        return any(r.state == 'ride' and r.ammo > 0 and exp.get(r.color, 0) > 0 for r in self.rockets)

    def slot_can_help(self):
        """Slottaki bir roket yörüngede yer bulup kendi rengini vurabilir mi (Unity AnySlotRocketCanHelp)?"""
        if self.belt_capacity - self.belt_used() <= 0: return False
        exp = self.exposed()
        return any(r.state == 'wait' and r.ammo > 0 and exp.get(r.color, 0) > 0 for r in self.rockets)

    def more_coming(self):
        if any(r.state == 'enter' for r in self.rockets): return True
        room = self.room()
        return any(len(self.board.block(i).ammo) <= room for i in self.state)

    def update_autofinisher(self):
        if self.autofinisher or self.state: return
        if any(r.state == 'enter' for r in self.rockets): return
        if sum(1 for r in self.rockets if r.ammo > 0) <= self.belt_capacity:
            self.autofinisher = True

    # ------------------------------------------------------------------ adım
    def step(self, dt):
        self.update_autofinisher()
        for r in self.rockets:
            if r.state == 'enter':
                r.timer -= dt
                if r.timer <= 0: r.state, r.step, r.used, r.side = 'ride', 0, set(), None
        hit = False
        for r in list(self.rockets):
            if r.state != 'ride': continue
            side, line = self.path[r.step]
            if side and side != r.side:
                r.side, r.used = side, set()              # kenar değişince hat hafızası sıfırlanır
            if side and r.ammo > 0 and line not in r.used:
                f = self.front(side, line)
                if f and self.g[f[0]][f[1]] == r.color:
                    self.g[f[0]][f[1]] = EMPTY; self.remaining -= 1; r.ammo -= 1; r.used.add(line); hit = True
            if r.ammo <= 0:
                self.rockets.remove(r); continue
            r.step += 1
            if r.step >= len(self.path):
                r.step, r.used, r.side = 0, set(), None
                if self.autofinisher: continue
                if self.slots_used() < self.slot_count:
                    r.state = 'wait'
                elif not self.any_rider_can_shoot() and not self.more_coming() and not self.slot_can_help() and \
                        self.slots_used() >= self.slot_count:
                    self.fail = 'yer kalmadı'
        self.stats['steps'] += 1
        if not hit: self.stats['idle_steps'] += 1
        self.stats['max_slots'] = max(self.stats['max_slots'], self.slots_used())

    def run(self, max_seconds=600, stall_seconds=60):
        last_remaining, last_progress = self.remaining, 0.0
        while self.t < max_seconds and self.fail is None:
            if self.remaining <= 0:
                return self.result(True)
            if self.remaining != last_remaining:
                last_remaining, last_progress = self.remaining, self.t
            elif self.t - last_progress > stall_seconds:
                self.fail = 'ilerleme yok'; break
            if self.time_limit and self.t >= self.time_limit:
                self.fail = 'süre'; break
            if self.t >= self.decision_at: self.decide()
            if self.fail: break
            speed = 2.0 if self.autofinisher else 1.0
            self.step(self.step_seconds)
            self.t += self.step_seconds / speed
            if not self.rockets and not self.state and self.remaining > 0:
                self.fail = 'mermi bitti'
        return self.result(False)

    def result(self, solved):
        s = self.stats
        return dict(solved=solved, fail=None if solved else (self.fail or 'zaman aşımı'), seconds=round(self.t, 1),
                    forced=s['forced'], max_slots=s['max_slots'], repositions=s['repositions'],
                    choice=s['useful_choices'] / max(1, s['decisions']), idle=s['idle_steps'] / max(1, s['steps']),
                    locked_exit_share=s['locked_exits'] / max(1, s['exits']))


# Süre payı: ortalama oyuncunun bitirme süresinin %80'lik dilimine çarpan (Kolay bölümde süre baskısı az).
# 2026-10-28: süreler oyuncuya çok uzun geldi; pay daraltıldı (ortalama oyuncu süreyi hissetsin).
TIME_MARGIN = {'Kolay': 1.0, 'Orta': 0.95, 'Zor': 0.9, 'Çok Zor': 0.88}


def human_time_limit(times, n: int, tier: str = 'Orta') -> int:
    """Süre sınırı: ortalama oyuncunun (süresiz) bitirme sürelerinin %80'lik dilimi × pay + 10 sn, 5'in katı, 60–300.
    Referanstaki gibi ilk 9 bölüm 300 sn, 10. bölüm en az 180 sn (öğretici bölümler süre baskısı taşımaz)."""
    import math
    if not times: return 120
    times = sorted(times)
    p80 = times[min(len(times) - 1, int(0.8 * len(times)))]
    t = int(min(240, max(45, 5 * math.ceil((TIME_MARGIN.get(tier, 1.0) * p80 + 5) / 5))))
    if n <= 3: return max(t, 90)                # öğretici bölümler: biraz rahat
    return t


def human_eval(picture: Picture, board: Board, n: int, runs=20, tier: str = 'Orta', **kw) -> dict:
    """Gerçekçi oyuncu tipleriyle oynatma: önce ortalama oyuncu süresiz oynar (süre sınırı buradan), sonra her tip süre
    sınırıyla `runs` kez. Döner: süre sınırı, tip başına kazanma ve süre yüzünden kayıp oranı, ağırlıklı kazanma."""
    base = [Sim(picture, board.copy(), seed=1000 + k, profile=HUMAN_PROFILES['ortalama'], **kw).run() for k in range(runs)]
    limit = human_time_limit([r['seconds'] for r in base if r['solved']], n, tier)
    out = dict(time_limit=limit, profiles={})
    total = 0.0
    for name, prof in HUMAN_PROFILES.items():
        res = [Sim(picture, board.copy(), seed=2000 + k, profile=prof, time_limit=limit, **kw).run() for k in range(runs)]
        win = [r for r in res if r['solved']]
        out['profiles'][name] = dict(win=len(win) / runs, time_fail=sum(1 for r in res if r['fail'] == 'süre') / runs,
                                     seconds=sum(r['seconds'] for r in win) / max(1, len(win)),
                                     left=sum(limit - r['seconds'] for r in win) / max(1, len(win)) / limit)
        total += prof['weight'] * len(win) / runs
    out['win'] = total
    return out


def evaluate(picture: Picture, board: Board, runs=20, noise=0.25, **kw) -> dict:
    """Usta bot (noise=0) bir kez + insana benzer bot `runs` kez: kazanma oranı, slot baskısı, süre."""
    expert = Sim(picture, board.copy(), **kw).run()
    noisy = [Sim(picture, board.copy(), noise=noise, seed=k, **kw).run() for k in range(runs)]
    win = [r for r in noisy if r['solved']]
    # Usta bot açgözlü oynar, çözülebilir tahtada da takılabilir: gürültülü botlardan biri kazandıysa tahta çözülebilir.
    expert = dict(expert, solved=expert['solved'] or bool(win))
    return dict(expert=expert, win_rate=len(win) / runs,
                slots=sum(r['max_slots'] for r in noisy) / runs,
                seconds=sum(r['seconds'] for r in win) / max(1, len(win)),
                forced=sum(r['forced'] for r in noisy) / runs,
                fails={k: sum(1 for r in noisy if r['fail'] == k) for k in {r['fail'] for r in noisy if r['fail']}})
