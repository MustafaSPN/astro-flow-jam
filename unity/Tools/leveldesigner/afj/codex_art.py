"""Codex ile piksel resim: künyeden prompt, `codex exec` ile üretim, ızgaraya aktarma, doğrulama, geri bildirimli tekrar.

Codex'in yerleşik görsel üretimi (gpt-image) kullanılır; üretilen orijinal ~/.codex/generated_images altına düşer.
Silüet resimlerde arka plan saf macenta (#FF00FF) istenir; macenta hücreler boş sayılır.
Her bölüm için ayrıca konuya uygun, uzay temalı gizli (ödül) resim çizdirilir (generate_hidden).
"""
import glob
import os
import shutil
import subprocess
import time
from typing import List, Optional, Tuple

from . import picture as pic
from .model import EMPTY, PALETTE, Picture, hex_of, lab_of

GEN_DIR = os.path.expanduser('~/.codex/generated_images')
KEY = (1.0, 0.0, 1.0)                      # macenta: boş hücre


def detail_word(regions: int, pixels: int) -> str:
    per = pixels / max(1, regions)
    if per > 14: return 'very simple, big flat areas, minimal details'
    if per > 9: return 'simple, a few clear details'
    if per > 6.5: return 'moderately detailed'
    return 'detailed, with several small accents (eyes, highlights, small patterns)'


def choose_locked(spec) -> List[str]:
    """İçte kalacak (kenara değmeyecek) renkler: konunun küçük iç detay renkleri, yani konu listesinde sona yakın olanlar.
    Ana renk, zemin rengi ve siyah (dış hat kenara değer) seçilmez; sahnelerde gökyüzü renkleri de seçilmez."""
    k = spec.picture['locked']
    if k <= 0: return []
    from .subjects import SUBJECTS
    entry = SUBJECTS.get(spec.picture['subject'])
    order = [c for c in (entry[2] if entry else spec.colors) if c in spec.colors]
    skip = {'2'} | ({'0', '1', '8', 'a', 't', 'n'} if spec.picture['kind'] == 'sahne' else set())
    cands = [c for c in reversed(order[1:]) if c not in skip]
    if len(cands) < k:                                       # desen ya da kısa liste: en az kullanılması beklenenler
        cands += [c for c in reversed(spec.colors) if c not in cands and c not in skip and c != spec.colors[0]]
    return cands[:k]


COMPOSITION = {
    'ikon': 'Subject: {subject}, centered, front view, filling about two thirds of the canvas. Adorable kawaii mobile puzzle game style{face}, '
            'chunky rounded shapes, big shiny eyes where it has a face. '
            'Every pixel is filled: behind the subject is a simple flat background in {bg}, with a thin decorative border '
            'around the canvas edge. Keep it symmetric left/right where the subject allows.',
    'siluet': 'Subject: {subject}, centered, large: it fills about 85-90% of the canvas width or height, touching close to '
              'the edges. Cute, polished mobile puzzle game pixel art{face}, like a high quality game sprite: the subject is '
              'rich in detail (clear parts, highlights, shadows, texture marks, small accents), not a flat blob. '
              'Keep every detail bold and readable at this size: eyes are big (at least 3 x 3 pixels, dark with a white highlight), mouth and accents at least 2 pixels wide. '
              'There is NO background, no ground, no scenery, no frame: everything around the subject is pure magenta '
              '#FF00FF (this marks empty pixels, not a color of the art).',
    'sahne': 'A full scene: {subject}, in bright daylight colors. Every pixel is filled; the background is part of the art. '
             'Very simple composition made of a FEW LARGE flat shapes (sky, ground, one main object), readable at this tiny size; '
             'no small clutter, no dark areas, no tiny details.',
}
FACE_CATEGORIES = ('Yiyecek', 'Doğa', 'Nesne', 'Uzay')


def build_prompt(spec, feedback: Optional[List[str]] = None) -> str:
    W, H = spec.picture_size
    pt = spec.picture
    colors = spec.__dict__.get('suggested_colors', spec.colors)     # künyenin önerisi (aktarımda çizimden yeniden seçilir)
    names = {c: PALETTE[c][0] for c in colors}
    locked = choose_locked(spec)
    face = ' with a friendly cute face' if pt['category'] in FACE_CATEGORIES else ''
    if pt['category'] in ('Karakter', 'Tanınmış karakter'):     # ince insan figürleri küçük ızgarada dağılır
        face = ', chibi proportions: a big head (about half the height) with big eyes, a small simple body, simple bold hair shapes'
    bg = [c for c in colors if c not in _subject_codes(spec)] or colors[-1:]
    # Silüette 'retro oyun sprite'ı tarifi: Codex'in çizdiği ızgara daha temiz ve hedefe daha yakın çıkıyor (denemede
    # ızgara gücü 0,96; düz tarifte 0,26). Codex yine kendi ızgarasında çizer, aktarım hedefe sığdırır.
    head = (f'Retro 8-bit game sprite on a {W} x {H} grid (like a NES / Game Boy Color sprite, but bigger): chunky, clearly '
            f'visible square pixels; the whole canvas is {W} pixels across, so each pixel is about 1/{W} of the image width. '
            'Do not draw finer than this grid. Flat colors only: no anti-aliasing, no gradients, no dithering, no text, no logos.'
            if pt['kind'] == 'siluet' else
            f'Pixel art, exactly {W} pixels wide and {H} pixels tall, on a strict grid. Each pixel is a perfectly flat square of '
            'one color: no anti-aliasing, no gradients, no dithering, no texture, no text, no logos.')
    lines = [
        head,
        COMPOSITION[pt['kind']].format(subject=pt['subject_en'], face=face,
                                        bg=' and '.join(f'{hex_of(c)} ({names[c]})' for c in bg[:2])),
        # Küçük ızgarada (≤ 36) ince ayrıntı küçültmede kaybolur: iri biçimler, en az 3×3 göz, az ama belirgin ayrıntı.
        f'Detail level: {("simple and bold: big clear shapes, eyes at least 3 x 3 pixels, only a few accents; no tiny details that would vanish on a " + str(W) + " x " + str(H) + " grid" if max(W, H) <= 36 else "detailed, with shading, highlights and several small accents") if pt["kind"] == "siluet" else detail_word(pt["regions"], W * H)}.',
        'Use EXACTLY these colors and no others:',
    ]
    for c in colors:
        lines.append(f'- {hex_of(c)} ({names[c]})')
    if pt['outline']:
        lines.append('Use the black #2F2F2F only INSIDE the subject: inner lines between parts, eyes, mouth and small details. '
                     'Do NOT draw a black outline around the outside of the subject: its outer edge uses the subject\'s own '
                     'colors (or their darker shade), so many different colors are visible from the outside.')
    if pt.get('shading'):
        lines.append('Shade forms with the lighter and darker colors of the same hue from the list (simple two-tone shading).')
    if pt.get('accents_min'):
        lines.append('Include at least one small accent color (eyes, cheeks, highlights, buttons) used on only a few pixels.')
    if locked:
        lines.append('These colors appear ONLY inside the subject and never touch the canvas edge: ' +
                     ', '.join(f'{hex_of(c)}' for c in locked) + '.')
    if pt['kind'] != 'siluet':
        lines.append(f'At least {pt["start_colors_min"]} of the colors touch the outer edge of the canvas somewhere.')
    lines.append(f'No single color covers more than {round(pt["top_share_max"] * 100)}% of the pixels.')
    if feedback:
        lines.append('Fix these problems from the previous attempt: ' + '; '.join(feedback) + '.')
    return '\n'.join(lines)


def _subject_codes(spec) -> str:
    from .subjects import SUBJECTS
    entry = SUBJECTS.get(spec.picture['subject'])
    return (entry[2] if entry else '') + '2'


HIDDEN_STYLE = ('Square 1:1 illustration in a polished, glossy 3D cartoon render style, like a premium mobile game reward '
                'picture: vivid saturated colors, soft lighting, cute and joyful. No text, no logo, no frame, no border.')


def hidden_prompt(spec) -> str:
    """Gizli resim: stil sabit; ortam ve renk havası künyedeki ortamdan (konuya göre değişir, yakın bölümlerde tekrar etmez)."""
    from .subjects import HIDDEN_SETTINGS
    pt = spec.picture
    cat, subj = pt['category'], pt['subject_en']
    place, mood = HIDDEN_SETTINGS[spec.theme.get('setting', 'bulutsu')]
    Subj = subj[0].upper() + subj[1:]
    if pt['kind'] == 'desen':
        what = (f'A cute little astronaut cat {place}; the scenery subtly forms a playful pattern of {subj}.')
    elif cat == 'Sahne':
        what = f'A space version of this scene: {subj}, set on a small round planet, {place}.'
    elif cat in ('Hayvan', 'Karakter', 'Tanınmış karakter'):
        what = (f'{Subj} as the hero, big and centered, wearing a little space helmet or suit, {place}, '
                'with a happy, expressive face.')
    else:
        what = f'{Subj} with a happy cute face, big and centered, like a little astronaut, {place}.'
    return f'{what} Color mood: {mood}. Vary the composition; do not use a generic purple nebula background. ' + HIDDEN_STYLE


def generate_hidden(spec, workdir: str, size=1024, log=print) -> Optional[str]:
    """Konuya uygun, uzay temalı gizli (ödül) resim: kare, oyundaki gizli resimlerle aynı stil. Yol döndürür."""
    from PIL import Image
    os.makedirs(workdir, exist_ok=True)
    prompt = hidden_prompt(spec)
    open(os.path.join(workdir, 'hidden_prompt.txt'), 'w').write(prompt)
    raw = run_codex(prompt, os.path.join(workdir, 'hidden_raw'))
    if not raw:
        log('  Codex gizli resim üretmedi'); return None
    img = Image.open(raw).convert('RGB')
    s = min(img.size)                                        # oyun kare kırpar: ortadaki kare
    img = img.crop(((img.width - s) // 2, (img.height - s) // 2, (img.width + s) // 2, (img.height + s) // 2))
    out = os.path.join(workdir, 'hidden.png')
    img.resize((size, size), Image.LANCZOS).save(out)
    return out


class CodexLimit(RuntimeError):
    """Codex kullanım sınırı: bu oturumda başka görsel üretilemez (sınır kalkınca yeniden çalıştırılır)."""


def _session(log_path: str) -> Optional[str]:
    try:
        for line in open(log_path, errors='ignore'):
            if line.startswith('session id:'): return line.split(':', 1)[1].strip()
    except OSError:
        pass
    return None


def run_codex(prompt: str, workdir: str, timeout=420, settle=20, images=()) -> Optional[str]:
    """Codex'e görsel ürettir; üretilen orijinal dosyanın yolunu döndür.
    Codex görseli ~/.codex/generated_images/<oturum>/ altına yazar; oturum kimliği kendi günlüğünden okunur ve yalnız o
    klasör izlenir (aynı anda birden çok Codex çalışabilir). Codex bazen görseli kaydettikten sonra bağlantı hatasıyla
    kapanmayı bekleyerek takılır: yeni görsel `settle` saniye değişmeden kalınca işlem sonlandırılır."""
    os.makedirs(workdir, exist_ok=True)
    start = time.time()
    task = (prompt + '\n\nGenerate this image with your built-in image generation tool. '
            'Do not resize or post-process it. Save the generated image in the current directory as art.png.')
    log_path = os.path.join(workdir, 'codex.log')
    attach = [a for path in images for a in ('-i', os.path.abspath(path))]      # başvuru görselleri (piksel resim, stil örneği)
    proc = subprocess.Popen(['codex', 'exec', '--skip-git-repo-check', '-s', 'workspace-write', '-C', workdir, *attach, '--', task],
                            stdin=subprocess.DEVNULL, stdout=open(log_path, 'w'), stderr=subprocess.STDOUT)
    session = None
    def fresh():
        if not session: return []
        return [f for f in glob.glob(os.path.join(GEN_DIR, session, '*.png')) if os.path.getmtime(f) >= start - 1]
    found, seen_at = None, None
    while time.time() - start < timeout:
        if proc.poll() is not None: break
        session = session or _session(log_path)
        files = fresh()
        if files:
            newest = max(files, key=os.path.getmtime)
            if newest != found: found, seen_at = newest, time.time()
            elif time.time() - seen_at > settle: break
        time.sleep(2)
    if proc.poll() is None:
        proc.terminate()
        try: proc.wait(timeout=10)
        except subprocess.TimeoutExpired: proc.kill()
    session = session or _session(log_path)
    try:
        if 'hit your usage limit' in open(log_path, errors='ignore').read():
            raise CodexLimit('Codex kullanım sınırı doldu')
    except OSError:
        pass
    files = fresh()
    if files:
        src = max(files, key=os.path.getmtime)
        dst = os.path.join(workdir, f'raw_{int(start)}.png')
        shutil.copy(src, dst)
        return dst
    art = os.path.join(workdir, 'art.png')
    return art if os.path.exists(art) and os.path.getmtime(art) >= start - 1 else None


def to_picture(path: str, spec) -> Picture:
    from PIL import Image
    import numpy as np
    img = Image.open(path).convert('RGBA')
    a = np.asarray(img).astype(float)
    # macenta → saydam (boş)
    mag = (a[..., 0] > 200) & (a[..., 1] < 70) & (a[..., 2] > 200)
    a[mag, 3] = 0
    img = Image.fromarray(a.astype('uint8'), 'RGBA')
    # Codex istenen ızgaraya uymaz, kendi ızgarasında çizer (20 istenir 29 çizer, 32 istenir 64 çizer). Izgarası
    # bulunabiliyorsa çizim o ızgarada hücre hücre okunur; hedefin yaklaşık katıysa hizalı bloklarla küçültülür. Bölüm
    # resmin gerçek boyutunu alır. Izgara bulunamazsa künyedeki ızgaraya çoğunluk oyuyla aktarılır.
    W, H = spec.picture_size
    cols, rows, strength = pic.detect_cells(img)
    # Bulunan ızgaranın oranı künyeden çok farklıysa (ör. 9×20) ızgara yanlış okunmuştur: künye boyutuna aktarılır.
    aspect_ok = rows > 0 and abs(cols / rows - W / H) <= 0.25 * (W / H)
    grid_ok = strength >= 0.5 and 8 <= cols <= 96 and 8 <= rows <= 96 and aspect_ok
    if not grid_ok and spec.picture['silhouette']:
        # Izgara okunamadı: hedefin iki katı ince ızgarada örneklenir, sonra kesirli oranla hedefe sığdırılır.
        cols, rows = min(96, 2 * W), min(96, 2 * H)
    if grid_ok or spec.picture['silhouette']:
        phase = pic.grid_phase(img, cols, rows) if grid_ok else (0.0, 0.0)
        labs = pic.cell_labs(img, cols, rows, phase=phase)
        # Renkler çizimden seçilir: künyedeki renk sayısı korunur, hangi renkler olduğu Codex'in çizdiğine en uygun
        # olanlardır (önceden sabit palete zorla aktarmak gölgeleri siyaha, ara tonları yanlış renge düşürüyordu).
        suggested = spec.__dict__.setdefault('suggested_colors', list(spec.colors))
        # Siyah (göz, burun, ağız) çizimde koyu pikseller varsa korunur: küçük ızgarada başka renge düşünce yüz kayboluyor.
        keep = ['2'] if '2' in suggested and pic.has_dark(labs) else []
        spec.colors = pic.choose_palette(labs, len(suggested), prefer=suggested, keep=keep)
        p = pic.labs_to_picture(labs, spec.colors)
        if spec.picture['silhouette']:
            # Silüet: Codex'in kendi ızgarasında okunur, boş kenar atılır, hedef kutuya kesirli oranla sığdırılır
            # (62 → 40 gibi tam kat olmayan oranlar da). Kaba piksellerle hizalı tam katlı indirme önce denenir.
            k = max(1, int(cols / W)) if grid_ok else 1
            if k > 1: p = pic.downsample(p, k)
            p = pic.fit(p, W, H)
        else:
            k = max(1, round(cols / W))
            if k > 1 and abs(cols / k - W) / W <= 0.3: p = pic.downsample(p, k)
    else:
        p = pic.cleanup(pic.image_to_picture_majority(img, spec.colors, W, H))
    p = pic.denoise(p)                            # palete aktarımdaki kumlanma
    return pic.open_outer_outline(p)              # Codex yine de dış siyah hat çizdiyse aç


def _cache_key(spec) -> str:
    return f"{spec.picture['subject']}|{spec.picture['kind']}"


def generate(spec, workdir: str, attempts=2, log=print) -> Tuple[Optional[Picture], float, List[str], List[str]]:
    """En iyi denemeyi döndür: (resim, ceza, ihlaller, ham görsel yolları)."""
    os.makedirs(workdir, exist_ok=True)
    best = None; feedback = None; raws = []
    # Daha önce bu konu için üretilmiş ham çizimler yeniden değerlendirilir (kural değişince Codex'i tekrar çağırmadan).
    import json
    index_path = os.path.join(workdir, 'raws.json')
    index = json.load(open(index_path)) if os.path.exists(index_path) else {}
    for raw in sorted(glob.glob(os.path.join(workdir, 'raw_*.png'))):
        if index.get(os.path.basename(raw)) != _cache_key(spec): continue
        p = to_picture(raw, spec)
        penalty, issues, _ = pic.validate(p, spec)
        raws.append(raw)
        if best is None or penalty < best[1]: best = (p, penalty, issues)
    if best is not None:
        log(f'  önceki çizimlerden en iyisi: ceza {best[1]:.2f}')
        return best[0], best[1], best[2], raws
    for k in range(attempts):
        prompt = build_prompt(spec, feedback)
        open(os.path.join(workdir, f'prompt_{k + 1}.txt'), 'w').write(prompt)
        raw = run_codex(prompt, workdir)
        if not raw:
            log('  Codex görsel üretmedi'); continue
        raws.append(raw)
        index[os.path.basename(raw)] = _cache_key(spec)
        json.dump(index, open(index_path, 'w'), ensure_ascii=False)
        p = to_picture(raw, spec)
        penalty, issues, m = pic.validate(p, spec)
        log(f'  deneme {k + 1}: {p.W}x{p.H}, ceza {penalty:.2f}, ' + ('; '.join(issues) if issues else 'kurallara uygun'))
        if best is None or penalty < best[1]: best = (p, penalty, issues)
        if penalty < 1.0: break
        feedback = issues
    if best is None: return None, 99.0, ['üretim başarısız'], raws
    return best[0], best[1], best[2], raws
