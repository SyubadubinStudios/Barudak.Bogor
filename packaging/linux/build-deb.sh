#!/usr/bin/env bash
# Builds a .deb and a .tar.gz from a published linux-x64 folder.
#   packaging/linux/build-deb.sh <version> <publish folder> <output folder>
set -euo pipefail

version="$1"
publish="$2"
output="$3"
here="$(cd "$(dirname "$0")" && pwd)"
root="$(mktemp -d)/barudak-bogor_${version}_amd64"

mkdir -p "$root/DEBIAN" "$root/opt/barudak-bogor" "$root/usr/bin" \
         "$root/usr/share/applications" "$root/usr/share/icons/hicolor/512x512/apps" "$output"
cp -r "$publish"/. "$root/opt/barudak-bogor/"
chmod +x "$root/opt/barudak-bogor/BarudakBogor"
cp "$here/../icon_512.png" "$root/usr/share/icons/hicolor/512x512/apps/barudak-bogor.png"
cp "$here/barudak-bogor.desktop" "$root/usr/share/applications/"

# A launcher rather than a symlink: the game finds its assets next to the executable.
cat > "$root/usr/bin/barudak-bogor" <<'LAUNCH'
#!/bin/sh
exec /opt/barudak-bogor/BarudakBogor "$@"
LAUNCH
chmod +x "$root/usr/bin/barudak-bogor"

size_kb="$(du -sk "$root/opt" | cut -f1)"
cat > "$root/DEBIAN/control" <<CONTROL
Package: barudak-bogor
Version: ${version}
Section: games
Priority: optional
Architecture: amd64
Installed-Size: ${size_kb}
Depends: libx11-6, libice6, libsm6, libfontconfig1, libvulkan1, libasound2 | libasound2t64
Maintainer: Syubadubin Studios
Homepage: https://github.com/SyubadubinStudios/Barudak.Bogor
Description: Barudak Bogor - open-world game set in the rainy city of Bogor
 Hujanna Asli, Petualanganna Teu Aya Tungtungna.
 Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
CONTROL

dpkg-deb --build --root-owner-group "$root" "$output/BarudakBogor-${version}-linux-x64.deb"
tar -C "$publish" -czf "$output/BarudakBogor-${version}-linux-x64.tar.gz" .
