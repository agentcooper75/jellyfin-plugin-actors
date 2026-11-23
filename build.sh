#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "${ROOT}"
export PATH="${HOME}/.dotnet:${PATH}"

VERSION="${1:-}"
if [[ -z "${VERSION}" ]]; then
  VERSION="$(python3 - <<'PY'
import re, pathlib
text = pathlib.Path("build.yaml").read_text()
m = re.search(r'^version:\s*"([^"]+)"', text, re.M)
print(m.group(1) if m else "1.0.0.0")
PY
)"
fi

CHANGELOG="$(python3 - <<'PY'
import pathlib, re
text = pathlib.Path("build.yaml").read_text()
m = re.search(r'^changelog:\s*\|-\s*\n((?:  .*\n?)+)', text, re.M)
if not m:
    print("Release")
else:
    lines = [ln[2:] if ln.startswith("  ") else ln for ln in m.group(1).splitlines()]
    print("\n".join(lines).strip() or "Release")
PY
)"
export CHANGELOG

STAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
OUT_DIR="${ROOT}/artifacts/package"
ZIP_NAME="jellyfin-plugin-actors_${VERSION}.zip"
ZIP_PATH="${ROOT}/artifacts/${ZIP_NAME}"
REPO="${GITHUB_REPOSITORY:-agentcooper75/jellyfin-plugin-actors}"

rm -rf "${OUT_DIR}"
mkdir -p "${OUT_DIR}" "${ROOT}/artifacts"

dotnet publish "${ROOT}/src/Jellyfin.Plugin.Actors/Jellyfin.Plugin.Actors.csproj" \
  -c Release \
  -p:Version="${VERSION}" \
  -p:FileVersion="${VERSION}" \
  -p:AssemblyVersion="${VERSION}" \
  -p:InformationalVersion="${VERSION}" \
  -o "${OUT_DIR}/publish"

cp "${OUT_DIR}/publish/Jellyfin.Plugin.Actors.dll" "${OUT_DIR}/Jellyfin.Plugin.Actors.dll"

python3 - <<PY
import json, os, pathlib
meta = {
  "category": "General",
  "changelog": os.environ["CHANGELOG"],
  "description": "Schauspieler-Übersicht, gefiltert auf fest konfigurierte Libraries.",
  "guid": "7c2e9a41-5f8b-4d3e-9a1c-0b6e8f2a4c10",
  "name": "Actors",
  "overview": "Browse actors from selected libraries only.",
  "owner": "jellyView",
  "targetAbi": "12.0.0.0",
  "timestamp": "${STAMP}",
  "version": "${VERSION}"
}
pathlib.Path("${OUT_DIR}/meta.json").write_text(json.dumps(meta, indent=2) + "\n")
PY

(
  cd "${OUT_DIR}"
  rm -f "${ZIP_PATH}"
  zip -q -r "${ZIP_PATH}" Jellyfin.Plugin.Actors.dll meta.json
)

CHECKSUM="$(md5 -q "${ZIP_PATH}" 2>/dev/null || md5sum "${ZIP_PATH}" | awk '{print $1}')"

python3 - <<PY
import json, pathlib, os
manifest_path = pathlib.Path("${ROOT}/manifest.json")
version = "${VERSION}"
checksum = "${CHECKSUM}"
stamp = "${STAMP}"
changelog = os.environ["CHANGELOG"]
repo = "${REPO}"
source_url = f"https://github.com/{repo}/releases/download/v{version}/jellyfin-plugin-actors_{version}.zip"

if manifest_path.exists():
    manifest = json.loads(manifest_path.read_text())
else:
    manifest = [{
        "guid": "7c2e9a41-5f8b-4d3e-9a1c-0b6e8f2a4c10",
        "name": "Actors",
        "description": "Schauspieler-Übersicht, gefiltert auf fest konfigurierte Libraries.",
        "overview": "Browse actors from selected libraries only.",
        "owner": "jellyView",
        "category": "General",
        "versions": []
    }]

plugin = manifest[0]
versions = [v for v in plugin.get("versions", []) if v.get("version") != version]
entry = {
    "version": version,
    "changelog": changelog,
    "targetAbi": "12.0.0.0",
    "sourceUrl": source_url,
    "checksum": checksum,
    "timestamp": stamp
}
versions.insert(0, entry)
plugin["versions"] = versions
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
print(f"Updated {manifest_path}")
print(f"Zip: {os.path.basename('${ZIP_PATH}')}")
print(f"MD5: {checksum}")
print(f"sourceUrl: {source_url}")
PY

echo
echo "Local package ready:"
echo "  ${ZIP_PATH}"
