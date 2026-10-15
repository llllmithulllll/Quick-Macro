# QuickMacro

QuickMacro is a Windows 11-style tray application for launching apps, websites, and workspaces with global keyboard shortcuts. The primary app is the WinUI 3 version. The original WinForms app remains available as a legacy build. Both use the Windows `RegisterHotKey` API and launch only targets added by the user.

**Latest beta:** [v1.0.0-beta.4](https://github.com/llllmithulllll/Quick-Macro/releases/tag/v1.0.0-beta.4) · [Download the self-contained Windows x64 app](https://github.com/llllmithulllll/Quick-Macro/releases/download/v1.0.0-beta.4/QuickMacro-v1.0.0-beta.4-win-x64.zip)

## Requirements

- Windows 10 or 11, x64
- .NET 8 SDK to build from source (end users do not need .NET when using the self-contained publish)
- .NET 8 SDK and Windows App SDK NuGet packages
- Windows SDK build tools (Visual Studio is optional; the WinUI workload provides an integrated development setup)

## Build and run

Build the primary WinUI 3 app directly (x64):

```powershell
dotnet restore .\src\QuickMacro.WinUI\QuickMacro.WinUI.csproj
dotnet build .\src\QuickMacro.WinUI\QuickMacro.WinUI.csproj -c Release -p:Platform=x64
dotnet test .\tests\QuickMacro.Tests\QuickMacro.Tests.csproj -c Release
```

Run the WinUI 3 main app from source with `dotnet run --project .\src\QuickMacro.WinUI\QuickMacro.WinUI.csproj`. To create the self-contained x64 app folder, run `dotnet publish .\src\QuickMacro.WinUI\QuickMacro.WinUI.csproj -c Release -r win-x64 --self-contained true`. Launch `QuickMacro.exe` from the publish folder and keep its companion files beside it. The legacy WinForms app is built as `QuickMacro.Legacy.exe`. Configuration is stored at `%LOCALAPPDATA%\QuickMacro\config.json`.

## Use

The main application is WinUI 3. See [README-WinUI.md](README-WinUI.md) for build instructions and [screenshots](README-WinUI.md#screenshots). Release history is in [CHANGELOG.md](CHANGELOG.md).

1. Select **Add application**, then choose a target type: desktop app or shortcut, Microsoft Store app, website link, or browser-installed app. Website links accept `http://` and `https://` addresses and open in the default browser. Browser-installed apps are discovered from Start Menu and Desktop shortcuts for Brave, Chrome, and Edge, and can be searched by name. Arguments and a working folder are available for desktop targets.
2. Select **Workspaces** to create layouts for apps you use together. Add already-configured apps, pick apps discovered in the Start Menu, Desktop, or Store, or browse to an executable or shortcut. Workspace-only apps are saved inside the workspace and do not need a separate global hotkey. Drag apps into place and resize them with the always-visible dots on their edges and corners; nearby edges snap together. When launched, QuickMacro opens missing apps, arranges their windows, and raises them above other windows. Matching apps that are already open are reused. Website links are not offered because QuickMacro cannot reliably identify which browser window belongs to a particular link.
3. Optionally select **Open this workspace on a new Windows desktop**. Each launch creates and switches to a new virtual desktop before opening the workspace apps.
4. Choose what pressing the workspace shortcut again should do: launch/arrange again, toggle minimize and restore, close the workspace apps, or close the apps and the workspace desktop. The close-desktop action works when that workspace's desktop is currently active.
5. Select **Set shortcut** and press a key chord with Ctrl, Alt, or Shift. To include the Windows key, select its checkbox before recording the remaining key. A shortcut can also be cleared.
6. Choose what the app shortcut does: always launch a new instance, cycle between launch, minimize, and restore/bring-to-front, or launch/request close if the app is already open. For the minimize/restore action, select **Hide app in QuickMacro’s tray when minimized** to remove that app’s window from the taskbar; restore it from QuickMacro’s tray icon or with the same shortcut. QuickMacro restores these hidden windows when it exits.
7. Save. Enabled app and workspace entries register their shortcuts immediately. A Windows conflict appears as a tray warning; change that shortcut in the editor.
8. Closing the window hides it in the tray by default. Use the tray menu to reopen, open Workspaces, toggle all shortcuts, reload the JSON file, change settings, or exit.

The **Settings** window controls sign-in startup, start minimized, tray behavior, global shortcuts, launch notifications, and duplicate launch prevention. Startup uses the current user's Run key and does not require elevation. QuickMacro requests no elevated permissions and does not bypass UAC. Windows may prohibit certain combinations regardless of availability in QuickMacro.

The Store app picker lists installed packaged apps exposed by Windows with an application ID. If an app is not listed, add its Start Menu shortcut if available or use its desktop executable path.

Minimize mode launches the app when closed, minimizes matching windows when any is not minimized, then restores and brings a matching window to the foreground when all are minimized. Close mode sends the normal Windows `WM_CLOSE` request so the app can save work or show its own confirmation. Matching supports executable paths and Store app IDs, including IDs exposed on child windows. If no matching window is found, QuickMacro launches the app. Windows can restrict foreground activation, and apps with custom window ownership or activation behavior may need Windows integration testing. Workspace launch waits for a matching window to settle before restoring and arranging it, then checks whether Windows accepted the requested size and position. Workspace matching uses the app's process path, Store app ID, or browser shortcut identity. Apps that do not expose a recognizable window are reported if they cannot be found after launch. Multi-monitor placement and URL-specific browser-window matching are not currently supported.

Use **Backup / restore** to export or validate and restore a JSON backup. Malformed local configuration is preserved with a `.corrupt-<timestamp>` suffix and the app starts with an empty configuration.

## Distribution

From a Windows machine with the .NET 8 SDK, run `packaging\Publish-Windows.ps1`. It publishes a self-contained x64 build under `artifacts\publish\win-x64`. To create an installer, install Inno Setup 6 and compile `packaging\QuickMacro.iss` against that publish folder. The installer supports clean install, upgrade-in-place, and uninstall.

### GitHub releases

Pushing a version tag such as `v1.0.0-beta.3` starts the Windows release workflow. It restores, builds, and tests the solution, then publishes the WinUI 3 application as a self-contained x64 app folder and attaches a ZIP containing the full folder to the GitHub Release. Tags containing `-beta` are marked as prereleases. See [CHANGELOG.md](CHANGELOG.md) for release notes. GitHub Actions must be enabled for the repository. The package is unsigned; SmartScreen may show a warning.

The installer is unsigned unless a maintainer signs the executable and setup package with an appropriate certificate. Windows SmartScreen may show a warning for unsigned builds. This repository does not include a signing certificate.

## Architecture and behavior

- `ConfigurationRepository`: versioned JSON, validation, atomic replacement, backup, import/export, corrupt-file recovery.
- `GlobalHotkeyService`: `RegisterHotKey` registrations for enabled entries and clean unregister on change/shutdown.
- `LauncherService` and `WindowActionService`: shell execution plus per-shortcut launch, minimize, graceful close, window raising, and virtual desktop support.
- `WorkspaceService`: workspace launch and placement, repeat-shortcut actions, foreground ordering, and optional new virtual desktop creation.
- `StartupService`: current-user Run registration.
- `src/QuickMacro.WinUI`: primary WinUI 3 application and interactive workspace editor with visible resize handles.
- `MainForm`, `EntryDialog`, `SettingsDialog`: legacy tray-first Windows Forms interface.
- `QuickMacro.Tests`: shortcut, entry, action persistence, serialization, and malformed-configuration tests.

Shortcut conflicts can occur with Windows or another application; failed registrations are skipped without terminating QuickMacro. Target existence is checked immediately before launch. Duplicate launch prevention tracks processes started by this QuickMacro session; it does not search for pre-existing processes started elsewhere. Store app activation is delegated to Windows Explorer, so duplicate prevention cannot reliably track a Store app's lifetime. For `.lnk` targets Windows shell execution handles shortcut resolution. Because shell-executed processes can be detached, duplicate prevention is best suited to ordinary executables.

## Windows integration checklist

Run this checklist on Windows 10 and Windows 11 under a standard user account:

1. Add Notepad and launch it with a registered shortcut.
2. Add an installed Microsoft Store app from the Store app picker and launch it with a shortcut.
3. With Notepad maximized, use **Launch or minimize** and confirm the window minimizes; repeat with it restored.
4. Use **Launch or close** and confirm Notepad receives a normal close request.
5. Focus another application and confirm the global shortcut still works.
6. Enable startup, sign out and back in, and confirm QuickMacro starts minimized.
7. Restore QuickMacro from the tray icon and tray menu.
8. Change an entry's shortcut and confirm the new shortcut works without restarting.
9. Register a shortcut already occupied by another app and confirm a tray warning appears while QuickMacro stays open.
10. Move or delete an executable and confirm launch shows an actionable error.
11. Disable and re-enable an entry and confirm its shortcut unregisters and registers.
12. Exit from the tray, confirm QuickMacro is gone, then restart and confirm saved entries load.
13. Repeat with standard user permissions and confirm no administrator prompt is required.

## Release checklist

- [ ] Build Release on Windows 10/11 x64.
- [ ] Run `dotnet test` and the Windows integration checklist above.
- [ ] Publish with `packaging\Publish-Windows.ps1` and test on a machine without .NET installed.
- [ ] Compile and test the Inno Setup installer, upgrade, and uninstall.
- [ ] Review generated config migration/backup behavior and release notes.
- [ ] Sign binaries and installer if a certificate is available; otherwise label the release unsigned.
- [ ] Measure startup time, idle CPU, and memory on the release build before making performance claims.
