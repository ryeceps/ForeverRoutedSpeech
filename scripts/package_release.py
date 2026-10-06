"""Package a previously built portable app and ready-to-install addon; Python standard library only."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--interface", type=int, required=True, help="Observed client Interface number")
    args = parser.parse_args()
    if not 1 <= args.interface <= 9999999:
        parser.error("Interface number must be positive and within the addon installer's range")
    root = Path(__file__).resolve().parents[1]
    dist = root / "dist"
    package = dist / "ForeverRoutedSpeech"
    required = ["Start.cmd", "ForeverRoutedSpeech.exe", "models/ggml-large-v3-turbo-q5_0.bin",
                "models/router.bin", "voice_router_native.dll", "prerequisites/vc_redist.x64.exe"]
    for name in required:
        if not (package / name).is_file():
            raise RuntimeError("Package is missing: " + name)
    expected_model = "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2"
    if digest(package / "models/ggml-large-v3-turbo-q5_0.bin") != expected_model:
        raise RuntimeError("Pinned Turbo model checksum mismatch")
    manifest = [{"path": p.relative_to(package).as_posix(), "bytes": p.stat().st_size, "sha256": digest(p)}
                for p in sorted(package.rglob("*")) if p.is_file() and p.name != "package-manifest.json"]
    (package / "package-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    addon_zip = dist / "VoiceRouter-addon.zip"
    with zipfile.ZipFile(addon_zip, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted((root / "addon/VoiceRouter").iterdir()):
            if path.name == "VoiceRouter.toc.in":
                archive.writestr("VoiceRouter/VoiceRouter.toc", path.read_text(encoding="utf-8").replace("@INTERFACE@", str(args.interface)))
            elif path.is_file():
                archive.write(path, "VoiceRouter/" + path.name)
        archive.writestr("INSTALL.txt", "Replace the VoiceRouter addon folder with this version. Copy it into the game's Interface/AddOns folder (currently _classic_beta_/Interface/AddOns). Enable it and reload WoW once. Use the matching updated app. No status strip, calibration or context setup. Interface: " + str(args.interface) + ".\n")
    app_zip = dist / "ForeverRoutedSpeech-windows-x64.zip"
    with zipfile.ZipFile(app_zip, "w", zipfile.ZIP_DEFLATED, compresslevel=3) as archive:
        for path in sorted(package.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(package).as_posix())
    print("Archives created; checking every member.", flush=True)
    for path in [app_zip, addon_zip]:
        with zipfile.ZipFile(path) as archive:
            bad = archive.testzip()
            if bad:
                raise RuntimeError("Corrupt ZIP member: " + bad)
            if len(archive.namelist()) != len(set(archive.namelist())):
                raise RuntimeError("Duplicate ZIP members")
    entries = [{"name": p.name, "bytes": p.stat().st_size, "sha256": digest(p)} for p in [app_zip, addon_zip]]
    (dist / "SHA256SUMS.txt").write_text("".join(f"{e['sha256']}  {e['name']}\n" for e in entries), encoding="utf-8")
    result = {"commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(), "files": entries}
    (dist / "preview2-manifest.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2), flush=True)


if __name__ == "__main__":
    main()
