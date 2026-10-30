#!/bin/bash
# Package the published output as a tarball and an AppImage. Usage: package-linux.sh <runtime> <version>
set -euo pipefail

runtime=$1
version=$2
name="ImageViewer-$version-$runtime"

mkdir -p "dist/$name"
cp -r output/. "dist/$name/"
cp Setup/imageviewer.desktop Setup/imageviewer.png Setup/eula.txt "dist/$name/"
tar -czf "dist/$name.tar.gz" -C dist "$name"
rm -r "dist/${name:?}"

# appimagetool has no package, so fetch a fixed release and check it's the expected file
tool=$(mktemp -d)/appimagetool
curl -fsSL -o "$tool" https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
echo "ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0  $tool" | sha256sum -c --quiet
chmod +x "$tool"

appdir="dist/ImageViewer.AppDir"
mkdir -p "$appdir/usr/bin"
cp -r output/. "$appdir/usr/bin/"
cp Setup/AppRun Setup/imageviewer.desktop Setup/imageviewer.png "$appdir/"

# Runners have no FUSE, and this runs the tool without it
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --no-appstream "$appdir" "dist/$name.AppImage"
rm -r "$appdir"
