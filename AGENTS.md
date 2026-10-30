# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Fast, private image viewer for Windows, Linux and macOS: an Avalonia app on .NET 10, published with Native AOT. **The design goal is the fastest possible browsing of folders of high-resolution pictures**, so judge changes by the time from keypress to sharp image, and measure rather than assume.

## Commands

The build is a NUKE project in `build/`.

```sh
./build.sh                      # Linux/macOS: publish for this machine (default target)
./build.ps1                     # Windows
./build.sh --target Clean       # empties output/
./build.sh --runtime osx-arm64  # another architecture of the same OS
```

Output goes to `output/`, always as Release. Native AOT can't cross-compile between operating systems. Set the `VERSION` env var to stamp the assembly version. Linux publishing needs clang or gcc, and zlib headers.

Tests are NUnit with Moq and run on any OS, without a display:

```sh
dotnet test ImageViewer.Tests
dotnet test ImageViewer.Tests --filter "FullyQualifiedName~ImageStoreTests"
```

`ViewerWindowTests` drives the real window through Avalonia's headless platform. Use it for UI behaviour rather than sending input to a desktop session.

To measure the pipeline, publish and then run:

```sh
output/ImageViewer --bench <folder with at least 8 images>
```

## Release process

Every push to `master` triggers `.github/workflows/build.yml`, which tags a new version, then tests and builds on Windows, Linux and macOS, and publishes a GitHub release with the NSIS installer, a Linux tarball and macOS zips. The version bump comes from the commit message: `#patch`, `#minor`, `#major`, or `#none` to skip it.

## Architecture

**Startup.** `Program.Main` is the composition root. It starts decoding the opened file before Avalonia starts, because bringing up the UI takes longer than a scaled decode. The window lists the folder in the background, so large folders don't delay the first image. Fluent is only loaded when a dialog first opens (`App.EnsureDialogTheme`): the main window draws everything itself and needs no theme.

**Image pipeline (`Pipeline/`).** `ImagePipeline` decides what gets decoded. Each image has up to three tiers (`ImageTier`):

- `Instant`: the cheapest decode the format offers, e.g. JPEG at 1/8 through DCT scaling.
- `Screen`: at least the size of the largest monitor, so resizing doesn't invalidate it.
- `Full`: only when the view zooms past `Screen`, or up front if the default zoom is actual size.

`Navigator` calls `SetWindow` with the current file and the files to prefetch, most urgent first. That replaces the whole pending list, so a held arrow key can't build a backlog; only running decodes finish. In rapid navigation, `Instant` jobs go first.

`ImageStore` holds images to a byte budget and evicts by distance from the current image, not by last use.

`ImageHandle` is a ref-counted lease on the pixels. The render thread can still be drawing after eviction, so never dispose an image directly: use `TryAcquire` and `Release`.

**Decoders (`Decoding/`).** `IImageDecoder` implementations go in the `DecoderChain` built in `Program.Main`, and their extensions become the formats the app lists. `SkiaImageDecoder` is the only one so far. SkiaSharp's libjpeg-turbo has no SIMD, and Magick.NET isn't faster once pixels are in BGRA, so a libjpeg-turbo binding is the known way to get faster JPEG decodes; Magick.NET would add formats, not speed.

**Rendering (`Viewer/`).** `ImageSurface` holds zoom, pan and rotation, and picks the best image available. `ImageDrawOperation` draws on the render thread through Skia. Images are drawn as GPU textures held by `TextureCache`, because a large image drawn from CPU memory costs over 100ms per frame. `ImageSurface` uploads the next image while idle. EXIF orientation and rotation are transforms at draw time (`ViewGeometry`), never pixel work. Geometry is in physical pixels, so actual size is correct at any display scaling. `MessageOverlay` is a separate control so that fading messages don't redraw the image.

**Keyboard actions.** `Settings.KeyBindings` maps key strings (combined with `|`, e.g. `Shift|R`) to a `KeyBinding`, which either names an action or holds a custom shell `Command` (`{0}` is replaced by the current file; `IsDeleteAction` removes it from the list afterwards). `KeyParser` reads them; the names began as WinForms `Keys` values, and existing settings files must keep working. `ViewerWindow.OnKeyDown` dispatches them.

To add an action, update all of:

1. `_defaultActions` in `ViewerWindow` (the action name to method map).
2. `_actionDescriptions` in `ViewerWindow` (the help overlay).
3. The default bindings in the `Settings` constructor.

Saved settings replace the default bindings entirely, so users with an existing settings file don't get new defaults. `GetHelpText` leaves actions with no binding out of the help overlay.

**Platform services (`Platform/`).** `ITrash` and `IClickThrough` have one implementation per OS, chosen by the `Platform` factory. Linux click-through goes through X11, and Linux trash through `gio` or `kioclient`. `ITrash` must never fall back to deleting permanently.

**Settings and state.** Settings are JSON, in `%userprofile%\codemade.imageviewer_settings.json` on Windows, `$XDG_CONFIG_HOME/imageviewer/settings.json` on Linux, and `~/Library/Application Support/ImageViewer/settings.json` on macOS. `ITempData` (`FileBasedTempData`) stores small transient values, such as the last delete confirmation time, in per-user app data.

**Version checking.** After startup, `App` runs `VersionChecker` in the background, at most once a day, using `IVersionGetter` (GitHub releases) and `IUpdateNotifier`. `App` implements the notifier: the message appears in the overlay, and Enter opens the release page while it's showing.

## Conventions

- Dependencies are constructor-injected through interfaces so logic can be tested with Moq; `Program.Main` is the composition root.
- Everything must work under Native AOT: no reflection-based JSON (add types to `AppJsonContext`) and no XAML loaded at runtime. Windows are built in code.
- `Pipeline/` and `Navigator` must stay free of Avalonia types.
- Nullable reference types are enabled in `ImageViewer`.
- `ImageViewer.Tests/testimages` holds fixture images copied to the test output directory.
