using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace BrowserSwitch.Windows
{
    public sealed class RegistrationSnapshot
    {
        public string Command { get; set; }
        public List<RegistrationValue> Values { get; set; }
        public RegistrationSnapshot() { Values = new List<RegistrationValue>(); }
    }
    public sealed class RegistrationValue
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Kind { get; set; }
    }
    public static class UserInstallation
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetCurrentPackageFullName(ref uint length, StringBuilder name);
        [DllImport("shell32.dll", EntryPoint = "SHChangeNotify", CharSet = CharSet.Unicode)]
        static extern void NotifyItemChanged(uint events, uint flags, string path, IntPtr unused);
        public static void RequireUnpackaged()
        {
            uint length = 0;
            if (GetCurrentPackageFullName(ref length, null) != 15700)
                throw new InvalidOperationException("Este proceso está empaquetado y puede virtualizar LOCALAPPDATA. Abre BrowserSwitch.exe desde el Explorador de Windows para instalar o registrar la copia real.");
        }
        public static bool IsStable
        {
            get { return Assembly.GetExecutingAssembly().Location.Equals(Path.Combine(SettingsStore.InstallDirectory, "BrowserSwitch.exe"), StringComparison.OrdinalIgnoreCase); }
        }
        public static void Install()
        {
            RequireUnpackaged();
            string install = SettingsStore.InstallDirectory;
            if (install.StartsWith(@"\\", StringComparison.Ordinal) || !Path.IsPathRooted(install)) throw new InvalidOperationException("LOCALAPPDATA debe estar en una unidad local de Windows.");
            string source = Assembly.GetExecutingAssembly().Location;
            string exe = Path.Combine(install, "BrowserSwitch.exe");
            string repo = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)));
            if (repo == null || !File.Exists(Path.Combine(repo, @"src\BrowserSwitch\BrowserSwitch.csproj"))) repo = null;
            foreach (Process process in Process.GetProcessesByName("BrowserSwitch"))
                using (process)
                    if (process.Id != Process.GetCurrentProcess().Id && string.Equals(process.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Cierra las otras ventanas de BrowserSwitch antes de actualizar.");
            Directory.CreateDirectory(install);
            string data = Path.Combine(install, "Data");
            string migration = Path.Combine(data, "Migration");
            Directory.CreateDirectory(migration);
            string snapshotPath = Path.Combine(migration, "registration-before.xml");
            RegistrationSnapshot snapshot;
            if (File.Exists(snapshotPath))
            {
                using (StreamReader reader = new StreamReader(snapshotPath)) snapshot = (RegistrationSnapshot)new XmlSerializer(typeof(RegistrationSnapshot)).Deserialize(reader);
            }
            else
            {
                snapshot = new RegistrationSnapshot();
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + WindowsRegistration.AppId + @"\shell\open\command")) snapshot.Command = key == null ? null : key.GetValue("") as string;
                SnapshotKey(snapshot, @"Software\Classes\" + WindowsRegistration.AppId);
                SnapshotKey(snapshot, @"Software\Clients\StartMenuInternet\" + WindowsRegistration.AppId);
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications"))
                    if (key != null) foreach (string name in key.GetValueNames())
                        if (string.Equals(key.GetValue(name) as string, @"Software\Clients\StartMenuInternet\" + WindowsRegistration.AppId + @"\Capabilities", StringComparison.OrdinalIgnoreCase))
                            snapshot.Values.Add(new RegistrationValue { Key = @"Software\RegisteredApplications", Name = name, Value = key.GetValue(name) as string, Kind = "String" });
                using (StreamWriter writer = new StreamWriter(snapshotPath)) new XmlSerializer(typeof(RegistrationSnapshot)).Serialize(writer, snapshot);
            }
            if (!source.Equals(exe, StringComparison.OrdinalIgnoreCase))
            {
                string staged = Path.Combine(install, "BrowserSwitch.new.exe");
                File.Copy(source, staged, true);
                File.Copy(source + ".config", staged + ".config", true);
                Run(staged, "--diagnostics", Path.Combine(data, "staging-check.xml"));
                if (File.Exists(exe + ".config")) File.Copy(exe + ".config", exe + ".config.previous", true);
                if (File.Exists(exe)) File.Replace(staged, exe, exe + ".previous");
                else File.Move(staged, exe);
                File.Copy(staged + ".config", exe + ".config", true);
                File.Delete(staged + ".config");
            }
            // This exact executable must start successfully before registry or shortcut changes.
            Run(exe, "--diagnostics", Path.Combine(data, "install-check.xml"));
            if (!File.Exists(SettingsStore.DefaultPath))
            {
                List<string> old = CommandLine.Split(snapshot.Command);
                string preference = old.Count > 0 ? Path.Combine(Path.GetDirectoryName(old[0]), "preferencia.txt") : null;
                if ((preference == null || !File.Exists(preference)) && repo != null) preference = Path.Combine(repo, @"SelectorNavegador\preferencia.txt");
                if (preference != null && File.Exists(preference)) Run(exe, "--migrate-preference", preference);
            }
            string iconPath = Path.Combine(install, "BrowserSwitch.ico");
            AppIcon.Save(iconPath);
            Run(exe, "--register");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            List<string> shortcuts = new List<string> { Path.Combine(desktop, "BrowserSwitch.lnk"), Path.Combine(programs, "BrowserSwitch.lnk") };
            List<string> legacy = new List<string> { Path.Combine(desktop, "Cambiar navegador.lnk") };
            if (repo != null) legacy.Add(Path.Combine(repo, "Cambiar navegador.lnk"));
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                foreach (string path in legacy)
                    if (File.Exists(path))
                    {
                        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                        try
                        {
                            string target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                            string name = Path.GetFileName(target);
                            if (name.Equals("SelectorNavegador.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("BrowserSwitch.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                string backupName = Path.Combine(migration, path.StartsWith(desktop, StringComparison.OrdinalIgnoreCase) ? "desktop-shortcut.lnk" : "repo-shortcut.lnk");
                                if (!File.Exists(backupName)) File.Copy(path, backupName);
                                shortcuts.Add(path);
                            }
                        }
                        finally { Marshal.FinalReleaseComObject(shortcut); }
                    }
                foreach (string path in shortcuts)
                {
                    object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                    try
                    {
                        SetShortcut(shortcut, "TargetPath", exe); SetShortcut(shortcut, "Arguments", "");
                        SetShortcut(shortcut, "WorkingDirectory", install); SetShortcut(shortcut, "IconLocation", iconPath + ",0");
                        SetShortcut(shortcut, "Description", "BrowserSwitch: cambiar navegador o preguntar siempre");
                        shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                        NotifyItemChanged(0x00002000, 0x0005, path, IntPtr.Zero); // SHCNE_UPDATEITEM + SHCNF_PATHW: refresh this shortcut only.
                    }
                    finally { Marshal.FinalReleaseComObject(shortcut); }
                }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
            Run(exe, "--diagnostics", Path.Combine(data, "install-check.xml"));
        }
        static void SetShortcut(object shortcut, string property, string value)
        {
            shortcut.GetType().InvokeMember(property, BindingFlags.SetProperty, null, shortcut, new object[] { value });
        }
        static void SnapshotKey(RegistrationSnapshot snapshot, string path)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
            {
                if (key == null) return;
                foreach (string name in key.GetValueNames()) snapshot.Values.Add(new RegistrationValue { Key = path, Name = name, Value = Convert.ToString(key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames)), Kind = key.GetValueKind(name).ToString() });
                foreach (string name in key.GetSubKeyNames()) SnapshotKey(snapshot, path + "\\" + name);
            }
        }
        static void Run(string exe, params string[] arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo(exe, CommandLine.Join(arguments)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe) };
            using (Process process = Process.Start(start))
            {
                System.Threading.Tasks.Task<string> error = System.Threading.Tasks.Task.Factory.StartNew(delegate { return process.StandardError.ReadToEnd(); });
                if (!process.WaitForExit(20000)) { process.Kill(); throw new IOException("La comprobación de BrowserSwitch no terminó en 20 segundos."); }
                if (process.ExitCode != 0) throw new IOException("BrowserSwitch: " + error.Result);
            }
        }
    }
}