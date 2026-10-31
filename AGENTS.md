# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Fast, private image viewer for Windows, Linux and macOS: an Avalonia app on .NET 10, published with Native AOT. **The design goal is the fastest possible browsing of folders of high-resolution pictures**, so judge changes by the time from keypress to sharp image, and measure rather than assume.

## Commands

```sh
dotnet run --project ImageViewer                          # run from source
dotnet publish ImageViewer -c Release -o output           # Native AOT build for this machine
dotnet publish ImageViewer -c Release -r osx-arm64 -o output  # another architecture of the same OS
```

Native AOT can't cross-compile between operating systems. Add `-p:Version=1.2.3` to stamp the version. Linux publishing needs clang or gcc, and zlib headers.

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

`.github/workflows/build.yml` tests and packages on Windows, Linux and macOS for every pull request and push to `master`, and can be started by hand on any branch. To release, push a version tag:

```sh
git tag 2.0.0
git push origin 2.0.0
```

The tag sets the version, and a draft GitHub release is created with the NSIS installer, a Linux tarball and AppImage, and macOS dmgs. It stays invisible to users until it's published on GitHub.

A tag with a suffix, like `2.0.0-rc1`, drafts a prerelease. GitHub's latest release skips those, so update notices ignore them.

Tags have no `v` prefix. `GitHubVersionGetter` parses the tag of the latest release, including in versions already installed, so a prefix would silently stop update notices.

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

**Rendering (`Viewer/`).** `ImageSurface` holds zoom, pan and rotation, and picks the best image available. `ImageDrawOperation` draws on the render thread through Skia. Images are drawn as GPU textures held by `TextureCache`, because a large image drawn from CPU memory costs over 100ms per frame. Textures have no mipmaps, which cuts first-draw time by about half: a shader averages samples when shrinking up to 6x, and mipmaps are only built past that. `ImageSurface` uploads the next image while idle. EXIF orientation and rotation are transforms at draw time (`ViewGeometry`), never pixel work. Geometry is in physical pixels, so actual size is correct at any display scaling. `MessageOverlay` and `HelpOverlay` are separate controls so that they don't redraw the image.

**Keyboard actions.** `Settings.KeyBindings` maps key strings (combined with `|`, e.g. `Shift|R`) to a `KeyBinding`, which either names an action or holds a custom shell `Command` (`{0}` is replaced by the current file; `IsDeleteAction` removes it from the list afterwards). `KeyParser` reads them; the names began as WinForms `Keys` values, and existing settings files must keep working. `ViewerWindow.OnKeyDown` dispatches them.

To add an action, update all of:

1. `_defaultActions` in `ViewerWindow` (the action name to method map).
2. `_helpSections` in `ViewerWindow` (the help overlay).
3. The default bindings in the `Settings` constructor.

Saved settings replace the default bindings entirely, so users with an existing settings file don't get new defaults. `HelpSheet` leaves actions with no binding out of the help overlay, lists custom commands in their own section, and only merges related actions onto one line when their keys read sensibly together.

**Platform services (`Platform/`).** Keep OS-specific code to a minimum, and look for a shared approach first. `ITrash`, `IClickThrough` and `IDefaultViewer` have one implementation per OS because nothing shared exists, chosen by the `Platform` factory. Linux click-through goes through X11, and Linux trash through `gio` or `kioclient`. `ITrash` must never fall back to deleting permanently. `IDefaultViewer` is the settings button: on Linux it installs a desktop entry and runs `xdg-mime`, Windows only lets the user choose in Settings (the installer registers the app there), and macOS shows instructions.

**Transparency.** `Window.Opacity` doesn't reach `ImageDrawOperation`, so `ViewerWindow.ApplyOpacity` sets it and `ImageSurface.ImageOpacity` together. The operation clears what was there, then fades the background and image as one layer. Keys 2 to 6 apply a level immediately, and pressing the same one again makes the window solid. This one path is tested on X11 and XWayland.

**Wayland.** The app runs through XWayland, using Avalonia's stable X11 backend. Its native Wayland backend is still experimental. To test on Wayland from any session, run the app under `kwin_wayland --virtual --xwayland`, which is isolated from the desktop. Its empty desktop is transparent, so screenshots need a bright window underneath to show real see-through, and comparing their RGB alone is misleading.

**Settings and state.** Settings are JSON, in `%userprofile%\codemade.imageviewer_settings.json` on Windows, `$XDG_CONFIG_HOME/imageviewer/settings.json` on Linux, and `~/Library/Application Support/ImageViewer/settings.json` on macOS. `ITempData` (`FileBasedTempData`) stores small transient values, such as the last delete confirmation time, in per-user app data.

**Version checking.** After startup, `App` runs `VersionChecker` in the background, at most once a day, using `IVersionGetter` (GitHub releases) and `IUpdateNotifier`. `App` implements the notifier: the message appears in the overlay, and Enter opens the release page while it's showing.

## Conventions

- Dependencies are constructor-injected through interfaces so logic can be tested with Moq; `Program.Main` is the composition root.
- Everything must work under Native AOT: no reflection-based JSON (add types to `AppJsonContext`) and no XAML loaded at runtime. Windows are built in code.
- `Pipeline/` and `Navigator` must stay free of Avalonia types.
- Nullable reference types are enabled in `ImageViewer`.
- `ImageViewer.Tests/testimages` holds fixture images copied to the test output directory.
