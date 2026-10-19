# Contributing to ShotTray

Thanks for your interest! Bug reports, ideas and pull requests are all welcome.

## Ground rules

- **Keep it dependency-free.** No NuGet packages, and no SDK required. The project must keep building with `build.bat` on a plain Windows 10/11 install.
- **Keep it small and focused.** ShotTray does one thing well: quick area screenshots you can drag, mark up and share.
- **Respect privacy.** No network access, telemetry or analytics.

## Getting started

```bat
git clone https://github.com/ShabanMughal/ShotTray.git
cd ShotTray
build.bat
bin\ShotTray.exe
```

Exit ShotTray from the tray icon before you rebuild. Windows locks a running exe, so the build can't overwrite it.

You can use any editor. VS Code or Visual Studio both work; just open the folder and build with `build.bat`.

Read [ARCHITECTURE.md](ARCHITECTURE.md) for a tour of how the pieces fit together.

## Coding rules

### C# 5 only

The compiler that ships with .NET Framework 4.x (`csc.exe` in `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`) only understands **C# 5**. These newer features **won't compile**:

| Not available | Use instead |
|---|---|
| `$"Hello {name}"` | `"Hello " + name` |
| `obj?.Method()` | `if (obj != null) obj.Method();` |
| `nameof(x)` | `"x"` |
| `int X => 5;` | `int X { get { return 5; } }` |
| `public int X { get; } = 5;` | a field, or set it in the constructor |
| `out var x` | declare `x` first |
| `catch (E) when (...)` | `if` inside the `catch` |

### Style

- Match the surrounding code: 4-space indents, braces on new lines, `readonly` fields where possible.
- Keep comments short and explain *why*, not *what*.
- Dispose GDI objects (`Bitmap`, `Pen`, `Brush`, `Font`, `Graphics`) with `using` or in `Dispose(bool)`.
- Name Win32 constants in a comment where they're used, e.g. `0x80 // WS_EX_TOOLWINDOW`.
- When you add a new `.cs` file, add it to the right `csc` line in `build.bat`.

## Testing checklist

There are no automated UI tests yet, so please check by hand the things your change could affect:

**Capture**
- [ ] `Ctrl+Shift+4` → drag → a thumbnail appears
- [ ] `Esc`, right-click and a plain click all cancel
- [ ] Capture works on every monitor, and with different scaling (100%, 150%, …) if you have them

**Thumbnail**
- [ ] It disappears after 6 s; hovering keeps it visible
- [ ] Dragging it to the Desktop or a folder copies the PNG, and the thumbnail follows the cursor
- [ ] A cancelled drag slides back to the corner
- [ ] The right-click menu items all work

**Markup**
- [ ] Every tool draws; Shift makes squares and circles
- [ ] Text: Enter commits, Shift+Enter starts a new line, Esc cancels
- [ ] Undo and redo work
- [ ] Done saves the file and copies the result; the new thumbnail shows the markup
- [ ] Closing with unsaved markup asks first

**CLI**
- [ ] `shot`, `shot list`, `shot dir` and `shot open` work

## Pull requests

1. Fork the repo and create a branch: `git checkout -b feature/blur-tool`.
2. Make your change, and keep each PR to one topic.
3. Run `build.bat` with no errors, then go through the relevant parts of the testing checklist.
4. Update `README.md` and `ARCHITECTURE.md` if behaviour or structure changed.
5. Add a line under **Unreleased** in `CHANGELOG.md`.
6. Open the PR and describe what changed and how you tested it. Screenshots or GIFs help a lot for UI changes.

## Reporting bugs and ideas

Use the [issue templates](../../issues/new/choose). For bugs, please include your Windows version, display scaling and monitor setup, and the steps to reproduce.

## Releasing (maintainers)

1. Update the version in `src/AssemblyInfo.cs` (all three version attributes).
2. In `CHANGELOG.md`, move everything under **Unreleased** into a new version section with today's date.
3. Exit ShotTray, then run `release.bat X.Y.Z`. This creates `dist\ShotTray-vX.Y.Z.zip` and `dist\ShotTray-Setup-vX.Y.Z.exe`. The installer needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`); without it, only the zip is built.
4. Commit and push:
   ```bat
   git add .
   git commit -m "Release vX.Y.Z"
   git push
   ```
5. On GitHub, go to **Releases → Draft a new release**:
   - Tag: `vX.Y.Z`, created on publish
   - Title: `ShotTray vX.Y.Z`
   - Notes: paste that version's section from `CHANGELOG.md`
   - Attach `dist\ShotTray-Setup-vX.Y.Z.exe` and `dist\ShotTray-vX.Y.Z.zip`, and list their SHA-256 hashes
6. Publish.

### Changing the icon

Edit `assets/icon.svg`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
powershell -ExecutionPolicy Bypass -File tools\make-installer-images.ps1
```

This regenerates `assets/icon-1024.png`, `assets/ShotTray.ico` and the installer artwork (`assets/installer-*.bmp`). Rebuild afterwards.

### Changing the installer's guide

The "How to use ShotTray" page shown at the end of setup is `installer/QuickStart.rtf`. Keep it in step with the README when features change.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
