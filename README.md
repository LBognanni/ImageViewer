# ImageViewer

Fast &amp; private image viewer for Windows, Linux and macOS.

Built for browsing folders of high-resolution pictures: images are decoded to screen size ahead of you as you move through a folder, so the next one is usually ready before you ask for it. Nothing is written to disk about what you've viewed.

Opens the usual web formats, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, PSD, SVG, EXR, TGA, DDS and dozens more, as well as raw files from all the major camera makers. Raw files open as fast as a JPEG, because the app shows the preview the camera stored in them. Press H in the app to see the keyboard shortcuts.

## Install

Download the file for your system from the [latest release](https://github.com/LBognanni/ImageViewer/releases/latest).

**Windows (x64):** run `ImageViewer_Setup.exe`. The installer isn't signed, so if SmartScreen stops it, choose More info, then Run anyway.

**macOS (Apple Silicon):** open the `.dmg` and drag Image Viewer to Applications. The app isn't notarized, so macOS blocks it the first time you open it. Go to System Settings, then Privacy & Security, and choose Open Anyway.

**Linux (x64, glibc 2.35 or newer):** either use the AppImage, or extract the tarball and run `ImageViewer` from that folder. The AppImage needs to be made executable first:

```sh
chmod +x ImageViewer-*.AppImage
./ImageViewer-*.AppImage
```

To make it your default image viewer, press P in the app and use the button in Preferences.

## License

ImageViewer is free software, licensed under the [GNU General Public License v3.0](LICENSE.md).
