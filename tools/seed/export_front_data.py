"""
Exports the front end's demo data (js/data/* and the seeded mock database) to JSON for the backend seed.

How it works: a copy of index.html is opened in headless Chrome (the app starts as usual and seeds its mock
database), then everything in SA.data and every localStorage entry is written into the page and read back.
Output: tools/seed/_front-data.json (raw, not committed) — then build_seed.py turns it into the seed files.

Run from the repository root:  python tools/seed/export_front_data.py
"""
import html
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "tools" / "seed" / "_front-data.json"
PAGE = ROOT / "tools" / "seed" / "_export.html"
BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
]

DUMP = """
<script>
window.addEventListener('load', () => setTimeout(() => {
  const store = {};
  for (let i = 0; i < localStorage.length; i++) {
    const k = localStorage.key(i);
    try { store[k] = JSON.parse(localStorage.getItem(k)); } catch { store[k] = localStorage.getItem(k); }
  }
  const data = { ...SA.data };
  const ar = data.i18nAr?.strings || {};    // only used to give seeded lookups their Arabic names
  delete data.i18nAr;
  delete data.fantasyLeagues;               // same objects as fantasy / fantasyEPL
  const pre = document.createElement('pre');
  pre.id = 'sa-export';
  pre.textContent = JSON.stringify({ exportedAt: Date.now(), data, storage: store, ar });
  document.body.appendChild(pre);
}, 3000));
</script>
"""


def build_page() -> None:
    page = (ROOT / "index.html").read_text(encoding="utf-8")
    page = page.replace("<head>", '<head>\n  <base href="../../">', 1)
    page = page.replace("</body>", DUMP + "</body>", 1)
    PAGE.write_text(page, encoding="utf-8")


def run_browser() -> str:
    browser = next((b for b in BROWSERS if pathlib.Path(b).exists()), None)
    if browser is None:
        sys.exit("Chrome or Edge not found.")
    profile = ROOT / "tools" / "seed" / "_profile"
    cmd = [browser, "--headless=new", "--disable-gpu", "--allow-file-access-from-files",
           f"--user-data-dir={profile}", "--virtual-time-budget=15000", "--dump-dom", PAGE.as_uri()]
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", timeout=120).stdout


def main() -> None:
    build_page()
    dom = run_browser()
    match = re.search(r'<pre id="sa-export">(.*?)</pre>', dom, re.S)
    if not match:
        sys.exit("Export block not found — the page did not finish loading.")
    payload = json.loads(html.unescape(match.group(1)))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    PAGE.unlink(missing_ok=True)
    print(f"Wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB)")
    print("data:", ", ".join(payload["data"].keys()))
    print("storage:", ", ".join(payload["storage"].keys()))


if __name__ == "__main__":
    main()
