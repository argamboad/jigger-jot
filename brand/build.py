"""
Rasterize the JiggerJot brand assets from the SVG sources in this folder.

The SVGs are the editable source. This script renders every PNG the platform's REBRANDING.md
expects (at the platform's exact sizes) plus favicon.ico, using headless Microsoft Edge so the
lockup wordmark renders with the real webfonts. Output lands next to the sources, in the same
sub-folder layout as the platform tree, so Phase 3 is a copy:

  Shared.Ui/  -> src/Shared.Ui/wwwroot/brand/
  Web/        -> src/Web/wwwroot/
  Maui/       -> src/Maui/Resources/AppIcon/ and Resources/Splash/
  Email/      -> src/Infrastructure/Email/Assets/

Run:  python brand/build.py      (needs network for Google Fonts, Edge, Pillow)
"""
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
FONTS = "https://fonts.googleapis.com/css2?family=Barlow+Semi+Condensed:wght@600&family=IBM+Plex+Sans:wght@500&display=swap"

MARK = ("M28 16 A22 4.2 0 0 1 72 16 L59 46 L41 46 Z M32 16 A18 2.7 0 1 0 68 16 A18 2.7 0 1 0 32 16 Z "
        "M41 47.4 H59 A1 1 0 0 1 60 48.4 V51.6 A1 1 0 0 1 59 52.6 H41 A1 1 0 0 1 40 51.6 V48.4 A1 1 0 0 1 41 47.4 Z "
        "M41 54 L59 54 L76 86 A26 3.8 0 0 1 24 86 Z M28.5 86.4 A21.5 1.6 0 1 0 71.5 86.4 A21.5 1.6 0 1 0 28.5 86.4 Z")

COPPER, BONE, NIGHT, WHITE = "#B4562A", "#F3F1EC", "#111418", "#FFFFFF"


def mark_svg(size, fill, bg=None, scale=0.78, rx=0):
    """The mark centred on a square canvas; `scale` = mark height as a fraction of the canvas."""
    s = size * scale / 78.0
    bg_rect = f'<rect width="{size}" height="{size}" rx="{rx}" fill="{bg}"/>' if bg else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}">'
            f'{bg_rect}<g transform="translate({size/2} {size/2}) scale({s}) translate(-50 -50.8)">'
            f'<path fill="{fill}" fill-rule="evenodd" d="{MARK}"/></g></svg>')


def page(inner, w, h, bg="transparent"):
    return (f'<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="{FONTS}">'
            f'<style>html,body{{margin:0;padding:0;background:{bg};width:{w}px;height:{h}px;overflow:hidden}}'
            f'svg{{display:block}}</style></head><body>{inner}</body></html>')


WORK_DIRS = []


def render(html, out, w, h):
    """Headless Edge returns before it has loaded the page or written the file, so the page and
    profile must outlive the launcher and the output has to be polled for."""
    out = Path(out); out.parent.mkdir(parents=True, exist_ok=True)
    if out.exists():
        out.unlink()
    td = Path(tempfile.mkdtemp(prefix="jj-brand-")); WORK_DIRS.append(td)
    src = td / "in.html"; src.write_text(html, encoding="utf-8")
    cmd = [EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
           "--disable-extensions", "--force-device-scale-factor=1", "--default-background-color=00000000",
           f"--user-data-dir={td / 'profile'}", f"--window-size={w},{h}", "--virtual-time-budget=10000",
           f"--screenshot={out}", src.as_uri()]
    subprocess.run(cmd, check=True, capture_output=True, timeout=120)
    deadline = time.time() + 90; last = -1
    while time.time() < deadline:
        if out.exists():
            size = out.stat().st_size
            if size > 0 and size == last:
                break
            last = size
        time.sleep(0.5)
    else:
        raise RuntimeError(f"timed out waiting for Edge to write {out}")
    im = Image.open(out)
    assert im.size == (w, h), f"{out.name}: got {im.size}, wanted {(w, h)}"
    print(f"  {out.relative_to(HERE) if out.is_relative_to(HERE) else out.name}  {w}x{h}")


def svg_file(rel):
    return (HERE / rel).read_text(encoding="utf-8")


def main():
    if not Path(EDGE).exists():
        sys.exit(f"Edge not found at {EDGE}")
    print("Rendering PNGs...")

    # Shared.Ui: icon + lockups (transparent)
    render(page(svg_file("Shared.Ui/icon_light.svg").replace("<svg ", '<svg width="1024" height="1024" ', 1), 1024, 1024),
           HERE / "Shared.Ui/icon_light_1024.png", 1024, 1024)
    for name in ("lockup_light", "lockup_dark"):
        render(page(svg_file(f"Shared.Ui/{name}.svg").replace("<svg ", '<svg width="1520" height="392" ', 1), 1520, 392),
               HERE / f"Shared.Ui/{name}_1520.png", 1520, 392)

    # Web chrome
    render(page(mark_svg(32, COPPER, scale=0.9), 32, 32), HERE / "Web/favicon.png", 32, 32)
    render(page(mark_svg(180, BONE, bg=NIGHT, scale=0.7), 180, 180), HERE / "Web/apple_touch_180.png", 180, 180)
    render(page(mark_svg(192, BONE, bg=NIGHT, scale=0.7), 192, 192), HERE / "Web/icon-192.png", 192, 192)
    render(page(mark_svg(512, BONE, bg=NIGHT, scale=0.7), 512, 512), HERE / "Web/icon-512.png", 512, 512)
    render(page(mark_svg(512, BONE, bg=NIGHT, scale=0.56), 512, 512), HERE / "Web/icon-maskable-512.png", 512, 512)

    og = ('<div style="width:1200px;height:630px;background:#111418;display:grid;place-items:center">'
          + svg_file("Shared.Ui/lockup_dark.svg").replace("<svg ", '<svg width="930" height="240" ', 1) + "</div>")
    render(page(og, 1200, 630, bg=NIGHT), HERE / "Web/og_image_1200x630.png", 1200, 630)

    # favicon.ico from a clean 256 render
    big = Path(tempfile.mkdtemp(prefix="jj-brand-")) / "fav256.png"; WORK_DIRS.append(big.parent)
    render(page(mark_svg(256, COPPER, scale=0.9), 256, 256), big, 256, 256)
    Image.open(big).save(HERE / "Web/favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])
    print("  Web/favicon.ico  16/32/48")

    # Email logo: flat PNG, no transparency
    render(page(mark_svg(256, COPPER, bg=WHITE, scale=0.76), 256, 256, bg=WHITE), HERE / "Email/logo.png", 256, 256)
    im = Image.open(HERE / "Email/logo.png").convert("RGB"); im.save(HERE / "Email/logo.png")

    time.sleep(2)
    for d in WORK_DIRS:
        shutil.rmtree(d, ignore_errors=True)
    print("Done.")


if __name__ == "__main__":
    main()
