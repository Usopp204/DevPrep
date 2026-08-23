"""Validator. Fails the build on any broken rule.

Rule 3 is path-wise, NOT 1:1. The golden reference has 31 calls and 67 returns,
because one call inside an `alt` returns once per branch. Checking a 1:1 ratio
would reject the very file that was approved.

Rule 3 matching is participant-aware: a call {d: a, t: b} is closed only by a
return {d: b, t: a} (the reversed pair), never by whatever happens to sit on top
of the stack. A positional LIFO pop had two failure modes:
  - false positive: a self-message sitting on the stack swallowed the return that
    belonged to the real call underneath it;
  - false negative: a return between the wrong two participants still popped, so a
    genuinely unclosed call looked balanced.
Self-messages neither open nor close a call, so they never touch the stack.
"""
import html
import re
import sys
import xml.etree.ElementTree as ET

from emit import text_w


def _escapes(flow, stack, pre_ids):
    """Lightweight pre-pass: does walking `flow` (on a private copy of
    `stack`) ever close a call that predates `pre_ids`? No error collection,
    no leaf tracking -- just the yes/no answer `_walk` needs BEFORE it can
    decide what a section's own ending means, which avoids a chicken-and-egg
    problem: a section's fate (dead end vs. "continue into what follows the
    frame") has to be known before we can correctly tell whether reaching
    the end of its body is a real leaf, but that fate is itself only knowable
    by walking the section. Structure mirrors `_walk`'s own threading rules
    (opt has an implicit "guard false" continuation, an escaping section
    doesn't feed into what follows) so the two never disagree about which
    paths exist.
    """
    for i, item in enumerate(flow):
        if "frame" in item:
            rest = flow[i + 1:]
            if item["frame"] == "opt" and _escapes(rest, list(stack), pre_ids):
                return True
            for sec in item["sections"]:
                sub_stack = list(stack)
                sub_pre_ids = set(id(x) for x in sub_stack)
                sub_escaped = _escapes(sec["body"], sub_stack, sub_pre_ids)
                if item["frame"] == "opt" and sub_escaped:
                    continue          # dead end -- doesn't feed into `rest`
                if _escapes(rest, sub_stack, pre_ids):
                    return True
            return False
        if item.get("self"):
            continue
        if item.get("k") == "call":
            stack.append(item)
        elif item.get("k") == "ret":
            for i2 in range(len(stack) - 1, -1, -1):
                if stack[i2]["d"] == item["t"] and stack[i2]["t"] == item["d"]:
                    popped = stack.pop(i2)
                    if id(popped) in pre_ids:
                        return True
                    break
            else:
                break                  # unmatched return -- _walk reports it
    return False


def _walk(flow, stack, pre_ids, errs, out, trail, finish):
    """Walk one message list start to finish, mutating `stack`, and recurse
    into every frame it contains -- INCLUDING whatever comes after that frame
    in the same list (the original bug: a bare `return` right after the
    per-section recursion exited the whole function on the first frame it
    saw, so nothing past it -- sibling messages, sibling frames, or anything
    inside a section that itself contained a later frame -- was ever
    checked).

    `pre_ids` is the set of id()s of call-frames already open when THIS list
    began -- see `_escapes` -- which is what distinguishes the two frame
    semantics:

      opt  - the guard may or may not fire. The "guard false" branch always
             continues into whatever follows the opt, unchanged. A "guard
             true" section only continues into what follows the opt if it
             stayed self-contained (`_escapes` said no). The moment a
             section's return reaches past its own scope and closes a call
             that predates the opt -- an early exit, e.g. F1-F5's "throw all
             the way back to the actor" -- that branch is a dead end: it is
             checked for its own internal consistency and then left alone,
             exactly like reaching the true end of the diagram. Threading an
             early-exit section's emptied-out stack into the rest of the
             flow would produce phantom "return doesn't match any call"
             errors for messages that can never actually run once that
             exception has already unwound everything.

      alt  - exhaustive: exactly one section always runs, there is no skip
             branch, and every section must land on the identical resulting
             stack (RULE3c) since whichever one fires, the same messages
             follow.

    Rather than returning "the leaf this list reaches", control is handed to
    `finish(trail, stack)` once `flow` is exhausted -- this is what lets a
    section's own natural completion be treated correctly: a dead-end
    (escaped) section's `finish` appends straight to `out` (a genuine leaf,
    unrelated to whatever follows the frame); every other section's `finish`
    IS "go on and process what follows the frame" (`resume`, reusing
    whatever `finish` the frame itself was given) -- so a section that
    merely does local, self-closing work (#15's `opt J`) or an `alt` section
    (which always threads) doesn't get double-counted as a leaf AND a
    continuation.
    """
    for i, item in enumerate(flow):
        if "frame" in item:
            rest = flow[i + 1:]

            def resume(rtrail, rstack, _rest=rest, _pre=pre_ids, _fin=finish):
                _walk(_rest, rstack, _pre, errs, out, rtrail, _fin)

            if item["frame"] == "opt":
                resume(f"{trail}/skip", list(stack))

            # RULE3c needs each alt section's TRUE final stack -- not just
            # whatever `sub_stack` happens to hold right after the `_walk`
            # call returns. That call returns as soon as it meets a frame OF
            # ITS OWN, at which point the rest of the section's body keeps
            # running on a COPY made inside `resume` (`list(stack)`), so
            # `sub_stack` itself would freeze at the point the nested frame
            # was hit -- stale the moment a section has its own internal
            # branching. Recording the stack right where `resume` actually
            # fires (however deep) is the only state that reflects what
            # really continues afterward.
            section_ends = []
            for si, sec in enumerate(item["sections"]):
                sub_trail = f"{trail}/{item['frame']}[{si}]"
                sub_stack = list(stack)
                sub_pre_ids = set(id(x) for x in sub_stack)
                sub_escaped = _escapes(sec["body"], list(sub_stack), sub_pre_ids)
                if item["frame"] == "opt" and sub_escaped:
                    _walk(sec["body"], sub_stack, sub_pre_ids, errs, out, sub_trail,
                          lambda t, s: out.append((t, list(s))))
                elif item["frame"] == "alt":
                    def alt_finish(rtrail, rstack, _resume=resume):
                        section_ends.append((rtrail, list(rstack)))
                        _resume(rtrail, rstack)
                    _walk(sec["body"], sub_stack, sub_pre_ids, errs, out, sub_trail,
                          alt_finish)
                else:
                    _walk(sec["body"], sub_stack, sub_pre_ids, errs, out, sub_trail,
                          resume)

            if item["frame"] == "alt" and len(section_ends) > 1:
                key = lambda s: tuple((m["d"], m["t"]) for m in s)
                keys = {key(s) for _, s in section_ends}
                if len(keys) > 1:
                    parts = "; ".join(f"{t}: con mo {[m.get('l') for m in s]}"
                                       for t, s in section_ends)
                    errs.append(f"RULE3c cac nhanh alt '{trail}' hoi tu ve "
                                f"ngan xep khac nhau sau khung: {parts}")
            return
        if item.get("self"):
            continue                      # exempt: opens nothing, closes nothing
        if item.get("k") == "call":
            stack.append(item)
        elif item.get("k") == "ret":
            # Close the most recent still-open call between the same two
            # participants, in the opposite direction.
            for i2 in range(len(stack) - 1, -1, -1):
                if stack[i2]["d"] == item["t"] and stack[i2]["t"] == item["d"]:
                    del stack[i2]
                    break
            else:
                errs.append(f"RULE3b return khong khop call nao tren nhanh "
                            f"'{trail or 'main'}': {item.get('l')}")
    finish(trail, stack)


def _check_guards(flow, errs, trail="flow"):
    """RULE13 - every alt/opt section needs a guard. A frame drawn without one
    tells the reader nothing about WHEN that branch runs."""
    for i, item in enumerate(flow):
        if "frame" not in item:
            continue
        where = f"{trail}[{i}] {item['frame']}"
        for si, sec in enumerate(item.get("sections") or []):
            if not (sec.get("guard") or "").strip():
                errs.append(f"RULE13 khung '{where}' section {si} thieu guard")
            _check_guards(sec.get("body") or [], errs, f"{where}.sections[{si}]")


def check_spec(spec):
    errs = []
    _check_guards(spec["flow"], errs)
    paths = []
    _walk(spec["flow"], [], set(), errs, paths, "",
          lambda t, s: paths.append((t, list(s))))
    for trail, left in paths:
        for m in left:
            errs.append(f"RULE3 call chua co return tren nhanh '{trail or 'main'}': "
                        f"{m.get('l')}")
    n = len(spec["participants"])
    for item in _flat(spec["flow"]):
        for k in ("d", "t"):
            if not (0 <= item[k] < n):
                errs.append(f"participant index ngoai pham vi: {item}")
    return errs


def _flat(flow):
    for item in flow:
        if "frame" in item:
            for sec in item["sections"]:
                yield from _flat(sec["body"])
        else:
            yield item


def check_file(path):
    errs = []
    raw = open(path, encoding="utf-8").read()
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as e:
        return [f"RULE1 XML khong parse duoc: {e}"]

    pages = root.findall("diagram")
    if len(pages) != 2:
        errs.append(f"RULE7 phai co dung 2 trang, dang co {len(pages)}")
    for p in pages:
        nm = p.get("name") or ""
        if not (nm.startswith("Sequence - ") or nm.startswith("Class - ")):
            errs.append(f"RULE7 ten trang sai dinh dang: {nm}")

    boxes_all = []
    for p in pages:
        cells = p.find("mxGraphModel").find("root").findall("mxCell")
        present = {c.get("id") for c in cells}
        ids = set()          # ids are scoped per page in draw.io ("0"/"1" repeat)
        for c in cells:
            i = c.get("id")
            if i in ids:
                errs.append(f"RULE2 id trung trong 1 trang: {i}")
            ids.add(i)
            for a in ("parent", "source", "target"):
                v = c.get(a)
                if v and v not in present:
                    errs.append(f"RULE2 {a}='{v}' khong ton tai (cell {i})")
            st = c.get("style") or ""
            if st and not c.get("edge") and "fillColor" in st:
                if "fillColor=#ffffff" not in st and "fillColor=none" not in st:
                    errs.append(f"RULE5 mau khong phai trang: {i}")
                if "strokeColor" in st and "strokeColor=#000000" not in st \
                        and "strokeColor=none" not in st:
                    errs.append(f"RULE5 vien khong phai den: {i}")

        if (p.get("name") or "").startswith("Class - "):
            boxes = []
            for c in cells:
                if "swimlane" in (c.get("style") or ""):
                    g = c.find("mxGeometry")
                    boxes.append((c.get("value"), float(g.get("x")), float(g.get("y")),
                                  float(g.get("width")), float(g.get("height"))))
            boxes_all = boxes
            for i in range(len(boxes)):
                for j in range(i + 1, len(boxes)):
                    if _overlap(boxes[i], boxes[j]):
                        errs.append(f"RULE6 hop class chong nhau: "
                                    f"{boxes[i][0]} <-> {boxes[j][0]}")
            _check_class_geometry(cells, errs)
            _check_class_text_fits(cells, errs)
        else:
            _check_seq_labels(cells, errs)
        _check_escaping(cells, errs)

    for m in re.finditer(r'value="([^"]*)"', raw):
        v = m.group(1)
        if re.search(r"(?<!&amp;)&lt;", v) or re.search(r"(?<!&amp;)&gt;", v):
            errs.append(f"RULE4 escape 1 lan (se bi nuot chu): {v[:60]}")
    return errs


def _overlap(a, b):
    _, ax, ay, aw, ah = a
    _, bx, by, bw, bh = b
    return not (ax + aw <= bx or bx + bw <= ax or ay + ah <= by or by + bh <= ay)


# --- geometry of the class page -----------------------------------------
# A green VALIDATE used to be meaningless for layout: edges ran straight
# through class boxes and two edges could share one line, and nothing caught
# it until a human looked at the PNG. These rules catch it at build time.

LANE = 18       # minimum spacing between two routed lines
TOL = 1.0       # a line touching a border is fine, only real overlap is a fault


def _seg_box(p, q, b):
    """Does segment p-q enter box b? Borders are shrunk by TOL, so a line
    running along an edge of the box is not reported."""
    _, bx, by, bw, bh = b
    x1, x2 = sorted((p[0], q[0]))
    y1, y2 = sorted((p[1], q[1]))
    return (x1 < bx + bw - TOL and bx + TOL < x2
            and y1 < by + bh - TOL and by + TOL < y2)


def _on_box(pt, b):
    _, bx, by, bw, bh = b
    return (bx - TOL <= pt[0] <= bx + bw + TOL
            and by - TOL <= pt[1] <= by + bh + TOL)


def _polyline(cell, boxes):
    """Rebuild what draw.io draws: exit point -> waypoints -> entry point."""
    st = cell.get("style") or ""

    def frac(key, default):
        m = re.search(key + r"=([-0-9.]+)", st)
        return float(m.group(1)) if m else default

    s, t = boxes.get(cell.get("source")), boxes.get(cell.get("target"))
    if not s or not t:
        return None
    p0 = (s[1] + frac("exitX", 0.5) * s[3], s[2] + frac("exitY", 0.5) * s[4])
    p1 = (t[1] + frac("entryX", 0.5) * t[3], t[2] + frac("entryY", 0.5) * t[4])
    mid = [(float(m.get("x")), float(m.get("y")))
           for m in cell.findall("./mxGeometry/Array/mxPoint")]
    return [p0] + mid + [p1]


MEMBER_SIDE_PAD = 12    # S_MEMBER spacingLeft 6 + spacingRight 6
LABEL_H = 14            # a message label occupies [y - LABEL_H, y]


def _rendered(value):
    """What the reader actually sees. ElementTree already undid the XML layer,
    draw.io then undoes the HTML layer because the styles carry html=1."""
    return html.unescape(value or "")


def _check_escaping(cells, errs):
    """RULE14 - a label that still shows an entity after both decodes was
    escaped twice: the YAML already contained &lt; and the generator escaped
    it again. Note a lone '&' is legitimate (e.g. a query string
    '?sessionId&modality=face'), so only leftover ENTITY text is a fault."""
    for c in cells:
        shown = _rendered(c.get("value"))
        for bad in ("&lt;", "&gt;", "&amp;"):
            if bad in shown:
                errs.append(f"RULE14 nhan bi escape 2 lan (hien nguyen chu "
                            f"'{bad}'): {shown[:70]}")
                break


def _check_class_text_fits(cells, errs):
    """RULE12 - S_MEMBER has overflow=hidden, so a member wider than its box is
    silently chopped mid-word. Measure instead of trusting the box width."""
    boxes = {}
    for c in cells:
        if "swimlane" in (c.get("style") or ""):
            boxes[c.get("id")] = (_rendered(c.get("value")),
                                  float(c.find("mxGeometry").get("width")))
    for name, w in boxes.values():
        need = text_w(name, 12, bold=True)
        if need > w:
            errs.append(f"RULE12 ten class '{name}' rong {need:.0f}px > hop {w:.0f}px")
    for c in cells:
        if not (c.get("style") or "").startswith("text;strokeColor=none"):
            continue
        box = boxes.get(c.get("parent"))
        if not box:
            continue
        member = _rendered(c.get("value"))
        need = text_w(member, 11) + MEMBER_SIDE_PAD
        if need > box[1]:
            errs.append(f"RULE12 class '{box[0]}' bi cat chu: can {need:.0f}px, "
                        f"hop rong {box[1]:.0f}px -> '{member}'")


def _check_seq_labels(cells, errs):
    """RULE15 - a frame guard and a message label must not fight for the same
    pixels. They used to: the guard sat at (first message y - 22) and the
    label occupies [y - 14, y], so every guard was partly buried."""
    guards, labels = [], []
    for c in cells:
        st = c.get("style") or ""
        val = _rendered(c.get("value"))
        if not val:
            continue
        if st.startswith("text;html=1;align=left"):
            g = c.find("mxGeometry")
            gx, gy = float(g.get("x")), float(g.get("y"))
            guards.append((val, gx, gx + text_w(val, 11, bold=True),
                           gy, gy + float(g.get("height"))))
        elif c.get("edge"):
            g = c.find("mxGeometry")
            pts = {p.get("as"): p for p in g.findall("mxPoint")}
            if "sourcePoint" not in pts or "targetPoint" not in pts:
                continue
            x1 = float(pts["sourcePoint"].get("x"))
            x2 = float(pts["targetPoint"].get("x"))
            y1 = float(pts["sourcePoint"].get("y"))
            y2 = float(pts["targetPoint"].get("y"))
            if y1 != y2:
                # A self-message's hook: source and target sit at different
                # heights (it leaves and re-enters the same activation bar),
                # so it isn't one horizontal line and this label math -- built
                # for a straight call/return -- doesn't describe where its
                # label actually renders.
                continue
            half = text_w(val, 11) / 2
            mid = (x1 + x2) / 2
            labels.append((val, mid - half, mid + half, y1 - LABEL_H, y1))
    for gv, gx1, gx2, gy1, gy2 in guards:
        for lv, lx1, lx2, ly1, ly2 in labels:
            if gx1 < lx2 and lx1 < gx2 and gy1 < ly2 and ly1 < gy2:
                errs.append(f"RULE15 guard '{gv[:40]}' va nhan '{lv[:40]}' "
                            f"de len nhau (guard y={gy1:.0f}-{gy2:.0f}, "
                            f"nhan y={ly1:.0f}-{ly2:.0f})")


def _check_class_geometry(cells, errs):
    boxes = {}
    for c in cells:
        if "swimlane" in (c.get("style") or ""):
            g = c.find("mxGeometry")
            boxes[c.get("id")] = (c.get("value"), float(g.get("x")), float(g.get("y")),
                                  float(g.get("width")), float(g.get("height")))

    corridors = []          # segments that run in open space, away from their own boxes
    for cell in cells:
        if not cell.get("edge") or not cell.get("source") or not cell.get("target"):
            continue
        poly = _polyline(cell, boxes)
        if poly is None:
            continue
        own = [boxes[i] for i in (cell.get("source"), cell.get("target")) if i in boxes]
        name = f"{own[0][0]} -> {own[-1][0]}" if len(own) == 2 else cell.get("id")
        for p, q in zip(poly, poly[1:]):
            if p == q:
                continue
            vertical, horizontal = p[0] == q[0], p[1] == q[1]
            if not (vertical or horizontal):
                errs.append(f"RULE11 edge '{name}' co doan xien {p}-{q} "
                            f"(draw.io se tu be goc, layout khong kiem soat duoc)")
                continue
            # RULE9 — an edge may only touch its own two boxes.
            for bid, b in boxes.items():
                if bid in (cell.get("source"), cell.get("target")):
                    continue
                if _seg_box(p, q, b):
                    errs.append(f"RULE9 edge '{name}' doan "
                                f"({p[0]:.0f},{p[1]:.0f})-({q[0]:.0f},{q[1]:.0f}) "
                                f"cat qua hop class '{b[0]}'")
            # Stubs leaving/entering their own box are allowed to share a line
            # (every edge out of one box starts at the same border point); only
            # the free-running corridor part must be unique.
            if any(_on_box(e, b) for e in (p, q) for b in own):
                continue
            corridors.append((name, vertical, p[0] if vertical else p[1],
                              min(p[1], q[1]) if vertical else min(p[0], q[0]),
                              max(p[1], q[1]) if vertical else max(p[0], q[0])))

    # Two parallel corridor lines are only a fault when they would READ as one
    # line: closer than LANE on the perpendicular axis AND overlapping (or
    # nearly touching) along their own axis. Two short segments on the same x
    # but 200px apart vertically are two visibly separate lines, not a merge.
    for i in range(len(corridors)):
        for j in range(i + 1, len(corridors)):
            n1, v1, c1, lo1, hi1 = corridors[i]
            n2, v2, c2, lo2, hi2 = corridors[j]
            if v1 != v2 or n1 == n2 or abs(c1 - c2) >= LANE:
                continue
            if lo1 > hi2 + LANE or hi1 < lo2 - LANE:
                continue
            axis = "doc x" if v1 else "ngang y"
            shared = "trung khit" if abs(c1 - c2) < TOL else f"chi cach {abs(c1 - c2):.0f}px"
            errs.append(f"RULE10 hai duong {axis}={c1:.0f} / {c2:.0f} {shared} "
                        f"(< {LANE}px) va chong lan doan "
                        f"[{max(lo1, lo2):.0f}..{min(hi1, hi2):.0f}]: "
                        f"edge '{n1}' va edge '{n2}'")


if __name__ == "__main__":
    e = check_file(sys.argv[1])
    print("\n".join(e) if e else "OK - khong loi")
    sys.exit(1 if e else 0)
