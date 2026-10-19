"""Convert the supplied cover into launcher and Windows icons, preserving its design."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
with Image.open(root / "assets/source/app-cover.jpg") as original:
    icon = original.convert("RGB").resize((512, 512), Image.Resampling.LANCZOS)
icon.save(root / "assets/icon.png")
icon.save(root / "assets/naifrog.ico", sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
android = root / "android/app/src/main/res/drawable/naifrog.png"
android.write_bytes((root / "assets/icon.png").read_bytes())
print("Application cover and seven Windows icon sizes prepared.")
