#!/usr/bin/env bash
# Build installable plugin zips into ./dist
#
#   ./build.sh            -> Jellyfin 12.x (default)
#   ./build.sh 10.11      -> Jellyfin 10.11.x
#   ./build.sh all        -> both
#
# Requires the .NET SDK (10.0 for Jellyfin 12, 9.0 or 10.0 for Jellyfin 10.11) and python3.
set -euo pipefail
cd "$(dirname "$0")"

PLUGIN_VERSION="${PLUGIN_VERSION:-2.0.0}"
PROJECT=Jellyfin.Plugin.CustomLogo/Jellyfin.Plugin.CustomLogo.csproj

build_one() {
  local jf="$1"
  local out="artifacts/jf$jf"
  echo "==> Building Custom Logo $PLUGIN_VERSION for Jellyfin $jf"
  rm -rf "$out"
  dotnet publish "$PROJECT" -c Release -o "$out" \
    -p:JellyfinVersion="$jf" -p:PluginVersion="$PLUGIN_VERSION"
  python3 scripts/package.py --bin "$out" --jellyfin "$jf" --version "$PLUGIN_VERSION"
}

case "${1:-12}" in
  12|12.*)  build_one 12.0.0 ;;
  10|10.11*) build_one 10.11.0 ;;
  all)      build_one 12.0.0; build_one 10.11.0 ;;
  *) echo "usage: $0 [12|10.11|all]"; exit 1 ;;
esac

echo
echo "Done. Zips are in ./dist"
