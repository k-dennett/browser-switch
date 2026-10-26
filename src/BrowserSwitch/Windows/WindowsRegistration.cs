using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BrowserSwitch.Windows
{
    public static class WindowsRegistration
    {
        // Retained from SelectorNavegador: existing protected UserChoice values stay valid.
        public const string AppId = "SelectorNavegadorPersonal";
        public const string ProductName = "BrowserSwitch";
        const string Client = @"Software\Clients\StartMenuInternet\" + AppId;
        const string Capabilities = Client + @"\Capabilities";
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static void Register()
        {
            UserInstallation.RequireUnpackaged();
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string stable = Path.Combine(SettingsStore.InstallDirectory, "BrowserSwitch.exe");
            if (!Path.GetFullPath(exe).Equals(stable, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Registra la copia local estable con scripts\\Install.ps1. No registres el ejecutable del repositorio.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Client))
            {
                key.SetValue("", ProductName);
                using (RegistryKey cmd = key.CreateSubKey(@"shell\open\command")) cmd.SetValue("", CommandLine.Quote(exe));
                using (RegistryKey icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", CommandLine.Quote(exe) + ",0");
            }
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + AppId))
            {
                key.SetValue("", ProductName);
                key.SetValue("URL Protocol", "");
                key.SetValue("FriendlyTypeName", "Enlace web de BrowserSwitch");
                using (RegistryKey app = key.CreateSubKey("Application"))
                {
                    app.SetValue("ApplicationName", ProductName);
                    app.SetValue("ApplicationDescription", "Elige dónde abrir cada enlace web.");
                    app.SetValue("ApplicationIcon", CommandLine.Quote(exe) + ",0");
                }
                using (RegistryKey cmd = key.CreateSubKey(@"shell\open\command")) cmd.SetValue("", CommandLine.Quote(exe) + " \"%1\"");
                using (RegistryKey icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", CommandLine.Quote(exe) + ",0");
            }
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Capabilities))
            {
                key.SetValue("ApplicationName", ProductName);
                key.SetValue("ApplicationDescription", "Elige un navegador o recuerda uno para tus próximos enlaces.");
                key.SetValue("ApplicationIcon", CommandLine.Quote(exe) + ",0");
                using (RegistryKey menu = key.CreateSubKey("StartMenu")) menu.SetValue("StartMenuInternet", AppId);
                using (RegistryKey files = key.CreateSubKey("FileAssociations"))
                { files.SetValue(".htm", AppId); files.SetValue(".html", AppId); }
                using (RegistryKey urls = key.CreateSubKey("URLAssociations"))
                { urls.SetValue("http", AppId); urls.SetValue("https", AppId); }
            }
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                // Remove only aliases that actually reference our own capabilities.
                foreach (string name in key.GetValueNames())
                    if (string.Equals(key.GetValue(name) as string, Capabilities, StringComparison.OrdinalIgnoreCase)) key.DeleteValue(name, false);
                key.SetValue(ProductName, Capabilities);
            }
            SHChangeNotify(0x08000000, 0x1003, IntPtr.Zero, IntPtr.Zero);
        }
        public static bool IsDefault(string scheme)
        {
            string path = scheme.StartsWith(".", StringComparison.Ordinal) ?
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + scheme + @"\UserChoice" :
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\" + scheme + @"\UserChoice";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                return key != null && string.Equals(key.GetValue("ProgId") as string, AppId, StringComparison.OrdinalIgnoreCase);
        }
        public static void OpenDefaults()
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString(ProductName)) { UseShellExecute = true });
        }
        public static void Unregister()
        {
            UserInstallation.RequireUnpackaged();
            foreach (string type in new string[] { "http", "https", ".htm", ".html" })
                if (IsDefault(type)) throw new InvalidOperationException("Antes de desinstalar, elige otro navegador para HTTP, HTTPS, .htm y .html desde Aplicaciones predeterminadas de Windows.");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
                if (key != null) foreach (string name in key.GetValueNames())
                    if (string.Equals(key.GetValue(name) as string, Capabilities, StringComparison.OrdinalIgnoreCase)) key.DeleteValue(name, false);
            StartupRegistration.SetEnabled(false);
            Registry.CurrentUser.DeleteSubKeyTree(Client, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + AppId, false);
            SHChangeNotify(0x08000000, 0x1003, IntPtr.Zero, IntPtr.Zero);
        }
    }
}