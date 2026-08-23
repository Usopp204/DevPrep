import re, sys, html, itertools, xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()[1].find('.//root')   # index 1 = class page

geo, widths = {}, {}
for c in root:
    g = c.find('mxGeometry')
    if c.get('vertex') == '1' and g is not None and g.get('width'):
        widths[c.get('id')] = float(g.get('width'))
        if c.get('parent') == '1':
            geo[c.get('id')] = tuple(float(g.get(k)) for k in ('x', 'y', 'width', 'height'))

clipped = [(c.get('value')[:70], widths[c.get('parent')]) for c in root
           if c.get('vertex') == '1' and 'text;strokeColor=none' in (c.get('style') or '')
           and c.get('value') and len(html.unescape(c.get('value'))) * 6.05 + 22 > widths[c.get('parent')]]

boxlap = [(a, b) for (a, ga), (b, gb) in itertools.combinations(geo.items(), 2)
          if not (ga[0] + ga[2] <= gb[0] or gb[0] + gb[2] <= ga[0]
                  or ga[1] + ga[3] <= gb[1] or gb[1] + gb[3] <= ga[1])]

def pt(b, rx, ry):
    x, y, w, h = geo[b]
    return (x + rx * w, y + ry * h)

def hits(p, q, b):
    x, y, w, h = b
    x1, x2 = sorted([p[0], q[0]]); y1, y2 = sorted([p[1], q[1]])
    return not (x2 <= x or x1 >= x + w or y2 <= y or y1 >= y + h)

segs, diag, through, noroute = [], [], [], []
for c in root:
    if c.get('edge') != '1':
        continue
    s, src, dst = c.get('style'), c.get('source'), c.get('target')
    if 'exitX' not in s or 'entryX' not in s:
        noroute.append(c.get('id')); continue
    gv = lambda k: float(re.search(k + r'=([-0-9.e]+);', s).group(1))
    path_pts = [pt(src, gv('exitX'), gv('exitY'))]
    arr = c.find('mxGeometry').find('Array')
    if arr is not None:
        path_pts += [(float(p.get('x')), float(p.get('y'))) for p in arr]
    path_pts.append(pt(dst, gv('entryX'), gv('entryY')))
    for p, q in zip(path_pts, path_pts[1:]):
        segs.append((c.get('id'), p, q))
        if abs(p[0] - q[0]) > 0.05 and abs(p[1] - q[1]) > 0.05:
            diag.append((c.get('id'), p, q))
        for bid, b in geo.items():
            if bid not in (src, dst) and hits(p, q, b):
                through.append((c.get('id'), bid))

overlap = []
for (i1, p1, q1), (i2, p2, q2) in itertools.combinations(segs, 2):
    if i1 == i2:
        continue
    for ax, oth in ((0, 1), (1, 0)):
        if (abs(p1[ax] - q1[ax]) < .05 and abs(p2[ax] - q2[ax]) < .05
                and abs(p1[ax] - p2[ax]) < .05):
            a1, b1 = sorted([p1[oth], q1[oth]]); a2, b2 = sorted([p2[oth], q2[oth]])
            if min(b1, b2) - max(a1, a2) > 1:
                overlap.append((i1, i2))

for name, res in (('clipped member rows', clipped), ('overlapping class boxes', boxlap),
                  ('edges without explicit routing', noroute), ('diagonal segments', diag),
                  ('arrows crossing a class', through), ('overlapping arrow segments', overlap)):
    print(f'{name}: {res}')

# ---------------------------------------------------------------------------
# EXTENSIONS - not part of the six checks specified in section 7 of
# CLASS_DIAGRAM_RULES.md. Added because at 30+ boxes two further defects appear
# that the original checks cannot see.
# ---------------------------------------------------------------------------
HEADER_CHAR_W = 6.20   # bold 12px swimlane header, calibrated on the reference file
LABEL_CHAR_W = 5.6     # fontSize=10 edge label
LABEL_H = 18

# Extension 1: the class header is the swimlane's own value, not a
# 'text;strokeColor=none' child, so the clipping check above skips it entirely.
header_clipped = [(html.unescape(c.get('value')), geo[c.get('id')][2]) for c in root
                  if c.get('vertex') == '1' and c.get('id') in geo and c.get('value')
                  and 'swimlane' in (c.get('style') or '')
                  and len(html.unescape(c.get('value'))) * HEADER_CHAR_W + 22 > geo[c.get('id')][2]]

# Extension 2: edge labels are rendered along the edge and can land on top of a
# class box or on another label. Reconstruct each label's box and test it.
def path_of(c):
    s = c.get('style')
    if 'exitX' not in s or 'entryX' not in s:
        return None
    gv = lambda k: float(re.search(k + r'=([-0-9.e]+);', s).group(1))
    pts = [pt(c.get('source'), gv('exitX'), gv('exitY'))]
    arr = c.find('mxGeometry').find('Array')
    if arr is not None:
        pts += [(float(p.get('x')), float(p.get('y'))) for p in arr]
    pts.append(pt(c.get('target'), gv('entryX'), gv('entryY')))
    return pts

def along(pts, t):
    segs_ = [(p, q) for p, q in zip(pts, pts[1:])]
    total = sum(abs(q[0] - p[0]) + abs(q[1] - p[1]) for p, q in segs_) or 1
    want = total * t
    for p, q in segs_:
        d = abs(q[0] - p[0]) + abs(q[1] - p[1])
        if want <= d:
            f = want / d if d else 0
            return (p[0] + (q[0] - p[0]) * f, p[1] + (q[1] - p[1]) * f)
        want -= d
    return pts[-1]

labels = []
for c in root:
    if c.get('edge') != '1':
        continue
    pts = path_of(c)
    if not pts:
        continue
    if c.get('value'):
        txt = html.unescape(re.sub(r'<[^>]+>', '', c.get('value')))
        if txt.strip():
            cx, cy = along(pts, 0.5)
            w = len(txt) * LABEL_CHAR_W
            labels.append((c.get('id'), txt, (cx - w / 2, cy - LABEL_H / 2, w, LABEL_H)))
    for k in root:
        if k.get('parent') != c.get('id') or not k.get('value'):
            continue
        g = k.find('mxGeometry')
        rx = float(g.get('x')) if g is not None and g.get('x') else 0.0
        txt = html.unescape(k.get('value'))
        cx, cy = along(pts, (rx + 1) / 2)
        w = len(txt) * LABEL_CHAR_W
        labels.append((k.get('id'), txt, (cx - w / 2, cy - LABEL_H / 2, w, LABEL_H)))

def rect_overlap(a, b):
    return not (a[0] + a[2] <= b[0] or b[0] + b[2] <= a[0]
                or a[1] + a[3] <= b[1] or b[1] + b[3] <= a[1])

label_on_box = [(lid, txt, bid) for lid, txt, r in labels
                for bid, b in geo.items() if rect_overlap(r, b)]
label_on_label = [(a[0], b[0]) for a, b in itertools.combinations(labels, 2)
                  if rect_overlap(a[2], b[2])]

for name, res in (('[ext] class headers wider than their box', header_clipped),
                  ('[ext] edge labels overlapping a class box', label_on_box),
                  ('[ext] edge labels overlapping another label', label_on_label)):
    print(f'{name}: {res}')
