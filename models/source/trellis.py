"""Image(s) -> GLB through the TRELLIS Hugging Face space (gradio sse_v3 protocol).
usage: python trellis.py out.glb img1 [img2 ...]   (2+ images = multi-image mode)"""
import json, random, string, sys, time
import requests

BASE = "https://trellis-community-trellis.hf.space/gradio_api"
out, imgs = sys.argv[1], sys.argv[2:]
sess = "".join(random.choices(string.ascii_lowercase + string.digits, k=11))
S = requests.Session()


def upload(path):
    with open(path, "rb") as f:
        r = S.post(f"{BASE}/upload", files={"files": (path.split("/")[-1], f, "image/png")}, timeout=120)
    r.raise_for_status()
    p = r.json()[0]
    return {"path": p, "url": f"{BASE}/file={p}", "orig_name": path.split("/")[-1], "meta": {"_type": "gradio.FileData"}}


def call(fn, data):
    r = S.post(f"{BASE}/queue/join", json={"data": data, "fn_index": fn, "session_hash": sess, "event_data": None, "trigger_id": None}, timeout=60)
    r.raise_for_status()
    with S.get(f"{BASE}/queue/data", params={"session_hash": sess}, stream=True, timeout=900) as st:
        for line in st.iter_lines():
            if not line or not line.startswith(b"data:"):
                continue
            m = json.loads(line[5:])
            if m.get("msg") in ("estimation", "process_starts"):
                print(m.get("msg"), m.get("rank"), m.get("rank_eta"), flush=True)
            if m.get("msg") == "process_completed":
                if not m.get("success"):
                    raise SystemExit(f"failed: {json.dumps(m)[:800]}")
                return m["output"]["data"]


call(4, [])  # start_session
files = [upload(p) for p in imgs]
multi = len(files) > 1
if multi:
    gal = call(9, [[{"image": f, "caption": None} for f in files]])[0]
    single = None
else:
    single = call(8, [files[0]])[0]
    gal = []
res = call(11, [single, gal, multi, 0, 7.5, 12, 3.0, 12, "stochastic", 0.95, 1024])
print(json.dumps(res)[:600])
glb = res[2] if isinstance(res[2], dict) else res[3]
url = glb.get("url") or f"{BASE}/file={glb['path']}"
b = S.get(url, timeout=300)
b.raise_for_status()
open(out, "wb").write(b.content)
print("saved", out, len(b.content))
