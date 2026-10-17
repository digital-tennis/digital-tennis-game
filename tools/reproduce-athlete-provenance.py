#!/usr/bin/env python3
"""Rebuild CC0 derivatives in isolation with the pinned verification dependencies."""
import importlib.util
from pathlib import Path
import shutil
import tempfile

import athlete_provenance as provenance


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, provenance.ROOT / 'tools' / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def main():
    try:
        import numpy
        import PIL
    except ImportError as error:
        raise SystemExit(
            'Derivative reproduction requires the verification environment. Run:\n'
            'python3 -m venv .work/verification-venv\n'
            './.work/verification-venv/bin/python3 -m pip install -r tools/requirements-verification.txt\n'
            './.work/verification-venv/bin/python3 -B tools/reproduce-athlete-provenance.py'
        ) from error

    root, base = provenance.ROOT, provenance.BASE
    manifest = provenance.load(base + 'provenance.json')
    body = provenance.load(base + 'body-bindings.json')
    # Never rewrite shipped assets or retained evidence during verification.
    with tempfile.TemporaryDirectory(prefix='athlete-provenance-') as directory:
        target = Path(directory)
        paths = {s['path'] for s in manifest['sources']}
        paths |= {'Assets/Resources/Characters/body-retarget.json',
                  'Assets/Resources/Characters/motion-retarget.json'}
        for path in paths:
            dest = target / path
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(root / path, dest)
        (target / 'Assets/Resources/Characters/Acquired').mkdir()
        bodies = module('reproduce_bodies', 'adapt-athlete-bodies.py')
        bodies.ROOT = target
        bodies.CFG = target / 'Assets/Resources/Characters/body-retarget.json'
        for profile in provenance.load('Assets/Resources/Characters/body-retarget.json')['profiles']:
            result = bodies.bake(profile)
            expected = next(o for o in body['outputs'] if o['id'] == profile['id'])
            if result != expected:
                raise ValueError('Reproduced body binding differs: ' + profile['id'])
        motion = module('reproduce_motion', 'adapt-athlete-motion.py')
        motion.ROOT = target
        motion.main()
        for path in ['Assets/Resources/Characters/Acquired/FullBodyMotion.json',
                     base + 'motion-bindings.json']:
            if (target / path).read_bytes() != (root / path).read_bytes():
                raise ValueError('Reproduced motion bytes differ: ' + path)
    print('Body bindings and full-body motion reproduce exactly from retained sources')


if __name__ == '__main__':
    main()
