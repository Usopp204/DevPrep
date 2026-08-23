#!/usr/bin/env python
"""Independent rebuild + verification harness for the class diagrams.

Run:
    python docs/diagrams/rebuild_all.py

For every spec in class_specs/ it:
  1. copies the current out/<file>.drawio into a clean scratch directory
     (.rebuild/, wiped first, so nothing stale can be mistaken for a result),
  2. regenerates ONLY the class page there and checks the generator's exit code
     explicitly - a non-zero exit is a FAIL row, never a skip that falls through
     to validating a stale file,
  3. runs validate_class_diagram.py and requires all nine checks to be empty,
  4. compares the file's sequence page against sequence_baseline.json, which
     holds the sha256 of the ORIGINAL sequence page of every diagram, captured
     before any rework started,
  5. compares the freshly generated file byte-for-byte against the one committed
     in out/. This is the check that catches a stale out/ file: a spec can be
     perfectly green in .rebuild/ while out/ still holds an older class page.

Exits non-zero if any row is not fully green.
"""

import glob
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
SPEC_DIR = os.path.join(ROOT, 'class_specs')
OUT_DIR = os.path.join(ROOT, 'out')
SCRATCH = os.path.join(ROOT, '.rebuild')
BASELINE = os.path.join(ROOT, 'sequence_baseline.json')
GENERATOR = os.path.join(ROOT, 'generate_class_diagram.py')
VALIDATOR = os.path.join(ROOT, 'validate_class_diagram.py')

# Files whose sequence page is baseline-checked but whose class page is NOT generated:
# hand-maintained references with no class_specs/ entry. Giving them one would force them
# into the generated set, which is exactly what must not happen.
BASELINE_ONLY = ('ConfigureInterviewSession.drawio',)

DIAGRAM_RE = re.compile(r'(<diagram\b[^>]*>)(.*?)(</diagram>)', re.S)


def sequence_page(path):
    text = open(path, encoding='utf-8').read()
    blocks = list(DIAGRAM_RE.finditer(text))
    if len(blocks) != 2:
        return None
    return text[blocks[0].start():blocks[0].end()]


def spec_target(spec_path):
    name = os.path.splitext(os.path.basename(spec_path))[0]
    s = importlib.util.spec_from_file_location(name, spec_path)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m.SPEC['file']


def main():
    baseline = json.load(open(BASELINE, encoding='utf-8'))

    if os.path.isdir(SCRATCH):
        shutil.rmtree(SCRATCH)
    os.makedirs(SCRATCH)

    specs = sorted(glob.glob(os.path.join(SPEC_DIR, '*.py')))
    rows = []

    for spec_path in specs:
        spec_name = os.path.basename(spec_path)
        try:
            target = spec_target(spec_path)
        except Exception as exc:
            rows.append((spec_name, '-', 'SPEC ERROR: %s' % exc, '-', '-', '-', '-', False))
            continue

        src = os.path.join(OUT_DIR, target)
        dst = os.path.join(SCRATCH, target)
        if not os.path.exists(src):
            rows.append((spec_name, target, 'missing out/%s' % target, '-', '-', '-', '-', False))
            continue
        shutil.copyfile(src, dst)

        proc = subprocess.run([sys.executable, GENERATOR, spec_path, '--out', SCRATCH],
                              capture_output=True, text=True)
        generated = proc.returncode == 0
        rounds = '-'
        if generated:
            hit = re.search(r'rounds used: (\d+)', proc.stdout)
            rounds = hit.group(1) if hit else '?'

        checks = 'not run'
        if generated:
            vp = subprocess.run([sys.executable, VALIDATOR, dst],
                                capture_output=True, text=True)
            if vp.returncode != 0:
                checks = 'validator error'
            else:
                bad = [ln for ln in vp.stdout.strip().splitlines()
                       if not ln.rstrip().endswith(': []')]
                checks = 'all 9 empty' if not bad else '%d FAILING' % len(bad)

        seq_state = 'not run'
        if generated:
            seq = sequence_page(dst)
            want = baseline.get(target)
            if seq is None:
                seq_state = 'unparsable'
            elif want is None:
                seq_state = 'NO BASELINE'
            else:
                got = hashlib.sha256(seq.encode()).hexdigest()
                seq_state = 'identical' if got == want['sequence_sha256'] else 'CHANGED'

        in_sync = 'not run'
        if generated:
            fresh = open(dst, 'rb').read()
            committed = open(src, 'rb').read()
            in_sync = 'in sync' if fresh == committed else 'STALE out/'

        ok = (generated and checks == 'all 9 empty'
              and seq_state == 'identical' and in_sync == 'in sync')
        rows.append((spec_name, target,
                     'yes' if generated else 'NO (exit %d)' % proc.returncode,
                     checks, seq_state, in_sync, rounds, ok))
        if not generated:
            sys.stderr.write('--- %s generator output ---\n%s%s\n'
                             % (spec_name, proc.stdout, proc.stderr))

    w = max([len(r[0]) for r in rows] + [12])
    line = '  '.join(['spec'.ljust(w), 'generated'.ljust(13),
                      '9 checks'.ljust(15), 'seq identical'.ljust(13),
                      'out/ in sync'.ljust(12), 'rounds'])
    print(line)
    print('-' * len(line))
    failures = 0
    for spec_name, target, gen, checks, seq_state, in_sync, rounds, ok in rows:
        print('  '.join([spec_name.ljust(w), gen.ljust(13), checks.ljust(15),
                         seq_state.ljust(13), in_sync.ljust(12), rounds]))
        if not ok:
            failures += 1
    print('-' * len(line))
    print('%d spec(s), %d fully green, %d FAILING' % (len(rows), len(rows) - failures, failures))

    # Baseline-only files: hand-maintained, never generated, but their sequence page still
    # has to be tripwired. Checked outside the spec loop above precisely because they have
    # no class_specs entry -- adding one would pull them into the generated set.
    print('\nbaseline-only (not generated):')
    for target in BASELINE_ONLY:
        path = os.path.join(OUT_DIR, target)
        want = baseline.get(target)
        seq = sequence_page(path) if os.path.exists(path) else None
        if seq is None:
            state = 'missing out/%s' % target
        elif want is None:
            state = 'NO BASELINE'
        else:
            state = ('identical'
                     if hashlib.sha256(seq.encode()).hexdigest() == want['sequence_sha256']
                     else 'CHANGED')
        print('  %s  %s' % (target.ljust(w), state))
        if state != 'identical':
            failures += 1

    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
