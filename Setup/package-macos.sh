#!/bin/bash
# Package the published output as an unsigned .app in a drag-and-drop dmg. Usage: package-macos.sh <runtime> <version>
set -euo pipefail

runtime=$1
version=$2
staging="dist/dmg"
app="$staging/Image Viewer.app"
dmg="dist/ImageViewer-$version-$runtime.dmg"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -r output/. "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" Setup/Info.plist > "$app/Contents/Info.plist"

# Apple Silicon refuses to run anything without at least an ad-hoc signature
codesign --force --deep --sign - "$app"

ln -s /Applications "$staging/Applications"
hdiutil create -volname "Image Viewer $version" -srcfolder "$staging" -format UDZO -ov "$dmg"
hdiutil verify "$dmg"
rm -r "$staging"
