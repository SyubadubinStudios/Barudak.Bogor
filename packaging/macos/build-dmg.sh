#!/usr/bin/env bash
# Wraps a published osx folder into "Barudak Bogor.app" inside a .dmg.
#   packaging/macos/build-dmg.sh <version> <rid> <publish folder> <output folder>
# The app is ad-hoc signed only: without a Developer ID, macOS asks the player to allow it
# (right-click > Open) the first time.
set -euo pipefail

version="$1"
rid="$2"
publish="$3"
output="$4"
here="$(cd "$(dirname "$0")" && pwd)"
stage="$(mktemp -d)"
app="$stage/Barudak Bogor.app"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$output"
cp -R "$publish"/. "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/BarudakBogor"
sed "s/@VERSION@/${version}/g" "$here/Info.plist" > "$app/Contents/Info.plist"

# Build the .icns from the PNG with the tools every Mac has.
iconset="$stage/BarudakBogor.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$here/../icon_512.png" --out "$iconset/icon_${size}x${size}.png" > /dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/BarudakBogor.icns"

codesign --force --deep --sign - "$app"
ln -s /Applications "$stage/Applications"
rm -rf "$iconset"
hdiutil create -volname "Barudak Bogor" -srcfolder "$stage" -ov -format UDZO \
  "$output/BarudakBogor-${version}-${rid}.dmg"
