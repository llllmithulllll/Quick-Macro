using Microsoft.Win32;
using QuickMacro.Services;
using QuickMacro.UI;

namespace QuickMacro;

internal static class Program
{
    private const string MutexName = @"Local\QuickMacro.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        var repository = new ConfigurationRepository();
        var load = repository.Load();
        var config = load.Configuration;
        using var startup = new StartupService();
        using var form = new MainForm(repository, config, startup, load.Warning);
        Application.Run(form);
    }
}
