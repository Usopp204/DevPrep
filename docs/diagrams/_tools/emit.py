"""Low-level draw.io XML emitters. Single source of truth for escaping and styles."""

WHITE = "fillColor=#ffffff;strokeColor=#000000;"

S_ACTOR = ("shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;"
           "outlineConnect=0;" + WHITE)
S_BOX = "rounded=0;whiteSpace=wrap;html=1;" + WHITE
S_DB = ("shape=cylinder3;whiteSpace=wrap;html=1;boundedLbl=1;backgroundOutline=1;size=15;"
        + WHITE)
S_ACT = "rounded=0;html=1;" + WHITE
S_LIFE = "html=1;dashed=1;endArrow=none;strokeColor=#000000;"
S_CALL = ("html=1;rounded=0;endArrow=block;endFill=1;strokeColor=#000000;fontSize=11;"
          "verticalAlign=bottom;")
S_RET = ("html=1;rounded=0;dashed=1;endArrow=open;endFill=0;strokeColor=#000000;fontSize=11;"
         "verticalAlign=bottom;")
S_FRAME = ("shape=umlFrame;whiteSpace=wrap;html=1;width=60;height=25;" + WHITE)
S_GUARD = "text;html=1;align=left;verticalAlign=middle;fontStyle=1;fontSize=11;"
S_DIV = "html=1;dashed=1;endArrow=none;strokeColor=#000000;"
S_NOTE = "shape=note;whiteSpace=wrap;html=1;size=14;verticalAlign=top;align=left;" + WHITE

S_CLASS = ("swimlane;fontStyle=1;align=center;verticalAlign=top;childLayout=stackLayout;"
           "horizontal=1;startSize=26;horizontalStack=0;resizeParent=1;resizeParentMax=0;"
           "rounded=0;shadow=0;strokeWidth=1;fillColor=#ffffff;swimlaneFillColor=#ffffff;"
           "fontSize=12;html=1;")
S_MEMBER = ("text;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;"
            "spacingLeft=6;spacingRight=6;overflow=hidden;points=[[0,0.5],[1,0.5]];"
            "portConstraint=eastwest;rounded=0;fontSize=11;html=1;")
S_LINE = ("line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;"
          "spacingLeft=3;spacingRight=3;rotatable=0;labelPosition=right;points=[];"
          "portConstraint=eastwest;strokeColor=#000000;")
S_EDGE = "edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#000000;"

REL = {
    "use":         S_EDGE + "dashed=1;endArrow=open;endFill=0;",
    "implements":  S_EDGE + "dashed=1;endArrow=block;endFill=0;",
    "extends":     S_EDGE + "endArrow=block;endFill=0;",
    "composition": S_EDGE + "startArrow=diamondThin;startFill=1;startSize=12;endArrow=none;",
}

CLASS_MEMBER_H = 20
CLASS_LINE_H = 8
CLASS_TITLE_H = 26

# --- text metrics -------------------------------------------------------
# draw.io renders labels in Helvetica. These are the real Helvetica advance
# widths (units per 1000 em), so a class box can be sized to its longest
# member instead of a guessed constant - a member wider than its box is
# silently clipped by `overflow=hidden`, which is how full signatures were
# being cut mid-word.
_HELV = {}


def _fill(chars, w):
    for ch in chars:
        _HELV[ch] = w


_fill(" !", 278); _fill('"', 355); _fill("#$", 556); _fill("%", 889)
_fill("&", 667); _fill("'", 191); _fill("()", 333); _fill("*", 389)
_fill("+", 584); _fill(",", 278); _fill("-", 333); _fill("./", 278)
_fill("0123456789", 556); _fill(":;", 278); _fill("<=>", 584); _fill("?", 556)
_fill("@", 1015)
_fill("ABEKPSVXY", 667); _fill("CDHNRU", 722); _fill("GOQ", 778); _fill("FTZ", 611)
_fill("I", 278); _fill("J", 500); _fill("L", 556); _fill("M", 833); _fill("W", 944)
_fill("abdeghnopqu", 556); _fill("cksvxyz", 500); _fill("ijl", 222)
_fill("ft", 278); _fill("r", 333); _fill("m", 833); _fill("w", 722)
_fill("[\\]", 278); _fill("^", 469); _fill("_", 556); _fill("`", 333)
_fill("{}", 334); _fill("|", 260); _fill("~", 584)

_DEFAULT_ADVANCE = 600      # unknown glyph: assume wide, never under-measure


def text_w(s, size, bold=False):
    """Rendered width of s in px. Biased wide on purpose: over-estimating
    costs a few px of whitespace, under-estimating clips the text."""
    total = sum(_HELV.get(ch, _DEFAULT_ADVANCE) for ch in str(s))
    return total * size / 1000.0 * (1.08 if bold else 1.0)


def esc(s):
    """Escape for an mxCell value attribute.

    draw.io renders the label as HTML (html=1), so `<` must survive TWO decodings:
    XML attribute decoding, then HTML rendering. Hence `&` -> `&amp;amp;` and
    `<` -> `&amp;lt;`. Escaping once turns Task<List<Guid>> into `Task>`.
    """
    s = str(s)
    s = s.replace("&", "&amp;amp;")
    s = s.replace("<", "&amp;lt;").replace(">", "&amp;gt;")
    s = s.replace('"', "&quot;")
    s = s.replace("\n", "&#10;")
    return s


class Ids:
    def __init__(self, prefix):
        self.p, self.n = prefix, 0

    def __call__(self, tag=""):
        self.n += 1
        return f"{self.p}-{tag}{self.n}" if tag else f"{self.p}-{self.n}"


def vertex(i, value, x, y, w, h, style, parent="1"):
    return (f'<mxCell id="{i}" value="{esc(value)}" style="{style}" vertex="1" '
            f'parent="{parent}"><mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" '
            f'as="geometry" /></mxCell>')


def child(i, value, y, w, h, style, parent):
    return (f'<mxCell id="{i}" value="{esc(value)}" style="{style}" vertex="1" '
            f'parent="{parent}"><mxGeometry y="{y}" width="{w}" height="{h}" '
            f'as="geometry" /></mxCell>')


def free_edge(i, value, x1, y1, x2, y2, style, parent="1"):
    """Edge pinned to absolute coordinates (no cell connection)."""
    return (f'<mxCell id="{i}" value="{esc(value)}" style="{style}" edge="1" '
            f'parent="{parent}"><mxGeometry relative="1" as="geometry">'
            f'<mxPoint x="{x1}" y="{y1}" as="sourcePoint" />'
            f'<mxPoint x="{x2}" y="{y2}" as="targetPoint" /></mxGeometry></mxCell>')


def free_edge_poly(i, value, points, style, parent="1"):
    """Edge pinned to absolute coordinates through a source point, any
    number of intermediate waypoints, and a target point -- for a message
    that isn't one straight segment (a self-call's hook)."""
    (x1, y1), *mid, (x2, y2) = points
    inner = "".join(f'<mxPoint x="{x}" y="{y}" />' for x, y in mid)
    pts = f'<Array as="points">{inner}</Array>' if mid else ""
    return (f'<mxCell id="{i}" value="{esc(value)}" style="{style}" edge="1" '
            f'parent="{parent}"><mxGeometry relative="1" as="geometry">'
            f'<mxPoint x="{x1}" y="{y1}" as="sourcePoint" />'
            f'<mxPoint x="{x2}" y="{y2}" as="targetPoint" />{pts}'
            f'</mxGeometry></mxCell>')


def cell_edge(i, src, tgt, style, value="", points=None, exit_=None, entry=None):
    st = style
    if exit_:
        st += f"exitX={exit_[0]};exitY={exit_[1]};exitDx=0;exitDy=0;"
    if entry:
        st += f"entryX={entry[0]};entryY={entry[1]};entryDx=0;entryDy=0;"
    pts = ""
    if points:
        inner = "".join(f'<mxPoint x="{x}" y="{y}" />' for x, y in points)
        pts = f'<Array as="points">{inner}</Array>'
    return (f'<mxCell id="{i}" value="{esc(value)}" style="{st}" edge="1" parent="1" '
            f'source="{src}" target="{tgt}"><mxGeometry relative="1" as="geometry">'
            f'{pts}</mxGeometry></mxCell>')


def page(pid, name, w, h, body):
    return (f'  <diagram id="{pid}" name="{esc(name)}">\n'
            f'    <mxGraphModel dx="1601" dy="835" grid="1" gridSize="10" guides="1" '
            f'tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" '
            f'pageWidth="{w}" pageHeight="{h}" math="0" shadow="0">\n'
            f'      <root>\n        <mxCell id="0" />\n'
            f'        <mxCell id="1" parent="0" />\n'
            + "".join(f"        {c}\n" for c in body)
            + '      </root>\n    </mxGraphModel>\n  </diagram>\n')


def mxfile(pages):
    return ('<mxfile host="app.diagrams.net" version="29.0.3" pages="%d">\n%s</mxfile>\n'
            % (len(pages), "".join(pages)))
