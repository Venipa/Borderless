# <img src="Borderless.App/Resources/Iconx48.png" width="32" height="32" alt="" /> Borderless

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)](https://github.com/Venipa/Borderless/releases)
[![Latest release](https://img.shields.io/github/v/release/Venipa/Borderless?label=latest&logo=github)](https://github.com/Venipa/Borderless/releases/latest)
[![License](https://img.shields.io/github/license/Venipa/Borderless)](LICENSE)

Windows desktop app that keeps games and other windows in borderless (and related) layouts. Match by window title and/or executable, then re-apply styles while Borderless is running — useful when a title already runs borderless, fights chrome, or needs extra input/audio rules.

<img width="1018" height="673" alt="Borderless_fVfFZMhH9T" src="https://github.com/user-attachments/assets/c993d5ec-d224-494e-89eb-507ef445d2ff" />


## Features

### Matching
- Match by exact window title and/or executable name
- Optional title **regex**
- Match condition: **Both** (all filled fields), **And** (title + exe required), or **Or**
- Live process picker when adding/editing a rule
- Per-rule enable/disable and live status (idle / active / error)

### Window & video
- Force **borderless** chrome (re-asserted if the app restores borders)
- **Always on top**
- **Expand / max size** to the current monitor
- **Custom position and size**

### Input & audio
- **Lock cursor** to the focused window (Alt+Tab releases)
- **Hide cursor** while focused (Alt+Tab restores)
- **Remove game menus** (window menu bar) while the rule is active
- **Mute** process audio when the window is in the background

### App
- Fluent UI ([WPF-UI](https://github.com/lepoco/wpfui)), system tray, start with Windows, close to tray
- **Defaults** page for new-rule presets
- Optional **GitHub Releases** updater: dialog and/or sidebar hint, download now or install after exit
- Portable zip, bundled zip, and installer builds
- UI languages: English, German, Spanish, French, Italian, Portuguese, Japanese, Korean, Polish, Ukrainian, Simplified Chinese, Traditional Chinese

## Docs

User docs: https://venipa.github.io/Borderless/

Source lives in [`docs/`](docs/) (Fumadocs + static GitHub Pages).

## Install

1. Install [.NET 9 Desktop Runtime (x64)](https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe). The plain ".NET Runtime" package is not enough (WPF).
2. Download the setup from [Releases](https://github.com/Venipa/Borderless/releases/latest).

| Asset | Notes |
| --- | --- |
| `*-win-x64-setup.exe` | Installer (needs Desktop Runtime) |
| `*-win-x64.zip` | Portable, needs Desktop Runtime |
| `*-win-x64-bundled.zip` | Portable, self-contained |

Run as admin so other windows can be restyled.

## Usage

1. Start Borderless
2. Add a rule (pick a process or enter title / exe; optional regex and match condition)
3. Set options (borderless, expand, input, mute, etc.)
4. Leave it running; matching windows are re-applied about once per second

Rules and settings are stored in `%LocalAppData%\Borderless\`.

## Build

```bash
dotnet build Borderless.App/Borderless.App.csproj -c Release
dotnet run --project Borderless.App/Borderless.App.csproj -c Release
```

Publish (framework-dependent):

```bash
dotnet publish Borderless.App/Borderless.App.csproj -c Release -r win-x64 --self-contained false -o publish
```

Inno Setup:

```bash
ISCC.exe /DMyAppVersion=1.0.0.0 /DMyAppSourceDir=publish installer\Borderless.iss
```

Release tags: `vMAJOR.MINOR.PATCH.BUILD` (e.g. `v1.0.0.3`). CI builds them in GitHub Actions.

## Stack

- C# / .NET 9 WPF + Windows Forms (tray)
- [WPF-UI](https://github.com/lepoco/wpfui), CommunityToolkit.Mvvm
- Inno Setup

## License

[GPL-3.0](LICENSE) - Venipa
