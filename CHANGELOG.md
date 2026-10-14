# Changelog

## v1.0.0-beta.3 — 2026-10-14

### Added

- WinUI 3 is the primary QuickMacro app, with the legacy WinForms build retained as a fallback.
- Added application shortcut and workspace pages with an interactive workspace layout editor.
- Added light, dark, and system appearance options, plus updated app branding.
- Added controls for enabling and disabling global macro shortcuts.
- Added installed-app discovery and browsing for applications while building workspaces.
- Added screenshots of the WinUI 3 app to the project documentation.

### Improved

- Expanded the main window and shortcut/workspace dialogs to make more room for their controls.
- Improved responsive shortcut cards and workspace editor layout.
- Refined dark-theme contrast and the add-shortcut form labels and actions.
- Updated the release workflow to build the WinUI 3 app and package the complete self-contained x64 publish folder in a ZIP.

### Notes

- This is a beta release. Window activation, minimize, close, and workspace positioning can vary by target app and Windows behavior; test your own apps before relying on a layout.
- The Windows x64 package is unsigned. Windows SmartScreen may show a warning.
