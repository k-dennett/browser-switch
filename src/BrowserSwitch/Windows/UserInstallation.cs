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
        public static bool HasInstalledCopy { get { return File.Exists(Path.Combine(SettingsStore.InstallDirectory, "BrowserSwitch.exe")); } }
        public static void Install()
        {
            RequireUnpackaged();
            ResidentSession session = ResidentSession.Current;
            using (System.Threading.Mutex update = new System.Threading.Mutex(false, session.Name("Install")))
            {
                bool held = false;
                try {
                    try { held = update.WaitOne(0); }
                    catch (System.Threading.AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("Ya se está instalando BrowserSwitch. Espera a que termine.");
                    InstallCore(session);
                }
                finally { if (held) update.ReleaseMutex(); }
            }
        }
        static void InstallCore(ResidentSession session)
        {
            string install = SettingsStore.InstallDirectory;
            if (install.StartsWith(@"\\", StringComparison.Ordinal) || !Path.IsPathRooted(install)) throw new InvalidOperationException("LOCALAPPDATA debe estar en una unidad local de Windows.");
            string source = Assembly.GetExecutingAssembly().Location;
            string exe = Path.Combine(install, "BrowserSwitch.exe");
            string repo = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)));
            if (repo == null || !File.Exists(Path.Combine(repo, @"src\BrowserSwitch\BrowserSwitch.csproj"))) repo = null;
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
            string work = Path.Combine(data, "Updates", Guid.NewGuid().ToString("N"));
            using (InstallationFiles files = new InstallationFiles(work))
            {
                string staged = Path.Combine(work, "BrowserSwitch.exe");
                File.Copy(source, staged);
                File.Copy(source + ".config", staged + ".config");
                // Reject a broken download before touching the running installation.
                Run(staged, "--diagnostics", Path.Combine(work, "staging-check.xml"));
                RegistrationBackup registration = new RegistrationBackup(WindowsRegistration.AppId, WindowsRegistration.ProductName);
                bool changed = false, wasRunning = false, started = false;
                try
                {
                    wasRunning = session.Stop(exe);
                    changed = true;
                    if (!source.Equals(exe, StringComparison.OrdinalIgnoreCase))
                    {
                        files.Protect(exe, true); files.Protect(exe + ".config", true);
                        files.Replace(staged, exe);
                        files.Replace(staged + ".config", exe + ".config");
                    }
                    // The exact stable executable must pass before registration changes.
                    Run(exe, "--diagnostics", Path.Combine(data, "install-check.xml"));
                    if (!File.Exists(SettingsStore.DefaultPath))
                    {
                        List<string> old = CommandLine.Split(snapshot.Command);
                        string preference = old.Count > 0 ? Path.Combine(Path.GetDirectoryName(old[0]), "preferencia.txt") : null;
                        if ((preference == null || !File.Exists(preference)) && repo != null) preference = Path.Combine(repo, @"SelectorNavegador\preferencia.txt");
                        if (preference != null && File.Exists(preference)) Run(exe, "--migrate-preference", preference);
                    }
                    string iconPath = Path.Combine(install, "BrowserSwitch.ico");
                    files.Protect(iconPath, false); AppIcon.Save(iconPath);
                    Run(exe, "--register");
                    CreateShortcuts(exe, install, repo, migration, iconPath, files);
                    Run(exe, "--diagnostics", Path.Combine(data, "install-check.xml"));
                    session.Start(exe, "--tray");
                    started = true;
                    files.Commit();
                }
                catch (Exception failure)
                {
                    if (!changed) throw;
                    List<Exception> recovery = new List<Exception>();
                    if (started) try { session.Stop(exe); } catch (Exception ex) { recovery.Add(ex); }
                    try { files.Rollback(); } catch (Exception ex) { recovery.Add(ex); }
                    try { registration.Restore(); } catch (Exception ex) { recovery.Add(ex); }
                    if (recovery.Count == 0 && wasRunning)
                        try { RestartPrevious(session, exe); } catch (Exception ex) { recovery.Add(ex); }
                    if (recovery.Count > 0)
                    {
                        recovery.Insert(0, failure);
                        throw new IOException("La actualización falló y la recuperación necesita revisión. Copias conservadas en: " + work, new AggregateException(recovery));
                    }
                    files.CompleteRecovery();
                    throw new IOException("No se pudo completar la actualización. Se recuperaron los archivos y el registro anteriores. " + failure.Message, failure);
                }
            }
        }
        static void RestartPrevious(ResidentSession session, string exe)
        {
            if (new Version(FileVersionInfo.GetVersionInfo(exe).FileVersion) >= new Version(1, 1, 0, 0)) session.Start(exe, "--tray");
            else using (Process process = Process.Start(new ProcessStartInfo(exe, "--tray") { UseShellExecute = false, CreateNoWindow = true }))
                if (process.WaitForExit(1000)) throw new IOException("Se recuperó la versión anterior, pero no se pudo reiniciar su bandeja.");
        }
        static void CreateShortcuts(string exe, string install, string repo, string migration, string iconPath, InstallationFiles files)
        {
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
                    files.Protect(path, false);
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