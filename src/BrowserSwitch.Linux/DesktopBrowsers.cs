#nullable enable
using System.Diagnostics;
using System.Runtime.InteropServices;
using BrowserSwitch.Core;

namespace BrowserSwitch.Linux;

internal static class DesktopBrowsers
{
    internal static readonly (string Id, string Name, string[] Commands)[] Supported =
    [
        ("firefox", "Firefox", ["firefox"]),
        ("chrome", "Google Chrome", ["google-chrome-stable", "google-chrome"]),
        ("chromium", "Chromium", ["chromium", "chromium-browser"])
    ];

    internal static List<BrowserDefinition> Detect()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator).Where(Path.IsPathFullyQualified)
            .Append("/snap/bin").Distinct().ToArray();

        var browsers = new List<BrowserDefinition>();
        foreach (var (id, name, commands) in Supported)
        {
            var executable = (from directory in directories
                              from command in commands
                              let path = Path.Combine(directory, command)
                              where File.Exists(path) && access(path, ExecuteOk) == 0
                              select path).FirstOrDefault();
            if (executable != null)
                browsers.Add(new BrowserDefinition { Id = id, Name = name, ExecutablePath = executable });
        }
        return browsers;
    }

    internal static string NormalizeWebLink(string value)
    {
        var target = LinkTarget.Normalize(value);
        var scheme = new Uri(target).Scheme;
        if (scheme is not ("http" or "https"))
            throw new ArgumentException("BrowserSwitch para Linux admite enlaces HTTP y HTTPS.");
        return target;
    }

    internal static void Open(BrowserDefinition browser, string link)
    {
        var start = new ProcessStartInfo(browser.ExecutablePath) { UseShellExecute = false };
        start.ArgumentList.Add(link);
        using var process = Process.Start(start);
    }

    private const int ExecuteOk = 1;

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
    private static extern int access(string path, int mode);
}

internal sealed class Preferences
{
    private readonly string directory;
    private readonly string path;

    internal Preferences()
    {
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var config = xdgConfig != null && Path.IsPathFullyQualified(xdgConfig)
            ? xdgConfig : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        directory = Path.Combine(config, "browser-switch");
        path = Path.Combine(directory, "preferred-browser");
    }

    internal BrowserSettings Load()
    {
        var settings = new BrowserSettings();
        try { settings.PreferredBrowserId = File.ReadAllText(path).Trim(); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        return settings;
    }

    internal void Save(string id)
    {
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Path.GetRandomFileName());
        var file = new FileStream(temporary, FileMode.CreateNew);
        try
        {
            using (file)
            using (var writer = new StreamWriter(file))
                writer.WriteLine(id);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
