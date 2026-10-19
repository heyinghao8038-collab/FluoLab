"""Build the optional offline CPU pack from pinned public artifacts, never lab inputs."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import time
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")
CACHE = ROOT / "tools" / "literature-downloads"
STAGE = ROOT / "artifacts" / "literature-component-v1"
CACHE.mkdir(parents=True, exist_ok=True)
STAGE.mkdir(parents=True, exist_ok=True)


def download(url, name, expected=None):
    target = CACHE / name
    if not target.exists() or target.stat().st_size == 0:
        temporary = target.with_suffix(target.suffix + ".downloading")
        request = urllib.request.Request(url, headers={"User-Agent": "FluoLab dependency builder"})
        for attempt in range(4):
            try:
                with urllib.request.urlopen(request, timeout=90) as response, temporary.open("wb") as output:
                    shutil.copyfileobj(response, output, 1024 * 1024)
                break
            except OSError:
                if attempt == 3:
                    raise
                time.sleep(2 + attempt)
        temporary.replace(target)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    if expected and digest != expected:
        raise RuntimeError("Downloaded artifact SHA-256 mismatch: " + name)
    print(json.dumps({"artifact": name, "bytes": target.stat().st_size, "sha256": digest}), flush=True)
    return target, {"url": url, "file": name, "sha256": digest}


def extract(archive_path, target, strip_prefix=""):
    with zipfile.ZipFile(archive_path) as archive:
        for entry in archive.infolist():
            name = entry.filename
            if strip_prefix and name.startswith(strip_prefix):
                name = name[len(strip_prefix):]
            if not name or entry.is_dir():
                continue
            destination = (target / name).resolve()
            if not destination.is_relative_to(target.resolve()) or ":" in name or "\\" in name:
                raise ValueError("Unsafe dependency archive path")
            destination.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(entry) as source, destination.open("wb") as output:
                shutil.copyfileobj(source, output)


artifacts = []
python_zip, info = download("https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip", "python-3.12.10-embed-amd64.zip", "4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3")
artifacts.append(info)
extract(python_zip, STAGE / "python")
(STAGE / "python" / "python312._pth").write_text("python312.zip\n.\nLib/site-packages\n", encoding="utf-8")
versions = {"python": "3.12.10"}
packages = {"ctranslate2": "4.6.0", "sentencepiece": "0.2.1", "numpy": "2.2.6", "pypdfium2": "5.14.0", "Pillow": "12.3.0", "PyYAML": "6.0.3", "setuptools": "80.9.0"}
for package, version in packages.items():
    url = "https://pypi.org/pypi/" + package + ("/" + version if version else "") + "/json"
    metadata_name = "pypi-" + package + "-" + version + ".json"
    previous = CACHE / ("pypi-" + package + "-latest.json")
    if not (CACHE / metadata_name).exists() and previous.exists() and json.loads(previous.read_text(encoding="utf-8"))["info"]["version"] == version:
        shutil.copyfile(previous, CACHE / metadata_name)
    metadata_file, _ = download(url, metadata_name)
    metadata = json.loads(metadata_file.read_text(encoding="utf-8"))
    versions[package] = metadata["info"]["version"]
    candidates = [item for item in metadata["urls"] if item["filename"].endswith(("cp312-cp312-win_amd64.whl", "py3-none-win_amd64.whl", "py3-none-any.whl", "py2.py3-none-any.whl", "cp39-abi3-win_amd64.whl", "cp37-abi3-win_amd64.whl"))]
    if not candidates:
        raise RuntimeError("No compatible Windows x64 Python 3.12 wheel: " + package)
    selected = sorted(candidates, key=lambda item: "win_amd64" not in item["filename"])[0]
    wheel, info = download(selected["url"], selected["filename"], selected["digests"]["sha256"])
    artifacts.append(info)
    extract(wheel, STAGE / "python" / "Lib" / "site-packages")

model, info = download("https://argos-net.com/v1/translate-en_zh-1_9.argosmodel", "translate-en_zh-1_9.argosmodel", "433e7c4f034d87fbe2353161e05f18646d7999452f801a4e1f0378522b9850ab")
artifacts.append(info)
with zipfile.ZipFile(model) as archive:
    entries = archive.namelist()
    prefix = entries[0].split("/")[0] + "/"
    if not all(name.startswith(prefix) for name in entries):
        prefix = ""
extract(model, STAGE / "model", prefix)
versions["model"] = "Argos English-Chinese 1.9"
manifest = {"id": "FluoLab.Literature.cpu.v1", "version": 1, "versions": versions, "artifacts": artifacts}
(STAGE / "component.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
for name in ("LICENSE", "LICENSE.txt", "README", "README.md", "metadata.json"):
    file = STAGE / "model" / name
    if file.exists():
        print(json.dumps({"model_document": name, "preview": file.read_text(encoding="utf-8", errors="replace")[:2500]}, ensure_ascii=False), flush=True)
print("READY: " + str(STAGE), flush=True)
license_path, _ = download("https://creativecommons.org/licenses/by/4.0/legalcode.txt", "CC-BY-4.0.txt")
shutil.copyfile(license_path, STAGE / "model" / "CC-BY-4.0.txt")
shutil.copyfile(ROOT / "Modules" / "Literature" / "THIRD-PARTY-NOTICES.txt", STAGE / "THIRD-PARTY-NOTICES.txt")
distribution = ROOT / "dist" / "FluoLab-Literature-CPU-1.0.zip"
distribution.parent.mkdir(exist_ok=True)
temporary = distribution.with_suffix(".building")
with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for file in sorted(STAGE.rglob("*")):
        if file.is_file() and "__pycache__" not in file.parts and file.suffix != ".pyc":
            archive.write(file, file.relative_to(STAGE).as_posix())
os.replace(temporary, distribution)
digest = hashlib.sha256(distribution.read_bytes()).hexdigest()
(ROOT / "Modules" / "Literature" / "component.sha256").write_text(digest + "\n", encoding="ascii")
(distribution.with_suffix(".zip.sha256")).write_text(digest + "  " + distribution.name + "\n", encoding="ascii")
print(json.dumps({"pack": distribution.name, "bytes": distribution.stat().st_size, "sha256": digest}), flush=True)
