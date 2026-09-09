"""
Regenerate every JiggerJot brand raster from the SVG sources checked into the tree.

Sources (editable):      src/Shared.Ui/wwwroot/brand/{icon_light,lockup_light,lockup_dark}.svg,
                         src/Web/wwwroot/favicon.svg, src/Maui/Resources/{AppIcon,Splash}/*.svg
Outputs (this script):   the PNGs next to those sources, src/Web/wwwroot/{favicon.ico,favicon.png,
                         apple_touch_180.png,icon-*.png,og_image_1200x630.png},
                         src/Infrastructure/Email/Assets/logo.png, and the store/marketing set in
                         docs/brand/.

Rendering uses headless Microsoft Edge so the lockup wordmark carries the real webfonts, and
Pillow for the .ico and flattening. Needs network (Google Fonts). Run from anywhere:

    python docs/brand/build_assets.py
"""
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SHARED = ROOT / "src/Shared.Ui/wwwroot/brand"
WEB = ROOT / "src/Web/wwwroot"
EMAIL = ROOT / "src/Infrastructure/Email/Assets"
DOCS_BRAND = ROOT / "docs/brand"

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
FONTS = "https://fonts.googleapis.com/css2?family=Barlow+Semi+Condensed:wght@600&family=IBM+Plex+Sans:wght@500&display=swap"

MARK = ("M28 16 A22 4.2 0 0 1 72 16 L59 46 L41 46 Z M32 16 A18 2.7 0 1 0 68 16 A18 2.7 0 1 0 32 16 Z "
        "M41 47.4 H59 A1 1 0 0 1 60 48.4 V51.6 A1 1 0 0 1 59 52.6 H41 A1 1 0 0 1 40 51.6 V48.4 A1 1 0 0 1 41 47.4 Z "
        "M41 54 L59 54 L76 86 A26 3.8 0 0 1 24 86 Z M28.5 86.4 A21.5 1.6 0 1 0 71.5 86.4 A21.5 1.6 0 1 0 28.5 86.4 Z")

COPPER, BONE, NIGHT, WHITE = "#B4562A", "#F3F1EC", "#111418", "#FFFFFF"
WORK_DIRS = []


def mark_svg(w, h, fill, bg=None, scale=0.78, rx=0):
    """The mark centred on a w×h canvas; `scale` = mark height as a fraction of the shorter side."""
    s = min(w, h) * scale / 78.0
    bg_rect = f'<rect width="{w}" height="{h}" rx="{rx}" fill="{bg}"/>' if bg else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">'
            f'{bg_rect}<g transform="translate({w/2} {h/2}) scale({s}) translate(-50 -50.8)">'
            f'<path fill="{fill}" fill-rule="evenodd" d="{MARK}"/></g></svg>')


def page(inner, w, h, bg="transparent"):
    return (f'<!doctype html><html><head><meta charset="utf-8"><link rel="stylesheet" href="{FONTS}">'
            f'<style>html,body{{margin:0;padding:0;background:{bg};width:{w}px;height:{h}px;overflow:hidden}}'
            f'svg{{display:block}}</style></head><body>{inner}</body></html>')


def render(html, out, w, h):
    """Headless Edge returns before it has loaded the page or written the file: keep the page and
    profile alive and poll for the output."""
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
    label = out.relative_to(ROOT) if str(out).startswith(str(ROOT)) else out.name
    print(f"  {label}  {w}x{h}")


def sized_svg(path, w, h):
    return (Path(path).read_text(encoding="utf-8")).replace("<svg ", f'<svg width="{w}" height="{h}" ', 1)


def flatten(path):
    Image.open(path).convert("RGB").save(path)


def main():
    if not Path(EDGE).exists():
        sys.exit(f"Edge not found at {EDGE}")
    print("Rendering...")

    # Shared.Ui: icon + lockups (transparent)
    render(page(sized_svg(SHARED / "icon_light.svg", 1024, 1024), 1024, 1024), SHARED / "icon_light_1024.png", 1024, 1024)
    for name in ("lockup_light", "lockup_dark"):
        render(page(sized_svg(SHARED / f"{name}.svg", 1520, 392), 1520, 392), SHARED / f"{name}_1520.png", 1520, 392)

    # Web chrome
    render(page(mark_svg(32, 32, COPPER, scale=0.9), 32, 32), WEB / "favicon.png", 32, 32)
    render(page(mark_svg(180, 180, BONE, bg=NIGHT, scale=0.7), 180, 180), WEB / "apple_touch_180.png", 180, 180)
    render(page(mark_svg(192, 192, BONE, bg=NIGHT, scale=0.7), 192, 192), WEB / "icon-192.png", 192, 192)
    render(page(mark_svg(512, 512, BONE, bg=NIGHT, scale=0.7), 512, 512), WEB / "icon-512.png", 512, 512)
    render(page(mark_svg(512, 512, BONE, bg=NIGHT, scale=0.56), 512, 512), WEB / "icon-maskable-512.png", 512, 512)
    og = ('<div style="width:1200px;height:630px;background:#111418;display:grid;place-items:center">'
          + sized_svg(SHARED / "lockup_dark.svg", 930, 240) + "</div>")
    render(page(og, 1200, 630, bg=NIGHT), WEB / "og_image_1200x630.png", 1200, 630)
    for p in ("apple_touch_180.png", "icon-192.png", "icon-512.png", "icon-maskable-512.png", "og_image_1200x630.png"):
        flatten(WEB / p)

    # favicon.ico from a clean 256 render
    big = Path(tempfile.mkdtemp(prefix="jj-brand-")) / "fav256.png"; WORK_DIRS.append(big.parent)
    render(page(mark_svg(256, 256, COPPER, scale=0.9), 256, 256), big, 256, 256)
    Image.open(big).save(WEB / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])
    print("  src/Web/wwwroot/favicon.ico  16/32/48")

    # Email logo: flat PNG, no transparency (email clients)
    render(page(mark_svg(256, 256, COPPER, bg=WHITE, scale=0.76), 256, 256, bg=WHITE), EMAIL / "logo.png", 256, 256)
    flatten(EMAIL / "logo.png")

    # Store / marketing set (docs/brand) — NEW_APP_GUIDE Phase 9
    render(page(mark_svg(1024, 1024, BONE, bg=NIGHT, scale=0.7), 1024, 1024), DOCS_BRAND / "app_store_icon_1024.png", 1024, 1024)
    render(page(mark_svg(512, 512, BONE, bg=NIGHT, scale=0.7), 512, 512), DOCS_BRAND / "play_store_icon_512.png", 512, 512)
    render(page(mark_svg(432, 432, BONE, scale=0.61), 432, 432), DOCS_BRAND / "android_adaptive_foreground_432.png", 432, 432)
    render(page(mark_svg(300, 300, BONE, bg=NIGHT, scale=0.7, rx=48), 300, 300), DOCS_BRAND / "linkedin_logo_300.png", 300, 300)
    banner = ('<div style="width:1128px;height:191px;background:#111418;display:grid;place-items:center">'
              + sized_svg(SHARED / "lockup_dark.svg", 620, 160) + "</div>")
    render(page(banner, 1128, 191, bg=NIGHT), DOCS_BRAND / "linkedin_banner_1128x191.png", 1128, 191)
    for p in ("app_store_icon_1024.png", "play_store_icon_512.png", "linkedin_banner_1128x191.png"):
        flatten(DOCS_BRAND / p)

    time.sleep(2)
    for d in WORK_DIRS:
        shutil.rmtree(d, ignore_errors=True)
    print("Done.")


if __name__ == "__main__":
    main()
