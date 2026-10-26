using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using BrowserSwitch.Core;
using BrowserSwitch.Windows;

namespace BrowserSwitch
{
    public sealed class DiagnosticReport
    {
        public string ExecutablePath { get; set; }
        public string SettingsPath { get; set; }
        public bool HttpDefault { get; set; }
        public bool HttpsDefault { get; set; }
        public string PreferredBrowserId { get; set; }
        public List<BrowserDefinition> Browsers { get; set; }
        public List<string> Warnings { get; set; }
    }
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool command = args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal);
            try
            {
                if (args.Length == 1 && args[0] == "--install") { UserInstallation.Install(); return 0; }
                if (args.Length == 1 && args[0] == "--register") { WindowsRegistration.Register(); return 0; }
                if (args.Length == 1 && args[0] == "--unregister") { WindowsRegistration.Unregister(); return 0; }
                if (args.Length == 1 && args[0] == "--defaults") { WindowsRegistration.OpenDefaults(); return 0; }
                BrowserDiscovery discovery = new BrowserDiscovery();
                List<BrowserDefinition> detected = discovery.Detect();
                SettingsStore store = new SettingsStore();
                if (args.Length == 2 && args[0] == "--migrate-preference") { store.ImportLegacy(args[1], detected); return 0; }
                BrowserSettings settings = store.Load();
                List<BrowserDefinition> browsers = BrowserDiscovery.Merge(detected, settings);
                if (args.Length == 2 && args[0] == "--diagnostics")
                {
                    DiagnosticReport report = new DiagnosticReport {
                        ExecutablePath = System.Reflection.Assembly.GetExecutingAssembly().Location,
                        SettingsPath = store.FilePath, HttpDefault = WindowsRegistration.IsDefault("http"), HttpsDefault = WindowsRegistration.IsDefault("https"),
                        PreferredBrowserId = settings.PreferredBrowserId, Browsers = browsers, Warnings = discovery.Warnings
                    };
                    if (store.Warning != null) report.Warnings.Add(store.Warning);
                    using (StreamWriter writer = new StreamWriter(args[1])) new XmlSerializer(typeof(DiagnosticReport)).Serialize(writer, report);
                    return 0;
                }
                bool tray = args.Length == 0 || (args.Length == 1 && args[0] == "--tray");
                bool window = args.Length == 1 && args[0] == "--settings";
                if ((!tray && !window && command) || args.Length > 1) throw new ArgumentException("Uso: BrowserSwitch.exe [enlace | --tray | --settings | --install | --register | --defaults | --unregister | --diagnostics archivo | --migrate-preference archivo]");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (store.Warning != null) MessageBox.Show(store.Warning, WindowsRegistration.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if ((tray || window) && UserInstallation.IsStable) { TrayApplicationContext.Run(args.Length == 0, window); return 0; }
                if (args.Length == 1 && args[0] == "--tray") throw new InvalidOperationException("Instala BrowserSwitch antes de iniciar el modo de bandeja.");
                string link = args.Length == 1 && !window ? LinkTarget.Normalize(args[0]) : null;
                BrowserDefinition preferred = settings.Preferred(browsers);
                if (link != null && preferred != null)
                {
                    try { BrowserLauncher.Open(preferred, link); return 0; }
                    catch (Exception ex) { MessageBox.Show("No se pudo usar el navegador guardado. Elige otro.\n\n" + ex.Message, WindowsRegistration.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
                Application.Run(new SelectorForm(link, store, settings, detected, discovery.Warnings));
                return 0;
            }
            catch (Exception ex)
            {
                if (command) Console.Error.WriteLine(ex.Message);
                else MessageBox.Show(ex.Message, WindowsRegistration.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}