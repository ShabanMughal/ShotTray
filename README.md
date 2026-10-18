# ShotTray

**macOS-style area screenshots for Windows.** Press a hotkey, drag a box, and a floating thumbnail shows up in the corner. Drag it into any folder, app or browser, or leave it alone and it's saved for you.

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Size](https://img.shields.io/badge/size-~30%20KB-lightgrey)

---

## Features

- **Ctrl+Shift+4 area capture**: the screen freezes and dims, you drag a box, and a live size badge shows the dimensions. Works across multiple monitors and mixed DPI scaling.
- **Floating thumbnail**: slides in at the bottom-right and stays for 6 seconds. It stays while you hover over it.
- **Markup**: click the thumbnail to draw on it with a pen, highlighter, arrows, rectangles, ellipses and text.
- **Drag & drop anywhere**: Desktop, Explorer folders, browsers, Discord, Slack, VS Code and more.
- **Always saved**: if you don't drag or save it, the screenshot stays in a temp folder anyway.
- **Copied to the clipboard automatically**: `Ctrl+V` pastes the image in apps and the file in Explorer.
- **CLI friendly**: `shot` prints the latest screenshot path, so you can hand it to terminal tools and AI coding assistants.
- **Auto clean-up**: screenshots older than 7 days are deleted.
- **Tiny and dependency-free**: two small `.exe` files, no installer or runtime download needed.

## Installation

### Option 1: Download

1. Download the latest `ShotTray.zip` from the [Releases](../../releases) page.
2. Extract it anywhere, for example `C:\Tools\ShotTray`.
3. Run `ShotTray.exe`. A blue icon appears in the system tray.
4. *(Optional)* Right-click the tray icon and choose **Start with Windows**.
5. *(Optional)* Add the folder to your `PATH` to use the `shot` command in any terminal.

> **Windows SmartScreen warning?** The app isn't code-signed yet. Click **More info → Run anyway**.

### Option 2: Build from source

No SDK needed. The build uses the C# compiler that ships with Windows (.NET Framework 4.x).

```bat
git clone https://github.com/ShabanMughal/ShotTray.git
cd ShotTray
build.bat
```

The output goes to `bin\ShotTray.exe` and `bin\shot.exe`.

## Usage

### Taking a screenshot

| Action | How |
|---|---|
| Capture an area | `Ctrl + Shift + 4`, then drag |
| Cancel | `Esc` or right-click |
| Capture from the tray | Left-click the tray icon |

### The thumbnail

| Action | Result |
|---|---|
| **Drag** it to a folder / Desktop / app | Copies the PNG there |
| **Click** | Opens the markup editor |
| **Hover** | Keeps it on screen |
| **Right-click** | Edit · Open · Copy image · Copy path · Save as… · Show in folder · Delete · Close |
| Do nothing | Fades out after 6 s; the file stays in the screenshots folder |

### Markup

Click the thumbnail to open the editor. **Done** saves your drawing into the screenshot, copies it to the clipboard and shows the updated thumbnail.

| Tool | Key | Tip |
|---|---|---|
| Pen | `P` | Freehand drawing |
| Highlighter | `H` | See-through marker |
| Arrow | `A` | Drag from tail to head |
| Rectangle | `R` | Hold `Shift` for a square |
| Ellipse | `E` | Hold `Shift` for a circle |
| Text | `T` | Click to type, `Enter` to finish, `Shift+Enter` for a new line |

| Shortcut | Action |
|---|---|
| `1` `2` `3` | Small / medium / large |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+C` | Copy the marked-up image |
| `Ctrl+S` or `Enter` | Done (save + copy) |
| `Esc` | Close (asks before discarding) |

### Tray menu

- **Capture area**
- **Open screenshots folder**
- **Copy last screenshot path**
- **Start with Windows** (toggle)
- **Exit**

## CLI: `shot`

```text
shot            print path of the latest screenshot
shot list [n]   list the n most recent screenshots (default 10)
shot dir        print the screenshots folder
shot open       open the screenshots folder in Explorer
```

### Examples

```powershell
# Show the latest screenshot path
shot

# Open the latest screenshot
start (shot)

# Copy the latest screenshot somewhere
cp (shot) .\docs\images\
```

```bash
# Git Bash / WSL-style shells
cp "$(shot)" ./docs/images/
```

### Sharing with AI coding tools (Claude Code, etc.)

- Paste the image directly: the screenshot is already on the clipboard. In Claude Code on Windows, press **Alt+V**.
- Or paste the path: right-click the thumbnail → **Copy path**, or run `shot`, then paste the path into your prompt.

## Where are screenshots stored?

```
%LOCALAPPDATA%\ShotTray\Screenshots\
```

Files are named like `shot_2026-10-18_14-32-05.png` (no spaces, so they're easy to use in a terminal).
Files older than **7 days** are deleted automatically when the app starts and every 6 hours after that.
Anything you drag or "Save as" somewhere else is a copy and is **never** auto-deleted.

## Uninstall

1. Right-click the tray icon, untick **Start with Windows**, then click **Exit**.
2. Delete the app folder.
3. *(Optional)* Delete `%LOCALAPPDATA%\ShotTray`.

## Project structure

```
src/
  Program.cs           entry point, single-instance guard
  TrayApp.cs           tray icon, menu, hotkey, capture flow
  SelectionOverlay.cs  full-screen area selector
  ThumbnailForm.cs     floating preview with drag & drop
  DragGhost.cs         thumbnail picture that follows the cursor while dragging
  EditorForm.cs        markup window and toolbar
  Shapes.cs            pen, highlighter, arrow, box, ellipse, text
  Clip.cs              clipboard helpers
  Native.cs            Win32 interop (hotkey, DPI)
  Store.cs             screenshot folder, naming, clean-up
  Cli.cs               the `shot` command
build.bat              builds bin\ShotTray.exe and bin\shot.exe
```

## Roadmap

- [ ] Full-screen and single-window capture
- [ ] Capture timer (3 s / 5 s / 10 s)
- [x] Markup: pen, highlighter, arrows, boxes, ellipses, text
- [ ] Blur / pixelate tool for hiding sensitive info
- [ ] Crop in the editor
- [ ] Settings: hotkey, thumbnail duration, retention days, save folder
- [ ] Installer and `winget` package

## Contributing

Issues and pull requests are welcome. Please keep the project dependency-free and buildable with `build.bat`.

1. Fork the repo
2. Create a branch: `git checkout -b feature/my-feature`
3. Commit your changes and open a pull request

## License

[MIT](LICENSE)
