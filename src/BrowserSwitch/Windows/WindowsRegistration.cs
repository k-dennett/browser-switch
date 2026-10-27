using System;
using System.Diagnostics;
using System.Collections.Generic;
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
    // Only BrowserSwitch's own registration is captured; UserChoice is never written.
    internal sealed class RegistrationBackup
    {
        sealed class Value { internal string Name; internal object Data; internal RegistryValueKind Kind; }
        sealed class Key { internal string Path; internal List<Value> Values = new List<Value>(); }
        readonly List<Key> keys = new List<Key>();
        readonly List<Value> aliases = new List<Value>();
        readonly string classes, client, capabilities, product;
        internal RegistrationBackup(string appId, string product)
        {
            this.product = product;
            classes = @"Software\Classes\" + appId;
            client = @"Software\Clients\StartMenuInternet\" + appId;
            capabilities = client + @"\Capabilities";
            Capture(classes); Capture(client);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications"))
                if (key != null) foreach (string name in key.GetValueNames())
                    if (name.Equals(product, StringComparison.OrdinalIgnoreCase) || IsOwnAlias(key.GetValue(name))) aliases.Add(Read(key, name));
        }
        bool IsOwnAlias(object value) { return string.Equals(value as string, capabilities, StringComparison.OrdinalIgnoreCase); }
        static Value Read(RegistryKey key, string name)
        {
            return new Value { Name = name, Data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), Kind = key.GetValueKind(name) };
        }
        void Capture(string path)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
            {
                if (key == null) return;
                Key copy = new Key { Path = path };
                foreach (string name in key.GetValueNames()) copy.Values.Add(Read(key, name));
                keys.Add(copy);
                foreach (string child in key.GetSubKeyNames()) Capture(path + "\\" + child);
            }
        }
        internal void Restore()
        {
            Registry.CurrentUser.DeleteSubKeyTree(classes, false);
            Registry.CurrentUser.DeleteSubKeyTree(client, false);
            foreach (Key copy in keys)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(copy.Path))
                    foreach (Value value in copy.Values) key.SetValue(value.Name, value.Data, value.Kind);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                foreach (string name in key.GetValueNames())
                    if (name.Equals(product, StringComparison.OrdinalIgnoreCase) || IsOwnAlias(key.GetValue(name))) key.DeleteValue(name, false);
                foreach (Value value in aliases) key.SetValue(value.Name, value.Data, value.Kind);
            }
        }
    }}