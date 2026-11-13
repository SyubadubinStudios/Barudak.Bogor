"""Regenerate every Barudak Bogor GLB:

    blender --background --python assets/blender/build_all.py
    blender --background --python assets/blender/build_all.py -- --no-previews
    blender --background --python assets/blender/build_all.py -- angkot rusa   (only these)

Inside a live Blender session:  import build_all; build_all.run(families=["vehicle"])
"""
import os, sys, importlib

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

MODULES = ["vehicles", "characters", "animals", "props", "landmarks", "street", "water"]

PREVIEWS = [
    (["vehicle"], "assets-vehicles.png", (0.6, -1.0, 0.55)),
    (["character"], "assets-characters.png", (0.35, -1.0, 0.3)),
    (["animal"], "assets-animals.png", (0.6, -1.0, 0.4)),
    (["prop", "building"], "assets-props.png", (0.5, -1.0, 0.45)),
    (["tree", "landmark"], "assets-landmarks.png", (0.5, -1.0, 0.35)),
]


def load():
    import common
    importlib.reload(common)
    for m in MODULES:
        if os.path.exists(os.path.join(HERE, m + ".py")):
            importlib.reload(importlib.import_module(m))
    return common


def run(names=None, families=None, export=True, previews=False):
    common = load()
    stats = common.build(names=names, families=families, export=export)
    if previews:
        render_previews(common)
    return stats


def render_previews(common=None, only=None):
    import bpy
    common = common or load()
    sc = common.ensure_scene()
    out = []
    for fams, fn, view in PREVIEWS:
        if only and fn not in only:
            continue
        try:
            out.append(common.render_preview(sc, fams, fn, view=view))
        except Exception as e:      # headless machines without a GPU cannot render
            print("preview failed:", fn, e)
    return out


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [a for a in argv if not a.startswith("--")] or None
    run(names=names, previews="--no-previews" not in argv)
