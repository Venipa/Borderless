# <img src="Borderless.App/Resources/Iconx48.png" width="32" height="32" alt="" /> Borderless

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white)](https://github.com/Venipa/Borderless/releases)
[![Latest release](https://img.shields.io/github/v/release/Venipa/Borderless?label=latest&logo=github)](https://github.com/Venipa/Borderless/releases/latest)
[![License](https://img.shields.io/github/license/Venipa/Borderless)](LICENSE)

Windows utility that forces apps into borderless windowed mode. Match by window title and/or executable name, then keep the styles applied.
<img width="1024" height="680" alt="image" src="https://github.com/user-attachments/assets/71d03716-1408-4dff-8302-c8078d5f0e9c" />


## Features

- Borderless window chrome, always-on-top, expand to screen, custom size/position
- Mute process audio when the window is in the background
- Rule defaults, search, Fluent UI (WPF-UI)
- System tray, start with Windows, optional updates from GitHub Releases
- English and German UI

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
2. Add a rule (pick a process or enter title / exe)
3. Set options (borderless, expand, mute, etc.)
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
