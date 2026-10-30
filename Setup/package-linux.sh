#!/bin/bash
# Package the published output as a tarball. Usage: package-linux.sh <runtime> <version>
set -euo pipefail

runtime=$1
version=$2
name="ImageViewer-$version-$runtime"

mkdir -p "dist/$name"
cp -r output/. "dist/$name/"
cp Setup/imageviewer.desktop Setup/eula.txt icon.svg "dist/$name/"
tar -czf "dist/$name.tar.gz" -C dist "$name"
rm -r "dist/${name:?}"
