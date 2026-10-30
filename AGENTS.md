# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Fast, private Windows image viewer: a WinForms app targeting .NET Framework (`net472`, x64). Image decoding uses FreeImage (native `FreeImage.dll`) and Windows Shell thumbnails, so the app and its tests only run on Windows. The solution can be compiled on Linux, which is how CI builds it.

## Commands

The build is a NUKE project in `build/`. It pins the .NET SDK to 6.0.104 through `global.json`.

```sh
./build.sh                # Linux/macOS: restore + compile (default target)
./build.ps1               # Windows
./build.sh --target Clean # empties output/
```

Output goes to `output/`, always as Release for `win-x64`. The `Compile` target only builds the `ImageViewer` project, not the tests, and also copies `FreeImage.dll` into `output/`. Set the `VERSION` env var to stamp the assembly version.

Tests are NUnit with Moq and are not run by the NUKE build or CI, so run them manually on Windows:

```sh
dotnet test ImageViewer.Tests
dotnet test ImageViewer.Tests --filter "FullyQualifiedName~ImageCacheTests"
```

## Release process

Every push to `master` triggers `.github/workflows/build.yml`, which tags a new version, builds, packages the NSIS installer (`Setup/ImageViewer_setup.nsi`) and publishes a GitHub release. The version bump comes from the commit message: `#patch`, `#minor`, `#major`, or `#none` to skip it.

## Architecture

**Image loading pipeline.** `frmImageViewer` holds the file list for the current directory and calls `ImageBox.LoadImage`. `ImageBox` owns a `TwoStepImageCache`, which starts two loaders concurrently:

- `QuickImageLoader`: Windows Shell thumbnail, low resolution.
- `ImageLoader`: full-resolution decode through FreeImage.

Whichever finishes first is pushed to the `IReceiveImage` receiver (`ImageBox`). If the thumbnail won, the full image replaces it once ready, after a minimum delay of 180ms. Results are dropped if the user has navigated away (`_lastImageToLoad`). `TwoStepImageCache` wraps an `ImageCache` (bounded LRU-style cache that disposes evicted `ImageMeta`) and passes itself in as that cache's `IImageLoader`.

**Rendering.** `ImageBox` is a custom-drawn `Control` that implements `IRenderControl`. It delegates drawing to `Rendering/ImageRenderer` and `Rendering/MessageRenderer` (on-screen overlay messages).

**Keyboard actions.** `Settings.KeyBindings` maps key strings (`Keys` enum names, combined with `|`, e.g. `Shift|R`) to a `KeyBinding`, which either names an action or holds a custom shell `Command` (`{0}` is replaced by the current file; `IsDeleteAction` removes it from the list afterwards). `frmImageViewer.ProcessCmdKey` dispatches these.

To add an action, update all of:

1. `_defaultActions` in `frmImageViewer` (the action name to method map).
2. `_actionDescriptions` in `frmImageViewer` (the help overlay).
3. The default bindings in the `Settings` constructor.

Saved settings replace the default bindings entirely, so users with an existing settings file don't get new defaults. `GetHelpText` leaves actions with no binding out of the help overlay.

**Settings and state.** Settings are JSON in `%userprofile%\codemade.imageviewer_settings.json`, loaded and re-saved at startup by `SettingsJsonStorage`. `ITempData` (`FileBasedTempData`) stores small transient values, such as the last delete confirmation time.

**Version checking.** `VersionChecker` runs in the background at startup, using `IVersionGetter` (GitHub releases) and `IWindowsNotification` (toast), at most once a day. The concrete implementations live in the same files as their interfaces under `VersionChecking/`.

## Conventions

- Dependencies are constructor-injected through interfaces so logic can be tested with Moq; `Program.Main` is the composition root.
- `ImageViewer` builds with nullable reference types enabled.
- `ImageViewer.Tests/testimages` holds fixture images copied to the test output directory.
