using System;
using System.IO;
using Microsoft.Win32;

namespace BrowserSwitch.Windows
{
    public static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static string Command
        {
            get { return CommandLine.Quote(Path.Combine(SettingsStore.InstallDirectory, "BrowserSwitch.exe")) + " --tray"; }
        }
        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && string.Equals(key.GetValue("BrowserSwitch") as string, Command, StringComparison.OrdinalIgnoreCase);
            }
        }
        public static void SetEnabled(bool enabled)
        {
            UserInstallation.RequireUnpackaged();
            if (enabled && !UserInstallation.IsStable) throw new InvalidOperationException("Instala BrowserSwitch antes de activar el inicio con Windows.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("BrowserSwitch", Command);
                else if (string.Equals(key.GetValue("BrowserSwitch") as string, Command, StringComparison.OrdinalIgnoreCase)) key.DeleteValue("BrowserSwitch", false);
            }
        }
    }
}