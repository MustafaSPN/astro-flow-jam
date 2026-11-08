"""Bölüm künyesi: bölüm numarasından resim, tahta ve oynanış hedeflerini üretir (1000+ bölüme ölçeklenir, belirleyici).

- Zorluk dalgası: referansın 10'luk kalıbı (Kolay, Kolay, Orta, Kolay, Zor, Kolay, Orta, Kolay, Zor, Çok Zor);
  1–10 öğretici, 11–20 yumuşatılmış. Her 50 bölümde bir "nefes" on'u (zorlar bir kademe hafif) yorgunluğu kırar.
- Yoğunluk: aynı zorluk seviyesinde ilerledikçe artan 0..1 değeri (log ölçekli; 100. bölümde ~0,55, 1000.'de ~1).
- Yenilik takvimi: yeni renk/şekil/mekanik önce kolay bir bölümde tanıtılır, 3–5 bölüm sonra zor bölümde sınanır.
  Oyunda henüz olmayan mekanikler config'teki `enabled` ile açılır.
- Resim tipi bölüm aralığına bağlı (referans analizi, referans verisi): 1–5 desen; 6–100 tam kare ikon
  kartpostal (arada mola desenleri); 101–400 boş zeminde silüet; 400+ silüet, tam sahne ve ikon karışık.
- Konu geniş yelpazeden (subjects.py); renkler konudan gelir (güneş sarı/turuncu), desenlerde referans resimden.
- Benzersizlik: konu son 60 bölümde, kategori bir önceki bölümde tekrar etmez.
"""
import collections
import json
import math
import os
import random
from dataclasses import asdict, dataclass, field
from typing import Dict, List, Optional

from . import targets as T

K, O, Z, CZ = T.TIERS
DECADE = [K, K, O, K, Z, K, O, K, Z, CZ]
FIRST = [K] * 9 + [O]
SECOND = [K, K, O, K, O, K, O, K, Z, Z]
MIN_DELTA_E = 22          # aynı bölümdeki renkler bir bakışta ayırt edilebilmeli (Lab ΔE)
# Renk tanıtım sırası (yalnız ilk bölümlerde, referans yokken): parlak, kolay ayırt edilen renkler
COLOR_ORDER = ['7', '0', '9', '3', '4', 'a', '6', '8']

# Yenilikler: (bölüm, anahtar, açıklama). Mekanik anahtarları config 'enabled' listesiyle süzülür.
NOVELTY = [
    (1, 'basics', 'Bloğu kendi rengindeki kapıdan çıkar; roket yörüngede pikselleri vurur'),
    (2, 'block2', '2 hücreli blok (kapıda 2 rokete ayrılır)'),
    (2, 'locked_color', 'İçte kilitli renk: dış katman kırılmadan vurulamaz'),
    (3, 'color3', '3. renk'),
    (5, 'slots', 'Bekleme slotları: mermisi kalan roket slota iner, dokununca tekrar uçar'),
    (7, 'color4', '4. renk'),
    (8, 'autofinisher', '2x hızlanma: tahta boşalınca kalan roketler yörüngeye sığarsa'),
    (11, 'reposition', 'İlk yer değiştirme: bir bloğu kenara çekmeden yol açılmıyor'),
    (12, 'oneway', 'Tek eksenli blok: yalnız yatay ya da yalnız dikey kayar'),
    (14, 'color5', '5. renk'),
    (16, 'holes', 'Delikli tahta: tahta düz dikdörtgen değil'),
    (21, 'shape_L', '3 hücreli L blok'),
    (24, 'ice', 'Buzlu blok: tahtadan sayacı kadar blok çıkınca çözülür'),
    (32, 'asteroid', 'Asteroit: hiç oynamayan engel'),
    (26, 'color6', '6. renk'),
    (38, 'color7', '7. renk'),
    (42, 'keylock', 'Anahtar – kilit: aynı renkteki anahtarlı blok çıkınca kilitli blok açılır'),
    (41, 'shape_T', '4 hücreli T blok + 3 genişlikli kapı'),
    (51, 'gate1', '1 genişlikli kapı: yalnız tek hücre genişliğinde bloklar geçer'),
    (56, 'layer', 'Katmanlı blok: dış katman kapıdan akıp roket olur, blok iç renkte kalır'),
    (64, 'color8', '8. renk'),
    (70, 'asteroid_count', 'Sayaçlı asteroit: sayacı kadar blok çıkınca patlar'),
]
DEFAULT_ENABLED = ['basics', 'block2', 'locked_color', 'color3', 'slots', 'color4', 'autofinisher', 'reposition',
                   'color5', 'color6', 'color7', 'color8', 'shape_L', 'shape_T', 'gate1', 'oneway', 'holes', 'ice', 'keylock',
                   'asteroid', 'layer', 'asteroid_count']
# Tahta mekaniklerinin kullanım planı (referansın 1701 bölümü):
# - Tanıtım: mekanik bölümünde kesin ve bol (tek eksen tanıtımında blokların %60'ı, buz tanıtımında 5 blokluk küçük tahta),
#   ardından bir "dalga": buz ve anahtar-kilit 7 bölüm art arda, tek eksen ve delik 3 bölüm.
# - Sonra kalıcı oran (yoğunlukla değişir): delik %60 → %95 (neredeyse her bölüm), tek eksen %30 → %60, buz ~%35–40,
#   anahtar-kilit ~%25–30. Aynı mekanik en çok MECH_GAP bölüm arayla yeniden görünür (referansta en uzun aralıklar).
# Katmanlı blok bölümlerin ~%20'sinde (bölümde 3–4, payı %17–24), sabit engel nadir (%4, 1–2 tek hücre), sayaçlı sandık
# tanıtımdan sonra ~%27 (bölümde 1–2, sayaç blok sayısının %41'i) – Küçük tahtamızda biraz azaltıldı.
# 2026-10-28: oyuncu mekanik yoğunluğunu az buldu: oranlar artırıldı, aralıklar kısaltıldı.
MECH_RATE = {'oneway': (0.45, 0.7), 'holes': (0.75, 0.95), 'ice': (0.6, 0.7), 'keylock': (0.5, 0.6),
             'asteroid': (0.2, 0.2), 'layer': (0.4, 0.45), 'asteroid_count': (0.35, 0.4)}
MECH_BURST = {'oneway': 3, 'holes': 3, 'ice': 7, 'keylock': 7, 'asteroid': 3, 'layer': 4, 'asteroid_count': 5}
MECH_GAP = {'oneway': 5, 'holes': 3, 'ice': 6, 'keylock': 9, 'asteroid': 12, 'layer': 7, 'asteroid_count': 8}
BOARD_MECHS = ['holes', 'oneway', 'ice', 'keylock', 'asteroid', 'layer', 'asteroid_count']
# Bölümde aynı anda en çok mekanik (delik sayılmaz, tahtanın zemini): referans 1–50 ortalama 2, 51–200 4; bizde resim tarafı da
# yeni şeyler öğrettiği için daha az.
def max_mechanics(n: int) -> int: return 1 if n <= 12 else 2 if n <= 30 else 3 if n <= 70 else 4
# Katmanlı blok tanıtım dalgasında blok payı (referans 16. bölüm: 13 bloğun 7'si); sonra bölümde 1–3.
LAYER_INTRO_SHARE = [0.4, 0.3, 0.25, 0.2]
# Tanıtım dalgasında tek eksenli blokların payı (referans 8–10. bölüm: %60, %33, %11); sonra 1–3 blok, yoğunlukla azalır.
ONEWAY_INTRO_SHARE = [0.5, 0.33, 0.15]

_REF = None
REF_TIER = {'Easy': K, 'Medium': O, 'Hard': Z, 'Very Hard': CZ, 'Ultra Hard': CZ}
_DATA = os.path.join(os.path.dirname(__file__), '..', 'data')


def style_reference(n: int, tier: str, used: List[int]) -> dict:
    """Referans resim derleminden (data/reference_corpus.json, depoda yok) aynı zorluktaki, numarası en yakın ve son 40
    bölümde kullanılmamış resim. Derlem yoksa yalnız bölüm numarası ve zorlukla boş bir referans döner."""
    global _REF
    if _REF is None:
        path = os.path.join(_DATA, 'reference_corpus.json')
        _REF = [r for r in json.load(open(path)) if r['bundle'] == '3db5681e'] if os.path.exists(path) else []
    pool = [r for r in _REF if REF_TIER[r['tier']] == tier]
    if not pool:
        return dict(n=n, tier=tier, W=0, H=0, colors=0, regions=0, locked=0)
    target = n if n <= 2600 else 1000 + (n % 1600)
    fresh = [r for r in pool if r['n'] not in used[-40:]] or pool
    return min(fresh, key=lambda r: (abs(r['n'] - target), r['n']))


_PALS = None


def reference_colors(ref_level: int, count: int) -> List[str]:
    """Desenler için: referans resmin renkleri (kullanım payına göre, data/reference_palettes.json); birbirine yakın
    renkler elenir. Eksik kalırsa en sık kullanılan renklerden, mevcutlardan yeterince farklı olanlar eklenir."""
    global _PALS
    from .model import CODE_OF_ID, delta_e
    if _PALS is None:
        path = os.path.join(_DATA, 'reference_palettes.json')
        _PALS = {int(k): v for k, v in json.load(open(path)).items()} if os.path.exists(path) else {}
    chosen: List[str] = []
    for pid, _ in _PALS.get(ref_level, []):
        if pid not in CODE_OF_ID: continue
        c = CODE_OF_ID[pid]
        if all(delta_e(c, o) >= MIN_DELTA_E for o in chosen): chosen.append(c)
        if len(chosen) == count: break
    return _fill(chosen, count)


FREQUENT = 'a2894370cbjd1prno56h'          # en sık kullanılan palet renkleri (sıklık sırasıyla)


def _fill(chosen: List[str], count: int) -> List[str]:
    from .model import delta_e
    for c in FREQUENT:
        if len(chosen) >= count: break
        if c not in chosen and all(delta_e(c, o) >= MIN_DELTA_E for o in chosen): chosen.append(c)
    return chosen[:count]


# Aynı renk ailesinden yakın tonlar: konunun rengi son bölümlerde çok kullanıldıysa yerine geçebilir, eksik renk
# yuvaları da öncelikle bunlarla dolar (iki tonlu gölge için doğal eş).
SIBLINGS = {
    '7': 'rki', 'r': '7i', 'k': '74g', 'i': 'r7',
    '0': '1h8', '1': '0h', '8': 't0', 't': '8d',
    '3': 'cdfs', 'c': '3d', 'd': 'c3t', 'f': 's3', 's': 'fc',
    '9': 'jq', 'j': '9qu', 'q': '94j', '4': 'qkg',
    'b': 'vu', 'v': 'bu', 'u': 'vbj',
    'p': '5go', '5': 'po', 'o': '5p', 'g': 'pk',
    '6': 'wen', 'w': '6o', 'e': 'h6p', 'n': '61', 'h': 'el',
    'a': 'lj', 'l': 'mxa', 'x': 'lm', 'm': 'x2', '2': 'm',
}
FINE_DETAIL = {'peri', 'denizkızı', 'prenses', 'büyücü', 'şövalye', 'kral', 'süper kahraman', 'palyaço', 'aşçı', 'korsan',
               'vampir', 'saat', 'bisiklet', 'gitar', 'Noel Baba'}
RECENT_COLORS = 8           # renk tekrarına bakılan son bölüm sayısı


def pick_subject_colors(colors: str, count: int, ptype: str, rng, history: Optional[dict] = None) -> List[str]:
    """Konunun renkleri önem sırasıyla; ikonlarda zemin için nesneyle en iyi ayrışan bir (çok renkte iki) renk ayrılır.
    Silüet ve ikonlarda 4+ renkte siyah göz/iç hat rengi çoğu zaman bulunur (referans: resimlerin %79'u; burada %60,
    yoksa her bölüm siyah-beyaz ağırlıklı görünür). Son bölümlerde sık kullanılan bir konu rengi bazen aynı ailedeki
    yakın tonla değişir; eksik yuvalar sabit sırayla değil, konu renklerinin ailesinden ve son bölümlerde az görülen
    renklerden seçilir: ardışık bölümlerin paletleri birbirine benzemez."""
    from .model import PALETTE, delta_e
    from .subjects import BACKGROUNDS
    history = history if history is not None else {}
    recent = history.setdefault('color_use', [])
    used = collections.Counter(c for cs in recent[-RECENT_COLORS:] for c in cs)
    ok = lambda c, chosen: c not in chosen and all(delta_e(c, o) >= MIN_DELTA_E for o in chosen)

    bg_slots = (2 if count >= 7 else 1) if ptype == 'ikon' else 0
    core = list(colors)
    if ptype in ('ikon', 'siluet') and count >= 4 and '2' not in core and rng.random() < 0.6:
        core.insert(min(2, len(core)), '2')
    chosen: List[str] = []
    for i, c in enumerate(core):
        if len(chosen) >= count - bg_slots: break
        # Ana renk (ilk) konunun kimliği: değişmez. Diğerleri son bölümlerde sıksa yakın tonla değişebilir.
        if i > 0 and c != '2' and used[c] >= RECENT_COLORS // 2 and rng.random() < 0.45:
            alts = [a for a in SIBLINGS.get(c, '') if ok(a, chosen) and used[a] < used[c]]
            if alts: c = min(alts, key=lambda a: used[a] + rng.random())
        if ok(c, chosen): chosen.append(c)
    for _ in range(bg_slots):
        cands = [c for c in BACKGROUNDS if ok(c, chosen)]
        if cands: chosen.append(max(cands, key=lambda c: min(delta_e(c, o) for o in chosen) + rng.random() * 30))
    while len(chosen) < count:
        cands = [c for c in PALETTE if ok(c, chosen)]
        if not cands: break
        family = {a for c in chosen for a in SIBLINGS.get(c, '')}
        # Ana rengin gölge tonu önce: Codex konuyu iki tonla gölgeler; ton yoksa gölgeler siyaha düşer.
        main_shade = set(SIBLINGS.get(chosen[0], '')) if not set(SIBLINGS.get(chosen[0], '')) & set(chosen) else set()
        chosen.append(max(cands, key=lambda c: (5.0 if c in main_shade else 2.0 if c in family else 0)
                          - 0.6 * used[c] + rng.random() * 1.5))
    chosen = _fill(chosen, count)
    recent.append(chosen)
    return chosen


@dataclass
class LevelSpec:
    n: int
    tier: str
    intensity: float
    seed: int
    colors: List[str]
    picture_grid: int
    picture_size: List[int]
    picture: Dict
    board: Dict
    play: Dict
    theme: Dict
    novelty: List[str] = field(default_factory=list)
    reference: Dict = field(default_factory=dict)
    mechanics: List[str] = field(default_factory=list)

    def to_json(self) -> dict:
        return asdict(self)


def tier_of(n: int) -> str:
    if n <= 10: t = FIRST[n - 1]
    elif n <= 20: t = SECOND[n - 11]
    else: t = DECADE[(n - 1) % 10]
    # nefes on'u: her 50 bölümün son on'unda zorlar bir kademe hafif (41–50 hariç, ilk tanıtımlar orada)
    if n > 60 and ((n - 1) // 10) % 5 == 4 and t in (Z, CZ):
        t = {Z: O, CZ: Z}[t]
    return t


def fill_range(n: int, tier: str):
    """Tahta doluluğu hedefi bölümle artar (referans: 6–100 %70–77, 151–300 %78, 301+ %81–85; zor bölümler ~%9 dolu)."""
    lo, hi = (0.50, 0.60) if n <= 5 else (0.68, 0.74) if n <= 50 else (0.71, 0.77) if n <= 100 else (0.74, 0.80) if n <= 300 else (0.78, 0.86)
    extra = {'Kolay': -0.02, 'Orta': 0.0, 'Zor': 0.05, 'Çok Zor': 0.07}[tier] if n > 5 else 0.0
    return round(lo + extra, 2), round(min(0.9, hi + extra), 2)


def picture_type(n: int, tier: str, rng) -> str:
    """1–400 çoğunlukla boş zeminde, konuyu tuvale dolduran detaylı silüet (arka planlı ikon kullanıcıya göre konuyu
    küçültüp zemini büyütüyor); 11–100 arası Kolay bölümlerin küçük bir kısmı mola deseni. 400+ silüet, tam sahne ve
    ikon karışık; desen 100'den sonra nadir."""
    r = rng.random()
    if n <= 5: return 'desen'                  # öğretici bölümler: kodla basit desen (referans ilk bölümleri gibi)
    if n <= 10: return 'siluet'
    if n <= 100: return 'desen' if tier == K and r < 0.1 else 'siluet'
    if n <= 400: return 'siluet' if r < 0.75 else 'ikon' if r < 0.95 else 'desen'
    return 'siluet' if r < 0.50 else 'sahne' if r < 0.85 else 'ikon' if r < 0.97 else 'desen'


def intensity_of(n: int) -> float:
    return max(0.0, min(1.0, math.log(n) / math.log(1000)))


def introduced(n: int, enabled: List[str]) -> List[str]:
    return [k for lvl, k, _ in NOVELTY if lvl <= n and k in enabled]


def lerp(a, b, t): return a + (b - a) * t


def mech_on(key: str, n: int, mech: List[str], inten: float, rng, history: Optional[dict] = None) -> bool:
    """Mekanik bu bölümde kullanılsın mı: tanıtım dalgasında evet, sonra kalıcı oran; MECH_GAP bölümdür görünmediyse zorla.
    history['mech_last'] son kullanıldığı bölümü tutar (künyeler hep 1. bölümden hesaplandığı için aralık güvenilir)."""
    if key not in mech: return False
    intro = next(lvl for lvl, k, _ in NOVELTY if k == key)
    last = (history if history is not None else {}).setdefault('mech_last', {})
    base, top = MECH_RATE[key]
    return n < intro + MECH_BURST[key] or n - last.get(key, intro) >= MECH_GAP[key] or rng.random() < lerp(base, top, inten)


def intro_offset(key: str, n: int) -> int:
    """Bölüm mekaniğin tanıtım dalgasının kaçıncı bölümü (0 tanıtım); dalga dışında -1."""
    intro = next(lvl for lvl, k, _ in NOVELTY if k == key)
    return n - intro if 0 <= n - intro < MECH_BURST[key] else -1


def hole_count(W: int, H: int, inten: float, rng) -> int:
    """Delik sayısı alanın payı olarak: referansta ortanca %14–16 (küçük tahtamızda %8 → %14, yoğunlukla artar); çift sayı
    (simetrik şablonlar). Tanıtımda 2 köşe deliği (referans 10. bölüm)."""
    area = W * H
    if area < 30: return 0
    share = lerp(0.11, 0.17, inten) * rng.uniform(0.8, 1.25)     # referans 21–100: delik hücresi alanın %12–18'i
    return max(2, int(round(area * share / 2)) * 2)


def mechanic_plan(n: int, mech: List[str], inten: float, w: int, h: int, rng, history) -> dict:
    """Tahta mekanikleri (tür araştırmasından çıkan kullanım oranları):
    - holes: delik sayısı; holes_intro tanıtımda yalnız iki köşe.
    - oneway: tek eksenli blok sayısı (1–3, yoğunlukla azalır); oneway_share tanıtım dalgasında blok payı.
    - ice: buzlu blok sayısı; ice_ratio sayacın blok sayısına oranı (referans: başta %30, sonra %50; sayaçlar farklı).
    - keylock: referansta bölümde hep tek kilit; anahtar ve kilit aynı renkte (bizim kuralımız).
    - asteroid: kalıcı asteroit sayısı (1–2, çoğu tek hücre); asteroid_count: sayaçlı asteroit (1–2), asteroid_ratio sayaç
      oranı (referans sandık %41).
    - layer: katmanlı blok sayısı (1–3); layer_share tanıtım dalgasında blok payı.
    Tanıtım bölümünde başka mekanik yok (delik hariç); bölümde en çok max_mechanics(n) mekanik (delik hariç): fazlası,
    tanıtım dalgasındaki ve uzun süredir görünmeyen mekanikler korunarak rastgele düşer."""
    on = {k: mech_on(k, n, mech, inten, rng, history) for k in BOARD_MECHS}
    io = {k: intro_offset(k, n) for k in on}
    last = history.setdefault('mech_last', {}) if history is not None else {}
    # Tanıtım bölümü yalnız yeni mekaniği öğretir (referans 8, 26, 46: tek mekanik); delikli tahta zemin olarak kalabilir.
    new = [k for k in on if io[k] == 0]
    if new:
        for k in on:
            if k not in new and (k != 'holes' or 'holes' in new): on[k] = False
    active = [k for k in on if on[k] and k != 'holes']
    cap = max_mechanics(n)
    if tier_of(n) == K: cap = min(cap, 2)        # kolay bölüm hafif kalır
    if len(active) > cap:
        def keep(k):
            intro = next(lvl for lvl, kk, _ in NOVELTY if kk == k)
            return (io[k] >= 0, n - last.get(k, intro) >= MECH_GAP[k], rng.random())
        for k in sorted(active, key=keep)[:len(active) - cap]: on[k] = False
    for k in on:
        if on[k]: last[k] = n
    plan = dict(holes=hole_count(w, h, inten, rng) if on['holes'] else 0, holes_intro=io['holes'] == 0,
                oneway=0, oneway_share=0.0, ice=0, ice_ratio=(0.0, 0.0), keylock=int(on['keylock']),
                asteroid=0, asteroid_count=0, asteroid_ratio=(0.3, 0.5), layer=0, layer_share=0.0)
    # referans 41–100'de bölüm başına 1,6–2,8 anahtar/kilitli blok: 60'tan sonra bazen iki kilit çifti.
    if on['keylock'] and io['keylock'] != 0 and n > 60 and rng.random() < 0.3: plan['keylock'] = 2
    if plan['holes_intro']: plan['holes'] = 2
    if on['oneway']:
        if io['oneway'] >= 0: plan['oneway_share'] = ONEWAY_INTRO_SHARE[min(io['oneway'], len(ONEWAY_INTRO_SHARE) - 1)]
        plan['oneway'] = 1 + (rng.random() < 0.55 - 0.3 * inten) + (rng.random() < 0.25 - 0.2 * inten)
    if on['ice']:
        plan['ice'] = 2 if io['ice'] == 0 else 2 + (rng.random() < lerp(0.5, 0.3, inten))   # referans: bölümde ~1, açıkken 2–3
        lo = lerp(0.25, 0.4, inten)
        plan['ice_ratio'] = (round(lo, 2), round(lo + 0.2, 2)) if io['ice'] != 0 else (0.3, 0.45)
    if on['asteroid']:
        plan['asteroid'] = 2 if io['asteroid'] == 0 else 1 + (rng.random() < 0.45)
        if plan['holes'] > 2: plan['holes'] -= 2      # asteroit de yer kaplar: küçük tahtada delik biraz azalır
    if on['asteroid_count']:
        plan['asteroid_count'] = 1 if io['asteroid_count'] == 0 else 1 + (rng.random() < 0.35)
        lo = lerp(0.3, 0.4, inten)
        plan['asteroid_ratio'] = (round(lo, 2), round(lo + 0.15, 2))
    if on['layer']:
        if io['layer'] >= 0: plan['layer_share'] = LAYER_INTRO_SHARE[min(io['layer'], len(LAYER_INTRO_SHARE) - 1)]
        plan['layer'] = 1 + (rng.random() < 0.6) + (rng.random() < 0.3 * inten)
    return plan


def build(n: int, enabled: Optional[List[str]] = None, history: Optional[dict] = None) -> LevelSpec:
    enabled = enabled or DEFAULT_ENABLED
    history = history if history is not None else {}
    tier = tier_of(n)
    # Tahta mekaniği tanıtılan bölüm kolaydır (referansta tanıtım bölümleri hep normal zorlukta): oyuncu yeniyi rahat öğrensin.
    if any(lvl == n and k in BOARD_MECHS and k in enabled for lvl, k, _ in NOVELTY): tier = K
    inten = intensity_of(n)
    rng = random.Random(7919 * n)
    mech = introduced(n, enabled)
    news = [desc for lvl, k, desc in NOVELTY if lvl == n and k in enabled]

    # Resim tipi ve konu
    ptype = picture_type(n, tier, rng)
    used = history.setdefault('themes', [])
    if ptype == 'desen':
        from .subjects import PATTERNS
        category = 'Desen'
        done = history.setdefault('patterns', [])
        pool = [t for t in PATTERNS if t not in used[-12:] and t not in done[-12:]] or list(PATTERNS)
        subject = 'iki renkli çerçeve, ortası boş' if n == 1 else rng.choice(pool)
        subject = PATTERN_PICKS.get(n, subject)   # elle seçilen desen (3 ve 4 aynı görünüyordu)
        done.append(subject)
        subject_en, subject_colors = PATTERNS.get(subject, 'a two-colour frame'), ''
    else:
        from .subjects import CATALOG, WEIGHTS
        # İlk 100 bölümde hiçbir konu tekrar etmez (referans ilk 100'de bile mantar, kaktüs, baykuş, ay tekrarlıyor);
        # sonra konu son 60 bölümde tekrar etmez.
        blocked = set(used) if n <= 100 else set(used[-60:])
        # İnce detaylı konular (insan figürü, ince çizgili nesne) 44'lük ızgarada ve 6–7 renkte dağılır: ilk 100'de yok.
        if n <= 100: blocked |= FINE_DETAIL
        def fresh(c): return [t for t in CATALOG[c] if t not in blocked]
        cats = ['Sahne'] if ptype == 'sahne' else [c for c in WEIGHTS if WEIGHTS[c] > 0 and c != history.get('last_category')]
        cats = [c for c in cats if fresh(c)] or cats
        category = rng.choices(cats, weights=[WEIGHTS.get(c, 1) for c in cats])[0]
        pool = fresh(category) or list(CATALOG[category])
        subject = rng.choice(pool)
        subject_en, subject_colors = CATALOG[category][subject]
    history['last_category'] = category
    # Gizli resim ortamı: kategoriye uygun ortamlardan, son 4 bölümde kullanılmayan
    from .subjects import SETTINGS_FOR
    recent = history.setdefault('settings', [])
    options = [k for k in SETTINGS_FOR[category] if k not in recent[-4:]] or SETTINGS_FOR[category]
    setting = rng.choice(options)
    recent.append(setting)

    # Renk sayısı: yenilik takvimine göre açılan renk sayısı ve bölüm aralığı; zorluk +1 (en çok 8).
    avail = sum(1 for c in ('color3', 'color4', 'color5', 'color6', 'color7', 'color8') if c in mech) + 2
    base = next(k for lim, k in ((2, 2), (6, 3), (10, 4), (14, 5), (60, 6), (100, 7), (400, 7), (10 ** 9, 8)) if n <= lim)
    t_colors, t_regions, t_locked = T.TIER_PICTURE[tier]
    count = max(2, min(avail, 8, base + (t_colors if n > 10 else 0)))

    # Izgara: bölüm aralığına göre; 300'den sonra dikey (telefon ekranına da uygun). Desenler kare.
    W, H = next(size for lim, size in ((3, (16, 16)), (10, (20, 20)), (30, (24, 24)), (100, (28, 28)), (300, (32, 32)),
                                        (700, (32, 40)), (10 ** 9, (36, 44))) if n <= lim)
    if ptype == 'desen': H = W
    # Silüet tuvalin ~%60'ını doldurur: aynı boncuk sayısında ızgara ~1,25 kat büyür, konunun detayı sığar.
    if ptype == 'siluet' and n <= 300: W = H = {16: 24, 20: 30, 24: 36, 28: 44, 32: 44}.get(W, W)
    ref = style_reference(n, tier, history.setdefault('refs', []))
    history['refs'].append(ref['n'])
    colors = reference_colors(ref['n'], count) if ptype == 'desen' else pick_subject_colors(subject_colors, count, ptype, rng, history)

    # Resim hedefleri: aralık tablosu + zorluk etkisi (renk +1, bölge ×1,18–1,3, kilitli +0,5–1)
    pr = next(v for lim, v in T.PICTURE_RANGES if n <= lim)
    silhouette = ptype == 'siluet'
    pixels_est = W * H * (0.62 if silhouette else 1.0)
    avg = T.PATTERN_REGION_AVG if ptype == 'desen' else pr['region_avg']
    regions = 2 if n == 1 else max(2, round(pixels_est / avg * t_regions))
    locked_f = pr['locked'] + t_locked
    locked = 0 if n == 1 else int(locked_f) + (1 if rng.random() < locked_f % 1 else 0)
    locked = max(0, min(locked, count - 2))
    picture = dict(kind=ptype, category=category, subject=subject, subject_en=subject_en, size=[W, H],
                   silhouette=silhouette, outline='2' in colors and ptype != 'desen', regions=regions,
                   tiny_max=T.PATTERN_TINY_MAX if ptype == 'desen' else pr['tiny_max'], locked=locked,
                   start_colors_min=min(pr['start_min'], count - locked),
                   top_share_max=max(pr['top_share'], 1 / count + 0.1),
                   balance_max=1 if n == 1 else round(pr['balance'] * (1.5 if tier == CZ else 1)),
                   accents_min=pr['accents'] if ptype != 'desen' else 0,
                   symmetry_min=pr['symmetry'] if ptype in ('ikon', 'siluet') else 0.0,
                   shading=(n > 100 or silhouette) and ptype != 'desen')
    reference = dict(level=ref['n'], tier=ref['tier'], size=f"{ref['W']}x{ref['H']}", colors=ref['colors'],
                     regions=ref['regions'], locked=ref['locked'], noisy_win=ref.get('noisy_win'))

    # Tahta
    bt = T.BOARD[tier]
    # referans 21–40 alan ortalaması 56, 41–100 64–68: tahta bir kademe büyük (ekrana 7×8'e kadar sığar).
    w, h = next(size for lim, size in ((3, (4, 5)), (6, (5, 5)), (13, (5, 6)), (25, (6, 7)), (60, (7, 7)),
                                        (150, (7, 8)), (10 ** 9, (7, 8))) if n <= lim)
    if tier in (Z, CZ) and n > 10: h += 1
    if tier == CZ and n >= 30: w += 1
    ammo_per_cell = 31 if n <= 5 else round(lerp(22, 30, inten))
    shapes = ['1'] + (['2'] if 'block2' in mech else []) + (['L'] if 'shape_L' in mech else []) + (['T'] if 'shape_T' in mech else [])
    # referansta tahta 7×7–8×8'de sabitlenir, doluluk %73'ten %83'e çıkar, katman/yer değiştirme 400. bölüme kadar
    # artıp yataylaşır. Bizde yörünge de zorluk kattığı için daha yumuşak: yoğunlukla katman ve yer değiştirme üst sınırı
    # artar, doluluk +%10'a kadar yükselir.
    grow = max(0.0, (inten - 0.55) / 0.45) if tier != K else 0.0
    layers_r = (bt['layers'][0], round(bt['layers'][1] * (1 + 0.5 * grow)))
    shuffles_r = (bt['shuffles'][0], round(bt['shuffles'][1] * (1 + 0.8 * grow))) if 'reposition' in mech else (0, 0)
    fill_r = fill_range(n, tier)
    board = dict(W=w, H=h, ammo_per_cell=ammo_per_cell, shapes=shapes,
                 first_free=bt['first_free'], layers=layers_r, shuffles=shuffles_r,
                 fill=fill_r,
                 **mechanic_plan(n, mech, inten, w, h, rng, history),
                 gate_widths=[2] + ([1] if 'gate1' in mech else []) + ([3] if 'shape_T' in mech else []))
    play = dict(win_rate=T.WIN_RATE[tier], slot_peak=T.SLOT_PEAK[tier], seconds=T.SECONDS[tier],
                aha=tier in (O, Z, CZ))
    theme = dict(kind=ptype, category=category, subject=subject, setting=setting, reward=f'{subject} – uzay temalı gizli resim')
    return LevelSpec(n=n, tier=tier, intensity=round(inten, 3), seed=7919 * n, colors=colors, picture_grid=W, picture_size=[W, H],
                     picture=picture, board=board, play=play, theme=theme, novelty=news, mechanics=mech,
                     reference=reference)


# Desenli arka planlı bölümler (2026-10-28): Kolay/Orta, en çok 6 renkli silüetler (zemin 2 renk ekler, toplam ≤ 8),
# 6–60 arasına yayılmış. Desenler: yıldızlar, dama 3 px, yatay şerit, dikey şerit, zigzag.
BACKGROUNDS = {
    9: 'yatay şerit', 10: 'yıldızlar', 13: 'dama 3 px', 17: 'zigzag', 18: 'dikey şerit', 24: 'yatay şerit', 27: 'yıldızlar',
    28: 'dama 3 px', 33: 'zigzag', 38: 'dikey şerit', 42: 'yıldızlar', 44: 'yatay şerit', 46: 'dama 3 px', 52: 'zigzag',
    54: 'dikey şerit',
}
# Elle seçilen desenler: 3 sarmal ile 4 labirent iç içe kare olarak aynı görünüyordu → 4 zikzak.
PATTERN_PICKS = {4: 'zikzak'}
# 1–5 desene çevrildi (2026-10-28). Sonraki bölümlerin konuları ve renkleri değişmesin diye tekrar önleme geçmişine eski
# silüet künyeleri yazılır: (konu, kategori, renkler).
LEGACY_HISTORY = {
    1: ('mars aracı', 'Uzay', ['l', '4']), 2: ('sırt çantası', 'Nesne', ['0', '4']), 3: ('kruvasan', 'Yiyecek', ['q', 'v', '9']),
    4: ('kulaklık', 'Nesne', ['x', '0', '2']), 5: ('kalp', 'Bayram', ['7', 'o', 'p']),
}
# Elle düzeltilen künyeler (geçmişe etki etmez): 60 kedi tilkiyle karışıyordu → gri kedi kafası.
SPEC_OVERRIDES = {
    60: dict(subject_en='the head of a cute gray tabby cat with big green eyes, pink nose and ears', colors=['l', 'm', 'a', '2', 'p', '3']),
}


# 2026-10-28: Codex çizimleri iki kez beğenilmeyen bölümlerin konusu değiştirildi (ilk 100'de kullanılmayan, net silüetli konular).
SUBJECT_SWAPS = {
    8: ('Yiyecek', 'elma'), 20: ('Hayvan', 'panda'), 35: ('Yiyecek', 'çilek'), 48: ('Hayvan', 'kelebek'), 67: ('Uzay', 'UFO'),
    83: ('Hayvan', 'aslan'), 88: ('Bayram', 'kardan adam'), 89: ('Yiyecek', 'karpuz dilimi'), 93: ('Bayram', 'hediye kutusu'),
    97: ('Bayram', 'Noel ağacı'), 100: ('Hayvan', 'fil'), 30: ('Yiyecek', 'portakal'),
}


def build_range(first: int, last: int, enabled=None) -> List[LevelSpec]:
    """Künyeler her zaman 1. bölümden hesaplanır (tekrar önleme geçmişi); hangi aralık istenirse istensin aynı bölüm aynı künyeyi alır."""
    history = {'palettes': [], 'themes': []}
    out = []
    for n in range(1, last + 1):
        s = build(n, enabled, history)
        if n in LEGACY_HISTORY:
            subject, category, colors = LEGACY_HISTORY[n]
            history['palettes'].append(colors); history['themes'].append(subject); history['last_category'] = category
        else:
            history['palettes'].append(s.colors); history['themes'].append(s.picture['subject'])
        if n in SUBJECT_SWAPS:
            # Konu değişimi: geçmişe eski konu yazıldı (diğer bölümler değişmez); renkler yeni konunun renklerinden, aynı sayıda.
            from .subjects import CATALOG
            category, subject = SUBJECT_SWAPS[n]
            subject_en, codes = CATALOG[category][subject]
            colors = list(dict.fromkeys(list(codes) + s.colors))[:len(s.colors)]
            s.picture.update(subject=subject, subject_en=subject_en, category=category)
            s.theme.update(subject=subject, category=category, reward=f'{subject} – uzay temalı gizli resim')
            s.colors = colors; s.__dict__['suggested_colors'] = list(colors)
        if n in SPEC_OVERRIDES:
            o = SPEC_OVERRIDES[n]
            if 'subject_en' in o: s.picture['subject_en'] = o['subject_en']
            if 'colors' in o: s.colors = list(o['colors']); s.__dict__['suggested_colors'] = list(o['colors'])
        if n >= first: out.append(s)
    return out


if __name__ == '__main__':
    import sys
    a, b = (int(x) for x in (sys.argv[1:3] if len(sys.argv) > 2 else (1, 30)))
    for s in build_range(a, b):
        print(s.n, s.tier, s.colors, f"{s.picture_grid}²", s.picture['kind'], s.picture['subject'],
              'silüet' if s.picture['silhouette'] else '', f"bölge~{s.picture['regions']} kilit {s.picture['locked']}",
              f"tahta {s.board['W']}x{s.board['H']} şekil {s.board['shapes']}", s.novelty)
