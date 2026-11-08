"""Unity LevelData (.asset YAML) okuma ve tasarlayıcı çıktısını Unity içe aktarıcısının okuyacağı JSON'a yazma."""
import json
import re
from typing import Tuple

from .model import Asteroid, Board, Block, Gate, Picture

GAME_COLORS = ['7', '0', '3', '9']                 # GameColor Red, Blue, Green, Yellow → referans paletindeki karşılıkları
EDGES = ['top', 'bottom', 'left', 'right']        # Edge: Top, Bottom, Left, Right


def read_asset(path: str) -> Tuple[Picture, Board, dict]:
    text = open(path, encoding='utf-8').read()
    def field(name, cast=int):
        m = re.search(rf'^  {name}: (.*)$', text, re.M)
        return cast(m.group(1)) if m else None
    W, H = field('boardWidth'), field('boardHeight')
    cols, rows = field('beadColumns'), field('beadRows')
    layout = field('beadLayout', str).strip().translate(str.maketrans('RBGY', '7039'))
    picture = Picture([layout[i * cols:(i + 1) * cols] for i in range(rows)])
    rockets_txt = text.split('\n  rockets:\n', 1)[1].split('\n  exits:', 1)[0]
    blocks = []
    for chunk in rockets_txt.split('\n  - ')[0:]:
        if 'color:' not in chunk: continue
        color = GAME_COLORS[int(re.search(r'color: (\d+)', chunk).group(1))]
        ox, oy = map(int, re.search(r'origin: \{x: (-?\d+), y: (-?\d+)\}', chunk).groups())
        sx, sy = map(int, re.search(r'size: \{x: (\d+), y: (\d+)\}', chunk).groups())
        cap = int(re.search(r'capacity: (\d+)', chunk).group(1))
        if re.search(r'isBlocker: 1', chunk): continue
        rel = [(x, y) for x in range(sx) for y in range(sy)]
        n = len(rel)
        ammo = [cap // n + (1 if k < cap % n else 0) for k in range(n)]      # RocketSpec.PieceCapacity
        blocks.append(Block(color, rel, (ox, oy), ammo=ammo))
    exits_txt = text.split('\n  exits:', 1)[1].split('\n  hiddenImage', 1)[0]
    gates = []
    for chunk in exits_txt.split('\n  - ')[0:]:
        if 'edge:' not in chunk: continue
        color = GAME_COLORS[int(re.search(r'color: (\d+)', chunk).group(1))]
        edge = EDGES[int(re.search(r'edge: (\d+)', chunk).group(1))]
        start = int(re.search(r'start: (-?\d+)', chunk).group(1))
        width = int(re.search(r'width: (\d+)', chunk).group(1))
        gates.append(Gate(color, edge, start, width))
    meta = dict(levelId=field('levelId', str), orbitCapacity=field('orbitCapacity') or 5, slotCount=field('slotCount') or 5)
    return picture, Board(W, H, blocks, gates), meta


def write_level_json(path: str, level_id: str, name: str, picture: Picture, board: Board, extra: dict):
    """Unity içe aktarıcısı (Editor/DesignerLevelImporter) bu dosyadan LevelData üretir."""
    data = dict(levelId=level_id, displayName=name, boardWidth=board.W, boardHeight=board.H,
                beadColumns=picture.W, beadRows=picture.H, beadLayout=picture.layout(),
                rockets=[dict(color=b.color, cells=[dict(x=c[0], y=c[1]) for c in b.rel], origin=dict(x=b.pos[0], y=b.pos[1]),
                              ammo=b.ammo, way=b.way or '', ice=b.ice, key=b.key, lock=b.lock, inner=b.inner,
                              innerAmmo=b.inner_ammo) for b in board.blocks],
                exits=[dict(color=g.color, edge=g.edge, start=g.lo, width=g.w) for g in board.gates],
                holes=[dict(x=h[0], y=h[1]) for h in sorted(board.holes)],
                asteroids=[dict(shape=a.shape, x=a.pos[0], y=a.pos[1], rot=a.rot, count=a.count) for a in board.asteroids], **extra)
    json.dump(data, open(path, 'w'), ensure_ascii=False, indent=1)


def read_level_json(path: str) -> Tuple[Picture, Board]:
    """Tasarlayıcının level.json'unu geri oku (test ve yeniden değerlendirme için)."""
    d = json.load(open(path))
    cols, rows = d['beadColumns'], d['beadRows']
    lay = d['beadLayout']
    picture = Picture([lay[i * cols:(i + 1) * cols] for i in range(rows)])
    blocks = [Block(r['color'], [(c['x'], c['y']) for c in r['cells']], (r['origin']['x'], r['origin']['y']), r.get('way') or None,
                    list(r['ammo']), r.get('ice', 0), r.get('key', 0), r.get('lock', 0), r.get('inner') or '',
                    list(r.get('innerAmmo') or [])) for r in d['rockets']]
    gates = [Gate(e['color'], e['edge'], e['start'], e['width']) for e in d['exits']]
    holes = {(h['x'], h['y']) for h in d.get('holes', [])}
    rocks = [Asteroid(a['shape'], (a['x'], a['y']), a.get('rot', 0), a.get('count', 0)) for a in d.get('asteroids', [])]
    return picture, Board(d['boardWidth'], d['boardHeight'], blocks, gates, holes, rocks)
