using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickMacro.Models;
using QuickMacro.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;
using Windows.System;
using WinRT.Interop;

namespace QuickMacro.WinUI;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly HotkeyWindow _hotkeys;
    private readonly WinForms.NotifyIcon _tray;
    private bool _exiting;
    private bool _firstActivation = true;
    private bool _darkTheme;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
        Navigation.ActualThemeChanged += (_, _) => ApplyThemeSurfaces();
        ExtendsContentIntoTitleBar = false;
        _hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        var iconPath = Path.Combine(AppContext.BaseDirectory, "QuickMacro.ico");
        if (File.Exists(iconPath)) _appWindow.SetIcon(iconPath);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1600, 950));
        _appWindow.Closing += OnAppClosing;
        Closed += (_, _) => { _hotkeys.Dispose(); _tray.Visible = false; _tray.Dispose(); };
        _hotkeys = new HotkeyWindow(_hwnd, OnHotkey);
        _tray = CreateTrayIcon();
        Activated += (_, _) =>
        {
            if (!_firstActivation) return;
            _firstActivation = false;
            if (App.LoadWarning is not null) _ = ShowMessage("QuickMacro configuration", App.LoadWarning);
            if (App.Configuration.Settings.StartMinimized) _appWindow.Hide();
        };
        Navigation.SelectedItem = Navigation.MenuItems[0] as NavigationViewItem;
        RefreshHotkeys();
    }

    private WinForms.NotifyIcon CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open QuickMacro", null, (_, _) => _dispatcher.TryEnqueue(ShowMainWindow));
        menu.Items.Add("Enable/disable shortcuts", null, (_, _) => _dispatcher.TryEnqueue(ToggleShortcuts));
        menu.Items.Add("Exit", null, (_, _) => _dispatcher.TryEnqueue(ExitApplication));
        var icon = new WinForms.NotifyIcon();
        icon.Text = "QuickMacro";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "QuickMacro.ico");
        icon.Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
        icon.ContextMenuStrip = menu;
        icon.Visible = true;
        icon.DoubleClick += (_, _) => _dispatcher.TryEnqueue(ShowMainWindow);
        return icon;
    }

    private void OnAppClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_exiting && App.Configuration.Settings.MinimizeToTray)
        {
            args.Cancel = true;
            sender.Hide();
        }
        else
        {
            _hotkeys.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        }
    }

    private void ShowMainWindow() { _appWindow.Show(); Activate(); }
    private void ExitApplication() { _exiting = true; Close(); }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string page) return;
        switch (page)
        {
            case "shortcuts": ShowShortcuts(); break;
            case "workspaces": ShowWorkspaces(); break;
            case "settings": ShowSettings(); break;
        }
    }

    private void ShowShortcuts()
    {
        PageHost.Children.Clear();
        var page = NewPage("Your shortcuts", "Launch apps, websites, and browser apps with a key press.");
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(new TextBlock { Text = $"{App.Configuration.Entries.Count} shortcuts", VerticalAlignment = VerticalAlignment.Center, Opacity = .68 });
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18, VerticalAlignment = VerticalAlignment.Center };
        var macroKeys = new ToggleSwitch { Header = "Macro keys", IsOn = App.Configuration.Settings.EnableAllShortcuts, OnContent = "On", OffContent = "Off", VerticalAlignment = VerticalAlignment.Center };
        macroKeys.Toggled += (_, _) => { App.Configuration.Settings.EnableAllShortcuts = macroKeys.IsOn; SaveConfiguration(); RefreshHotkeys(); };
        var add = PrimaryButton("＋  Add shortcut"); add.Click += async (_, _) => await EditEntry(null);
        commands.Children.Add(macroKeys); commands.Children.Add(add);
        Grid.SetColumn(commands, 1); toolbar.Children.Add(commands); page.Children.Add(toolbar);
        if (App.Configuration.Entries.Count == 0)
        {
            page.Children.Add(InfoCard("No shortcuts yet", "Add an app or website to get started."));
        }
        else
        {
            var tiles = TileGrid();
            var entries = App.Configuration.Entries.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var card = Card(); card.Padding = new Thickness(16, 14, 16, 13); card.Margin = new Thickness(0);
                var layout = new Grid { RowSpacing = 13 };
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var details = new StackPanel { Spacing = 4 };
                details.Children.Add(new TextBlock { Text = entry.Name, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                var targetTag = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                targetTag.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = TargetBrush(entry.TargetType), VerticalAlignment = VerticalAlignment.Center });
                targetTag.Children.Add(new TextBlock { Text = LauncherTargetTypeLabels.Get(entry.TargetType), Foreground = TargetBrush(entry.TargetType), FontSize = 12 });
                details.Children.Add(targetTag);
                details.Children.Add(new TextBlock { Text = ActionLabel(entry), Opacity = .58, FontSize = 12 });
                layout.Children.Add(details);
                var shortcut = new Border { Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ControlFillColorSecondaryBrush"], CornerRadius = new CornerRadius(7), Padding = new Thickness(11, 7, 11, 7), VerticalAlignment = VerticalAlignment.Top };
                shortcut.Child = new TextBlock { Text = entry.Shortcut?.ToString() ?? "Not set", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                Grid.SetColumn(shortcut, 1); layout.Children.Add(shortcut);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Right };
                var enabled = new ToggleSwitch { OnContent = "Enabled", OffContent = "Disabled", IsOn = entry.Enabled, VerticalAlignment = VerticalAlignment.Center };
                enabled.Toggled += (_, _) => { entry.Enabled = enabled.IsOn; SaveConfiguration(); RefreshHotkeys(); };
                var run = new Button { Content = "Run", Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] }; run.Click += async (_, _) => await RunEntry(entry);
                var edit = new Button { Content = "Edit" }; edit.Click += async (_, _) => await EditEntry(entry);
                var remove = new Button { Content = "Remove" }; remove.Click += async (_, _) => await DeleteEntry(entry);
                buttons.Children.Add(enabled); buttons.Children.Add(run); buttons.Children.Add(edit); buttons.Children.Add(remove);
                Grid.SetRow(buttons, 1); Grid.SetColumnSpan(buttons, 2); layout.Children.Add(buttons);
                card.Child = layout; AddTile(tiles, card, i);
            }
            page.Children.Add(tiles);
        }
        PageHost.Children.Add(PageScroll(page));
    }

    private async Task EditEntry(LauncherEntry? original)
    {
        var name = new TextBox { Text = original?.Name ?? "", PlaceholderText = "For example, Discord", MinWidth = 220 };
        var kind = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        kind.ItemsSource = Enum.GetValues<LauncherTargetType>().Select(value => new Choice<LauncherTargetType>(LauncherTargetTypeLabels.Get(value), value)).ToList();
        kind.SelectedIndex = Math.Max(0, (int)(original?.TargetType ?? LauncherTargetType.FilePath));
        var path = new TextBox { Text = original?.TargetPath ?? "", PlaceholderText = "Select a file or enter a website URL" };
        var args = new TextBox { Text = original?.Arguments ?? "" };
        var working = new TextBox { Text = original?.WorkingDirectory ?? "" };
        var targetHelp = new TextBlock { Opacity = .68, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var targetBrowse = new Button { Content = "Browse for app…", MinWidth = 142 };
        var installedApps = new ComboBox { PlaceholderText = "Select an installed app…", Visibility = Visibility.Collapsed };
        var browseWorking = new Button { Content = "Browse…", MinWidth = 100 };
        browseWorking.Click += async (_, _) =>
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                InitializeWithWindow.Initialize(picker, _hwnd);
                picker.FileTypeFilter.Add("*");
                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null) working.Text = folder.Path;
            }
            catch (Exception ex) { targetHelp.Text = $"Could not browse folders: {ex.Message}"; }
        };
        var shortcut = new TextBox { Text = original?.Shortcut?.ToString() ?? "Not set", IsReadOnly = true, MinWidth = 190 };
        var windowsKey = new CheckBox { Content = "Windows key", IsChecked = original?.Shortcut?.Windows ?? false, VerticalAlignment = VerticalAlignment.Center };
        ShortcutBinding? captured = original?.Shortcut;
        shortcut.KeyDown += (_, e) =>
        {
            e.Handled = true;
            var vk = (int)e.Key;
            var binding = new ShortcutBinding { Key = (System.Windows.Forms.Keys)vk, Control = IsKeyDown(0x11), Alt = IsKeyDown(0x12), Shift = IsKeyDown(0x10), Windows = windowsKey.IsChecked == true || IsKeyDown(0x5B) || IsKeyDown(0x5C) };
            var error = ShortcutRules.Validate(binding);
            if (error is not null) { shortcut.Text = error; captured = original?.Shortcut; return; }
            captured = binding; shortcut.Text = binding.ToString();
        };
        var action = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        action.ItemsSource = Enum.GetValues<ShortcutAction>().Select(value => new Choice<ShortcutAction>(value switch { ShortcutAction.Launch => "Always launch a new instance", ShortcutAction.Minimize => "Launch / minimize / restore", _ => "Launch, or close if open" }, value)).ToList();
        var actionIndex = (int)(original?.ShortcutAction ?? ShortcutAction.Launch);
        action.SelectedIndex = actionIndex >= 0 && actionIndex < action.Items.Count ? actionIndex : 0;
        var minimizeTray = new CheckBox { Content = "Minimize the app to the system tray", IsChecked = original?.MinimizeToSystemTray ?? false };
        var enabled = new CheckBox { Content = "Shortcut enabled", IsChecked = original?.Enabled ?? true };
        var setShortcut = new Button { Content = "Set shortcut", MinWidth = 116 };
        var clearShortcut = new Button { Content = "Clear", MinWidth = 76 };
        setShortcut.Click += (_, _) => { shortcut.Text = "Press a key combination…"; shortcut.Focus(FocusState.Programmatic); };
        clearShortcut.Click += (_, _) => { captured = null; shortcut.Text = "Not set"; };
        var fields = new Grid { MinWidth = 760, MaxWidth = 1180, Margin = new Thickness(8, 4, 8, 8), HorizontalAlignment = HorizontalAlignment.Stretch, RowSpacing = 11, ColumnSpacing = 14 };
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < 12; i++) fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void AddRow(int row, string label, FrameworkElement control, int span = 1)
        {
            var rowLabel = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Opacity = .86 };
            Grid.SetRow(rowLabel, row); fields.Children.Add(rowLabel);
            Grid.SetRow(control, row); Grid.SetColumn(control, 1); Grid.SetColumnSpan(control, span); fields.Children.Add(control);
        }
        var intro = new TextBlock { Text = "Choose what the shortcut should open, then set how the hotkey behaves.", Opacity = .68, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) };
        Grid.SetColumnSpan(intro, 3); fields.Children.Add(intro);
        AddRow(1, "Name", name, 2);
        AddRow(2, "Opens", kind, 2);
        Grid.SetRow(targetHelp, 3); Grid.SetColumn(targetHelp, 1); Grid.SetColumnSpan(targetHelp, 2); fields.Children.Add(targetHelp);
        AddRow(4, "Target", path);
        Grid.SetRow(targetBrowse, 4); Grid.SetColumn(targetBrowse, 2); fields.Children.Add(targetBrowse);
        Grid.SetRow(installedApps, 5); Grid.SetColumn(installedApps, 1); Grid.SetColumnSpan(installedApps, 2); fields.Children.Add(installedApps);
        AddRow(6, "Arguments", args, 2);
        AddRow(7, "Working folder", working);
        Grid.SetRow(browseWorking, 7); Grid.SetColumn(browseWorking, 2); fields.Children.Add(browseWorking);
        var shortcutControls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        shortcutControls.Children.Add(setShortcut); shortcutControls.Children.Add(clearShortcut); shortcutControls.Children.Add(shortcut); shortcutControls.Children.Add(windowsKey);
        AddRow(8, "Global shortcut", shortcutControls, 2);
        AddRow(9, "Hotkey action", action, 2);
        Grid.SetRow(minimizeTray, 10); Grid.SetColumn(minimizeTray, 1); Grid.SetColumnSpan(minimizeTray, 2); fields.Children.Add(minimizeTray);
        Grid.SetRow(enabled, 11); Grid.SetColumn(enabled, 1); Grid.SetColumnSpan(enabled, 2); fields.Children.Add(enabled);
        void UpdateTargetType()
        {
            var targetType = (kind.SelectedItem as Choice<LauncherTargetType>)?.Value ?? LauncherTargetType.FilePath;
            targetHelp.Text = targetType switch
            {
                LauncherTargetType.FilePath => "Choose a Windows program or shortcut file on this PC.",
                LauncherTargetType.StoreApp => "Select an installed Microsoft Store app.",
                LauncherTargetType.WebsiteUrl => "Enter a web address; QuickMacro opens it in your default browser.",
                LauncherTargetType.BrowserAppShortcut => "Select an app shortcut installed by Chrome, Edge, or Brave.",
                _ => string.Empty
            };
            path.PlaceholderText = targetType == LauncherTargetType.WebsiteUrl ? "https://example.com" : targetType == LauncherTargetType.StoreApp ? "Select a Microsoft Store app" : targetType == LauncherTargetType.BrowserAppShortcut ? "Select a browser-installed app" : "Select an .exe or .lnk file";
            targetBrowse.Content = targetType switch { LauncherTargetType.StoreApp => "Choose Store app…", LauncherTargetType.BrowserAppShortcut => "Choose browser app…", LauncherTargetType.WebsiteUrl => "Website URL", _ => "Browse for app…" };
            targetBrowse.IsEnabled = targetType != LauncherTargetType.WebsiteUrl;
            targetBrowse.Visibility = targetType == LauncherTargetType.WebsiteUrl ? Visibility.Collapsed : Visibility.Visible;
            if (targetType == LauncherTargetType.WebsiteUrl) action.SelectedIndex = (int)ShortcutAction.Launch;
            action.IsEnabled = targetType != LauncherTargetType.WebsiteUrl;
            minimizeTray.Visibility = targetType != LauncherTargetType.WebsiteUrl && action.SelectedIndex == (int)ShortcutAction.Minimize ? Visibility.Visible : Visibility.Collapsed;
            installedApps.Visibility = Visibility.Collapsed;
        }
        kind.SelectionChanged += (_, _) => UpdateTargetType();
        action.SelectionChanged += (_, _) => UpdateTargetType();
        targetBrowse.Click += async (_, _) =>
        {
            try
            {
                var targetType = (kind.SelectedItem as Choice<LauncherTargetType>)?.Value ?? LauncherTargetType.FilePath;
                if (targetType == LauncherTargetType.FilePath)
                {
                    var picker = new Windows.Storage.Pickers.FileOpenPicker();
                    InitializeWithWindow.Initialize(picker, _hwnd);
                    foreach (var extension in new[] { ".exe", ".lnk", ".bat", ".cmd" }) picker.FileTypeFilter.Add(extension);
                    var file = await picker.PickSingleFileAsync();
                    if (file is null) return;
                    path.Text = file.Path;
                    if (string.IsNullOrWhiteSpace(name.Text)) name.Text = Path.GetFileNameWithoutExtension(file.Name);
                }
                else
                {
                    // Keep selection in this dialog. Opening a second ContentDialog
                    // while this one is active can crash WinUI.
                    var apps = InstalledAppCatalog.GetInstalledApps().Where(app => app.TargetType == targetType).ToList();
                    installedApps.ItemsSource = apps;
                    installedApps.DisplayMemberPath = nameof(InstalledAppChoice.DisplayName);
                    installedApps.SelectedItem = null;
                    installedApps.Visibility = Visibility.Visible;
                    installedApps.IsDropDownOpen = apps.Count > 0;
                    if (apps.Count == 0) targetHelp.Text = "No matching installed apps were found on this PC.";
                }
            }
            catch (Exception ex) { targetHelp.Text = $"Could not load apps: {ex.Message}"; }
        };
        installedApps.SelectionChanged += (_, _) =>
        {
            if (installedApps.SelectedItem is not InstalledAppChoice choice) return;
            name.Text = choice.Name; path.Text = choice.TargetPath;
            targetHelp.Text = $"Selected: {choice.DisplayName}";
            installedApps.Visibility = Visibility.Collapsed;
        };
        var dialogWidth = Math.Max(600, Math.Min(1240, Content.XamlRoot.Size.Width - 64));
        var dialog = new ContentDialog { Title = original is null ? "Add a shortcut" : "Edit shortcut", Content = new ScrollViewer { Content = fields, MaxHeight = Math.Max(400, Content.XamlRoot.Size.Height - 160), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, PrimaryButtonText = "Save shortcut", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, FullSizeDesired = true, Width = dialogWidth, MaxWidth = dialogWidth, XamlRoot = Content.XamlRoot };
        dialog.RequestedTheme = Navigation.RequestedTheme;
        UpdateTargetType();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var entry = original ?? new LauncherEntry();
        entry.Name = name.Text.Trim(); entry.TargetPath = path.Text.Trim(); entry.TargetType = (kind.SelectedItem as Choice<LauncherTargetType>)?.Value ?? LauncherTargetType.FilePath;
        entry.Arguments = args.Text; entry.WorkingDirectory = working.Text.Trim(); entry.Shortcut = captured;
        entry.ShortcutAction = (action.SelectedItem as Choice<ShortcutAction>)?.Value ?? ShortcutAction.Launch;
        entry.MinimizeToSystemTray = entry.ShortcutAction == ShortcutAction.Minimize && minimizeTray.IsChecked == true;
        entry.Enabled = enabled.IsChecked == true;
        var validation = ShortcutRules.ValidateEntry(entry);
        if (validation is not null) { await ShowMessage("Check the shortcut", validation); return; }
        if (App.Configuration.Entries.Any(other => other.Id != entry.Id && other.Enabled && entry.Enabled && entry.Shortcut is not null && entry.Shortcut.Equals(other.Shortcut))) { await ShowMessage("Shortcut already assigned", "Choose a shortcut not used by another app."); return; }
        if (original is null) App.Configuration.Entries.Add(entry);
        SaveConfiguration(); RefreshHotkeys(); ShowShortcuts();
    }

    private async Task DeleteEntry(LauncherEntry entry)
    {
        var dialog = new ContentDialog { Title = $"Remove {entry.Name}?", Content = "This removes the shortcut from QuickMacro.", PrimaryButtonText = "Remove", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = Content.XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        App.Configuration.Entries.Remove(entry);
        foreach (var workspace in App.Configuration.Workspaces) workspace.Items.RemoveAll(item => item.EntryId == entry.Id);
        App.Configuration.Workspaces.RemoveAll(workspace => workspace.Items.Count == 0);
        SaveConfiguration(); RefreshHotkeys(); ShowShortcuts();
    }

    private void ShowWorkspaces()
    {
        PageHost.Children.Clear();
        var page = NewPage("Workspaces", "Bring your go-to apps together in one shortcut.");
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(new TextBlock { Text = $"{App.Configuration.Workspaces.Count} workspaces", VerticalAlignment = VerticalAlignment.Center, Opacity = .68 });
        var add = PrimaryButton("＋  Create workspace"); add.Click += async (_, _) => await EditWorkspace(null); Grid.SetColumn(add, 1); toolbar.Children.Add(add); page.Children.Add(toolbar);
        if (App.Configuration.Workspaces.Count == 0) page.Children.Add(InfoCard("No workspaces yet", "Create a workspace from your configured app shortcuts."));
        else
        {
            var tiles = TileGrid();
            var workspaces = App.Configuration.Workspaces.ToArray();
            for (var i = 0; i < workspaces.Length; i++)
            {
                var workspace = workspaces[i];
                var card = Card(); card.Padding = new Thickness(16, 14, 16, 13); card.Margin = new Thickness(0);
                var layout = new Grid { RowSpacing = 13 };
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel { Spacing = 4 };
                text.Children.Add(new TextBlock { Text = workspace.Name, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                text.Children.Add(new TextBlock { Text = $"{workspace.Items.Count} apps", Opacity = .68, FontSize = 12 });
                layout.Children.Add(text);
                var key = new Border { Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ControlFillColorSecondaryBrush"], CornerRadius = new CornerRadius(7), Padding = new Thickness(11, 7, 11, 7), VerticalAlignment = VerticalAlignment.Top };
                key.Child = new TextBlock { Text = workspace.Shortcut?.ToString() ?? "Not set", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                Grid.SetColumn(key, 1); layout.Children.Add(key);
                var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Right };
                var launch = new Button { Content = "Launch", Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"] }; launch.Click += async (_, _) => await RunWorkspace(workspace);
                var edit = new Button { Content = "Edit" }; edit.Click += async (_, _) => await EditWorkspace(workspace);
                var remove = new Button { Content = "Remove" }; remove.Click += async (_, _) => await DeleteWorkspace(workspace);
                commands.Children.Add(launch); commands.Children.Add(edit); commands.Children.Add(remove); Grid.SetRow(commands, 1); Grid.SetColumnSpan(commands, 2); layout.Children.Add(commands);
                card.Child = layout; AddTile(tiles, card, i);
            }
            page.Children.Add(tiles);
        }
        PageHost.Children.Add(PageScroll(page));
    }

    private async Task EditWorkspace(Workspace? original)
    {
        var name = new TextBox { Header = "Workspace name", Text = original?.Name ?? "" };
        var openOnNewDesktop = new CheckBox { Content = "Open this workspace on a new Windows desktop", IsChecked = original?.OpenOnNewDesktop ?? false };
        var repeatAction = new ComboBox { Header = "When shortcut is pressed again", Width = 320, SelectedIndex = (int)(original?.RepeatAction ?? WorkspaceRepeatAction.Launch) };
        repeatAction.Items.Add("Launch / arrange again");
        repeatAction.Items.Add("Minimize / restore apps");
        repeatAction.Items.Add("Close workspace apps");
        repeatAction.Items.Add("Close apps and this desktop");
        void UpdateRepeatActionAvailability() => repeatAction.IsEnabled = openOnNewDesktop.IsChecked == true || repeatAction.SelectedIndex != (int)WorkspaceRepeatAction.CloseAppsAndDesktop;
        openOnNewDesktop.Checked += (_, _) => UpdateRepeatActionAvailability();
        openOnNewDesktop.Unchecked += (_, _) => UpdateRepeatActionAvailability();
        repeatAction.SelectionChanged += (_, _) => UpdateRepeatActionAvailability();
        UpdateRepeatActionAvailability();
        var shortcut = new TextBox { Header = "Global shortcut", Text = original?.Shortcut?.ToString() ?? "", IsReadOnly = true, PlaceholderText = "Click here, then press a shortcut" };
        ShortcutBinding? binding = original?.Shortcut;
        shortcut.KeyDown += (_, e) =>
        {
            e.Handled = true;
            var value = new ShortcutBinding { Key = (System.Windows.Forms.Keys)(int)e.Key, Control = IsKeyDown(0x11), Alt = IsKeyDown(0x12), Shift = IsKeyDown(0x10), Windows = IsKeyDown(0x5B) || IsKeyDown(0x5C) };
            if (ShortcutRules.Validate(value) is null) { binding = value; shortcut.Text = value.ToString(); }
        };
        var appChecks = new StackPanel { Spacing = 6 };
        var candidates = new List<(string Name, Guid EntryId, LauncherEntry? Target)>();
        foreach (var entry in App.Configuration.Entries.Where(entry => entry.TargetType != LauncherTargetType.WebsiteUrl))
            candidates.Add((entry.Name, entry.Id, null));
        foreach (var item in original?.Items.Where(item => item.EntryId == Guid.Empty && item.Target is not null) ?? [])
            if (!candidates.Any(candidate => candidate.Target?.TargetPath.Equals(item.Target!.TargetPath, StringComparison.OrdinalIgnoreCase) == true))
                candidates.Add((item.Target!.Name, Guid.Empty, CopyWorkspaceTarget(item.Target)));
        try
        {
            foreach (var choice in InstalledAppCatalog.GetInstalledApps().Where(choice => choice.TargetType != LauncherTargetType.WebsiteUrl))
            {
                var configured = App.Configuration.Entries.Any(entry => entry.TargetType == choice.TargetType && string.Equals(entry.TargetPath, choice.TargetPath, StringComparison.OrdinalIgnoreCase));
                var alreadyInWorkspace = original?.Items.Any(item => item.Target is not null && item.Target.TargetType == choice.TargetType && string.Equals(item.Target.TargetPath, choice.TargetPath, StringComparison.OrdinalIgnoreCase)) == true;
                if (configured || alreadyInWorkspace) continue;
                candidates.Add((choice.Name, Guid.Empty, new LauncherEntry { Name = choice.Name, TargetPath = choice.TargetPath, TargetType = choice.TargetType, Arguments = choice.Arguments, WorkingDirectory = choice.WorkingDirectory }));
            }
        }
        catch { /* Keep configured shortcuts available if the installed app catalog is unavailable. */ }
        candidates = candidates.OrderBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var placementItems = new List<WorkspaceItem>();
        foreach (var candidate in candidates)
        {
            var existing = original?.Items.FirstOrDefault(item => candidate.EntryId != Guid.Empty ? item.EntryId == candidate.EntryId : item.EntryId == Guid.Empty && (item.Target?.TargetPath.Equals(candidate.Target?.TargetPath, StringComparison.OrdinalIgnoreCase) ?? false));
            var item = new WorkspaceItem { EntryId = candidate.EntryId, Target = candidate.EntryId == Guid.Empty ? CopyWorkspaceTarget(candidate.Target) : null, X = existing?.X ?? 0, Y = existing?.Y ?? 0, Width = existing?.Width ?? 100, Height = existing?.Height ?? 100 };
            if (existing is not null) placementItems.Add(item);
            appChecks.Children.Add(new CheckBox { Content = candidate.Name, Tag = item, IsChecked = existing is not null });
        }
        var preview = new WorkspaceLayoutEditor(placementItems, item => item.EntryId != Guid.Empty ? candidates.FirstOrDefault(candidate => candidate.EntryId == item.EntryId).Name ?? "App" : item.Target?.Name ?? "App");
        var selectedCount = new TextBlock { Opacity = .7, VerticalAlignment = VerticalAlignment.Center };
        void UpdateSelectedCount() => selectedCount.Text = $"{appChecks.Children.OfType<CheckBox>().Count(box => box.IsChecked == true)} selected";
        void WireAppChoice(CheckBox box)
        {
            box.Checked += (_, _) => { if (box.Tag is WorkspaceItem item && !placementItems.Contains(item)) { placementItems.Add(item); ArrangeWorkspaceItems(placementItems); preview.RefreshItems(); } UpdateSelectedCount(); };
            box.Unchecked += (_, _) => { if (box.Tag is WorkspaceItem item) { placementItems.Remove(item); ArrangeWorkspaceItems(placementItems); preview.RefreshItems(); } UpdateSelectedCount(); };
        }
        foreach (var box in appChecks.Children.OfType<CheckBox>())
            WireAppChoice(box);
        UpdateSelectedCount();
        var search = new TextBox { PlaceholderText = "Search apps", Margin = new Thickness(0, 8, 0, 4) };
        search.TextChanged += (_, _) =>
        {
            foreach (var box in appChecks.Children.OfType<CheckBox>())
                box.Visibility = box.Content?.ToString()?.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase) != false ? Visibility.Visible : Visibility.Collapsed;
        };
        var selectionActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var selectAll = new Button { Content = "Select visible" };
        selectAll.Click += (_, _) => { foreach (var box in appChecks.Children.OfType<CheckBox>().Where(box => box.Visibility == Visibility.Visible)) box.IsChecked = true; };
        var clearSelection = new Button { Content = "Clear" };
        clearSelection.Click += (_, _) => { foreach (var box in appChecks.Children.OfType<CheckBox>()) box.IsChecked = false; };
        selectionActions.Children.Add(selectAll);
        selectionActions.Children.Add(clearSelection);
        var browse = new Button { Content = "Browse for an app…", HorizontalAlignment = HorizontalAlignment.Stretch };
        browse.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            InitializeWithWindow.Initialize(picker, _hwnd);
            foreach (var extension in new[] { ".exe", ".lnk", ".bat", ".cmd" }) picker.FileTypeFilter.Add(extension);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var existingChoice = appChecks.Children.OfType<CheckBox>().FirstOrDefault(box => box.Tag is WorkspaceItem item && (item.Target?.TargetPath.Equals(file.Path, StringComparison.OrdinalIgnoreCase) == true || item.EntryId != Guid.Empty && App.Configuration.Entries.FirstOrDefault(entry => entry.Id == item.EntryId)?.TargetPath.Equals(file.Path, StringComparison.OrdinalIgnoreCase) == true));
            if (existingChoice is not null) { existingChoice.IsChecked = true; existingChoice.Visibility = Visibility.Visible; return; }
            var target = new LauncherEntry { Name = Path.GetFileNameWithoutExtension(file.Name), TargetPath = file.Path, TargetType = LauncherTargetType.FilePath };
            var workspaceItem = new WorkspaceItem { Target = target, X = 0, Y = 0, Width = 100, Height = 100 };
            candidates.Add((target.Name, Guid.Empty, target));
            var box = new CheckBox { Content = target.Name, Tag = workspaceItem };
            WireAppChoice(box);
            appChecks.Children.Add(box);
            box.IsChecked = true;
            UpdateSelectedCount();
        };

        var narrowDialog = Content.XamlRoot.Size.Width < 1120;
        FrameworkElement identityFields;
        if (narrowDialog)
        {
            var identityStack = new StackPanel { Spacing = 6 };
            identityStack.Children.Add(name);
            identityStack.Children.Add(shortcut);
            identityFields = identityStack;
        }
        else
        {
            var identity = new Grid { ColumnSpacing = 18, Margin = new Thickness(0, 0, 0, 12) };
            identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            identity.Children.Add(name); Grid.SetColumn(shortcut, 1); identity.Children.Add(shortcut);
            identityFields = identity;
        }

        var appHeading = new Grid { ColumnSpacing = 8 };
        appHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        appHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appHeading.Children.Add(new TextBlock { Text = "Apps", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(selectedCount, 1); appHeading.Children.Add(selectedCount);
        var appPanel = new StackPanel { Spacing = 7 };
        appPanel.Children.Add(appHeading);
        appPanel.Children.Add(new TextBlock { Text = "Choose configured or installed apps for this workspace.", Opacity = .7, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        appPanel.Children.Add(search);
        appPanel.Children.Add(selectionActions);
        appPanel.Children.Add(browse);
        appPanel.Children.Add(new ScrollViewer { Content = appChecks, Height = narrowDialog ? 235 : 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var appCard = Card(); appCard.Padding = new Thickness(16); appCard.Margin = new Thickness(0); appCard.Child = appPanel;

        var layoutPanel = new StackPanel { Spacing = 8 };
        layoutPanel.Children.Add(new TextBlock { Text = "Desktop layout", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        layoutPanel.Children.Add(new TextBlock { Text = "Drag a window to move it. Grab any edge or corner to resize; nearby edges snap together.", Opacity = .7, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        preview.Height = narrowDialog ? 320 : Math.Min(620, Math.Max(430, (Content.XamlRoot.Size.Width - 430) * 9 / 16 + 36));
        layoutPanel.Children.Add(preview);
        var layoutCard = Card(); layoutCard.Padding = new Thickness(14); layoutCard.Margin = new Thickness(0); layoutCard.Child = layoutPanel;
        FrameworkElement workspaceEditor;
        if (narrowDialog)
        {
            var editorStack = new StackPanel { Spacing = 12 };
            editorStack.Children.Add(appCard);
            editorStack.Children.Add(layoutCard);
            workspaceEditor = editorStack;
        }
        else
        {
            var editorColumns = new Grid { ColumnSpacing = 16, VerticalAlignment = VerticalAlignment.Stretch };
            editorColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            editorColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editorColumns.Children.Add(appCard); Grid.SetColumn(layoutCard, 1); editorColumns.Children.Add(layoutCard);
            workspaceEditor = editorColumns;
        }
        var fields = new StackPanel { Spacing = 10 };
        fields.Children.Add(identityFields);
        fields.Children.Add(openOnNewDesktop);
        fields.Children.Add(repeatAction);
        fields.Children.Add(workspaceEditor);
        var workspaceDialogWidth = Math.Max(700, Math.Min(1500, Content.XamlRoot.Size.Width - 64));
        var dialog = new ContentDialog { Title = original is null ? "Create workspace" : "Edit workspace", Content = new ScrollViewer { Content = fields, MaxHeight = Math.Max(360, Content.XamlRoot.Size.Height - 130), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, PrimaryButtonText = "Save workspace", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, FullSizeDesired = true, Width = workspaceDialogWidth, MaxWidth = workspaceDialogWidth, XamlRoot = Content.XamlRoot };
        dialog.RequestedTheme = Navigation.RequestedTheme;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var selected = appChecks.Children.OfType<CheckBox>().Where(box => box.IsChecked == true).Select(box => (WorkspaceItem)box.Tag).ToList();
        if (string.IsNullOrWhiteSpace(name.Text) || binding is null || selected.Count == 0) { await ShowMessage("Workspace incomplete", "Enter a name, set a shortcut, and select at least one app."); return; }
        if (App.Configuration.Entries.Any(entry => entry.Enabled && entry.Shortcut?.Equals(binding) == true && entry.Enabled)) { await ShowMessage("Shortcut already assigned", "Choose a shortcut not used by an app."); return; }
        var workspace = original ?? new Workspace(); workspace.Name = name.Text.Trim(); workspace.Shortcut = binding; workspace.Enabled = true; workspace.OpenOnNewDesktop = openOnNewDesktop.IsChecked == true; workspace.RepeatAction = (WorkspaceRepeatAction)Math.Clamp(repeatAction.SelectedIndex, 0, 3);
        workspace.Items = selected.Select(item => new WorkspaceItem { EntryId = item.EntryId, Target = item.Target, X = item.X, Y = item.Y, Width = item.Width, Height = item.Height }).ToList();
        if (original is null) App.Configuration.Workspaces.Add(workspace);
        SaveConfiguration(); RefreshHotkeys(); ShowWorkspaces();
    }

    private async Task DeleteWorkspace(Workspace workspace)
    {
        var dialog = new ContentDialog { Title = $"Remove {workspace.Name}?", PrimaryButtonText = "Remove", CloseButtonText = "Cancel", XamlRoot = Content.XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        App.Configuration.Workspaces.Remove(workspace); SaveConfiguration(); RefreshHotkeys(); ShowWorkspaces();
    }

    private static LauncherEntry? CopyWorkspaceTarget(LauncherEntry? source) => source is null ? null : new LauncherEntry
    {
        Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
        Name = source.Name,
        TargetPath = source.TargetPath,
        Arguments = source.Arguments,
        WorkingDirectory = source.WorkingDirectory,
        TargetType = source.TargetType,
        Shortcut = null,
        Enabled = true
    };

    private static void ArrangeWorkspaceItems(IReadOnlyList<WorkspaceItem> items)
    {
        if (items.Count == 0) return;
        var columns = items.Count == 1 ? 1 : 2;
        var rows = (int)Math.Ceiling(items.Count / (double)columns);
        for (var i = 0; i < items.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var left = column * 100 / columns;
            var top = row * 100 / rows;
            items[i].X = left;
            items[i].Y = top;
            items[i].Width = (column + 1) * 100 / columns - left;
            items[i].Height = (row + 1) * 100 / rows - top;
        }
    }

    private void ShowSettings()
    {
        PageHost.Children.Clear();
        var page = NewPage("Settings", "Choose how QuickMacro looks and works on your PC.");
        var tiles = TileGrid();
        var appearanceCard = Card(); appearanceCard.Padding = new Thickness(20); appearanceCard.Margin = new Thickness(0);
        var options = new StackPanel { Spacing = 14 };
        options.Children.Add(new TextBlock { Text = "Appearance & startup", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        var theme = new ComboBox { Header = "Appearance", Width = 320, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { "System", "Light", "Dark" } };
        theme.SelectedItem = App.Configuration.Settings.ThemeMode;
        theme.SelectionChanged += (_, _) => { App.Configuration.Settings.ThemeMode = theme.SelectedItem?.ToString() ?? "System"; ApplyTheme(); SaveConfiguration(); ShowSettings(); };
        options.Children.Add(theme);
        var startup = new ToggleSwitch { Header = "Launch when I sign in to Windows", IsOn = App.Startup.IsEnabled };
        startup.Toggled += (_, _) => { try { App.Startup.SetEnabled(startup.IsOn); App.Configuration.Settings.StartWithWindows = startup.IsOn; SaveConfiguration(); } catch (Exception ex) { _ = ShowMessage("Startup setting failed", ex.Message); } };
        options.Children.Add(startup);
        AddSettingToggle(options, "Start minimized", App.Configuration.Settings.StartMinimized, value => App.Configuration.Settings.StartMinimized = value);
        AddSettingToggle(options, "Hide in the system tray when closed", App.Configuration.Settings.MinimizeToTray, value => App.Configuration.Settings.MinimizeToTray = value);
        appearanceCard.Child = options; AddTile(tiles, appearanceCard, 0);

        var behaviorCard = Card(); behaviorCard.Padding = new Thickness(20); behaviorCard.Margin = new Thickness(0);
        var behavior = new StackPanel { Spacing = 14 };
        behavior.Children.Add(new TextBlock { Text = "Shortcuts & actions", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
        var shortcuts = new ToggleSwitch { Header = "Enable global shortcuts", IsOn = App.Configuration.Settings.EnableAllShortcuts };
        shortcuts.Toggled += (_, _) => { App.Configuration.Settings.EnableAllShortcuts = shortcuts.IsOn; SaveConfiguration(); RefreshHotkeys(); };
        behavior.Children.Add(shortcuts);
        AddSettingToggle(behavior, "Show notifications after an action", App.Configuration.Settings.ShowLaunchNotifications, value => App.Configuration.Settings.ShowLaunchNotifications = value);
        AddSettingToggle(behavior, "Prevent duplicate launches", App.Configuration.Settings.PreventDuplicateLaunches, value => App.Configuration.Settings.PreventDuplicateLaunches = value);
        behaviorCard.Child = behavior; AddTile(tiles, behaviorCard, 1);

        var backupCard = Card(); backupCard.Padding = new Thickness(20); backupCard.Margin = new Thickness(0);
        var backupContent = new StackPanel { Spacing = 12 };
        backupContent.Children.Add(new TextBlock { Text = "Backup & restore", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        backupContent.Children.Add(new TextBlock { Text = "Keep a copy of your shortcuts and workspace setup.", Opacity = .68, FontSize = 12 });
        var backup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var export = new Button { Content = "Export backup…" }; export.Click += async (_, _) => await ExportBackup();
        var restore = new Button { Content = "Restore backup…" }; restore.Click += async (_, _) => await ImportBackup();
        backup.Children.Add(export); backup.Children.Add(restore); backupContent.Children.Add(backup);
        backupCard.Child = backupContent; Grid.SetColumnSpan(backupCard, 2); AddTile(tiles, backupCard, 2);
        page.Children.Add(tiles); PageHost.Children.Add(PageScroll(page));
    }

    private void AddSettingToggle(Panel parent, string label, bool value, Action<bool> update)
    {
        var toggle = new ToggleSwitch { Header = label, IsOn = value };
        toggle.Toggled += (_, _) => { update(toggle.IsOn); SaveConfiguration(); };
        parent.Children.Add(toggle);
    }

    private async Task ExportBackup()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        InitializeWithWindow.Initialize(picker, _hwnd);
        picker.SuggestedFileName = "QuickMacro-backup"; picker.FileTypeChoices.Add("QuickMacro backup", new List<string> { ".json" });
        var file = await picker.PickSaveFileAsync();
        if (file is not null) { App.Repository.Export(file.Path, App.Configuration); await ShowMessage("Backup saved", file.Path); }
    }

    private async Task ImportBackup()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _hwnd); picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        try
        {
            var imported = App.Repository.Import(file.Path);
            if (imported is not null) { App.Configuration = imported; SaveConfiguration(); ApplyTheme(); RefreshHotkeys(); ShowShortcuts(); }
        }
        catch (Exception ex) { await ShowMessage("Backup could not be restored", ex.Message); }
    }

    private async Task RunEntry(LauncherEntry entry)
    {
        try { await App.Launcher.LaunchAsync(entry, App.Configuration.Settings.PreventDuplicateLaunches); }
        catch (Exception ex) { await ShowMessage("Could not run shortcut", ex.Message); }
    }

    private async Task RunWorkspace(Workspace workspace)
    {
        var errors = await App.Workspaces.LaunchAsync(workspace, App.Configuration.Entries);
        if (errors.Count > 0) await ShowMessage($"Workspace {workspace.Name}", string.Join(Environment.NewLine, errors));
    }

    private void OnHotkey(Guid id)
    {
        var entry = App.Configuration.Entries.FirstOrDefault(value => value.Id == id);
        if (entry is not null) { _ = RunEntry(entry); return; }
        var workspace = App.Configuration.Workspaces.FirstOrDefault(value => value.Id == id);
        if (workspace is not null) _ = RunWorkspaceShortcut(workspace);
    }

    private async Task RunWorkspaceShortcut(Workspace workspace)
    {
        var errors = await App.Workspaces.HandleShortcutAsync(workspace, App.Configuration.Entries);
        if (errors.Count > 0) await ShowMessage($"Workspace {workspace.Name}", string.Join(Environment.NewLine, errors));
    }

    private void RefreshHotkeys()
    {
        IEnumerable<(Guid Id, ShortcutBinding Binding)> assignments = App.Configuration.Settings.EnableAllShortcuts
            ? App.Configuration.Entries.Where(entry => entry.Enabled && entry.Shortcut is not null).Select(entry => (entry.Id, entry.Shortcut!))
                .Concat(App.Configuration.Workspaces.Where(item => item.Enabled && item.Shortcut is not null).Select(item => (item.Id, item.Shortcut!)))
            : Enumerable.Empty<(Guid, ShortcutBinding)>();
        _hotkeys.Replace(assignments);
    }

    private void ToggleShortcuts() { App.Configuration.Settings.EnableAllShortcuts = !App.Configuration.Settings.EnableAllShortcuts; SaveConfiguration(); RefreshHotkeys(); }
    private void SaveConfiguration() { try { App.Repository.Save(App.Configuration); } catch (Exception ex) { _ = ShowMessage("Could not save settings", ex.Message); } }
    private void ApplyTheme()
    {
        var theme = App.Configuration.Settings.ThemeMode switch { "Dark" => ElementTheme.Dark, "Light" => ElementTheme.Light, _ => ElementTheme.Default };
        Navigation.RequestedTheme = theme;
        PageHost.RequestedTheme = theme;
        _darkTheme = theme == ElementTheme.Dark || theme == ElementTheme.Default && Navigation.ActualTheme == ElementTheme.Dark;
        ApplyThemeSurfaces();
    }

    private void ApplyThemeSurfaces()
    {
        if (App.Configuration.Settings.ThemeMode == "System") _darkTheme = Navigation.ActualTheme == ElementTheme.Dark;
        Navigation.Background = new SolidColorBrush(_darkTheme ? ColorHelper.FromArgb(255, 13, 19, 29) : Colors.White);
        PageHost.Background = new SolidColorBrush(_darkTheme ? ColorHelper.FromArgb(255, 15, 21, 32) : ColorHelper.FromArgb(255, 246, 248, 251));
    }

    private async Task ShowMessage(string title, string message)
    {
        var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private static StackPanel NewPage(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 14, MaxWidth = 1540, HorizontalAlignment = HorizontalAlignment.Stretch };
        var header = new Grid { ColumnSpacing = 14, Margin = new Thickness(0, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var accent = title.Contains("Workspace", StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(ColorHelper.FromArgb(255, 154, 111, 255)) : title.Contains("Settings", StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(ColorHelper.FromArgb(255, 49, 184, 166)) : new SolidColorBrush(ColorHelper.FromArgb(255, 67, 126, 255));
        header.Children.Add(new Border { Background = accent, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 3, 0, 6) });
        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = subtitle, FontSize = 14, Opacity = .72 });
        Grid.SetColumn(heading, 1); header.Children.Add(heading); panel.Children.Add(header);
        return panel;
    }
    private static Brush TargetBrush(LauncherTargetType type) => type switch
    {
        LauncherTargetType.StoreApp => new SolidColorBrush(ColorHelper.FromArgb(255, 177, 130, 255)),
        LauncherTargetType.WebsiteUrl => new SolidColorBrush(ColorHelper.FromArgb(255, 50, 190, 169)),
        LauncherTargetType.BrowserAppShortcut => new SolidColorBrush(ColorHelper.FromArgb(255, 244, 170, 78)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 91, 150, 255))
    };
    private Grid TileGrid()
    {
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        var columns = _appWindow.Size.Width >= 1300 ? 2 : 1;
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.SizeChanged += (_, args) =>
        {
            var desired = args.NewSize.Width >= 1050 ? 2 : 1;
            if (desired == columns) return;
            columns = desired;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var children = grid.Children.OfType<FrameworkElement>().ToArray();
            for (var i = 0; i < children.Length; i++)
            {
                var row = i / columns;
                while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(children[i], row);
                Grid.SetColumn(children[i], i % columns);
            }
        };
        return grid;
    }
    private static void AddTile(Grid grid, FrameworkElement element, int index)
    {
        if (element is Border card && card.Shadow is Microsoft.UI.Xaml.Media.ThemeShadow shadow) shadow.Receivers.Add(grid);
        var columns = Math.Max(1, grid.ColumnDefinitions.Count);
        var row = index / columns;
        while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(element, row); Grid.SetColumn(element, index % columns); grid.Children.Add(element);
    }
    private Border Card() => new() { Background = new SolidColorBrush(_darkTheme ? ColorHelper.FromArgb(255, 27, 36, 50) : Colors.White), BorderBrush = new SolidColorBrush(_darkTheme ? ColorHelper.FromArgb(255, 48, 62, 82) : ColorHelper.FromArgb(255, 226, 231, 239)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12), Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow(), Translation = new Vector3(0, 0, 6) };
    private static ScrollViewer PageScroll(FrameworkElement page) => new() { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 18, 0) };
    private Border InfoCard(string title, string body) { var card = Card(); var stack = new StackPanel { Spacing = 7 }; stack.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }); stack.Children.Add(new TextBlock { Text = body, Opacity = .7 }); card.Child = stack; return card; }
    private static Button PrimaryButton(string text) => new() { Content = text, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"], HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 7, 16, 7) };
    private static string ActionLabel(LauncherEntry entry) => entry.ShortcutAction switch { ShortcutAction.Minimize when entry.MinimizeToSystemTray => "Minimize to tray", ShortcutAction.Minimize => "Minimize / restore", ShortcutAction.Close => "Close if open", _ => "Launch app" };
    private sealed record Choice<T>(string Label, T Value) { public override string ToString() => Label; }

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private sealed class HotkeyWindow : IDisposable
    {
        private const uint WmHotkey = 0x0312;
        private const uint ModAlt = 1, ModControl = 2, ModShift = 4, ModWindows = 8;
        private const nuint SubclassId = 0x514D;
        private readonly IntPtr _hwnd;
        private readonly Action<Guid> _invoke;
        private readonly SubclassProc _proc;
        private readonly Dictionary<int, Guid> _ids = [];
        private int _nextId = 0x5100;
        private bool _disposed;

        public HotkeyWindow(IntPtr hwnd, Action<Guid> invoke)
        {
            _hwnd = hwnd; _invoke = invoke; _proc = WindowProc;
            if (!SetWindowSubclass(hwnd, _proc, SubclassId, IntPtr.Zero)) throw new InvalidOperationException("Could not attach QuickMacro keyboard shortcuts to the window.");
        }

        public void Replace(IEnumerable<(Guid Id, ShortcutBinding Binding)> assignments)
        {
            foreach (var id in _ids.Keys.ToArray()) UnregisterHotKey(_hwnd, id);
            _ids.Clear();
            foreach (var (entityId, binding) in assignments)
            {
                var modifiers = (binding.Alt ? ModAlt : 0) | (binding.Control ? ModControl : 0) | (binding.Shift ? ModShift : 0) | (binding.Windows ? ModWindows : 0);
                var id = _nextId++;
                if (RegisterHotKey(_hwnd, id, modifiers, (uint)binding.Key)) _ids[id] = entityId;
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr subclass, IntPtr data)
        {
            if (message == WmHotkey && _ids.TryGetValue(unchecked((int)wParam.ToUInt64()), out var entityId)) _invoke(entityId);
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var id in _ids.Keys) UnregisterHotKey(_hwnd, id);
            _ids.Clear(); RemoveWindowSubclass(_hwnd, _proc, SubclassId);
        }

        private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr subclass, IntPtr data);
        [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, IntPtr data);
        [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
