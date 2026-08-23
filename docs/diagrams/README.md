# Class diagrams — how to verify

## Which pipeline owns which page — read this first

Two generators exist and they disagree about what a correct class page looks like.
**`class_specs/<name>.py` + `generate_class_diagram.py` is authoritative for every class
page**; `_tools/build.py` (from `specs/<name>.yaml` + `specs/<name>_classes.yaml`) is
authoritative for **sequence pages only**, and its class output is a scaffold — it omits
the mandatory title cell, admits WebClient classes, and does not trim members to the drawn
flow. Critically, **`build.py` printing `VALIDATE: OK` does NOT mean the nine checks pass**:
its bundled `_tools/validate.py` is weaker and misses clipped member rows, overlapping arrow
segments and edge labels sitting on top of a class box. Always confirm a class page with
`python validate_class_diagram.py out/<File>.drawio` (all nine lines must read `[]`), or
just run `rebuild_all.py`. Workflow for a new diagram: `build.py` to emit both pages, then
`generate_class_diagram.py` to replace the class page, then add a `sequence_baseline.json`
entry.

The class page of every `.drawio` in `out/` is generated from a spec in `class_specs/`.
Sequence pages are never touched. To regenerate and check the whole set:

```bash
python docs/diagrams/rebuild_all.py     # exit 0 = every file green
```

It rebuilds each spec into a clean `.rebuild/` dir, runs `validate_class_diagram.py`
(all nine checks must be empty), and confirms every sequence page still matches its
original sha256 in `sequence_baseline.json`. A non-zero exit means at least one file
failed — the table shows which and why.

To rewrite a single diagram in place: `python docs/diagrams/generate_class_diagram.py
class_specs/<name>.py` (add `--report` for the layout, width and routing detail).

Rules: [out/CLASS_DIAGRAM_RULES.md](out/CLASS_DIAGRAM_RULES.md) ·
Sequence-vs-class gaps: [KNOWN_DEVIATIONS.md](KNOWN_DEVIATIONS.md) ·
`out/ConfigureInterviewSession.drawio` is the hand-tuned reference and is never modified.
