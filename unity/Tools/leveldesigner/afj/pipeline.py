"""Uçtan uca bölüm üretimi: künye → resim → tahta → simülasyon → çıktı (bölüm JSON'u, görseller, önizleme sayfası).

Kullanım:
  python3 -m afj.pipeline 1 20                # 1–20. bölümler, Codex ile (3 Codex, 8 tahta paralel)
  python3 -m afj.pipeline 1 20 --no-codex     # Codex yerine yalnız kural tabanlı resim
  python3 -m afj.pipeline 5 5 --force         # 5. bölümü baştan üret (önbelleği yok say)
  python3 -m afj.pipeline --samples           # her resim tipinden bir örnek: piksel resim + gizli resim
Çıktı: out/levels/NNN/ (picture.json, hidden.png, level.json, card.png, ham Codex görselleri), out/onizleme.html
"""
import argparse
import math
import base64
import io
import json
import os
import time

from . import board_gen, codex_art, patterns, picture as pic, render
from .model import COLOR_NAMES, Picture
from .spec import build_range
from .unity_io import write_level_json

ROOT = os.path.join(os.path.dirname(__file__), '..', 'out')


def make_picture(spec, d, use_codex, attempts, force, log):
    cache = os.path.join(d, 'picture.json')
    if os.path.exists(cache) and not force:
        data = json.load(open(cache))
        # Codex çizemediği için yedek desene düşen resim, Codex açıkken yeniden denenir (ör. kullanım sınırı kalkınca).
        fallback = data.get('source', '').startswith('desen (') and spec.picture['kind'] != 'desen' and use_codex
        if data.get('suggested', data.get('colors')) == spec.colors and data.get('size') == spec.picture_size and data.get('subject') == spec.picture['subject'] \
                and not fallback:
            log('  resim önbellekten'); return Picture(data['rows']), data['source']
    source = 'desen'
    if spec.picture['kind'] == 'desen':
        p, penalty, issues = patterns.generate(spec)
    elif use_codex:
        p, penalty, issues, _ = codex_art.generate(spec, d, attempts=attempts, log=log)
        source = 'codex'
        # Nesne/karakter/sahne bölümüne desen konmaz: Codex bir çizim ürettiyse en iyisi kullanılır (kullanmadığı renkler
        # bölümden düşer; roketler resimdeki gerçek renklerden üretilir). Yalnız hiç çizim yoksa desen yedeği.
        if p is None:
            log('  Codex çizim üretmedi, desen yedeği kullanılıyor')
            p, penalty, issues = patterns.generate(spec); source = 'desen (yedek)'
    else:
        p, penalty, issues = patterns.generate(spec); source = 'desen (Codex kapalı)'
    json.dump(dict(rows=p.rows, colors=spec.colors, suggested=spec.__dict__.get('suggested_colors', spec.colors), size=spec.picture_size, subject=spec.picture['subject'], source=source,
                   penalty=penalty, issues=issues),
              open(cache, 'w'), ensure_ascii=False, indent=1)
    return p, source


def make_hidden(spec, d, use_codex, force, log):
    """Konuya uygun uzay temalı gizli resim (hidden.png); bir kez üretilir, konu değişmedikçe yeniden üretilmez."""
    path = os.path.join(d, 'hidden.png')
    meta = os.path.join(d, 'hidden.json')
    if os.path.exists(path) and os.path.exists(meta) and not force and json.load(open(meta)).get('subject') == spec.picture['subject']:
        log('  gizli resim önbellekten'); return path
    if not use_codex: return None
    out = codex_art.generate_hidden(spec, d, log=log)
    if out: json.dump(dict(subject=spec.picture['subject']), open(meta, 'w'), ensure_ascii=False)
    return out


def _level_dir(n): return os.path.join(ROOT, 'levels', f'{n:03d}')


def _file_log(n):
    path = os.path.join(_level_dir(n), 'log.txt')
    def log(msg):
        with open(path, 'a') as f: f.write(msg + '\n')
    return log


def _art_job(args):
    """1. aşama (paralel): piksel resim + gizli resim. Codex oturumları ayrı klasörlere yazdığı için aynı anda çalışabilir."""
    spec, use_codex, attempts, force, with_hidden = args
    d = _level_dir(spec.n); log = _file_log(spec.n)
    try:
        picture, source = make_picture(spec, d, use_codex, attempts, force, log)
        # Gizli resim istenmezse Codex'e çizdirilmez (sonra konu listesinden üretilecek); konusu tutan eski çizim varsa kullanılır.
        hidden = make_hidden(spec, d, use_codex and with_hidden, force and with_hidden, log)
        return spec.n, picture.rows, source, hidden, None
    except Exception as e:                       # bir bölümdeki hata akışı durdurmaz
        return spec.n, None, None, None, repr(e)


def _board_job(args):
    """2. aşama (paralel): tahta araması + simülasyon."""
    spec, rows = args
    try:
        res = board_gen.generate(spec, Picture(rows), log=_file_log(spec.n))
        return spec.n, res, None
    except Exception as e:
        return spec.n, None, repr(e)


SHRINK_SIZE = 30
# 2026-10-28: Codex çizimleri küçültülmeden kullanılır (30'a indirmek detayları siliyordu; zorluk artık tahta doluluğundan
# ayarlanıyor). --shrink ile tek tek küçültme hâlâ mümkün.
SHRUNK = set()


def shrink_spec(spec):
    """Resmi küçült (44 → 30): çok ayrıntılı resim (çok küçük renk bölgesi, iç içe renkler) yörüngeyi tıkar, usta bot
    bile çözemez. Codex'in mevcut çizimi küçük ızgarada yeniden okunur (Codex çağrılmaz); bölge hedefi alanla ölçeklenir."""
    W, H = spec.picture_size
    if max(W, H) <= SHRINK_SIZE or spec.picture['kind'] == 'desen': return spec
    k = (SHRINK_SIZE / max(W, H)) ** 2
    spec.picture_size = [SHRINK_SIZE, SHRINK_SIZE]
    spec.picture['size'] = [SHRINK_SIZE, SHRINK_SIZE]
    spec.picture['regions'] = int(round(spec.picture['regions'] * k))
    return spec


def build(first, last, use_codex=True, attempts=2, force=False, log=print, art_workers=3, board_workers=8, art_only=False,
          with_hidden=True, only=None, shrink=()):
    """Bölümleri üç aşamada üretir: resimler (Codex, art_workers paralel), tahtalar (board_workers paralel), çıktılar."""
    from concurrent.futures import ProcessPoolExecutor
    # Süreç havuzu standart giriş açıkken takılabiliyor (macOS spawn): giriş /dev/null'a bağlanır.
    os.dup2(os.open(os.devnull, os.O_RDONLY), 0)
    specs = build_range(first, last)
    if only: specs = [s for s in specs if s.n in only]
    for s in specs:
        if s.n in shrink or s.n in SHRUNK: shrink_spec(s)
    os.makedirs(os.path.join(ROOT, 'levels'), exist_ok=True)
    for spec in specs:
        os.makedirs(_level_dir(spec.n), exist_ok=True)
        json.dump(spec.to_json(), open(os.path.join(_level_dir(spec.n), 'spec.json'), 'w'), ensure_ascii=False, indent=1)
    t0 = time.time()
    art = {}
    with ProcessPoolExecutor(max_workers=max(1, art_workers)) as ex:
        for n, rows, source, hidden, err in ex.map(_art_job, [(s, use_codex, attempts, force, with_hidden) for s in specs]):
            art[n] = (rows, source, hidden)
            log(f'{n}. bölüm resim: ' + (f'HATA {err}' if err else f'{source}, gizli resim {"var" if hidden else "yok"}') +
                f' ({time.time() - t0:.0f} sn)')
    # Seçili bölümlere desenli arka plan (silüet kutusu +1 piksel); yakın bölümlerde aynı zemin rengi tekrar etmez.
    from .spec import BACKGROUNDS
    recent = []
    for s in specs:
        if s.n not in BACKGROUNDS or not art.get(s.n, (None,))[0]: continue
        rows, source, hidden = art[s.n]
        p = Picture(rows)
        pair = pic.background_pair({c for r in rows for c in r if c != '.'}, avoid=recent[-3:])
        recent.append(pair)
        art[s.n] = (pic.with_background(p, BACKGROUNDS[s.n], pair).rows, f'{source} + {BACKGROUNDS[s.n]} arka plan', hidden)
    write_subjects(specs, art)
    if art_only: return art
    by_n = {s.n: s for s in specs}
    todo = [(s, art[s.n][0]) for s in specs if art[s.n][0]]
    rows_out = []
    # Her tahta çıktığı anda diske yazılır: uzun üretim yarıda kesilirse biten bölümler kaybolmaz.
    with ProcessPoolExecutor(max_workers=max(1, board_workers)) as ex:
        for n, res, err in ex.map(_board_job, todo):
            if err or not res:
                log(f'{n}. bölüm tahta: {"HATA " + err if err else "üretilemedi"}')
                continue
            rows, source, hidden = art[n]
            total, board, bm, ev = res
            rows_out.append(write_level(by_n[n], Picture(rows), source, hidden, board, bm, ev))
            log(f'{n}. bölüm tahta: kazanma %{ev["win_rate"] * 100:.0f} ({time.time() - t0:.0f} sn)')
    write_preview(rows_out)
    log(f'{len(rows_out)}/{len(specs)} bölüm yazıldı ({time.time() - t0:.0f} sn)')
    return rows_out


def write_subjects(specs, art):
    """Her bölümün piksel resminde ne olduğu (gizli resimler sonra bu listeden üretilecek): out/konular.json ve .txt."""
    path = os.path.join(ROOT, 'konular.json')
    data = json.load(open(path)) if os.path.exists(path) else {}
    for s in specs:
        if s.n not in art or not art[s.n][0]: continue
        data[str(s.n)] = dict(subject=s.picture['subject'], category=s.picture.get('category', ''), kind=s.picture['kind'],
                              english=s.picture.get('subject_en', ''), source=art[s.n][1],
                              setting=s.theme.get('setting', ''))
    data = dict(sorted(data.items(), key=lambda kv: int(kv[0])))
    json.dump(data, open(path, 'w'), ensure_ascii=False, indent=1)
    with open(os.path.join(ROOT, 'konular.txt'), 'w') as f:
        for k, v in data.items():
            f.write(f"{int(k):3d}. bölüm: {v['subject']} ({v['kind']}, {v['category']}){' – ' + v['english'] if v['english'] else ''}\n")


def time_limit(n: int, play_seconds: float) -> int:
    """Bölümün süre sınırı (sn). Referans (1702 bölüm): 1–9. bölüm 300, 10. bölüm 180, sonra
    çoğu 100–140; zor bölümlere daha çok süre, değerler 5'in katı. Referansta süre tahta ölçüleriyle neredeyse ilişkisiz
    (R² 0,1): oyuncu verisinden ayarlanıyor. Bizde karşılığı simülasyonun insan benzeri bitirme süresi: 1,6 katı + 30 sn
    (bot düşünme süresini kısa tutar; zor bölüm uzun sürdüğü için kendiliğinden daha çok süre alır), 60–240 arası."""
    if n <= 9: return 300
    if n == 10: return 180
    t = 1.6 * play_seconds + 30
    return int(min(240, max(60, 5 * math.ceil(t / 5))))


def write_level(spec, picture, source, hidden, board, bm, ev):
    """Bölüm çıktıları: card.png, level.json (Unity içe aktarıcısı için)."""
    d = _level_dir(spec.n)
    pm = pic.metrics(picture)
    mech = []
    if board.holes: mech.append(f'{len(board.holes)} delik')
    for name, k in (('tek eksen', sum(1 for b in board.blocks if b.way)), ('buz', sum(1 for b in board.blocks if b.ice)),
                    ('kilit', sum(1 for b in board.blocks if b.lock)), ('katmanlı', sum(1 for b in board.blocks if b.inner)),
                    ('asteroit', sum(1 for a in board.asteroids if not a.count)), ('sayaçlı asteroit', sum(1 for a in board.asteroids if a.count))):
        if k: mech.append(f'{k} {name}')
    lines = [
        f"Resim ({source}): {picture.W}×{picture.H}, {pm['pixels']} piksel, renkler " +
        ', '.join(f"{COLOR_NAMES[c]} {v}" for c, v in sorted(pm['counts'].items(), key=lambda kv: -kv[1])),
        f"  bölge {pm['regions']} (hedef {spec.picture['regions']}), kilitli {len(pm['locked'])} (hedef {spec.picture['locked']}), "
        f"başta açık {len(pm['start_colors'])}, en büyük renk %{pm['top_share'] * 100:.0f}, simetri %{pm['symmetry'] * 100:.0f}",
        f"Tahta: {board.W}×{board.H}, {len(board.blocks)} blok, doluluk %{bm['fill'] * 100:.0f}, başta serbest {bm['peel']['first_free']}, "
        f"katman {bm['layers']}, yer değiştirme {bm['peel']['shuffles']}, simetri %{bm['symmetry'] * 100:.0f}" + (f"; {', '.join(mech)}" if mech else ''),
        f"Oynanış: usta bot {'çözdü' if ev['expert']['solved'] else 'çözemedi'} ({ev['expert']['seconds']:.0f} sn), "
        f"insan benzeri kazanma %{ev['win_rate'] * 100:.0f} (hedef %{spec.play['win_rate'][0] * 100:.0f}–{spec.play['win_rate'][1] * 100:.0f}), "
        f"en yüksek slot {ev['slots']:.1f}, süre {ev['seconds']:.0f} sn",
    ] + ([f"Gerçekçi oyuncular: %{ev['human']['win'] * 100:.0f} (" + ', '.join(f"{k} %{v['win'] * 100:.0f}" for k, v in ev['human']['profiles'].items()) +
          f"), süre sınırı {ev['human']['time_limit']} sn, acemi süreden kayıp %{ev['human']['profiles']['acemi']['time_fail'] * 100:.0f}"] if 'human' in ev else []) + ([f"Yenilik: {'; '.join(spec.novelty)}"] if spec.novelty else [])
    im = render.card(spec, picture, board, lines, hidden)
    im.save(os.path.join(d, 'card.png'))
    write_level_json(os.path.join(d, 'level.json'), f'P{spec.n:03d}', f"Bölüm {spec.n} – {spec.theme['subject']}",
                     picture, board, dict(tier=spec.tier, picture_source=source, hiddenImage='hidden.png' if hidden else '',
                                          timeLimit=ev['human']['time_limit'] if 'human' in ev else time_limit(spec.n, ev['seconds']),
                                          metrics=dict(picture=pm, board={k: v for k, v in bm.items() if k != 'peel'},
                                                       play=dict(win_rate=ev['win_rate'], slots=ev['slots'],
                                                                 seconds=ev['seconds'],
                                                                 expert_solved=ev['expert']['solved'],
                                                                 human=ev.get('human')))))
    return dict(n=spec.n, tier=spec.tier, card=im, win=ev['win_rate'], target=spec.play['win_rate'])


def samples_for(levels, out='samples2', use_codex=True, attempts=2, force=False, log=print):
    """Belirli bölümlerin künyeleriyle piksel resim + gizli resim (tahta yok). Çıktı: out/<out>/<NNN>/, out/<out>.html."""
    from .spec import build_range
    specs = {s.n: s for s in build_range(1, max(levels))}
    cards = []
    for n in levels:
        spec = specs[n]
        d = os.path.join(ROOT, out, f'{n:03d}')
        os.makedirs(d, exist_ok=True)
        log(f'{n}. bölüm ({spec.picture["kind"]}, {spec.picture["category"]}, {spec.picture["subject"]}, ortam {spec.theme["setting"]})')
        picture, source = make_picture(spec, d, use_codex, attempts, force, log)
        hidden = make_hidden(spec, d, use_codex, force, log)
        penalty, issues, m = pic.validate(picture, spec)
        cards.append((spec, picture, hidden, source, issues, m))
    write_samples(cards, os.path.join(ROOT, f'{out}.html'))
    return cards


def samples(kinds=('desen', 'ikon', 'siluet', 'sahne'), use_codex=True, attempts=2, force=False, log=print):
    """Her resim tipinden bir örnek: takvimde o tipin ilk geldiği bölümün künyesiyle piksel resim + gizli resim.
    Çıktı: out/samples/<tip>/ ve out/ornekler.html."""
    from .spec import build_range
    want = {}
    for spec in build_range(1, 600):
        k = spec.picture['kind']
        if k in kinds and k not in want and (k != 'desen' or spec.n > 1): want[k] = spec
    cards = []
    for k in kinds:
        spec = want[k]
        d = os.path.join(ROOT, 'samples', k)
        os.makedirs(d, exist_ok=True)
        log(f'{k}: {spec.n}. bölüm künyesi ({spec.tier}, {spec.picture["subject"]})')
        picture, source = make_picture(spec, d, use_codex, attempts, force, log)
        hidden = make_hidden(spec, d, use_codex, force, log)
        penalty, issues, m = pic.validate(picture, spec)
        cards.append((spec, picture, hidden, source, issues, m))
    write_samples(cards)
    return cards


def write_samples(cards, path=None):
    from PIL import Image
    def uri(im, fmt='PNG'):
        b = io.BytesIO(); im.convert('RGB').save(b, fmt, **({'quality': 85} if fmt == 'JPEG' else {}))
        return f'data:image/{fmt.lower()};base64,' + base64.b64encode(b.getvalue()).decode()
    names = {'desen': 'Desen', 'ikon': 'Tam kare ikon', 'siluet': 'Silüet', 'sahne': 'Sahne'}
    html = []
    for spec, picture, hidden, source, issues, m in cards:
        pim = pic.render(picture, cell=max(8, 420 // max(picture.W, picture.H)))
        him = Image.open(hidden) if hidden else None
        cols = ', '.join(COLOR_NAMES[c] for c in spec.colors)
        html.append(f"""<section><h2>{names[spec.picture['kind']]} · {spec.picture['subject']}</h2>
<p>{spec.n}. bölüm künyesi ({spec.tier}) · {picture.W}×{picture.H} · {m['pixels']} piksel · {len(spec.colors)} renk: {cols}<br>
bölge {m['regions']} (hedef {spec.picture['regions']}) · kilitli {len(m['locked'])} · kaynak: {source}{' · ' + '; '.join(issues) if issues else ''}</p>
<div class="row"><figure><img src="{uri(pim)}" alt="piksel resim"><figcaption>Piksel resim</figcaption></figure>
{f'<figure><img src="{uri(him, "JPEG")}" alt="gizli resim"><figcaption>Gizli resim (uzay temalı)</figcaption></figure>' if him else '<p>gizli resim yok</p>'}</div></section>""")
    page = f"""<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Resim Tipi Örnekleri</title><style>body{{margin:0;padding:20px 16px;background:#0f1430;color:#e8ecff;font:14px/1.45 -apple-system,system-ui,sans-serif}}
h1{{font-size:22px;margin:0 0 12px}}h2{{font-size:18px;margin:28px 0 4px}}p{{color:#a7b0d8;margin:0 0 10px}}
.row{{display:flex;flex-wrap:wrap;gap:16px}}figure{{margin:0;flex:1 1 300px;max-width:460px}}img{{width:100%;border-radius:12px;display:block}}
figcaption{{color:#a7b0d8;margin-top:4px}}</style></head><body><h1>Resim tipi örnekleri</h1>{''.join(html)}</body></html>"""
    open(path or os.path.join(ROOT, 'ornekler.html'), 'w').write(page)


def write_preview(rows):
    def uri(im):
        im = im.copy(); im.thumbnail((640, 640))
        b = io.BytesIO(); im.convert('RGB').save(b, 'JPEG', quality=70)
        return 'data:image/jpeg;base64,' + base64.b64encode(b.getvalue()).decode()
    ok = sum(1 for r in rows if r['target'][0] <= r['win'] <= r['target'][1])
    cards = ''.join(f'<div class="c"><img src="{uri(r["card"])}" alt="{r["n"]}. bölüm"></div>' for r in rows)
    html = f"""<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Bölüm Tasarlayıcı Önizleme</title><style>body{{margin:0;padding:20px 16px;background:#0f1430;color:#e8ecff;font:13px/1.4 -apple-system,system-ui,sans-serif}}
h1{{font-size:22px;margin:0 0 6px}}p{{color:#9aa3cc}}.g{{display:flex;flex-wrap:wrap;gap:14px}}.c img{{width:100%;max-width:640px;border-radius:10px}}.c{{flex:1 1 560px;max-width:640px}}</style></head><body>
<h1>Bölüm tasarlayıcı önizlemesi</h1><p>{len(rows)} bölüm; insan benzeri botun kazanma oranı hedef aralıkta: {ok}/{len(rows)}.</p>
<div class="g">{cards}</div></body></html>"""
    open(os.path.join(ROOT, 'onizleme.html'), 'w').write(html)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('first', type=int, nargs='?', default=1); ap.add_argument('last', type=int, nargs='?', default=10)
    ap.add_argument('--samples', action='store_true')
    ap.add_argument('--levels', type=str, default='', help='virgülle bölüm numaraları: yalnız resim + gizli resim örneği')
    ap.add_argument('--no-codex', action='store_true'); ap.add_argument('--force', action='store_true')
    ap.add_argument('--attempts', type=int, default=2)
    ap.add_argument('--art-workers', type=int, default=3, help='aynı anda çalışan Codex sayısı')
    ap.add_argument('--board-workers', type=int, default=8, help='aynı anda üretilen tahta sayısı')
    ap.add_argument('--art-only', action='store_true', help='yalnız resim ve gizli resim (tahta yok)')
    ap.add_argument('--no-hidden', action='store_true', help='gizli resim üretme (konu listesi yine yazılır)')
    ap.add_argument('--only', type=str, default='', help='aralıkta yalnız bu bölümler (virgülle)')
    ap.add_argument('--shrink', type=str, default='', help=f'bu bölümlerin resmi {SHRINK_SIZE} karaya küçültülür (virgülle)')
    a = ap.parse_args()
    if a.levels:
        samples_for([int(x) for x in a.levels.split(',')], use_codex=not a.no_codex, attempts=a.attempts, force=a.force)
    elif a.samples:
        samples(use_codex=not a.no_codex, attempts=a.attempts, force=a.force)
    else:
        build(a.first, a.last, use_codex=not a.no_codex, attempts=a.attempts, force=a.force,
              art_workers=a.art_workers, board_workers=a.board_workers, art_only=a.art_only, with_hidden=not a.no_hidden,
              only={int(x) for x in a.only.split(',') if x}, shrink={int(x) for x in a.shrink.split(',') if x})
