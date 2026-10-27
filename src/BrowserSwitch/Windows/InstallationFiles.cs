using System;
using System.Collections.Generic;
using System.IO;

namespace BrowserSwitch.Windows
{
    // Back up each changed file once. Data/settings.xml is never part of an update.
    internal sealed class InstallationFiles : IDisposable
    {
        sealed class Entry { internal string Path, Backup; internal bool Previous; }
        readonly List<Entry> entries = new List<Entry>();
        readonly string folder;
        bool finished;
        internal InstallationFiles(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }
        internal void Protect(string path, bool keepPrevious)
        {
            foreach (Entry existing in entries) if (string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase)) return;
            if (keepPrevious) Protect(path + ".previous", false);
            Entry entry = new Entry { Path = path, Previous = keepPrevious };
            if (File.Exists(path)) { entry.Backup = System.IO.Path.Combine(folder, entries.Count + ".bak"); File.Copy(path, entry.Backup); }
            entries.Add(entry);
        }
        internal void Replace(string source, string destination)
        {
            Protect(destination, false);
            string incoming = destination + ".update-" + Guid.NewGuid().ToString("N");
            try {
                File.Copy(source, incoming);
                if (File.Exists(destination)) File.Replace(incoming, destination, null);
                else File.Move(incoming, destination);
            }
            finally { if (File.Exists(incoming)) File.Delete(incoming); }
        }
        internal void Commit()
        {
            foreach (Entry entry in entries)
                if (entry.Previous && entry.Backup != null) File.Copy(entry.Backup, entry.Path + ".previous", true);
            finished = true;
        }
        internal void Rollback()
        {
            List<Exception> failures = new List<Exception>();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                try {
                    if (entry.Backup == null) { if (File.Exists(entry.Path)) File.Delete(entry.Path); }
                    else File.Copy(entry.Backup, entry.Path, true);
                }
                catch (Exception ex) { failures.Add(ex); }
            }
            if (failures.Count > 0) throw new IOException("No se pudo recuperar algún archivo. Se conservan las copias en: " + folder, new AggregateException(failures));
        }
        internal void CompleteRecovery() { finished = true; }
        public void Dispose()
        {
            if (!finished) return; // Keep recovery copies if rollback itself failed.
            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}