"""Build a .drawio from YAML specs.  Usage: python build.py login"""
import sys, os, yaml
import seq, cls, validate
from emit import mxfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SHARED_CLASS_DIR = os.path.join(ROOT, "specs", "_shared", "classes")


def resolve_includes(cspec):
    """Expand top-level `include:` into extra entries in `classes:`.

    Each shared file (specs/_shared/classes/<file>.yaml) holds exactly one class
    body (name/fields/methods, no position) so it renders identically everywhere
    it's reused. `include:` entries may override `name` (needed when the same
    shared shape has to appear under 2 project-suffixed names in one diagram,
    e.g. ApiResponse<T> (InterviewAPI) vs ApiResponse<T> (EvaluationAPI)) plus
    `column`/`w` for local placement. No-op when the spec has no `include:` key,
    so files without it (login_classes.yaml) are untouched byte-for-byte.
    """
    includes = cspec.get("include")
    if not includes:
        return cspec
    classes = list(cspec.get("classes", []))
    seen_names = {c["name"] for c in classes}
    for inc in includes:
        fname = inc["file"]
        fpath = os.path.join(SHARED_CLASS_DIR, fname)
        if not os.path.exists(fpath):
            raise FileNotFoundError(
                f"include: khong tim thay file shared 'specs/_shared/classes/{fname}'")
        shared = yaml.safe_load(open(fpath, encoding="utf-8"))
        merged = dict(shared)
        if "name" in inc:
            merged["name"] = inc["name"]
        for key in ("column", "w"):
            if key in inc:
                merged[key] = inc[key]
        name = merged.get("name")
        if name in seen_names:
            raise ValueError(
                f"include: trung ten class '{name}' (da co trong classes: cuc bo hoac "
                f"1 include khac) - dat 'name:' rieng trong include de phan biet")
        seen_names.add(name)
        classes.append(merged)
    cspec = dict(cspec)
    cspec["classes"] = classes
    return cspec


def main(feature):
    sp = yaml.safe_load(open(f"{ROOT}/specs/{feature}.yaml", encoding="utf-8"))
    errs = validate.check_spec(sp)
    if errs:
        print("SPEC LOI:"); [print("  -", e) for e in errs]; sys.exit(1)
    p1, nmsg = seq.build(sp)
    cpath = f"{ROOT}/specs/{feature}_classes.yaml"
    if os.path.exists(cpath):
        cspec = yaml.safe_load(open(cpath, encoding="utf-8"))
        cspec = resolve_includes(cspec)
        p2, ncls = cls.build(cspec, sp["feature"], sp["title"],
                             sp.get("uc_id", ""), sp.get("backlog", []),
                             sp.get("uc_label", ""))
    else:
        p2, ncls = cls.build({"classes": [{"name": "TODO", "fields": [], "methods": []}]},
                             sp["feature"], sp["title"]), 0
    out = f"{ROOT}/out/{sp['feature']}.drawio"
    open(out, "w", encoding="utf-8").write(mxfile([p1, p2]))
    print(f"-> {out}  | {nmsg} message | {ncls} class")
    e = validate.check_file(out)
    print("VALIDATE: " + ("OK" if not e else f"{len(e)} loi"))
    for x in e[:15]: print("  -", x)
    return len(e)

if __name__ == "__main__":
    sys.exit(1 if main(sys.argv[1]) else 0)
