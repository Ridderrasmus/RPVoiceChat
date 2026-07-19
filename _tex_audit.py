import json
import re
from pathlib import Path

root = Path(r"c:\Users\romai\source\repos\RPVoiceChat\RPVoiceChat\assets\rpvoicechat")
shapes_dir = root / "shapes"
blocktypes_dir = root / "blocktypes"


def extract_hash_textures(obj, found=None):
    if found is None:
        found = set()
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == "texture" and isinstance(v, str) and v.startswith("#"):
                found.add(v[1:])
            else:
                extract_hash_textures(v, found)
    elif isinstance(obj, list):
        for i in obj:
            extract_hash_textures(i, found)
    return found


def get_textures_section(obj):
    t = obj.get("textures")
    if t is None:
        return None, set()
    if isinstance(t, dict):
        return t, set(t.keys())
    return t, set()


shape_info = {}
for p in shapes_dir.rglob("*.json"):
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"FAIL shape {p}: {e}")
        continue
    tex_sect, tex_keys = get_textures_section(data)
    used = extract_hash_textures(data)
    rel = p.relative_to(shapes_dir).as_posix()
    shape_info[rel] = {
        "path": str(p),
        "defined": tex_keys,
        "used": used,
        "textures_empty": isinstance(tex_sect, dict) and len(tex_sect) == 0,
        "has_textures_sect": tex_sect is not None,
        "is_mechpart": "mechpart" in p.name.lower(),
        "data": data,
    }

bt_info = {}
for p in blocktypes_dir.rglob("*.json"):
    try:
        text = p.read_text(encoding="utf-8")
        data = json.loads(text)
    except Exception as e:
        print(f"FAIL bt {p}: {e}")
        continue
    tex_sect, tex_keys = get_textures_section(data)
    shape_bases = []
    for key in ("shape", "shapeinventory"):
        s = data.get(key)
        if isinstance(s, dict) and "base" in s:
            shape_bases.append(s["base"])
        elif isinstance(s, dict):
            for v in s.values():
                if isinstance(v, dict) and "base" in v:
                    shape_bases.append(v["base"])
    path_refs = re.findall(r'"(?:base)"\s*:\s*"([^"]+)"', text)
    path_refs2 = re.findall(r'"(block/[^"]+)"', text)
    # mechpart refs in entityBehaviors / attributes
    all_refs = list(set(path_refs + path_refs2 + shape_bases))
    rel = p.relative_to(blocktypes_dir).as_posix()
    bt_info[rel] = {
        "path": str(p),
        "code": data.get("code"),
        "defined": tex_keys,
        "shape_bases": shape_bases,
        "all_refs": all_refs,
        "has_textures": "textures" in data,
        "textures_empty": isinstance(tex_sect, dict) and len(tex_sect) == 0,
        "text": text,
    }


def normalize_shape_ref(ref: str) -> list:
    """Map blocktype shape base to possible shape relative paths."""
    ref = ref.replace("\\", "/")
    # strip domain prefix
    if ":" in ref:
        ref = ref.split(":", 1)[1]
    candidates = []
    # shapes live under shapes/block/... or shapes/item/...
    for prefix in ("", "block/", "item/"):
        c = ref
        if prefix and not c.startswith(("block/", "item/")):
            c = prefix + c
        # try with and without .json
        candidates.append(c if c.endswith(".json") else c + ".json")
        candidates.append(c)
    # also raw
    candidates.append(ref + ".json")
    candidates.append(ref)
    return candidates


def find_shape_for_ref(ref: str):
    cands = normalize_shape_ref(ref)
    for c in cands:
        # match against shape_info keys
        if c in shape_info:
            return c
        # try without leading block/
        for key in shape_info:
            if key == c or key.endswith("/" + c) or key == c.replace("block/", "", 1):
                return key
            def strip_json(s):
                return s[:-5] if s.endswith(".json") else s
            if strip_json(key) == strip_json(c):
                return key
    # fuzzy: endswith
    ref_norm = ref.replace("\\", "/")
    if ":" in ref_norm:
        ref_norm = ref_norm.split(":", 1)[1]
    for key in shape_info:
        k = key[:-5] if key.endswith(".json") else key
        if k == ref_norm or k.endswith("/" + ref_norm.split("/")[-1]) or ref_norm.endswith(k) or k.endswith(ref_norm):
            return key
        # radioemitter/radioemitter vs radioemitter
        if ref_norm in k or k in ref_norm:
            return key
    return None


# Priority block names
priority = [
    "switchboard",
    "radioconsole",
    "radiomixingconsole",
    "radiomicrophone",
    "radioreceiver",
    "radioantenna_part",
    "bellhammer",
    "radioemitter",
    "speaker",
    "connector",
    "telephone",
    "telegraph",
    "printer",
    "signallamp",
    "callbell",
]

print("=" * 80)
print("ALL SHAPES WITH TEXTURE USAGE")
print("=" * 80)
for rel, info in sorted(shape_info.items()):
    if info["used"] or info["defined"] or info["is_mechpart"]:
        print(f"\n{rel}")
        print(f"  defined: {sorted(info['defined'])}")
        print(f"  used: {sorted(info['used'])}")
        print(f"  empty_tex: {info['textures_empty']} mechpart: {info['is_mechpart']}")
        missing_in_def = info["used"] - info["defined"]
        if missing_in_def:
            print(f"  USED_NOT_IN_SHAPE_TEXTURES: {sorted(missing_in_def)}")

print("\n" + "=" * 80)
print("BLOCKTYPE vs SHAPE MISMATCHES")
print("=" * 80)

mismatches = []

for bt_rel, bt in sorted(bt_info.items()):
    # find related shapes
    related = set()
    for ref in bt["all_refs"]:
        found = find_shape_for_ref(ref)
        if found:
            related.add(found)
    # also match by code name
    code = (bt["code"] or Path(bt_rel).stem).lower()
    for key in shape_info:
        klow = key.lower()
        if code in klow or Path(key).stem.lower().startswith(code):
            related.add(key)
        # folder match
        if f"/{code}/" in f"/{klow}" or klow.startswith(f"block/{code}"):
            related.add(key)

    if not related:
        continue

    all_used = set()
    mech_empty = []
    shape_details = []
    for srel in sorted(related):
        s = shape_info[srel]
        all_used |= s["used"]
        # keys defined only in shape as fallbacks - blocktype should still declare for overrides
        shape_details.append(srel)
        if s["is_mechpart"] and (s["textures_empty"] or (s["used"] and not s["defined"])):
            mech_empty.append(srel)

    # Also: if shape defines textures but faces use #keys, those keys need to be in blocktype
    # for VS texture resolution from blocktype
    missing = all_used - bt["defined"]

    # Filter out null/#null weirdness
    missing = {m for m in missing if m and m != "null"}

    if missing or mech_empty:
        is_prio = any(p in bt_rel.lower() or p in code for p in priority)
        mismatches.append(
            {
                "priority": is_prio,
                "bt": bt_rel,
                "bt_path": bt["path"],
                "code": code,
                "bt_textures": sorted(bt["defined"]),
                "shapes": shape_details,
                "used": sorted(all_used),
                "missing": sorted(missing),
                "mech_empty": mech_empty,
                "has_textures_sect": bt["has_textures"],
            }
        )

# sort: priority first, then by missing count
mismatches.sort(key=lambda m: (not m["priority"], -len(m["missing"]), m["bt"]))

for m in mismatches:
    tag = "PRIORITY" if m["priority"] else "other"
    print(f"\n[{tag}] blocktype: {m['bt']}")
    print(f"  path: {m['bt_path']}")
    print(f"  blocktype textures: {m['bt_textures'] or '(NONE)'}")
    print(f"  related shapes:")
    for s in m["shapes"]:
        si = shape_info[s]
        print(f"    - {s}")
        print(f"        used={sorted(si['used'])} defined={sorted(si['defined'])} empty={si['textures_empty']} mech={si['is_mechpart']}")
    print(f"  MISSING from blocktype: {m['missing']}")
    if m["mech_empty"]:
        print(f"  MECHPART empty/incomplete textures: {m['mech_empty']}")

print("\n" + "=" * 80)
print("MECHPART SHAPES DETAIL")
print("=" * 80)
for rel, info in sorted(shape_info.items()):
    if info["is_mechpart"]:
        print(f"\n{rel}")
        print(f"  textures dict: {info['defined'] or '{}'}")
        print(f"  empty: {info['textures_empty']}")
        print(f"  faces use: {sorted(info['used'])}")

print("\nDONE")
print(f"Total mismatches: {len(mismatches)}")
print(f"Priority with missing: {sum(1 for m in mismatches if m['priority'] and m['missing'])}")
