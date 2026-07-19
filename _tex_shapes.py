import re
from pathlib import Path

shapes = [
    "radioconsole.json",
    "radiomicrophone.json",
    "radioreceiver.json",
    "radioantenna/radioantenna_top.json",
    "radioantenna/radioantenna_part.json",
    "radioemitter.json",
    "switchboard/switchboard.json",
    "switchboard/switchboard_mechpart.json",
    "bellhammer/bellhammer.json",
    "bellhammer/bellhammer_mechpart.json",
    "bellhammer/bellhammer_chains.json",
    "radiomixingconsole.json",
    "printer.json",
    "speaker/speaker.json",
    "speaker/speaker_wall.json",
    "speaker/speaker_ceiling.json",
    "connector/connector.json",
    "connector/connector_wall.json",
    "telephone.json",
    "telegraphkey.json",
    "signallamp.json",
    "callbell.json",
    "carillonbell/carillonbell.json",
]
base = Path(r"assets/rpvoicechat/shapes/block")
for s in shapes:
    p = base / s
    if not p.exists():
        print("MISSING", s)
        continue
    text = p.read_text(encoding="utf-8")
    used = sorted(set(re.findall(r'"texture"\s*:\s*"#([^"]+)"', text)))
    # get textures object more carefully - brace match from first textures
    defined = []
    empty = False
    idx = text.find('"textures"')
    if idx >= 0:
        brace = text.find("{", idx)
        if brace >= 0:
            depth = 0
            end = brace
            for i, ch in enumerate(text[brace:], brace):
                if ch == "{":
                    depth += 1
                elif ch == "}":
                    depth -= 1
                    if depth == 0:
                        end = i
                        break
            body = text[brace + 1 : end]
            empty = body.strip() == ""
            defined = re.findall(r'"([^"]+)"\s*:', body)
    print(s)
    print("  defined:", defined)
    print("  used:", used)
    print("  empty_dict:", empty)
    print("  missing_in_shape_def:", sorted(set(used) - set(defined)))
