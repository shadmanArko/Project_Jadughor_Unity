"""Parse Godot 'new town.tscn' (live town map) into a JSON layout for Unity.

Only the visible 'Town Ui/ground' subtree is exported. Positions are in the ground
sprite's local pixel space (Godot: origin = ground centre, +y down). Absolute z is
the sum of z_index down the tree (Godot z_as_relative default).
"""
import json, re, os, sys

GODOT = r"D:\Godot\ProjectMuseum\Godot4CS.ProjectMuseum"
TOWN = os.path.join(GODOT, r"Scenes\Museum\Town\new town.tscn")

def res_to_path(p):
    return os.path.join(GODOT, p.replace("res://", "").replace("/", os.sep))

def parse_tscn(path):
    ext, nodes, cur = {}, [], None
    for raw in open(path, encoding="utf-8"):
        line = raw.strip()
        m = re.match(r'\[ext_resource type="(\w+)".*?path="([^"]+)" id="([^"]+)"\]', line)
        if m:
            ext[m.group(3)] = (m.group(1), m.group(2)); cur = None; continue
        if line.startswith("[node "):
            attrs = dict(re.findall(r'(\w+)="([^"]*)"', line))
            inst = re.search(r'instance=ExtResource\("([^"]+)"\)', line)
            cur = {"name": attrs["name"], "type": attrs.get("type"), "parent": attrs.get("parent"),
                   "instance": inst.group(1) if inst else None, "props": {}}
            nodes.append(cur); continue
        if line.startswith("["):
            cur = None; continue
        if cur is not None and " = " in line:
            k, v = line.split(" = ", 1)
            cur["props"][k] = v
    return ext, nodes

def vec2(v):
    m = re.match(r"Vector2\(([-\d.e]+), ([-\d.e]+)\)", v)
    return [float(m.group(1)), float(m.group(2))] if m else None

def ext_id(v):
    m = re.match(r'ExtResource\("([^"]+)"\)', v)
    return m.group(1) if m else None

def sprite_name(res_path):
    return os.path.splitext(os.path.basename(res_path))[0]

sub_cache = {}
def load_subscene(res_path):
    """Root sprite data of an instanced building/tree scene."""
    if res_path in sub_cache: return sub_cache[res_path]
    ext, nodes = parse_tscn(res_to_path(res_path))
    root = nodes[0]; p = root["props"]
    info = {"position": vec2(p.get("position", "Vector2(0, 0)")),
            "z": int(p.get("z_index", "0")),
            "texture": ext[ext_id(p["texture"])][1] if "texture" in p else None,
            "script": ext[ext_id(p["script"])][1] if "script" in p else None,
            "livingHouse": p.get("_livingHouse") == "true",
            "hasDiggingBuddy": p.get("_hasDiggingBuddy") == "true",
            "polygon": None}
    for n in nodes:
        if n["type"] == "CollisionPolygon2D":
            off = vec2(n["props"].get("position", "Vector2(0, 0)"))
            nums = [float(x) for x in re.findall(r"[-\d.]+", n["props"]["polygon"].split("(", 1)[1])]
            info["polygon"] = [round(nums[i] + off[i % 2], 3) for i in range(len(nums))]
    sub_cache[res_path] = info
    return info

ext, nodes = parse_tscn(TOWN)
by_path = {}
for n in nodes:
    path = n["name"] if n["parent"] in (None, ".") else n["parent"] + "/" + n["name"]
    n["path"] = path
    by_path[path] = n

ground = by_path["Town Ui/ground"]
gtex = ext[ext_id(ground["props"]["texture"])][1]

objects = []
order = 0
def walk(parent_path, parent_z, parent_offset):
    global order
    for n in nodes:
        if n["parent"] != parent_path: continue
        p = n["props"]
        if p.get("visible") == "false": continue
        sub = load_subscene(ext[n["instance"]][1]) if n["instance"] else None
        pos = vec2(p["position"]) if "position" in p else (sub["position"] if sub else [0.0, 0.0])
        z = parent_z + int(p.get("z_index", str(sub["z"] if sub else 0)))
        world = [parent_offset[0] + pos[0], parent_offset[1] + pos[1]]
        tex = None
        if "texture" in p: tex = ext[ext_id(p["texture"])][1]
        elif sub: tex = sub["texture"]
        if tex:
            script = sub["script"] if sub else None
            kind = "decor"
            if script and script.endswith("TownBuilding.cs"): kind = "building"
            elif script and script.endswith("TownTree.cs"): kind = "tree"
            objects.append({
                "name": n["path"].replace("Town Ui/ground/", ""),
                "sprite": sprite_name(tex),
                "x": world[0], "y": world[1], "z": z, "order": order,
                "kind": kind,
                "livingHouse": (p.get("_livingHouse") == "true") if "_livingHouse" in p else bool(sub and sub["livingHouse"]),
                "hasDiggingBuddy": (p.get("_hasDiggingBuddy") == "true") if "_hasDiggingBuddy" in p else bool(sub and sub["hasDiggingBuddy"]),
                "polygon": (sub["polygon"] if sub and kind != "decor" else None) or [],
            })
            order += 1
        walk(n["path"], z, world)

walk("Town Ui/ground", 0, [0.0, 0.0])

layout = {"source": "Godot Scenes/Museum/Town/new town.tscn (node Town Ui/ground)",
          "groundSprite": sprite_name(gtex), "objects": objects}
out = sys.argv[1]
with open(out, "w", encoding="utf-8") as f:
    json.dump(layout, f, indent=1)
print(len(objects), "objects;", "sprites:", sorted({o["sprite"] for o in objects} | {layout["groundSprite"]}))
