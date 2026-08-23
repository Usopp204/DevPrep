# Class Diagram Rules — DevPrep (SEP490_34)

Rules for authoring and reworking `.drawio` class diagrams under `docs/diagrams/out/`.

**Reference implementation:** `ConfigureInterviewSession.drawio` — already reworked and manually
adjusted. **Do not modify this file.** Read it to copy styles, spacing and routing conventions.

**Why these rules exist:** the review board rejected the first version of the class diagrams because
they mixed the UI/presentation tier with the back-end tier in a single diagram. The rules below
encode that correction plus every layout defect found while fixing it.

---

## 0. Workflow (mandatory)

1. Inventory every `.drawio` under `docs/diagrams/out/`; for each, list the classes on the class page
   and mark which rule below it violates.
2. Present the file-level plan and wait for explicit approval before editing anything.
3. Edit one file at a time, then run the validator (§7) on it.
4. Report changed files and stop. Never auto-commit, never auto-push.

**Ownership:** a diagram whose use case belongs to a teammate-owned service (AuthAPI, UserAPI,
PaymentAPI, QuestionAPI, NotificationAPI, NewsAPI, CvJdAPI, FeedbackAPI) must not be edited without
explicit confirmation from duyentq, even though the file lives in a shared docs folder.

---

## 1. Scope — what belongs on a class diagram

DevPrep is a microservice system. A class diagram documents the **internal design of exactly one
service**, for exactly one use case.

### Include

- Controllers, service interfaces + implementations, repository interfaces + implementations,
  `DbContext`, entities, DTOs, and custom exceptions **of the owning service only**.
- Outbound HTTP client classes (e.g. `AiServiceClient`, `QuestionIndexClient`) — these are real
  classes in the owning service and represent the boundary to other services.

### Exclude

| Excluded | Why |
| --- | --- |
| WebClient classes (`InterviewController`, `IInterviewService`, `InterviewService`, `IApiClient`, `ApiClient`, view models) | Different service / presentation tier. This is the exact defect the board flagged. |
| ApiGateway (YARP) | Infrastructure, not a class of this service. |
| Classes of teammate-owned services | Different service. |
| Framework base classes: `ControllerBase`, `Exception`, `DbContext` (EF Core base), `HttpClient`, `ILogger<T>` as standalone boxes | Not part of the designed system. Inheritance from them is implementation detail, answered verbally if asked. |
| External services drawn as boxes (`ai-service (FastAPI)`, SQL Server, ChromaDB) | Not classes. Cross-service communication belongs to the **sequence diagram** only. |
| Note / legend boxes | Removed by request; the diagram must be self-explanatory. |

**Consistency test — apply to every diagram:** if a box would appear as a *lifeline* in the sequence
diagram rather than as a class inside the service, it does not belong on the class diagram.

---

## 2. Member selection

Show only the members exercised by this use case, not the entire class.

- Wide interfaces (e.g. `IAiServiceClient`, which serves every ai-service call in the system) are
  trimmed to the methods this use case actually calls — usually one or two of them.
- Entities are trimmed to the columns this use case reads or writes (e.g. `InterviewSession` from
  ~30 properties down to ~14).
- Service fields unrelated to the use case are dropped (`_paymentApiClient`, `_biometricsRepository`,
  `_scopeFactory`, `_httpContextAccessor`, …).
- Keep full C# signatures with parameter names and types — do not abbreviate to `ConfigLevel(...)`.
- Visibility prefixes are required: `+` public, `-` private, `#` protected.
- Duplicate type names across services (there is an `ApiResponse<T>` in both WebClient and
  InterviewAPI) must not both appear — only the owning service's version.

---

## 3. UML notation — standard only

Nothing in the diagram may use a non-standard convention.

| Relationship | Style | Label |
| --- | --- | --- |
| Generalization (`: BaseClass`) | solid line, hollow closed triangle at target | none |
| Realization (`: IInterface`) | **dashed** line, hollow closed triangle at target | none |
| Dependency (uses / creates / throws) | **dashed** line, open arrow | `«use»`, `«create»`, `«throw»` |
| Association (holds a reference) | solid line, open arrow | multiplicity at both ends |

**Derived properties carry a leading slash** — `+ /ShouldEndSession: bool`. Use it for any member
whose value is computed from other members rather than stored (in C#, an expression-bodied or
get-only property such as `public bool ShouldEndSession => Ended;`). A plain field name asserts
stored state, which is wrong for these.

**Class box borders are always solid.** Dashed borders are not UML for classes — dashes belong to
*relationships* and *notes*. If a box needs to be marked as special, use a stereotype in the header
(`«interface»`, `«entity»`, `«DTO»`), never a dashed border. This was a real defect in the first pass.

Multiplicity is rendered as two `edgeLabel` children on the association at relative `x = -0.85`
(source end) and `x = 0.85` (target end) — not as one label in the middle of the line.

---

## 4. Layout

Layered left → right, matching the call order in the sequence diagram:

```
col 1            col 2                col 3                  col 4                    col 5
Controller  →  IService/Service  →  IHttpClient/Client  →  IRepository/Repository  →  Entity
  + request DTOs   + response DTOs                            + DbContext
  + ApiResponse<T> + custom exception
```

- All first-row boxes share the same top `y` (140). Keep `y < 140` free — it is routing space (§5).
- Vertical gap between boxes in a column: 60–90 px.
- Horizontal gap between columns: 90 px (this gap is a routing channel — keep it empty).
- DTOs sit inside their owner's column, indented right by `3 × LANE + 20` so the left edge of the
  column stays free as routing lanes.

### Box width — sizing rule (defect found in review)

drawio member rows use `overflow=hidden`, so **any text wider than the box is silently clipped**.
Long signatures like `ResolveSessionAsync(sessionId: Guid?, userId: Guid, ct: CancellationToken, factory: Func<InterviewSession>): Task<InterviewSession>`
overflow every default width.

Compute width from content, per column:

```
width = max over all member rows in the column of ( len(unescaped_text) * 6.05 + 22 )
rounded up to the nearest 10
```

`6.05` px/char is calibrated for the default Helvetica 11 px member font. Never hand-pick a width.

### Box height — must be exact

drawio `childLayout=stackLayout` does not auto-fit the parent; a wrong parent height leaves a gap or
clips the last row:

```
height = 30 + 20 * (number of fields) + (8 if any methods else 0) + 20 * (number of methods)
```

`30` = `startSize` (header), `20` = one member row, `8` = the divider line between fields and methods.

---

## 5. Edge routing

Never rely on drawio's automatic routing — it draws lines straight across class boxes. Every edge
gets explicit `exitX/exitY`, `entryX/entryY` and a waypoint array.

- Style base: `edgeStyle=orthogonalEdgeStyle;rounded=0;jumpStyle=arc;jumpSize=8`.
  `jumpStyle=arc` makes unavoidable crossings read as crossings, not as connections.
- **Lanes:** reserve vertical lanes at `column_x + n*34` (n = 1, 2, 3) inside each column for edges
  that must travel past intermediate boxes. Assign the innermost lane to the shortest edge.
- **Channels:** the 90 px gap between columns is a vertical channel. Long left↔right edges enter the
  channel, travel vertically, then leave — they never cut through a column.
- **Horizontal channels** above the first row at `y = 35 / 65 / 95` carry edges that span 2+ columns.
  One edge per channel — two edges at the same `y` would overlap into a single visible line.
- **Orthogonality:** every segment must be exactly horizontal or exactly vertical. Do not use round
  fractions like `exitY=0.4` and hope they line up; derive the fraction from the absolute target
  coordinate: `exitY = (target_abs_y - box_y) / box_height`, and round the waypoint to an integer.
  A 0.6 px mismatch produces a visible diagonal kink.

Hard constraints, all verified by the validator:

1. No edge segment passes through any class box other than its own endpoints.
2. No two edge segments from different edges are collinear and overlapping.
3. No segment is diagonal.
4. No class boxes overlap.
5. No member row is clipped.

---

## 6. File handling

- One `.drawio` file per use case, two pages: `Sequence - <UseCase> [BL#...]` then
  `Class - <UseCase> [BL#...]`.
- **Never touch the sequence page.** Splice only the class `<diagram>` element; byte-compare the
  sequence page before and after to prove it is unchanged.
- Preserve the existing `<diagram id="...">` and `name="..."` attributes.
- Class page `<mxGraphModel>`: `page="0"`, `pageWidth`/`pageHeight` set from the computed bounding
  box + 80 px margin.
- Title text at `y = -70`, font size 18, bold:
  `Class Diagram - <UseCase> [BL#...] - Back-end scope (<ServiceName>)`.

### Style strings (copy verbatim)

```
CLASS  swimlane;fontStyle=1;align=center;verticalAlign=top;childLayout=stackLayout;horizontal=1;
       startSize=30;horizontalStack=0;resizeParent=1;resizeParentMax=0;rounded=0;shadow=0;
       strokeWidth=1;fillColor=#ffffff;swimlaneFillColor=#ffffff;fontSize=12;html=1;
ROW    text;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;spacingLeft=6;
       spacingRight=6;overflow=hidden;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;rounded=0;
       fontSize=11;html=1;
DIV    line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;spacingLeft=3;
       spacingRight=3;rotatable=0;labelPosition=right;points=[];portConstraint=eastwest;
       strokeColor=#000000;
EDGE   html=1;rounded=0;edgeStyle=orthogonalEdgeStyle;strokeColor=#000000;jumpStyle=arc;jumpSize=8;
  + generalization  endArrow=block;endSize=14;endFill=0;
  + realization     endArrow=block;endSize=14;endFill=0;dashed=1;
  + dependency      endArrow=open;endSize=12;dashed=1;fontSize=10;labelBackgroundColor=#ffffff;
  + association     endArrow=open;endSize=12;fontSize=10;labelBackgroundColor=#ffffff;
```

Angle brackets in member text must be XML-escaped as `&lt;` / `&gt;` (`Task&lt;IActionResult&gt;`),
and stereotypes as `&lt;&lt;DTO&gt;&gt;`.

### Generate, don't hand-place

Write a Python generator that computes widths, heights, column positions and waypoints from the class
content, then emits the XML — do not hand-write coordinates. Every constant above must appear once as
a variable. Keep the generator next to the diagrams so the layout is reproducible.

---

## 7. Validator — run on every edited file

Save as `docs/diagrams/validate_class_diagram.py` and run `python validate_class_diagram.py <file>`.
A file is done only when all six checks print empty.

```python
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
```

Beyond the validator, check by eye and by reading the code:

- [ ] every class on the diagram exists in the owning service's source
- [ ] every method shown matches the real C# signature
- [ ] every backend message in the sequence diagram maps to a method on the class diagram
- [ ] no WebClient / Gateway / teammate-service / framework / external-service box remains
- [ ] no dashed class border anywhere
- [ ] no note or legend box
- [ ] sequence page is byte-identical to the original
