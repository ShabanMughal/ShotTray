# ShotTray

**macOS-style area screenshots for Windows.** Press a hotkey, drag a box, and a floating thumbnail shows up in the corner. Drag it into any folder, app or browser, or leave it alone and it's saved for you.

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Size](https://img.shields.io/badge/download-%3C%201%20MB-lightgrey)

---

## Features

- **Ctrl+Shift+4 area capture**: the screen freezes and dims, you drag a box, and a live size badge shows the dimensions. Works across multiple monitors and mixed DPI scaling.
- **Ctrl+Shift+5 toolbar**: like macOS, capture the entire screen, a single window or a selected portion, or **record the screen to MP4**. Options for a 5/10 s timer, showing the mouse pointer and highlighting clicks.
- **Screen recording**: entire screen or a selected portion, H.264 MP4 at 30 fps using the encoder built into Windows. A floating stop button shows the elapsed time and stays out of the video.
- **Floating thumbnail**: slides in at the bottom-right and stays for 6 seconds. It stays while you hover over it.
- **Markup**: click the thumbnail to draw on it with a pen, highlighter, arrows, rectangles, ellipses and text.
- **Drag & drop anywhere**: Desktop, Explorer folders, browsers, Discord, Slack, VS Code and more.
- **Always saved**: if you don't drag or save it, the screenshot stays in a temp folder anyway.
- **Copied to the clipboard automatically**: `Ctrl+V` pastes the image in apps and the file in Explorer.
- **CLI friendly**: `shot` prints the latest screenshot path, so you can hand it to terminal tools and AI coding assistants.
- **Auto clean-up**: screenshots older than 7 days are deleted.
- **Tiny and dependency-free**: two small `.exe` files and no runtime download. Use the installer, or the portable zip.

## Installation

### Option 1: Installer (recommended)

1. Download `ShotTray-Setup-vX.Y.Z.exe` from the [latest release](../../releases/latest).
2. Run it. No admin rights are needed: it installs for your user only, into `%LOCALAPPDATA%\Programs\ShotTray`.
3. Pick your options:
   - **Start ShotTray when I sign in to Windows**
   - **Add the "shot" command to PATH** (open a new terminal afterwards)
   - **Create a desktop shortcut**
4. Read the **How to use ShotTray** page, a quick tour of every feature.
5. Click **Finish** with **Launch ShotTray** ticked. A blue icon appears in the system tray.

To update, run the newer installer; it closes ShotTray and replaces it in place, keeping your screenshots and settings.
If Windows shows a warning the first time, see [First-run warnings](#first-run-warnings).

### Option 2: Portable zip

1. Download `ShotTray-vX.Y.Z.zip` from the [latest release](../../releases/latest).
2. *(Recommended)* Right-click the zip → **Properties** → tick **Unblock** → **OK**. This skips the warning in step 4.
3. Right-click the zip → **Extract All**, and pick a permanent folder, for example `C:\Tools\ShotTray`.
4. Run `ShotTray.exe`. A blue icon appears in the system tray.
5. *(Optional)* Right-click the tray icon and choose **Start with Windows**.
6. *(Optional)* Add the folder to your `PATH` to use the `shot` command in any terminal.

#### First-run warnings

ShotTray isn't code-signed yet, so Windows may warn you the first time. This is normal for new open-source apps.

| You see | What to do |
|---|---|
| Edge: *"isn't commonly downloaded"* | **⋯ → Keep → Show more → Keep anyway** |
| **"Windows protected your PC"** (SmartScreen) | Click **More info**, then **Run anyway**. Windows only asks once. |
| **"Smart App Control blocked an app"** | There's no way to allow a single app. Smart App Control must be turned off in **Windows Security → App & browser control → Smart App Control settings**. ⚠️ On many Windows versions, turning it off can't be undone without resetting Windows. |

You can check that your download is genuine by comparing its SHA-256 hash with the one on the release page; see [SECURITY.md](SECURITY.md#verifying-downloads).

### Option 3: Build from source

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
| Switch between area and window (while selecting) | `Space` |

### Capture and record toolbar

Press `Ctrl + Shift + 5` to open the toolbar at the bottom of the screen.

```text
 ⓧ │ [Screen] [Window] [Portion] │ [Record Screen] [Record Portion] │ Options ▾  [ Capture ]
```

| Button | What it does |
|---|---|
| Capture Entire Screen | Captures the whole monitor (with several monitors, click the one you want) |
| Capture Selected Window | Hover a window to highlight it, click to capture it |
| Capture Selected Portion | Drag a box, like `Ctrl+Shift+4` |
| Record Entire Screen | Records the whole monitor to MP4 |
| Record Selected Portion | Drag a box, then everything inside it is recorded (shown with a red frame) |
| Options | **Timer** (None / 5 s / 10 s), **Show Mouse Pointer**, **Show Mouse Clicks** |
| Capture / Record | Starts the selected mode (`Enter`) |

`←` `→` change the mode, `Esc` or ⓧ closes the toolbar, and you can drag it by its empty space. Your choices are remembered.

**While recording**, a small pill at the top of the screen shows the elapsed time. Stop with any of these:

- Click the pill's stop button
- `Ctrl + Shift + 5`
- Left-click the tray icon, or **Stop recording** in the tray menu

The recording is saved as `rec_….mp4`, copied to the clipboard as a file, and shown as a thumbnail. Click the thumbnail to play it, or drag it into a chat, a browser or a folder.

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

- **Stop recording** (only while recording)
- **Capture area**
- **Capture or record...** (opens the toolbar)
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

Files are named like `shot_2026-10-18_14-32-05.png` and `rec_2026-10-18_14-32-05.mp4` (no spaces, so they're easy to use in a terminal).
Files older than **7 days** are deleted automatically when the app starts and every 6 hours after that.
Anything you drag or "Save as" somewhere else is a copy and is **never** auto-deleted.

## Uninstall

**Installed with the installer:** go to **Settings → Apps → Installed apps → ShotTray → Uninstall** (or use **Uninstall ShotTray** in the Start menu). It closes the app, removes the startup entry and the PATH entry, and asks whether to delete your saved screenshots too.

**Portable zip:**

1. Right-click the tray icon, untick **Start with Windows**, then click **Exit**.
2. Delete the app folder.
3. *(Optional)* Delete `%LOCALAPPDATA%\ShotTray`.

## Project structure

```
src/
  Program.cs           entry point, single-instance guard
  TrayApp.cs           tray icon, menu, hotkey, capture flow
  SelectionOverlay.cs  full-screen area / window / screen picker
  CaptureBar.cs        Ctrl+Shift+5 capture and record toolbar
  Recorder.cs          screen recording to MP4 (Media Foundation)
  RecordingUI.cs       countdown, stop button, frame around the recorded area
  Settings.cs          toolbar choices, saved in the registry
  ThumbnailForm.cs     floating preview with drag & drop
  DragGhost.cs         thumbnail picture that follows the cursor while dragging
  EditorForm.cs        markup window and toolbar
  Shapes.cs            pen, highlighter, arrow, box, ellipse, text
  Clip.cs              clipboard helpers
  Native.cs            Win32 interop (hotkey, DPI)
  Store.cs             screenshot folder, naming, clean-up
  Cli.cs               the `shot` command
  AssemblyInfo.cs      version number and file details
assets/
  icon.svg             icon source
  icon-1024.png        icon render used to build the .ico
  ShotTray.ico         multi-size Windows icon (16-256 px)
tools/
  MakeIcon.cs          turns icon-1024.png into ShotTray.ico
  make-installer-images.ps1  draws the installer's logo artwork
installer/
  ShotTray.iss         Inno Setup script for the installer
  QuickStart.rtf       "How to use ShotTray" page shown at the end of setup
build.bat              builds bin\ShotTray.exe and bin\shot.exe
release.bat            builds the zip and installer: release.bat 0.1.0
```

## Roadmap

- [x] Full-screen and single-window capture
- [x] Capture timer (5 s / 10 s)
- [x] Screen recording (MP4)
- [ ] Audio in recordings (microphone / system sound)
- [x] Markup: pen, highlighter, arrows, boxes, ellipses, text
- [ ] Blur / pixelate tool for hiding sensitive info
- [ ] Crop in the editor
- [ ] Settings: hotkey, thumbnail duration, retention days, save folder
- [x] Installer
- [ ] `winget` package

## Documentation

| Document | What's in it |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | How ShotTray works inside: components, capture flow, DPI, drag & drop, editor |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Building, coding rules (C# 5), test checklist, releasing |
| [CHANGELOG.md](CHANGELOG.md) | What changed in each version |
| [SECURITY.md](SECURITY.md) | Reporting vulnerabilities, what the app touches, verifying downloads |

## Contributing

Issues and pull requests are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first. In short, keep it dependency-free, C# 5 compatible and buildable with `build.bat`.

## License

[MIT](LICENSE)
