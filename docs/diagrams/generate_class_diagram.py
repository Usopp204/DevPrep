"""Class-diagram generator for DevPrep .drawio files.

Implements docs/diagrams/out/CLASS_DIAGRAM_RULES.md. Every layout constant from
sections 4, 5 and 6 of that document appears exactly once, as a module-level
variable. Coordinates are never hand-written: widths, heights, column positions
and edge waypoints are all derived from the class content in the spec.

Usage:
    python generate_class_diagram.py <spec_module.py> [--out DIR] [--report]

The generator rewrites ONLY the class <diagram> element of the target file. The
sequence <diagram> element is copied through byte-for-byte, and the splice is
verified before the file is written.
"""

import importlib.util
import itertools
import math
import os
import re
import sys

# --- section 4: layout constants -------------------------------------------
LEFT_MARGIN = 40      # x of the first column
FIRST_ROW_Y = 140     # top y shared by every first-row box; y < 140 is routing space
COL_GAP = 90          # horizontal gap between columns (also a routing channel)
VGAP = 60             # vertical gap between boxes inside one column
LANE = 34             # width of one vertical routing lane inside a column
COL_LANES = 3         # section 5 reserves lanes n = 1, 2, 3 inside a column
DTO_INDENT = COL_LANES * LANE + 20   # DTOs are indented so lanes 1..3 stay free
PAGE_MARGIN = 80      # added to the bounding box for pageWidth/pageHeight

# --- section 4: box metrics ------------------------------------------------
HEADER_H = 30         # swimlane startSize (header row)
ROW_H = 20            # one member row
DIV_H = 8             # divider line between fields and methods
CHAR_W = 6.05         # px per char, calibrated for Helvetica 11px member font
TEXT_PAD = 22         # horizontal padding inside a member row
HEADER_CHAR_W = 6.20  # px per char, bold 12px header; calibrated so the
                      # reference file's 27-char header fits its hand-set 190px box
WIDTH_ROUND = 10      # widths are rounded up to this multiple

# --- section 5: routing ----------------------------------------------------
CHANNEL_YS = [35, 65, 95]   # horizontal channels above the first row
GAP_LANE = 18         # spacing of vertical lanes inside a COL_GAP channel
GAP_LANES = 4         # how many lanes fit in one gap (4 * 18 < COL_GAP)
LABEL_CHAR_W = 5.6    # fontSize=10 edge label, must match the validator
LABEL_H = 18          # rendered height of an edge label, must match the validator
MULT_MIN_PATH = 200   # an edge carrying multiplicity needs this much path, or the
                      # labels at +/-0.85 land on the boxes (section 3 fixes the 0.85)
NUDGE = 12            # step used to separate two anchors that landed collinear
NUDGE_ROUNDS = 60     # bound on the de-confliction pass

# --- section 6: title ------------------------------------------------------
TITLE_Y = -70
TITLE_H = 40
TITLE_FONT_SIZE = 18

# --- section 6: style strings (copied verbatim from the rules) -------------
CLASS_STYLE = (
    'swimlane;fontStyle=1;align=center;verticalAlign=top;childLayout=stackLayout;'
    'horizontal=1;startSize=%d;horizontalStack=0;resizeParent=1;resizeParentMax=0;'
    'rounded=0;shadow=0;strokeWidth=1;fillColor=#ffffff;swimlaneFillColor=#ffffff;'
    'fontSize=12;html=1;' % HEADER_H
)
ROW_STYLE = (
    'text;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;spacingLeft=6;'
    'spacingRight=6;overflow=hidden;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;'
    'rounded=0;fontSize=11;html=1;'
)
DIV_STYLE = (
    'line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;'
    'spacingLeft=3;spacingRight=3;rotatable=0;labelPosition=right;points=[];'
    'portConstraint=eastwest;strokeColor=#000000;'
)
EDGE_BASE = (
    'html=1;rounded=0;edgeStyle=orthogonalEdgeStyle;strokeColor=#000000;'
    'jumpStyle=arc;jumpSize=8;'
)
EDGE_KIND = {
    'generalization': 'endArrow=block;endSize=14;endFill=0;',
    'realization': 'endArrow=block;endSize=14;endFill=0;dashed=1;',
    'dependency': 'endArrow=open;endSize=12;dashed=1;fontSize=10;labelBackgroundColor=#ffffff;',
    'association': 'endArrow=open;endSize=12;fontSize=10;labelBackgroundColor=#ffffff;',
}
LABEL_STYLE = (
    'edgeLabel;html=1;align=center;verticalAlign=middle;resizable=0;points=[];'
    'fontSize=11;labelBackgroundColor=#ffffff;'
)
TITLE_STYLE = (
    'text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;'
    'whiteSpace=wrap;rounded=0;fontSize=%d;fontStyle=1;' % TITLE_FONT_SIZE
)
MULT_SRC_X = -0.85    # section 3: multiplicity at both ends, never mid-line
MULT_DST_X = 0.85


def esc(text):
    """XML-escape exactly once. Guards against the double-escaping defect."""
    return (text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
                .replace('"', '&quot;'))


def row_width(text):
    return len(text) * CHAR_W + TEXT_PAD


def header_width(text):
    return len(text) * HEADER_CHAR_W + TEXT_PAD


def box_height(cls):
    """height = 30 + 20*fields + (8 if any methods) + 20*methods"""
    nf, nm = len(cls.get('fields', [])), len(cls.get('methods', []))
    return HEADER_H + ROW_H * nf + (DIV_H if nm else 0) + ROW_H * nm


def box_min_width(cls):
    widths = [row_width(t) for t in cls.get('fields', []) + cls.get('methods', [])]
    widths.append(header_width(cls['name']))
    return math.ceil(max(widths) / WIDTH_ROUND) * WIDTH_ROUND


class Layout:
    def __init__(self, spec):
        self.spec = spec
        self.cls = spec['classes']
        self.geo = {}        # key -> (x, y, w, h)
        self.col_of = {}     # key -> column index
        self.col_x = []
        self.col_w = []
        self._place()

    def _place(self):
        cols = self.spec['columns']
        # column widths: a DTO must still fit after being indented
        for col in cols:
            main = [box_min_width(self.cls[k]) for k in col if not self.cls[k].get('dto')]
            dtos = [box_min_width(self.cls[k]) for k in col if self.cls[k].get('dto')]
            w = max(main or [0])
            if dtos:
                w = max(w, DTO_INDENT + max(dtos))
            self.col_w.append(int(math.ceil(w / WIDTH_ROUND) * WIDTH_ROUND))
        x = LEFT_MARGIN
        for w in self.col_w:
            self.col_x.append(x)
            x += w + COL_GAP
        for ci, col in enumerate(cols):
            y = FIRST_ROW_Y
            for key in col:
                c = self.cls[key]
                h = box_height(c)
                if c.get('dto'):
                    bx, bw = self.col_x[ci] + DTO_INDENT, self.col_w[ci] - DTO_INDENT
                else:
                    bx, bw = self.col_x[ci], self.col_w[ci]
                self.geo[key] = (bx, y, bw, h)
                self.col_of[key] = ci
                y += h + VGAP

    def bbox(self):
        xs = [g[0] for g in self.geo.values()] + [LEFT_MARGIN]
        ys = [g[1] for g in self.geo.values()] + [TITLE_Y]
        x2 = [g[0] + g[2] for g in self.geo.values()]
        y2 = [g[1] + g[3] for g in self.geo.values()]
        return min(xs), min(ys), max(x2), max(y2)

    def between(self, key_a, key_b):
        """boxes of the same column vertically between a and b"""
        ci = self.col_of[key_a]
        ya, ha = self.geo[key_a][1], self.geo[key_a][3]
        yb = self.geo[key_b][1]
        lo, hi = min(ya + ha, yb + self.geo[key_b][3]), max(ya, yb)
        out = []
        for k, ci2 in self.col_of.items():
            if ci2 != ci or k in (key_a, key_b):
                continue
            y, h = self.geo[k][1], self.geo[k][3]
            if y + h > lo and y < hi:
                out.append(k)
        return out


class Router:
    """Assigns explicit exit/entry fractions and integer waypoints (section 5).

    Planning is global and runs in three passes, because the constraints are
    global:
      A. classify each edge - which routing idiom, and which side of each box
         it leaves and enters
      B. allocate a distinct anchor per (box, side), so two edges never leave
         the same side at the same coordinate (that produced collinear,
         overlapping segments)
      C. build the concrete exit/entry fractions and integer waypoints
    """

    def __init__(self, layout):
        self.L = layout
        self.col_lane_used = {}   # column -> in-column lanes taken
        # One pool per PHYSICAL gap. Gap g lies between column g and column g+1,
        # so the gap left of column ci and the gap right of column ci-1 are the
        # same 90px channel and must not be allocated twice.
        self.gap_used = {}
        self.channel_of = {}      # edge index -> horizontal channel y
        self.plan = {}
        self.kind_of = {}
        self.slot = {}
        self.nudge = {}       # edge index -> how many NUDGE steps its anchors moved
        self.lane_bump = {}   # edge index -> how many lanes it was pushed outward
        self.lane_override = {}   # edge index -> a freshly reserved lane x

    # -- lane helpers -------------------------------------------------------
    def _col_lane_x(self, ci, n):
        return self.L.col_x[ci] + n * LANE

    def _gap_offset(self, k):
        """Offset of lane k from the left wall of a gap, always strictly inside it.

        Lanes 0..GAP_LANES-1 sit on the 18px grid; beyond that the gap is
        subdivided halfway between existing lanes rather than letting the lane
        escape the gap and land on a class box.
        """
        if k < GAP_LANES:
            off = GAP_LANE * (k + 1)
        else:
            off = GAP_LANE * (k - GAP_LANES) + GAP_LANE // 2
        return min(off, COL_GAP - 6)

    def _gap_x(self, g, k):
        """x of lane k inside physical gap g (between column g and g+1)."""
        off = self._gap_offset(k)
        if g < 0:                       # margin to the left of the first column
            return int(LEFT_MARGIN - off)
        return int(self.L.col_x[g] + self.L.col_w[g] + off)

    def _take_gap(self, g, centred=False):
        order = [1, 2, 0, 3] if centred else [0, 3, 1, 2]
        return self._take(self.gap_used, g, order[:GAP_LANES])

    def _take(self, store, ci, prefer):
        used = store.setdefault(ci, set())
        for n in prefer:
            if n not in used:
                used.add(n)
                return n
        n = (max(used) + 1) if used else prefer[0]
        used.add(n)
        return n

    def _dto_only_between(self, src, dst):
        """True when everything in the way (and the target) is an indented DTO,
        so the three in-column lanes of section 5 are still clear."""
        limit = self.L.col_x[self.L.col_of[src]] + COL_LANES * LANE
        for k in self.L.between(src, dst) + [dst]:
            if self.L.geo[k][0] < limit:
                return False
        return True

    def _topmost(self, key):
        ci = self.L.col_of[key]
        return all(self.L.geo[k][1] >= self.L.geo[key][1]
                   for k, c in self.L.col_of.items() if c == ci)

    def _take_channel(self, idx):
        for y in CHANNEL_YS:
            if y not in self.channel_of.values():
                self.channel_of[idx] = y
                return y
        y = min(CHANNEL_YS) - 30 * (len(self.channel_of) - len(CHANNEL_YS) + 1)
        self.channel_of[idx] = y
        return y

    # -- pass A -------------------------------------------------------------
    def _classify(self, e, idx):
        L = self.L
        src, dst = e['from'], e['to']
        has_mult = bool(e.get('mult_src') or e.get('mult_dst'))
        sx, sy, sw, sh = L.geo[src]
        tx, ty, tw, th = L.geo[dst]
        cs, ct = L.col_of[src], L.col_of[dst]
        p = {'src': src, 'dst': dst, 'flip': False, 'mult': has_mult}

        if cs == ct:
            downward = ty > sy
            lo, hi = max(sx, tx), min(sx + sw, tx + tw)
            gap_travelled = (ty - (sy + sh)) if ty > sy else (sy - (ty + th))
            too_short = has_mult and gap_travelled < MULT_MIN_PATH
            if not L.between(src, dst) and hi - lo > 20 and not too_short:
                p.update(kind='straight-v', span=(lo, hi),
                         src_side='B' if downward else 'T',
                         dst_side='T' if downward else 'B')
            elif self._dto_only_between(src, dst) and not too_short:
                # innermost lane (nearest the indented DTOs) to the shortest edge;
                # edges reach here already sorted shortest-first. A labelled edge is
                # capped further out, because the label rides the lane and would
                # otherwise overlap the DTO column.
                max_n = COL_LANES
                if e.get('label'):
                    half = len(e['label']) * LABEL_CHAR_W / 2
                    max_n = max(1, min(COL_LANES, int((DTO_INDENT - half - 4) // LANE)))
                n = self._take(self.col_lane_used, cs, list(range(max_n, 0, -1)))
                p.update(kind='column lane %d (x=%d)' % (n, self._col_lane_x(cs, n)),
                         lane_n=n, lane_col=cs, lane_x=self._col_lane_x(cs, n),
                         src_side='B' if downward else 'T', dst_side='L')
            else:
                k = self._take_gap(cs - 1, centred=bool(e.get('label')))
                p.update(kind='gap channel lane %d (x=%d)'
                              % (k + 1, self._gap_x(cs - 1, k)),
                         lane_k=k, lane_gap=cs - 1, lane_x=self._gap_x(cs - 1, k),
                         src_side='L', dst_side='L')
            return p

        left, right = (src, dst) if ct > cs else (dst, src)
        cl, cr = L.col_of[left], L.col_of[right]
        lg, rg = L.geo[left], L.geo[right]
        lo, hi = max(lg[1], rg[1]), min(lg[1] + lg[3], rg[1] + rg[3])
        p['flip'] = ct < cs
        p['left'], p['right'] = left, right

        # A multiplicity edge never takes the bare COL_GAP hop: its labels at
        # +/-0.85 would land on the boxes. It uses the vertical-channel idiom
        # instead, with anchors spread to the far ends (see _build).
        if cr == cl + 1 and hi - lo > 20 and not has_mult:
            p.update(kind='straight-h', span=(lo, hi))
            p['src_side'], p['dst_side'] = ('R', 'L') if not p['flip'] else ('L', 'R')
        elif cr == cl + 1:
            k = self._take_gap(cl, centred=bool(e.get('label')))
            p.update(kind='vertical channel (x=%d)' % self._gap_x(cl, k),
                     lane_k=k, lane_gap=cl, lane_x=self._gap_x(cl, k))
            p['src_side'], p['dst_side'] = ('R', 'L') if not p['flip'] else ('L', 'R')
        else:
            chy = self._take_channel(idx)
            p.update(kind='horizontal channel y=%d' % chy, chy=chy)
            # rise: how the edge gets up to the channel from the left-hand box
            if self._topmost(left):
                p['rise'], lside = 'top', 'T'
            else:
                k = self._take_gap(cl)
                p['rise'], p['lane_x'], lside = 'gap', self._gap_x(cl, k), 'R'
            # descend: how it comes back down into the right-hand box. Entering the
            # top only works when nothing is stacked above it, otherwise the segment
            # would cut straight through those boxes.
            if self._topmost(right):
                p['descend'], rside = 'top', 'T'
            else:
                k2 = self._take_gap(cr - 1)
                p['descend'], p['dlane_x'], rside = 'gap', self._gap_x(cr - 1, k2), 'L'
            p['src_side'], p['dst_side'] = ((lside, rside) if not p['flip'] else (rside, lside))
        return p

    # -- pass B -------------------------------------------------------------
    def _anchor(self, idx, box, side, interval=None):
        """A distinct absolute coordinate on one side of one box.

        The per-(box, side) slot keeps edges leaving the SAME box apart. Two
        different boxes can still project a stub into the same gap at the same
        coordinate, so a bounded de-confliction pass may add NUDGE steps."""
        j, n = self.slot[(idx, box, side)]
        x, y, w, h = self.L.geo[box]
        lo, hi = interval if interval else ((y, y + h) if side in 'LR' else (x, x + w))
        base = lo + (hi - lo) * (j + 1) / (n + 1)
        base += self.nudge.get(idx, 0) * NUDGE
        return int(min(max(base, lo + 4), hi - 4))

    # -- driver -------------------------------------------------------------
    def plan_all(self, edges):
        self.edges = edges

        def span_of(i):
            a, b = self.L.geo[edges[i]['from']], self.L.geo[edges[i]['to']]
            return abs(a[1] - b[1])
        same, multi, rest = [], [], []
        for i in range(len(edges)):
            cs = self.L.col_of[edges[i]['from']]
            ct = self.L.col_of[edges[i]['to']]
            (same if cs == ct else (multi if abs(ct - cs) > 1 else rest)).append(i)
        # section 5: the innermost lane goes to the shortest edge
        order = sorted(same, key=span_of) + sorted(multi, key=span_of) + rest
        plans = {i: self._classify(edges[i], i) for i in order}

        buckets = {}
        for i in order:
            p = plans[i]
            buckets.setdefault((p['src'], p['src_side']), []).append(i)
            buckets.setdefault((p['dst'], p['dst_side']), []).append(i)
        for (box, side), lst in buckets.items():
            for j, i in enumerate(lst):
                self.slot[(i, box, side)] = (j, len(lst))

        for i in order:
            self.kind_of[i] = plans[i]['kind']

        # Build, then separate any two segments that came out collinear and
        # overlapping. This only moves anchors - it never relaxes a rule. If it
        # cannot converge, that is a routing failure: raise rather than emit a
        # file that silently violates section 5.
        self.rounds = 0
        for attempt in range(NUDGE_ROUNDS):
            for i in order:
                self.plan[i] = self._build(i, plans[i])
            clashes = self._collinear(plans) + self._label_clashes(plans)
            self.rounds = attempt
            if not clashes:
                break
            for a, b in clashes:
                moved = False
                if 'lane_gap' in plans[b] or 'lane_n' in plans[b]:
                    moved = self._relane(b, plans[b])
                if not moved or a == b:
                    self.nudge[b] = self.nudge.get(b, 0) + 1
        else:
            raise SystemExit(
                'ROUTING FAILURE: de-confliction did not converge in %d rounds; '
                '%d collinear overlapping edge pair(s) remain: %s\n'
                'Refusing to write the file. Fix the layout or the router - do not '
                'relax the validator.' % (NUDGE_ROUNDS, len(clashes), clashes))
        return self.plan

    def _label_rects(self, plans):
        """Where each edge label lands, mirroring the validator's reconstruction."""
        out = []
        for i, (fr, wps) in self.plan.items():
            sg = self.L.geo[plans[i]['src']]
            tg = self.L.geo[plans[i]['dst']]
            pts = [(sg[0] + fr[0] * sg[2], sg[1] + fr[1] * sg[3])]
            pts += [(float(a), float(b)) for a, b in wps]
            pts.append((tg[0] + fr[2] * tg[2], tg[1] + fr[3] * tg[3]))
            e = self.edges[i]
            spots = []
            if e.get('label'):
                spots.append((e['label'], 0.5))
            if e.get('mult_src'):
                spots.append((e['mult_src'], (MULT_SRC_X + 1) / 2))
            if e.get('mult_dst'):
                spots.append((e['mult_dst'], (MULT_DST_X + 1) / 2))
            for txt, frac in spots:
                segs = list(zip(pts, pts[1:]))
                total = sum(abs(q[0] - p[0]) + abs(q[1] - p[1]) for p, q in segs) or 1
                want = total * frac
                px, py = pts[-1]
                for a, b in segs:
                    d = abs(b[0] - a[0]) + abs(b[1] - a[1])
                    if want <= d:
                        f = want / d if d else 0
                        px, py = a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f
                        break
                    want -= d
                w = len(txt) * LABEL_CHAR_W
                out.append((i, (px - w / 2, py - LABEL_H / 2, w, LABEL_H)))
        return out

    @staticmethod
    def _rect_hit(a, b):
        return not (a[0] + a[2] <= b[0] or b[0] + b[2] <= a[0]
                    or a[1] + a[3] <= b[1] or b[1] + b[3] <= a[1])

    def _label_clashes(self, plans):
        rects = self._label_rects(plans)
        bad = set()
        for i, r in rects:
            for bid, b in self.L.geo.items():
                if self._rect_hit(r, b):
                    bad.add((i, i))
        for (i1, r1), (i2, r2) in itertools.combinations(rects, 2):
            if i1 != i2 and self._rect_hit(r1, r2):
                bad.add((min(i1, i2), max(i1, i2)))
        return sorted(bad)

    def _collinear(self, plans):
        """Pairs of edges with a collinear, overlapping segment (section 5.2)."""
        segs = []
        for i, (fr, wps) in self.plan.items():
            sg = self.L.geo[plans[i]['src']]
            tg = self.L.geo[plans[i]['dst']]
            pts = [(sg[0] + fr[0] * sg[2], sg[1] + fr[1] * sg[3])]
            pts += [(float(a), float(b)) for a, b in wps]
            pts.append((tg[0] + fr[2] * tg[2], tg[1] + fr[3] * tg[3]))
            for a, b in zip(pts, pts[1:]):
                segs.append((i, a, b))
        bad = set()
        for (i1, p1, q1), (i2, p2, q2) in itertools.combinations(segs, 2):
            if i1 == i2:
                continue
            for ax, oth in ((0, 1), (1, 0)):
                if (abs(p1[ax] - q1[ax]) < .05 and abs(p2[ax] - q2[ax]) < .05
                        and abs(p1[ax] - p2[ax]) < .05):
                    a1, b1 = sorted([p1[oth], q1[oth]])
                    a2, b2 = sorted([p2[oth], q2[oth]])
                    if min(b1, b2) - max(a1, a2) > 1:
                        bad.add((min(i1, i2), max(i1, i2)))
        return sorted(bad)

    # -- pass C -------------------------------------------------------------
    def _lane_of(self, idx, p):
        """Lane x for this edge, honouring any de-confliction reallocation."""
        if idx in self.lane_override:
            return self.lane_override[idx]
        b = self.lane_bump.get(idx, 0)
        if b and 'lane_n' in p:                # inside a column: move toward its edge
            return self._col_lane_x(p['lane_col'], max(1, p['lane_n'] - b))
        return p['lane_x']

    def _relane(self, idx, p):
        """Move this edge to a lane nobody else holds. Gap lanes are pooled, so a
        bump must RESERVE its new lane or two edges silently share one."""
        if 'lane_gap' in p:
            k = self._take(self.gap_used, p['lane_gap'], list(range(64)))
            self.lane_override[idx] = self._gap_x(p['lane_gap'], k)
            return True
        if 'lane_n' in p:
            self.lane_bump[idx] = self.lane_bump.get(idx, 0) + 1
            return True
        return False

    def _build(self, idx, p):
        L = self.L
        src, dst = p['src'], p['dst']
        sx, sy, sw, sh = L.geo[src]
        tx, ty, tw, th = L.geo[dst]
        kind = p['kind']

        if kind == 'straight-v':
            ax = self._anchor(idx, src, p['src_side'], p['span'])
            down = p['src_side'] == 'B'
            return ((ax - sx) / sw, 1.0 if down else 0.0,
                    (ax - tx) / tw, 0.0 if down else 1.0), []

        if kind.startswith('column lane'):
            ax = self._lane_of(idx, p)
            ey = self._anchor(idx, dst, 'L')
            down = p['src_side'] == 'B'
            return ((ax - sx) / sw, 1.0 if down else 0.0,
                    0.0, (ey - ty) / th), [(ax, ey)]

        if kind.startswith('gap channel'):
            ax = self._lane_of(idx, p)
            if p.get('mult'):
                third_s, third_t = sh / 3.0, th / 3.0
                sep_a = abs((ty + th) - sy)
                sep_b = abs((sy + sh) - ty)
                si = (sy, sy + third_s) if sep_a >= sep_b else (sy + sh - third_s, sy + sh)
                ti = (ty + th - third_t, ty + th) if sep_a >= sep_b else (ty, ty + third_t)
                ay = self._anchor(idx, src, 'L', si)
                ey = self._anchor(idx, dst, 'L', ti)
            else:
                ay = self._anchor(idx, src, 'L')
                ey = self._anchor(idx, dst, 'L')
            return (0.0, (ay - sy) / sh, 0.0, (ey - ty) / th), [(ax, ay), (ax, ey)]

        lg, rg = L.geo[p['left']], L.geo[p['right']]

        if kind == 'straight-h':
            ay = self._anchor(idx, p['left'], 'R', p['span'])
            fl = (1.0, (ay - lg[1]) / lg[3])
            fr = (0.0, (ay - rg[1]) / rg[3])
            return ((fr + fl) if p['flip'] else (fl + fr)), []

        if kind.startswith('vertical channel'):
            ax = self._lane_of(idx, p)
            if p.get('mult'):
                # The multiplicity labels sit at +/-0.85 of the path (section 3), so
                # the path has to be long. Pick whichever pair of box extremes is
                # furthest apart, then anchor inside a third of the box at that end
                # so several edges leaving the same box still get distinct anchors.
                third_l, third_r = lg[3] / 3.0, rg[3] / 3.0
                top_l, bot_l = (lg[1], lg[1] + third_l), (lg[1] + lg[3] - third_l, lg[1] + lg[3])
                top_r, bot_r = (rg[1], rg[1] + third_r), (rg[1] + rg[3] - third_r, rg[1] + rg[3])
                sep_a = abs((rg[1] + rg[3]) - lg[1])      # left box top  <-> right box bottom
                sep_b = abs((lg[1] + lg[3]) - rg[1])      # left box bottom <-> right box top
                li, ri = (top_l, bot_r) if sep_a >= sep_b else (bot_l, top_r)
                ay = self._anchor(idx, p['left'], 'R', li)
                by = self._anchor(idx, p['right'], 'L', ri)
            else:
                ay = self._anchor(idx, p['left'], 'R')
                by = self._anchor(idx, p['right'], 'L')
            wps = [(ax, ay), (ax, by)]
            fl = (1.0, (ay - lg[1]) / lg[3])
            fr = (0.0, (by - rg[1]) / rg[3])
            if p['flip']:
                return fr + fl, list(reversed(wps))
            return fl + fr, wps

        # horizontal channel
        chy = p['chy']
        if p['rise'] == 'top':
            ax = self._anchor(idx, p['left'], 'T')
            wps = [(ax, chy)]
            fl = ((ax - lg[0]) / lg[2], 0.0)
        else:
            lane = self._lane_of(idx, p)
            ay = self._anchor(idx, p['left'], 'R')
            wps = [(lane, ay), (lane, chy)]
            fl = (1.0, (ay - lg[1]) / lg[3])
        if p['descend'] == 'top':
            bx = self._anchor(idx, p['right'], 'T')
            wps.append((bx, chy))
            fr = ((bx - rg[0]) / rg[2], 0.0)
        else:
            dlane = p['dlane_x']
            by = self._anchor(idx, p['right'], 'L')
            wps += [(dlane, chy), (dlane, by)]
            fr = (0.0, (by - rg[1]) / rg[3])
        if p['flip']:
            return fr + fl, list(reversed(wps))
        return fl + fr, wps


def emit(spec, want=None):
    L = Layout(spec)
    R = Router(L)
    routes = R.plan_all(spec['edges'])
    if want is not None:
        want.update(layout=L, router=R, routes=routes)
    out = []
    add = out.append

    add('        <mxCell id="0" />')
    add('        <mxCell id="1" parent="0" />')

    x1, y1, x2, y2 = L.bbox()
    for _fr, wps in routes.values():           # waypoints may sit outside the boxes
        for wx, wy in wps:
            x1, y1 = min(x1, wx), min(y1, wy)
            x2, y2 = max(x2, wx), max(y2, wy)
    add('        <mxCell id="k_title" value="%s" style="%s" vertex="1" parent="1">'
        % (esc(spec['title']), TITLE_STYLE))
    add('          <mxGeometry x="%d" y="%d" width="%d" height="%d" as="geometry" />'
        % (LEFT_MARGIN, TITLE_Y, max(600, x2 - x1), TITLE_H))
    add('        </mxCell>')

    for key in [k for col in spec['columns'] for k in col]:
        c = spec['classes'][key]
        bx, by, bw, bh = L.geo[key]
        add('        <mxCell id="%s" value="%s" style="%s" vertex="1" parent="1">'
            % (key, esc(c['name']), CLASS_STYLE))
        add('          <mxGeometry x="%d" y="%d" width="%d" height="%d" as="geometry" />'
            % (bx, by, bw, bh))
        add('        </mxCell>')
        oy = HEADER_H
        for i, text in enumerate(c.get('fields', [])):
            add('        <mxCell id="%s_f%d" value="%s" style="%s" vertex="1" parent="%s">'
                % (key, i, esc(text), ROW_STYLE, key))
            add('          <mxGeometry y="%d" width="%d" height="%d" as="geometry" />'
                % (oy, bw, ROW_H))
            add('        </mxCell>')
            oy += ROW_H
        if c.get('methods'):
            add('        <mxCell id="%s_div" value="" style="%s" vertex="1" parent="%s">'
                % (key, DIV_STYLE, key))
            add('          <mxGeometry y="%d" width="%d" height="%d" as="geometry" />'
                % (oy, bw, DIV_H))
            add('        </mxCell>')
            oy += DIV_H
            for i, text in enumerate(c['methods']):
                add('        <mxCell id="%s_m%d" value="%s" style="%s" vertex="1" parent="%s">'
                    % (key, i, esc(text), ROW_STYLE, key))
                add('          <mxGeometry y="%d" width="%d" height="%d" as="geometry" />'
                    % (oy, bw, ROW_H))
                add('        </mxCell>')
                oy += ROW_H

    for i, e in enumerate(spec['edges']):
        eid = 'k_e%d' % i
        (ex, ey, nx, ny), wps = routes[i]
        style = EDGE_BASE + EDGE_KIND[e['kind']] + (
            'exitX=%g;exitY=%g;exitDx=0;exitDy=0;entryX=%g;entryY=%g;entryDx=0;entryDy=0;'
            % (round(ex, 6), round(ey, 6), round(nx, 6), round(ny, 6)))
        add('        <mxCell id="%s" value="%s" style="%s" edge="1" parent="1" source="%s" target="%s">'
            % (eid, esc(e.get('label', '')), style, e['from'], e['to']))
        if wps:
            add('          <mxGeometry relative="1" as="geometry">')
            add('            <Array as="points">')
            for wx, wy in wps:
                add('              <mxPoint x="%d" y="%d" />' % (wx, wy))
            add('            </Array>')
            add('          </mxGeometry>')
        else:
            add('          <mxGeometry relative="1" as="geometry" />')
        add('        </mxCell>')
        for mult, rel in ((e.get('mult_src'), MULT_SRC_X), (e.get('mult_dst'), MULT_DST_X)):
            if not mult:
                continue
            add('        <mxCell id="%s_l%s" value="%s" style="%s" vertex="1" connectable="0" parent="%s">'
                % (eid, 's' if rel < 0 else 'd', esc(mult), LABEL_STYLE, eid))
            add('          <mxGeometry x="%g" relative="1" as="geometry">' % rel)
            add('            <mxPoint as="offset" />')
            add('          </mxGeometry>')
            add('        </mxCell>')

    pw = int(x2 - x1 + 2 * PAGE_MARGIN)
    ph = int(y2 - y1 + 2 * PAGE_MARGIN)
    body = '\n'.join(out)
    return (
        '    <mxGraphModel dx="1422" dy="794" grid="0" gridSize="10" guides="1" tooltips="1" '
        'connect="1" arrows="1" fold="1" page="0" pageScale="1" pageWidth="%d" pageHeight="%d" '
        'math="0" shadow="0">\n      <root>\n%s\n      </root>\n    </mxGraphModel>' % (pw, ph, body)
    )


DIAGRAM_RE = re.compile(r'(<diagram\b[^>]*>)(.*?)(</diagram>)', re.S)


def splice(path, model_xml):
    original = open(path, encoding='utf-8').read()
    blocks = list(DIAGRAM_RE.finditer(original))
    if len(blocks) != 2:
        raise SystemExit('expected exactly 2 <diagram> elements, found %d' % len(blocks))
    seq_before = original[blocks[0].start():blocks[0].end()]
    cls_open, _, cls_close = blocks[1].groups()
    updated = (original[:blocks[1].start()]
               + cls_open + '\n' + model_xml + '\n  ' + cls_close
               + original[blocks[1].end():])
    seq_after = list(DIAGRAM_RE.finditer(updated))[0]
    if updated[seq_after.start():seq_after.end()] != seq_before:
        raise SystemExit('refusing to write: the sequence page would change')
    open(path, 'w', encoding='utf-8', newline='').write(updated)
    return len(seq_before)


def load_spec(spec_path):
    name = os.path.splitext(os.path.basename(spec_path))[0]
    s = importlib.util.spec_from_file_location(name, spec_path)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m.SPEC


def report(spec, L, R, routes):
    print('\n(a) LAYOUT MAP')
    for ci, col in enumerate(spec['columns']):
        print('  column %d  x=%d  width=%d' % (ci + 1, L.col_x[ci], L.col_w[ci]))
        for key in col:
            x, y, w, h = L.geo[key]
            tag = ' [DTO, indented]' if spec['classes'][key].get('dto') else ''
            print('      y=%-6d h=%-4d w=%-4d %s%s' % (y, h, w, spec['classes'][key]['name'], tag))
    x1, y1, x2, y2 = L.bbox()
    for _fr, wps in routes.values():
        for wx, wy in wps:
            x1, y1, x2, y2 = min(x1, wx), min(y1, wy), max(x2, wx), max(y2, wy)
    print('  canvas: pageWidth=%d pageHeight=%d  (bbox %d,%d .. %d,%d + %d margin)'
          % (x2 - x1 + 2 * PAGE_MARGIN, y2 - y1 + 2 * PAGE_MARGIN, x1, y1, x2, y2, PAGE_MARGIN))

    print('\n(b) WIDTH JUSTIFICATION  (section 4: len*%.2f + %d, rounded up to %d)'
          % (CHAR_W, TEXT_PAD, WIDTH_ROUND))
    for ci, col in enumerate(spec['columns']):
        best, bestw, owner = '', 0, ''
        for key in col:
            c = spec['classes'][key]
            for t in c.get('fields', []) + c.get('methods', []):
                if row_width(t) > bestw:
                    best, bestw, owner = t, row_width(t), c['name']
        need = math.ceil(bestw / WIDTH_ROUND) * WIDTH_ROUND
        print('  column %d -> width %d' % (ci + 1, L.col_w[ci]))
        print('      longest row (%s), %d chars' % (owner, len(best)))
        print('        %s' % best)
        print('        %d*%.2f + %d = %.1f px  -> rounded %d' % (len(best), CHAR_W, TEXT_PAD, bestw, need))
        if L.col_w[ci] > need:
            # say which of the two other drivers actually set the column width
            hdr, hw = '', 0
            for key in col:
                c = spec['classes'][key]
                if header_width(c['name']) > hw:
                    hdr, hw = c['name'], header_width(c['name'])
            dto = [math.ceil(box_min_width(spec['classes'][k]) / WIDTH_ROUND) * WIDTH_ROUND
                   for k in col if spec['classes'][k].get('dto')]
            if dto and DTO_INDENT + max(dto) >= math.ceil(hw / WIDTH_ROUND) * WIDTH_ROUND:
                print('        widened by DTO indent: %d + %d = %d'
                      % (DTO_INDENT, max(dto), DTO_INDENT + max(dto)))
            else:
                print('        widened by header "%s": %d*%.2f + %d = %.1f -> %d'
                      % (hdr, len(hdr), HEADER_CHAR_W, TEXT_PAD, hw,
                         math.ceil(hw / WIDTH_ROUND) * WIDTH_ROUND))

    print('\n(c) ROUTING SUMMARY')
    kinds = {}
    for i in range(len(spec['edges'])):
        k = R.kind_of.get(i, '?')
        kinds.setdefault(k.split(' (')[0].split(' y=')[0], []).append(i)
    for k in sorted(kinds):
        print('  %-24s %d edge(s)' % (k, len(kinds[k])))
    print('  horizontal channels (section 5: one edge each):')
    used = {}
    for i, y in R.channel_of.items():
        used.setdefault(y, []).append(i)
    for y in CHANNEL_YS:
        who = used.get(y, [])
        names = ['%s -> %s' % (spec['edges'][i]['from'], spec['edges'][i]['to']) for i in who]
        flag = '  <-- DEFECT: %d edges share this channel' % len(who) if len(who) > 1 else ''
        print('      y=%-4d %s%s' % (y, ', '.join(names) if names else '(free)', flag))
    for i in range(len(spec['edges'])):
        e = spec['edges'][i]
        print('      %-14s %-22s -> %-22s %s'
              % ('k_e%d' % i, e['from'], e['to'], R.kind_of.get(i, '?')))


def main():
    argv = sys.argv[1:]
    args = [a for a in argv if not a.startswith('--')]
    spec = load_spec(args[0])
    out_dir = None
    for i, a in enumerate(argv):
        if a == '--out' and i + 1 < len(argv):
            out_dir = argv[i + 1]
        elif a.startswith('--out='):
            out_dir = a.split('=', 1)[1]
    if out_dir is None:
        out_dir = os.path.join(os.path.dirname(os.path.abspath(args[0])), '..', 'out')
    target = os.path.normpath(os.path.join(out_dir, spec['file']))
    want = {}
    xml = emit(spec, want)
    n = splice(target, xml)
    print('wrote class page of %s (sequence page unchanged, %d bytes)' % (target, n))
    print('de-confliction rounds used: %d of %d'
          % (want['router'].rounds, NUDGE_ROUNDS))
    if '--report' in sys.argv:
        report(spec, want['layout'], want['router'], want['routes'])


if __name__ == '__main__':
    main()
