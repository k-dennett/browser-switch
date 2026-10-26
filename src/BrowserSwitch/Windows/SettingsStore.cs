using System;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using BrowserSwitch.Core;

namespace BrowserSwitch.Windows
{
    public sealed class SettingsStore
    {
        public static readonly string InstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrowserSwitch");
        public static readonly string DefaultPath = Path.Combine(InstallDirectory, "Data", "settings.xml");
        readonly string path;
        public string Warning { get; private set; }
        public SettingsStore() : this(DefaultPath) { }
        public SettingsStore(string filePath) { path = filePath; }
        public string FilePath { get { return path; } }

        T Locked<T>(Func<T> action)
        {
            string sid = WindowsIdentity.GetCurrent().User.Value;
            using (Mutex mutex = new Mutex(false, @"Local\BrowserSwitch.Settings." + sid))
            {
                bool held = false;
                try
                {
                    try { held = mutex.WaitOne(10000); }
                    catch (AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("Otra ventana está guardando la configuración. Vuelve a intentarlo.");
                    return action();
                }
                finally { if (held) mutex.ReleaseMutex(); }
            }
        }
        public BrowserSettings Load()
        {
            return Locked(delegate
            {
                if (!File.Exists(path)) return new BrowserSettings();
                try
                {
                    using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    {
                        BrowserSettings settings = (BrowserSettings)new XmlSerializer(typeof(BrowserSettings)).Deserialize(reader);
                        if (settings.ManualBrowsers == null) settings.ManualBrowsers = new System.Collections.Generic.List<BrowserDefinition>();
                        if (settings.HiddenBrowserIds == null) settings.HiddenBrowserIds = new System.Collections.Generic.List<string>();
                        foreach (BrowserDefinition browser in settings.ManualBrowsers)
                            if (browser == null || string.IsNullOrEmpty(browser.Id) || string.IsNullOrEmpty(browser.Name) || string.IsNullOrEmpty(browser.ExecutablePath) || !Path.IsPathRooted(browser.ExecutablePath) || browser.Arguments == null)
                                throw new InvalidOperationException("Entrada de navegador incompleta.");
                        return settings;
                    }
                }
                catch (InvalidOperationException) { return Recover(); }
                catch (XmlException) { return Recover(); }
            });
        }
        BrowserSettings Recover()
        {
            string backup = path + ".corrupt-" + Guid.NewGuid().ToString("N");
            File.Copy(path, backup);
            Warning = "La configuración no se pudo leer. Se usará Preguntar siempre. Copia conservada en: " + backup;
            return new BrowserSettings();
        }
        public void Save(BrowserSettings settings)
        {
            Locked(delegate
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        new XmlSerializer(typeof(BrowserSettings)).Serialize(stream, settings);
                        stream.Flush(true);
                    }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                    else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return true;
            });
        }
        public bool ImportLegacy(string preferencePath, System.Collections.Generic.List<BrowserDefinition> detected)
        {
            if (File.Exists(path) || !File.Exists(preferencePath)) return false;
            string preferred = File.ReadAllText(preferencePath).Trim();
            BrowserSettings settings = new BrowserSettings();
            foreach (BrowserDefinition browser in detected)
            {
                string executable = Path.GetFileNameWithoutExtension(browser.ExecutablePath);
                if (string.Equals(preferred, browser.Name, StringComparison.OrdinalIgnoreCase) ||
                    (preferred == "Edge" && executable.Equals("msedge", StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(preferred, executable, StringComparison.OrdinalIgnoreCase))
                { settings.PreferredBrowserId = browser.Id; break; }
            }
            Save(settings);
            return true;
        }
    }
}