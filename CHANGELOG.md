# Changelog

All notable changes to ShotTray are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Windows installer (`ShotTray-Setup-vX.Y.Z.exe`): per-user install with no admin prompt, Start menu shortcut, optional desktop shortcut, optional "Start with Windows" and `shot` on PATH, in-place upgrades, and an uninstaller in Settings → Apps. It shows the ShotTray logo, lists the features on the Welcome page, and ends with a "How to use ShotTray" guide.

## [0.1.0] - 2026-10-18

First public release.

### Added
- `Ctrl+Shift+4` area capture with a frozen, dimmed screen and a live size badge; works across multiple monitors and mixed DPI.
- Floating thumbnail in the bottom-right corner that stays for 6 seconds and pauses while hovered.
- Drag and drop of the thumbnail into folders, the Desktop, browsers and apps, with the thumbnail following the cursor and gliding back if the drop is cancelled.
- Markup editor: pen, highlighter, arrow, rectangle, ellipse and text; 8 colours, 3 sizes, undo/redo, copy, save as.
- Automatic save of every capture to `%LOCALAPPDATA%\ShotTray\Screenshots`.
- Clipboard copy of every capture (as an image and as a file).
- `shot` CLI: `shot`, `shot list`, `shot dir`, `shot open`.
- Automatic deletion of screenshots older than 7 days.
- Tray menu with "Start with Windows".
- App icon and version details.

[Unreleased]: https://github.com/ShabanMughal/ShotTray/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ShabanMughal/ShotTray/releases/tag/v0.1.0
