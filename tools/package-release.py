"""Build portable artifacts from an explicit file allowlist; never package credentials."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT.parent / "outputs"
OUTPUT.mkdir(exist_ok=True)
VERSION = "0.3.0"
PREFIX = "奶蛙V" + VERSION
top = {".gitignore", ".gitattributes", "app.manifest", "README.md", "package.json", "pnpm-lock.yaml",
       "build-windows.ps1", "build-android.ps1", "test-java.ps1"}
files = [ROOT / name for name in top]
for directory in ["src", "tools", "tests", "docs", "assets/source", "android/app/src/main"]:
    files.extend(p for p in (ROOT / directory).rglob("*") if p.is_file()
                 and p.suffix.lower() in {".cs", ".java", ".mjs", ".py", ".md", ".json", ".png", ".gif", ".jpg", ".wav", ".mp4", ".txt", ".xml"})
files += [ROOT / "android/build.gradle", ROOT / "android/settings.gradle",
          ROOT / "android/app/build.gradle", ROOT / "assets/icon.png", ROOT / "assets/naifrog.ico"]
files = sorted(set(files))
assert all(p.is_file() for p in files)
assert all(p.suffix.lower() not in {".p12", ".jks", ".key", ".log"} for p in files)

def entry(p):
    raw = p.read_bytes()
    is_binary = p.suffix.lower() in {".png", ".gif", ".ico", ".jpg", ".wav", ".mp4"}
    return {"path": p.relative_to(ROOT).as_posix(), "binary": is_binary,
            "size": len(raw), "sha256": hashlib.sha256(raw).hexdigest(),
            "gitSha": hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()}

inventory = [entry(p) for p in files]
(ROOT / "build").mkdir(exist_ok=True)
(ROOT / "build/upload-inventory.json").write_text(json.dumps(inventory, ensure_ascii=False), encoding="utf-8")
text_entries = [{"path": p.relative_to(ROOT).as_posix(), "mode": "100644", "type": "blob",
                 "content": p.read_bytes().decode("utf-8")}
                for p in files if p.suffix.lower() not in {".png", ".gif", ".ico", ".jpg", ".wav", ".mp4"}]
(ROOT / "build/upload-text.json").write_text(json.dumps(text_entries, ensure_ascii=False), encoding="utf-8")

with ZipFile(OUTPUT / (PREFIX + "-Windows便携版.zip"), "w", ZIP_DEFLATED) as archive:
    dist = ROOT / "dist/windows"
    for p in sorted(dist.rglob("*")):
        if p.is_file() and "data" not in p.relative_to(dist).parts:
            archive.write(p, Path(PREFIX + "-Windows") / p.relative_to(dist))
with ZipFile(OUTPUT / (PREFIX + "-Windows便携版.zip"), "r") as archive:
    assert archive.testzip() is None, "Windows archive CRC failed"

with ZipFile(OUTPUT / (PREFIX + "-源码与素材.zip"), "w", ZIP_DEFLATED) as archive:
    for p in files:
        archive.write(p, Path("naifrog") / p.relative_to(ROOT))
    for p in sorted([*(ROOT / "assets/derived").rglob("*"), *(ROOT / "assets/windows").rglob("*")]):
        if p.is_file():
            archive.write(p, Path("naifrog") / p.relative_to(ROOT))
with ZipFile(OUTPUT / (PREFIX + "-源码与素材.zip"), "r") as archive:
    assert archive.testzip() is None, "Source archive CRC failed"

shutil.copy2(ROOT / ("dist/android/奶蛙-" + VERSION + "-测试版.apk"), OUTPUT / (PREFIX + "-Android测试版.apk"))
shutil.copy2(ROOT / "docs/STATUS.md", OUTPUT / (PREFIX + "-交付说明.md"))
release = []
for name in [PREFIX + "-Windows便携版.zip", PREFIX + "-源码与素材.zip", PREFIX + "-Android测试版.apk", PREFIX + "-交付说明.md"]:
    p = OUTPUT / name
    release.append({"file": name, "bytes": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
(OUTPUT / (PREFIX + "-SHA256.json")).write_text(json.dumps(release, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"sourceFiles": len(files), "binaryFiles": sum(x["binary"] for x in inventory), "release": release}, ensure_ascii=False))
