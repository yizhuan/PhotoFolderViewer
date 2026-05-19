using System.IO;
using System.Windows;

namespace PhotoFolderViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupTarget = ResolveStartupTarget(e.Args);
        var window = new MainWindow(startupTarget);
        MainWindow = window;
        window.Show();
    }

    private static string? ResolveStartupTarget(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (File.Exists(arg) || Directory.Exists(arg))
            {
                return arg;
            }
        }

        return null;
    }
}
