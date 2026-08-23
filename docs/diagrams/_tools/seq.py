"""Sequence page generator.

Layout constants measured from the golden reference Login.drawio:
  header y=30 h=40 (actor/db y=10)   x[i+1] = x[i] + w[i] + 20
  y-step 30                          activation bar w=10 at cx-5
"""
from emit import (Ids, vertex, free_edge, free_edge_poly, page, text_w, S_ACTOR, S_BOX,
                  S_DB, S_ACT, S_LIFE, S_CALL, S_RET, S_FRAME, S_GUARD, S_DIV, S_NOTE)

GAP = 20          # horizontal gap between participant boxes
STEP = 30         # vertical distance between messages
FRAME_PAD_T = 8   # frame top edge -> guard row
GUARD_H = 20      # height of the guard row
GUARD_GAP = 20    # guard row -> first message of the section
FRAME_PAD_B = 20  # last message of a frame -> frame bottom edge
AFTER_FRAME = 20  # frame bottom edge -> next message, so its label clears the border
SEC_GAP = 30      # space around a section divider
LABEL_H = 14      # a message label sits in [y - LABEL_H, y] (verticalAlign=bottom)
HEAD_Y = 30
HEAD_H = 40
TOP_Y = 110       # y of the first message
BAR_W = 10
MARGIN_R = 135
FOOT_GAP = 165

# A self-message is a UML hook, not a stub poking sideways off the
# activation bar: out from the bar, down, back into the bar with the
# arrowhead pointing left. HOOK_H (the down leg) reuses STEP so the hook's
# own rhythm matches every other line on the page; SELF_STEP gives it a
# second STEP of clearance below so the hook's bottom leg and label never
# collide with whatever message comes next.
HOOK_W = 40
HOOK_H = STEP
SELF_STEP = STEP + HOOK_H


def layout_x(parts):
    x = 45
    for p in parts:
        p["x"] = x
        p["cx"] = x + p["w"] / 2
        x += p["w"] + GAP
    return x - GAP


def _walk(flow, y, acc, frames, num, prefix=""):
    """Assign y to every message; collect frames. Returns next free y."""
    i = 0
    for item in flow:
        if "frame" in item:
            f = {"type": item["frame"], "y": y, "secs": []}
            frames.append(f)
            y += FRAME_PAD_T
            for si, sec in enumerate(item["sections"]):
                if si:
                    f["secs"].append({"divider_y": y})
                    y += SEC_GAP
                # The guard owns a row of its own. The first message of the
                # section starts below it, so the guard can never be buried
                # under a message label (which occupies [y - LABEL_H, y]).
                f["secs"].append({"guard": sec["guard"], "y": y})
                y += GUARD_H + GUARD_GAP
                y = _walk(sec["body"], y, acc, frames, num, prefix)
            y += FRAME_PAD_B
            f["h"] = y - f["y"]
            y += AFTER_FRAME
        else:
            m = dict(item)
            m["y"] = y
            acc.append(m)
            y += SELF_STEP if item.get("self") else STEP
        i += 1
    return y


def _bars(parts, msgs):
    """Activation bar span per participant: from first touch to last touch.
    A self-message's hook physically extends to y + HOOK_H, so the bar has
    to reach that far too, or the hook's down/return legs would hang off
    the end of the bar."""
    span = {}
    for m in msgs:
        bottom = m["y"] + HOOK_H if m.get("self") else m["y"]
        for k in (m["d"], m["t"]):
            lo, hi = span.get(k, (m["y"], m["y"]))
            span[k] = (min(lo, m["y"]), max(hi, bottom))
    return span


def build(spec):
    parts = spec["participants"]
    for p in parts:
        p.setdefault("kind", "box")
        p.setdefault("w", 160)
    layout_x(parts)

    msgs, frames = [], []
    end_y = _walk(spec["flow"], TOP_Y, msgs, frames, [0])
    page_h = int(end_y + FOOT_GAP + 60)
    page_w = int(parts[-1]["x"] + parts[-1]["w"] + MARGIN_R)

    ids = Ids("s")
    body = []

    def headers(y_box, y_ico):
        for p in parts:
            if p["kind"] == "actor":
                body.append(vertex(ids("a"), p["name"], int(p["cx"] - 15), y_ico, 30, 45,
                                   S_ACTOR))
            elif p["kind"] == "db":
                body.append(vertex(ids("db"), p["name"], int(p["cx"] - p["w"] / 2), y_ico,
                                   int(p["w"]), 55, S_DB))
            else:
                body.append(vertex(ids("h"), p["name"], int(p["x"]), y_box, int(p["w"]),
                                   HEAD_H, S_BOX))

    headers(HEAD_Y, 10)
    foot = page_h - FOOT_GAP
    headers(foot, foot - 20)

    # lifelines
    for p in parts:
        body.append(free_edge(ids("l"), "", int(p["cx"]), 70, int(p["cx"]), foot - 25,
                              S_LIFE))

    # activation bars
    for idx, (lo, hi) in _bars(parts, msgs).items():
        p = parts[idx]
        body.append(vertex(ids("b"), "", int(p["cx"] - BAR_W / 2), int(lo - 10), BAR_W,
                           int(hi - lo + 20), S_ACT))

    # frames + guards + dividers
    xs = [p["cx"] for p in parts]
    for f in frames:
        # Strict upper bound: the message that follows the frame sits exactly
        # on f.y + f.h, and counting it stretched the frame across lifelines
        # it has nothing to do with.
        touched = [m for m in msgs if f["y"] <= m["y"] < f["y"] + f["h"]]
        if touched:
            lo = min(min(xs[m["d"]], xs[m["t"]]) for m in touched) - 60
            hi = max(max(xs[m["d"]], xs[m["t"]]) for m in touched) + 60
        else:
            lo, hi = 100, page_w - 100
        f["x"], f["w"] = int(lo), int(hi - lo)
        body.append(vertex(ids("f"), f["type"], f["x"], int(f["y"]), f["w"], int(f["h"]),
                           S_FRAME))
        for s in f["secs"]:
            if "divider_y" in s:
                body.append(free_edge(ids("dv"), "", f["x"], int(s["divider_y"]),
                                      f["x"] + f["w"], int(s["divider_y"]), S_DIV))
            else:
                gw = int(text_w(s["guard"], 11, bold=True)) + 16
                body.append(vertex(ids("g"), s["guard"], f["x"] + 70, int(s["y"]),
                                   gw, GUARD_H, S_GUARD))

    # messages
    for m in msgs:
        a, b = xs[m["d"]], xs[m["t"]]
        st = S_RET if m["k"] == "ret" else S_CALL
        if m.get("self"):
            # UML hook: out from the activation bar, down, back into the bar
            # with the arrowhead pointing left -- not a ~10px stub jogging
            # sideways off the bar into empty space.
            x0 = a + BAR_W / 2
            x1 = x0 + HOOK_W
            y0, y1 = m["y"], m["y"] + HOOK_H
            body.append(free_edge_poly(ids("m"), m["l"],
                                       [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], st))
        else:
            sx = a + 5 if b > a else a - 5
            tx = b - 5 if b > a else b + 5
            body.append(free_edge(ids("m"), m["l"], int(sx), int(m["y"]), int(tx),
                                  int(m["y"]), st))

    for n in spec.get("notes", []):
        body.append(vertex(ids("n"), n["text"], int(n["x"]), int(n["y"]),
                           int(n.get("w", 420)), int(n.get("h", 60)), S_NOTE))

    # uc_label carries the fully-formed UC segment so one key covers all four shapes:
    #   "[UC-41]"  ·  "[UC-32, 33, 39]"  ·  "(UC-33 sub-flow)"  ·  "(UC-42, 56 sub-flow)"
    # Falls back to the legacy uc_id prefix only when uc_label is absent.
    uc = (spec.get("uc_label") or "").strip()
    bl = ",".join(f"#{b}" for b in spec.get("backlog", []))
    if uc:
        name = f"Sequence - {spec['title']} {uc}"
    else:
        legacy = (spec.get("uc_id") or "").strip()
        name = f"Sequence - {legacy + ' ' if legacy else ''}{spec['title']}"
    if bl:
        name += f" [BL{bl}]"
    return page(f"seq-{spec['feature'].lower()}", name, page_w, page_h, body), len(msgs)
