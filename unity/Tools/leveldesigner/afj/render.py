"""Tahta ve bölüm kartı çizimi (önizleme için)."""
from PIL import Image, ImageDraw, ImageFont

from . import picture as pic
from .model import PALETTE, Board, Picture


def font(size=13):
    for path in ('/System/Library/Fonts/Supplemental/Arial Unicode.ttf', '/System/Library/Fonts/Supplemental/Arial.ttf'):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def rgb(code):
    r, g, b = PALETTE[code][1]
    return int(r * 255), int(g * 255), int(b * 255)


def render_board(board: Board, cell=48) -> Image.Image:
    W, H = board.W, board.H
    m = cell // 2 + 6
    im = Image.new('RGB', (W * cell + 2 * m, H * cell + 2 * m), (24, 30, 56))
    d = ImageDraw.Draw(im)
    def px(x, y): return m + x * cell, m + (H - 1 - y) * cell
    d.rounded_rectangle([m - 8, m - 8, m + W * cell + 8, m + H * cell + 8], radius=14, fill=(44, 50, 74))
    for x in range(W):
        for y in range(H):
            if (x, y) in board.holes: continue
            X, Y = px(x, y)
            d.rounded_rectangle([X + 3, Y + 3, X + cell - 3, Y + cell - 3], radius=6, fill=(112, 120, 140))
    for g in board.gates:
        col = rgb(g.color)
        if g.edge in ('top', 'bottom'):
            X0, _ = px(g.lo, 0); X1 = X0 + g.w * cell
            Y = m - 16 if g.edge == 'top' else m + H * cell + 4
            d.rounded_rectangle([X0 + 4, Y, X1 - 4, Y + 12], radius=6, fill=col)
        else:
            _, Y1 = px(0, g.lo); Y0 = Y1 - (g.w - 1) * cell
            X = m - 16 if g.edge == 'left' else m + W * cell + 4
            d.rounded_rectangle([X, Y0 + 4, X + 12, Y1 + cell - 4], radius=6, fill=col)
    for a in board.asteroids:
        # Asteroit: koyu kurşuni kaya, sayaçlıda sayı.
        for (x, y) in a.cells():
            X, Y = px(x, y)
            d.rounded_rectangle([X + 4, Y + 4, X + cell - 4, Y + cell - 4], radius=14, fill=(74, 86, 110), outline=(150, 120, 200), width=2)
        if a.count:
            X, Y = px(*a.pos)
            d.text((X + cell / 2 - 6, Y + cell / 2 - 9), str(a.count), fill=(255, 255, 255), font=font(16))
    for b in board.blocks:
        col = rgb(b.color); dark = tuple(int(v * 0.7) for v in col)
        cs = set(b.cells())
        for (x, y) in cs:
            X, Y = px(x, y)
            # bitişik hücreye doğru genişlet: blok tek parça görünsün
            l = 0 if (x - 1, y) in cs else 6; r = cell if (x + 1, y) in cs else cell - 6
            t = 0 if (x, y + 1) in cs else 6; btm = cell if (x, y - 1) in cs else cell - 6
            d.rectangle([X + l, Y + t, X + r, Y + btm], fill=dark)
            d.rectangle([X + l + 1, Y + t + 1, X + r - 1, Y + btm - 4], fill=col)
        if b.inner:
            # Katmanlı blok: iç katman hücrelerin ortasında iç renkte çekirdek (komşuya doğru birleşik).
            icol = rgb(b.inner)
            for (x, y) in cs:
                X, Y = px(x, y)
                l = 0 if (x - 1, y) in cs else cell * 0.22; r = cell if (x + 1, y) in cs else cell * 0.78
                t = 0 if (x, y + 1) in cs else cell * 0.22; btm = cell if (x, y - 1) in cs else cell * 0.78
                d.rectangle([X + l, Y + t, X + r, Y + btm], fill=icol)
        x0, y0 = min(c[0] for c in cs), max(c[1] for c in cs)
        X, Y = px(x0, y0)
        d.text((X + 9, Y + 6), str(sum(b.ammo)) + (f'+{sum(b.inner_ammo)}' if b.inner else ''), fill=(15, 15, 25), font=font(13))
        # Mekanik işaretleri: buz (açık mavi örtü + sayaç), kilit (zincir), anahtar, tek eksen oku.
        xs = [c[0] for c in cs]; ys = [c[1] for c in cs]
        CX = m + (min(xs) + max(xs) + 1) / 2 * cell; CY = m + (H - (min(ys) + max(ys) + 1) / 2) * cell
        if b.ice:
            for (x, y) in cs:
                X1, Y1 = px(x, y)
                d.rounded_rectangle([X1 + 4, Y1 + 4, X1 + cell - 4, Y1 + cell - 4], radius=7, outline=(235, 250, 255), width=3)
            d.text((CX - 6, CY - 9), str(b.ice), fill=(240, 252, 255), font=font(18))
        if b.lock:
            for (x, y) in cs:
                X1, Y1 = px(x, y)
                d.line([X1 + 6, Y1 + 6, X1 + cell - 6, Y1 + cell - 6], fill=(200, 205, 220), width=3)
                d.line([X1 + cell - 6, Y1 + 6, X1 + 6, Y1 + cell - 6], fill=(200, 205, 220), width=3)
            d.text((CX - 7, CY - 9), 'K', fill=(255, 215, 80), font=font(18))   # K: kilitli
        if b.key:
            d.text((CX - 7, CY - 9), 'A', fill=(255, 215, 80), font=font(18))   # A: anahtar
        if b.way:
            L = cell * 0.32
            if b.way == 'H': d.line([CX - L, CY + 12, CX + L, CY + 12], fill=(250, 250, 250), width=4)
            else: d.line([CX + 12, CY - L, CX + 12, CY + L], fill=(250, 250, 250), width=4)
    return im


def card(spec, picture: Picture, board: Board, lines, hidden=None, width=1060) -> Image.Image:
    pim = pic.render(picture, cell=max(8, 300 // max(picture.W, picture.H)))
    pim.thumbnail((330, 330))
    bim = render_board(board)
    bim.thumbnail((330, 380))
    him = Image.open(hidden).convert('RGB') if hidden else None
    if him: him.thumbnail((290, 290))
    h = max(pim.height, bim.height, him.height if him else 0) + 40 + 16 * len(lines)
    im = Image.new('RGB', (width, h), (18, 24, 54))
    d = ImageDraw.Draw(im)
    d.text((12, 6), f"{spec.n}. bölüm · {spec.tier} · {spec.theme['subject']}", fill=(235, 238, 255), font=font(16))
    im.paste(pim, (12, 30)); im.paste(bim, (12 + 345, 30))
    if him: im.paste(him, (12 + 690, 30))
    y = 36 + max(pim.height, bim.height)
    for ln in lines:
        d.text((12, y), ln, fill=(190, 200, 235), font=font(12)); y += 16
    return im
