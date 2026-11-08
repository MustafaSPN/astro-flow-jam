"""Piksel resim ölçümleri, künyeye göre doğrulama, görselden ızgaraya aktarma (Codex çıktısı) ve önizleme çizimi."""
import math
from typing import Dict, List, Optional, Tuple

from .model import EMPTY, PALETTE, Picture


# ---------------------------------------------------------------- ölçümler
def front_cells(p: Picture) -> set:
    W, H, out = p.W, p.H, set()
    for y in range(H):
        for xs in (range(W), range(W - 1, -1, -1)):
            for x in xs:
                if p.rows[y][x] != EMPTY: out.add((x, y)); break
    for x in range(W):
        for ys in (range(H), range(H - 1, -1, -1)):
            for y in ys:
                if p.rows[y][x] != EMPTY: out.add((x, y)); break
    return out


def regions(p: Picture) -> List[Tuple[str, int]]:
    W, H = p.W, p.H
    seen = set(); out = []
    for y in range(H):
        for x in range(W):
            c = p.rows[y][x]
            if c == EMPTY or (x, y) in seen: continue
            st = [(x, y)]; seen.add((x, y)); size = 0
            while st:
                cx, cy = st.pop(); size += 1
                for xx, yy in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= xx < W and 0 <= yy < H and (xx, yy) not in seen and p.rows[yy][xx] == c:
                        seen.add((xx, yy)); st.append((xx, yy))
            out.append((c, size))
    return out


def metrics(p: Picture) -> dict:
    counts = p.counts()
    total = sum(counts.values())
    regs = regions(p)
    front = front_cells(p)
    start = {p.rows[y][x] for x, y in front}
    vals = sorted(counts.values())
    W, H = p.W, p.H
    ring = [p.rows[y][x] for y in range(H) for x in range(W) if y in (0, H - 1) or x in (0, W - 1)]
    sym = sum(1 for y in range(H) for x in range(W // 2) if p.rows[y][x] == p.rows[y][W - 1 - x]) / max(1, H * (W // 2))
    return dict(W=W, H=H, pixels=total, counts=counts, colors=len(counts), regions=len(regs),
                tiny=sum(1 for _, s in regs if s <= 2) / max(1, len(regs)),
                top_share=vals[-1] / total if total else 0, balance=vals[-1] / vals[0] if vals else 0,
                start_colors=sorted(start), locked=sorted(set(counts) - start), symmetry=sym,
                silhouette=ring.count(EMPTY) / len(ring), fill=total / (W * H))


def validate(p: Picture, spec) -> Tuple[float, List[str], dict]:
    """Künyeye uygunluk: 0 (mükemmel) ... yüksek (kötü) ceza ve ihlal listesi."""
    m = metrics(p)
    pt = spec.picture
    issues = []; penalty = 0.0
    if sorted(m['counts']) != sorted(spec.colors):
        issues.append(f"renkler {sorted(m['counts'])} ≠ künye {sorted(spec.colors)}"); penalty += 10
    target = pt['regions']
    if target > 2:
        ratio = m['regions'] / target
        if ratio < 0.6 or ratio > (2.5 if pt['silhouette'] else 1.6):   # silüette detay serbest
            issues.append(f"bölge sayısı {m['regions']} (hedef ~{target})"); penalty += 3 * abs(math.log(ratio))
    if m['tiny'] > pt['tiny_max'] + 0.05:
        issues.append(f"küçük detay %{m['tiny'] * 100:.0f} (en çok %{pt['tiny_max'] * 100:.0f})"); penalty += 4 * (m['tiny'] - pt['tiny_max'])
    if len(m['locked']) != pt['locked']:
        issues.append(f"kilitli renk {len(m['locked'])} {m['locked']} (hedef {pt['locked']})"); penalty += 2 * abs(len(m['locked']) - pt['locked'])
    if len(m['start_colors']) < pt['start_colors_min']:
        issues.append(f"başta açık renk {len(m['start_colors'])} (en az {pt['start_colors_min']})"); penalty += 2
    if m['top_share'] > pt['top_share_max'] + 0.03:
        issues.append(f"en büyük renk %{m['top_share'] * 100:.0f} (en çok %{pt['top_share_max'] * 100:.0f})"); penalty += 8 * (m['top_share'] - pt['top_share_max'])
    if m['balance'] > pt['balance_max'] * 1.25:
        issues.append(f"en çok/en az {m['balance']:.1f}× (en çok {pt['balance_max']}×)"); penalty += 0.3 * math.log(m['balance'] / pt['balance_max'])
    small = sum(1 for v in m['counts'].values() if v / max(1, m['pixels']) < 0.05)
    if small < pt.get('accents_min', 0):
        issues.append('aksan rengi yok (göz, yanak, parlama gibi küçük bir renk olmalı)'); penalty += 1
    if m['symmetry'] < pt.get('symmetry_min', 0) - 0.1:
        issues.append(f"sağ-sol simetri %{m['symmetry'] * 100:.0f} (en az %{pt['symmetry_min'] * 100:.0f})"); penalty += 1
    fb = front_black_share(p)
    if '2' in m['counts'] and fb > 0.3:
        issues.append(f'kenardan görünen piksellerin %{fb * 100:.0f} siyah (dış siyah hat; en çok %30)'); penalty += 3 * (fb - 0.3) + 1
    if pt['silhouette'] != (m['silhouette'] > 0.5):
        issues.append('silüet ' + ('olmalı' if pt['silhouette'] else 'olmamalı')); penalty += 1
    return penalty, issues, m


# ---------------------------------------------------------------- görselden ızgaraya
def _srgb_to_lab(rgb):
    import numpy as np
    rgb = np.asarray(rgb, dtype=float)
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def detect_grid(img, lo=12, hi=48) -> Tuple[int, int]:
    """Düz renkli piksel görselinde hücre sayısını bul: hücre ortalarındaki renk sapmasının en az olduğu ızgara."""
    import numpy as np
    a = np.asarray(img.convert('RGB'), dtype=float) / 255
    H, W = a.shape[:2]
    best = None
    for n in range(lo, hi + 1):
        cw, ch = W / n, H / n
        spread = 0.0
        for y in range(n):
            for x in range(n):
                patch = a[int((y + 0.3) * ch):int((y + 0.7) * ch) + 1, int((x + 0.3) * cw):int((x + 0.7) * cw) + 1].reshape(-1, 3)
                spread += patch.std(0).sum()
        spread /= n * n
        # küçük ızgara ayrımı: eşit sapmada daha küçük n tercih edilir (aynı resmin katları)
        if best is None or spread < best[0] - 0.002:
            best = (spread, n)
    return best[1], best[1]


def image_to_picture(img, colors: List[str], n: Optional[int] = None, empty_alpha=128) -> Picture:
    """Görseli n×n ızgaraya oturt, her hücreyi izin verilen renklerden en yakınına eşle (Lab). Saydam hücre boş."""
    import numpy as np
    rgba = np.asarray(img.convert('RGBA'), dtype=float) / 255
    H, W = rgba.shape[:2]
    if n is None: n, _ = detect_grid(img)
    cw, ch = W / n, H / n
    pal = _srgb_to_lab(np.array([PALETTE[c][1] for c in colors]))
    rows = []
    for y in range(n):
        row = ''
        for x in range(n):
            patch = rgba[int((y + 0.3) * ch):int((y + 0.7) * ch) + 1, int((x + 0.3) * cw):int((x + 0.7) * cw) + 1].reshape(-1, 4)
            if np.median(patch[:, 3]) < empty_alpha / 255: row += EMPTY; continue
            lab = _srgb_to_lab(np.median(patch[:, :3], axis=0))
            row += colors[int(np.linalg.norm(pal - lab, axis=1).argmin())]
        rows.append(row)
    return Picture(rows)


def image_to_picture_majority(img, colors: List[str], n: int, rows_n: Optional[int] = None, empty_alpha=128) -> Picture:
    """Görseli n×n ızgaraya çoğunluk oyuyla aktar: her piksel önce izin verilen en yakın renge (Lab) eşlenir, sonra her
    hedef hücrede en sık renk alınır (saydam piksel çoğunluktaysa hücre boş). Yapay zeka görselleri düzgün bir ızgaraya
    oturmadığı için ızgara aramaktan daha sağlam. n sütun, rows_n satır (verilmezse kare)."""
    import numpy as np
    cols, rows_n = n, rows_n or n
    small = img.convert('RGBA')
    scale = max(1, min(small.size) // (max(cols, rows_n) * 12))                  # hız için küçült (hücre başı ~12 piksel kalsın)
    if scale > 1: small = small.resize((small.size[0] // scale, small.size[1] // scale), resample=0)
    a = np.asarray(small, dtype=float) / 255
    H, W = a.shape[:2]
    pal = _srgb_to_lab(np.array([PALETTE[c][1] for c in colors]))
    lab = _srgb_to_lab(a[..., :3].reshape(-1, 3))
    idx = np.linalg.norm(lab[:, None, :] - pal[None, :, :], axis=-1).argmin(1).reshape(H, W)
    idx[a[..., 3] < empty_alpha / 255] = -1
    rows = []
    for y in range(rows_n):
        row = ''
        for x in range(cols):
            cell = idx[int(y * H / rows_n):int((y + 1) * H / rows_n), int(x * W / cols):int((x + 1) * W / cols)].ravel()
            vals, cnt = np.unique(cell, return_counts=True)
            v = int(vals[cnt.argmax()])
            row += EMPTY if v < 0 else colors[v]
        rows.append(row)
    return Picture(rows)


def _period(signal, lo: float, hi: float) -> Tuple[float, float]:
    """Kenar sinyalinin tekrar aralığı: her aday aralık için kenarların aynı fazda toplanma gücü (0..1)."""
    import numpy as np
    x = np.arange(len(signal))
    total = max(1.0, signal.sum())
    best = (0.0, lo)
    for s in np.arange(lo, hi, 0.05):
        score = abs((signal * np.exp(2j * np.pi * x / s)).sum()) / total
        if score > best[0]: best = (score, s)
    return best[1], best[0]


def _phase(signal, period: float) -> float:
    """Hücre sınırlarının konumu (0..period): kenarlar x ≡ faz (mod aralık) çevresinde toplanır. np.diff sınırı solundaki
    pikselde işaretlediği için +0,5."""
    import numpy as np
    x = np.arange(len(signal))
    z = (signal * np.exp(2j * np.pi * x / period)).sum()
    return (np.angle(z) / (2 * np.pi) * period + 0.5) % period


def grid_phase(img, cols: int, rows: int) -> Tuple[float, float]:
    """Bulunan ızgaranın faz kayması (piksel): hücreler 0'dan değil bu kaymadan başlar."""
    import numpy as np
    a = np.asarray(img.convert('RGB'), dtype=float)
    H, W = a.shape[:2]
    sx = (np.abs(np.diff(a, axis=1)).sum(2) > 60).sum(0).astype(float)
    sy = (np.abs(np.diff(a, axis=0)).sum(2) > 60).sum(1).astype(float)
    return _phase(sx, W / cols), _phase(sy, H / rows)


def detect_cells(img) -> Tuple[int, int, float]:
    """Yapay zekanın kendi çizdiği piksel ızgarasını bul: renk kenarları hücre sınırlarında düzenli aralıkla tekrar eder.
    (sütun, satır, güç) döndürür; güç ~0,5 üstünde ızgara güvenilir."""
    import numpy as np
    a = np.asarray(img.convert('RGB'), dtype=float)
    H, W = a.shape[:2]
    sx = (np.abs(np.diff(a, axis=1)).sum(2) > 60).sum(0).astype(float)
    sy = (np.abs(np.diff(a, axis=0)).sum(2) > 60).sum(1).astype(float)
    px, qx = _period(sx, W / 96, W / 10)
    py, qy = _period(sy, H / 96, H / 10)
    # Codex'in pikselleri karedir: iki eksen ayrı tahmin edilince biri yanılır ve resim basık/uzun çıkar. Güçlü eksenin
    # aralığı diğerinde de denenir; ortak aralık iki eksende de yeterince güçlüyse onu kullan.
    p = px if qx >= qy else py
    q_common = min(_strength(sx, p), _strength(sy, p))
    if q_common >= 0.6 * max(qx, qy):
        return round(W / p), round(H / p), q_common
    return round(W / px), round(H / py), min(qx, qy)


def _strength(signal, period: float) -> float:
    import numpy as np
    x = np.arange(len(signal))
    return abs((signal * np.exp(2j * np.pi * x / period)).sum()) / max(1.0, signal.sum())


def cell_labs(img, cols: int, rows: int, empty_alpha=128, phase=(0.0, 0.0)):
    """Görselin kendi ızgarasındaki her hücrenin Lab rengi (hücre ortasının ortancası) ya da None (boş). Hücreler 'phase'
    kadar kaymış ızgarada okunur; kenardan taşan yarım hücreler (boş zemin) atlanır."""
    import numpy as np
    a = np.asarray(img.convert('RGBA'), dtype=float) / 255
    H, W = a.shape[:2]
    cw, ch = W / cols, H / rows
    ox, oy = phase[0] % cw, phase[1] % ch
    if ox > cw / 2: ox -= cw                                  # en yakın sınıra hizala: hücre sayısı değişmesin
    if oy > ch / 2: oy -= ch
    out = []
    for y in range(rows):
        row = []
        for x in range(cols):
            y0, y1 = max(0, int(oy + (y + 0.3) * ch)), min(H, int(oy + (y + 0.7) * ch) + 1)
            x0, x1 = max(0, int(ox + (x + 0.3) * cw)), min(W, int(ox + (x + 0.7) * cw) + 1)
            if y1 <= y0 or x1 <= x0: row.append(None); continue
            patch = a[y0:y1, x0:x1].reshape(-1, 4)
            if np.median(patch[:, 3]) < empty_alpha / 255: row.append(None); continue
            row.append(_srgb_to_lab(np.median(patch[:, :3], axis=0)))
        out.append(row)
    return out


def _costs(labs, codes):
    """Her dolu hücrenin her palet rengine uzaklığı (siyaha ek BLACK_BIAS): (hücre sayısı × renk sayısı)."""
    import numpy as np
    flat = np.array([lab for row in labs for lab in row if lab is not None])
    pal = _srgb_to_lab(np.array([PALETTE[c][1] for c in codes]))
    bias = np.array([BLACK_BIAS if c == '2' else 0.0 for c in codes])
    return np.linalg.norm(flat[:, None, :] - pal[None, :, :], axis=2) + bias


PREFER_MARGIN = 4.0   # Lab ΔE: künyenin (son bölümlerde az kullanılmış) renkleri, çizime belirgin şekilde daha uygun
                      # bir renk yoksa seçilir: renkler bölümden bölüme dengeli dağılır


def has_dark(labs, share=0.004) -> bool:
    """Çizimde siyaha yakın (Lab L < 22) pikseller en az 'share' oranında var mı?"""
    import numpy as np
    a = np.array([lab for row in labs for lab in row if lab is not None])
    return a.size > 0 and float((a[:, 0] < 22).mean()) >= share


def choose_palette(labs, count: int, prefer=(), min_delta=22.0, keep=()) -> List[str]:
    """Çizime en uygun 'count' palet rengi: aktarım hatasını en çok azaltan renk açgözlü eklenir, sonra takas turlarıyla
    iyileştirilir. Seçilen renkler birbirinden en az 'min_delta' (Lab ΔE) uzak olur (tahtada bir bakışta ayırt edilir).
    Künyenin önerdiği renkler hafif önceliklidir (aynı hatada onlar seçilir). Renkler resimdeki kullanım sırasıyla döner."""
    import numpy as np
    from .model import delta_e
    codes = list(PALETTE)
    cost = _costs(labs, codes)
    cost = cost + np.array([0.0 if c in prefer else PREFER_MARGIN for c in codes])[None, :]
    def err(sel):
        return cost[:, sel].min(1).sum() if sel else float('inf')
    def ok(i, sel):
        return all(delta_e(codes[i], codes[j]) >= min_delta for j in sel)
    # keep: çizimde birkaç pikselle de olsa bulunan zorunlu renkler (siyah göz/burun): baştan seçilir, takasta çıkmaz.
    sel: List[int] = [codes.index(c) for c in keep if c in codes][:count]
    fixed = set(sel)
    while len(sel) < count:
        cands = [i for i in range(len(codes)) if i not in sel and ok(i, sel)]
        if not cands: break
        sel.append(min(cands, key=lambda i: err(sel + [i])))
    for _ in range(3):                                         # takas: tek rengi değiştirip hatayı düşür
        improved = False
        for k in range(len(sel)):
            if sel[k] in fixed: continue
            rest = sel[:k] + sel[k + 1:]
            best = min((i for i in range(len(codes)) if i not in rest and ok(i, rest)), key=lambda i: err(rest + [i]))
            if err(rest + [best]) < err(sel) - 1e-6: sel = rest + [best]; improved = True
        if not improved: break
    use = np.bincount(cost[:, sel].argmin(1), minlength=len(sel))
    return [codes[sel[k]] for k in np.argsort(-use)]


def labs_to_picture(labs, colors: List[str]) -> Picture:
    import numpy as np
    pal = _srgb_to_lab(np.array([PALETTE[c][1] for c in colors]))
    bias = np.array([BLACK_BIAS if c == '2' else 0.0 for c in colors])
    return Picture([''.join(EMPTY if lab is None else colors[int((np.linalg.norm(pal - lab, axis=1) + bias).argmin())]
                            for lab in row) for row in labs])


def image_to_picture_native(img, colors: List[str], cols: int, rows: int, empty_alpha=128, phase=(0.0, 0.0)) -> Picture:
    """Görseli kendi ızgarasında oku: her hücre izin verilen en yakın renge (Lab; siyaha ek uzaklık)."""
    return labs_to_picture(cell_labs(img, cols, rows, empty_alpha, phase), colors)


BLACK_BIAS = 6     # Lab ΔE: Codex'in koyu gölge tonları siyaha değil kendi renginin tonuna düşsün (siyah yalnız göz/hat)


def downsample(p: Picture, k: int) -> Picture:
    """k×k blokları tek hücreye indir (çoğunluk; boş çoğunluktaysa boş). Blok hizası k×k olası kaymadan, blokların en
    tekdüze olduğu (Codex'in kaba pikselleriyle çakışan) kayma seçilir: şekiller kaymaz, ince detay bölünmez."""
    from collections import Counter
    total = Counter(c for r in p.rows for c in r if c != EMPTY)
    rare = lambda c: 0 if c == EMPTY else 1 / (1 + total[c])       # eşitlikte nadir renk (göz, aksan) kazanır
    def at(x, y):
        return p.rows[y][x] if 0 <= x < p.W and 0 <= y < p.H else EMPTY
    best = None
    for dy in range(k):
        for dx in range(k):
            rows, mixed = [], 0
            for y in range(-dy, p.H, k):
                row = ''
                for x in range(-dx, p.W, k):
                    cells = [at(xx, yy) for yy in range(y, y + k) for xx in range(x, x + k)]
                    cnt = Counter(cells)
                    c = max(cnt, key=lambda v: cnt[v] + rare(v)); m = cnt[c]
                    mixed += k * k - m
                    row += c
                rows.append(row)
            if best is None or mixed < best[0]: best = (mixed, rows)
    return trim(Picture(best[1]))


def resample(p: Picture, W: int, H: int) -> Picture:
    """Kesirli oranla küçült: her hedef hücre kapladığı kaynak hücrelerin alan ağırlıklı oyuyla boyanır. Boş hücre ancak
    alanın yarısından fazlasıysa kazanır (silüetin kenarı aşınmaz); eşitliğe yakın durumda nadir renk (göz, aksan) kazanır."""
    from collections import Counter, defaultdict
    total = Counter(c for r in p.rows for c in r if c != EMPTY)
    sx, sy = p.W / W, p.H / H
    rows = []
    for ty in range(H):
        row = ''
        y0, y1 = ty * sy, (ty + 1) * sy
        for tx in range(W):
            x0, x1 = tx * sx, (tx + 1) * sx
            w = defaultdict(float)
            for yy in range(int(y0), min(p.H, int(y1 - 1e-9) + 1)):
                hy = min(y1, yy + 1) - max(y0, yy)
                for xx in range(int(x0), min(p.W, int(x1 - 1e-9) + 1)):
                    w[p.rows[yy][xx]] += hy * (min(x1, xx + 1) - max(x0, xx))
            area = sum(w.values())
            if w.get(EMPTY, 0) > area / 2: row += EMPTY; continue
            w.pop(EMPTY, None)
            row += max(w, key=lambda c: w[c] * (1 + 0.35 / (1 + total[c] / 40)))
        rows.append(row)
    return Picture(rows)


def fit(p: Picture, W: int, H: int) -> Picture:
    """Boş kenarları at; konu hedef kutuya sığmıyorsa oranı korunarak kesirli küçült."""
    p = trim(p, keep=0)
    f = min(W / p.W, H / p.H)
    if f < 0.97: p = resample(p, max(1, round(p.W * f)), max(1, round(p.H * f)))
    return p


def trim(p: Picture, keep: int = 1) -> Picture:
    """Tamamen boş kenar satır/sütunlarını at (en çok 'keep' boş kenar kalır)."""
    rows = p.rows
    filled_r = [i for i, r in enumerate(rows) if r.strip(EMPTY)]
    if not filled_r: return p
    filled_c = [x for x in range(p.W) if any(r[x] != EMPTY for r in rows)]
    y0, y1 = max(0, filled_r[0] - keep), min(p.H, filled_r[-1] + 1 + keep)
    x0, x1 = max(0, filled_c[0] - keep), min(p.W, filled_c[-1] + 1 + keep)
    return Picture([r[x0:x1] for r in rows[y0:y1]])


def front_black_share(p: Picture, black: str = '2') -> float:
    """Kenardan görünen (oyun başında vurulabilen) piksellerin siyah payı."""
    f = front_cells(p)
    return sum(1 for x, y in f if p.rows[y][x] == black) / max(1, len(f))


def open_outer_outline(p: Picture, black: str = '2', limit: float = 0.3, passes: int = 2) -> Picture:
    """Dış siyah hattı aç: kenardan görünen siyah pikseller bitişik iç rengin rengine boyanır (şekil küçülmez).
    Referansta siyah içeride (göz, iç çizgi) kullanılır; kenardan görünen piksellerin ortanca %11'i siyahtır."""
    for _ in range(passes):
        if front_black_share(p, black) <= limit: break
        out = [list(r) for r in p.rows]
        for x, y in front_cells(p):
            if p.rows[y][x] != black: continue
            near = [p.rows[yy][xx] for yy in range(y - 1, y + 2) for xx in range(x - 1, x + 2)
                    if 0 <= xx < p.W and 0 <= yy < p.H and p.rows[yy][xx] not in (EMPTY, black)]
            if near: out[y][x] = max(set(near), key=near.count)
        p = Picture([''.join(r) for r in out])
    return p


def cleanup(p: Picture) -> Picture:
    """Tek başına kalan pikseli (4 komşusundan hiçbiri aynı renk değil) en sık komşu rengine çevirir."""
    W, H = p.W, p.H
    out = [list(r) for r in p.rows]
    for y in range(H):
        for x in range(W):
            c = p.rows[y][x]
            if c == EMPTY: continue
            four = [p.rows[yy][xx] for xx, yy in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)) if 0 <= xx < W and 0 <= yy < H]
            if c in four: continue
            eight = [p.rows[yy][xx] for yy in range(y - 1, y + 2) for xx in range(x - 1, x + 2)
                     if (xx, yy) != (x, y) and 0 <= xx < W and 0 <= yy < H and p.rows[yy][xx] != EMPTY]
            if eight: out[y][x] = max(set(eight), key=eight.count)
    return Picture([''.join(r) for r in out])


def denoise(p: Picture, black: str = '2', passes: int = 2) -> Picture:
    """Codex çiziminin palete aktarımında oluşan kumlanmayı temizler: tek başına kalan piksel (4 komşusundan hiçbiri aynı
    renk değil) ve tamamı tek bir renkle çevrili 2 piksellik lekeler çevrenin rengine katılır. İç kısımdaki siyah tek
    pikseller (göz, ağız gibi detaylar) korunur; kenardan görünen siyah kumlanma temizlenir."""
    W, H = p.W, p.H
    for _ in range(passes):
        rows = [list(r) for r in p.rows]
        front = front_cells(p)
        changed = False
        for y in range(H):
            for x in range(W):
                c = p.rows[y][x]
                if c == EMPTY: continue
                four = [p.rows[yy][xx] for xx, yy in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)) if 0 <= xx < W and 0 <= yy < H]
                if c in four: continue
                if c == black and (x, y) not in front: continue
                eight = [p.rows[yy][xx] for yy in range(y - 1, y + 2) for xx in range(x - 1, x + 2)
                         if (xx, yy) != (x, y) and 0 <= xx < W and 0 <= yy < H and p.rows[yy][xx] != EMPTY]
                if eight:
                    rows[y][x] = max(set(eight), key=eight.count); changed = True
        p = Picture([''.join(r) for r in rows])
        # 2 piksellik lekeler: çevresi tek renkse o renge
        rows = [list(r) for r in p.rows]
        for y in range(H):
            for x in range(W):
                c = p.rows[y][x]
                if c == EMPTY or c == black: continue
                for dx, dy in ((1, 0), (0, 1)):
                    x2, y2 = x + dx, y + dy
                    if x2 >= W or y2 >= H or p.rows[y2][x2] != c: continue
                    pair = {(x, y), (x2, y2)}
                    around = [p.rows[yy][xx] for px, py in pair for xx, yy in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1))
                              if 0 <= xx < W and 0 <= yy < H and (xx, yy) not in pair]
                    if c in around or len(set(around)) != 1 or around[0] == EMPTY: continue
                    for px, py in pair: rows[py][px] = around[0]
                    changed = True
        p = Picture([''.join(r) for r in rows])
        if not changed: break
    return p


# ---------------------------------------------------------------- çizim
def render(p: Picture, cell=16):
    from PIL import Image, ImageDraw
    pad = 6
    im = Image.new('RGB', (p.W * cell + 2 * pad, p.H * cell + 2 * pad), (52, 58, 82))
    d = ImageDraw.Draw(im)
    for y in range(p.H):
        for x in range(p.W):
            c = p.rows[y][x]
            if c == EMPTY: continue
            r, g, b = PALETTE[c][1]
            base = (int(r * 255), int(g * 255), int(b * 255))
            dark = tuple(int(v * 0.7) for v in base)
            light = tuple(min(255, int(v * 1.12 + 18)) for v in base)
            x0, y0 = pad + x * cell + 1, pad + y * cell + 1
            d.rounded_rectangle([x0, y0, x0 + cell - 2, y0 + cell - 2], radius=cell * 0.22, fill=dark)
            d.rounded_rectangle([x0 + 1, y0 + 1, x0 + cell - 3, y0 + cell - 5], radius=cell * 0.2, fill=base)
            d.rounded_rectangle([x0 + 3, y0 + 2, x0 + cell - 5, y0 + cell * 0.36], radius=cell * 0.15, fill=light)
    return im


# ---------------------------------------------------------------- desenli arka plan (2026-10-28, seçili bölümler)
import colorsys as _cs

def _star(x, y):
    px, py = x % 8, y % 8
    if ((x // 8) + (y // 8)) % 2: px, py = (x + 4) % 8, (y + 4) % 8
    return (px == 3 and py in (2, 3, 4)) or (py == 3 and px in (2, 4))

BACKGROUND_PATTERNS = {
    'yıldızlar': _star,
    'dama 3 px': lambda x, y: ((x // 3) + (y // 3)) % 2,
    'yatay şerit': lambda x, y: (y // 2) % 2,
    'dikey şerit': lambda x, y: (x // 2) % 2,
    'zigzag': lambda x, y: ((y + abs((x % 8) - 4)) // 2) % 2,
}
# Zemin renk çiftleri (açık, koyu aynı ton).
BACKGROUND_PAIRS = [('6', 'n'), ('0', '1'), ('8', 't'), ('9', 'q'), ('5', 'o'), ('3', 'c'), ('e', 'h'), ('d', 'c')]


def background_pair(colors, avoid=()):
    """Resimde olmayan, tonu resmin doygun renklerinden en uzak zemin çifti (avoid: yakın bölümlerde kullanılanlar)."""
    def hue(c):
        r, g, b = PALETTE[c][1]; return _cs.rgb_to_hsv(r, g, b)
    sat = [hue(c)[0] for c in colors if hue(c)[1] > 0.25]
    best = None
    for a, b in BACKGROUND_PAIRS:
        if a in colors or b in colors: continue
        h = hue(a)[0]
        dist = min((min(abs(h - x), 1 - abs(h - x)) for x in sat), default=0.5) - (0.2 if (a, b) in avoid else 0)
        if best is None or dist > best[0]: best = (dist, a, b)
    return (best[1], best[2]) if best else BACKGROUND_PAIRS[0]


def with_background(p, kind, pair):
    """Silüet kutusunu her yandan 1 piksel genişletir, boş hücreleri desenle doldurur."""
    rows = p.rows
    ys = [y for y, r in enumerate(rows) if r.strip(EMPTY)]
    xs = [x for x in range(len(rows[0])) if any(r[x] != EMPTY for r in rows)]
    crop = [r[min(xs):max(xs) + 1] for r in rows[min(ys):max(ys) + 1]]
    H, W = len(crop) + 2, len(crop[0]) + 2
    f = BACKGROUND_PATTERNS[kind]
    a, b = pair
    out = []
    for y in range(H):
        row = []
        for x in range(W):
            c = crop[y - 1][x - 1] if 0 < y < H - 1 and 0 < x < W - 1 else EMPTY
            row.append(c if c != EMPTY else (b if f(x, y) else a))
        out.append(''.join(row))
    return Picture(out)
