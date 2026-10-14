using Microsoft.UI.Xaml;
using QuickMacro.Models;
using QuickMacro.Services;
using System;
using System.Threading;

namespace QuickMacro.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Mutex? _mutex;
    public static ConfigurationRepository Repository { get; } = new();
    public static AppConfiguration Configuration { get; set; } = new();
    public static StartupService Startup { get; } = new();
    public static LauncherService Launcher { get; } = new();
    public static WorkspaceService Workspaces { get; } = new(Launcher);
    public static string? LoadWarning { get; private set; }
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        _mutex = new Mutex(true, @"Local\QuickMacro.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Environment.Exit(0);
            return;
        }
        var load = Repository.Load();
        Configuration = load.Configuration;
        LoadWarning = load.Warning;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow ??= new MainWindow();
        MainWindow.Activate();
    }
}
