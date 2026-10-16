"""
Markdown → PDF for the backend planning documents (docs/backend/*.md → docs/backend/pdf/*.pdf).

  python tools/docs/md2pdf.py docs/backend/PRD.md          # one document
  python tools/docs/md2pdf.py docs/backend/*.md            # all of them

How it works: Markdown is converted to HTML (python-markdown: tables, fenced code, table of contents), wrapped in a
print stylesheet (A4, cover page, page numbers, Super App colours), and printed to PDF by headless Chrome or Edge.
Mermaid diagrams (```mermaid blocks) are rendered in the browser before printing.
Requires: pip install markdown · Chrome or Edge installed.
"""
import html
import pathlib
import re
import subprocess
import sys

import markdown

BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

CSS = """
@page { size: A4; margin: 18mm 16mm 20mm;
  @bottom-left { content: "Super App — " var(--doc); font: 9px 'Segoe UI', Arial, sans-serif; color: #6b756f; }
  @bottom-right { content: "Page " counter(page) " of " counter(pages); font: 9px 'Segoe UI', Arial, sans-serif; color: #6b756f; } }
@page :first { margin: 0; @bottom-left { content: none; } @bottom-right { content: none; } }
:root { --turf: #0B6E4F; --blaze: #E8590C; --ink: #0D1411; --ink2: #3a4540; --line: #d5dcd7; --soft: #F3F5F2; }
* { box-sizing: border-box; }
body { font: 10.5pt/1.55 'Segoe UI', Arial, sans-serif; color: var(--ink); margin: 0; }
.cover { height: 297mm; padding: 34mm 24mm; display: flex; flex-direction: column; justify-content: space-between; page-break-after: always;
  background: radial-gradient(120% 80% at 100% 0%, #13684a 0%, #0B6E4F 35%, #0D1411 100%); color: #fff; }
.cover .brand { font-weight: 800; letter-spacing: .14em; font-size: 12pt; opacity: .9; }
.cover h1 { font-size: 34pt; line-height: 1.1; margin: 0 0 10px; border: 0; color: #fff; }
.cover .sub { font-size: 14pt; opacity: .85; max-width: 140mm; }
.cover .bar { width: 64px; height: 6px; background: var(--blaze); border-radius: 3px; margin: 18px 0 22px; }
.cover table { color: #fff; border: 0; width: auto; margin: 0; font-size: 10pt; }
.cover td { border: 0; padding: 3px 18px 3px 0; background: none !important; }
.cover td:first-child { opacity: .7; }
.toc-page { page-break-after: always; }
.toc-page h2 { margin-top: 0; }
.toc ul { list-style: none; padding-left: 14px; margin: 2px 0; }
.toc > ul { padding-left: 0; }
.toc a { color: var(--ink); text-decoration: none; }
.toc > ul > li { margin: 6px 0; font-weight: 650; }
.toc > ul > li li { font-weight: 400; font-size: 9.5pt; color: var(--ink2); }
h1, h2, h3, h4 { color: var(--ink); line-height: 1.25; page-break-after: avoid; }
h1 { font-size: 20pt; border-bottom: 3px solid var(--turf); padding-bottom: 6px; margin-top: 0; page-break-before: always; }
h1:first-of-type { page-break-before: auto; }
h2 { font-size: 14.5pt; color: var(--turf); margin-top: 22px; border-bottom: 1px solid var(--line); padding-bottom: 4px; }
h3 { font-size: 12pt; margin-top: 16px; }
h4 { font-size: 10.5pt; margin-top: 12px; color: var(--ink2); }
p, li { orphans: 3; widows: 3; }
a { color: var(--turf); }
table { width: 100%; border-collapse: collapse; margin: 10px 0 14px; font-size: 9pt; page-break-inside: auto; }
tr { page-break-inside: avoid; }
th { background: var(--turf); color: #fff; text-align: left; padding: 6px 8px; font-weight: 650; }
td { border-bottom: 1px solid var(--line); padding: 5px 8px; vertical-align: top; }
tr:nth-child(even) td { background: var(--soft); }
code { font-family: Consolas, 'Cascadia Mono', monospace; font-size: 8.8pt; background: var(--soft); padding: 1px 4px; border-radius: 3px; }
pre { background: #0D1411; color: #e6ede9; padding: 10px 12px; border-radius: 6px; font-size: 8.5pt; line-height: 1.45; white-space: pre-wrap; page-break-inside: avoid; }
pre code { background: none; color: inherit; padding: 0; }
blockquote { margin: 10px 0; padding: 8px 12px; border-left: 4px solid var(--blaze); background: #FFF4EC; color: var(--ink2); }
blockquote p { margin: 4px 0; }
hr { border: 0; border-top: 1px solid var(--line); margin: 18px 0; }
td.rid { white-space: nowrap; font-weight: 650; color: var(--turf); }
.mermaid { text-align: center; margin: 12px 0; page-break-inside: avoid; }
.mermaid svg { max-width: 100%; height: auto; }
"""


def cover(title: str, sub: str, meta: dict) -> str:
    rows = "".join(f"<tr><td>{html.escape(k)}</td><td>{html.escape(v)}</td></tr>" for k, v in meta.items())
    return f"""<section class="cover"><div class="brand">SUPER APP</div>
      <div><h1>{html.escape(title)}</h1><div class="bar"></div><div class="sub">{html.escape(sub)}</div></div>
      <table>{rows}</table></section>"""


def split_meta(text: str):
    """The document starts with '# Title' then a block of 'Key: value' lines until a blank line."""
    lines = text.splitlines()
    title = lines[0].lstrip("# ").strip()
    meta, i = {}, 1
    while i < len(lines) and lines[i].strip() and ":" in lines[i]:
        k, v = lines[i].split(":", 1)
        meta[k.strip()] = v.strip()
        i += 1
    return title, meta, "\n".join(lines[i:])


def render(md_path: pathlib.Path) -> pathlib.Path:
    title, meta, body = split_meta(md_path.read_text(encoding="utf-8"))
    # python-markdown needs a blank line before a list; Markdown written for GitHub often has none
    body = re.sub(r"(?m)^(?!\s*(?:[-*+]|\d+\.)\s)(\S.*)\n(\s*(?:[-*+]|\d+\.)\s)", r"\1\n\n\2", body)
    body = re.sub(r"```mermaid\n(.*?)```", lambda m: f'<div class="mermaid">\n{m.group(1)}</div>', body, flags=re.S)
    md = markdown.Markdown(extensions=["tables", "fenced_code", "toc", "sane_lists", "attr_list"], extension_configs={"toc": {"toc_depth": "1-2"}})
    content = md.convert(body)
    content = re.sub(r"<td>(N?FR-[A-Z]+-[0-9]+)</td>", lambda m: '<td class="rid">' + m.group(1) + '</td>', content)
    has_mermaid = 'class="mermaid"' in content
    sub = meta.pop("Subtitle", "")
    doc_name = md_path.stem
    page = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{html.escape(title)}</title>
      <style>:root{{--doc:"{doc_name}"}}{CSS}</style></head><body>
      {cover(title, sub, meta)}
      <section class="toc-page"><h2>Contents</h2><div class="toc">{md.toc.replace('<div class="toc">', '').rsplit('</div>', 1)[0]}</div></section>
      {content}
      {'<script src="https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js"></script><script>mermaid.initialize({startOnLoad:true,theme:"neutral",er:{useMaxWidth:true},flowchart:{useMaxWidth:true}});</script>' if has_mermaid else ''}
      </body></html>"""
    out_dir = md_path.parent / "pdf"
    out_dir.mkdir(exist_ok=True)
    html_path = out_dir / f"{doc_name}.html"
    pdf_path = out_dir / f"{doc_name}.pdf"
    html_path.write_text(page, encoding="utf-8")
    browser = next((b for b in BROWSERS if pathlib.Path(b).exists()), None)
    if not browser:
        sys.exit("Chrome or Edge not found — open the .html file and print it to PDF.")
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--virtual-time-budget=15000",
                    f"--print-to-pdf={pdf_path.resolve()}", html_path.resolve().as_uri()], check=True, capture_output=True, timeout=180)
    if not pdf_path.exists():
        sys.exit(f"PDF was not created — open {html_path} and print it to PDF.")
    html_path.unlink()
    return pdf_path


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        for p in sorted(pathlib.Path().glob(arg)) if any(c in arg for c in "*?") else [pathlib.Path(arg)]:
            if p.name == "PROGRESS.md":
                continue
            print("PDF:", render(p))
