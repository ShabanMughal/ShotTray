# Security Policy

## Supported versions

Only the latest release receives fixes.

| Version | Supported |
|---|---|
| 0.1.x | ✅ |

## Reporting a vulnerability

Please **don't** open a public issue for security problems.

Report it privately through GitHub instead: open the repo's **Security** tab and choose **Report a vulnerability**. Please include:

- what the problem is and what an attacker could do with it
- steps to reproduce it
- your Windows version and the ShotTray version

You can expect a reply within a week. Once the problem is confirmed, a fix will be released as soon as possible, and you'll be credited in the release notes unless you'd rather not be.

## What ShotTray touches

This helps you judge the impact of an issue.

| Area | What it does |
|---|---|
| Network | **None.** ShotTray never connects to the internet. |
| Files | Reads and writes PNGs in `%LOCALAPPDATA%\ShotTray\Screenshots`; deletes `shot_*.png` files there that are older than 7 days. Writes elsewhere only when you drag a screenshot out or use **Save as**. |
| Registry | Writes and removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ShotTray` when you toggle **Start with Windows**. |
| Clipboard | Writes the screenshot, or its path, after a capture or a copy action. Never reads it. |
| Screen | Captures the screen only when you press the hotkey or click **Capture**. |
| Privileges | Runs as a normal user; never asks for admin rights. The installer is per-user too. |
| Installer | Installs to `%LOCALAPPDATA%\Programs\ShotTray`, adds Start menu (and optionally desktop) shortcuts, and, if you tick the options, the `Run` value above and that folder on your user `PATH`. The uninstaller removes all of these. |

## Verifying downloads

Each GitHub release lists the SHA-256 hashes of its installer and zip under **Assets**. To check a download, run this in PowerShell and compare the result with the hash on the release page:

```powershell
Get-FileHash .\ShotTray-Setup-v0.1.0.exe -Algorithm SHA256
Get-FileHash .\ShotTray-v0.1.0.zip -Algorithm SHA256
```

The binaries aren't code-signed yet. If you'd rather not trust a prebuilt exe, build from source with `build.bat`.
