#!/usr/bin/env bash
# Holt TressFX (Echtzeit-Haar, GDExtension) aus dem GitHub-Release und legt es nach game/addons/tressfx/.
# Version und Prüfsummen stehen in tools/tressfx.version. Braucht nur bash, curl, sha256sum und unzip (oder python3).
# Aufruf:  tools/tressfx_holen.sh [Zielordner]      (Standard: game/addons/tressfx)
# Fehlt das Release (noch nicht gebaut), meldet das Skript es und endet mit 0, damit der Export
# nicht bricht, solange das Spiel TressFX nicht nutzt. Mit TRESSFX_STRENG=1 endet es dann mit 1.
set -euo pipefail

wurzel="$(cd "$(dirname "$0")/.." && pwd)"
ziel="${1:-$wurzel/game/addons/tressfx}"
repo="${TRESSFX_REPO:-B411Y/reichsgilde}"
datei="$wurzel/tools/tressfx.version"

TAG=""
summen=()
while IFS= read -r zeile; do
  case "$zeile" in
    ''|'#'*) ;;
    TAG=*) TAG="${zeile#TAG=}" ;;
    *) summen+=("$zeile") ;;   # "<sha256>  <Dateiname>"
  esac
done < "$datei"

if [ -z "$TAG" ] || [ ${#summen[@]} -eq 0 ]; then
  echo "TressFX: $datei hat noch keinen Tag/keine Prüfsummen – übersprungen."
  [ "${TRESSFX_STRENG:-0}" = 1 ] && exit 1
  exit 0
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
basis="https://github.com/$repo/releases/download/$TAG"

for z in "${summen[@]}"; do
  name="${z##* }"; name="${name#\*}"
  if ! curl -fsSL --retry 3 -o "$tmp/$name" "$basis/$name"; then
    echo "TressFX: $basis/$name nicht ladbar – übersprungen."
    [ "${TRESSFX_STRENG:-0}" = 1 ] && exit 1
    exit 0
  fi
done

# Prüfsummen aus der Versionsdatei (nicht aus dem Release!) prüfen.
printf '%s\n' "${summen[@]}" > "$tmp/summen.txt"
(cd "$tmp" && sha256sum --check --strict summen.txt)

mkdir -p "$ziel/bin"
if command -v unzip >/dev/null; then
  unzip -q -o "$tmp/tressfx-addon.zip" -d "$tmp/addon"
else
  python3 -I -c 'import sys,zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$tmp/tressfx-addon.zip" "$tmp/addon"
fi
cp -r --no-preserve=all "$tmp/addon/addons/tressfx/." "$ziel/"
cp "$tmp"/libtressfx.* "$ziel/bin/"
cp "$tmp"/LICENSE-*.txt "$ziel/"
echo "TressFX $TAG nach $ziel gelegt."
