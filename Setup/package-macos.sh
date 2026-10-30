#!/bin/bash
# Package the published output as an unsigned .app in a zip. Usage: package-macos.sh <runtime> <version>
set -euo pipefail

runtime=$1
version=$2
app="dist/Image Viewer.app"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -r output/. "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" Setup/Info.plist > "$app/Contents/Info.plist"

# Apple Silicon refuses to run anything without at least an ad-hoc signature
codesign --force --deep --sign - "$app"

(cd dist && zip -qry "ImageViewer-$version-$runtime.zip" "Image Viewer.app")
rm -r "$app"
