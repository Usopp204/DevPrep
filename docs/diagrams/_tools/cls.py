"""Class page generator.

Boxes are grouped into columns. Interfaces are forced directly above their
implementer (same x) so the realize arrow is a short vertical segment.

Edge routing is corridor-based and collision-checked. Three rules drive it:

  * ONE global pool of routed coordinates. Every vertical line gets an x no
    other vertical line on the page uses, at least LANE apart; same for
    horizontal lines and y. The old code kept a counter per (source-x,
    target-x) pair while deriving the base from the individual box width, so
    two counters could hand out the same x, and two boxes of different widths
    in one column produced lines a few px apart.
  * A segment is only accepted if it crosses no class box other than its own
    source/target. Boxes are inflated by CLEARANCE while routing so lines never
    graze a border.
  * Short routes win. A direct vertical (interface above its implementer) or a
    3-segment hop stays as-is whenever it is already clean; only an edge that
    would cut through a box is promoted to the 5-segment route
    (out -> vertical corridor -> horizontal lane -> vertical corridor -> in).
    That keeps the many already-correct short edges untouched.
"""
import math

from emit import (Ids, vertex, child, cell_edge, page, text_w, S_CLASS, S_MEMBER,
                  S_LINE, REL, CLASS_MEMBER_H, CLASS_LINE_H, CLASS_TITLE_H)

MEMBER_PAD = 22   # S_MEMBER spacingLeft 6 + spacingRight 6 + slack
TITLE_PAD = 30    # the title is centred and bold, give it more room
WIDTH_SAFETY = 1.04
COL_GAP = 100     # corridor width between columns
ROW_GAP = 60      # vertical gap between boxes in a column
LANE = 18         # minimum spacing between any two routed lines
CLEARANCE = 10    # keep routed lines this far clear of every box while routing
STEP = 3          # granularity of the candidate scan
TRIES = 12        # how many corridor candidates to re-try before giving up
X0, Y0 = 40, 110  # Y0 leaves a band above the boxes free for horizontal lanes


class RoutingError(Exception):
    """No collision-free corridor left. Raised instead of drawing a broken edge."""


def box_h(c):
    n = len(c.get("fields", [])) + len(c.get("methods", []))
    h = CLASS_TITLE_H + n * CLASS_MEMBER_H
    if c.get("fields") and c.get("methods"):
        h += CLASS_LINE_H
    return max(h, 40)


def content_w(c):
    """Width the box needs so neither its title nor any member is clipped."""
    need = text_w(c["name"], 12, bold=True) + TITLE_PAD
    for m in list(c.get("fields", [])) + list(c.get("methods", [])):
        need = max(need, text_w(m, 11) + MEMBER_PAD)
    return int(math.ceil(need * WIDTH_SAFETY / 10.0) * 10)


def auto_layout(classes, relations):
    """Place boxes when the spec gives no explicit x/y.

    Every box in a column gets the SAME width - the column's widest content.
    Uniform width keeps an interface and its implementer centred on the same
    x, so the realize arrow stays a short vertical line instead of needing a
    detour around the offset."""
    impl = {r["from"]: r["to"] for r in relations if r["type"] == "implements"}
    cols = {}
    for c in classes:
        cols.setdefault(c.get("column", 0), []).append(c)
    x = X0
    for ci in sorted(cols):
        group = cols[ci]
        w = max(max(c.get("w", 0), content_w(c)) for c in group)
        ordered, done = [], set()
        for c in group:                      # interface immediately above impl
            if c["name"] in done:
                continue
            iface = impl.get(c["name"])
            if iface:
                for o in group:
                    if o["name"] == iface and o["name"] not in done:
                        ordered.append(o)
                        done.add(o["name"])
            ordered.append(c)
            done.add(c["name"])
        y = Y0
        for c in ordered:
            c["x"], c["y"], c["w"] = x, y, w
            y += box_h(c) + ROW_GAP
        x += w + COL_GAP
    return classes


def _hits(p, q, box, inflate):
    """Does segment p-q enter box (grown by inflate)? Touching a border is fine."""
    x1, x2 = sorted((p[0], q[0]))
    y1, y2 = sorted((p[1], q[1]))
    return (x1 < box["x"] + box["w"] + inflate and box["x"] - inflate < x2
            and y1 < box["y"] + box["h"] + inflate and box["y"] - inflate < y2)


class _Router:
    def __init__(self, classes, page_w, page_h):
        self.classes = classes
        self.page_w, self.page_h = page_w, page_h
        self.used_v, self.used_h = [], []

    # -- geometry helpers -------------------------------------------------
    def _clean(self, poly, exempt, inflate=CLEARANCE):
        for i in range(len(poly) - 1):
            for c in self.classes:
                if c["name"] in exempt:
                    continue
                if _hits(poly[i], poly[i + 1], c, inflate):
                    return False
        return True

    @staticmethod
    def _fits(cand, used, extra=()):
        """cand = (coord, lo, hi). Two parallel lines may only share a coordinate
        (or sit closer than LANE) when their spans are at least LANE apart, so
        they can never be read as one continuous line."""
        v, lo, hi = cand
        for u, ulo, uhi in list(used) + list(extra):
            if abs(v - u) >= LANE:
                continue
            if lo <= uhi + LANE and hi >= ulo - LANE:
                return False
        return True

    def _scan(self, lo, hi, pref):
        """Candidate coordinates inside (lo, hi), nearest to pref first."""
        lo, hi = int(lo), int(hi)
        if hi < lo:                       # a band exactly one point wide is usable
            return []
        return sorted(range(lo, hi + 1, STEP), key=lambda v: abs(v - pref))

    def _gap_right_of(self, x_edge):
        """Right bound of the empty band starting at x_edge."""
        nxt = [c["x"] for c in self.classes if c["x"] >= x_edge - 1]
        return min(nxt) if nxt else self.page_w

    def _gap_left_of(self, x_edge):
        """Left bound of the empty band ending at x_edge."""
        prv = [c["x"] + c["w"] for c in self.classes if c["x"] + c["w"] <= x_edge + 1]
        return max(prv) if prv else 0

    # -- routes -----------------------------------------------------------
    def _vertical(self, a, b, exempt):
        """Interface directly above/below its implementer: keep the short line."""
        if a["y"] > b["y"]:
            exit_, entry = (0.5, 0), (0.5, 1)
            p = (a["x"] + a["w"] / 2, a["y"])
            q = (b["x"] + b["w"] / 2, b["y"] + b["h"])
        else:
            exit_, entry = (0.5, 1), (0.5, 0)
            p = (a["x"] + a["w"] / 2, a["y"] + a["h"])
            q = (b["x"] + b["w"] / 2, b["y"])
        if abs(p[0] - q[0]) > 0.5 or not self._clean([p, q], exempt):
            return None
        return exit_, entry, None, [], []

    def _side(self, a, b, exempt):
        """Same column but not directly stacked (something sits between them):
        step out into the corridor beside the column, run vertically past the
        obstacle, come back in on the same side."""
        a_mid, b_mid = a["y"] + a["h"] / 2, b["y"] + b["h"] / 2
        right = max(a["x"] + a["w"], b["x"] + b["w"])
        left = min(a["x"], b["x"])
        options = [
            ((1, 0.5), (1, 0.5), (a["x"] + a["w"], a_mid), (b["x"] + b["w"], b_mid),
             self._scan(right + CLEARANCE, self._gap_right_of(right) - CLEARANCE,
                        right + 30)),
            ((0, 0.5), (0, 0.5), (a["x"], a_mid), (b["x"], b_mid),
             self._scan(self._gap_left_of(left) + CLEARANCE, left - CLEARANCE,
                        left - 30)),
        ]
        for exit_, entry, p, q, cands in options:
            for cx in cands:
                cand = (cx, min(p[1], q[1]), max(p[1], q[1]))
                if not self._fits(cand, self.used_v):
                    continue
                poly = [p, (cx, p[1]), (cx, q[1]), q]
                if self._clean(poly, exempt):
                    return exit_, entry, poly[1:-1], [cand], []
        return None

    def _endpoints(self, a, b):
        a_mid = a["y"] + a["h"] / 2
        b_mid = b["y"] + b["h"] / 2
        if b["x"] > a["x"]:
            return ((a["x"] + a["w"], a_mid), (b["x"], b_mid), (1, 0.5), (0, 0.5), True)
        return ((a["x"], a_mid), (b["x"] + b["w"], b_mid), (0, 0.5), (1, 0.5), False)

    def _three(self, a, b, exempt):
        """out -> vertical corridor -> in. Only when it crosses nothing."""
        p, q, exit_, entry, rightward = self._endpoints(a, b)
        if rightward:
            lo, hi, pref = p[0] + CLEARANCE, q[0] - CLEARANCE, p[0] + 30
        else:
            lo, hi, pref = q[0] + CLEARANCE, p[0] - CLEARANCE, p[0] - 30
        for cx in self._scan(lo, hi, pref):
            cand = (cx, min(p[1], q[1]), max(p[1], q[1]))
            if not self._fits(cand, self.used_v):
                continue
            poly = [p, (cx, p[1]), (cx, q[1]), q]
            if self._clean(poly, exempt):
                return exit_, entry, poly[1:-1], [cand], []
        return None

    def _face(self, box, side):
        """Exit/entry point on one side of a box + the corridor band next to it."""
        mid = box["y"] + box["h"] / 2
        if side == "R":
            x = box["x"] + box["w"]
            return ((x, mid), (1, 0.5),
                    self._scan(x + CLEARANCE, self._gap_right_of(x) - CLEARANCE, x + 30))
        x = box["x"]
        return ((x, mid), (0, 0.5),
                self._scan(self._gap_left_of(x) + CLEARANCE, x - CLEARANCE, x - 30))

    def _five(self, a, b, exempt):
        """out -> vertical -> horizontal lane -> vertical -> in.

        Tries the two sides facing each other first, then the outer sides — a
        box wedged into a 30px gap (Login's ForbiddenException) can only be
        reached by leaving from the far side and going around."""
        facing = ("R", "L") if b["x"] > a["x"] else ("L", "R")
        combos, seen = [facing, ("R", "R"), ("L", "L"), facing[::-1]], set()

        def pick(cands, y_end, anchor, taken):
            for cx in cands:
                span = (cx, min(y_end, anchor), max(y_end, anchor))
                if not self._fits(span, self.used_v, taken):
                    continue
                if self._clean([(cx, y_end), (cx, anchor)], exempt):
                    return cx, span
            return None, None

        for sa, sb in combos:
            if (sa, sb) in seen:
                continue
            seen.add((sa, sb))
            p, exit_, c1 = self._face(a, sa)
            q, entry, c2 = self._face(b, sb)
            lanes = self._scan(CLEARANCE, self.page_h - CLEARANCE, (p[1] + q[1]) / 2)
            for ly in lanes[:TRIES * 40]:
                cx1, v1 = pick(c1, p[1], ly, [])
                if cx1 is None or not self._clean([p, (cx1, p[1])], exempt):
                    continue
                cx2, v2 = pick(c2, q[1], ly, [v1])
                if cx2 is None or not self._clean([(cx2, q[1]), q], exempt):
                    continue
                h1 = (ly, min(cx1, cx2), max(cx1, cx2))
                if not self._fits(h1, self.used_h):
                    continue
                poly = [p, (cx1, p[1]), (cx1, ly), (cx2, ly), (cx2, q[1]), q]
                if self._clean(poly, exempt):
                    return exit_, entry, poly[1:-1], [v1, v2], [h1]
        return None

    def route(self, a, b):
        exempt = {a["name"], b["name"]}
        same_col = abs(a["x"] - b["x"]) <= 5
        order = ([self._vertical, self._side, self._five] if same_col
                 else [self._three, self._five])
        for attempt in order:
            got = attempt(a, b, exempt)
            if got:
                exit_, entry, pts, vs, hs = got
                self.used_v.extend(vs)
                self.used_h.extend(hs)
                return exit_, entry, pts
        raise RoutingError(
            f"khong con hanh lang sach cho edge '{a['name']}' -> '{b['name']}' "
            f"(cot x={a['x']} -> x={b['x']}): da dung {len(self.used_v)} duong doc "
            f"/ {len(self.used_h)} duong ngang, moi duong cach nhau >= {LANE}px. "
            f"Noi rong COL_GAP/ROW_GAP hoac tach bot class sang cot khac.")


def build(spec, feature, title, uc_id="", backlog=(), uc_label=""):
    classes = spec["classes"]
    relations = spec.get("relations", [])
    if any("x" not in c for c in classes):
        auto_layout(classes, relations)
    else:
        # Hand-placed spec: still refuse to clip. A box may only grow, and
        # RULE6/RULE12 fail the build if growing made it collide.
        for c in classes:
            c["w"] = max(c.get("w", 0), content_w(c))

    ids = Ids("c")
    body, ref = [], {}
    for c in classes:
        cid = ids("k")
        ref[c["name"]] = c
        c["id"] = cid
        h = box_h(c)
        c["h"] = h
        body.append(vertex(cid, c["name"], c["x"], c["y"], c["w"], h, S_CLASS))
        y = CLASS_TITLE_H
        for f in c.get("fields", []):
            body.append(child(ids("m"), f, y, c["w"], CLASS_MEMBER_H, S_MEMBER, cid))
            y += CLASS_MEMBER_H
        if c.get("fields") and c.get("methods"):
            body.append(child(ids("ln"), "", y, c["w"], CLASS_LINE_H, S_LINE, cid))
            y += CLASS_LINE_H
        for m in c.get("methods", []):
            body.append(child(ids("m"), m, y, c["w"], CLASS_MEMBER_H, S_MEMBER, cid))
            y += CLASS_MEMBER_H

    pw = max(c["x"] + c["w"] for c in classes) + 100
    ph = max(c["y"] + c["h"] for c in classes) + 100
    router = _Router(classes, pw, ph)
    for r in relations:
        a, b = ref.get(r["from"]), ref.get(r["to"])
        if not a or not b:
            continue
        exit_, entry, pts = router.route(a, b)
        body.append(cell_edge(ids("e"), a["id"], b["id"],
                              REL.get(r["type"], REL["use"]), r.get("label", ""),
                              pts, exit_, entry))

    # See seq.py: uc_label is the fully-formed UC segment; uc_id is the legacy prefix.
    uc = (uc_label or "").strip()
    bl = ",".join(f"#{x}" for x in backlog)
    if uc:
        name = f"Class - {title} {uc}"
    else:
        legacy = (uc_id or "").strip()
        name = f"Class - {legacy + ' ' if legacy else ''}{title}"
    if bl:
        name += f" [BL{bl}]"
    return page(f"cls-{feature.lower()}", name, int(pw), int(ph), body), len(classes)
