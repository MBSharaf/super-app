"""Keep only the shape of a TRELLIS GLB (indices + positions); drop the baked texture and UVs.
The site colours the garment itself, so the texture (with baked light) is not needed.
usage: python slim.py in.glb out.glb"""
import json, struct, sys

src, dst = sys.argv[1], sys.argv[2]
b = open(src, "rb").read()
jl = struct.unpack("<I", b[12:16])[0]
j = json.loads(b[20:20 + jl])
bin0 = 20 + jl + 8
prim = j["meshes"][0]["primitives"][0]


def chunk(acc_i):
    a = j["accessors"][acc_i]
    bv = j["bufferViews"][a["bufferView"]]
    off = bin0 + bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    comp = {5121: 1, 5123: 2, 5125: 4, 5126: 4}[a["componentType"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[a["type"]]
    return a, b[off:off + a["count"] * comp * n]


ia, idata = chunk(prim["indices"])
pa, pdata = chunk(prim["attributes"]["POSITION"])
pad = lambda d: d + b"\0" * (-len(d) % 4)
idata, pdata = pad(idata), pad(pdata)
binbuf = idata + pdata
out = {
    "asset": {"version": "2.0", "generator": "TRELLIS (slimmed for Super App)"},
    "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0, "name": "garment"}],
    "meshes": [{"primitives": [{"attributes": {"POSITION": 1}, "indices": 0, "mode": 4, "material": 0}]}],
    "materials": [{"pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0, "roughnessFactor": 1}}],
    "buffers": [{"byteLength": len(binbuf)}],
    "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": len(idata), "target": 34963},
                    {"buffer": 0, "byteOffset": len(idata), "byteLength": len(pdata), "target": 34962}],
    "accessors": [{"bufferView": 0, "componentType": ia["componentType"], "count": ia["count"], "type": "SCALAR"},
                  {"bufferView": 1, "componentType": 5126, "count": pa["count"], "type": "VEC3", "min": pa["min"], "max": pa["max"]}],
}
js = json.dumps(out, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
glb = b"glTF" + struct.pack("<II", 2, 12 + 8 + len(js) + 8 + len(binbuf))
glb += struct.pack("<I", len(js)) + b"JSON" + js + struct.pack("<I", len(binbuf)) + b"BIN\0" + binbuf
open(dst, "wb").write(glb)
print(dst, len(glb))
